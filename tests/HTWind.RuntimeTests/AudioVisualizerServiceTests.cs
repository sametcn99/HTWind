using System.Text.Json;

using HTWind.Services.WebView;

using NAudio.Wave;

using Xunit;

namespace HTWind.RuntimeTests;

public sealed class AudioVisualizerServiceTests
{
    [Fact]
    public void Snapshot_StartsCaptureAndReportsSilentFloatPcm()
    {
        using var fixture = new Fixture();
        var snapshot = fixture.Snapshot();

        Assert.True(snapshot.Available);
        Assert.True(snapshot.IsSilent);
        Assert.Equal(0, snapshot.PeakLevel);
        Assert.Equal(0, snapshot.RmsLevel);
        Assert.Equal(16, snapshot.Bands.Length);
        Assert.Equal(1, fixture.Capture.StartCount);

        fixture.Capture.Emit(Samples(2048, (_, _) => 0f));
        snapshot = fixture.Snapshot();
        Assert.Equal(0, snapshot.PeakLevel);
        Assert.Equal(0, snapshot.RmsLevel);
    }

    [Fact]
    public void Snapshot_ComputesPeakAndRmsFromConstantAmplitude()
    {
        using var fixture = new Fixture();
        fixture.Snapshot();
        fixture.Capture.Emit(Samples(256, (_, _) => 0.5f));

        var snapshot = fixture.Snapshot();

        Assert.InRange(snapshot.PeakLevel, 0.499, 0.501);
        Assert.InRange(snapshot.RmsLevel, 0.499, 0.501);
        Assert.False(snapshot.IsSilent);
    }

    [Fact]
    public void Snapshot_UsesPerChannelPeakAndMonoMixForRms()
    {
        using var fixture = new Fixture();
        fixture.Snapshot();
        fixture.Capture.Emit(Samples(256, (_, channel) => channel == 0 ? 0.8f : -0.8f));

        var snapshot = fixture.Snapshot();

        Assert.InRange(snapshot.PeakLevel, 0.799, 0.801);
        Assert.Equal(0, snapshot.RmsLevel);
        Assert.False(snapshot.IsSilent);
    }

    [Fact]
    public void Snapshot_ToneProducesFiniteBoundedFftBands()
    {
        using var fixture = new Fixture();
        fixture.Snapshot();
        fixture.Capture.Emit(Samples(2048, (frame, _) => 0.7f * MathF.Sin(2 * MathF.PI * 1000 * frame / 48000)));

        var snapshot = fixture.Snapshot(24);

        Assert.Equal(24, snapshot.Bands.Length);
        Assert.Contains(snapshot.Bands, band => band > 0.01);
        Assert.All(snapshot.Bands, band => Assert.InRange(band, 0, 1));
    }

    [Fact]
    public void Snapshot_HandlesShortAndEmptyFloatBuffers()
    {
        using var fixture = new Fixture();
        fixture.Snapshot();
        fixture.Capture.Emit(Samples(1, (_, _) => 0.25f));
        var shortSnapshot = fixture.Snapshot();
        Assert.InRange(shortSnapshot.PeakLevel, 0.249, 0.251);
        Assert.All(shortSnapshot.Bands, band => Assert.Equal(0, band));

        fixture.Capture.Emit([]);
        var afterEmpty = fixture.Snapshot();
        Assert.Equal(shortSnapshot.PeakLevel, afterEmpty.PeakLevel);
    }

    [Fact]
    public void Snapshot_IgnoresMalformedPartialFramesAndNonFiniteSamples()
    {
        using var fixture = new Fixture();
        fixture.Snapshot();
        fixture.Capture.EmitRaw(BitConverter.GetBytes(0.9f).Concat(new byte[] { 1, 2, 3 }).ToArray(), 7);
        var malformed = fixture.Snapshot();
        Assert.Equal(0, malformed.PeakLevel);
        Assert.Equal(0, malformed.RmsLevel);

        fixture.Capture.Emit(Samples(8, (frame, channel) => frame == 0 && channel == 0 ? float.NaN : float.PositiveInfinity));
        var nonFinite = fixture.Snapshot();
        Assert.Equal(0, nonFinite.PeakLevel);
        Assert.Equal(0, nonFinite.RmsLevel);
    }

