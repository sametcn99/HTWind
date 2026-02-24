using System.IO;

namespace HTWind.Services;

public sealed class ApplicationBootstrapper : IApplicationBootstrapper
{
    private readonly MainWindow _mainWindow;
    private readonly IThemeService _themeService;
    private readonly IWidgetManager _widgetManager;
    private readonly IWidgetTemplateService _widgetTemplateService;

    public ApplicationBootstrapper(
        IWidgetManager widgetManager,
        IWidgetTemplateService widgetTemplateService,
        IThemeService themeService,
        MainWindow mainWindow
    )
    {
        _widgetManager = widgetManager ?? throw new ArgumentNullException(nameof(widgetManager));
        _widgetTemplateService = widgetTemplateService ?? throw new ArgumentNullException(nameof(widgetTemplateService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
    }

    public void Initialize()
    {
        _mainWindow.SetThemeService(_themeService);
        _themeService.ApplyTheme(ThemeOption.Device);

        _mainWindow.Show();
        EnsureTemplateFilesMaterialized();
        _widgetManager.LoadPersistedWidgets();

        SyncBuiltInWidgetFiles(_widgetManager, _widgetTemplateService);
        EnsureDefaultWidgets();
    }

    private void EnsureTemplateFilesMaterialized()
    {
        foreach (BuiltInWidgetType templateType in Enum.GetValues(typeof(BuiltInWidgetType)))
        {
            _widgetTemplateService.CreateTemplateFile(templateType);
        }
    }

    private void EnsureDefaultWidgets()
    {
        if (_widgetManager.Widgets.Count > 0)
        {
            return;
        }

        foreach (BuiltInWidgetType templateType in Enum.GetValues(typeof(BuiltInWidgetType)))
        {
            var templatePath = _widgetTemplateService.CreateTemplateFile(templateType);
            _widgetManager.AddWidget(templatePath, false);
        }
    }

    private static void SyncBuiltInWidgetFiles(
        IWidgetManager widgetManager,
        IWidgetTemplateService widgetTemplateService
    )
    {
        var builtInTemplatePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["clock.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.Clock),
            ["weather.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.Weather),
            ["tictactoe.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.TicTacToe),
            ["system-monitor.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.SystemMonitor),
            ["app-launcher.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.AppLauncher),
            ["visualizer.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.Visualizer),
            ["spotify-visualizer.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.Visualizer),
            ["search-box.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.SearchBox),
            ["quick-links.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.QuickLinks),
            ["clipboard-studio.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.ClipboardStudio),
            ["system-time.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.SystemTime),
            ["memory-stats.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.MemoryStats),
            ["environment-info.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.EnvironmentInfo),
            ["network-tools.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.NetworkTools),
            ["process-manager.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.ProcessManager),
            ["file-explorer.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.FileExplorer),
            ["text-file-editor.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.TextFileEditor),
            ["media-controls.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.MediaControls),
            ["dns-lookup.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.DnsLookup),
            ["file-actions.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.FileActions),
            ["drive-roots.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.DriveRoots),
            ["powershell-console.html"] = widgetTemplateService.CreateTemplateFile(BuiltInWidgetType.PowerShellConsole)
        };

        foreach (var widget in widgetManager.Widgets)
        {
            if (string.IsNullOrWhiteSpace(widget.Name))
            {
                continue;
            }

            if (!builtInTemplatePaths.TryGetValue(widget.Name, out var templatePath))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(widget.FilePath) || !File.Exists(widget.FilePath))
            {
                widget.FilePath = templatePath;
                continue;
            }
        }
    }
}
