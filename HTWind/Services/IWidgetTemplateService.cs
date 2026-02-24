namespace HTWind.Services;

public enum BuiltInWidgetType
{
    Clock,
    Weather,
    TicTacToe,
    SystemMonitor,
    AppLauncher,
    Visualizer,
    SearchBox,
    QuickLinks,
    ClipboardStudio,
    SystemTime,
    MemoryStats,
    EnvironmentInfo,
    NetworkTools,
    ProcessManager,
    FileExplorer,
    TextFileEditor,
    MediaControls,
    DnsLookup,
    FileActions,
    DriveRoots,
    PowerShellConsole
}

public interface IWidgetTemplateService
{
    string CreateTemplateFile(BuiltInWidgetType widgetType);
}
