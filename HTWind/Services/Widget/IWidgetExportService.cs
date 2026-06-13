namespace HTWind.Services.Widget;

public interface IWidgetExportService
{
    string ExportWidgetToDirectory(string widgetFilePath, string destinationDirectory);
    string ExportWidgetsToDirectory(IEnumerable<string> widgetFilePaths, string destinationDirectory, string exportName);
}
