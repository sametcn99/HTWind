using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

using HTWind.Model;

namespace HTWind.Services.Widget;

public sealed class WidgetGeometryService : IWidgetGeometryService
{
    private const int MonitorDefaultToNearest = 2;
    private const int MonitorDefaultToNull = 0;
    private const int MonitorDpiTypeEffectiveDpi = 0;
    private static readonly TimeSpan MonitorCacheDuration = TimeSpan.FromMilliseconds(750);
    private static readonly object MonitorCacheSync = new();
    private static DateTimeOffset MonitorCacheExpiresAtUtc = DateTimeOffset.MinValue;
    private static Dictionary<string, Rect> MonitorCache =
        new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, double> MonitorScaleCache =
        new(StringComparer.OrdinalIgnoreCase);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, ref uint dpiX, ref uint dpiY);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData
    );

    public void CaptureGeometry(WidgetWindow window, WidgetModel model, bool updatePreferredMonitor = true)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(model);

        model.Left = window.Left;
        model.Top = window.Top;
        model.WidgetWidth = window.Width;
        model.WidgetHeight = window.Height;

        var currentMonitorDeviceName = GetMonitorDeviceName(window);
        model.MonitorDeviceName = currentMonitorDeviceName;

        if (!string.IsNullOrWhiteSpace(currentMonitorDeviceName))
        {
            UpdateMonitorPlacement(
                model,
                currentMonitorDeviceName,
                window.Left,
                window.Top,
                window.Width,
                window.Height
            );

            if (updatePreferredMonitor)
            {
                var hasPreferredMonitor = !string.IsNullOrWhiteSpace(model.PreferredMonitorDeviceName);
                var preferredMonitorConnected =
                    hasPreferredMonitor && IsMonitorConnected(model.PreferredMonitorDeviceName!);

                if (!hasPreferredMonitor || preferredMonitorConnected)
                {
                    model.PreferredMonitorDeviceName = currentMonitorDeviceName;
                }
            }
        }
    }

    public void ApplyPersistedGeometry(
        WidgetWindow window,
        WidgetModel model,
        double defaultWidth,
        double defaultHeight
    )
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(model);

        window.Width = model.WidgetWidth.GetValueOrDefault(defaultWidth);
        window.Height = model.WidgetHeight.GetValueOrDefault(defaultHeight);

        var connectedMonitors = GetConnectedMonitors();
        var targetMonitorDeviceName = ResolveBestAvailableMonitor(model, connectedMonitors);
        var targetWorkArea = ResolveTargetMonitorWorkArea(targetMonitorDeviceName);

        if (
            targetMonitorDeviceName is not null
            && TryGetPlacement(model, targetMonitorDeviceName, out var monitorPlacement)
        )
        {
            window.Left = monitorPlacement.Left;
            window.Top = monitorPlacement.Top;
            window.Width = monitorPlacement.Width;
            window.Height = monitorPlacement.Height;

            ClampWindowToWorkArea(window, targetWorkArea);
            return;
        }

        if (model.Left.HasValue && model.Top.HasValue)
        {
            window.Left = model.Left.Value;
            window.Top = model.Top.Value;

            if (!IsOnAnyScreen(window.Left, window.Top, window.Width, window.Height))
            {
                CenterWindowOnWorkArea(window, targetWorkArea);
            }
        }
        else
        {
            CenterWindowOnWorkArea(window, targetWorkArea);
        }

        ClampWindowToWorkArea(window, targetWorkArea);
    }

    public void ApplyDefaultGeometry(WidgetWindow window, double defaultWidth, double defaultHeight)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.Width = defaultWidth;
        window.Height = defaultHeight;
        CenterWindowOnWorkArea(window, null);
    }

    public void ResetToPrimaryDisplayCenter(
        WidgetWindow window,
        WidgetModel model,
        double defaultWidth,
        double defaultHeight
    )
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(model);

        if (window.Width <= 0)
        {
            window.Width = model.WidgetWidth.GetValueOrDefault(defaultWidth);
        }

        if (window.Height <= 0)
        {
            window.Height = model.WidgetHeight.GetValueOrDefault(defaultHeight);
        }

        CenterWindowOnWorkArea(window, null);
        CaptureGeometry(window, model);
    }

    public bool EnsureVisibleOnAvailableDisplay(WidgetWindow window, WidgetModel model)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(model);

        var connectedMonitors = GetConnectedMonitors();
        var preferredMonitorConnected =
            !string.IsNullOrWhiteSpace(model.PreferredMonitorDeviceName)
            && connectedMonitors.ContainsKey(model.PreferredMonitorDeviceName);

        if (preferredMonitorConnected)
        {
            var preferredMonitorName = model.PreferredMonitorDeviceName!;
            var preferredWorkArea = connectedMonitors[preferredMonitorName];

            if (TryGetPlacement(model, preferredMonitorName, out var preferredPlacement))
            {
                if (
                    Math.Abs(window.Left - preferredPlacement.Left) > 0.5
                    || Math.Abs(window.Top - preferredPlacement.Top) > 0.5
                    || Math.Abs(window.Width - preferredPlacement.Width) > 0.5
                    || Math.Abs(window.Height - preferredPlacement.Height) > 0.5
                )
                {
                    window.Left = preferredPlacement.Left;
                    window.Top = preferredPlacement.Top;
                    window.Width = preferredPlacement.Width;
                    window.Height = preferredPlacement.Height;
                    ClampWindowToWorkArea(window, preferredWorkArea);
                    return true;
                }
            }

            var preferredWindowRect = new Rect(window.Left, window.Top, window.Width, window.Height);
            if (preferredWindowRect.IntersectsWith(preferredWorkArea))
            {
                return false;
            }

            CenterWindowOnWorkArea(window, preferredWorkArea);
            ClampWindowToWorkArea(window, preferredWorkArea);
            return true;
        }

        var hasStoredMonitor = !string.IsNullOrWhiteSpace(model.MonitorDeviceName);
        var targetMonitorDeviceName = ResolveBestAvailableMonitor(model, connectedMonitors);
        var targetWorkArea = ResolveTargetMonitorWorkArea(targetMonitorDeviceName);
        var windowRect = new Rect(window.Left, window.Top, window.Width, window.Height);

        if (targetWorkArea.HasValue)
        {
            var target = targetWorkArea.Value;

            if (windowRect.IntersectsWith(target))
            {
                return false;
            }

            CenterWindowOnWorkArea(window, targetWorkArea);
            ClampWindowToWorkArea(window, targetWorkArea);
            return true;
        }

        var isWindowOnAnyScreen = IsOnAnyScreen(window.Left, window.Top, window.Width, window.Height);
        if (!hasStoredMonitor && isWindowOnAnyScreen)
        {
            return false;
        }

        CenterWindowOnWorkArea(window, null);
        ClampWindowToWorkArea(window, null);
        return true;
    }

    private static string? ResolveBestAvailableMonitor(
        WidgetModel model,
        IReadOnlyDictionary<string, Rect> connectedMonitors
    )
    {
        return ResolveBestAvailableMonitorName(
            model.PreferredMonitorDeviceName,
            model.MonitorPlacements,
            model.MonitorDeviceName,
            connectedMonitors
        );
    }

    /// <summary>
    ///     Pure monitor priority resolution: preferred monitor (when connected), then the
    ///     freshest seen placement of a connected monitor, then the last-known monitor (when
    ///     connected), otherwise null (caller falls back to the primary display center).
    /// </summary>
    internal static string? ResolveBestAvailableMonitorName(
        string? preferredMonitorDeviceName,
        IReadOnlyList<WidgetMonitorPlacement> monitorPlacements,
        string? monitorDeviceName,
        IReadOnlyDictionary<string, Rect> connectedMonitors
    )
    {
        if (
            !string.IsNullOrWhiteSpace(preferredMonitorDeviceName)
            && connectedMonitors.ContainsKey(preferredMonitorDeviceName)
        )
        {
            return preferredMonitorDeviceName;
        }

        var fallback = monitorPlacements
            .Where(placement =>
                !string.IsNullOrWhiteSpace(placement.MonitorDeviceName)
                && connectedMonitors.ContainsKey(placement.MonitorDeviceName!)
            )
            .OrderByDescending(placement => placement.LastSeenAtUtc)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(fallback?.MonitorDeviceName))
        {
            return fallback.MonitorDeviceName;
        }

        if (
            !string.IsNullOrWhiteSpace(monitorDeviceName)
            && connectedMonitors.ContainsKey(monitorDeviceName)
        )
        {
            return monitorDeviceName;
        }

        return null;
    }

    private static bool TryGetPlacement(
        WidgetModel model,
        string monitorDeviceName,
        out WidgetMonitorPlacement placement
    )
    {
        var existingPlacement = model.MonitorPlacements.FirstOrDefault(existing =>
            string.Equals(existing.MonitorDeviceName, monitorDeviceName, StringComparison.OrdinalIgnoreCase)
        );

        if (existingPlacement is null)
        {
            placement = new WidgetMonitorPlacement();
            return false;
        }

        placement = existingPlacement;

        if (placement.Width <= 0 || placement.Height <= 0)
        {
            return false;
        }

        return true;
    }

    private static void UpdateMonitorPlacement(
        WidgetModel model,
        string monitorDeviceName,
        double left,
        double top,
        double width,
        double height
    )
    {
        var existing = model.MonitorPlacements.FirstOrDefault(placement =>
            string.Equals(placement.MonitorDeviceName, monitorDeviceName, StringComparison.OrdinalIgnoreCase)
        );

        if (existing is null)
        {
            existing = new WidgetMonitorPlacement { MonitorDeviceName = monitorDeviceName };
            model.MonitorPlacements.Add(existing);
        }

        existing.Left = left;
        existing.Top = top;
        existing.Width = width;
        existing.Height = height;
        existing.LastSeenAtUtc = DateTimeOffset.UtcNow;
    }

    private static bool IsMonitorConnected(string monitorDeviceName)
    {
        return GetConnectedMonitors().ContainsKey(monitorDeviceName);
    }

    internal static void InvalidateMonitorCache()
    {
        lock (MonitorCacheSync)
        {
            MonitorCacheExpiresAtUtc = DateTimeOffset.MinValue;
            MonitorCache.Clear();
            MonitorScaleCache.Clear();
        }
    }

    /// <summary>
    ///     Work areas of all connected monitors in WPF device-independent pixels. Used by the
    ///     magnetic snap logic in the widget window.
    /// </summary>
    internal static IReadOnlyList<Rect> GetWorkAreasForSnapping()
    {
        return GetConnectedMonitors().Values.ToList();
    }

    private static Dictionary<string, Rect> GetConnectedMonitors()
    {
        lock (MonitorCacheSync)
        {
            if (DateTimeOffset.UtcNow <= MonitorCacheExpiresAtUtc && MonitorCache.Count > 0)
            {
                return new Dictionary<string, Rect>(MonitorCache, StringComparer.OrdinalIgnoreCase);
            }
        }

        var monitors = new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);
        var scaleCache = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (monitor, _, _, _) =>
            {
                var info = new MonitorInfoEx { CbSize = Marshal.SizeOf<MonitorInfoEx>() };
                if (!GetMonitorInfo(monitor, ref info) || string.IsNullOrWhiteSpace(info.SzDevice))
                {
                    return true;
                }

                var scale = GetMonitorScale(monitor, info.SzDevice);
                scaleCache[info.SzDevice] = scale;

                // Win32 returns physical pixels; convert to WPF DIPs so every value compared or
                // assigned against window.Left/Top/Width/Height stays correct under
                // PerMonitorV2 scaling (125%/150%/200%).
                monitors[info.SzDevice] = ConvertPhysicalRectToDip(
                    info.RcWork.Left,
                    info.RcWork.Top,
                    info.RcWork.Right - info.RcWork.Left,
                    info.RcWork.Bottom - info.RcWork.Top,
                    scale
                );

                return true;
            },
            IntPtr.Zero
        );

        lock (MonitorCacheSync)
        {
            MonitorCache = new Dictionary<string, Rect>(monitors, StringComparer.OrdinalIgnoreCase);
            MonitorScaleCache = scaleCache;
            MonitorCacheExpiresAtUtc = DateTimeOffset.UtcNow.Add(MonitorCacheDuration);
        }

        return monitors;
    }

    private static double GetMonitorScale(IntPtr hMonitor, string monitorDeviceName)
    {
        lock (MonitorCacheSync)
        {
            if (MonitorScaleCache.TryGetValue(monitorDeviceName, out var cachedScale))
            {
                return cachedScale;
            }
        }

        var dpiX = 0u;
        var dpiY = 0u;
        try
        {
            if (GetDpiForMonitor(hMonitor, MonitorDpiTypeEffectiveDpi, ref dpiX, ref dpiY) == 0 && dpiX > 0)
            {
                var scale = dpiX / 96.0;
                if (scale > 0)
                {
                    return scale;
                }
            }
        }
        catch
        {
            // Fall through to the 100% default below.
        }

        return 1.0;
    }

    private static void CenterWindowOnWorkArea(WidgetWindow window, Rect? workArea)
    {
        if (!workArea.HasValue)
        {
            var workAreaFallback = SystemParameters.WorkArea;
            window.Left = workAreaFallback.Left + ((workAreaFallback.Width - window.Width) / 2);
            window.Top = workAreaFallback.Top + ((workAreaFallback.Height - window.Height) / 2);
            return;
        }

        var centered = CenterRectInWorkArea(
            new Rect(window.Left, window.Top, window.Width, window.Height),
            workArea.Value
        );
        window.Left = centered.Left;
        window.Top = centered.Top;
    }

    private static void ClampWindowToWorkArea(WidgetWindow window, Rect? workArea)
    {
        if (!workArea.HasValue)
        {
            return;
        }

        var clamped = ClampRectToWorkArea(
            new Rect(window.Left, window.Top, window.Width, window.Height),
            workArea.Value
        );
        window.Left = clamped.Left;
        window.Top = clamped.Top;
        window.Width = clamped.Width;
        window.Height = clamped.Height;
    }

    /// <summary>Pure clamping: keeps the rect inside the work area, shrinking first if needed.</summary>
    internal static Rect ClampRectToWorkArea(Rect rect, Rect workArea)
    {
        var width = rect.Width > workArea.Width ? workArea.Width : rect.Width;
        var height = rect.Height > workArea.Height ? workArea.Height : rect.Height;
        var maxLeft = workArea.Right - width;
        var maxTop = workArea.Bottom - height;

        return new Rect(
            Math.Min(Math.Max(rect.Left, workArea.Left), maxLeft),
            Math.Min(Math.Max(rect.Top, workArea.Top), maxTop),
            width,
            height
        );
    }

    /// <summary>Pure centering: places the rect in the middle of the work area.</summary>
    internal static Rect CenterRectInWorkArea(Rect rect, Rect workArea)
    {
        return new Rect(
            workArea.Left + ((workArea.Width - rect.Width) / 2),
            workArea.Top + ((workArea.Height - rect.Height) / 2),
            rect.Width,
            rect.Height
        );
    }

    private static bool IsOnAnyScreen(double left, double top, double width, double height)
    {
        var virtualScreenBounds = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight
        );

        return IsRectOnAnyScreen(new Rect(left, top, width, height), virtualScreenBounds);
    }

    /// <summary>Pure on-screen test: does the rect overlap the given virtual screen bounds?</summary>
    internal static bool IsRectOnAnyScreen(Rect rect, Rect virtualScreenBounds)
    {
        var right = rect.Left + rect.Width;
        var bottom = rect.Top + rect.Height;

        return right > virtualScreenBounds.Left
               && rect.Left < virtualScreenBounds.Right
               && bottom > virtualScreenBounds.Top
               && rect.Top < virtualScreenBounds.Bottom;
    }

    /// <summary>
    ///     Pure physical-pixel to device-independent-pixel conversion so monitor rectangles
    ///     returned by Win32 can be converted (and unit tested) without P/Invoke.
    /// </summary>
    internal static Rect ConvertPhysicalRectToDip(int left, int top, int width, int height, double scale)
    {
        if (scale <= 0)
        {
            scale = 1.0;
        }

        return new Rect(left / scale, top / scale, width / scale, height / scale);
    }

    private static Rect? ResolveTargetMonitorWorkArea(string? monitorDeviceName)
    {
        if (string.IsNullOrWhiteSpace(monitorDeviceName))
        {
            return null;
        }

        Rect? result = null;
        EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (monitor, _, _, _) =>
            {
                var info = new MonitorInfoEx { CbSize = Marshal.SizeOf<MonitorInfoEx>() };
                if (!GetMonitorInfo(monitor, ref info))
                {
                    return true;
                }

                if (
                    string.Equals(
                        info.SzDevice,
                        monitorDeviceName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    var scale = GetMonitorScale(monitor, info.SzDevice);
                    result = ConvertPhysicalRectToDip(
                        info.RcWork.Left,
                        info.RcWork.Top,
                        info.RcWork.Right - info.RcWork.Left,
                        info.RcWork.Bottom - info.RcWork.Top,
                        scale
                    );
                    return false;
                }

                return true;
            },
            IntPtr.Zero
        );

        return result;
    }

    private static string? GetMonitorDeviceName(WidgetWindow window)
    {
        // Resolve the monitor from the window handle so no physical/DIP coordinate conversion is
        // needed before the monitor (and its DPI) is known.
        try
        {
            var windowHandle = new WindowInteropHelper(window).EnsureHandle();
            var monitor = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
            {
                return null;
            }

            var info = new MonitorInfoEx { CbSize = Marshal.SizeOf<MonitorInfoEx>() };
            return GetMonitorInfo(monitor, ref info) ? info.SzDevice : null;
        }
        catch
        {
            return null;
        }
    }

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

    private delegate bool MonitorEnumProc(
        IntPtr monitor,
        IntPtr hdc,
        IntPtr lprcMonitor,
        IntPtr dwData
    );
}
