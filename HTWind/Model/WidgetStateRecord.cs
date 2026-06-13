namespace HTWind.Model;

public sealed class WidgetStateRecord
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? FilePath { get; set; }

    public bool IsVisible { get; set; }

    public bool IsLocked { get; set; }

    public bool IsPinned { get; set; }

    public double? Left { get; set; }

    public double? Top { get; set; }

    public double? WidgetWidth { get; set; }

    public double? WidgetHeight { get; set; }

    public string? MonitorDeviceName { get; set; }

    public string? PreferredMonitorDeviceName { get; set; }

    public List<WidgetMonitorPlacement> MonitorPlacements { get; set; } = [];
}
