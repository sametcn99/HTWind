namespace HTWind.Services;

public interface IWidgetStateRepository
{
    IReadOnlyList<WidgetStateRecord> Load();

    void Save(IEnumerable<WidgetStateRecord> states);

    string CopyWidgetToManagedStorage(string sourcePath);

    string CreateManagedWidgetFile(string requestedFileName, string content);

    bool IsManagedWidgetPath(string? filePath);

    void DeleteManagedWidgetFile(string? filePath);
}
