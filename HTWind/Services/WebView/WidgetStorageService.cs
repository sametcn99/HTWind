using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

using HTWind.Services.Application;

namespace HTWind.Services.WebView;

/// <summary>
///     Persists per-widget JSON settings in a hidden file inside the widget's own folder so the
///     configuration survives WebView2 profile resets. Powers <c>storage.get</c> /
///     <c>storage.set</c> and the manifest settingsSchema settings form.
/// </summary>
public sealed class WidgetStorageService
{
    public const string SettingsFileName = ".htwind-settings.json";
    public const string SettingsKey = "settings";

    internal static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public object? Get(string? widgetFilePath, JsonElement? args)
    {
        var settingsPath = ResolveSettingsPath(widgetFilePath);
        var key = ReadStringArg(args, "key");

        if (!File.Exists(settingsPath))
        {
            return key is null ? ToJsonElement("{}") : null;
        }

        try
        {
            var json = File.ReadAllText(settingsPath);
            var node = JsonNode.Parse(json);
            if (node is null)
            {
                return null;
            }

            if (key is null)
            {
                return ToJsonElement(node.ToJsonString(SerializerOptions));
            }

            var value = node is JsonObject obj ? obj[key] : null;
            return value is null ? null : ToJsonElement(value.ToJsonString(SerializerOptions));
        }
        catch
        {
            return null;
        }
    }

    public object? Set(string? widgetFilePath, JsonElement? args)
    {
        var settingsPath = ResolveSettingsPath(widgetFilePath);
        var key = ReadStringArg(args, "key");

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("'key' is required for storage.set.");
        }

        JsonObject root;
        if (File.Exists(settingsPath))
        {
            try
            {
                root = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject ?? [];
            }
            catch
            {
                root = [];
            }
        }
        else
        {
            root = [];
        }

        if (args.HasValue && args.Value.ValueKind == JsonValueKind.Object && args.Value.TryGetProperty("value", out var valueElement))
        {
            // Re-parse the raw JSON so stored values keep their types; GetRawText alone would
            // store strings double-encoded (the quotes become part of the stored value).
            root[key] = valueElement.ValueKind == JsonValueKind.Null
                ? null
                : JsonNode.Parse(valueElement.GetRawText());
        }
        else
        {
            root[key] = null;
        }

        WriteAtomic(settingsPath, root);

        var stored = root[key];
        return stored is null ? null : ToJsonElement(stored.ToJsonString(SerializerOptions));
    }

    /// <summary>Reads the values saved by the widget settings form (manifest settingsSchema).</summary>
    public JsonObject? GetSettings(string? widgetFilePath)
    {
        var settingsPath = ResolveSettingsPath(widgetFilePath);
        if (!File.Exists(settingsPath))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(settingsPath)) is JsonObject root
                ? root[SettingsKey] as JsonObject
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Persists the widget settings form values under the reserved "settings" key.</summary>
    public void SaveSettings(string? widgetFilePath, JsonObject settings)
    {
        var settingsPath = ResolveSettingsPath(widgetFilePath);

        JsonObject root;
        if (File.Exists(settingsPath))
        {
            try
            {
                root = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject ?? [];
            }
            catch
            {
                root = [];
            }
        }
        else
        {
            root = [];
        }

        root[SettingsKey] = settings;
        WriteAtomic(settingsPath, root);
    }

    private static void WriteAtomic(string settingsPath, JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        AtomicFile.WriteAllText(settingsPath, root.ToJsonString(SerializerOptions));
    }

    private static string ResolveSettingsPath(string? widgetFilePath)
    {
        if (string.IsNullOrWhiteSpace(widgetFilePath))
        {
            throw new InvalidOperationException("The widget file path is required for storage APIs.");
        }

        var fullPath = Path.GetFullPath(widgetFilePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new InvalidOperationException("The widget folder does not exist for storage APIs.");
        }

        // Widgets with their own package folder use the shared settings file name; widgets that
        // share a folder (e.g. built-in templates) get a per-file settings file so their storage
        // never collides.
        var hasOwnPackageFolder = File.Exists(
            Path.Combine(directory, HTWind.Model.WidgetPackageConstants.ManifestFileName));

        return hasOwnPackageFolder
            ? Path.Combine(directory, SettingsFileName)
            : Path.Combine(directory, Path.GetFileName(fullPath) + "." + SettingsFileName);
    }

    private static string? ReadStringArg(JsonElement? args, string key)
    {
        if (!args.HasValue || args.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return args.Value.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static JsonElement ToJsonElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
