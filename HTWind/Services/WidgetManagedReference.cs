namespace HTWind.Services;

public sealed class WidgetManagedReference
{
    public string WidgetName { get; init; } = string.Empty;

    public string EntryFilePath { get; init; } = string.Empty;

    public string WidgetRootPath { get; init; } = string.Empty;

    public string? PackageWidgetId { get; init; }
}
