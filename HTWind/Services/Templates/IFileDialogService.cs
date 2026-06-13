namespace HTWind.Services.Templates;

public interface IFileDialogService
{
    bool TryPickHtmlFiles(out IReadOnlyList<string> filePaths);
    bool TryPickHtmlFile(out string filePath);
    bool TryPickDirectory(out string directoryPath);
}
