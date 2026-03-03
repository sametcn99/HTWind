namespace HTWind.Services;

public sealed class WidgetStateSnapshot
{
    public List<WidgetStateRecord> Widgets { get; set; } = [];

    public bool SuppressWidgetsOnFullscreen { get; set; } = true;

    public bool SuppressWidgetsOnMaximized { get; set; }
}
