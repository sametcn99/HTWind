using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

using HTWind.Localization;
using HTWind.RuntimeTests.Support;
using HTWind.Services.Widget;

using UiButton = Wpf.Ui.Controls.Button;

using Xunit;

namespace HTWind.RuntimeTests;

[Collection(WebView2Collection.CollectionName)]
public sealed class DialogInteractionTests
{
    [Fact]
    public async Task ProfileNameInput_CreateTrimsNameAndReturnsConfirmation()
    {
        using var sta = new StaRunner();
        await sta.RunAsync(async () =>
        {
            var window = CreateOffscreen(new ProfileNameInputWindow());
            ShowDialogAndDrive(window, () =>
            {
                window.ProfileNameTextBox.Text = "  Work layout  ";
                ClickButton(window, "ProfileWindow_ConfirmSave");
            });

            Assert.True(window.DialogResult);
            Assert.Equal("Work layout", window.ProfileName);
            return true;
        });
    }

    [Fact]
    public async Task ProfileNameInput_BlankConfirmationKeepsDialogOpenAndCancelReturnsNoName()
    {
        using var sta = new StaRunner();
        await sta.RunAsync(async () =>
        {
            var window = CreateOffscreen(new ProfileNameInputWindow());
            var remainedOpenAfterBlank = false;
            ShowDialogAndDrive(window, () =>
            {
                window.ProfileNameTextBox.Text = "  \t ";
                ClickButton(window, "ProfileWindow_ConfirmSave");
                remainedOpenAfterBlank = window.IsVisible;
                ClickButton(window, "WidgetSettings_Cancel");
            });

            Assert.True(remainedOpenAfterBlank);
            Assert.False(window.DialogResult);
            Assert.Null(window.ProfileName);
            return true;
        });
    }

    [Fact]
    public async Task ProfileNameInput_CancelReturnsFalse()
    {
        using var sta = new StaRunner();
        await sta.RunAsync(async () =>
        {
            var window = CreateOffscreen(new ProfileNameInputWindow());
            ShowDialogAndDrive(window, () => ClickButton(window, "WidgetSettings_Cancel"));

            Assert.False(window.DialogResult);
            Assert.Null(window.ProfileName);
            return true;
        });
    }

    [Fact]
    public async Task ProfileNameInput_DeletionSelectsFirstThenTracksChangedSelection()
    {
        using var sta = new StaRunner();
        await sta.RunAsync(async () =>
        {
            var window = CreateOffscreen(new ProfileNameInputWindow
            {
                IsDeletionMode = true,
                ExistingProfileNames = ["Work", "Gaming", "Streaming"]
            });
            string? initialSelection = null;
            ShowDialogAndDrive(window, () =>
            {
                initialSelection = window.ProfileName;
                window.ExistingProfileComboBox.SelectedIndex = 2;
                ClickButton(window, "ProfileWindow_ConfirmDelete");
            });

            Assert.Equal("Work", initialSelection);
            Assert.True(window.DialogResult);
            Assert.Equal("Streaming", window.ProfileName);
            Assert.Equal(Visibility.Collapsed, window.ProfileNameTextBox.Visibility);
            Assert.Equal(Visibility.Visible, window.ExistingProfileComboBox.Visibility);
        });
    }

    [Fact]
    public async Task ProfileNameInput_DeletionWithoutNamesFallsBackToCreateInput()
    {
        using var sta = new StaRunner();
        await sta.RunAsync(async () =>
        {
            var window = CreateOffscreen(new ProfileNameInputWindow
            {
                IsDeletionMode = true,
                ExistingProfileNames = []
            });
            ShowDialogAndDrive(window, () =>
            {
                window.ProfileNameTextBox.Text = "Fallback";
                ClickButton(window, "ProfileWindow_ConfirmSave");
            });

            Assert.True(window.DialogResult);
            Assert.Equal("Fallback", window.ProfileName);
            Assert.Equal(Visibility.Visible, window.ProfileNameTextBox.Visibility);
            Assert.Equal(Visibility.Collapsed, window.ExistingProfileComboBox.Visibility);
        });
    }

    [Theory]
    [InlineData("PowerShellPermissionDialog_ReadOnly", PowerShellExecutionDecision.ReadOnly)]
    [InlineData("PowerShellPermissionDialog_Full", PowerShellExecutionDecision.Full)]
    [InlineData("PowerShellPermissionDialog_Deny", PowerShellExecutionDecision.Deny)]
    public async Task PowerShellPermission_ButtonsReturnTheirDecision(string buttonKey, PowerShellExecutionDecision expected)
    {
        using var sta = new StaRunner();
        await sta.RunAsync(async () =>
        {
            var window = CreateOffscreen(new PowerShellPermissionWindow("Weather Widget", suggestFullAccess: true));
            ShowDialogAndDrive(window, () => ClickButton(window, buttonKey));

            Assert.True(window.DialogResult);
            Assert.Equal(expected, window.Decision);
            return true;
        });
    }

