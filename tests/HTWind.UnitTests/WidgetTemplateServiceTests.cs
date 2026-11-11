using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

using HTWind.Services.Templates;

using Xunit;

namespace HTWind.UnitTests;

/// <summary>
///     Template materialization state machine: first-run extraction, upgrade refresh, and the
///     guarantee that user-modified templates are never overwritten. Every scenario runs in its
///     own isolated temporary data directory.
/// </summary>
public sealed class WidgetTemplateServiceTests : IClassFixture<Fixtures.TempDirectoryFixture>
{
    private const string VersionStoreFileName = "template-versions.json";

    private readonly Fixtures.TempDirectoryFixture _temp;

    public WidgetTemplateServiceTests(Fixtures.TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    private string UseTemplateDirectory()
    {
        var basePath = Path.Combine(_temp.Path, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(basePath);
        return Path.Combine(basePath, "BuiltInTemplates");
    }

    private static WidgetTemplateService CreateService(string templateDirectory)
    {
        return new WidgetTemplateService(Path.GetDirectoryName(templateDirectory));
    }

    private static string TemplatePath(string templateDirectory, string fileName) =>
        Path.Combine(templateDirectory, fileName);

    private static string VersionStorePath(string templateDirectory) =>
        Path.Combine(templateDirectory, VersionStoreFileName);

    private static JsonObject ReadVersionRecords(string templateDirectory)
    {
        var path = VersionStorePath(templateDirectory);
        Assert.True(File.Exists(path), "template-versions.json must exist after materialization");
        return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    }

    [Fact]
    public void FirstRun_ExtractsAllTemplatesAndSchemaFile()
    {
        var templateDirectory = UseTemplateDirectory();
        var service = CreateService(templateDirectory);

        // Every BuiltInWidgetType member materializes its embedded template exactly once.
        foreach (var widgetType in Enum.GetValues<BuiltInWidgetType>())
        {
            service.CreateTemplateFile(widgetType);
        }

        var schemaPath = service.CreateWidgetPackageSchemaFile();

        Assert.True(File.Exists(schemaPath));
        var htmlTemplates = Directory.GetFiles(templateDirectory, "*.html");
        Assert.Equal(Enum.GetValues<BuiltInWidgetType>().Length, htmlTemplates.Length);
        Assert.True(File.Exists(TemplatePath(templateDirectory, "htwind.widget.schema.json")));
        Assert.True(File.Exists(VersionStorePath(templateDirectory)));

        var records = ReadVersionRecords(templateDirectory);
        foreach (var fileName in Directory
            .GetFiles(templateDirectory)
            .Select(Path.GetFileName)
            .Where(name => !string.Equals(name, VersionStoreFileName, StringComparison.OrdinalIgnoreCase)))
        {
            Assert.True(records.ContainsKey(fileName!), $"{fileName} must have a version record");
            Assert.Equal("current", records[fileName!]!["State"]?.ToString());
        }

        // Repeat calls reuse the same file (no churn).
        var before = File.ReadAllText(TemplatePath(templateDirectory, "clock.html"));
        service.CreateTemplateFile(BuiltInWidgetType.Clock);
        Assert.Equal(before, File.ReadAllText(TemplatePath(templateDirectory, "clock.html")));
    }

    [Fact]
    public void SchemaFile_IsValidJsonDocument()
    {
        var templateDirectory = UseTemplateDirectory();
        var service = CreateService(templateDirectory);

        var schemaPath = service.CreateWidgetPackageSchemaFile();

        using var document = JsonDocument.Parse(File.ReadAllText(schemaPath));
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }

    [Fact]
    public void Upgrade_UpdatesUntouchedTemplate()
    {
        var templateDirectory = UseTemplateDirectory();
        var service = CreateService(templateDirectory);
        service.CreateTemplateFile(BuiltInWidgetType.Clock);

        // Simulate an app upgrade: the last written (stale) template still matches the
        // recorded hash, but the embedded content shipped with the new version differs.
        const string staleContent = "<html>stale template from an old release</html>";
        File.WriteAllText(TemplatePath(templateDirectory, "clock.html"), staleContent);
        var staleHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(staleContent)));
        var records = ReadVersionRecords(templateDirectory);
        records["clock.html"]!["Hash"] = staleHash;
        records["clock.html"]!["State"] = "current";
        File.WriteAllText(VersionStorePath(templateDirectory), records.ToJsonString());

        var refreshed = CreateService(templateDirectory).CreateTemplateFile(BuiltInWidgetType.Clock);

        var content = File.ReadAllText(refreshed);
        Assert.NotEqual(staleContent, content);
        Assert.Equal(
            "current",
            ReadVersionRecords(templateDirectory)["clock.html"]!["State"]?.ToString());
    }

