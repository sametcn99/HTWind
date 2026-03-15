using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace HTWind.Services;

public sealed partial class WidgetStateRepository : IWidgetStateRepository
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private static readonly WidgetStateJsonContext _jsonContext = new(_jsonOptions);
    private static readonly JsonSerializerOptions _widgetManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _stateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HTWind",
        "widgets-state.json"
    );

    private readonly string _widgetsDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HTWind",
        "Widgets"
    );

    public bool HasStateFile()
    {
        return File.Exists(_stateFilePath);
    }

    public WidgetStateSnapshot Load()
    {
        if (!File.Exists(_stateFilePath))
        {
            return new WidgetStateSnapshot();
        }

        try
        {
            var json = File.ReadAllText(_stateFilePath);

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var legacyStates = JsonSerializer.Deserialize(json, _jsonContext.ListWidgetStateRecord);
                return new WidgetStateSnapshot
                {
                    Widgets = legacyStates ?? [],
                    SuppressWidgetsOnFullscreen = true,
                    SuppressWidgetsOnMaximized = false
                };
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                var snapshot = JsonSerializer.Deserialize(json, _jsonContext.WidgetStateSnapshot);
                return snapshot ?? new WidgetStateSnapshot();
            }

            return new WidgetStateSnapshot();
        }
        catch
        {
            return new WidgetStateSnapshot();
        }
    }

    public void Save(WidgetStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
            var json = JsonSerializer.Serialize(snapshot, _jsonContext.WidgetStateSnapshot);
            File.WriteAllText(_stateFilePath, json);
        }
        catch
        {
            // Persistence failures should not interrupt widget usage.
        }
    }

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

        var manifest = CreateSingleWidgetManifest(
            packageName: Path.GetFileNameWithoutExtension(widgetName),
            widgetName: Path.GetFileNameWithoutExtension(widgetName),
            entryFile: WidgetPackageConstants.DefaultEntryFileName,
            assets: [],
            widgetId: Guid.NewGuid().ToString("N"),
            createdUtc: DateTime.UtcNow.ToString("O")
        );

        WriteManifest(widgetRootPath, manifest);
        return CreateManagedReference(
            widgetName,
            entryFilePath,
            widgetRootPath,
            manifest.Widgets[0].WidgetId
        );
    }

    public string ExportWidgetToDirectory(string widgetFilePath, string destinationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        if (!Directory.Exists(destinationDirectory))
        {
            throw new DirectoryNotFoundException("Selected export directory was not found.");
        }

        var isManagedSource = IsManagedWidgetPath(widgetFilePath);
        var sourceReference = isManagedSource
            ? GetManagedReferenceForExistingPath(widgetFilePath)
            : CreateExternalReference(widgetFilePath);

        var exportFolderName = SanitizeFileName(Path.GetFileNameWithoutExtension(sourceReference.WidgetName));
        var exportRootPath = BuildUniqueDirectoryPath(destinationDirectory, exportFolderName);
        Directory.CreateDirectory(exportRootPath);

        var manifest = ExportPackage(
            [sourceReference],
            exportRootPath,
            Path.GetFileNameWithoutExtension(sourceReference.WidgetName),
            useWorkspaceSubdirectory: false
        );

        manifest.ExportedUtc = DateTime.UtcNow.ToString("O");
        WriteManifest(exportRootPath, manifest);
        return exportRootPath;
    }

    public string ExportWidgetsToDirectory(
        IEnumerable<string> widgetFilePaths,
        string destinationDirectory,
        string exportName
    )
    {
        ArgumentNullException.ThrowIfNull(widgetFilePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportName);

        if (!Directory.Exists(destinationDirectory))
        {
            throw new DirectoryNotFoundException("Selected export directory was not found.");
        }

        var sourcePaths = widgetFilePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (sourcePaths.Count == 0)
        {
            throw new InvalidOperationException("No widgets were provided for export.");
        }

        var exportRootPath = BuildUniqueDirectoryPath(destinationDirectory, SanitizeFileName(exportName));
        Directory.CreateDirectory(exportRootPath);

        var sourceReferences = sourcePaths
            .Select(path => IsManagedWidgetPath(path) ? GetManagedReferenceForExistingPath(path) : CreateExternalReference(path))
            .ToList();

        var manifest = ExportPackage(sourceReferences, exportRootPath, exportName, useWorkspaceSubdirectory: true);
        manifest.ExportedUtc = DateTime.UtcNow.ToString("O");
        WriteManifest(exportRootPath, manifest);
        return exportRootPath;
    }

    public string CopyWidgetToManagedStorage(string sourcePath)
    {
        return ImportWidgetSource(sourcePath)[0].EntryFilePath;
    }

    public string CreateManagedWidgetFile(string requestedFileName, string content)
    {
        return CreateManagedWidget(requestedFileName, content).EntryFilePath;
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
            // Avoid blocking widget removal for file system edge cases.
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
        var manifest = LoadManifestIfPresent(widgetRootPath);
        var widget = manifest is null ? null : GetSingleWidget(manifest);
        var widgetName = widget is null
            ? Path.GetFileName(fullPath)
            : EnsureWidgetName(widget.Name);
        var entryFilePath = manifest is null
            ? fullPath
            : ResolveEntryFilePath(widgetRootPath, widget!.EntryFile);

        if (!File.Exists(entryFilePath))
        {
            throw new InvalidOperationException("The widget entry file could not be found.");
        }

        return CreateManagedReference(widgetName, entryFilePath, widgetRootPath, widget?.WidgetId);
    }

    private IReadOnlyList<WidgetManagedReference> ImportManifestBackedWidgets(string manifestPath)
    {
        var sourceRootPath = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
        if (string.IsNullOrWhiteSpace(sourceRootPath) || !Directory.Exists(sourceRootPath))
        {
            throw new InvalidOperationException("The widget package root folder could not be found.");
        }

        var manifest = LoadManifest(manifestPath);
        ValidateManifest(manifest, sourceRootPath);

        var managedReferences = new List<WidgetManagedReference>(manifest.Widgets.Count);
        foreach (var widget in manifest.Widgets)
        {
            var sourceWidgetRootPath = ResolveWidgetRootPath(sourceRootPath, widget.RelativePath);
            var widgetRootPath = BuildUniqueWidgetDirectoryPath(widget.Name);
            CopyWidgetDirectory(sourceWidgetRootPath, widgetRootPath);

            var entryFilePath = ResolveEntryFilePath(widgetRootPath, widget.EntryFile);
            var managedManifest = CreateSingleWidgetManifest(
                packageName: widget.Name,
                widgetName: widget.Name,
                entryFile: widget.EntryFile,
                assets: CollectAssetPaths(widgetRootPath, widget.EntryFile),
                widgetId: widget.WidgetId,
                createdUtc: manifest.CreatedUtc,
                sourceManifest: manifest
            );

            WriteManifest(widgetRootPath, managedManifest);
            managedReferences.Add(
                CreateManagedReference(
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

        var manifest = CreateSingleWidgetManifest(
            packageName: Path.GetFileNameWithoutExtension(sourceFileName),
            widgetName: Path.GetFileNameWithoutExtension(sourceFileName),
            entryFile: sourceFileName,
            assets: [],
            widgetId: Guid.NewGuid().ToString("N"),
            createdUtc: DateTime.UtcNow.ToString("O")
        );

        WriteManifest(widgetRootPath, manifest);
        return CreateManagedReference(
            sourceFileName,
            entryFilePath,
            widgetRootPath,
            manifest.Widgets[0].WidgetId
        );
    }

    private static WidgetManagedReference CreateExternalReference(string widgetFilePath)
    {
        var fullPath = Path.GetFullPath(widgetFilePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The widget file to export was not found.", widgetFilePath);
        }

        return CreateManagedReference(
            Path.GetFileName(fullPath),
            fullPath,
            Path.GetDirectoryName(fullPath)!,
            null
        );
    }

    private static WidgetManagedReference CreateManagedReference(
        string widgetName,
        string entryFilePath,
        string widgetRootPath,
        string? packageWidgetId
    )
    {
        return new WidgetManagedReference
        {
            WidgetName = widgetName,
            EntryFilePath = entryFilePath,
            WidgetRootPath = widgetRootPath,
            PackageWidgetId = packageWidgetId
        };
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

    private WidgetPackageManifest LoadManifest(string manifestPath)
    {
        try
        {
            var manifestJson = File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize<WidgetPackageManifest>(manifestJson, _widgetManifestJsonOptions)
                ?? throw new InvalidOperationException("The widget manifest is empty or invalid.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("The widget manifest is not valid JSON.");
        }
    }

    private WidgetPackageManifest? LoadManifestIfPresent(string widgetRootPath)
    {
        var manifestPath = Path.Combine(widgetRootPath, WidgetPackageConstants.ManifestFileName);
        return File.Exists(manifestPath)
            ? LoadManifest(manifestPath)
            : null;
    }

    private void WriteManifest(string widgetRootPath, WidgetPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var manifestPath = Path.Combine(widgetRootPath, WidgetPackageConstants.ManifestFileName);
        var manifestJson = JsonSerializer.Serialize(manifest, _widgetManifestJsonOptions);
        File.WriteAllText(manifestPath, manifestJson);
    }

    private void ValidateManifest(WidgetPackageManifest manifest, string widgetRootPath)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetRootPath);

        var validationErrors = new List<string>();

        if (manifest.SchemaVersion != WidgetPackageConstants.SupportedSchemaVersion)
        {
            validationErrors.Add("schemaVersion is not supported.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            validationErrors.Add("name is required.");
        }

        if (manifest.Widgets is null || manifest.Widgets.Count == 0)
        {
            validationErrors.Add("widgets is required.");
        }

        TryValidateHomepage(manifest.Homepage, validationErrors);

        var widgets = manifest.Widgets;
        if (widgets is null)
        {
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "The widget manifest is invalid:" + Environment.NewLine + string.Join(Environment.NewLine, validationErrors.Select(error => $"- {error}"))
                );
            }

            return;
        }

        var normalizedWidgetRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < widgets.Count; index++)
        {
            var widget = widgets[index];
            var widgetLabel = string.IsNullOrWhiteSpace(widget.Name)
                ? $"widgets[{index}]"
                : $"widgets[{index}] ({widget.Name})";

            if (string.IsNullOrWhiteSpace(widget.WidgetId))
            {
                validationErrors.Add($"{widgetLabel}: widgetId is required.");
            }

            if (string.IsNullOrWhiteSpace(widget.Name))
            {
                validationErrors.Add($"{widgetLabel}: name is required.");
            }

            if (widget.Assets is null)
            {
                validationErrors.Add($"{widgetLabel}: assets is required.");
                continue;
            }

            string normalizedWidgetRoot;
            try
            {
                normalizedWidgetRoot = NormalizeAndValidateRelativePath(widget.RelativePath, allowHtml: true);
            }
            catch (InvalidOperationException ex)
            {
                validationErrors.Add($"{widgetLabel}: relativePath is invalid: {ex.Message}");
                continue;
            }

            if (!normalizedWidgetRoots.Add(normalizedWidgetRoot))
            {
                validationErrors.Add($"{widgetLabel}: relativePath is duplicated: {normalizedWidgetRoot}.");
                continue;
            }

            string widgetItemRootPath;
            try
            {
                widgetItemRootPath = ResolveWidgetRootPath(widgetRootPath, normalizedWidgetRoot);
            }
            catch (InvalidOperationException ex)
            {
                validationErrors.Add($"{widgetLabel}: relativePath is invalid: {ex.Message}");
                continue;
            }

            if (!Directory.Exists(widgetItemRootPath))
            {
                validationErrors.Add($"{widgetLabel}: relativePath does not exist.");
                continue;
            }

            ValidateWidgetItem(widget, widgetItemRootPath, widgetLabel, validationErrors);
        }

        if (validationErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "The widget manifest is invalid:" + Environment.NewLine + string.Join(Environment.NewLine, validationErrors.Select(error => $"- {error}"))
            );
        }
    }

    private static void TryValidateHomepage(string? homepage, List<string> validationErrors)
    {
        if (string.IsNullOrWhiteSpace(homepage))
        {
            return;
        }

        if (!Uri.TryCreate(homepage, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            validationErrors.Add("homepage must be a valid absolute http or https URL.");
        }
    }

    private string ResolveEntryFilePath(string widgetRootPath, string entryFile)
    {
        var normalizedEntryFile = NormalizeAndValidateRelativePath(entryFile, allowHtml: true);
        return ResolvePathInsideRoot(widgetRootPath, normalizedEntryFile);
    }

    private static string ResolveWidgetRootPath(string packageRootPath, string relativePath)
    {
        var normalizedRelativePath = NormalizeAndValidateRelativePath(relativePath, allowHtml: true);
        return ResolvePathInsideRoot(packageRootPath, normalizedRelativePath);
    }

    private static string ResolvePathInsideRoot(string widgetRootPath, string relativePath)
    {
        var rootPathWithSeparator = Path.GetFullPath(widgetRootPath) + Path.DirectorySeparatorChar;
        var resolvedPath = Path.GetFullPath(Path.Combine(widgetRootPath, relativePath));
        if (!resolvedPath.StartsWith(rootPathWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The widget manifest contains a path outside the widget root.");
        }

        return resolvedPath;
    }

    private static string NormalizeAndValidateRelativePath(string? path, bool allowHtml)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("The widget manifest contains an empty path.");
        }

        if (Path.IsPathRooted(path))
        {
            throw new InvalidOperationException("The widget manifest path must be relative.");
        }

        var normalizedPath = NormalizeRelativePath(path);
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment == ".."))
        {
            throw new InvalidOperationException("The widget manifest path is not allowed.");
        }

        if (!allowHtml && string.Equals(Path.GetExtension(normalizedPath), ".html", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("HTML files are not allowed in this manifest field.");
        }

        return normalizedPath;
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
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
        return BuildUniqueDirectoryPath(_widgetsDataDirectory, SanitizeFileName(Path.GetFileNameWithoutExtension(fileName)));
    }

    private static string BuildUniqueDirectoryPath(string parentDirectory, string sanitizedName)
    {
        var firstCandidate = Path.Combine(parentDirectory, sanitizedName);
        if (!Directory.Exists(firstCandidate) && !File.Exists(firstCandidate))
        {
            return firstCandidate;
        }

        for (var index = 2; index < 10000; index++)
        {
            var candidate = Path.Combine(parentDirectory, $"{sanitizedName}-{index}");
            if (!Directory.Exists(candidate) && !File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(parentDirectory, $"{sanitizedName}-{Guid.NewGuid():N}");
    }

    private static void CopyWidgetDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directoryPath in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativeDirectoryPath = Path.GetRelativePath(sourceDirectory, directoryPath);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativeDirectoryPath));
        }

        foreach (var filePath in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativeFilePath = Path.GetRelativePath(sourceDirectory, filePath);
            var destinationFilePath = Path.Combine(destinationDirectory, relativeFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);
            File.Copy(filePath, destinationFilePath, overwrite: true);
        }
    }

    private static void CopyWidgetContent(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directoryPath in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativeDirectoryPath = Path.GetRelativePath(sourceDirectory, directoryPath);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativeDirectoryPath));
        }

        foreach (var filePath in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            if (string.Equals(Path.GetFileName(filePath), WidgetPackageConstants.ManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relativeFilePath = Path.GetRelativePath(sourceDirectory, filePath);
            var destinationFilePath = Path.Combine(destinationDirectory, relativeFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);
            File.Copy(filePath, destinationFilePath, overwrite: true);
        }
    }

    private static List<string> CollectAssetPaths(string widgetRootPath, string entryFile)
    {
        var normalizedEntryFile = NormalizeRelativePath(entryFile);
        return Directory
            .EnumerateFiles(widgetRootPath, "*", SearchOption.AllDirectories)
            .Select(path => NormalizeRelativePath(Path.GetRelativePath(widgetRootPath, path)))
            .Where(path => !string.Equals(path, WidgetPackageConstants.ManifestFileName, StringComparison.OrdinalIgnoreCase))
            .Where(path => !string.Equals(path, normalizedEntryFile, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static WidgetPackageItemManifest GetSingleWidget(WidgetPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.Widgets.Count != 1)
        {
            throw new InvalidOperationException("The managed widget manifest must contain exactly one widget.");
        }

        return manifest.Widgets[0];
    }

    private static WidgetPackageManifest CreateSingleWidgetManifest(
        string packageName,
        string widgetName,
        string entryFile,
        List<string> assets,
        string widgetId,
        string? createdUtc,
        WidgetPackageManifest? sourceManifest = null
    )
    {
        return new WidgetPackageManifest
        {
            SchemaVersion = WidgetPackageConstants.SupportedSchemaVersion,
            Name = packageName,
            Description = sourceManifest?.Description,
            Author = sourceManifest?.Author,
            Version = sourceManifest?.Version,
            Homepage = sourceManifest?.Homepage,
            CreatedUtc = createdUtc,
            ExportedUtc = sourceManifest?.ExportedUtc,
            Tags = sourceManifest?.Tags is null ? null : [.. sourceManifest.Tags],
            Requirements = sourceManifest?.Requirements,
            Dev = sourceManifest?.Dev,
            Widgets =
            [
                new WidgetPackageItemManifest
                {
                    WidgetId = widgetId,
                    Name = widgetName,
                    RelativePath = ".",
                    EntryFile = NormalizeRelativePath(entryFile),
                    Assets = [.. assets]
                }
            ]
        };
    }

    private WidgetPackageManifest ExportPackage(
        IReadOnlyList<WidgetManagedReference> sourceReferences,
        string exportRootPath,
        string packageName,
        bool useWorkspaceSubdirectory
    )
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

        foreach (var sourceReference in sourceReferences)
        {
            var isManagedSource = IsManagedWidgetPath(sourceReference.EntryFilePath);
            var itemDirectoryName = useWorkspaceSubdirectory
                ? BuildUniquePackageItemDirectoryName(contentRootPath, Path.GetFileNameWithoutExtension(sourceReference.WidgetName))
                : ".";
            var itemRelativePath = useWorkspaceSubdirectory
                ? NormalizeRelativePath(Path.Combine(WidgetPackageConstants.WorkspaceWidgetsDirectoryName, itemDirectoryName))
                : ".";
            var itemDestinationRoot = useWorkspaceSubdirectory
                ? Path.Combine(contentRootPath, itemDirectoryName)
                : exportRootPath;

            if (isManagedSource)
            {
                CopyWidgetContent(sourceReference.WidgetRootPath, itemDestinationRoot);
            }
            else
            {
                Directory.CreateDirectory(itemDestinationRoot);
                File.Copy(
                    sourceReference.EntryFilePath,
                    Path.Combine(itemDestinationRoot, Path.GetFileName(sourceReference.EntryFilePath)),
                    overwrite: false
                );
            }

            var sourceManifest = isManagedSource ? LoadManifestIfPresent(sourceReference.WidgetRootPath) : null;
            var sourceWidget = sourceManifest is null ? null : GetSingleWidget(sourceManifest);
            var entryFile = sourceWidget?.EntryFile ?? Path.GetFileName(sourceReference.EntryFilePath);

            var packageItem = new WidgetPackageItemManifest
            {
                WidgetId = sourceWidget?.WidgetId ?? Guid.NewGuid().ToString("N"),
                Name = Path.GetFileNameWithoutExtension(sourceWidget?.Name ?? sourceReference.WidgetName),
                RelativePath = itemRelativePath,
                EntryFile = NormalizeRelativePath(entryFile),
                Assets = CollectAssetPaths(itemDestinationRoot, entryFile)
            };

            if (sourceManifest is not null && packageManifest.Widgets.Count == 0)
            {
                packageManifest.Description = sourceManifest.Description;
                packageManifest.Author = sourceManifest.Author;
                packageManifest.Version = sourceManifest.Version;
                packageManifest.Homepage = sourceManifest.Homepage;
                packageManifest.CreatedUtc = sourceManifest.CreatedUtc;
                packageManifest.Tags = sourceManifest.Tags is null ? null : [.. sourceManifest.Tags];
                packageManifest.Requirements = sourceManifest.Requirements;
                packageManifest.Dev = sourceManifest.Dev;
            }

            packageManifest.Widgets.Add(packageItem);
        }

        return packageManifest;
    }

    private static string BuildUniquePackageItemDirectoryName(string parentDirectory, string suggestedName)
    {
        var sanitizedName = SanitizeFileName(suggestedName);
        return Path.GetFileName(BuildUniqueDirectoryPath(parentDirectory, sanitizedName));
    }

    private void ValidateWidgetItem(
        WidgetPackageItemManifest widget,
        string widgetRootPath,
        string widgetLabel,
        List<string> validationErrors
    )
    {
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetLabel);
        ArgumentNullException.ThrowIfNull(validationErrors);

        string? entryFilePath = null;
        try
        {
            entryFilePath = ResolveEntryFilePath(widgetRootPath, widget.EntryFile);
            if (!File.Exists(entryFilePath))
            {
                validationErrors.Add($"{widgetLabel}: entryFile does not exist.");
            }
        }
        catch (InvalidOperationException ex)
        {
            validationErrors.Add($"{widgetLabel}: entryFile is invalid: {ex.Message}");
        }

        var normalizedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assetPath in widget.Assets)
        {
            try
            {
                var normalizedAssetPath = NormalizeAndValidateRelativePath(assetPath, allowHtml: true);
                if (!normalizedAssets.Add(normalizedAssetPath))
                {
                    validationErrors.Add($"{widgetLabel}: duplicate asset path: {normalizedAssetPath}.");
                    continue;
                }

                var resolvedAssetPath = ResolvePathInsideRoot(widgetRootPath, normalizedAssetPath);
                if (!File.Exists(resolvedAssetPath))
                {
                    validationErrors.Add($"{widgetLabel}: asset does not exist: {normalizedAssetPath}.");
                }
            }
            catch (InvalidOperationException ex)
            {
                validationErrors.Add($"{widgetLabel}: asset path '{assetPath}' is invalid: {ex.Message}");
            }
        }

        if (string.IsNullOrWhiteSpace(entryFilePath) || !File.Exists(entryFilePath))
        {
            return;
        }

        var normalizedEntryFile = NormalizeRelativePath(Path.GetRelativePath(widgetRootPath, entryFilePath));
        var actualFiles = Directory
            .EnumerateFiles(widgetRootPath, "*", SearchOption.AllDirectories)
            .Select(path => NormalizeRelativePath(Path.GetRelativePath(widgetRootPath, path)))
            .Where(path => !string.Equals(path, WidgetPackageConstants.ManifestFileName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var relativeFilePath in actualFiles)
        {
            if (string.Equals(relativeFilePath, normalizedEntryFile, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!normalizedAssets.Contains(relativeFilePath))
            {
                validationErrors.Add($"{widgetLabel}: package contains undeclared file: {relativeFilePath}.");
            }
        }
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

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedChars = fileName
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray();

        var sanitized = new string(sanitizedChars).Trim();
        sanitized = Regex.Replace(sanitized, "\\s+", " ");
        return string.IsNullOrWhiteSpace(sanitized) ? "widget" : sanitized;
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(WidgetStateSnapshot))]
    [JsonSerializable(typeof(List<WidgetStateRecord>))]
    private sealed partial class WidgetStateJsonContext : JsonSerializerContext;
}
