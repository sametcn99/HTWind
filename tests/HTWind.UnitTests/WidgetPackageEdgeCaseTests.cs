using System.IO;
using System.Text.Json;

using HTWind.Model;
using HTWind.Services.Widget;

using Xunit;

namespace HTWind.UnitTests;

public sealed class WidgetPackageEdgeCaseTests : IDisposable
{
    private readonly Fixtures.TempDirectoryFixture _temp = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public void Dispose() => _temp.Dispose();

    private string CreatePackage(Action<WidgetPackageManifest>? customize = null)
    {
        var root = _temp.CreateSubdirectory("source");
        var widgetRoot = Directory.CreateDirectory(Path.Combine(root, "widgets", "clock")).FullName;
        File.WriteAllText(Path.Combine(widgetRoot, "index.html"), "<html>original</html>");
        var manifest = new WidgetPackageManifest
        {
            SchemaVersion = WidgetPackageConstants.SupportedSchemaVersion,
            Name = "Clocks",
            Widgets = [new WidgetPackageItemManifest
            {
                WidgetId = "clock-id", Name = "Clock", RelativePath = "widgets/clock",
                EntryFile = "index.html", Assets = []
            }]
        };
        customize?.Invoke(manifest);
        WidgetManifestHelper.WriteManifest(root, manifest, JsonOptions);
        return Path.Combine(root, WidgetPackageConstants.ManifestFileName);
    }

    [Theory]
    [InlineData("../outside.html")]
    [InlineData("nested/../../outside.html")]
    [InlineData("..\\outside.html")]
    public void Import_InvalidEntryPathsDoNotCreateAnyManagedFiles(string entryFile)
    {
        var manifestPath = CreatePackage(manifest => manifest.Widgets[0].EntryFile = entryFile);
        var service = new WidgetImportService(_temp.CreateSubdirectory("managed"));

        Assert.Throws<InvalidOperationException>(() => service.ImportWidgetSource(manifestPath));
        Assert.False(Directory.Exists(service.WidgetsDataDirectory));
        Assert.Equal("<html>original</html>", File.ReadAllText(Path.Combine(Path.GetDirectoryName(manifestPath)!, "widgets", "clock", "index.html")));
    }

    [Fact]
    public void Import_InvalidSecondWidgetDoesNotLeaveFirstWidgetPartiallyImported()
    {
        var manifestPath = CreatePackage(manifest => manifest.Widgets.Add(new WidgetPackageItemManifest
        {
            WidgetId = "broken-id", Name = "Broken", RelativePath = "missing", EntryFile = "index.html", Assets = []
        }));
        var service = new WidgetImportService(_temp.CreateSubdirectory("managed"));

        Assert.Throws<InvalidOperationException>(() => service.ImportWidgetSource(manifestPath));
        Assert.False(Directory.Exists(service.WidgetsDataDirectory));
    }

    [Theory]
    [InlineData("./widgets/clock")]
    [InlineData("widgets//clock")]
    [InlineData("widgets/clock/.")]
    public void Manifest_EquivalentWidgetRootsAreRejectedAsDuplicates(string alias)
    {
        var path = CreatePackage(manifest => manifest.Widgets.Add(new WidgetPackageItemManifest
        {
            WidgetId = "second-id", Name = "Second", RelativePath = alias, EntryFile = "index.html", Assets = []
        }));
        var manifest = WidgetManifestHelper.LoadManifest(path, JsonOptions);

        var error = Assert.Throws<InvalidOperationException>(() => WidgetManifestHelper.ValidateManifest(manifest, Path.GetDirectoryName(path)!));
        Assert.Contains("duplicated", error.Message);
    }

    [Fact]
    public void Manifest_NullWidgetRecordProducesValidationError()
    {
        var path = CreatePackage(manifest => manifest.Widgets.Add(null!));
        var manifest = WidgetManifestHelper.LoadManifest(path, JsonOptions);

        var error = Assert.Throws<InvalidOperationException>(() => WidgetManifestHelper.ValidateManifest(manifest, Path.GetDirectoryName(path)!));
        Assert.Contains("widgets[1]", error.Message);
    }

    [Fact]
    public void Manifest_MissingAndDuplicateAssetsAreReportedTogether()
    {
        var path = CreatePackage(manifest => manifest.Widgets[0].Assets = ["missing.css", "MISSING.css"]);
        var manifest = WidgetManifestHelper.LoadManifest(path, JsonOptions);

        var error = Assert.Throws<InvalidOperationException>(() => WidgetManifestHelper.ValidateManifest(manifest, Path.GetDirectoryName(path)!));
        Assert.Contains("asset does not exist", error.Message);
        Assert.Contains("duplicate asset path", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolveFileInsideRoot_AcceptsRootWithOrWithoutTrailingSeparator(bool trailingSeparator)
    {
        var root = _temp.CreateSubdirectory("root");
        var input = trailingSeparator ? root + Path.DirectorySeparatorChar : root;

        Assert.Equal(Path.Combine(root, "assets", "icon.png"), WidgetManifestHelper.ResolveFileInsideRoot(input, "assets/icon.png"));
        Assert.Equal(root, WidgetManifestHelper.ResolveWidgetRootPath(input, "."));
    }

    [Fact]
    public void Import_AlreadyManagedEntryIsIdempotentAndKeepsIdentity()
    {
        var service = new WidgetImportService(_temp.CreateSubdirectory("managed"));
        var created = service.CreateManagedWidget("Clock", "<html>original</html>");
        var before = Directory.GetDirectories(service.WidgetsDataDirectory);

        var loaded = Assert.Single(service.ImportWidgetSource(created.EntryFilePath));

        Assert.Equal(created.EntryFilePath, loaded.EntryFilePath);
        Assert.Equal(created.PackageWidgetId, loaded.PackageWidgetId);
        Assert.Equal(before, Directory.GetDirectories(service.WidgetsDataDirectory));
    }

    [Fact]
    public void Delete_PrefixSiblingAndTraversalCannotDeleteExternalContent()
    {
        var service = new WidgetImportService(_temp.CreateSubdirectory("managed"));
        var created = service.CreateManagedWidget("Clock", "<html>original</html>");
        var external = Directory.CreateDirectory(service.WidgetsDataDirectory + "-backup").FullName;
        var outsidePath = Path.Combine(external, "index.html");
        File.WriteAllText(outsidePath, "keep");

        service.DeleteManagedWidgetFile(outsidePath);
        service.DeleteManagedWidgetFile(Path.Combine(service.WidgetsDataDirectory, "..", "Widgets-backup", "index.html"));

        Assert.False(service.IsManagedWidgetPath(outsidePath));
        Assert.Equal("keep", File.ReadAllText(outsidePath));
        Assert.True(File.Exists(created.EntryFilePath));
    }
}
