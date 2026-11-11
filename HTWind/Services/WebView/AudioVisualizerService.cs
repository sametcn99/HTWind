using System.Text.Json;

using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;

namespace HTWind.Services.WebView;

/// <summary>
///     Provides real-time audio peak and FFT band data for the <c>audio.visualizer</c> host API
///     using a WASAPI loopback capture (zero child processes). The capture is started lazily and
///     stops automatically after a period without requests.
/// </summary>
public sealed class AudioVisualizerService : IDisposable
{
    private const int FftLength = 2048;
    private const int DefaultBandCount = 16;
    private const int IdleStopDelayMilliseconds = 8000;
    private const int StaleDataDecayThresholdMilliseconds = 500;
    private const float SmoothingFactor = 0.45f;

    private readonly object _sync = new();
    private WasapiLoopbackCapture? _capture;
    private Timer? _idleTimer;
    private float _peakLevel;
    private float _rmsLevel;
    private float[] _bandLevels = new float[DefaultBandCount];
    private string? _lastError;
    private bool _isCaptureFailed;
    private DateTime _lastRequestUtc = DateTime.MinValue;
    private DateTime _lastDataReceivedUtc = DateTime.MinValue;
    private bool _isDisposed;

    public object GetSnapshot(JsonElement? args)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var requestedBands = ReadIntArg(args, "bands") ?? DefaultBandCount;
        requestedBands = Math.Clamp(requestedBands, 4, 64);

        EnsureStarted();
        _lastRequestUtc = DateTime.UtcNow;
        EnsureIdleTimer();

        lock (_sync)
        {
            // WASAPI loopback delivers no DataAvailable events during silence, so decay the last
            // values toward zero when capture data is stale instead of freezing them.
            if (DateTime.UtcNow - _lastDataReceivedUtc > TimeSpan.FromMilliseconds(StaleDataDecayThresholdMilliseconds))
            {
                const float decayFactor = 0.6f;
                _peakLevel *= decayFactor;
                _rmsLevel *= decayFactor;
                for (var index = 0; index < _bandLevels.Length; index++)
                {
                    _bandLevels[index] *= decayFactor;
                }

                if (_peakLevel < 0.01f)
                {
                    _peakLevel = 0f;
                }

                if (_rmsLevel < 0.005f)
                {
                    _rmsLevel = 0f;
                }
            }
        }

        lock (_sync)
        {
            if (_isCaptureFailed)
            {
                return new
                {
                    Available = false,
                    Error = _lastError ?? "Audio capture is unavailable on this system."
                };
            }

            var bands = new double[requestedBands];
            for (var index = 0; index < requestedBands; index++)
            {
                var sourceIndex = (int)Math.Round(index * (double)(_bandLevels.Length - 1) / Math.Max(1, requestedBands - 1));
                bands[index] = Math.Round(_bandLevels[sourceIndex], 4);
            }

            return new
            {
                Available = true,
                PeakLevel = Math.Round(_peakLevel, 4),
                RmsLevel = Math.Round(_rmsLevel, 4),
                IsSilent = _peakLevel < 0.01,
                BandCount = requestedBands,
                Bands = bands
            };
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        lock (_sync)
        {
            _isDisposed = true;
            StopCaptureCore();
        }

        GC.SuppressFinalize(this);
    }

    private void EnsureStarted()
    {
        lock (_sync)
        {
            if (_isDisposed || _capture is not null)
            {
                return;
            }

            try
            {
                var capture = new WasapiLoopbackCapture();
                capture.DataAvailable += Capture_DataAvailable;
                capture.RecordingStopped += Capture_RecordingStopped;
                capture.StartRecording();
                _capture = capture;
                _isCaptureFailed = false;
                _lastError = null;
            }
            catch (Exception exception)
            {
                _isCaptureFailed = true;
                _lastError = exception.Message;
                _capture = null;
            }
        }
    }

    private void EnsureIdleTimer()
    {
        lock (_sync)
        {
            if (_isDisposed || _idleTimer is not null)
            {
                return;
            }

            _idleTimer = new Timer(
                _ =>
                {
                    if (DateTime.UtcNow - _lastRequestUtc > TimeSpan.FromMilliseconds(IdleStopDelayMilliseconds))
                    {
                        lock (_sync)
                        {
                            if (_isDisposed)
                            {
                                return;
                            }

                            StopCaptureCore();
                        }
                    }
                },
                null,
                TimeSpan.FromMilliseconds(IdleStopDelayMilliseconds),
                TimeSpan.FromMilliseconds(IdleStopDelayMilliseconds));
        }
    }

    private void Capture_RecordingStopped(object? sender, EventArgs e)
    {
        lock (_sync)
        {
            _capture?.Dispose();
            _capture = null;
        }
    }

