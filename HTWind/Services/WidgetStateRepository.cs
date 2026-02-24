using System.IO;
using System.Text.Json;

namespace HTWind.Services;

public sealed class WidgetStateRepository : IWidgetStateRepository
{
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

    public IReadOnlyList<WidgetStateRecord> Load()
    {
        if (!File.Exists(_stateFilePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_stateFilePath);
            var states = JsonSerializer.Deserialize<List<WidgetStateRecord>>(json);
            return states ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Save(IEnumerable<WidgetStateRecord> states)
    {
        ArgumentNullException.ThrowIfNull(states);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
            var json = JsonSerializer.Serialize(states, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_stateFilePath, json);
        }
        catch
        {
            // Persistence failures should not interrupt widget usage.
        }
    }

    public string CopyWidgetToManagedStorage(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(sourcePath));
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Selected widget file was not found.", sourcePath);
        }

        Directory.CreateDirectory(_widgetsDataDirectory);

        var fileName = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);
        var sanitizedName = SanitizeFileName(fileName);
        var targetPath = BuildUniqueWidgetPath(sanitizedName, extension);

        File.Copy(sourcePath, targetPath, false);
        return targetPath;
    }

    public string CreateManagedWidgetFile(string requestedFileName, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedFileName);
        ArgumentNullException.ThrowIfNull(content);

        Directory.CreateDirectory(_widgetsDataDirectory);

        var effectiveExtension = ".html";
        var baseName = Path.GetFileNameWithoutExtension(requestedFileName);
        var sanitizedName = SanitizeFileName(baseName);
        var targetPath = BuildUniqueWidgetPath(sanitizedName, effectiveExtension);

        File.WriteAllText(targetPath, content);
        return targetPath;
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

            if (
                fullPath.StartsWith(managedRoot, StringComparison.OrdinalIgnoreCase)
                && File.Exists(fullPath)
            )
            {
                File.Delete(fullPath);
            }
        }
        catch
        {
            // Avoid blocking widget removal for file system edge cases.
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedChars = fileName
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray();

        var sanitized = new string(sanitizedChars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "widget" : sanitized;
    }

    private string BuildUniqueWidgetPath(string sanitizedName, string extension)
    {
        var firstCandidate = Path.Combine(_widgetsDataDirectory, $"{sanitizedName}{extension}");
        if (!File.Exists(firstCandidate))
        {
            return firstCandidate;
        }

        for (var index = 2; index < 10000; index++)
        {
            var candidate = Path.Combine(
                _widgetsDataDirectory,
                $"{sanitizedName}-{index}{extension}"
            );

            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        // Fallback keeps uniqueness even under extreme collisions.
        return Path.Combine(_widgetsDataDirectory, $"{sanitizedName}-{Guid.NewGuid():N}{extension}");
    }
}
