using System.Text.Json;
using System.Text.Json.Serialization;

namespace HTWind.Services;

public sealed class WidgetPackageManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = WidgetPackageConstants.SupportedSchemaVersion;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }

    [JsonPropertyName("createdUtc")]
    public string? CreatedUtc { get; set; }

    [JsonPropertyName("exportedUtc")]
    public string? ExportedUtc { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("requirements")]
    public JsonElement? Requirements { get; set; }

    [JsonPropertyName("dev")]
    public JsonElement? Dev { get; set; }

    [JsonPropertyName("widgets")]
    public List<WidgetPackageItemManifest> Widgets { get; set; } = [];
}

public sealed class WidgetPackageItemManifest
{
    [JsonPropertyName("widgetId")]
    public string WidgetId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("relativePath")]
    public string RelativePath { get; set; } = ".";

    [JsonPropertyName("entryFile")]
    public string EntryFile { get; set; } = WidgetPackageConstants.DefaultEntryFileName;

    [JsonPropertyName("assets")]
    public List<string> Assets { get; set; } = [];
}
