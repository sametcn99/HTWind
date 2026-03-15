using System.Text.Json.Serialization;

namespace HTWind.Services;

public sealed class WidgetWorkspaceExportManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = WidgetPackageConstants.SupportedSchemaVersion;

    [JsonPropertyName("exportType")]
    public string ExportType { get; set; } = "workspace";

    [JsonPropertyName("exportedUtc")]
    public string ExportedUtc { get; set; } = DateTime.UtcNow.ToString("O");

    [JsonPropertyName("widgets")]
    public List<WidgetWorkspaceExportItem> Widgets { get; set; } = [];
}

public sealed class WidgetWorkspaceExportItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("relativePath")]
    public string RelativePath { get; set; } = string.Empty;

    [JsonPropertyName("entryFile")]
    public string EntryFile { get; set; } = string.Empty;
}
