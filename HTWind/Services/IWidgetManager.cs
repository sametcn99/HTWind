using System.Collections.ObjectModel;

namespace HTWind.Services;

public interface IWidgetManager
{
    ObservableCollection<WidgetModel> Widgets { get; }
    void LoadPersistedWidgets();
    void AddWidget(string filePath);
    void AddWidget(string filePath, bool isVisible);
    void CreateWidgetWithEditor(string fileName, bool isVisible, bool enableHotReload);
    void OpenEditor(WidgetModel model);
    void ApplyVisibility(WidgetModel model);
    void ApplyPinState(WidgetModel model);
    void RemoveWidget(WidgetModel model);
    void CloseAll();
}
