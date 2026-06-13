using System.Runtime.InteropServices;
using System.Windows.Threading;

using HTWind.Model;

namespace HTWind.Services.Widget;

public sealed class WidgetSuppressionMonitor : IWidgetSuppressionMonitor, IDisposable
{
    private const int FullscreenSuppressionCheckIntervalMilliseconds = 400;
    private const int FullscreenBoundsTolerancePixels = 2;
    private const uint MonitorDefaultToNull = 0;

    private readonly DispatcherTimer _suppressionTimer;
    private readonly EventHandler _suppressionTickHandler;
    private Action? _reconcileCallback;
    private bool _isDisposed;

    public WidgetSuppressionMonitor()
    {
        _suppressionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(FullscreenSuppressionCheckIntervalMilliseconds)
        };
        _suppressionTickHandler = SuppressionTimer_Tick;
        _suppressionTimer.Tick += _suppressionTickHandler;
    }

    public bool IsFullscreenSuppressionEnabled { get; set; } = true;
    public bool IsMaximizedSuppressionEnabled { get; set; }

    public void Start(Action reconcileCallback)
    {
        _reconcileCallback = reconcileCallback ?? throw new ArgumentNullException(nameof(reconcileCallback));
        _suppressionTimer.Start();
    }

    public void Stop()
    {
        _suppressionTimer.Stop();
        _reconcileCallback = null;
    }

    public bool ShouldSuppressWidget(WidgetModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!IsFullscreenSuppressionEnabled && !IsMaximizedSuppressionEnabled)
        {
            return false;
        }

        var activeSuppressionMonitorDeviceName = GetActiveSuppressionMonitorDeviceName();
        return ShouldSuppressWidgetForMonitor(model, activeSuppressionMonitorDeviceName);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Stop();
        _suppressionTimer.Tick -= _suppressionTickHandler;
        _isDisposed = true;
    }

    private void SuppressionTimer_Tick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _reconcileCallback?.Invoke();
    }

    private static bool ShouldSuppressWidgetForMonitor(
        WidgetModel model,
        string? fullscreenMonitorDeviceName)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrWhiteSpace(fullscreenMonitorDeviceName))
        {
            return false;
        }

        var widgetMonitorDeviceName = ResolveWidgetMonitorDeviceName(model);
        if (string.IsNullOrWhiteSpace(widgetMonitorDeviceName))
        {
            return false;
        }

        return string.Equals(
            widgetMonitorDeviceName,
            fullscreenMonitorDeviceName,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static string? ResolveWidgetMonitorDeviceName(WidgetModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!string.IsNullOrWhiteSpace(model.PreferredMonitorDeviceName))
        {
            return model.PreferredMonitorDeviceName;
        }

        if (!string.IsNullOrWhiteSpace(model.MonitorDeviceName))
        {
            return model.MonitorDeviceName;
        }

        var recentPlacement = model.MonitorPlacements
            .Where(placement => !string.IsNullOrWhiteSpace(placement.MonitorDeviceName))
            .OrderByDescending(placement => placement.LastSeenAtUtc)
            .FirstOrDefault();

        return recentPlacement?.MonitorDeviceName;
    }

    private string? GetActiveSuppressionMonitorDeviceName()
    {
        if (!IsFullscreenSuppressionEnabled && !IsMaximizedSuppressionEnabled)
        {
            return null;
        }

        var foregroundWindowHandle = GetForegroundWindow();
        if (foregroundWindowHandle == IntPtr.Zero)
        {
            return null;
        }

        if (!IsWindowVisible(foregroundWindowHandle) || IsIconic(foregroundWindowHandle))
        {
            return null;
        }

        GetWindowThreadProcessId(foregroundWindowHandle, out var processId);
        if (processId == Environment.ProcessId)
        {
            return null;
        }

        if (!GetWindowRect(foregroundWindowHandle, out var windowRect))
        {
            return null;
        }

        if (windowRect.Right <= windowRect.Left || windowRect.Bottom <= windowRect.Top)
        {
            return null;
        }

        var monitor = MonitorFromWindow(foregroundWindowHandle, MonitorDefaultToNull);
        if (monitor == IntPtr.Zero)
        {
            return null;
        }

        var monitorInfo = new MonitorInfoEx { CbSize = Marshal.SizeOf<MonitorInfoEx>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return null;
        }

        var isFullscreenWindow =
            Math.Abs(windowRect.Left - monitorInfo.RcMonitor.Left) <= FullscreenBoundsTolerancePixels
            && Math.Abs(windowRect.Top - monitorInfo.RcMonitor.Top) <= FullscreenBoundsTolerancePixels
            && Math.Abs(windowRect.Right - monitorInfo.RcMonitor.Right) <= FullscreenBoundsTolerancePixels
            && Math.Abs(windowRect.Bottom - monitorInfo.RcMonitor.Bottom)
                <= FullscreenBoundsTolerancePixels;

        if (IsFullscreenSuppressionEnabled && isFullscreenWindow)
        {
            return monitorInfo.SzDevice;
        }

        if (IsMaximizedSuppressionEnabled && IsZoomed(foregroundWindowHandle))
        {
            return monitorInfo.SzDevice;
        }

        return null;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfoEx
    {
        public int CbSize;
        public NativeRect RcMonitor;
        public NativeRect RcWork;
        public int DwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string SzDevice;
    }
}
