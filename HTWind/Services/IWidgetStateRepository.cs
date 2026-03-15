namespace HTWind.Services;

public interface IWidgetStateRepository
{
    bool HasStateFile();

    WidgetStateSnapshot Load();

    void Save(WidgetStateSnapshot snapshot);

    IReadOnlyList<WidgetManagedReference> ImportWidgetSource(string sourcePath);

    WidgetManagedReference CreateManagedWidget(string requestedFileName, string content);

    string ExportWidgetToDirectory(string widgetFilePath, string destinationDirectory);

    string ExportWidgetsToDirectory(IEnumerable<string> widgetFilePaths, string destinationDirectory, string exportName);

    string CopyWidgetToManagedStorage(string sourcePath);

    string CreateManagedWidgetFile(string requestedFileName, string content);

    bool IsManagedWidgetPath(string? filePath);

    void DeleteManagedWidgetFile(string? filePath);
}