    private void Capture_DataAvailable(object? sender, NAudio.Wave.WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
        {
            return;
        }

        try
        {
            var waveBuffer = new WaveBuffer(e.Buffer);
            var floatCount = e.BytesRecorded / sizeof(float);
            var channels = Math.Max(1, _capture?.WaveFormat.Channels ?? 2);
            var frameCount = floatCount / channels;
            if (frameCount <= 0)
            {
                return;
            }

            var peak = 0f;
            var sumOfSquares = 0d;
            // Mono mixdown of the newest frames, then Hann window + FFT for band magnitudes.
            var mono = new float[Math.Min(frameCount, FftLength)];
            var monoOffset = frameCount - mono.Length;
            for (var frame = monoOffset; frame < frameCount; frame++)
            {
                var sampleSum = 0f;
                for (var channel = 0; channel < channels; channel++)
                {
                    var sample = waveBuffer.FloatBuffer[(frame * channels) + channel];
                    sampleSum += sample;
                    var absolute = Math.Abs(sample);
                    if (absolute > peak)
                    {
                        peak = absolute;
                    }
                }

                var monoSample = sampleSum / channels;
                mono[frame - monoOffset] = monoSample;
                sumOfSquares += monoSample * monoSample;
            }

            var rms = (float)Math.Sqrt(sumOfSquares / mono.Length);
            var bands = ComputeBands(mono, channels, _capture?.WaveFormat.SampleRate ?? 48000);

            lock (_sync)
            {
                _lastDataReceivedUtc = DateTime.UtcNow;
                _peakLevel = Math.Clamp(peak, 0, 1);
                _rmsLevel = Math.Clamp(rms, 0, 1);

                for (var index = 0; index < _bandLevels.Length; index++)
                {
                    var smoothed = (_bandLevels[index] * (1 - SmoothingFactor)) + (bands[index] * SmoothingFactor);
                    _bandLevels[index] = Math.Clamp(smoothed, 0, 1);
                }
            }
        }
        catch
        {
            // Malformed buffers must never crash the capture thread.
        }
    }

    private static float[] ComputeBands(float[] mono, int channels, int sampleRate)
    {
        var bands = new float[DefaultBandCount];
        if (mono.Length < FftLength)
        {
            return bands;
        }

        var complex = new Complex[FftLength];
        for (var index = 0; index < FftLength; index++)
        {
            var window = FastFourierTransform.HannWindow(index, FftLength);
            complex[index].X = (float)(mono[index] * window);
            complex[index].Y = 0;
        }

        FastFourierTransform.FFT(true, (int)Math.Log2(FftLength), complex);

        var binCount = FftLength / 2;
        var binHz = sampleRate / (double)FftLength;
        var magnitudes = new float[binCount];
        var maxMagnitude = 0.0001f;

        for (var bin = 0; bin < binCount; bin++)
        {
            var magnitude = (float)Math.Sqrt((complex[bin].X * complex[bin].X) + (complex[bin].Y * complex[bin].Y));
            magnitudes[bin] = magnitude;
            if (magnitude > maxMagnitude)
            {
                maxMagnitude = magnitude;
            }
        }

        // Aggregate into logarithmically spaced bands over the audible range.
        var minHz = 40d;
        var maxHz = Math.Min(16000d, sampleRate / 2.0);
        for (var band = 0; band < DefaultBandCount; band++)
        {
            var startHz = minHz * Math.Pow(maxHz / minHz, band / (double)DefaultBandCount);
            var endHz = minHz * Math.Pow(maxHz / minHz, (band + 1) / (double)DefaultBandCount);
            var startBin = Math.Max(0, (int)(startHz / binHz));
            var endBin = Math.Min(binCount - 1, Math.Max(startBin, (int)(endHz / binHz)));

            var sum = 0f;
            for (var bin = startBin; bin <= endBin; bin++)
            {
                sum += magnitudes[bin];
            }

            var normalized = (sum / Math.Max(1, endBin - startBin + 1)) / maxMagnitude;
            bands[band] = Math.Clamp(normalized * 3f, 0f, 1f);
        }

        return bands;
    }

    private static int? ReadIntArg(JsonElement? args, string key)
    {
        if (!args.HasValue || args.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!args.Value.TryGetProperty(key, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private void StopCaptureCore()
    {
        _idleTimer?.Dispose();
        _idleTimer = null;

        var capture = _capture;
        _capture = null;

        if (capture is null)
        {
            return;
        }

        try
        {
            capture.DataAvailable -= Capture_DataAvailable;
            capture.RecordingStopped -= Capture_RecordingStopped;
            capture.StopRecording();
            capture.Dispose();
        }
        catch
        {
        }
    }
}
