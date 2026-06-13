using HTWind.Model;

namespace HTWind.Services.Widget;

public interface IWidgetEditorManager
{
    bool IsEditorOpen(string widgetId);
    void OpenEditor(WidgetModel model, bool enableHotReload, Func<WidgetModel, bool, HtmlCodeEditorWindow> editorFactory);
    void CloseEditor(string widgetId);
    void CloseAll();
}