    [Fact]
    public void UserModifiedTemplate_IsNeverOverwritten()
    {
        const string userContent = "<html>my customized clock</html>";
        var templateDirectory = UseTemplateDirectory();
        CreateService(templateDirectory).CreateTemplateFile(BuiltInWidgetType.Clock);

        File.WriteAllText(TemplatePath(templateDirectory, "clock.html"), userContent);
        // A new service instance (fresh app run) must detect the user edit, never clobber it,
        // and record it as userModified.
        CreateService(templateDirectory).CreateTemplateFile(BuiltInWidgetType.Clock);

        Assert.Equal(userContent, File.ReadAllText(TemplatePath(templateDirectory, "clock.html")));
        Assert.Equal(
            "userModified",
            ReadVersionRecords(templateDirectory)["clock.html"]!["State"]?.ToString());

        // Still protected on later runs.
        CreateService(templateDirectory).CreateTemplateFile(BuiltInWidgetType.Clock);
        Assert.Equal(userContent, File.ReadAllText(TemplatePath(templateDirectory, "clock.html")));
    }

    [Fact]
    public void LegacyInstallWithoutVersionRecord_IsRefreshedExactlyOnceThenProtected()
    {
        const string legacyContent = "<html>legacy clock</html>";
        var templateDirectory = UseTemplateDirectory();

        // A pre-tracking install: file exists, but template-versions.json has no record.
        Directory.CreateDirectory(templateDirectory);
        File.WriteAllText(TemplatePath(templateDirectory, "clock.html"), legacyContent);

        var service = CreateService(templateDirectory);
        var path = service.CreateTemplateFile(BuiltInWidgetType.Clock);

        // Refreshed exactly once to the embedded content.
        var refreshedContent = File.ReadAllText(path);
        Assert.NotEqual(legacyContent, refreshedContent);
        Assert.Equal(
            "current",
            ReadVersionRecords(templateDirectory)["clock.html"]!["State"]?.ToString());

        // From now on the file is protected: a user edit is never clobbered.
        File.WriteAllText(path, legacyContent);
        CreateService(templateDirectory).CreateTemplateFile(BuiltInWidgetType.Clock);
        Assert.Equal(legacyContent, File.ReadAllText(TemplatePath(templateDirectory, "clock.html")));
        Assert.Equal(
            "userModified",
            ReadVersionRecords(templateDirectory)["clock.html"]!["State"]?.ToString());
    }

    [Fact]
    public void AllTemplateTypes_MaterializeDistinctFiles()
    {
        var templateDirectory = UseTemplateDirectory();
        var service = CreateService(templateDirectory);
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var widgetType in Enum.GetValues<BuiltInWidgetType>())
        {
            var path = service.CreateTemplateFile(widgetType);
            Assert.True(File.Exists(path));
            Assert.True(seenPaths.Add(path), $"{widgetType} must map to a unique file");
        }

        Assert.Equal(Enum.GetValues<BuiltInWidgetType>().Length, seenPaths.Count);
    }
}
