using System.Windows;

using HTWind.Localization;

namespace HTWind;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        LocalizationService.SetCulture("en-US");

        var serviceContainer = new ServiceContainer();

        if (!serviceContainer.ExecutionRiskConsentService.HasAccepted())
        {
            var consentWindow = new RiskConsentWindow();
            var dialogAccepted = consentWindow.ShowDialog() == true && consentWindow.IsAccepted;

            if (!dialogAccepted)
            {
                Shutdown(1);
                return;
            }

            serviceContainer.ExecutionRiskConsentService.MarkAccepted();
        }

        var mainWindowViewModel = serviceContainer.CreateMainWindowViewModel();
        var mainWindow = new MainWindow(mainWindowViewModel, serviceContainer.WidgetManager);
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        var bootstrapper = serviceContainer.CreateBootstrapper(mainWindow);

        bootstrapper.Initialize();

        base.OnStartup(e);
    }
}
