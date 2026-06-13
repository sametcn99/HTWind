using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

using HTWind.Model;

namespace HTWind.Services.Widget;

public sealed class WidgetImportService : IWidgetImportService
{
    private static readonly JsonSerializerOptions _widgetManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _widgetsDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HTWind",
        "Widgets"
    );

    public string WidgetsDataDirectory => _widgetsDataDirectory;

    public IReadOnlyList<WidgetManagedReference> ImportWidgetSource(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        if (IsManagedWidgetPath(sourcePath))
        {
            return [GetManagedReferenceForExistingPath(sourcePath)];
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Selected widget source was not found.", sourcePath);
        }

        if (IsManifestPath(sourcePath))
        {
            return ImportManifestBackedWidgets(sourcePath);
        }

        return [ImportSingleFileWidget(sourcePath)];
    }

    public WidgetManagedReference CreateManagedWidget(string requestedFileName, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedFileName);
        ArgumentNullException.ThrowIfNull(content);

        Directory.CreateDirectory(_widgetsDataDirectory);

        var widgetName = EnsureWidgetName(requestedFileName);
        var widgetRootPath = BuildUniqueWidgetDirectoryPath(Path.GetFileNameWithoutExtension(widgetName));
        Directory.CreateDirectory(widgetRootPath);

        var entryFilePath = Path.Combine(widgetRootPath, WidgetPackageConstants.DefaultEntryFileName);
        File.WriteAllText(entryFilePath, content);

        var manifest = WidgetManifestHelper.CreateSingleWidgetManifest(
            packageName: Path.GetFileNameWithoutExtension(widgetName),
            widgetName: Path.GetFileNameWithoutExtension(widgetName),
            entryFile: WidgetPackageConstants.DefaultEntryFileName,
            assets: [],
            widgetId: Guid.NewGuid().ToString("N"),
            createdUtc: DateTime.UtcNow.ToString("O")
        );

        WidgetManifestHelper.WriteManifest(widgetRootPath, manifest, _widgetManifestJsonOptions);
        return WidgetManifestHelper.CreateManagedReference(
            widgetName,
            entryFilePath,
            widgetRootPath,
            manifest.Widgets[0].WidgetId
        );
    }

    public bool IsManagedWidgetPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var managedRoot = Path.GetFullPath(_widgetsDataDirectory) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(managedRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void DeleteManagedWidgetFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var managedRoot = Path.GetFullPath(_widgetsDataDirectory) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(managedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var widgetRootPath = TryGetWidgetRootPath(fullPath);
            if (!string.IsNullOrWhiteSpace(widgetRootPath) && Directory.Exists(widgetRootPath))
            {
                Directory.Delete(widgetRootPath, true);
                return;
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch
        {
        }
    }

    private WidgetManagedReference GetManagedReferenceForExistingPath(string sourcePath)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Selected managed widget source was not found.", sourcePath);
        }

        var widgetRootPath = TryGetWidgetRootPath(fullPath) ?? Path.GetDirectoryName(fullPath)!;
        var manifest = WidgetManifestHelper.LoadManifestIfPresent(widgetRootPath, _widgetManifestJsonOptions);
        var widget = manifest is null ? null : WidgetManifestHelper.GetSingleWidget(manifest);
        var widgetName = widget is null
            ? Path.GetFileName(fullPath)
            : EnsureWidgetName(widget.Name);
        var entryFilePath = manifest is null
            ? fullPath
            : WidgetManifestHelper.ResolveEntryFilePath(widgetRootPath, widget!.EntryFile);

        if (!File.Exists(entryFilePath))
        {
            throw new InvalidOperationException("The widget entry file could not be found.");
        }

        return WidgetManifestHelper.CreateManagedReference(widgetName, entryFilePath, widgetRootPath, widget?.WidgetId);
    }

    private IReadOnlyList<WidgetManagedReference> ImportManifestBackedWidgets(string manifestPath)
    {
        var sourceRootPath = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
        if (string.IsNullOrWhiteSpace(sourceRootPath) || !Directory.Exists(sourceRootPath))
        {
            throw new InvalidOperationException("The widget package root folder could not be found.");
        }

        var manifest = WidgetManifestHelper.LoadManifest(manifestPath, _widgetManifestJsonOptions);
        WidgetManifestHelper.ValidateManifest(manifest, sourceRootPath);

        var managedReferences = new List<WidgetManagedReference>(manifest.Widgets.Count);
        foreach (var widget in manifest.Widgets)
        {
            var sourceWidgetRootPath = WidgetManifestHelper.ResolveWidgetRootPath(sourceRootPath, widget.RelativePath);
            var widgetRootPath = BuildUniqueWidgetDirectoryPath(widget.Name);
            WidgetManifestHelper.CopyWidgetDirectory(sourceWidgetRootPath, widgetRootPath);

            var entryFilePath = WidgetManifestHelper.ResolveEntryFilePath(widgetRootPath, widget.EntryFile);
            var managedManifest = WidgetManifestHelper.CreateSingleWidgetManifest(
                packageName: widget.Name,
                widgetName: widget.Name,
                entryFile: widget.EntryFile,
                assets: WidgetManifestHelper.CollectAssetPaths(widgetRootPath, widget.EntryFile),
                widgetId: widget.WidgetId,
                createdUtc: manifest.CreatedUtc,
                sourceManifest: manifest
            );

            WidgetManifestHelper.WriteManifest(widgetRootPath, managedManifest, _widgetManifestJsonOptions);
            managedReferences.Add(
                WidgetManifestHelper.CreateManagedReference(
                    EnsureWidgetName(widget.Name),
                    entryFilePath,
                    widgetRootPath,
                    widget.WidgetId
                )
            );
        }

        return managedReferences;
    }

    private WidgetManagedReference ImportSingleFileWidget(string sourcePath)
    {
        var sourceFileName = Path.GetFileName(sourcePath);
        var widgetRootPath = BuildUniqueWidgetDirectoryPath(sourceFileName);
        Directory.CreateDirectory(widgetRootPath);

        var entryFilePath = Path.Combine(widgetRootPath, sourceFileName);
        File.Copy(sourcePath, entryFilePath, overwrite: false);

        var manifest = WidgetManifestHelper.CreateSingleWidgetManifest(
            packageName: Path.GetFileNameWithoutExtension(sourceFileName),
            widgetName: Path.GetFileNameWithoutExtension(sourceFileName),
            entryFile: sourceFileName,
            assets: [],
            widgetId: Guid.NewGuid().ToString("N"),
            createdUtc: DateTime.UtcNow.ToString("O")
        );

        WidgetManifestHelper.WriteManifest(widgetRootPath, manifest, _widgetManifestJsonOptions);
        return WidgetManifestHelper.CreateManagedReference(
            sourceFileName,
            entryFilePath,
            widgetRootPath,
            manifest.Widgets[0].WidgetId
        );
    }

    private static string EnsureWidgetName(string requestedFileName)
    {
        var trimmedName = Path.GetFileName(requestedFileName.Trim());
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return "widget.html";
        }

        return Path.HasExtension(trimmedName)
            ? trimmedName
            : $"{trimmedName}.html";
    }

    private static bool IsManifestPath(string path)
    {
        return string.Equals(
            Path.GetFileName(path),
            WidgetPackageConstants.ManifestFileName,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private string BuildUniqueWidgetDirectoryPath(string fileName)
    {
        Directory.CreateDirectory(_widgetsDataDirectory);
        return WidgetManifestHelper.BuildUniqueDirectoryPath(_widgetsDataDirectory, WidgetManifestHelper.SanitizeFileName(Path.GetFileNameWithoutExtension(fileName)));
    }

    private string? TryGetWidgetRootPath(string fullPath)
    {
        var managedRoot = Path.GetFullPath(_widgetsDataDirectory) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(managedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var directoryPath = Directory.Exists(fullPath)
            ? fullPath
            : Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return null;
        }

        var manifestPath = Path.Combine(directoryPath, WidgetPackageConstants.ManifestFileName);
        if (File.Exists(manifestPath))
        {
            return directoryPath;
        }

        return string.Equals(directoryPath.TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(_widgetsDataDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            ? null
            : directoryPath;
    }
}
