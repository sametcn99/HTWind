using System.Windows;
using System.Windows.Media;

using HTWind.Services.WebView;

using Wpf.Ui.Appearance;

namespace HTWind.Services.Application;

public class ThemeService : IThemeService
{
    private readonly Window _window;
    private readonly IWidgetHostApiService? _widgetHostApiService;
    private bool _isWatching;
    private bool _isApplicationThemeHooked;
    private RoutedEventHandler? _pendingLoadedHandler;

    public ThemeService(Window window, IWidgetHostApiService? widgetHostApiService = null)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _widgetHostApiService = widgetHostApiService;
    }

    public ThemeOption CurrentTheme { get; private set; } = ThemeOption.Device;

    public void ApplyTheme(ThemeOption option)
    {
        CurrentTheme = option;

        EnsureApplicationThemeHooked();

        switch (option)
        {
            case ThemeOption.Device:
                var systemTheme = ApplicationThemeManager.GetSystemTheme();
                var appTheme = systemTheme
                    is SystemTheme.Dark
                    or SystemTheme.CapturedMotion
                    or SystemTheme.Glow
                    ? ApplicationTheme.Dark
                    : ApplicationTheme.Light;
                ApplicationThemeManager.Apply(appTheme);
                StartWatchingSystemTheme();
                break;
            case ThemeOption.Light:
                StopWatchingSystemTheme();
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                break;
            case ThemeOption.Dark:
                StopWatchingSystemTheme();
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                break;
        }

        PublishThemeChanged();
    }

    private static void OnApplicationThemeManagerChanged(ApplicationTheme currentApplicationTheme, Color systemAccent)
    {
        _ = currentApplicationTheme;
        _ = systemAccent;

        // Static hook cannot reach an instance; broadcast through the shared service list.
        foreach (var service in ActiveServices)
        {
            service.PublishThemeChanged();
        }
    }

    private static readonly List<ThemeService> ActiveServices = [];
    private static readonly object ActiveServicesSync = new();

    private void PublishThemeChanged()
    {
        try
        {
            _widgetHostApiService?.PublishThemeChanged();
        }
        catch
        {
        }
    }

    private void EnsureApplicationThemeHooked()
    {
        if (_isApplicationThemeHooked)
        {
            return;
        }

        try
        {
            ApplicationThemeManager.Changed += OnApplicationThemeManagerChanged;
            lock (ActiveServicesSync)
            {
                ActiveServices.Add(this);
            }

            _isApplicationThemeHooked = true;
        }
        catch
        {
            // Theme change notification is best-effort.
        }
    }

    private void StartWatchingSystemTheme()
    {
        RemovePendingLoadedHandler();

        if (_isWatching)
        {
            return;
        }

        if (_window.IsLoaded)
        {
            SystemThemeWatcher.Watch(_window);
            _isWatching = true;
            return;
        }

        _pendingLoadedHandler = (_, _) =>
        {
            RemovePendingLoadedHandler();

            if (CurrentTheme != ThemeOption.Device || _isWatching)
            {
                return;
            }

            SystemThemeWatcher.Watch(_window);
            _isWatching = true;
        };

        _window.Loaded += _pendingLoadedHandler;
    }

    private void StopWatchingSystemTheme()
    {
        RemovePendingLoadedHandler();

        if (!_isWatching)
        {
            return;
        }

        if (!_window.IsLoaded)
        {
            return;
        }

        SystemThemeWatcher.UnWatch(_window);
        _isWatching = false;
    }

    private void RemovePendingLoadedHandler()
    {
        if (_pendingLoadedHandler is null)
        {
            return;
        }

        _window.Loaded -= _pendingLoadedHandler;
        _pendingLoadedHandler = null;
    }
}
