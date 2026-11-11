using System.Windows;

using HTWind.Services.Widget;

using Wpf.Ui.Controls;

namespace HTWind;

public partial class PowerShellPermissionWindow : FluentWindow
{
    public PowerShellPermissionWindow(string widgetName, bool suggestFullAccess)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetName);

        InitializeComponent();
        WidgetValueText.Text = widgetName;
        _ = suggestFullAccess;
    }

    public PowerShellExecutionDecision? Decision { get; private set; }

    private void ReadOnly_Click(object sender, RoutedEventArgs e)
    {
        Decision = PowerShellExecutionDecision.ReadOnly;
        DialogResult = true;
        Close();
    }

    private void Full_Click(object sender, RoutedEventArgs e)
    {
        Decision = PowerShellExecutionDecision.Full;
        DialogResult = true;
        Close();
    }

    private void Deny_Click(object sender, RoutedEventArgs e)
    {
        Decision = PowerShellExecutionDecision.Deny;
        DialogResult = true;
        Close();
    }
}
