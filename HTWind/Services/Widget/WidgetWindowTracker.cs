using System.IO;
using System.Windows.Threading;

using HTWind.Model;

namespace HTWind.Services.Widget;

public sealed class WidgetWindowTracker : IWidgetWindowTracker
{
    private const int VisibleWindowQueueFastDelayMilliseconds = 20;
    private const int VisibleWindowQueueSteadyDelayMilliseconds = 40;
    private const int GeometryCaptureDebounceMilliseconds = 180;

    private readonly Dictionary<string, WidgetWindow> _windowsById = new();
    private readonly Dictionary<string, DispatcherTimer> _geometryCaptureTimersByWidgetId = new(StringComparer.Ordinal);
    private readonly Queue<(WidgetModel Model, bool HasPersistedGeometry, Func<WidgetModel, bool, WidgetWindow> WindowFactory)> _visibleWindowCreateQueue = new();
    private readonly HashSet<string> _queuedVisibleWindowIds = new(StringComparer.Ordinal);
    private bool _isProcessingVisibleWindowQueue;

    public WidgetWindow? GetWindow(string widgetId)
    {
        return _windowsById.TryGetValue(widgetId, out var window) ? window : null;
    }

    public bool HasWindow(string widgetId)
    {
        return _windowsById.ContainsKey(widgetId);
    }

    public IReadOnlyDictionary<string, WidgetWindow> GetAllWindows()
    {
        return _windowsById;
    }

    public void TrackWindow(WidgetModel model, WidgetWindow window)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(window);
        _windowsById[model.Id] = window;
    }

    public void UntrackWindow(string widgetId)
    {
        _windowsById.Remove(widgetId);
        _queuedVisibleWindowIds.Remove(widgetId);
        RemoveGeometryCaptureTimer(widgetId);
    }

    public async Task EnqueueWindowCreationAsync(
        WidgetModel model,
        bool hasPersistedGeometry,
        Func<WidgetModel, bool, WidgetWindow> windowFactory)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(windowFactory);

        if (string.IsNullOrWhiteSpace(model.Id))
        {
            return;
        }

        if (_windowsById.ContainsKey(model.Id))
        {
            await _windowsById[model.Id].ShowAndInitializeAsync();
            return;
        }

        if (!_queuedVisibleWindowIds.Add(model.Id))
        {
            return;
        }

        _visibleWindowCreateQueue.Enqueue((model, hasPersistedGeometry, windowFactory));
        await ProcessVisibleWindowQueueAsync();
    }

    public void CancelQueuedCreation(string widgetId)
    {
        _queuedVisibleWindowIds.Remove(widgetId);
    }

    public void ScheduleGeometryCapture(
        string widgetId,
        WidgetWindow window,
        WidgetModel model,
        Action captureCallback)
    {
        if (!_geometryCaptureTimersByWidgetId.TryGetValue(widgetId, out var timer))
        {
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(GeometryCaptureDebounceMilliseconds)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                captureCallback();
            };
            _geometryCaptureTimersByWidgetId[widgetId] = timer;
        }

        timer.Stop();
        timer.Start();
    }

    public void RemoveGeometryCaptureTimer(string widgetId)
    {
        if (!_geometryCaptureTimersByWidgetId.TryGetValue(widgetId, out var timer))
        {
            return;
        }

        timer.Stop();
        _geometryCaptureTimersByWidgetId.Remove(widgetId);
    }

    public void ClearAll()
    {
        foreach (var timer in _geometryCaptureTimersByWidgetId.Values)
        {
            timer.Stop();
        }

        _geometryCaptureTimersByWidgetId.Clear();
        _windowsById.Clear();
        _visibleWindowCreateQueue.Clear();
        _queuedVisibleWindowIds.Clear();
        _isProcessingVisibleWindowQueue = false;
    }

    private async Task ProcessVisibleWindowQueueAsync()
    {
        if (_isProcessingVisibleWindowQueue)
        {
            return;
        }

        _isProcessingVisibleWindowQueue = true;
        var processedWindows = 0;

        try
        {
            while (_visibleWindowCreateQueue.Count > 0)
            {
                var (model, hasPersistedGeometry, windowFactory) = _visibleWindowCreateQueue.Dequeue();
                _queuedVisibleWindowIds.Remove(model.Id);

                if (!model.IsVisible)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(model.FilePath) || !File.Exists(model.FilePath))
                {
                    continue;
                }

                if (!_windowsById.ContainsKey(model.Id))
                {
                    var window = windowFactory(model, hasPersistedGeometry);
                    _windowsById[model.Id] = window;

                    try
                    {
                        await window.ShowAndInitializeAsync();
                    }
                    catch
                    {
                    }

                    processedWindows++;

                    if (_visibleWindowCreateQueue.Count == 0)
                    {
                        continue;
                    }

                    var delayMilliseconds = processedWindows <= 2
                        ? VisibleWindowQueueFastDelayMilliseconds
                        : VisibleWindowQueueSteadyDelayMilliseconds;
                    await Task.Delay(delayMilliseconds);
                }
            }
        }
        finally
        {
            _isProcessingVisibleWindowQueue = false;
        }
    }
}
