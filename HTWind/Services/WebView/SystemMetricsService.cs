using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HTWind.Services.WebView;

/// <summary>
///     Collects system telemetry in-process (zero child processes) for the
///     <c>system.metrics</c> host API.
/// </summary>
public sealed class SystemMetricsService : IDisposable
{
    private const uint TotalProcessorTimeCounterFallbackPercent = 0;

    private PerformanceCounter? _cpuCounter;
    private bool _cpuCounterWarmupDone;
    private double _lastCpuLoadPercent;
    private PerformanceCounterCategory? _networkCategory;
    private List<(string Instance, PerformanceCounter Received, PerformanceCounter Sent)>? _networkCounters;
    private DateTime _networkCountersRefreshedAtUtc = DateTime.MinValue;
    private readonly object _sync = new();
    private bool _isDisposed;

    public async Task<object> GetSnapshotAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        // PerformanceCounter.NextValue needs a warm-up call; keep the rest fully synchronous.
        var cpuLoadPercent = await Task.Run(ReadCpuLoadPercent);
        var memory = ReadMemoryStatus();
        var disks = ReadDisks();
        var (rxBytesPerSecond, txBytesPerSecond) = await Task.Run(ReadNetworkThroughput);
        var battery = ReadBatteryStatus();

        return new
        {
            Available = true,
            MachineName = Environment.MachineName,
            OsVersion = Environment.OSVersion.VersionString,
            UptimeSeconds = (long)TimeSpan.FromMilliseconds(Environment.TickCount64).TotalSeconds,
            ProcessCount = ReadProcessCount(),
            CpuLoadPercent = cpuLoadPercent,
            MemoryUsedBytes = memory.UsedBytes,
            MemoryTotalBytes = memory.TotalBytes,
            MemoryUsedPercent = memory.UsedPercent,
            NetworkBytesReceivedPerSecond = rxBytesPerSecond,
            NetworkBytesSentPerSecond = txBytesPerSecond,
            Battery = battery,
            Disks = disks
        };
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        lock (_sync)
        {
            _cpuCounter?.Dispose();
            _cpuCounter = null;

            if (_networkCounters is not null)
            {
                foreach (var (_, received, sent) in _networkCounters)
                {
                    received.Dispose();
                    sent.Dispose();
                }

                _networkCounters = null;
            }

            _isDisposed = true;
        }

