using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

using HTWind.Model;

namespace HTWind.Services.Widget;

internal static class WidgetManifestHelper
{
    public static WidgetPackageManifest LoadManifest(string manifestPath, JsonSerializerOptions jsonOptions)
    {
        try
        {
            var manifestJson = File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize<WidgetPackageManifest>(manifestJson, jsonOptions)
                ?? throw new InvalidOperationException("The widget manifest is empty or invalid.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("The widget manifest is not valid JSON.");
        }
    }

    public static WidgetPackageManifest? LoadManifestIfPresent(string widgetRootPath, JsonSerializerOptions jsonOptions)
    {
        var manifestPath = Path.Combine(widgetRootPath, WidgetPackageConstants.ManifestFileName);
        return File.Exists(manifestPath)
            ? LoadManifest(manifestPath, jsonOptions)
            : null;
    }

    public static void WriteManifest(string widgetRootPath, WidgetPackageManifest manifest, JsonSerializerOptions jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var manifestPath = Path.Combine(widgetRootPath, WidgetPackageConstants.ManifestFileName);
        var manifestJson = JsonSerializer.Serialize(manifest, jsonOptions);
        File.WriteAllText(manifestPath, manifestJson);
    }

    public static void ValidateManifest(WidgetPackageManifest manifest, string widgetRootPath)
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

    private static void ValidateWidgetItem(
        WidgetPackageItemManifest widget,
        string widgetRootPath,
        string widgetLabel,
        List<string> validationErrors)
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

    public static string ResolveEntryFilePath(string widgetRootPath, string entryFile)
    {
        var normalizedEntryFile = NormalizeAndValidateRelativePath(entryFile, allowHtml: true);
        return ResolvePathInsideRoot(widgetRootPath, normalizedEntryFile);
    }

    public static string ResolveWidgetRootPath(string packageRootPath, string relativePath)
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

    public static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    public static WidgetManagedReference CreateManagedReference(
        string widgetName,
        string entryFilePath,
        string widgetRootPath,
        string? packageWidgetId)
    {
        return new WidgetManagedReference
        {
            WidgetName = widgetName,
            EntryFilePath = entryFilePath,
            WidgetRootPath = widgetRootPath,
            PackageWidgetId = packageWidgetId
        };
    }

    public static WidgetPackageItemManifest GetSingleWidget(WidgetPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.Widgets.Count != 1)
        {
            throw new InvalidOperationException("The managed widget manifest must contain exactly one widget.");
        }

        return manifest.Widgets[0];
    }

    public static WidgetPackageManifest CreateSingleWidgetManifest(
        string packageName,
        string widgetName,
        string entryFile,
        List<string> assets,
        string widgetId,
        string? createdUtc,
        WidgetPackageManifest? sourceManifest = null)
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

    public static List<string> CollectAssetPaths(string widgetRootPath, string entryFile)
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

    public static void CopyWidgetDirectory(string sourceDirectory, string destinationDirectory)
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

    public static void CopyWidgetContent(string sourceDirectory, string destinationDirectory)
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

    public static string BuildUniqueDirectoryPath(string parentDirectory, string sanitizedName)
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

    public static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedChars = fileName
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray();

        var sanitized = new string(sanitizedChars).Trim();
        sanitized = Regex.Replace(sanitized, "\\s+", " ");
        return string.IsNullOrWhiteSpace(sanitized) ? "widget" : sanitized;
    }
}
