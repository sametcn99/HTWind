namespace HTWind.Services;

public interface IFileDialogService
{
    bool TryPickHtmlFile(out string filePath);
}
