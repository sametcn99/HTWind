using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;

using HTWind.Services;
using HTWind.ViewModels;

using Wpf.Ui.Controls;

namespace HTWind;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IWidgetManager _widgetManager;

    private bool _isExiting;
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

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void RunOnStartupToggle_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetRunOnStartup(true);
    }

    private void RunOnStartupToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetRunOnStartup(false);
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

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        aboutWindow.ShowDialog();
    }

    private void AddWidgetOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement target || target.ContextMenu is not ContextMenu menu)
        {
            return;
        }

        menu.PlacementTarget = target;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void CreateWithEditorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var createWindow = new CreateWidgetWithEditorWindow
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        if (createWindow.ShowDialog() != true)
        {
            return;
        }

        _widgetManager.CreateWidgetWithEditor(
            createWindow.RequestedFileName,
            createWindow.IsVisibleByDefault,
            createWindow.EnableHotReload
        );
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
        _viewModel.ThemeRequested -= ViewModel_ThemeRequested;
        _widgetManager.CloseAll();
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
