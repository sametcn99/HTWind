using System.Runtime.InteropServices;
using System.Windows;

namespace HTWind.Services;

public sealed class WidgetGeometryService : IWidgetGeometryService
{
    private const int MonitorDefaultToNearest = 2;

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

    public void CaptureGeometry(WidgetWindow window, WidgetModel model)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(model);

        model.Left = window.Left;
        model.Top = window.Top;
        model.WidgetWidth = window.Width;
        model.WidgetHeight = window.Height;
        model.MonitorDeviceName = GetMonitorDeviceName(window);
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

        var targetWorkArea = ResolveTargetMonitorWorkArea(model.MonitorDeviceName);

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
