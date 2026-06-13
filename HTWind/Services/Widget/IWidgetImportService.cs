using HTWind.Model;

namespace HTWind.Services.Widget;

public interface IWidgetImportService
{
    IReadOnlyList<WidgetManagedReference> ImportWidgetSource(string sourcePath);
    WidgetManagedReference CreateManagedWidget(string requestedFileName, string content);
    bool IsManagedWidgetPath(string? filePath);
    void DeleteManagedWidgetFile(string? filePath);
    string WidgetsDataDirectory { get; }
}
