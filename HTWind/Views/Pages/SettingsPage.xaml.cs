using System.Windows;
using System.Windows.Controls;

using HTWind.ViewModels;

namespace HTWind.Views.Pages;

public partial class SettingsPage : UserControl
{
    private readonly MainWindowViewModel _viewModel;

    public SettingsPage(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;

        InitializeComponent();
    }

    private void RunOnStartupToggle_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetRunOnStartup(true);
    }

    private void RunOnStartupToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetRunOnStartup(false);
    }

    private void DeveloperModeToggle_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetDeveloperMode(true);
    }

    private void DeveloperModeToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetDeveloperMode(false);
    }

    private void FullscreenSuppressionToggle_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetFullscreenSuppression(true);
    }

    private void FullscreenSuppressionToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetFullscreenSuppression(false);
    }

    private void MaximizedSuppressionToggle_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetMaximizedSuppression(true);
    }

    private void MaximizedSuppressionToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetMaximizedSuppression(false);
    }
}
