using System.IO;
using System.Reflection;
using System.Text;

namespace HTWind.Services;

public class WidgetTemplateService : IWidgetTemplateService
{
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

    private readonly string _templateDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HTWind",
        "BuiltInTemplates"
    );

    public string CreateTemplateFile(BuiltInWidgetType widgetType)
    {
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

        var path = Path.Combine(_templateDirectory, fileName);
        File.WriteAllText(path, LoadEmbeddedTemplate(widgetType), Encoding.UTF8);
        return path;
    }

    private static string LoadEmbeddedTemplate(BuiltInWidgetType widgetType)
    {
        if (!EmbeddedTemplateMap.TryGetValue(widgetType, out var resourceName))
        {
            throw new ArgumentOutOfRangeException(nameof(widgetType), widgetType, null);
        }

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException($"Template resource not found: {resourceName}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
