using System.IO;
using System.Text.Json;

using HTWind.Services.WebView;
using Xunit;

namespace HTWind.RuntimeTests;

public sealed class WidgetHostApiEdgeCaseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "htwind-host-api-", Guid.NewGuid().ToString("N"));
    private readonly WidgetHostApiService _api = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_RejectsMissingCommand(string? command)
    {
        var result = await _api.ExecuteAsync(command!, null);

        Assert.False(result.Success);
        Assert.Equal("Command is required.", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsUnsupportedCommand()
    {
        var result = await _api.ExecuteAsync("unknown.command", Json("{}"));

        Assert.False(result.Success);
        Assert.Equal("Unsupported command: unknown.command", result.Error);
    }

    [Fact]
    public async Task StorageGet_ReturnsEmptyObjectWhenWidgetHasNoStoredData()
    {
        var widget = CreateWidget("empty");

        var result = await _api.ExecuteAsync("storage.get", null, Context(widget));

        Assert.True(result.Success);
        Assert.Equal("{}", AsJson(result.Result));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-string")]
    public async Task StorageSet_RejectsMissingOrWronglyTypedKey(object? key)
    {
        var widget = CreateWidget("bad-key");
        var args = key is null ? "{}" : "{\"key\":42}";

        var result = await _api.ExecuteAsync("storage.set", Json(args), Context(widget));

        Assert.False(result.Success);
        Assert.Equal("'key' is required for storage.set.", result.Error);
    }

    [Fact]
    public async Task StorageApis_RequireExistingWidgetContext()
    {
        var noContext = await _api.ExecuteAsync("storage.get", null);
        Assert.False(noContext.Success);
        Assert.Equal("The widget file path is required for storage APIs.", noContext.Error);

        var missingFolder = await _api.ExecuteAsync(
            "storage.set",
            Json("{\"key\":\"x\",\"value\":1}"),
            Context(Path.Combine(_root, "missing", "widget.html")));
        Assert.False(missingFolder.Success);
        Assert.Equal("The widget folder does not exist for storage APIs.", missingFolder.Error);
    }

    [Fact]
    public async Task StorageSetAndGet_PreserveJsonValueTypesAndMissingKeys()
    {
        var widget = CreateWidget("roundtrip");
        var context = Context(widget);

        var set = await _api.ExecuteAsync("storage.set", Json("{\"key\":\"count\",\"value\":7}"), context);
        Assert.True(set.Success);
        Assert.Equal("7", AsJson(set.Result));

        var get = await _api.ExecuteAsync("storage.get", Json("{\"key\":\"count\"}"), context);
        Assert.True(get.Success);
        Assert.Equal("7", AsJson(get.Result));

        var absent = await _api.ExecuteAsync("storage.get", Json("{\"key\":\"missing\"}"), context);
        Assert.True(absent.Success);
        Assert.Null(absent.Result);

        var persisted = await _api.ExecuteAsync("storage.get", null, context);
        using var document = JsonDocument.Parse(AsJson(persisted.Result));
        Assert.True(JsonElement.DeepEquals(Json("{\"count\":7}")!.Value, document.RootElement));
    }

    [Fact]
    public async Task StorageGet_ReturnsNullForMalformedStoredJson()
    {
        var widget = CreateWidget("malformed");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(widget)!, WidgetStorageService.SettingsFileName), "{");

        var result = await _api.ExecuteAsync("storage.get", null, Context(widget));

        Assert.True(result.Success);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task PowerShell_RequiresPerWidgetAuthorizationAndNonemptyScript()
    {
        var unauthorized = await _api.ExecuteAsync("powershell.exec", Json("{\"script\":\"1\"}"), Context(CreateWidget("unauthorized")));
        Assert.False(unauthorized.Success);
        Assert.Equal("PowerShell execution requires per-widget authorization.", unauthorized.Error);

        var noScript = await _api.ExecuteAsync("powershell.exec", Json("{\"script\":\"  \"}"),
            new WidgetExecutionContext { PowerShellAuthorization = PowerShellExecutionAuthorization.Full });
        Assert.False(noScript.Success);
        Assert.Equal("'script' is required for powershell.exec.", noScript.Error);
    }

    [Fact]
    public async Task PowerShell_NormalizesShellAndClampsOutputLimitAndWorkingDirectory()
    {
        var cwd = Path.GetDirectoryName(CreateWidget("working-dir"))!;
        var result = await _api.ExecuteAsync(
            "powershell.exec",
            Json("{\"script\":\"Write-Output ('abcdefgh' * 10)\",\"shell\":\"  mystery  \",\"maxOutputChars\":\"5\",\"timeoutMs\":\"1\",\"workingDirectory\":\"  " + EscapeJson(cwd) + "  \"}"),
            new WidgetExecutionContext { PowerShellAuthorization = PowerShellExecutionAuthorization.Full });

        Assert.True(result.Success, result.Error);
        using var output = JsonDocument.Parse(JsonSerializer.Serialize(result.Result));
        var root = output.RootElement;
        Assert.Equal(5, root.GetProperty("Output").GetString()!.Length);
        Assert.True(root.GetProperty("OutputTruncated").GetBoolean());
        Assert.Equal("inproc-pwsh", root.GetProperty("Shell").GetString());
        Assert.Equal(cwd, root.GetProperty("WorkingDirectory").GetString());
    }

    [Fact]
    public async Task PowerShell_ReadOnlyAuthorizationUsesRestrictedMode()
    {
        var result = await _api.ExecuteAsync(
            "powershell.exec",
            Json("{\"script\":\"Get-Location\",\"shell\":\"pwsh\"}"),
            new WidgetExecutionContext { PowerShellAuthorization = PowerShellExecutionAuthorization.ReadOnly });

        Assert.True(result.Success, result.Error);
        using var output = JsonDocument.Parse(JsonSerializer.Serialize(result.Result));
        Assert.Equal("inproc-readonly", output.RootElement.GetProperty("Shell").GetString());
    }

    [Fact]
    public void ArgumentReaders_HandleAbsentWrongKindsAndStringIntegers()
    {
        Assert.Null(WidgetHostApiArgs.ReadStringArg(null, "name"));
        Assert.Null(WidgetHostApiArgs.ReadStringArg(Json("[]"), "name"));
        Assert.Null(WidgetHostApiArgs.ReadStringArg(Json("{\"name\":1}"), "name"));
        Assert.Equal("value", WidgetHostApiArgs.ReadStringArg(Json("{\"name\":\"value\"}"), "name"));

        Assert.Null(WidgetHostApiArgs.ReadIntArg(null, "n"));
        Assert.Null(WidgetHostApiArgs.ReadIntArg(Json("[]"), "n"));
        Assert.Null(WidgetHostApiArgs.ReadIntArg(Json("{\"n\":1.5}"), "n"));
        Assert.Null(WidgetHostApiArgs.ReadIntArg(Json("{\"n\":\"bad\"}"), "n"));
        Assert.Equal(7, WidgetHostApiArgs.ReadIntArg(Json("{\"n\":7}"), "n"));
        Assert.Equal(8, WidgetHostApiArgs.ReadIntArg(Json("{\"n\":\"8\"}"), "n"));
    }

    private string CreateWidget(string name)
    {
        var folder = Path.Combine(_root, name);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "widget.html");
        File.WriteAllText(path, "<html></html>");
        File.WriteAllText(Path.Combine(folder, HTWind.Model.WidgetPackageConstants.ManifestFileName), "{}");
        return path;
    }

    private static WidgetExecutionContext Context(string path) => new() { WidgetFilePath = path };

    private static JsonElement? Json(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string AsJson(object? value) => JsonSerializer.Serialize(value);

    private static string EscapeJson(string value) => JsonSerializer.Serialize(value)[1..^1];

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
