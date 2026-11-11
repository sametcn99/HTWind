using System.Windows;

using HTWind.Model;
using HTWind.Services.Widget;

using Xunit;

namespace HTWind.UnitTests;

/// <summary>Pure geometry logic: monitor resolution priority, DPI conversion, clamping, centering.</summary>
public sealed class WidgetGeometryMathTests
{
    private static readonly Dictionary<string, Rect> Connected = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DISPLAY1"] = new Rect(0, 0, 1920, 1040),
        ["DISPLAY2"] = new Rect(-1920, 0, 2560, 1400)
    };

    private static WidgetMonitorPlacement Placement(
        string device,
        DateTimeOffset lastSeen,
        double left = 0,
        double top = 0,
        double width = 400,
        double height = 300)
    {
        return new WidgetMonitorPlacement
        {
            MonitorDeviceName = device,
            LastSeenAtUtc = lastSeen,
            Left = left,
            Top = top,
            Width = width,
            Height = height
        };
    }

    #region Monitor resolution priority

    [Fact]
    public void Resolution_PrefersPreferredMonitor_WhenConnected()
    {
        var model = new WidgetModel
        {
            PreferredMonitorDeviceName = "DISPLAY2",
            MonitorDeviceName = "DISPLAY1",
            MonitorPlacements = [Placement("DISPLAY1", DateTimeOffset.UtcNow)]
        };

        var resolved = WidgetGeometryService.ResolveBestAvailableMonitorName(
            model.PreferredMonitorDeviceName,
            model.MonitorPlacements,
            model.MonitorDeviceName,
            Connected);

        Assert.Equal("DISPLAY2", resolved);
    }

    [Fact]
    public void Resolution_FallsBackToFreshestConnectedPlacement()
    {
        var older = DateTimeOffset.UtcNow.AddMinutes(-10);
        var fresher = DateTimeOffset.UtcNow;
        var model = new WidgetModel
        {
            PreferredMonitorDeviceName = "DISPLAY-REMOVED",
            MonitorDeviceName = "DISPLAY1",
            MonitorPlacements =
            [
                Placement("DISPLAY1", older),
                Placement("DISPLAY2", fresher)
            ]
        };

        var resolved = WidgetGeometryService.ResolveBestAvailableMonitorName(
            model.PreferredMonitorDeviceName,
            model.MonitorPlacements,
            model.MonitorDeviceName,
            Connected);

        Assert.Equal("DISPLAY2", resolved);
    }

    [Fact]
    public void Resolution_FallsBackToLastKnownMonitor_WhenConnected()
    {
        var model = new WidgetModel
        {
            PreferredMonitorDeviceName = "DISPLAY-REMOVED",
            MonitorDeviceName = "DISPLAY1",
            MonitorPlacements = [Placement("ALSO-REMOVED", DateTimeOffset.UtcNow)]
        };

        var resolved = WidgetGeometryService.ResolveBestAvailableMonitorName(
            model.PreferredMonitorDeviceName,
            model.MonitorPlacements,
            model.MonitorDeviceName,
            Connected);

        Assert.Equal("DISPLAY1", resolved);
    }

    [Fact]
    public void Resolution_ReturnsNull_WhenNoKnownMonitorIsConnected()
    {
        var model = new WidgetModel
        {
            PreferredMonitorDeviceName = "GONE-A",
            MonitorDeviceName = "GONE-B",
            MonitorPlacements = [Placement("GONE-C", DateTimeOffset.UtcNow)]
        };

        var resolved = WidgetGeometryService.ResolveBestAvailableMonitorName(
            model.PreferredMonitorDeviceName,
            model.MonitorPlacements,
            model.MonitorDeviceName,
            Connected);

        // The caller then centers the widget on the primary display.
        Assert.Null(resolved);
    }

    [Fact]
    public void Resolution_IgnoresPlacementsWithoutDeviceName()
    {
        var model = new WidgetModel
        {
            MonitorDeviceName = "DISPLAY1",
            MonitorPlacements = [Placement("", DateTimeOffset.UtcNow)]
        };

        var resolved = WidgetGeometryService.ResolveBestAvailableMonitorName(
            model.PreferredMonitorDeviceName,
            model.MonitorPlacements,
            model.MonitorDeviceName,
            Connected);

        Assert.Equal("DISPLAY1", resolved);
    }

    #endregion

    #region Clamping and centering

    [Fact]
    public void ClampRect_KeepsRectInsideWorkArea()
    {
        var workArea = new Rect(0, 0, 1920, 1040);

        var clamped = WidgetGeometryService.ClampRectToWorkArea(
            new Rect(1900, 1000, 300, 200),
            workArea);

        Assert.Equal(1920 - 300, clamped.Left);
        Assert.Equal(1040 - 200, clamped.Top);
        Assert.Equal(300, clamped.Width);
        Assert.Equal(200, clamped.Height);
    }

    [Fact]
    public void ClampRect_ShrinksOversizedRect()
    {
        var workArea = new Rect(0, 0, 800, 600);

        var clamped = WidgetGeometryService.ClampRectToWorkArea(
            new Rect(-100, -100, 1200, 900),
            workArea);

        Assert.Equal(0, clamped.Left);
        Assert.Equal(0, clamped.Top);
        Assert.Equal(800, clamped.Width);
        Assert.Equal(600, clamped.Height);
    }

    [Fact]
    public void ClampRect_NoOpForRectInsideWorkArea()
    {
        var workArea = new Rect(0, 0, 1920, 1040);
        var rect = new Rect(100, 100, 300, 200);

        var clamped = WidgetGeometryService.ClampRectToWorkArea(rect, workArea);

        Assert.Equal(rect, clamped);
    }

    [Fact]
    public void CenterRect_PlacesRectInTheMiddle()
    {
        var workArea = new Rect(0, 0, 1920, 1040);

        var centered = WidgetGeometryService.CenterRectInWorkArea(
            new Rect(0, 0, 400, 300),
            workArea);

        Assert.Equal((1920 - 400) / 2, centered.Left);
        Assert.Equal((1040 - 300) / 2, centered.Top);
    }

    [Theory]
    [InlineData(1.0, 0, 0, 1920, 1040)]
    [InlineData(1.25, 0, 0, 1536, 832)]
    [InlineData(1.5, 0, 0, 1280, 693.3333333333334)]
    [InlineData(2.0, 0, 0, 960, 520)]
    public void ConvertPhysicalRectToDip_AppliesScaleFactor(
        double scale,
        double expectedLeft,
        double expectedTop,
        double expectedWidth,
        double expectedHeight)
    {
        var dip = WidgetGeometryService.ConvertPhysicalRectToDip(0, 0, 1920, 1040, scale);

        Assert.Equal(expectedLeft, dip.Left);
        Assert.Equal(expectedTop, dip.Top);
        Assert.Equal(expectedWidth, dip.Width, 9);
        Assert.Equal(expectedHeight, dip.Height, 9);
    }

    [Fact]
    public void ConvertPhysicalRectToDip_FallsBackToUnityScale_WhenScaleIsInvalid()
    {
        var dip = WidgetGeometryService.ConvertPhysicalRectToDip(10, 20, 300, 400, 0);

        Assert.Equal(new Rect(10, 20, 300, 400), dip);
    }

    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(-5000, 100, false)]
    [InlineData(5000, 100, false)]
    [InlineData(100, -5000, false)]
    [InlineData(100, 5000, false)]
    [InlineData(-150, 100, true)]
    public void IsRectOnAnyScreen_DetectsOffScreenPositions(
        double left,
        double top,
        bool expectedOnScreen)
    {
        var virtualScreen = new Rect(-1920, 0, 3840, 1040);

        var onScreen = WidgetGeometryService.IsRectOnAnyScreen(
            new Rect(left, top, 300, 200),
            virtualScreen);

        Assert.Equal(expectedOnScreen, onScreen);
    }

    [Fact]
    public void IsRectOnAnyScreen_StraddlingRectCountsAsOnScreen()
    {
        var virtualScreen = new Rect(0, 0, 1920, 1040);

        var onScreen = WidgetGeometryService.IsRectOnAnyScreen(
            new Rect(1900, 500, 300, 200),
            virtualScreen);

        Assert.True(onScreen);
    }

    #endregion
}

