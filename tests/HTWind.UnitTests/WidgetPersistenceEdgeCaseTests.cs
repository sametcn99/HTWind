using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

using HTWind.Model;
using HTWind.Services.Application;
using HTWind.Services.WebView;
using HTWind.Services.Widget;

using Xunit;

namespace HTWind.UnitTests;

public sealed class WidgetPersistenceEdgeCaseTests : IDisposable
{
    private readonly Fixtures.TempDirectoryFixture _temp = new();
    private readonly WidgetStorageService _storage = new();
    private string WidgetPath => Path.Combine(_temp.Path, "widget.html");
    private string StoragePath => Path.Combine(_temp.Path, "widget.html." + WidgetStorageService.SettingsFileName);

    public void Dispose() => _temp.Dispose();
    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Theory]
    [InlineData("true", JsonValueKind.True)]
    [InlineData("false", JsonValueKind.False)]
    [InlineData("0", JsonValueKind.Number)]
    [InlineData("-123.5", JsonValueKind.Number)]
    [InlineData("\"quotes \\\" and İstanbul 🚀\"", JsonValueKind.String)]
    [InlineData("[1,null,\"two\",false]", JsonValueKind.Array)]
    [InlineData("{\"nested\":{\"values\":[1,2]}}", JsonValueKind.Object)]
    public void Storage_RoundTripsJsonTypesAcrossServiceInstances(string value, JsonValueKind kind)
    {
        var result = Assert.IsType<JsonElement>(_storage.Set(WidgetPath, Json($$"""{"key":"value","value":{{value}}}""")));
        var loaded = Assert.IsType<JsonElement>(new WidgetStorageService().Get(WidgetPath, Json("""{"key":"value"}""")));

        Assert.Equal(kind, result.ValueKind);
        Assert.Equal(kind, loaded.ValueKind);
        Assert.True(JsonElement.DeepEquals(Json(value), loaded));
        Assert.Empty(Directory.GetFiles(_temp.Path, "*.tmp"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("\"key\"")]
    [InlineData("{}")]
    [InlineData("{\"key\":null}")]
    [InlineData("{\"key\":5}")]
    [InlineData("{\"key\":\"  \"}")]
    public void Storage_InvalidKeyDoesNotModifyExistingStore(string args)
    {
        _storage.Set(WidgetPath, Json("""{"key":"keep","value":42}"""));
        var before = File.ReadAllText(StoragePath);

        Assert.Throws<InvalidOperationException>(() => _storage.Set(WidgetPath, Json(args)));
        Assert.Equal(before, File.ReadAllText(StoragePath));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("123")]
    public void Storage_SetRecoversMalformedOrNonObjectRoot(string json)
    {
        File.WriteAllText(StoragePath, json);

        _storage.Set(WidgetPath, Json("""{"key":"recovered","value":true}"""));

        var root = Assert.IsType<JsonElement>(_storage.Get(WidgetPath, null));
        Assert.True(root.GetProperty("recovered").GetBoolean());
        Assert.Single(root.EnumerateObject());
    }

    [Fact]
    public void Storage_MissingValuePersistsExplicitNullAndKeepsOtherKeys()
    {
        _storage.Set(WidgetPath, Json("""{"key":"keep","value":7}"""));
        Assert.Null(_storage.Set(WidgetPath, Json("""{"key":"nullable"}""")));

        var root = Assert.IsType<JsonElement>(_storage.Get(WidgetPath, null));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("nullable").ValueKind);
        Assert.Equal(7, root.GetProperty("keep").GetInt32());
    }

    [Fact]
    public void Settings_CanBeLoadedEditedAndSavedRepeatedlyWithoutReparentingErrors()
    {
        _storage.Set(WidgetPath, Json("""{"key":"widgetData","value":[1,2]}"""));
        var settings = new JsonObject { ["theme"] = "dark" };
        _storage.SaveSettings(WidgetPath, settings);
        Assert.Null(settings.Parent);

        settings["theme"] = "light";
        Assert.Equal("dark", _storage.GetSettings(WidgetPath)!["theme"]!.GetValue<string>());
        _storage.SaveSettings(WidgetPath, settings);

        var loaded = _storage.GetSettings(WidgetPath)!;
        loaded["fontSize"] = 16;
        _storage.SaveSettings(WidgetPath, loaded);

        Assert.Equal("light", _storage.GetSettings(WidgetPath)!["theme"]!.GetValue<string>());
        Assert.Equal(16, _storage.GetSettings(WidgetPath)!["fontSize"]!.GetValue<int>());
        var root = Assert.IsType<JsonElement>(_storage.Get(WidgetPath, null));
        Assert.Equal(2, root.GetProperty("widgetData").GetArrayLength());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Storage_MissingWidgetPathIsRejected(string? path)
    {
        Assert.Throws<InvalidOperationException>(() => _storage.Get(path, null));
        Assert.Throws<InvalidOperationException>(() => _storage.Set(path, Json("""{"key":"x"}""")));
        Assert.Throws<InvalidOperationException>(() => _storage.GetSettings(path));
        Assert.Throws<InvalidOperationException>(() => _storage.SaveSettings(path, new JsonObject()));
    }

    [Theory]
    [InlineData("{\"Widgets\":null,\"Culture\":\"tr-TR\"}")]
    [InlineData("{\"Widgets\":[null],\"Culture\":\"tr-TR\"}")]
    public void State_NullWidgetCollectionsAndRecordsAreSafeToEnumerate(string json)
    {
        File.WriteAllText(HTWindPathProvider.GetStateFilePath(_temp.Path), json);

        var snapshot = new WidgetStateStore(_temp.Path).Load();

        Assert.Empty(snapshot.Widgets);
        Assert.Equal("tr-TR", snapshot.Culture);
    }

    [Theory]
    [InlineData(false, "null", 0)]
    [InlineData(true, "null", 0)]
    [InlineData(false, "[null,{\"MonitorDeviceName\":\"DISPLAY1\"}]", 1)]
    [InlineData(true, "[null,{\"MonitorDeviceName\":\"DISPLAY1\"}]", 1)]
    public void State_RestoresValidWidgetsDespiteNullMonitorPlacements(bool legacy, string placements, int expectedPlacements)
    {
        File.WriteAllText(WidgetPath, "<html>kept</html>");
        var widgets = $$"""[null,{"Id":"kept","FilePath":{{JsonSerializer.Serialize(WidgetPath)}},"MonitorPlacements":{{placements}}}]""";
        var json = legacy ? widgets : $$"""{"Widgets":{{widgets}}}""";
        File.WriteAllText(HTWindPathProvider.GetStateFilePath(_temp.Path), json);
        var lifecycle = new WidgetLifecycleService(new WidgetStateStore(_temp.Path), new WidgetImportService(_temp.Path));

        lifecycle.LoadPersistedWidgets((_, _) => false, (_, _) => Assert.Fail("The saved widget is hidden."));

        var restored = Assert.Single(lifecycle.Widgets);
        Assert.Equal("kept", restored.Id);
        Assert.Equal(expectedPlacements, restored.MonitorPlacements.Count);
        Assert.All(restored.MonitorPlacements, placement => Assert.Equal("DISPLAY1", placement.MonitorDeviceName));
    }

    [Fact]
    public void State_SerializationFailurePreservesLastKnownGoodSnapshot()
    {
        var store = new WidgetStateStore(_temp.Path);
        store.Save(new WidgetStateSnapshot { ThemeOption = "Dark" });
        var before = File.ReadAllText(HTWindPathProvider.GetStateFilePath(_temp.Path));

        store.Save(new WidgetStateSnapshot
        {
            Widgets = [new WidgetStateRecord { Left = double.NaN }]
        });

        Assert.Equal(before, File.ReadAllText(HTWindPathProvider.GetStateFilePath(_temp.Path)));
        Assert.Equal("Dark", store.Load().ThemeOption);
        Assert.Empty(Directory.GetFiles(_temp.Path, "*.tmp"));
    }
}
