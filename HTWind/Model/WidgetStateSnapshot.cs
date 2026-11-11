namespace HTWind.Model;

public sealed class WidgetStateSnapshot
{
    public List<WidgetStateRecord> Widgets { get; set; } = [];

    public bool SuppressWidgetsOnFullscreen { get; set; } = true;

    public bool SuppressWidgetsOnMaximized { get; set; }

    /// <summary>Persisted preferred theme ("Device", "Light" or "Dark"); null means default.</summary>
    public string? ThemeOption { get; set; }

    /// <summary>Persisted UI culture name (e.g. "en-US"); null means the default culture.</summary>
    public string? Culture { get; set; }
}
