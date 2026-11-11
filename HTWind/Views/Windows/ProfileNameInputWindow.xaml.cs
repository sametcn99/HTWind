using System.Windows;
using System.Windows.Controls;

using HTWind.Localization;

using Wpf.Ui.Controls;

namespace HTWind;

/// <summary>
///     Small dialog used by the tray menu to name a new workspace layout profile, or to pick an
///     existing profile for deletion.
/// </summary>
public partial class ProfileNameInputWindow : FluentWindow
{
    public ProfileNameInputWindow()
    {
        InitializeComponent();
        Loaded += ProfileNameInputWindow_Loaded;
    }

    public bool IsDeletionMode { get; set; }

    public IReadOnlyList<string>? ExistingProfileNames { get; set; }

    public string? ProfileName { get; private set; }

    private void ProfileNameInputWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= ProfileNameInputWindow_Loaded;

        if (IsDeletionMode && ExistingProfileNames is { Count: > 0 })
        {
            PromptText.Text = LocalizationService.Get("ProfileWindow_DeletePrompt");
            ConfirmButton.Content = LocalizationService.Get("ProfileWindow_ConfirmDelete");
            ProfileNameTextBox.Visibility = Visibility.Collapsed;
            ExistingProfileComboBox.Visibility = Visibility.Visible;

            foreach (var profileName in ExistingProfileNames)
            {
                ExistingProfileComboBox.Items.Add(profileName);
            }

            ExistingProfileComboBox.SelectedIndex = 0;
            ProfileName = ExistingProfileNames[0];
        }
        else
        {
            ExistingProfileComboBox.Visibility = Visibility.Collapsed;
            ProfileNameTextBox.Focus();
        }
    }

    private void ExistingProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ProfileName = ExistingProfileComboBox.SelectedItem as string;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (IsDeletionMode)
        {
            DialogResult = true;
            Close();
            return;
        }

        var profileName = ProfileNameTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        ProfileName = profileName;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