    [Fact]
    public async Task PowerShellPermission_TitleBarCloseLeavesDecisionUnset()
    {
        using var sta = new StaRunner();
        await sta.RunAsync(async () =>
        {
            var window = CreateOffscreen(new PowerShellPermissionWindow("Weather Widget", suggestFullAccess: false));
            ShowDialogAndDrive(window, window.Close);

            Assert.False(window.DialogResult);
            Assert.Null(window.Decision);
            return true;
        });
    }

    [Fact]
    public async Task WidgetPermission_AllowSetsAllowedAndTrueDialogResult()
    {
        using var sta = new StaRunner();
        await sta.RunAsync(async () =>
        {
            var window = CreateOffscreen(new WidgetPermissionDecisionWindow("Clipboard read", "Notes Widget"));
            ShowDialogAndDrive(window, () => ClickButton(window, "WidgetPermissionDialog_Allow"));

            Assert.True(window.DialogResult);
            Assert.True(window.IsAllowed);
            return true;
        });
    }

    [Fact]
    public async Task WidgetPermission_DenySetsFalseDialogResult()
    {
        using var sta = new StaRunner();
        await sta.RunAsync(async () =>
        {
            var window = CreateOffscreen(new WidgetPermissionDecisionWindow("Clipboard read", "Notes Widget"));
            ShowDialogAndDrive(window, () => ClickButton(window, "WidgetPermissionDialog_Deny"));

            Assert.False(window.DialogResult);
            Assert.False(window.IsAllowed);
            return true;
        });
    }

    [Fact]
    public async Task WidgetPermission_TitleBarCloseLeavesDefaultDenial()
    {
        using var sta = new StaRunner();
        await sta.RunAsync(async () =>
        {
            var window = CreateOffscreen(new WidgetPermissionDecisionWindow("Clipboard read", "Notes Widget"));
            ShowDialogAndDrive(window, window.Close);

            Assert.False(window.DialogResult);
            Assert.False(window.IsAllowed);
            return true;
        });
    }

    [Fact]
    public async Task CreateWidgetWithEditor_CreateTrimsNameAndReturnsBothToggleValues()
    {
        using var sta = new StaRunner();
        await sta.RunAsync(async () =>
        {
            var window = CreateOffscreen(new CreateWidgetWithEditorWindow());
            ShowDialogAndDrive(window, () =>
            {
                window.FileNameTextBox.Text = "  daily dashboard  ";
                window.VisibleByDefaultToggle.IsChecked = false;
                window.EnableHotReloadToggle.IsChecked = true;
                ClickButton(window, "CreateWithEditorWindow_Create");
            });

            Assert.True(window.DialogResult);
            Assert.Equal("daily dashboard", window.RequestedFileName);
            Assert.False(window.IsVisibleByDefault);
            Assert.True(window.EnableHotReload);
            return true;
        });
    }

    [Fact]
    public async Task CreateWidgetWithEditor_CancelReturnsFalseAndDoesNotAcceptName()
    {
        using var sta = new StaRunner();
        await sta.RunAsync(async () =>
        {
            var window = CreateOffscreen(new CreateWidgetWithEditorWindow());
            ShowDialogAndDrive(window, () =>
            {
                window.FileNameTextBox.Text = "draft name";
                ClickButton(window, "CreateWithEditorWindow_Cancel");
            });

            Assert.False(window.DialogResult);
            Assert.Equal(string.Empty, window.RequestedFileName);
            Assert.True(window.IsVisibleByDefault);
            Assert.True(window.EnableHotReload);
            return true;
        });
    }

    private static T CreateOffscreen<T>(T window) where T : Window
    {
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -32000;
        window.Top = -32000;
        return window;
    }

    private static void ShowDialogAndDrive(Window window, Action drive)
    {
        Exception? actionException = null;
        var timeout = new DispatcherTimer(
            TimeSpan.FromSeconds(15),
            DispatcherPriority.Background,
            (_, _) =>
            {
                if (!window.IsVisible)
                {
                    return;
                }

                actionException = new TimeoutException("The modal dialog interaction did not complete within 15 seconds.");
                window.Close();
            },
            window.Dispatcher);
        window.Loaded += (_, _) =>
        {
            _ = window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    drive();
                    if (window.IsVisible)
                    {
                        throw new InvalidOperationException("The interaction finished without closing its modal dialog.");
                    }

                }
                catch (Exception exception)
                {
                    actionException = exception;
                    if (window.IsVisible)
                    {
                        window.Close();
                    }
                }
            }));
        };

        try
        {
            timeout.Start();
            window.ShowDialog();
        }
        finally
        {
            timeout.Stop();
            if (window.IsVisible)
            {
                window.Close();
            }
        }

        if (actionException is not null)
        {
            ExceptionDispatchInfo.Capture(actionException).Throw();
        }
    }

    private static void ClickButton(DependencyObject root, string localizationKey)
    {
        var label = LocalizationService.Get(localizationKey);
        var button = FindVisualChildren<UiButton>(root)
            .Single(candidate => string.Equals(candidate.Content?.ToString(), label, StringComparison.Ordinal));
        button.RaiseEvent(new RoutedEventArgs(UiButton.ClickEvent));
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T matched)
            {
                yield return matched;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
