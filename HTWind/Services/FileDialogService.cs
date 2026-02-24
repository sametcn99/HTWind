using HTWind.Localization;

using Microsoft.Win32;

namespace HTWind.Services;

public class FileDialogService : IFileDialogService
{
    public bool TryPickHtmlFile(out string filePath)
    {
        var dialog = new OpenFileDialog
        {
            Filter = LocalizationService.Get("MainWindow_FileDialogFilter"),
            Title = LocalizationService.Get("MainWindow_FileDialogTitle")
        };

        var result = dialog.ShowDialog() == true;
        filePath = result ? dialog.FileName : string.Empty;
        return result;
    }
}
