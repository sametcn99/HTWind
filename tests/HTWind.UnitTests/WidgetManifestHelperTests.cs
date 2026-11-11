using System.IO;
using System.Text.Json;

using HTWind.Model;
using HTWind.Services.Widget;

using Xunit;

namespace HTWind.UnitTests;

/// <summary>Security-focused tests for widget manifest path and field validation.</summary>
public sealed class WidgetManifestHelperTests : IClassFixture<Fixtures.TempDirectoryFixture>
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly Fixtures.TempDirectoryFixture _temp;

    public WidgetManifestHelperTests(Fixtures.TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("a/../../secret.txt")]
    [InlineData("..")]
    public void ResolveFileInsideRoot_PathTraversal_IsRejected(string relativePath)
    {
        var root = _temp.CreateSubdirectory("widget-root");
        Assert.Throws<InvalidOperationException>(() =>
            WidgetManifestHelper.ResolveFileInsideRoot(root, relativePath));
    }

    [Theory]
    [InlineData("C:\\evil\\payload.html")]
    [InlineData("\\evil\\payload.html")]
    [InlineData("\\\\unc\\share\\payload.html")]
    public void ResolveFileInsideRoot_RootedPaths_AreRejected(string relativePath)
    {
        var root = _temp.CreateSubdirectory("widget-root-rooted");
        Assert.Throws<InvalidOperationException>(() =>
            WidgetManifestHelper.ResolveFileInsideRoot(root, relativePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveFileInsideRoot_EmptyPaths_AreRejected(string relativePath)
    {
        var root = _temp.CreateSubdirectory("widget-root-empty");
        Assert.Throws<InvalidOperationException>(() =>
            WidgetManifestHelper.ResolveFileInsideRoot(root, relativePath));
    }

    [Fact]
    public void NormalizeAndValidateRelativePath_AllowHtmlFalse_RejectsHtmlFiles()
    {
        Assert.Throws<InvalidOperationException>(() =>
            WidgetManifestHelper.NormalizeAndValidateRelativePath("page.html", allowHtml: false));
        Assert.Throws<InvalidOperationException>(() =>
            WidgetManifestHelper.NormalizeAndValidateRelativePath("assets/page.HTML", allowHtml: false));
    }

    [Fact]
    public void NormalizeAndValidateRelativePath_AllowHtmlTrue_AcceptsHtmlFiles()
    {
        var normalized = WidgetManifestHelper.NormalizeAndValidateRelativePath(
            "assets\\index.html",
            allowHtml: true);
        Assert.Equal("assets/index.html", normalized);
    }

    private static WidgetPackageManifest CreateValidManifest(string widgetRootRelativePath)
    {
        return new WidgetPackageManifest
        {
            SchemaVersion = WidgetPackageConstants.SupportedSchemaVersion,
            Name = "sample",
            Widgets =
            [
                new WidgetPackageItemManifest
                {
                    WidgetId = "id-1",
                    Name = "sample",
                    RelativePath = widgetRootRelativePath,
                    EntryFile = "index.html",
                    Assets = []
                }
            ]
        };
    }

    private string CreateWidgetPackage(Action<WidgetPackageManifest>? customize = null)
    {
        var packageRoot = _temp.CreateSubdirectory($"package-{Guid.NewGuid():N}");
        var widgetFolder = Directory.CreateDirectory(
            System.IO.Path.Combine(packageRoot, "widgets", "sample")).FullName;
        File.WriteAllText(System.IO.Path.Combine(widgetFolder, "index.html"), "<html></html>");
        var manifest = CreateValidManifest("widgets/sample");
        customize?.Invoke(manifest);
        WidgetManifestHelper.WriteManifest(packageRoot, manifest, ManifestJsonOptions);
        return packageRoot;
    }

    [Fact]
    public void ValidateManifest_UnsupportedSchemaVersion_IsRejected()
    {
        var packageRoot = CreateWidgetPackage(manifest => manifest.SchemaVersion = 999);
        var manifest = WidgetManifestHelper.LoadManifest(
            System.IO.Path.Combine(packageRoot, WidgetPackageConstants.ManifestFileName),
            ManifestJsonOptions);

        var error = Assert.Throws<InvalidOperationException>(() =>
            WidgetManifestHelper.ValidateManifest(manifest, packageRoot));
        Assert.Contains("schemaVersion", error.Message);
    }

    [Fact]
    public void ValidateManifest_MissingName_IsRejected()
    {
        var packageRoot = CreateWidgetPackage(manifest => manifest.Name = string.Empty);
        var manifest = WidgetManifestHelper.LoadManifest(
            System.IO.Path.Combine(packageRoot, WidgetPackageConstants.ManifestFileName),
            ManifestJsonOptions);

        var error = Assert.Throws<InvalidOperationException>(() =>
            WidgetManifestHelper.ValidateManifest(manifest, packageRoot));
        Assert.Contains("name is required", error.Message);
    }

    [Fact]
    public void ValidateManifest_MissingWidgetId_IsRejected()
    {
        var packageRoot = CreateWidgetPackage(manifest => manifest.Widgets[0].WidgetId = string.Empty);
        var manifest = WidgetManifestHelper.LoadManifest(
            System.IO.Path.Combine(packageRoot, WidgetPackageConstants.ManifestFileName),
            ManifestJsonOptions);

        var error = Assert.Throws<InvalidOperationException>(() =>
            WidgetManifestHelper.ValidateManifest(manifest, packageRoot));
        Assert.Contains("widgetId is required", error.Message);
    }

    [Fact]
    public void ValidateManifest_MissingWidgetName_IsRejected()
    {
        var packageRoot = CreateWidgetPackage(manifest => manifest.Widgets[0].Name = string.Empty);
        var manifest = WidgetManifestHelper.LoadManifest(
            System.IO.Path.Combine(packageRoot, WidgetPackageConstants.ManifestFileName),
            ManifestJsonOptions);

        var error = Assert.Throws<InvalidOperationException>(() =>
            WidgetManifestHelper.ValidateManifest(manifest, packageRoot));
        Assert.Contains("name is required", error.Message);
    }

    [Fact]
    public void ValidateManifest_DuplicateRelativePath_IsRejected()
    {
        var packageRoot = CreateWidgetPackage(manifest =>
        {
            var clone = new WidgetPackageItemManifest
            {
                WidgetId = "id-2",
                Name = "sample-two",
                RelativePath = manifest.Widgets[0].RelativePath,
                EntryFile = manifest.Widgets[0].EntryFile,
                Assets = []
            };
            manifest.Widgets.Add(clone);
        });
        var manifest = WidgetManifestHelper.LoadManifest(
            System.IO.Path.Combine(packageRoot, WidgetPackageConstants.ManifestFileName),
            ManifestJsonOptions);

        var error = Assert.Throws<InvalidOperationException>(() =>
            WidgetManifestHelper.ValidateManifest(manifest, packageRoot));
        Assert.Contains("duplicated", error.Message);
    }

    [Theory]
    [InlineData("javascript:void(0)")]
    [InlineData("ftp://example.com")]
    public void ValidateManifest_InvalidHomepageScheme_IsRejected(string homepage)
    {
        var packageRoot = CreateWidgetPackage(manifest => manifest.Homepage = homepage);
        var manifest = WidgetManifestHelper.LoadManifest(
            System.IO.Path.Combine(packageRoot, WidgetPackageConstants.ManifestFileName),
            ManifestJsonOptions);

        var error = Assert.Throws<InvalidOperationException>(() =>
            WidgetManifestHelper.ValidateManifest(manifest, packageRoot));
        Assert.Contains("homepage", error.Message);
    }

    [Fact]
    public void ValidateManifest_UndeclaredFile_IsRejected()
    {
        var packageRoot = CreateWidgetPackage();
        File.WriteAllText(
            System.IO.Path.Combine(packageRoot, "widgets", "sample", "smuggled.js"),
            "alert(1)");
        var manifest = WidgetManifestHelper.LoadManifest(
            System.IO.Path.Combine(packageRoot, WidgetPackageConstants.ManifestFileName),
            ManifestJsonOptions);

        var error = Assert.Throws<InvalidOperationException>(() =>
            WidgetManifestHelper.ValidateManifest(manifest, packageRoot));
        Assert.Contains("undeclared file", error.Message);
    }

    [Fact]
    public void ValidateManifest_ValidManifest_Passes()
    {
        var packageRoot = CreateWidgetPackage();
        var manifest = WidgetManifestHelper.LoadManifest(
            System.IO.Path.Combine(packageRoot, WidgetPackageConstants.ManifestFileName),
            ManifestJsonOptions);

        WidgetManifestHelper.ValidateManifest(manifest, packageRoot);
    }
}