/// <summary>Drag snapping and corner-resize math extracted from WidgetWindow.</summary>
public sealed class WidgetWindowMathTests
{
    private static readonly IReadOnlyList<Rect> WorkAreas =
    [
        new Rect(0, 0, 1920, 1040)
    ];

    #region Screen edge snapping (8 px threshold)

    [Fact]
    public void ScreenEdgeSnapping_SnapsLeftEdgeWithinThreshold()
    {
        var (left, top) = WidgetWindowMath.SnapToScreenEdges(4, 500, 300, 200, WorkAreas);
        Assert.Equal(0, left);
        Assert.Equal(500, top);
    }

    [Fact]
    public void ScreenEdgeSnapping_SnapsRightEdgeWithinThreshold()
    {
        var (left, _) = WidgetWindowMath.SnapToScreenEdges(1920 - 300 + 6, 500, 300, 200, WorkAreas);
        Assert.Equal(1920 - 300, left);
    }

    [Fact]
    public void ScreenEdgeSnapping_SnapsTopAndBottomEdgesWithinThreshold()
    {
        var (left, top) = WidgetWindowMath.SnapToScreenEdges(500, 3, 300, 200, WorkAreas);
        Assert.Equal(500, left);
        Assert.Equal(0, top);

        (_, top) = WidgetWindowMath.SnapToScreenEdges(500, 1040 - 200 + 7, 300, 200, WorkAreas);
        Assert.Equal(1040 - 200, top);
    }

