using System.Runtime.InteropServices;
using System.Windows;

using HTWind.Model;

namespace HTWind.Services.Widget;

public sealed class WidgetGeometryService : IWidgetGeometryService
{
    private const int MonitorDefaultToNearest = 2;
    private static readonly TimeSpan MonitorCacheDuration = TimeSpan.FromMilliseconds(750);
    private static readonly object MonitorCacheSync = new();
    private static DateTimeOffset MonitorCacheExpiresAtUtc = DateTimeOffset.MinValue;
    private static Dictionary<string, Rect> MonitorCache =
        new(StringComparer.OrdinalIgnoreCase);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint pt, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

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
        if (
            !string.IsNullOrWhiteSpace(model.PreferredMonitorDeviceName)
            && connectedMonitors.ContainsKey(model.PreferredMonitorDeviceName)
        )
        {
            return model.PreferredMonitorDeviceName;
        }

        var fallback = model.MonitorPlacements
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
            !string.IsNullOrWhiteSpace(model.MonitorDeviceName)
            && connectedMonitors.ContainsKey(model.MonitorDeviceName)
        )
        {
            return model.MonitorDeviceName;
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
        }
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

                monitors[info.SzDevice] = new Rect(
                    info.RcWork.Left,
                    info.RcWork.Top,
                    info.RcWork.Right - info.RcWork.Left,
                    info.RcWork.Bottom - info.RcWork.Top
                );

                return true;
            },
            IntPtr.Zero
        );

        lock (MonitorCacheSync)
        {
            MonitorCache = new Dictionary<string, Rect>(monitors, StringComparer.OrdinalIgnoreCase);
            MonitorCacheExpiresAtUtc = DateTimeOffset.UtcNow.Add(MonitorCacheDuration);
        }

        return monitors;
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

        var target = workArea.Value;
        window.Left = target.Left + ((target.Width - window.Width) / 2);
        window.Top = target.Top + ((target.Height - window.Height) / 2);
    }

    private static void ClampWindowToWorkArea(WidgetWindow window, Rect? workArea)
    {
        if (!workArea.HasValue)
        {
            return;
        }

        var target = workArea.Value;

        if (window.Width > target.Width)
        {
            window.Width = target.Width;
        }

        if (window.Height > target.Height)
        {
            window.Height = target.Height;
        }

        var maxLeft = target.Right - window.Width;
        var maxTop = target.Bottom - window.Height;

        window.Left = Math.Min(Math.Max(window.Left, target.Left), maxLeft);
        window.Top = Math.Min(Math.Max(window.Top, target.Top), maxTop);
    }

    private static bool IsOnAnyScreen(double left, double top, double width, double height)
    {
        var right = left + width;
        var bottom = top + height;
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

        return right > virtualLeft
               && left < virtualRight
               && bottom > virtualTop
               && top < virtualBottom;
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
                    result = new Rect(
                        info.RcWork.Left,
                        info.RcWork.Top,
                        info.RcWork.Right - info.RcWork.Left,
                        info.RcWork.Bottom - info.RcWork.Top
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
        var centerX = window.Left + (window.Width / 2);
        var centerY = window.Top + (window.Height / 2);
        var monitor = MonitorFromPoint(
            new NativePoint { X = (int)Math.Round(centerX), Y = (int)Math.Round(centerY) },
            MonitorDefaultToNearest
        );
        if (monitor == IntPtr.Zero)
        {
            return null;
        }

        var info = new MonitorInfoEx { CbSize = Marshal.SizeOf<MonitorInfoEx>() };
        return GetMonitorInfo(monitor, ref info) ? info.SzDevice : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;

        public int Y;
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
