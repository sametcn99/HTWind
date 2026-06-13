using HTWind.Model;
using HTWind.Services.Application;
using HTWind.Services.Editor;
using HTWind.Services.Templates;
using HTWind.Services.WebView;
using HTWind.Services.Widget;
using HTWind.ViewModels;

namespace HTWind;

public sealed class ServiceContainer
{
    public ServiceContainer()
    {
        FileDialogService = new FileDialogService();
        DeveloperModeService = new DeveloperModeService();
        WebViewEnvironmentProvider = new WebViewEnvironmentProvider();
        WidgetPermissionStateService = new WidgetPermissionStateService();
        WidgetHostApiService = new WidgetHostApiService();
        WidgetWindowFactory = new WidgetWindowFactory(
            WidgetHostApiService,
            WebViewEnvironmentProvider,
            DeveloperModeService,
            WidgetPermissionStateService
        );
        WidgetStateStore = new WidgetStateStore();
        WidgetImportService = new WidgetImportService();
        WidgetExportService = new WidgetExportService();
        WidgetGeometryService = new WidgetGeometryService();
        HtmlEditorService = new HtmlEditorService(WebViewEnvironmentProvider);

        WidgetSuppressionMonitor = new WidgetSuppressionMonitor();
        WidgetWindowTracker = new WidgetWindowTracker();
        WidgetEditorManager = new WidgetEditorManager();
        WidgetLifecycleService = new WidgetLifecycleService(WidgetStateStore, WidgetImportService);

        WidgetManager = new WidgetManager(
            WidgetWindowFactory,
            WidgetStateStore,
            WidgetLifecycleService,
            WidgetWindowTracker,
            WidgetEditorManager,
            WidgetSuppressionMonitor,
            WidgetGeometryService,
            HtmlEditorService,
            WidgetExportService,
            WidgetPermissionStateService
        );
        WidgetTemplateService = new WidgetTemplateService();
        StartupRegistrationService = new StartupRegistrationService();
        ExecutionRiskConsentService = new ExecutionRiskConsentService();
        BuiltInWidgetInitializationStateService = new BuiltInWidgetInitializationStateService();
    }

    public IFileDialogService FileDialogService { get; }

    public IWidgetHostApiService WidgetHostApiService { get; }

    public IWidgetPermissionStateService WidgetPermissionStateService { get; }

    public IDeveloperModeService DeveloperModeService { get; }

    public IWebViewEnvironmentProvider WebViewEnvironmentProvider { get; }

    public IWidgetWindowFactory WidgetWindowFactory { get; }

    public IWidgetManager WidgetManager { get; }

    public IWidgetStateStore WidgetStateStore { get; }

    public IWidgetImportService WidgetImportService { get; }

    public IWidgetExportService WidgetExportService { get; }

    public IWidgetGeometryService WidgetGeometryService { get; }

    public IHtmlEditorService HtmlEditorService { get; }

    public IWidgetTemplateService WidgetTemplateService { get; }

    public IStartupRegistrationService StartupRegistrationService { get; }

    public IExecutionRiskConsentService ExecutionRiskConsentService { get; }

    public IBuiltInWidgetInitializationStateService BuiltInWidgetInitializationStateService { get; }

    public IWidgetSuppressionMonitor WidgetSuppressionMonitor { get; }

    public IWidgetWindowTracker WidgetWindowTracker { get; }

    public IWidgetEditorManager WidgetEditorManager { get; }

    public IWidgetLifecycleService WidgetLifecycleService { get; }

    public MainWindowViewModel CreateMainWindowViewModel()
    {
        return new MainWindowViewModel(
            FileDialogService,
            WidgetManager,
            StartupRegistrationService,
            DeveloperModeService
        );
    }

    public IApplicationBootstrapper CreateBootstrapper(MainWindow mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);

        var themeService = new ThemeService(mainWindow);
        return new ApplicationBootstrapper(
            WidgetManager,
            WidgetTemplateService,
            BuiltInWidgetInitializationStateService,
            themeService,
            mainWindow
        );
    }
}