    [Theory]
    [InlineData(null, 16)]
    [InlineData("{}", 16)]
    [InlineData("{\"bands\":\"bad\"}", 16)]
    [InlineData("{\"bands\":2}", 4)]
    [InlineData("{\"bands\":100}", 64)]
    [InlineData("{\"bands\":\"7\"}", 7)]
    public void Snapshot_NormalizesRequestedBandCount(string? json, int expected)
    {
        using var fixture = new Fixture();
        var args = json is null ? null : Parse(json);

        var snapshot = Read(fixture.Service.GetSnapshot(args));

        Assert.Equal(expected, snapshot.Bands.Length);
        Assert.Equal(expected, snapshot.BandCount);
    }

    [Fact]
    public void Snapshot_DecaysStaleLevelsAndEventuallyZerosNearSilence()
    {
        using var fixture = new Fixture();
        fixture.Snapshot();
        fixture.Capture.Emit(Samples(2048, (_, _) => 0.5f));
        Assert.InRange(fixture.Snapshot().PeakLevel, 0.499, 0.501);

        fixture.Clock.Advance(TimeSpan.FromMilliseconds(501));
        var decayed = fixture.Snapshot();
        Assert.InRange(decayed.PeakLevel, 0.2999, 0.3001);
        Assert.InRange(decayed.RmsLevel, 0.2999, 0.3001);
        Assert.False(decayed.IsSilent);

        for (var i = 0; i < 10; i++)
        {
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(501));
            decayed = fixture.Snapshot();
        }
        Assert.Equal(0, decayed.PeakLevel);
    }

    [Fact]
    public void IdleTimer_StopsAndNextRequestRestartsCapture()
    {
        using var fixture = new Fixture();
        fixture.Snapshot();
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(8001));
        Assert.Equal(1, fixture.Capture.StopCount);
        Assert.Equal(1, fixture.Capture.DisposeCount);

        fixture.Snapshot();
        Assert.Equal(2, fixture.Captures.Count);
        Assert.Equal(1, fixture.Captures[1].StartCount);
    }

    [Fact]
    public void UnexpectedRecordingStopped_DisposesCaptureAndNextRequestRestarts()
    {
        using var fixture = new Fixture();
        fixture.Snapshot();

        fixture.Capture.EmitStopped();

        Assert.Equal(1, fixture.Capture.DisposeCount);
        fixture.Snapshot();
        Assert.Equal(2, fixture.Captures.Count);
        Assert.Equal(1, fixture.Captures[1].StartCount);
    }

    [Fact]
    public void FactoryFailure_IsReportedAndLaterRequestCanRecover()
    {
        var clock = new ManualTimeProvider();
        var capture = new FakeWaveIn();
        var attempts = 0;
        using var service = new AudioVisualizerService(() => ++attempts == 1 ? throw new InvalidOperationException("capture unavailable") : capture, clock);

        var failed = Read(service.GetSnapshot(null));
        Assert.False(failed.Available);
        Assert.Equal("capture unavailable", failed.Error);
        Assert.True(Read(service.GetSnapshot(null)).Available);
        Assert.Equal(2, attempts);
        Assert.Equal(1, capture.StartCount);
    }

    [Fact]
    public void StartFailure_DisposesFailedCaptureAndAllowsRetry()
    {
        var clock = new ManualTimeProvider();
        var failedCapture = new FakeWaveIn { StartException = new InvalidOperationException("start failed") };
        var recoveredCapture = new FakeWaveIn();
        var queue = new Queue<FakeWaveIn>([failedCapture, recoveredCapture]);
        using var service = new AudioVisualizerService(() => queue.Dequeue(), clock);

        var failed = Read(service.GetSnapshot(null));
        Assert.False(failed.Available);
        Assert.Equal("start failed", failed.Error);
        Assert.Equal(1, failedCapture.DisposeCount);
        Assert.True(Read(service.GetSnapshot(null)).Available);
        Assert.Equal(1, recoveredCapture.StartCount);
    }

    [Fact]
    public void StopFailure_StillDisposesCaptureAndDisposeIsIdempotent()
    {
        var capture = new FakeWaveIn { StopException = new InvalidOperationException("stop failed") };
        using var service = new AudioVisualizerService(() => capture, new ManualTimeProvider());
        service.GetSnapshot(null);

        service.Dispose();
        service.Dispose();

        Assert.Equal(1, capture.StopCount);
        Assert.Equal(1, capture.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => service.GetSnapshot(null));
    }

    [Fact]
    public void StaleCaptureEventCannotOverwriteRestartedCaptureState()
    {
        var clock = new ManualTimeProvider();
        var oldCapture = new FakeWaveIn();
        var newCapture = new FakeWaveIn();
        var queue = new Queue<FakeWaveIn>([oldCapture, newCapture]);
        using var service = new AudioVisualizerService(() => queue.Dequeue(), clock);
        service.GetSnapshot(null);
        var oldHandler = oldCapture.CaptureDataHandler();

        clock.Advance(TimeSpan.FromMilliseconds(8001));
        service.GetSnapshot(null);
        newCapture.Emit(Samples(256, (_, _) => 0.2f));
        var before = Read(service.GetSnapshot(null));
        var staleBytes = Bytes(Samples(256, (_, _) => 1f));
        ((EventHandler<WaveInEventArgs>)oldHandler!)(oldCapture, new WaveInEventArgs(staleBytes, staleBytes.Length));
        var after = Read(service.GetSnapshot(null));

        Assert.Equal(before.PeakLevel, after.PeakLevel);
        Assert.Equal(before.RmsLevel, after.RmsLevel);
    }

    private static float[] Samples(int frames, Func<int, int, float> sample) =>
        Enumerable.Range(0, frames).SelectMany(frame => new[] { sample(frame, 0), sample(frame, 1) }).ToArray();

    private static byte[] Bytes(float[] samples)
    {
        var bytes = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static JsonElement? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static Snapshot Read(object value)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value));
        var root = doc.RootElement;
        return new Snapshot(
            root.GetProperty("Available").GetBoolean(),
            root.TryGetProperty("Error", out var error) ? error.GetString() : null,
            root.TryGetProperty("PeakLevel", out var peak) ? peak.GetDouble() : 0,
            root.TryGetProperty("RmsLevel", out var rms) ? rms.GetDouble() : 0,
            root.TryGetProperty("IsSilent", out var silent) && silent.GetBoolean(),
            root.TryGetProperty("BandCount", out var count) ? count.GetInt32() : 0,
            root.TryGetProperty("Bands", out var bands) ? bands.EnumerateArray().Select(b => b.GetDouble()).ToArray() : []);
    }

    private sealed record Snapshot(bool Available, string? Error, double PeakLevel, double RmsLevel, bool IsSilent, int BandCount, double[] Bands);

    private sealed class Fixture : IDisposable
    {
        public ManualTimeProvider Clock { get; } = new();
        public List<FakeWaveIn> Captures { get; } = [];
        public FakeWaveIn Capture => Captures[0];
        public AudioVisualizerService Service { get; }

        public Fixture()
        {
            Service = new AudioVisualizerService(() =>
            {
                var capture = new FakeWaveIn();
                Captures.Add(capture);
                return capture;
            }, Clock);
        }

        public Snapshot Snapshot(int bands = 16) => Read(Service.GetSnapshot(Parse($"{{\"bands\":{bands}}}")));

        public void Dispose() => Service.Dispose();
    }

    private sealed class FakeWaveIn : IWaveIn
    {
        public WaveFormat WaveFormat { get; set; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }
        public Exception? StartException { get; init; }
        public Exception? StopException { get; init; }
        public event EventHandler<WaveInEventArgs>? DataAvailable;
        public event EventHandler<StoppedEventArgs>? RecordingStopped;

        public void StartRecording()
        {
            StartCount++;
            if (StartException is not null) throw StartException;
        }

        public void StopRecording()
        {
            StopCount++;
            if (StopException is not null) throw StopException;
        }

        public void Emit(float[] samples) => EmitRaw(Bytes(samples), samples.Length * sizeof(float));

        public void EmitRaw(byte[] bytes, int bytesRecorded) => DataAvailable?.Invoke(this, new WaveInEventArgs(bytes, bytesRecorded));

        public void EmitStopped() => RecordingStopped?.Invoke(this, new StoppedEventArgs(null));

        public Delegate? CaptureDataHandler() => DataAvailable?.GetInvocationList().SingleOrDefault();

        public void Dispose() => DisposeCount++;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private readonly List<ManualTimer> _timers = [];
        public override DateTimeOffset GetUtcNow() => _now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state, dueTime, period);
            _timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan amount)
        {
            _now += amount;
            foreach (var timer in _timers.ToArray()) timer.FireIfDue(_now);
        }

        private sealed class ManualTimer(ManualTimeProvider provider, TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) : ITimer
        {
            private DateTimeOffset _next = provider._now + dueTime;
            private TimeSpan _period = period;
            private bool _disposed;
            public bool Change(TimeSpan newDueTime, TimeSpan newPeriod)
            {
                if (_disposed) return false;
                _next = provider._now + newDueTime;
                _period = newPeriod;
                return true;
            }
            public void Dispose() => _disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
            public void FireIfDue(DateTimeOffset now)
            {
                if (_disposed || now < _next) return;
                callback(state);
                _next = _period == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : _next + _period;
            }
        }
    }
}
