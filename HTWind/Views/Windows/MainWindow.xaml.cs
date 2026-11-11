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
        RebuildProfilesTrayMenu();
    }

    private void RebuildProfilesTrayMenu()
    {
        var profilesMenu = ProfilesTrayMenuItem;
        if (profilesMenu is null)
        {
            return;
        }

        var menuItems = new List<object>();

        foreach (var profileName in _widgetManager.GetWorkspaceProfileNames())
        {
            var profileItem = new System.Windows.Controls.MenuItem
            {
                Header = profileName,
                Tag = profileName
            };
            profileItem.Click += WorkspaceProfileMenuItem_Click;
            menuItems.Add(profileItem);
        }

        var saveItem = new System.Windows.Controls.MenuItem
        {
            Header = Localization.LocalizationService.Get("MainWindow_SaveProfile")
        };
        saveItem.Click += SaveWorkspaceProfileMenuItem_Click;
        menuItems.Add(saveItem);

        if (_widgetManager.GetWorkspaceProfileNames().Count > 0)
        {
            var deleteItem = new System.Windows.Controls.MenuItem
            {
                Header = Localization.LocalizationService.Get("MainWindow_DeleteProfile")
            };
            deleteItem.Click += DeleteWorkspaceProfileMenuItem_Click;
            menuItems.Add(deleteItem);
        }

        profilesMenu.Items.Clear();
        foreach (var item in menuItems)
        {
            profilesMenu.Items.Add(item);
        }
    }

    private void WorkspaceProfileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { Tag: string profileName })
        {
            return;
        }

        if (_widgetManager.ApplyWorkspaceProfile(profileName))
        {
            RebuildProfilesTrayMenu();
        }
    }

    private void SaveWorkspaceProfileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var inputWindow = new ProfileNameInputWindow
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        if (inputWindow.ShowDialog() != true)
        {
            return;
        }

        var profileName = inputWindow.ProfileName;
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        _widgetManager.CaptureWorkspaceProfile(profileName);
        RebuildProfilesTrayMenu();
    }

    private void DeleteWorkspaceProfileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var profiles = _widgetManager.GetWorkspaceProfileNames();
        if (profiles.Count == 0)
        {
            return;
        }

        var selector = new ProfileNameInputWindow
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsDeletionMode = true,
            ExistingProfileNames = profiles
        };

        if (selector.ShowDialog() != true || string.IsNullOrWhiteSpace(selector.ProfileName))
        {
            return;
        }

        _widgetManager.DeleteWorkspaceProfile(selector.ProfileName);
        RebuildProfilesTrayMenu();
    }

    private void HomeNavItem_Click(object sender, RoutedEventArgs e)
    {
        if (!_isNavigationReady)
        {
            return;
        }

        NavigateUsingItem(HomeNavItem, "Home");
    }

    private void GalleryNavItem_Click(object sender, RoutedEventArgs e)
    {
        if (!_isNavigationReady)
        {
            return;
        }

        NavigateUsingItem(GalleryNavItem, "Gallery");
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

        if (!ReferenceEquals(activeItem, GalleryNavItem))
        {
            GalleryNavItem.Deactivate(NavigationView);
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
                "Gallery" => new GalleryPage(_widgetManager),
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
        _widgetManager.SetPreferredTheme(option);
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