    [Fact]
    public void ScreenEdgeSnapping_DoesNotSnapBeyondThreshold()
    {
        var (left, top) = WidgetWindowMath.SnapToScreenEdges(9, 9, 300, 200, WorkAreas);
        Assert.Equal(9, left);
        Assert.Equal(9, top);
    }

    #endregion

    #region Widget-to-widget snapping (8 px edges, 16 px center guides)

    private static readonly WidgetWindowRect Other = new(100, 100, 400, 300);

    [Fact]
    public void WidgetSnapping_AlignsLeftEdges()
    {
        var (left, top) = WidgetWindowMath.SnapToOtherWidget(104, 500, 300, 200, Other);
        Assert.Equal(100, left);
        Assert.Equal(500, top);
    }

    [Fact]
    public void WidgetSnapping_AlignsRightEdges()
    {
        var (left, _) = WidgetWindowMath.SnapToOtherWidget(100 + 400 - 300 + 5, 500, 300, 200, Other);
        Assert.Equal(100 + 400 - 300, left);
    }

    [Fact]
    public void WidgetSnapping_PlacesBesideOtherWidget()
    {
        var (left, _) = WidgetWindowMath.SnapToOtherWidget(100 + 400 + 6, 500, 300, 200, Other);
        Assert.Equal(100 + 400, left);
    }

    [Fact]
    public void WidgetSnapping_AlignsTopEdges()
    {
        var (_, top) = WidgetWindowMath.SnapToOtherWidget(600, 103, 300, 200, Other);
        Assert.Equal(100, top);
    }

    [Fact]
    public void WidgetSnapping_AlignsBottomEdges()
    {
        var (_, top) = WidgetWindowMath.SnapToOtherWidget(600, 100 + 300 - 200 + 4, 300, 200, Other);
        Assert.Equal(100 + 300 - 200, top);
    }

    [Fact]
    public void WidgetSnapping_AlignsHorizontalCenterGuides()
    {
        var other = new WidgetWindowRect(100, 100, 400, 300);
        var otherCenterX = other.Left + (other.Width / 2);
        // Position the dragged window so its center X is 10 px away from the other center
        // (inside the 16 px center-guide threshold).
        var draggedLeft = otherCenterX - (300 / 2) + 10;

        var (left, _) = WidgetWindowMath.SnapToOtherWidget(draggedLeft, 900, 300, 200, other);

        Assert.Equal(otherCenterX - (300 / 2), left);
    }

    [Fact]
    public void WidgetSnapping_AlignsVerticalCenterGuides()
    {
        var other = new WidgetWindowRect(100, 100, 400, 300);
        var otherCenterY = other.Top + (other.Height / 2);
        var draggedTop = otherCenterY - (200 / 2) + 12;

        var (_, top) = WidgetWindowMath.SnapToOtherWidget(900, draggedTop, 300, 200, other);

        Assert.Equal(otherCenterY - (200 / 2), top);
    }

