using System.Windows;

namespace HTWind.Services.Application;

/// <summary>Production dialog service backed by WPF MessageBox.</summary>
public sealed class MessageBoxDialogService : IDialogService
{
    public static MessageBoxDialogService Instance { get; } = new();

    public void ShowInformation(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowWarning(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