        GC.SuppressFinalize(this);
    }

    private double ReadCpuLoadPercent()
    {
        try
        {
            lock (_sync)
            {
                if (_isDisposed)
                {
                    return _lastCpuLoadPercent;
                }

                _cpuCounter ??= new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);

                var value = _cpuCounter.NextValue();

                // The first NextValue call always returns 0; keep the previous real sample.
                if (!_cpuCounterWarmupDone)
                {
                    _cpuCounterWarmupDone = true;
                    return _lastCpuLoadPercent;
                }

                _lastCpuLoadPercent = Math.Clamp(value, 0, 100);
                return _lastCpuLoadPercent;
            }
        }
        catch
        {
            return _lastCpuLoadPercent;
        }
    }

    private static int ReadProcessCount()
    {
        try
        {
            return Process.GetProcesses().Length;
        }
        catch
        {
            return 0;
        }
    }

    private static (long UsedBytes, long TotalBytes, double UsedPercent) ReadMemoryStatus()
    {
        if (!GlobalMemoryStatusEx(out var status))
        {
            return (0, 0, 0);
        }

        var total = (long)status.UllTotalPhys;
        var available = (long)status.UllAvailPhys;
        var used = Math.Max(0, total - available);
        var usedPercent = total > 0 ? Math.Round(used * 100.0 / total, 1) : 0;
        return (used, total, usedPercent);
    }

    private static object[] ReadDisks()
    {
        var disks = new List<object>();

        try
        {
            foreach (var drive in System.IO.DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType != System.IO.DriveType.Fixed)
                {
                    continue;
                }

                try
                {
                    var total = drive.TotalSize;
                    var free = drive.AvailableFreeSpace;
                    var used = Math.Max(0, total - free);
                    var usedPercent = total > 0 ? Math.Round(used * 100.0 / total, 1) : 0;

                    disks.Add(new
                    {
                        Name = drive.Name,
                        Label = drive.VolumeLabel,
                        UsedPercent = usedPercent,
                        UsedBytes = used,
                        TotalBytes = total
                    });
                }
                catch
                {
                    // Individual drive failures should not drop the remaining disks.
                }
            }
        }
        catch
        {
            // Fall through with an empty disk list.
        }

        return [.. disks];
    }

    private (long ReceivedPerSecond, long SentPerSecond) ReadNetworkThroughput()
    {
        try
        {
            lock (_sync)
            {
                if (_isDisposed)
                {
                    return (0, 0);
                }

                EnsureNetworkCounters();

                long received = 0;
                long sent = 0;

                if (_networkCounters is null)
                {
                    return (0, 0);
                }

                foreach (var (_, receivedCounter, sentCounter) in _networkCounters)
                {
                    try
                    {
                        received += (long)receivedCounter.NextValue();
                        sent += (long)sentCounter.NextValue();
                    }
                    catch
                    {
                        // A disappearing interface should not break the aggregate.
                    }
                }

                return (Math.Max(0, received), Math.Max(0, sent));
            }
        }
        catch
        {
            return (0, 0);
        }
    }

    private void EnsureNetworkCounters()
    {
        if (_networkCounters is not null && DateTime.UtcNow - _networkCountersRefreshedAtUtc < TimeSpan.FromMinutes(2))
        {
            return;
        }

        if (_networkCounters is not null)
        {
            foreach (var (_, received, sent) in _networkCounters)
            {
                received.Dispose();
                sent.Dispose();
            }

            _networkCounters = null;
        }

        try
        {
            _networkCategory ??= new PerformanceCounterCategory("Network Interface");
            var instances = _networkCategory.GetInstanceNames();

            var counters = new List<(string, PerformanceCounter, PerformanceCounter)>();
            foreach (var instance in instances)
            {
                try
                {
                    var received = new PerformanceCounter("Network Interface", "Bytes Received/Sec", instance, readOnly: true);
                    var sent = new PerformanceCounter("Network Interface", "Bytes Sent/Sec", instance, readOnly: true);
                    received.NextValue();
                    sent.NextValue();
                    counters.Add((instance, received, sent));
                }
                catch
                {
                    // Skip unavailable instances.
                }
            }

            _networkCounters = counters;
            _networkCountersRefreshedAtUtc = DateTime.UtcNow;
        }
        catch
        {
            _networkCounters = [];
            _networkCountersRefreshedAtUtc = DateTime.UtcNow;
        }
    }

    private static object ReadBatteryStatus()
    {
        if (!GetSystemPowerStatus(out var status))
        {
            return new
            {
                HasBattery = false,
                IsOnAcPower = true,
                ChargePercent = (double?)null,
                BatterySecondsRemaining = (long?)null
            };
        }

        var hasBattery = status.BatteryFlag != 128;
        var chargePercent = hasBattery && status.BatteryLifePercent <= 100
            ? (double?)status.BatteryLifePercent
            : null;
        var secondsRemaining = status.BatteryLifeTime > 0 && status.BatteryLifeTime != uint.MaxValue
            ? (long?)status.BatteryLifeTime
            : null;

        return new
        {
            HasBattery = hasBattery,
            IsOnAcPower = status.ACLineStatus == 1,
            ChargePercent = chargePercent,
            BatterySecondsRemaining = secondsRemaining
        };
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(out NativeMemoryStatus buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMemoryStatus
    {
        public uint DwLength;
        public uint DwMemoryLoad;
        public ulong UllTotalPhys;
        public ulong UllAvailPhys;
        public ulong UllTotalPageFile;
        public ulong UllAvailPageFile;
        public ulong UllTotalVirtual;
        public ulong UllAvailVirtual;
        public ulong UllAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out NativeSystemPowerStatus systemPowerStatus);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte Reserved1;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }
}
