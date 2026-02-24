using System.IO;
using System.Windows;

using HTWind.Localization;

using Wpf.Ui.Controls;

using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace HTWind;

public partial class CreateWidgetWithEditorWindow : FluentWindow
{
    public CreateWidgetWithEditorWindow()
    {
        InitializeComponent();
        var defaultFileName = LocalizationService.Get("CreateWithEditorWindow_DefaultFileName");
        FileNameTextBox.Text = Path.GetFileNameWithoutExtension(defaultFileName);
        FileNameTextBox.SelectAll();
    }

    public string RequestedFileName { get; private set; } = string.Empty;

    public bool IsVisibleByDefault { get; private set; } = true;

    public bool EnableHotReload { get; private set; } = true;

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        var fileName = FileNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            MessageBox.Show(
                LocalizationService.Get("CreateWithEditorWindow_FileNameRequired"),
                LocalizationService.Get("CreateWithEditorWindow_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        RequestedFileName = fileName;
        IsVisibleByDefault = VisibleByDefaultToggle.IsChecked == true;
        EnableHotReload = EnableHotReloadToggle.IsChecked == true;

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
