using System.Windows;

namespace HTWind;

/// <summary>Which corner grips are being dragged during a widget resize.</summary>
internal enum CornerResizeMode
{
    None,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

/// <summary>Data-only rectangle of another widget window, used by the snap math.</summary>
internal readonly record struct WidgetWindowRect(double Left, double Top, double Width, double Height)
{
    public bool IsValid =>
        !double.IsNaN(Left)
        && !double.IsNaN(Top)
        && !double.IsNaN(Width)
        && !double.IsNaN(Height)
        && Width > 0
        && Height > 0;
}

/// <summary>
///     Pure drag-snap and corner-resize math extracted from <see cref="WidgetWindow"/> so unit
///     tests can exercise the thresholds without instantiating windows. Screen and other-widget
///     edges snap at 8 px, center alignment guides at 16 px; widgets cannot shrink below
///     160x100.
/// </summary>
internal static class WidgetWindowMath
{
    public const double ScreenEdgeSnapThreshold = 8;
    public const double WidgetEdgeSnapThreshold = 8;
    public const double CenterGuideSnapThreshold = 16;
    public const double MinWidgetWidth = 160;
    public const double MinWidgetHeight = 100;

    /// <summary>
    ///     Snaps the window position to screen work-area edges (left/right/top/bottom) when
    ///     within <see cref="ScreenEdgeSnapThreshold"/>. Applies every work area in order.
    /// </summary>
    public static (double Left, double Top) SnapToScreenEdges(
        double left,
        double top,
        double width,
        double height,
        IReadOnlyList<Rect> workAreas
    )
    {
        foreach (var workArea in workAreas)
        {
            if (Math.Abs(left - workArea.Left) <= ScreenEdgeSnapThreshold)
            {
                left = workArea.Left;
            }

            if (Math.Abs((left + width) - workArea.Right) <= ScreenEdgeSnapThreshold)
            {
                left = workArea.Right - width;
            }

            if (Math.Abs(top - workArea.Top) <= ScreenEdgeSnapThreshold)
            {
                top = workArea.Top;
            }

            if (Math.Abs((top + height) - workArea.Bottom) <= ScreenEdgeSnapThreshold)
            {
                top = workArea.Bottom - height;
            }
        }

        return (left, top);
    }

    /// <summary>
    ///     Snaps the window position against one other widget: edge alignment (both sides, both
    ///     axes) plus center alignment guides at <see cref="CenterGuideSnapThreshold"/>.
    /// </summary>
    public static (double Left, double Top) SnapToOtherWidget(
        double left,
        double top,
        double width,
        double height,
        WidgetWindowRect other
    )
    {
        // Widget-to-widget edge snapping.
        if (Math.Abs(left - other.Left) <= WidgetEdgeSnapThreshold)
        {
            left = other.Left;
        }

        if (Math.Abs((left + width) - (other.Left + other.Width)) <= WidgetEdgeSnapThreshold)
        {
            left = other.Left + other.Width - width;
        }

        if (Math.Abs((left + width) - other.Left) <= WidgetEdgeSnapThreshold)
        {
            left = other.Left - width;
        }

        if (Math.Abs(left - (other.Left + other.Width)) <= WidgetEdgeSnapThreshold)
        {
            left = other.Left + other.Width;
        }

        if (Math.Abs(top - other.Top) <= WidgetEdgeSnapThreshold)
        {
            top = other.Top;
        }

        if (Math.Abs((top + height) - (other.Top + other.Height)) <= WidgetEdgeSnapThreshold)
        {
            top = other.Top + other.Height - height;
        }

        if (Math.Abs((top + height) - other.Top) <= WidgetEdgeSnapThreshold)
        {
            top = other.Top - height;
        }

        if (Math.Abs(top - (other.Top + other.Height)) <= WidgetEdgeSnapThreshold)
        {
            top = other.Top + other.Height;
        }

        // Center alignment guides.
        var centerX = left + (width / 2);
        var otherCenterX = other.Left + (other.Width / 2);
        if (Math.Abs(centerX - otherCenterX) <= CenterGuideSnapThreshold)
        {
            left = otherCenterX - (width / 2);
        }

        var centerY = top + (height / 2);
        var otherCenterY = other.Top + (other.Height / 2);
        if (Math.Abs(centerY - otherCenterY) <= CenterGuideSnapThreshold)
        {
            top = otherCenterY - (height / 2);
        }

        return (left, top);
    }

    /// <summary>
    ///     Pure corner-resize math: applies the drag delta for the given resize mode, then
    ///     enforces the minimum size of 160x100, re-anchoring the opposite edge when a top or
    ///     left grip is dragged past the minimum.
    /// </summary>
    public static (double Left, double Top, double Width, double Height) ApplyCornerResize(
        CornerResizeMode mode,
        double startLeft,
        double startTop,
        double startWidth,
        double startHeight,
        double deltaX,
        double deltaY
    )
    {
        var nextLeft = startLeft;
        var nextTop = startTop;
        var nextWidth = startWidth;
        var nextHeight = startHeight;

        if (mode == CornerResizeMode.TopLeft || mode == CornerResizeMode.BottomLeft)
        {
            nextWidth = startWidth - deltaX;
            nextLeft = startLeft + deltaX;
        }

        if (mode == CornerResizeMode.TopRight || mode == CornerResizeMode.BottomRight)
        {
            nextWidth = startWidth + deltaX;
        }

        if (mode == CornerResizeMode.TopLeft || mode == CornerResizeMode.TopRight)
        {
            nextHeight = startHeight - deltaY;
            nextTop = startTop + deltaY;
        }

        if (mode == CornerResizeMode.BottomLeft || mode == CornerResizeMode.BottomRight)
        {
            nextHeight = startHeight + deltaY;
        }

        if (nextWidth < MinWidgetWidth)
        {
            nextWidth = MinWidgetWidth;
            if (mode == CornerResizeMode.TopLeft || mode == CornerResizeMode.BottomLeft)
            {
                nextLeft = startLeft + (startWidth - MinWidgetWidth);
            }
        }

        if (nextHeight < MinWidgetHeight)
        {
            nextHeight = MinWidgetHeight;
            if (mode == CornerResizeMode.TopLeft || mode == CornerResizeMode.TopRight)
            {
                nextTop = startTop + (startHeight - MinWidgetHeight);
            }
        }

        return (nextLeft, nextTop, nextWidth, nextHeight);
    }
}
