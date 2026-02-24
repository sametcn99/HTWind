using System.Windows;

using Wpf.Ui.Appearance;

namespace HTWind.Services;

public class ThemeService : IThemeService
{
    private readonly Window _window;
    private bool _isWatching;
    private RoutedEventHandler? _pendingLoadedHandler;

    public ThemeService(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public ThemeOption CurrentTheme { get; private set; } = ThemeOption.Device;

    public void ApplyTheme(ThemeOption option)
    {
        CurrentTheme = option;

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
