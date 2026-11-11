using System.IO;

using HTWind.Services.Widget;

using Microsoft.Web.WebView2.Core;

using Xunit;

namespace HTWind.UnitTests;

/// <summary>Permission decisions persist per widget identity (package folder or single file).</summary>
public sealed class WidgetPermissionStateServiceTests : IClassFixture<Fixtures.TempDirectoryFixture>
{
    private readonly Fixtures.TempDirectoryFixture _temp;

    public WidgetPermissionStateServiceTests(Fixtures.TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    private WidgetPermissionStateService CreateService()
    {
        return new WidgetPermissionStateService(_temp.Path);
    }

    private string CreateManagedWidgetFolder(string name)
    {
        var folder = _temp.CreateSubdirectory(name);
        File.WriteAllText(
            Path.Combine(folder, HTWind.Model.WidgetPackageConstants.ManifestFileName),
            "{}");
        File.WriteAllText(Path.Combine(folder, "index.html"), "<html></html>");
        return folder;
    }

    private string CreateSharedFolderWidget(string folderName, string fileName)
    {
        var folder = _temp.CreateSubdirectory(folderName);
        var path = Path.Combine(folder, fileName);
        File.WriteAllText(path, "<html></html>");
        return path;
    }

    [Fact]
    public void PackageFolderWidgets_ResolveIdentityByFolder()
    {
        var folder = CreateManagedWidgetFolder("perm-pkg");
        var service = CreateService();

        service.SavePowerShellDecision(
            Path.Combine(folder, "index.html"),
            PowerShellExecutionDecision.ReadOnly);

        // A different entry file of the same package folder shares the identity.
        Assert.True(service.TryGetPowerShellDecision(
            Path.Combine(folder, "panel.html"),
            out var decision));
        Assert.Equal(PowerShellExecutionDecision.ReadOnly, decision);
    }

    [Fact]
    public void SharedFolderWidgets_ResolveIdentityByFile()
    {
        var pathA = CreateSharedFolderWidget("perm-shared", "a.html");
        var pathB = CreateSharedFolderWidget("perm-shared", "b.html");
        var service = CreateService();

        service.SavePowerShellDecision(pathA, PowerShellExecutionDecision.Full);

        Assert.True(service.TryGetPowerShellDecision(pathA, out var decisionA));
        Assert.Equal(PowerShellExecutionDecision.Full, decisionA);
        Assert.False(service.TryGetPowerShellDecision(pathB, out _));
    }

    [Fact]
    public void GenericPermissionDecisions_PersistAllowAndDeny()
    {
        var folder = CreateManagedWidgetFolder("perm-generic");
        var widgetPath = Path.Combine(folder, "index.html");
        var service = CreateService();

        service.SaveDecision(widgetPath, CoreWebView2PermissionKind.Geolocation, CoreWebView2PermissionState.Allow);
        service.SaveDecision(widgetPath, CoreWebView2PermissionKind.Camera, CoreWebView2PermissionState.Deny);

        Assert.True(service.TryGetDecision(widgetPath, CoreWebView2PermissionKind.Geolocation, out var geolocation));
        Assert.Equal(CoreWebView2PermissionState.Allow, geolocation);
        Assert.True(service.TryGetDecision(widgetPath, CoreWebView2PermissionKind.Camera, out var camera));
        Assert.Equal(CoreWebView2PermissionState.Deny, camera);
        Assert.False(service.TryGetDecision(widgetPath, CoreWebView2PermissionKind.Microphone, out _));

        // Decisions survive a fresh service instance reading the same store.
        var reloaded = CreateService();
        Assert.True(reloaded.TryGetDecision(widgetPath, CoreWebView2PermissionKind.Geolocation, out var persisted));
        Assert.Equal(CoreWebView2PermissionState.Allow, persisted);
    }

    [Fact]
    public void SaveDecision_IgnoresNonFinalStates()
    {
        var path = CreateSharedFolderWidget("perm-nonfinal", "w.html");
        var service = CreateService();

        service.SaveDecision(path, CoreWebView2PermissionKind.Geolocation, CoreWebView2PermissionState.Default);

        Assert.False(service.TryGetDecision(path, CoreWebView2PermissionKind.Geolocation, out _));
    }

    [Theory]
    [InlineData(PowerShellExecutionDecision.ReadOnly)]
    [InlineData(PowerShellExecutionDecision.Full)]
    [InlineData(PowerShellExecutionDecision.Deny)]
    public void PowerShellDecisions_PersistAcrossInstances(PowerShellExecutionDecision decision)
    {
        var path = CreateSharedFolderWidget($"perm-ps-{decision}", "w.html");
        var service = CreateService();

        service.SavePowerShellDecision(path, decision);

        var reloaded = CreateService();
        Assert.True(reloaded.TryGetPowerShellDecision(path, out var persisted));
        Assert.Equal(decision, persisted);
    }

    [Fact]
    public void ClearAll_RemovesEveryDecisionAndPersists()
    {
        var folder = CreateManagedWidgetFolder("perm-clear");
        var widgetPath = Path.Combine(folder, "index.html");
        var service = CreateService();

        service.SaveDecision(widgetPath, CoreWebView2PermissionKind.Geolocation, CoreWebView2PermissionState.Allow);
        service.SavePowerShellDecision(widgetPath, PowerShellExecutionDecision.ReadOnly);

        service.ClearAll();

        Assert.False(service.TryGetDecision(widgetPath, CoreWebView2PermissionKind.Geolocation, out _));
        Assert.False(service.TryGetPowerShellDecision(widgetPath, out _));

        var reloaded = CreateService();
        Assert.False(reloaded.TryGetPowerShellDecision(widgetPath, out _));
    }

    [Fact]
    public void Constructor_UsesInjectedBasePath()
    {
        var folder = CreateManagedWidgetFolder("perm-basepath");
        var widgetPath = Path.Combine(folder, "index.html");
        var service = CreateService();

        service.SavePowerShellDecision(widgetPath, PowerShellExecutionDecision.ReadOnly);

        Assert.True(File.Exists(Path.Combine(_temp.Path, "widget-permissions.json")));
    }
}
