using System.IO;

using HTWind.Localization;

using Win32OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Win32OpenFolderDialog = Microsoft.Win32.OpenFolderDialog;

namespace HTWind.Services;

public class FileDialogService : IFileDialogService
{
    public bool TryPickHtmlFiles(out IReadOnlyList<string> filePaths)
    {
        var dialog = new Win32OpenFileDialog
        {
            Filter = LocalizationService.Get("MainWindow_FileDialogFilter"),
            Title = LocalizationService.Get("MainWindow_FileDialogTitle"),
            Multiselect = true
        };

        var result = dialog.ShowDialog() == true;
        filePaths = result
            ? dialog.FileNames.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
        return result && filePaths.Count > 0;
    }

    public bool TryPickHtmlFile(out string filePath)
    {
        var result = TryPickHtmlFiles(out var filePaths);
        filePath = result ? filePaths[0] : string.Empty;
        return result;
    }

    public bool TryPickDirectory(out string directoryPath)
    {
        var dialog = new Win32OpenFolderDialog
        {
            Title = LocalizationService.Get("MainWindow_WidgetExportDirectoryDialogTitle"),
            Multiselect = false
        };

        var result = dialog.ShowDialog() == true;
        directoryPath = result && !string.IsNullOrWhiteSpace(dialog.FolderName) && Directory.Exists(dialog.FolderName)
            ? dialog.FolderName
            : string.Empty;
        return !string.IsNullOrWhiteSpace(directoryPath);
    }
}
