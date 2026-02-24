using System.ComponentModel;
using System.Windows;

using Wpf.Ui.Controls;

namespace HTWind;

public partial class RiskConsentWindow : FluentWindow
{
    public RiskConsentWindow()
    {
        InitializeComponent();
    }

    public bool IsAccepted { get; private set; }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        IsAccepted = true;
        DialogResult = true;
        Close();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!IsAccepted)
        {
            e.Cancel = true;
        }
    }
}
