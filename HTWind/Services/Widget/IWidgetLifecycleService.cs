using System.Collections.ObjectModel;
using System.ComponentModel;

using HTWind.Model;

namespace HTWind.Services.Widget;

public interface IWidgetLifecycleService
{
    ObservableCollection<WidgetModel> Widgets { get; }
    bool HasPersistedState { get; }

    void LoadPersistedWidgets(Func<WidgetModel, bool, bool> shouldSuppressWidget, Action<WidgetModel, bool> enqueueWindowCreation);
    WidgetModel AddWidget(string filePath, bool isVisible, Action<WidgetModel, bool> enqueueWindowCreation);
    WidgetModel CreateWidgetWithEditor(string fileName, bool isVisible, Action<WidgetModel, bool> enqueueWindowCreation);
    void RemoveWidget(WidgetModel model, Action<string> closeEditor, Action<string> untrackWindow);

    void RegisterModelChangeHandler(WidgetModel model, Action<WidgetModel, string> onPropertyChanged);
    void UnregisterModelChangeHandler(WidgetModel model);
    void UnregisterAllModelChangeHandlers();

    void CloseAll();
}
