using System.Collections.ObjectModel;

using HTWind.Model;
using HTWind.Services.Application;

namespace HTWind.Services.Widget;

public interface IWidgetManager
{
    ObservableCollection<WidgetModel> Widgets { get; }
    bool HasPersistedState { get; }
    bool IsFullscreenSuppressionEnabled { get; set; }
    bool IsMaximizedSuppressionEnabled { get; set; }
    ThemeOption? PreferredTheme { get; }
    string? PreferredCulture { get; }
    void LoadPersistedWidgets();
    void AddWidget(string filePath);
    void AddWidget(string filePath, bool isVisible);
    void AddWidgets(IEnumerable<string> filePaths, bool isVisible);
    void CreateWidgetWithEditor(string fileName, bool isVisible, bool enableHotReload);
    void OpenEditor(WidgetModel model);
    void OpenWidgetSettings(WidgetModel model);
    string ExportWidget(WidgetModel model, string destinationDirectory);
    string ExportWidgets(IEnumerable<WidgetModel> models, string destinationDirectory, string exportName);
    void ApplyVisibility(WidgetModel model);
    void ApplyPinState(WidgetModel model);
    void ResetWidgetPosition(WidgetModel model);
    void ResetAllWidgetsToDefaultState();
    void RemoveWidget(WidgetModel model);
    void SetPreferredTheme(ThemeOption option);
    void SetPreferredCulture(string cultureName);
    IReadOnlyList<string> GetWorkspaceProfileNames();
    void CaptureWorkspaceProfile(string profileName);
    bool ApplyWorkspaceProfile(string profileName);
    bool DeleteWorkspaceProfile(string profileName);
    void CloseAll();
}
