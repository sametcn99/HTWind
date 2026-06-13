using HTWind.Model;

namespace HTWind.Services.Widget;

public sealed class WidgetEditorManager : IWidgetEditorManager
{
    private readonly Dictionary<string, HtmlCodeEditorWindow> _editorWindowsById = new();

    public bool IsEditorOpen(string widgetId)
    {
        return _editorWindowsById.ContainsKey(widgetId);
    }

    public void OpenEditor(
        WidgetModel model,
        bool enableHotReload,
        Func<WidgetModel, bool, HtmlCodeEditorWindow> editorFactory)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(editorFactory);

        if (_editorWindowsById.TryGetValue(model.Id, out var existingEditor))
        {
            existingEditor.Activate();
            return;
        }

        var editor = editorFactory(model, enableHotReload);
        editor.Closed += (_, _) => _editorWindowsById.Remove(model.Id);

        _editorWindowsById[model.Id] = editor;
        editor.Show();
    }

    public void CloseEditor(string widgetId)
    {
        if (_editorWindowsById.TryGetValue(widgetId, out var editor))
        {
            editor.Close();
            _editorWindowsById.Remove(widgetId);
        }
    }

    public void CloseAll()
    {
        foreach (var editor in _editorWindowsById.Values.ToList())
        {
            editor.Close();
        }

        _editorWindowsById.Clear();
    }
}
