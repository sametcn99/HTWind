using System.IO;
using System.Text.Json;

using HTWind.Model;

namespace HTWind.Services.Widget;

public sealed class WidgetExportService : IWidgetExportService
{
    private static readonly JsonSerializerOptions _widgetManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string ExportWidgetToDirectory(string widgetFilePath, string destinationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        if (!Directory.Exists(destinationDirectory))
        {
            throw new DirectoryNotFoundException("Selected export directory was not found.");
        }

        var widgetName = Path.GetFileNameWithoutExtension(widgetFilePath);
        var widgetRootPath = Path.GetDirectoryName(widgetFilePath) ?? string.Empty;

        var exportFolderName = WidgetManifestHelper.SanitizeFileName(widgetName);
        var exportRootPath = WidgetManifestHelper.BuildUniqueDirectoryPath(destinationDirectory, exportFolderName);
        Directory.CreateDirectory(exportRootPath);

        var sourceReference = new WidgetManagedReference
        {
            WidgetName = widgetName,
            EntryFilePath = widgetFilePath,
            WidgetRootPath = widgetRootPath
        };

        var manifest = ExportPackage(
            [sourceReference],
            exportRootPath,
            widgetName,
            useWorkspaceSubdirectory: false
        );

        manifest.ExportedUtc = DateTime.UtcNow.ToString("O");
        WidgetManifestHelper.WriteManifest(exportRootPath, manifest, _widgetManifestJsonOptions);
        return exportRootPath;
    }

    public string ExportWidgetsToDirectory(
        IEnumerable<string> widgetFilePaths,
        string destinationDirectory,
        string exportName)
    {
        ArgumentNullException.ThrowIfNull(widgetFilePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportName);

        if (!Directory.Exists(destinationDirectory))
        {
            throw new DirectoryNotFoundException("Selected export directory was not found.");
        }

        var exportRootPath = WidgetManifestHelper.BuildUniqueDirectoryPath(destinationDirectory, WidgetManifestHelper.SanitizeFileName(exportName));
        Directory.CreateDirectory(exportRootPath);

        var sourceReferences = widgetFilePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new WidgetManagedReference
            {
                WidgetName = Path.GetFileName(path),
                EntryFilePath = path,
                WidgetRootPath = Path.GetDirectoryName(path) ?? string.Empty
            })
            .ToList();

        var manifest = ExportPackage(sourceReferences, exportRootPath, exportName, useWorkspaceSubdirectory: true);
        manifest.ExportedUtc = DateTime.UtcNow.ToString("O");
        WidgetManifestHelper.WriteManifest(exportRootPath, manifest, _widgetManifestJsonOptions);
        return exportRootPath;
    }

    private static WidgetPackageManifest ExportPackage(
        IReadOnlyList<WidgetManagedReference> sourceReferences,
        string exportRootPath,
        string packageName,
        bool useWorkspaceSubdirectory)
    {
        ArgumentNullException.ThrowIfNull(sourceReferences);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);

        var packageManifest = new WidgetPackageManifest
        {
            SchemaVersion = WidgetPackageConstants.SupportedSchemaVersion,
            Name = packageName,
            ExportedUtc = DateTime.UtcNow.ToString("O")
        };

        var contentRootPath = useWorkspaceSubdirectory
            ? Path.Combine(exportRootPath, WidgetPackageConstants.WorkspaceWidgetsDirectoryName)
            : exportRootPath;
        Directory.CreateDirectory(contentRootPath);

        var exportJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        foreach (var sourceReference in sourceReferences)
        {
            var itemDirectoryName = useWorkspaceSubdirectory
                ? BuildUniquePackageItemDirectoryName(contentRootPath, Path.GetFileNameWithoutExtension(sourceReference.WidgetName))
                : ".";
            var itemRelativePath = useWorkspaceSubdirectory
                ? WidgetManifestHelper.NormalizeRelativePath(Path.Combine(WidgetPackageConstants.WorkspaceWidgetsDirectoryName, itemDirectoryName))
                : ".";
            var itemDestinationRoot = useWorkspaceSubdirectory
                ? Path.Combine(contentRootPath, itemDirectoryName)
                : exportRootPath;
            Directory.CreateDirectory(itemDestinationRoot);

            var widgetRootPath = sourceReference.WidgetRootPath;
            var hasWidgetRoot = !string.IsNullOrWhiteSpace(widgetRootPath) && Directory.Exists(widgetRootPath);

            // Scope the export strictly to this widget's own entry file plus its declared or
            // referenced assets; shared folders (e.g. BuiltInTemplates) must not leak sibling
            // widgets into the package.
            var exportFiles = hasWidgetRoot
                ? WidgetManifestHelper.CollectWidgetExportFiles(sourceReference.EntryFilePath, widgetRootPath, exportJsonOptions)
                : [];
            if (exportFiles.Count == 0)
            {
                exportFiles = [sourceReference.EntryFilePath];
            }

            var widgetRootForRelativePaths = hasWidgetRoot
                ? widgetRootPath!
                : Path.GetDirectoryName(sourceReference.EntryFilePath) ?? string.Empty;

            foreach (var sourceFilePath in exportFiles)
            {
                var relativeFilePath = Path.GetRelativePath(widgetRootForRelativePaths, sourceFilePath);
                var destinationFilePath = Path.Combine(itemDestinationRoot, relativeFilePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);
                File.Copy(sourceFilePath, destinationFilePath, overwrite: false);
            }

            var entryFile = Path.GetFileName(sourceReference.EntryFilePath);

            var packageItem = new WidgetPackageItemManifest
            {
                WidgetId = Guid.NewGuid().ToString("N"),
                Name = Path.GetFileNameWithoutExtension(sourceReference.WidgetName),
                RelativePath = itemRelativePath,
                EntryFile = WidgetManifestHelper.NormalizeRelativePath(entryFile),
                Assets = exportFiles
                    .Where(path => !string.Equals(path, sourceReference.EntryFilePath, StringComparison.OrdinalIgnoreCase))
                    .Select(path => WidgetManifestHelper.NormalizeRelativePath(Path.GetRelativePath(widgetRootForRelativePaths, path)))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            packageManifest.Widgets.Add(packageItem);
        }

        return packageManifest;
    }

    private static string BuildUniquePackageItemDirectoryName(string parentDirectory, string suggestedName)
    {
        var sanitizedName = WidgetManifestHelper.SanitizeFileName(suggestedName);
        return Path.GetFileName(WidgetManifestHelper.BuildUniqueDirectoryPath(parentDirectory, sanitizedName));
    }
}
