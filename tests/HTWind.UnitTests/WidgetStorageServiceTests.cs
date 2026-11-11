using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

using HTWind.Services.WebView;

using Xunit;

namespace HTWind.UnitTests;

/// <summary>
///     Per-widget storage identity: package folders share one hidden settings file while
///     widgets sharing a folder get per-file storage, and the reserved "settings" key never
///     collides with widget-owned storage keys.
/// </summary>
public sealed class WidgetStorageServiceTests : IClassFixture<Fixtures.TempDirectoryFixture>
{
    private readonly Fixtures.TempDirectoryFixture _temp;

    public WidgetStorageServiceTests(Fixtures.TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    /// <summary>Builds a storage API args element from raw JSON (key plus optional value).</summary>
    private static JsonElement Args(string rawJson)
    {
        return JsonSerializer.Deserialize<JsonElement>(rawJson);
    }

    private static string KeyArgs(string key) => $$"""{"key":"{{key}}"}""";

    private static string ValueArgs(string key, string rawJsonValue) =>
        $$"""{"key":"{{key}}","value":{{rawJsonValue}}}""";

    [Fact]
    public void PackageFolder_WidgetsShareOneSettingsFile()
    {
        var widgetFolder = _temp.CreateSubdirectory("pkg-widget");
        File.WriteAllText(
            Path.Combine(widgetFolder, HTWind.Model.WidgetPackageConstants.ManifestFileName),
            "{}");
        var service = new WidgetStorageService();

        service.Set(Path.Combine(widgetFolder, "index.html"), Args(ValueArgs("color", "\"red\"")));
        service.Set(Path.Combine(widgetFolder, "panel.html"), Args(ValueArgs("count", "3")));

        // Both widgets in the package folder write the same hidden settings file.
        var settingsPath = Path.Combine(widgetFolder, WidgetStorageService.SettingsFileName);
        Assert.True(File.Exists(settingsPath));
        Assert.False(File.Exists(Path.Combine(widgetFolder, "index.html" + "." + WidgetStorageService.SettingsFileName)));
        var root = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
        Assert.NotNull(root["color"]);
        // storage.set re-parses the raw JSON so values keep their types.
        Assert.Equal(3, root["count"]?.GetValue<int>());
    }

    [Fact]
    public void SharedFolder_WidgetsGetPerFileStorageIdentity()
    {
        var sharedFolder = _temp.CreateSubdirectory("shared-templates");
        File.WriteAllText(Path.Combine(sharedFolder, "a.html"), "<html></html>");
        File.WriteAllText(Path.Combine(sharedFolder, "b.html"), "<html></html>");
        var service = new WidgetStorageService();

        service.Set(Path.Combine(sharedFolder, "a.html"), Args(ValueArgs("who", "\"a\"")));
        service.Set(Path.Combine(sharedFolder, "b.html"), Args(ValueArgs("who", "\"b\"")));

        // Widgets without their own package folder get per-file settings files.
        var aSettingsPath = Path.Combine(sharedFolder, "a.html" + "." + WidgetStorageService.SettingsFileName);
        var bSettingsPath = Path.Combine(sharedFolder, "b.html" + "." + WidgetStorageService.SettingsFileName);
        Assert.True(File.Exists(aSettingsPath));
        Assert.True(File.Exists(bSettingsPath));

        var aSettings = JsonNode.Parse(File.ReadAllText(aSettingsPath))!.AsObject();
        Assert.True(aSettings.ContainsKey("who"));
        Assert.False(JsonNode.Parse(File.ReadAllText(bSettingsPath))!.AsObject().ContainsKey("b-only"));
    }

    [Fact]
    public void StorageSet_DoesNotClobberSettingsKey_AndViceVersa()
    {
        var widgetFolder = _temp.CreateSubdirectory("settings-key-widget");
        File.WriteAllText(
            Path.Combine(widgetFolder, HTWind.Model.WidgetPackageConstants.ManifestFileName),
            "{}");
        var entryPath = Path.Combine(widgetFolder, "index.html");
        var service = new WidgetStorageService();

        // The settings form writes the reserved "settings" key...
        service.SaveSettings(entryPath, new JsonObject { ["fontSize"] = 14 });
        // ...then the widget stores its own keys through storage.set.
        service.Set(entryPath, Args(ValueArgs("lastTab", "\"overview\"")));

        var stored = service.Get(entryPath, Args(KeyArgs("lastTab")));
        Assert.NotNull(stored);
        Assert.Equal(JsonValueKind.String, ((JsonElement)stored).ValueKind);

        var settings = service.GetSettings(entryPath);
        Assert.NotNull(settings);
        Assert.Equal(14, settings!["fontSize"]?.GetValue<int>());

        // Both directions preserve each other's data.
        service.SaveSettings(entryPath, new JsonObject { ["fontSize"] = 16 });
        Assert.Equal(
            ((JsonElement)stored!).ToString(),
            ((JsonElement)service.Get(entryPath, Args(KeyArgs("lastTab")))!).ToString());
        Assert.Equal(16, service.GetSettings(entryPath)!["fontSize"]?.GetValue<int>());

        service.Set(entryPath, Args(ValueArgs("lastTab", "\"editor\"")));
        Assert.Equal(16, service.GetSettings(entryPath)!["fontSize"]?.GetValue<int>());
        Assert.NotEqual(
            ((JsonElement)stored).ToString(),
            ((JsonElement)service.Get(entryPath, Args(KeyArgs("lastTab")))!).ToString());
    }

    [Fact]
    public void SetThenGet_NumericValue_RoundTripsWithItsType()
    {
        var widgetFolder = _temp.CreateSubdirectory("numeric-storage-widget");
        var entryPath = Path.Combine(widgetFolder, "index.html");
        var service = new WidgetStorageService();

        service.Set(entryPath, Args(ValueArgs("count", "42")));

        // The value keeps its JSON type across Set/Get.
        var value = (JsonElement)service.Get(entryPath, Args(KeyArgs("count")))!;
        Assert.Equal(JsonValueKind.Number, value.ValueKind);
        Assert.Equal(42, value.GetInt32());
    }

    [Fact]
    public void SetThenGet_StringValue_RoundTripsThroughTheSameRepresentation()
    {
        var widgetFolder = _temp.CreateSubdirectory("string-storage-widget");
        var entryPath = Path.Combine(widgetFolder, "index.html");
        var service = new WidgetStorageService();

        var set = (JsonElement)service.Set(entryPath, Args(ValueArgs("who", "\"a\"")))!;
        var get = (JsonElement)service.Get(entryPath, Args(KeyArgs("who")))!;

        // Set and Get expose the identical representation for the same key.
        Assert.Equal(set.ToString(), get.ToString());
    }

    [Fact]
    public void Get_MissingFile_ReturnsEmptyOrKeyNull()
    {
        var widgetFolder = _temp.CreateSubdirectory("empty-storage-widget");
        var entryPath = Path.Combine(widgetFolder, "index.html");
        var service = new WidgetStorageService();

        var wholeStore = service.Get(entryPath, null);
        Assert.Equal("{}", wholeStore?.ToString());

        var specificKey = service.Get(entryPath, Args(KeyArgs("anything")));
        Assert.Null(specificKey);
    }

    [Fact]
    public void Set_WithoutKey_Throws()
    {
        var widgetFolder = _temp.CreateSubdirectory("no-key-widget");
        var entryPath = Path.Combine(widgetFolder, "index.html");
        var service = new WidgetStorageService();

        Assert.Throws<InvalidOperationException>(() => service.Set(entryPath, null));
    }

    [Fact]
    public void Set_WithExplicitNullValue_StoresNullJson()
    {
        var widgetFolder = _temp.CreateSubdirectory("null-value-widget");
        File.WriteAllText(
            Path.Combine(widgetFolder, HTWind.Model.WidgetPackageConstants.ManifestFileName),
            "{}");
        var entryPath = Path.Combine(widgetFolder, "index.html");
        var service = new WidgetStorageService();

        var result = service.Set(entryPath, Args(ValueArgs("maybe", "null")));

        // A null value round-trips as an explicit null entry (not a missing key).
        Assert.Null(result);
        var root = JsonNode.Parse(File.ReadAllText(
            Path.Combine(widgetFolder, WidgetStorageService.SettingsFileName)))!.AsObject();
        Assert.True(root.ContainsKey("maybe"));
        Assert.Null(root["maybe"]);
    }
}
