using System.Collections.ObjectModel;

namespace HTWind.Services;

public interface IWidgetManager
{
    ObservableCollection<WidgetModel> Widgets { get; }
    bool HasPersistedState { get; }
    bool IsFullscreenSuppressionEnabled { get; set; }
    bool IsMaximizedSuppressionEnabled { get; set; }
    void LoadPersistedWidgets();
    void AddWidget(string filePath);
    void AddWidget(string filePath, bool isVisible);
    void CreateWidgetWithEditor(string fileName, bool isVisible, bool enableHotReload);
    void OpenEditor(WidgetModel model);
    void ApplyVisibility(WidgetModel model);
    void ApplyPinState(WidgetModel model);
    void ResetWidgetPosition(WidgetModel model);
    void ResetAllWidgetsToDefaultState();
    void RemoveWidget(WidgetModel model);
    void CloseAll();
}
