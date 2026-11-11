namespace HTWind.Services.Application;

/// <summary>
///     Modal message-box abstraction so view models and services can surface dialogs without
///     unit tests opening real modal windows.
/// </summary>
public interface IDialogService
{
    void ShowInformation(string message, string title);

    void ShowWarning(string message, string title);
}
