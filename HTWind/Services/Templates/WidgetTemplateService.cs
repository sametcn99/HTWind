using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using HTWind.Services.Application;

namespace HTWind.Services.Templates;

public class WidgetTemplateService : IWidgetTemplateService
{
    private const string WidgetPackageSchemaFileName = "htwind.widget.schema.json";
    private const string WidgetPackageSchemaResourceName = "HTWind.Templates.htwind.widget.schema.json";
    private const string VersionStoreFileName = "template-versions.json";

    /// <summary>
    ///     Content version per template. Bump a value whenever the embedded template changes so
    ///     existing installations receive the update (see audit finding B3).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, int> TemplateVersions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["clock.html"] = 1,
        ["weather.html"] = 1,
        ["tictactoe.html"] = 1,
        ["system-monitor.html"] = 2,
        ["app-launcher.html"] = 1,
        ["visualizer.html"] = 2,
        ["spotify-visualizer.html"] = 1,
        ["search-box.html"] = 1,
        ["quick-links.html"] = 1,
        ["clipboard-studio.html"] = 1,
        ["system-time.html"] = 1,
        ["memory-stats.html"] = 2,
        ["environment-info.html"] = 1,
        ["network-tools.html"] = 1,
        ["process-manager.html"] = 1,
        ["file-explorer.html"] = 1,
        ["text-file-editor.html"] = 1,
        ["media-controls.html"] = 2,
        ["dns-lookup.html"] = 1,
        ["file-actions.html"] = 1,
        ["drive-roots.html"] = 1,
        ["powershell-console.html"] = 1,
        [WidgetPackageSchemaFileName] = 1
    };

    private static readonly IReadOnlyDictionary<BuiltInWidgetType, string> EmbeddedTemplateMap =
        new Dictionary<BuiltInWidgetType, string>
        {
            [BuiltInWidgetType.Clock] = "HTWind.Templates.clock.html",
            [BuiltInWidgetType.Weather] = "HTWind.Templates.weather.html",
            [BuiltInWidgetType.TicTacToe] = "HTWind.Templates.tictactoe.html",
            [BuiltInWidgetType.SystemMonitor] = "HTWind.Templates.system-monitor.html",
            [BuiltInWidgetType.AppLauncher] = "HTWind.Templates.app-launcher.html",
            [BuiltInWidgetType.Visualizer] = "HTWind.Templates.visualizer.html",
            [BuiltInWidgetType.SearchBox] = "HTWind.Templates.search-box.html",
            [BuiltInWidgetType.QuickLinks] = "HTWind.Templates.quick-links.html",
            [BuiltInWidgetType.ClipboardStudio] = "HTWind.Templates.clipboard-studio.html",
            [BuiltInWidgetType.SystemTime] = "HTWind.Templates.system-time.html",
            [BuiltInWidgetType.MemoryStats] = "HTWind.Templates.memory-stats.html",
            [BuiltInWidgetType.EnvironmentInfo] = "HTWind.Templates.environment-info.html",
            [BuiltInWidgetType.NetworkTools] = "HTWind.Templates.network-tools.html",
            [BuiltInWidgetType.ProcessManager] = "HTWind.Templates.process-manager.html",
            [BuiltInWidgetType.FileExplorer] = "HTWind.Templates.file-explorer.html",
            [BuiltInWidgetType.TextFileEditor] = "HTWind.Templates.text-file-editor.html",
            [BuiltInWidgetType.MediaControls] = "HTWind.Templates.media-controls.html",
            [BuiltInWidgetType.DnsLookup] = "HTWind.Templates.dns-lookup.html",
            [BuiltInWidgetType.FileActions] = "HTWind.Templates.file-actions.html",
            [BuiltInWidgetType.DriveRoots] = "HTWind.Templates.drive-roots.html",
            [BuiltInWidgetType.PowerShellConsole] = "HTWind.Templates.powershell-console.html"
        };

    private readonly string _templateDirectory;
    private readonly object _materializedTemplatesSync = new();
    private readonly Dictionary<BuiltInWidgetType, string> _materializedTemplatePaths = new();
    private Dictionary<string, TemplateVersionRecord>? _versionRecords;

    public WidgetTemplateService(string? basePath = null)
    {
        _templateDirectory = HTWindPathProvider.GetTemplateDirectory(basePath);
    }

    public string CreateTemplateFile(BuiltInWidgetType widgetType)
    {
        lock (_materializedTemplatesSync)
        {
            if (_materializedTemplatePaths.TryGetValue(widgetType, out var cachedPath))
            {
                return cachedPath;
            }
        }

        Directory.CreateDirectory(_templateDirectory);

        var fileName = widgetType switch
        {
            BuiltInWidgetType.Clock => "clock.html",
            BuiltInWidgetType.Weather => "weather.html",
            BuiltInWidgetType.TicTacToe => "tictactoe.html",
            BuiltInWidgetType.SystemMonitor => "system-monitor.html",
            BuiltInWidgetType.AppLauncher => "app-launcher.html",
            BuiltInWidgetType.Visualizer => "visualizer.html",
            BuiltInWidgetType.SearchBox => "search-box.html",
            BuiltInWidgetType.QuickLinks => "quick-links.html",
            BuiltInWidgetType.ClipboardStudio => "clipboard-studio.html",
            BuiltInWidgetType.SystemTime => "system-time.html",
            BuiltInWidgetType.MemoryStats => "memory-stats.html",
            BuiltInWidgetType.EnvironmentInfo => "environment-info.html",
            BuiltInWidgetType.NetworkTools => "network-tools.html",
            BuiltInWidgetType.ProcessManager => "process-manager.html",
            BuiltInWidgetType.FileExplorer => "file-explorer.html",
            BuiltInWidgetType.TextFileEditor => "text-file-editor.html",
            BuiltInWidgetType.MediaControls => "media-controls.html",
            BuiltInWidgetType.DnsLookup => "dns-lookup.html",
            BuiltInWidgetType.FileActions => "file-actions.html",
            BuiltInWidgetType.DriveRoots => "drive-roots.html",
            BuiltInWidgetType.PowerShellConsole => "powershell-console.html",
            _ => throw new ArgumentOutOfRangeException(nameof(widgetType), widgetType, null)
        };

        var path = MaterializeTemplateFile(fileName, LoadEmbeddedTemplate(widgetType));

        lock (_materializedTemplatesSync)
        {
            _materializedTemplatePaths[widgetType] = path;
        }

        return path;
    }

    public string CreateWidgetPackageSchemaFile()
    {
        Directory.CreateDirectory(_templateDirectory);
        return MaterializeTemplateFile(
            WidgetPackageSchemaFileName,
            LoadEmbeddedTextResource(WidgetPackageSchemaResourceName));
    }

    /// <summary>
    ///     Writes the embedded template content with version/hash tracking. Files the user has
    ///     modified are never clobbered: a file is only overwritten while its on-disk hash still
    ///     matches the hash HTWind last wrote. Legacy files written before this tracking existed
    ///     are refreshed once so app updates actually reach existing users (audit B3).
    /// </summary>
    private string MaterializeTemplateFile(string fileName, string embeddedContent)
    {
        var path = Path.Combine(_templateDirectory, fileName);
        var embeddedHash = ComputeHash(embeddedContent);
        var currentVersion = TemplateVersions.TryGetValue(fileName, out var version) ? version : 1;

        lock (_materializedTemplatesSync)
        {
            var records = _versionRecords ??= LoadVersionRecords();
            records.TryGetValue(fileName, out var record);

            string onDiskHash;
            if (File.Exists(path))
            {
                try
                {
                    onDiskHash = ComputeHash(File.ReadAllText(path));
                }
                catch
                {
                    onDiskHash = string.Empty;
                }
            }
            else
            {
                onDiskHash = string.Empty;
                File.WriteAllText(path, embeddedContent, Encoding.UTF8);
            }

            var isUserModified =
                record is not null
                && (!string.Equals(record.Hash, onDiskHash, StringComparison.Ordinal)
                    || string.Equals(record.State, TemplateVersionRecord.UserModifiedState, StringComparison.Ordinal));

            if (record is null)
            {
                // Legacy install: no tracking record exists yet. Refresh the file with the
                // current embedded content so fixes reach existing users, then protect it going
                // forward via the recorded hash.
                if (!string.Equals(onDiskHash, embeddedHash, StringComparison.Ordinal))
                {
                    File.WriteAllText(path, embeddedContent, Encoding.UTF8);
                }

                record = new TemplateVersionRecord
                {
                    Version = currentVersion,
                    Hash = embeddedHash,
                    State = TemplateVersionRecord.CurrentState
                };
            }
            else if (isUserModified)
            {
                // User edited the file; never clobber, only refresh the baseline hash.
                record.Hash = onDiskHash;
                record.State = TemplateVersionRecord.UserModifiedState;
                record.Version = Math.Max(record.Version, currentVersion);
            }
            else if (!string.Equals(onDiskHash, embeddedHash, StringComparison.Ordinal))
            {
                // Stock file that differs from the embedded content: apply the update.
                File.WriteAllText(path, embeddedContent, Encoding.UTF8);
                record.Version = currentVersion;
                record.Hash = embeddedHash;
                record.State = TemplateVersionRecord.CurrentState;
            }
            else
            {
                record.Version = currentVersion;
                record.Hash = embeddedHash;
                record.State = TemplateVersionRecord.CurrentState;
            }

            records[fileName] = record;
            SaveVersionRecords(records);
        }

        return path;
    }

    private string _versionRecordsFilePath => Path.Combine(_templateDirectory, VersionStoreFileName);

    private Dictionary<string, TemplateVersionRecord> LoadVersionRecords()
    {
        try
        {
            if (!File.Exists(_versionRecordsFilePath))
            {
                return [];
            }

            return JsonSerializer.Deserialize<Dictionary<string, TemplateVersionRecord>>(
                File.ReadAllText(_versionRecordsFilePath))
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveVersionRecords(Dictionary<string, TemplateVersionRecord> records)
    {
        try
        {
            Directory.CreateDirectory(_templateDirectory);
            AtomicFile.WriteAllText(
                _versionRecordsFilePath,
                JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private sealed class TemplateVersionRecord
    {
        public const string CurrentState = "current";
        public const string UserModifiedState = "userModified";

        public int Version { get; set; }

        public string Hash { get; set; } = string.Empty;

        public string State { get; set; } = CurrentState;
    }

    private static string LoadEmbeddedTemplate(BuiltInWidgetType widgetType)
    {
        if (!EmbeddedTemplateMap.TryGetValue(widgetType, out var resourceName))
        {
            throw new ArgumentOutOfRangeException(nameof(widgetType), widgetType, null);
        }

        return LoadEmbeddedTextResource(resourceName);
    }

    private static string LoadEmbeddedTextResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException($"Template resource not found: {resourceName}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
