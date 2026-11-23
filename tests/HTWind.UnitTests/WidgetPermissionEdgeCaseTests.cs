using System.IO;
using System.Text.Json;

using HTWind.Services.Widget;
using HTWind.UnitTests.Fixtures;

using Microsoft.Web.WebView2.Core;

using Xunit;

namespace HTWind.UnitTests;

/// <summary>Exercises permission persistence against an isolated on-disk store per test.</summary>
public sealed class WidgetPermissionEdgeCaseTests
{
    private static WidgetPermissionStateService CreateService(TempDirectoryFixture temp) =>
        new(temp.Path);

    private static string CreateWidgetFile(TempDirectoryFixture temp, string relativePath)
    {
        return temp.CreateFile(relativePath, "<html></html>");
    }

    [Fact]
    public void MalformedJson_IsIgnoredAndALaterSaveReplacesIt()
    {
        using var temp = new TempDirectoryFixture();
        temp.CreateFile("widget-permissions.json", "{ not valid json");
        var path = CreateWidgetFile(temp, "widget.html");
        var service = CreateService(temp);

        Assert.False(service.TryGetDecision(
            path,
            CoreWebView2PermissionKind.Geolocation,
            out _));

        service.SaveDecision(path, CoreWebView2PermissionKind.Geolocation, CoreWebView2PermissionState.Allow);

        var reloaded = CreateService(temp);
        Assert.True(reloaded.TryGetDecision(path, CoreWebView2PermissionKind.Geolocation, out var state));
        Assert.Equal(CoreWebView2PermissionState.Allow, state);
    }

    [Fact]
    public void NullRecord_DoesNotPreventLoadingFollowingValidRecords()
    {
        using var temp = new TempDirectoryFixture();
        var validPath = Path.Combine(temp.Path, "valid.html");
        temp.CreateFile("widget-permissions.json", $$"""
            [
              { "WidgetFilePath": {{JsonSerializer.Serialize(validPath)}}, "PermissionKind": "Geolocation", "State": "Allow" },
              null,
              { "WidgetFilePath": {{JsonSerializer.Serialize(Path.Combine(temp.Path, "later.html"))}}, "PermissionKind": "Camera", "State": "Deny" }
            ]
            """);
        var service = CreateService(temp);

        Assert.True(service.TryGetDecision(validPath, CoreWebView2PermissionKind.Geolocation, out var first));
        Assert.Equal(CoreWebView2PermissionState.Allow, first);

        var laterPath = Path.Combine(temp.Path, "later.html");
        Assert.True(service.TryGetDecision(laterPath, CoreWebView2PermissionKind.Camera, out var later));
        Assert.Equal(CoreWebView2PermissionState.Deny, later);
    }

    [Fact]
    public void InvalidRecords_AreSkippedWhileValidRecordsRemainAvailable()
    {
        using var temp = new TempDirectoryFixture();
        var validPath = Path.Combine(temp.Path, "valid.html");
        var unknownKindPath = Path.Combine(temp.Path, "unknown-kind.html");
        var numericKindPath = Path.Combine(temp.Path, "numeric-kind.html");
        var numericStatePath = Path.Combine(temp.Path, "numeric-state.html");
        var unknownPowerShellPath = Path.Combine(temp.Path, "unknown-powershell.html");
        var numericPowerShellPath = Path.Combine(temp.Path, "numeric-powershell.html");
        temp.CreateFile("widget-permissions.json", $$"""
            [
              { "WidgetFilePath": {{JsonSerializer.Serialize(validPath)}}, "PermissionKind": "Geolocation", "State": "Allow" },
              { "PermissionKind": "Geolocation", "State": "Deny" },
              { "WidgetFilePath": {{JsonSerializer.Serialize(unknownKindPath)}}, "PermissionKind": "NotARealPermission", "State": "Allow" },
              { "WidgetFilePath": {{JsonSerializer.Serialize(numericKindPath)}}, "PermissionKind": "9999", "State": "Allow" },
              { "WidgetFilePath": {{JsonSerializer.Serialize(numericStatePath)}}, "PermissionKind": "Camera", "State": "9999" },
              { "WidgetFilePath": {{JsonSerializer.Serialize(unknownPowerShellPath)}}, "PermissionKind": "PowerShellExecution", "State": "NotARealDecision" },
              { "WidgetFilePath": {{JsonSerializer.Serialize(numericPowerShellPath)}}, "PermissionKind": "PowerShellExecution", "State": "9999" }
            ]
            """);
        var service = CreateService(temp);

        Assert.True(service.TryGetDecision(validPath, CoreWebView2PermissionKind.Geolocation, out var valid));
        Assert.Equal(CoreWebView2PermissionState.Allow, valid);
        Assert.False(service.TryGetDecision(unknownKindPath, CoreWebView2PermissionKind.Geolocation, out _));
        Assert.False(service.TryGetDecision(numericKindPath, (CoreWebView2PermissionKind)9999, out _));
        Assert.False(service.TryGetDecision(numericStatePath, CoreWebView2PermissionKind.Camera, out _));
        Assert.False(service.TryGetPowerShellDecision(unknownPowerShellPath, out _));
        Assert.False(service.TryGetPowerShellDecision(numericPowerShellPath, out _));
    }

