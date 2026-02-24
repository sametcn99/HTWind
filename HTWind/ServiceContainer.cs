using HTWind.Services;
using HTWind.ViewModels;

namespace HTWind;

public sealed class ServiceContainer
{
    public ServiceContainer()
    {
        FileDialogService = new FileDialogService();
        WidgetHostApiService = new WidgetHostApiService();
        WidgetWindowFactory = new WidgetWindowFactory(WidgetHostApiService);
        WidgetStateRepository = new WidgetStateRepository();
        WidgetGeometryService = new WidgetGeometryService();
        HtmlEditorService = new HtmlEditorService();
        WidgetManager = new WidgetManager(
            WidgetWindowFactory,
            WidgetStateRepository,
            WidgetGeometryService,
            HtmlEditorService
        );
        WidgetTemplateService = new WidgetTemplateService();
        StartupRegistrationService = new StartupRegistrationService();
        ExecutionRiskConsentService = new ExecutionRiskConsentService();
    }

    public IFileDialogService FileDialogService { get; }

    public IWidgetHostApiService WidgetHostApiService { get; }

    public IWidgetWindowFactory WidgetWindowFactory { get; }

    public IWidgetManager WidgetManager { get; }

    public IWidgetStateRepository WidgetStateRepository { get; }

    public IWidgetGeometryService WidgetGeometryService { get; }

    public IHtmlEditorService HtmlEditorService { get; }

    public IWidgetTemplateService WidgetTemplateService { get; }

    public IStartupRegistrationService StartupRegistrationService { get; }

    public IExecutionRiskConsentService ExecutionRiskConsentService { get; }

    public MainWindowViewModel CreateMainWindowViewModel()
    {
        return new MainWindowViewModel(
            FileDialogService,
            WidgetManager,
            StartupRegistrationService
        );
    }

    public IApplicationBootstrapper CreateBootstrapper(MainWindow mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);

        var themeService = new ThemeService(mainWindow);
        return new ApplicationBootstrapper(
            WidgetManager,
            WidgetTemplateService,
            themeService,
            mainWindow
        );
    }
}