    [Fact]
    public void WidgetSnapping_IgnoresPositionsBeyondThresholds()
    {
        var (left, top) = WidgetWindowMath.SnapToOtherWidget(600, 600, 300, 200, Other);
        Assert.Equal(600, left);
        Assert.Equal(600, top);
    }

    #endregion

    #region Corner resize (minimum size 160x100)

    [Theory]
    [InlineData((int)CornerResizeMode.BottomRight, -400, 0)]
    [InlineData((int)CornerResizeMode.TopRight, -400, 0)]
    [InlineData((int)CornerResizeMode.BottomLeft, 400, 0)]
    [InlineData((int)CornerResizeMode.TopLeft, 400, 0)]
    public void CornerResize_EnforcesMinimumWidth(int modeValue, double deltaX, double deltaY)
    {
        var result = WidgetWindowMath.ApplyCornerResize(
            (CornerResizeMode)modeValue,
            startLeft: 100,
            startTop: 100,
            startWidth: 200,
            startHeight: 200,
            deltaX,
            deltaY);

        Assert.Equal(WidgetWindowMath.MinWidgetWidth, result.Width);
    }

    [Theory]
    [InlineData((int)CornerResizeMode.BottomRight, 0, -300)]
    [InlineData((int)CornerResizeMode.TopRight, 0, 300)]
    [InlineData((int)CornerResizeMode.BottomLeft, 0, -300)]
    [InlineData((int)CornerResizeMode.TopLeft, 0, 300)]
    public void CornerResize_EnforcesMinimumHeight(int modeValue, double deltaX, double deltaY)
    {
        var result = WidgetWindowMath.ApplyCornerResize(
            (CornerResizeMode)modeValue,
            startLeft: 100,
            startTop: 100,
            startWidth: 200,
            startHeight: 200,
            deltaX,
            deltaY);

        Assert.Equal(WidgetWindowMath.MinWidgetHeight, result.Height);
    }

    [Fact]
    public void CornerResize_TopLeftAnchorsOppositeEdgeAtMinimum()
    {
        var result = WidgetWindowMath.ApplyCornerResize(
            CornerResizeMode.TopLeft,
            startLeft: 100,
            startTop: 100,
            startWidth: 200,
            startHeight: 300,
            deltaX: 300,
            deltaY: 0);

        // Width clamps to the minimum; the top-left corner moves so the bottom-right stays put.
        Assert.Equal(WidgetWindowMath.MinWidgetWidth, result.Width);
        Assert.Equal(100 + (200 - WidgetWindowMath.MinWidgetWidth), result.Left);
        Assert.Equal(300, result.Height);
    }

    [Fact]
    public void CornerResize_TopRightGrowsWidthAndHeight()
    {
        var result = WidgetWindowMath.ApplyCornerResize(
            CornerResizeMode.TopRight,
            startLeft: 100,
            startTop: 100,
            startWidth: 200,
            startHeight: 200,
            deltaX: 50,
            deltaY: -30);

        Assert.Equal(250, result.Width);
        Assert.Equal(100, result.Left);
        Assert.Equal(230, result.Height);
        Assert.Equal(70, result.Top);
    }

    [Fact]
    public void CornerResize_BottomRightGrowsBothDimensions()
    {
        var result = WidgetWindowMath.ApplyCornerResize(
            CornerResizeMode.BottomRight,
            startLeft: 100,
            startTop: 100,
            startWidth: 200,
            startHeight: 200,
            deltaX: 40,
            deltaY: 60);

        Assert.Equal(100, result.Left);
        Assert.Equal(100, result.Top);
        Assert.Equal(240, result.Width);
        Assert.Equal(260, result.Height);
    }

    [Fact]
    public void CornerResize_BottomLeftClampsWidthAndAnchorsRightEdge()
    {
        var result = WidgetWindowMath.ApplyCornerResize(
            CornerResizeMode.BottomLeft,
            startLeft: 100,
            startTop: 100,
            startWidth: 200,
            startHeight: 200,
            deltaX: 300,
            deltaY: 50);

        Assert.Equal(WidgetWindowMath.MinWidgetWidth, result.Width);
        Assert.Equal(100 + (200 - WidgetWindowMath.MinWidgetWidth), result.Left);
        Assert.Equal(250, result.Height);
        Assert.Equal(100, result.Top);
    }

    #endregion
}
