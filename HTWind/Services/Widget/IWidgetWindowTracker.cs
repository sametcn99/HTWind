using System.Windows.Threading;

using HTWind.Model;

namespace HTWind.Services.Widget;

public interface IWidgetWindowTracker
{
    WidgetWindow? GetWindow(string widgetId);
    bool HasWindow(string widgetId);
    IReadOnlyDictionary<string, WidgetWindow> GetAllWindows();

    void TrackWindow(WidgetModel model, WidgetWindow window);
    void UntrackWindow(string widgetId);

    Task EnqueueWindowCreationAsync(WidgetModel model, bool hasPersistedGeometry, Func<WidgetModel, bool, WidgetWindow> windowFactory);
    void CancelQueuedCreation(string widgetId);

    void ScheduleGeometryCapture(string widgetId, WidgetWindow window, WidgetModel model, Action captureCallback);
    void RemoveGeometryCaptureTimer(string widgetId);

    void ClearAll();
}
