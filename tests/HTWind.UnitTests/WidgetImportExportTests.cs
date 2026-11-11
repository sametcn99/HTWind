using System.IO;
using System.Text.Json;

using HTWind.Model;
using HTWind.Services.Widget;

using Xunit;

namespace HTWind.UnitTests;

/// <summary>Import/export behavior including reference extraction and round-trip fidelity.</summary>
public sealed class WidgetImportExportTests : IClassFixture<Fixtures.TempDirectoryFixture>
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly Fixtures.TempDirectoryFixture _temp;

    public WidgetImportExportTests(Fixtures.TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    private WidgetImportService CreateImportService()
    {
        return new WidgetImportService(_temp.Path);
    }

    private WidgetExportService CreateExportService()
    {
        return new WidgetExportService();
    }

    #region Reference extraction (plain HTML export)

    private string CreatePlainHtmlWidget(string folderName, string entryName = "widget.html")
    {
        var folder = _temp.CreateSubdirectory(folderName);
        var entryPath = Path.Combine(folder, entryName);
        File.WriteAllText(entryPath, "<html><head></head><body>hello</body></html>");
        return entryPath;
    }

    [Fact]
    public void CollectWidgetExportFiles_FollowsSrcHrefAndUrlReferences()
    {
        var folder = _temp.CreateSubdirectory("refs-widget");
        File.WriteAllText(
            Path.Combine(folder, "widget.html"),
            """
            <html>
              <head>
                <link rel="stylesheet" href="style.css" />
                <script src="app.js"></script>
              </head>
              <body style="background-image: url('bg.png')"></body>
            </html>
            """);
        File.WriteAllText(Path.Combine(folder, "style.css"), "body { color: red }");
        File.WriteAllText(Path.Combine(folder, "app.js"), "console.log(1);");
        File.WriteAllText(Path.Combine(folder, "bg.png"), "png");

        var files = WidgetManifestHelper.CollectWidgetExportFiles(
            Path.Combine(folder, "widget.html"),
            folder,
            ManifestJsonOptions);

        var names = files.Select(Path.GetFileName).ToList();
        Assert.Contains("widget.html", names);
        Assert.Contains("style.css", names);
        Assert.Contains("app.js", names);
        Assert.Contains("bg.png", names);
    }

    [Fact]
    public void CollectWidgetExportFiles_SkipsRemoteAndDataUris()
    {
        var folder = _temp.CreateSubdirectory("remote-refs-widget");
        File.WriteAllText(
            Path.Combine(folder, "widget.html"),
            """
            <html>
              <head>
                <script src="https://cdn.example.com/lib.js"></script>
                <script src="data:text/javascript;base64,AAA"></script>
                <script src="http://insecure.example.com/x.js"></script>
              </head>
              <body></body>
            </html>
            """);

        var files = WidgetManifestHelper.CollectWidgetExportFiles(
            Path.Combine(folder, "widget.html"),
            folder,
            ManifestJsonOptions);

        Assert.Single(files);
        Assert.Equal("widget.html", Path.GetFileName(files[0]));
    }

    [Fact]
    public void CollectWidgetExportFiles_LimitsCssTraversalToSixteenHops()
    {
        var folder = _temp.CreateSubdirectory("css-chain-widget");

        // widget.html -> c1.css -> c2.css -> ... -> c20.css. The reference walker follows at
        // most 16 CSS hops, so c16 is the last discovered file.
        for (var i = 1; i <= 20; i++)
        {
            var next = i < 20 ? $"@import url('c{i + 1}.css');" : string.Empty;
            File.WriteAllText(Path.Combine(folder, $"c{i}.css"), next);
        }

        File.WriteAllText(
            Path.Combine(folder, "widget.html"),
            "<html><head><link rel=\"stylesheet\" href=\"c1.css\" /></head><body></body></html>");

        var files = WidgetManifestHelper.CollectWidgetExportFiles(
            Path.Combine(folder, "widget.html"),
            folder,
            ManifestJsonOptions);

        var names = files.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i <= 16; i++)
        {
            Assert.True(names.Contains($"c{i}.css"), $"expected c{i}.css to be collected");
        }

        for (var i = 17; i <= 20; i++)
        {
            Assert.False(names.Contains($"c{i}.css"), $"c{i}.css must not be collected beyond the 16-hop limit");
        }
    }

    [Fact]
    public void CollectWidgetExportFiles_ToleratesCyclicCssReferences()
    {
        var folder = _temp.CreateSubdirectory("css-cycle-widget");
        File.WriteAllText(
            Path.Combine(folder, "widget.html"),
            "<html><head><link rel=\"stylesheet\" href=\"a.css\" /></head><body></body></html>");
        File.WriteAllText(Path.Combine(folder, "a.css"), "@import url('b.css');");
        File.WriteAllText(Path.Combine(folder, "b.css"), "@import url('a.css');");

        var files = WidgetManifestHelper.CollectWidgetExportFiles(
            Path.Combine(folder, "widget.html"),
            folder,
            ManifestJsonOptions);

        var names = files.Select(Path.GetFileName).ToList();
        Assert.Equal(3, names.Count);
        Assert.Contains("widget.html", names);
        Assert.Contains("a.css", names);
        Assert.Contains("b.css", names);
    }

    [Fact]
    public void CollectWidgetExportFiles_ExcludesRuntimeGeneratedFiles()
    {
        var folder = _temp.CreateSubdirectory("runtime-files-widget");
        File.WriteAllText(
            Path.Combine(folder, "widget.html"),
            "<html><head><link rel=\"stylesheet\" href=\"style.css\" /></head><body></body></html>");
        File.WriteAllText(Path.Combine(folder, ".htwind-settings.json"), "{}");
        File.WriteAllText(Path.Combine(folder, "widget.html..htwind-settings.json"), "{}");
        File.WriteAllText(Path.Combine(folder, "widget.html.htwind-live-preview.html"), "<html></html>");
        File.WriteAllText(Path.Combine(folder, "style.css"), "body{}");

        var files = WidgetManifestHelper.CollectWidgetExportFiles(
            Path.Combine(folder, "widget.html"),
            folder,
            ManifestJsonOptions);

        var names = files.Select(Path.GetFileName).ToList();
        Assert.DoesNotContain(".htwind-settings.json", names);
        Assert.DoesNotContain("widget.html..htwind-settings.json", names);
        Assert.DoesNotContain("widget.html.htwind-live-preview.html", names);
        Assert.Contains("widget.html", names);
        Assert.Contains("style.css", names);
    }

    [Fact]
    public void CollectWidgetExportFiles_DoesNotExportSiblingWidgetsInSharedFolder()
    {
        var folder = _temp.CreateSubdirectory("shared-templates");
        File.WriteAllText(
            Path.Combine(folder, "a.html"),
            "<html><head><link rel=\"stylesheet\" href=\"a.css\" /></head><body></body></html>");
        File.WriteAllText(Path.Combine(folder, "a.css"), "body{}");
        File.WriteAllText(Path.Combine(folder, "b.html"), "<html><body>sibling</body></html>");
        File.WriteAllText(Path.Combine(folder, "b-assets.js"), "// sibling asset");

        var files = WidgetManifestHelper.CollectWidgetExportFiles(
            Path.Combine(folder, "a.html"),
            folder,
            ManifestJsonOptions);

        var names = files.Select(Path.GetFileName).ToList();
        Assert.Contains("a.html", names);
        Assert.Contains("a.css", names);
        Assert.DoesNotContain("b.html", names);
        Assert.DoesNotContain("b-assets.js", names);
    }

    #endregion

    #region Full import -> export round-trips

    private static void AssertFileContentMatches(string expectedRoot, string actualRoot, string relativePath)
    {
        var expected = File.ReadAllText(Path.Combine(expectedRoot, relativePath));
        var actual = File.ReadAllText(Path.Combine(actualRoot, relativePath));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ImportSingleFile_CopiesEntryOnly_AndExportReimportPreservesContent()
    {
        var importService = CreateImportService();
        var exportService = CreateExportService();

        var sourcePath = CreatePlainHtmlWidget("rt-single", "my-widget.html");
        File.WriteAllText(
            sourcePath,
            "<html><head><link rel=\"stylesheet\" href=\"s.css\" /></head><body>x</body></html>");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(sourcePath)!, "s.css"), "body{}");

        // Import: a single HTML file becomes a managed package containing ONLY the entry file
        // (loose sibling files are not adopted into the managed copy).
        var references = importService.ImportWidgetSource(sourcePath);
        var reference = Assert.Single(references);
        Assert.True(File.Exists(reference.EntryFilePath));
        Assert.True(importService.IsManagedWidgetPath(reference.EntryFilePath));
        Assert.Equal(
            "<html><head><link rel=\"stylesheet\" href=\"s.css\" /></head><body>x</body></html>",
            File.ReadAllText(reference.EntryFilePath));
        var managedFolder = Path.GetDirectoryName(reference.EntryFilePath)!;
        Assert.False(File.Exists(Path.Combine(managedFolder, "s.css")));

        // Export: managed package becomes a portable manifest package.
        var exportDirectory = _temp.CreateSubdirectory("rt-single-export");
        var exportRoot = exportService.ExportWidgetToDirectory(reference.EntryFilePath, exportDirectory);
        Assert.True(File.Exists(Path.Combine(exportRoot, WidgetPackageConstants.ManifestFileName)));
        Assert.Equal(
            File.ReadAllText(reference.EntryFilePath),
            File.ReadAllText(Path.Combine(exportRoot, "my-widget.html")));

        // Re-import the exported package: content fidelity is preserved.
        var reimportReferences = importService.ImportWidgetSource(
            Path.Combine(exportRoot, WidgetPackageConstants.ManifestFileName));
        var reimported = Assert.Single(reimportReferences);
        Assert.Equal(
            File.ReadAllText(reference.EntryFilePath),
            File.ReadAllText(reimported.EntryFilePath));
    }

    private string CreateSingleWidgetPackage(string packageName)
    {
        var packageRoot = _temp.CreateSubdirectory($"pkg-{packageName}");
        var widgetFolder = Directory.CreateDirectory(
            Path.Combine(packageRoot, "widgets", packageName)).FullName;
        File.WriteAllText(
            Path.Combine(widgetFolder, "index.html"),
            "<html><head><script src=\"app.js\"></script></head><body>pkg</body></html>");
        File.WriteAllText(Path.Combine(widgetFolder, "app.js"), "// app");
        File.WriteAllText(Path.Combine(widgetFolder, "theme.css"), "body{}");
        File.WriteAllText(
            Path.Combine(packageRoot, WidgetPackageConstants.ManifestFileName),
            JsonSerializer.Serialize(
                new WidgetPackageManifest
                {
                    SchemaVersion = WidgetPackageConstants.SupportedSchemaVersion,
                    Name = packageName,
                    Widgets =
                    [
                        new WidgetPackageItemManifest
                        {
                            WidgetId = "pkg-widget-1",
                            Name = packageName,
                            RelativePath = $"widgets/{packageName}",
                            EntryFile = "index.html",
                            Assets = ["app.js", "theme.css"]
                        }
                    ]
                },
                ManifestJsonOptions));
        return packageRoot;
    }

    [Fact]
    public void ImportSingleWidgetPackage_ExportReimport_PreservesContent()
    {
        var importService = CreateImportService();
        var exportService = CreateExportService();

        var packageRoot = CreateSingleWidgetPackage("single-package");
        var sourceWidgetFolder = Path.Combine(packageRoot, "widgets", "single-package");
        var references = importService.ImportWidgetSource(
            Path.Combine(packageRoot, WidgetPackageConstants.ManifestFileName));
        var reference = Assert.Single(references);
        Assert.True(File.Exists(reference.EntryFilePath));
        AssertFileContentMatches(sourceWidgetFolder, Path.GetDirectoryName(reference.EntryFilePath)!, "index.html");
        AssertFileContentMatches(sourceWidgetFolder, Path.GetDirectoryName(reference.EntryFilePath)!, "app.js");
        AssertFileContentMatches(sourceWidgetFolder, Path.GetDirectoryName(reference.EntryFilePath)!, "theme.css");

        var exportDirectory = _temp.CreateSubdirectory("single-package-export");
        var exportRoot = exportService.ExportWidgetToDirectory(reference.EntryFilePath, exportDirectory);
        AssertFileContentMatches(sourceWidgetFolder, exportRoot, "index.html");
        AssertFileContentMatches(sourceWidgetFolder, exportRoot, "app.js");
        AssertFileContentMatches(sourceWidgetFolder, exportRoot, "theme.css");

        var reimportReferences = importService.ImportWidgetSource(
            Path.Combine(exportRoot, WidgetPackageConstants.ManifestFileName));
        var reimported = Assert.Single(reimportReferences);
        AssertFileContentMatches(sourceWidgetFolder, Path.GetDirectoryName(reimported.EntryFilePath)!, "index.html");
        AssertFileContentMatches(sourceWidgetFolder, Path.GetDirectoryName(reimported.EntryFilePath)!, "app.js");
        AssertFileContentMatches(sourceWidgetFolder, Path.GetDirectoryName(reimported.EntryFilePath)!, "theme.css");
    }

    [Fact]
    public void ImportMultiWidgetPackage_CreatesBothWidgets_AndExportWorkspacePreservesAll()
    {
        var importService = CreateImportService();
        var exportService = CreateExportService();

        // Copy the shipped example package into the fixture so the real embedded template
        // files are never mutated.
        var examplePackageSource = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "HTWind", "Templates", "examples", "multi-widget-package");
        examplePackageSource = Path.GetFullPath(examplePackageSource);
        Assert.True(Directory.Exists(examplePackageSource), "multi-widget-package example must exist");

        var packageRoot = _temp.CreateSubdirectory("multi-package");
        foreach (var sourceFile in Directory.GetFiles(examplePackageSource, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(examplePackageSource, sourceFile);
            var destination = Path.Combine(packageRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(sourceFile, destination);
        }

        var references = importService.ImportWidgetSource(
            Path.Combine(packageRoot, WidgetPackageConstants.ManifestFileName));
        Assert.Equal(2, references.Count);

        var focusTimer = references.Single(r => r.WidgetName.Contains("focus", StringComparison.OrdinalIgnoreCase));
        var statusBoard = references.Single(r => r.WidgetName.Contains("status", StringComparison.OrdinalIgnoreCase));

        AssertFileContentMatches(
            Path.Combine(packageRoot, "widgets", "focus-timer"),
            Path.GetDirectoryName(focusTimer.EntryFilePath)!,
            "index.html");
        AssertFileContentMatches(
            Path.Combine(packageRoot, "widgets", "focus-timer"),
            Path.GetDirectoryName(focusTimer.EntryFilePath)!,
            "styles.css");
        AssertFileContentMatches(
            Path.Combine(packageRoot, "widgets", "status-board"),
            Path.GetDirectoryName(statusBoard.EntryFilePath)!,
            "index.html");

        // Workspace export with both widgets preserves every file of both widgets.
        var exportDirectory = _temp.CreateSubdirectory("multi-workspace-export");
        var exportRoot = exportService.ExportWidgetsToDirectory(
            [focusTimer.EntryFilePath, statusBoard.EntryFilePath],
            exportDirectory,
            "workspace");

        var reimported = importService.ImportWidgetSource(
            Path.Combine(exportRoot, WidgetPackageConstants.ManifestFileName));
        Assert.Equal(2, reimported.Count);

        // Both widgets use index.html as entry file, so match original <-> reimported pairs by
        // entry content (unique per widget) before comparing the remaining files.
        foreach (var original in references)
        {
            var originalEntryContent = File.ReadAllText(original.EntryFilePath);
            var counterpart = reimported.Single(candidate =>
                string.Equals(
                    File.ReadAllText(candidate.EntryFilePath),
                    originalEntryContent,
                    StringComparison.Ordinal));

            var originalRoot = Path.GetDirectoryName(original.EntryFilePath)!;
            var counterpartRoot = Path.GetDirectoryName(counterpart.EntryFilePath)!;
            foreach (var fileName in Directory
                .GetFiles(originalRoot)
                .Select(Path.GetFileName)
                .Where(name => !string.Equals(
                    name,
                    WidgetPackageConstants.ManifestFileName,
                    StringComparison.OrdinalIgnoreCase)))
            {
                AssertFileContentMatches(originalRoot, counterpartRoot, fileName!);
            }
        }
    }

    [Fact]
    public void CreateManagedWidget_GeneratesUniqueFolderWithManifest()
    {
        var importService = CreateImportService();

        var first = importService.CreateManagedWidget("notes", "<html></html>");
        var second = importService.CreateManagedWidget("notes", "<html></html>");

        Assert.NotEqual(first.WidgetRootPath, second.WidgetRootPath);
        Assert.True(File.Exists(first.EntryFilePath));
        Assert.True(File.Exists(Path.Combine(first.WidgetRootPath, WidgetPackageConstants.ManifestFileName)));
        Assert.True(importService.IsManagedWidgetPath(first.EntryFilePath));
        Assert.Equal("<html></html>", File.ReadAllText(first.EntryFilePath));
    }

    [Fact]
    public void DeleteManagedWidgetFile_RemovesManagedFolder_ButIgnoresExternalPaths()
    {
        var importService = CreateImportService();

        var reference = importService.CreateManagedWidget("deletable", "<html></html>");
        importService.DeleteManagedWidgetFile(reference.EntryFilePath);
        Assert.False(Directory.Exists(reference.WidgetRootPath));

        var externalPath = CreatePlainHtmlWidget("external-widget", "external.html");
        importService.DeleteManagedWidgetFile(externalPath);
        Assert.True(File.Exists(externalPath));
    }

    #endregion
}
