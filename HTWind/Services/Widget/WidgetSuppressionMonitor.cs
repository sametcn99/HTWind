using System.Runtime.InteropServices;
using System.Windows.Threading;

using HTWind.Model;

namespace HTWind.Services.Widget;

/// <summary>
///     Event-driven fullscreen/maximized suppression monitor. Instead of polling every 400 ms it
///     installs WinEvent hooks (EVENT_SYSTEM_FOREGROUND + EVENT_OBJECT_LOCATIONCHANGE) and only
///     re-evaluates the suppression state when the foreground window changes or a window
///     moves/resizes, marshaled onto the UI thread via the dispatcher.
/// </summary>
public sealed class WidgetSuppressionMonitor : IWidgetSuppressionMonitor, IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectLocationChange = 0x800B;
    private const uint WinEventOutOfContext = 0;
    private const int ObjectIdWindow = 0;
    private const int FullscreenBoundsTolerancePixels = 2;
    private const uint MonitorDefaultToNull = 0;
    private const int ReconcileCoalesceMilliseconds = 50;

    private readonly Dispatcher _dispatcher;
    private readonly WinEventDelegate _winEventCallbackDelegate;
    private Action? _reconcileCallback;
    private IntPtr _foregroundHook;
    private IntPtr _locationHook;
    private DateTimeOffset _nextReconcileAllowedAtUtc = DateTimeOffset.MinValue;
    private bool _isReconcileQueued;
    private string? _lastSuppressionMonitorDeviceName;
    private bool _isDisposed;

    public WidgetSuppressionMonitor()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        // The delegate must be kept alive for the lifetime of the hooks (native code holds a
        // function pointer to it).
        _winEventCallbackDelegate = WinEventCallback;
    }

    public bool IsFullscreenSuppressionEnabled { get; set; } = true;
    public bool IsMaximizedSuppressionEnabled { get; set; }

    public void Start(Action reconcileCallback)
    {
        _reconcileCallback = reconcileCallback ?? throw new ArgumentNullException(nameof(reconcileCallback));
        EnsureHooksInstalled();
    }

    public void Stop()
    {
        UnhookWinEventSafe(ref _foregroundHook);
        UnhookWinEventSafe(ref _locationHook);
        _reconcileCallback = null;
        _lastSuppressionMonitorDeviceName = null;
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
        _isDisposed = true;
    }

    private void EnsureHooksInstalled()
    {
        if (_foregroundHook != IntPtr.Zero)
        {
            return;
        }

        // The hooks are installed on the UI thread (which pumps messages) so the
        // WINEVENT_OUTOFCONTEXT callbacks are delivered on that same thread.
        _foregroundHook = SetWinEventHook(
            EventSystemForeground,
            EventSystemForeground,
            IntPtr.Zero,
            _winEventCallbackDelegate,
            0,
            0,
            WinEventOutOfContext);

        _locationHook = SetWinEventHook(
            EventObjectLocationChange,
            EventObjectLocationChange,
            IntPtr.Zero,
            _winEventCallbackDelegate,
            0,
            0,
            WinEventOutOfContext);
    }

    private void WinEventCallback(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        _ = hWinEventHook;
        _ = dwEventThread;
        _ = dwmsEventTime;

        if (hwnd == IntPtr.Zero || idObject != ObjectIdWindow || idChild != 0)
        {
            return;
        }

        // LOCATIONCHANGE fires for every window move/resize on the system; coalesce the
        // evaluations so a single reconcile runs per short burst of events.
        var now = DateTimeOffset.UtcNow;
        if (now < _nextReconcileAllowedAtUtc)
        {
            if (_isReconcileQueued)
            {
                return;
            }

            _isReconcileQueued = true;
            _ = _dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(ReconcileIfChanged));
            return;
        }

        _nextReconcileAllowedAtUtc = now.AddMilliseconds(ReconcileCoalesceMilliseconds);
        _isReconcileQueued = false;

        var dispatcher = System.Windows.Application.Current?.Dispatcher ?? _dispatcher;
        if (dispatcher.CheckAccess())
        {
            ReconcileIfChanged();
        }
        else
        {
            _ = dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ReconcileIfChanged));
        }
    }

    private void ReconcileIfChanged()
    {
        _isReconcileQueued = false;

        if (_reconcileCallback is null || _isDisposed)
        {
            return;
        }

        var suppressionMonitorDeviceName = GetActiveSuppressionMonitorDeviceName();
        if (string.Equals(
                suppressionMonitorDeviceName,
                _lastSuppressionMonitorDeviceName,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _lastSuppressionMonitorDeviceName = suppressionMonitorDeviceName;
        _reconcileCallback();
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

    private void UnhookWinEventSafe(ref IntPtr hook)
    {
        if (hook == IntPtr.Zero)
        {
            return;
        }

        try
        {
            UnhookWinEvent(hook);
        }
        catch
        {
        }

        hook = IntPtr.Zero;
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

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate proc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime);

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
