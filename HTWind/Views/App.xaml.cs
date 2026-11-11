using System.Globalization;
using System.Windows;

using HTWind.Localization;
using HTWind.Services.Application;

namespace HTWind;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var serviceContainer = new ServiceContainer();

        // Restore the persisted culture before any window is created so every localized string
        // (including the consent dialog) uses the saved language.
        var persistedSnapshot = serviceContainer.WidgetStateStore.Load();
        SetPersistedCulture(persistedSnapshot.Culture);

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

        var initialTheme = ThemeOption.Device;
        if (
            !string.IsNullOrWhiteSpace(persistedSnapshot.ThemeOption)
            && Enum.TryParse<ThemeOption>(persistedSnapshot.ThemeOption, ignoreCase: true, out var parsedTheme)
        )
        {
            initialTheme = parsedTheme;
        }

        var mainWindowViewModel = serviceContainer.CreateMainWindowViewModel(initialTheme);
        var mainWindow = new MainWindow(mainWindowViewModel, serviceContainer.WidgetManager);
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        var bootstrapper = serviceContainer.CreateBootstrapper(mainWindow);

        bootstrapper.Initialize();

        base.OnStartup(e);
    }

    private static void SetPersistedCulture(string? persistedCulture)
    {
        var cultureName = "en-US";

        if (!string.IsNullOrWhiteSpace(persistedCulture))
        {
            try
            {
                // Validate the persisted value before using it.
                CultureInfo.GetCultureInfo(persistedCulture);
                cultureName = persistedCulture;
            }
            catch
            {
                cultureName = "en-US";
            }
        }

        LocalizationService.SetCulture(cultureName);
    }
}
