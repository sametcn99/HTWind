using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

using HTWind.Services.Application;
using HTWind.Services.Widget;
using HTWind.ViewModels;
using HTWind.Views.Pages;

using Wpf.Ui.Controls;

namespace HTWind;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IWidgetManager _widgetManager;
    private readonly Dictionary<string, UserControl> _pageCache = new();
    private string _activePageTag = "Home";

    private bool _isExiting;
    private bool _isNavigationReady;
    private IThemeService? _themeService;

    public MainWindow(MainWindowViewModel viewModel, IWidgetManager widgetManager)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _widgetManager =
            widgetManager ?? throw new ArgumentNullException(nameof(widgetManager));

        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.ThemeRequested += ViewModel_ThemeRequested;
        _viewModel.RefreshStartupState();
        Loaded += MainWindow_Loaded;

        try
        {
            var icon = LoadTrayIcon();
            if (icon != null)
            {
                TrayIcon.Icon = icon;
            }

            if (Icon is null)
            {
                Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/favicon.ico"));
            }
        }
        catch
        {
            // Ignore icon extraction errors
        }

    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isNavigationReady)
        {
            return;
        }

        _isNavigationReady = true;
        NavigateUsingItem(HomeNavItem, "Home");
    }

    private void HomeNavItem_Click(object sender, RoutedEventArgs e)
    {
        if (!_isNavigationReady)
        {
            return;
        }

        NavigateUsingItem(HomeNavItem, "Home");
    }

    private void SettingsNavItem_Click(object sender, RoutedEventArgs e)
    {
        if (!_isNavigationReady)
        {
            return;
        }

        NavigateUsingItem(SettingsNavItem, "Settings");
    }

    private void AboutNavItem_Click(object sender, RoutedEventArgs e)
    {
        if (!_isNavigationReady)
        {
            return;
        }

        NavigateUsingItem(AboutNavItem, "About");
    }

    private void NavigateUsingItem(NavigationViewItem item, string tag)
    {
        UpdateActiveNavigationItem(item);
        item.Activate(NavigationView);
        NavigateTo(tag);
    }

    private void UpdateActiveNavigationItem(NavigationViewItem activeItem)
    {
        if (!ReferenceEquals(activeItem, HomeNavItem))
        {
            HomeNavItem.Deactivate(NavigationView);
        }

        if (!ReferenceEquals(activeItem, SettingsNavItem))
        {
            SettingsNavItem.Deactivate(NavigationView);
        }

        if (!ReferenceEquals(activeItem, AboutNavItem))
        {
            AboutNavItem.Deactivate(NavigationView);
        }
    }

    private void NavigateTo(string tag)
    {
        TrimPageCacheIfMemoryPressure(tag);

        if (!_pageCache.TryGetValue(tag, out var page))
        {
            page = tag switch
            {
                "Home" => CreateHomePage(),
                "Settings" => CreateSettingsPage(),
                "About" => new AboutPage(),
                _ => null
            };

            if (page is null)
            {
                return;
            }

            _pageCache[tag] = page;
        }

        _activePageTag = tag;

        NavigationView.ReplaceContent(page, null);
    }

    private void TrimPageCacheIfMemoryPressure(string incomingTag)
    {
        if (_pageCache.Count <= 1)
        {
            return;
        }

        var gcInfo = GC.GetGCMemoryInfo();
        var highMemoryLoadThresholdBytes = gcInfo.HighMemoryLoadThresholdBytes;
        var currentMemoryLoadBytes = gcInfo.MemoryLoadBytes;
        if (highMemoryLoadThresholdBytes <= 0)
        {
            return;
        }

        var isUnderPressure = currentMemoryLoadBytes >= highMemoryLoadThresholdBytes * 0.85;
        if (!isUnderPressure)
        {
            return;
        }

        var tagsToKeep = new HashSet<string>(StringComparer.Ordinal)
        {
            _activePageTag,
            incomingTag
        };

        foreach (var cacheTag in _pageCache.Keys.ToList())
        {
            if (tagsToKeep.Contains(cacheTag))
            {
                continue;
            }

            _pageCache.Remove(cacheTag);
        }
    }

    private HomePage CreateHomePage()
    {
        var homePage = new HomePage(_widgetManager)
        {
            DataContext = _viewModel
        };
        return homePage;
    }

    private SettingsPage CreateSettingsPage()
    {
        var settingsPage = new SettingsPage(_viewModel)
        {
            DataContext = _viewModel
        };
        return settingsPage;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void TrayIcon_Show_Click(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void TrayIcon_Exit_Click(object sender, RoutedEventArgs e)
    {
        _isExiting = true;
        TrayIcon.Dispose();
        Application.Current.Shutdown();
    }

    public void SetThemeService(IThemeService themeService)
    {
        _themeService =
            themeService ?? throw new ArgumentNullException(nameof(themeService));
    }

    private void ViewModel_ThemeRequested(object? sender, ThemeOption option)
    {
        if (_themeService is null)
        {
            return;
        }

        _themeService.ApplyTheme(option);
    }

    protected override void OnClosed(EventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        _viewModel.ThemeRequested -= ViewModel_ThemeRequested;
        if (_widgetManager is IDisposable disposableWidgetManager)
        {
            disposableWidgetManager.Dispose();
        }
        else
        {
            _widgetManager.CloseAll();
        }

        base.OnClosed(e);
    }

    private static System.Drawing.Icon? LoadTrayIcon()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            var extracted = System.Drawing.Icon.ExtractAssociatedIcon(processPath);
            if (extracted != null)
            {
                return (System.Drawing.Icon)extracted.Clone();
            }
        }

        return null;
    }
}