    [Theory]
    [InlineData(CoreWebView2PermissionState.Allow)]
    [InlineData(CoreWebView2PermissionState.Deny)]
    public void NonFinalState_DoesNotOverwriteAnExistingFinalDecision(CoreWebView2PermissionState existingState)
    {
        using var temp = new TempDirectoryFixture();
        var path = CreateWidgetFile(temp, "widget.html");
        var service = CreateService(temp);

        service.SaveDecision(path, CoreWebView2PermissionKind.Geolocation, existingState);
        service.SaveDecision(path, CoreWebView2PermissionKind.Geolocation, CoreWebView2PermissionState.Default);

        Assert.True(service.TryGetDecision(path, CoreWebView2PermissionKind.Geolocation, out var actual));
        Assert.Equal(existingState, actual);
        var reloaded = CreateService(temp);
        Assert.True(reloaded.TryGetDecision(path, CoreWebView2PermissionKind.Geolocation, out var persisted));
        Assert.Equal(existingState, persisted);
    }

    [Fact]
    public void Decisions_AreIsolatedByWidgetAndPermissionKind()
    {
        using var temp = new TempDirectoryFixture();
        var firstWidget = CreateWidgetFile(temp, "first.html");
        var secondWidget = CreateWidgetFile(temp, "second.html");
        var service = CreateService(temp);

        service.SaveDecision(firstWidget, CoreWebView2PermissionKind.Geolocation, CoreWebView2PermissionState.Allow);
        service.SaveDecision(firstWidget, CoreWebView2PermissionKind.Camera, CoreWebView2PermissionState.Deny);
        service.SaveDecision(secondWidget, CoreWebView2PermissionKind.Geolocation, CoreWebView2PermissionState.Deny);

        Assert.True(service.TryGetDecision(firstWidget, CoreWebView2PermissionKind.Geolocation, out var firstGeo));
        Assert.Equal(CoreWebView2PermissionState.Allow, firstGeo);
        Assert.True(service.TryGetDecision(firstWidget, CoreWebView2PermissionKind.Camera, out var firstCamera));
        Assert.Equal(CoreWebView2PermissionState.Deny, firstCamera);
        Assert.True(service.TryGetDecision(secondWidget, CoreWebView2PermissionKind.Geolocation, out var secondGeo));
        Assert.Equal(CoreWebView2PermissionState.Deny, secondGeo);
        Assert.False(service.TryGetDecision(secondWidget, CoreWebView2PermissionKind.Camera, out _));
    }

    [Fact]
    public void ClearThenSave_PersistsOnlyTheNewDecision()
    {
        using var temp = new TempDirectoryFixture();
        var path = CreateWidgetFile(temp, "widget.html");
        var service = CreateService(temp);
        service.SaveDecision(path, CoreWebView2PermissionKind.Geolocation, CoreWebView2PermissionState.Allow);
        service.SavePowerShellDecision(path, PowerShellExecutionDecision.Full);

        service.ClearAll();
        Assert.False(File.Exists(Path.Combine(temp.Path, "widget-permissions.json")));
        service.SaveDecision(path, CoreWebView2PermissionKind.Camera, CoreWebView2PermissionState.Deny);

        var reloaded = CreateService(temp);
        Assert.False(reloaded.TryGetDecision(path, CoreWebView2PermissionKind.Geolocation, out _));
        Assert.True(reloaded.TryGetDecision(path, CoreWebView2PermissionKind.Camera, out var camera));
        Assert.Equal(CoreWebView2PermissionState.Deny, camera);
        Assert.False(reloaded.TryGetPowerShellDecision(path, out _));
    }

    [Fact]
    public void RelativeAndCaseChangedPaths_ResolveToTheSameWidgetDecision()
    {
        using var temp = new TempDirectoryFixture();
        var path = CreateWidgetFile(temp, "widget.html");
        var service = CreateService(temp);
        service.SaveDecision(path, CoreWebView2PermissionKind.Geolocation, CoreWebView2PermissionState.Allow);

        var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, path);
        Assert.True(service.TryGetDecision(relativePath, CoreWebView2PermissionKind.Geolocation, out var relativeState));
        Assert.Equal(CoreWebView2PermissionState.Allow, relativeState);
        Assert.True(service.TryGetDecision(path.ToUpperInvariant(), CoreWebView2PermissionKind.Geolocation, out var caseState));
        Assert.Equal(CoreWebView2PermissionState.Allow, caseState);
    }
}
