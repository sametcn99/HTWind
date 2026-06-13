using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

using HTWind.Model;

namespace HTWind.Services.Widget;

public sealed class WidgetLifecycleService : IWidgetLifecycleService
{
    private const string DefaultNewWidgetTemplate =
        "<!doctype html>\n"
        + "<html lang=\"en\">\n"
        + "<head>\n"
        + "  <meta charset=\"utf-8\" />\n"
        + "  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\" />\n"
        + "  <title>New Widget</title>\n"
        + "  <style>\n"
        + "    :root { color-scheme: light dark; }\n"
        + "    html, body { height: 100%; margin: 0; }\n"
        + "    body {\n"
        + "      display: grid;\n"
        + "      place-items: center;\n"
        + "      font-family: 'Segoe UI', system-ui, sans-serif;\n"
        + "      background: linear-gradient(135deg, #111827, #1f2937);\n"
        + "      color: #f9fafb;\n"
        + "    }\n"
        + "    .card {\n"
        + "      width: min(420px, calc(100% - 24px));\n"
        + "      border: 1px solid rgba(255,255,255,0.18);\n"
        + "      border-radius: 12px;\n"
        + "      padding: 16px;\n"
        + "      background: rgba(17,24,39,0.65);\n"
        + "      backdrop-filter: blur(4px);\n"
        + "    }\n"
        + "    h1 { margin: 0 0 8px; font-size: 20px; }\n"
        + "    p { margin: 0; opacity: 0.9; line-height: 1.4; }\n"
        + "  </style>\n"
        + "</head>\n"
        + "<body>\n"
        + "  <section class=\"card\">\n"
        + "    <h1>New Widget</h1>\n"
        + "    <p>Edit this template in HTWind to build your custom widget.</p>\n"
        + "  </section>\n"
        + "</body>\n"
        + "</html>\n";

    private readonly IWidgetStateStore _stateStore;
    private readonly IWidgetImportService _importService;
    private readonly Dictionary<string, PropertyChangedEventHandler> _modelPropertyChangedHandlers = new();
    private bool _isRestoring;

    public WidgetLifecycleService(
        IWidgetStateStore stateStore,
        IWidgetImportService importService)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
    }

    public ObservableCollection<WidgetModel> Widgets { get; } = new();
    public bool HasPersistedState { get; private set; }

    public void LoadPersistedWidgets(
        Func<WidgetModel, bool, bool> shouldSuppressWidget,
        Action<WidgetModel, bool> enqueueWindowCreation)
    {
        ArgumentNullException.ThrowIfNull(shouldSuppressWidget);
        ArgumentNullException.ThrowIfNull(enqueueWindowCreation);

        HasPersistedState = _stateStore.HasStateFile();

        var snapshot = _stateStore.Load();
        var states = snapshot.Widgets;
        if (states.Count == 0)
        {
            return;
        }

        _isRestoring = true;
        try
        {
            foreach (var state in states)
            {
                if (string.IsNullOrWhiteSpace(state.FilePath) || !File.Exists(state.FilePath))
                {
                    continue;
                }

                var model = new WidgetModel
                {
                    Id = string.IsNullOrWhiteSpace(state.Id)
                        ? Guid.NewGuid().ToString()
                        : state.Id,
                    Name = state.Name,
                    FilePath = state.FilePath,
                    IsVisible = state.IsVisible,
                    IsLocked = state.IsLocked,
                    IsPinned = state.IsPinned,
                    Left = state.Left,
                    Top = state.Top,
                    WidgetWidth = state.WidgetWidth,
                    WidgetHeight = state.WidgetHeight,
                    MonitorDeviceName = state.MonitorDeviceName,
                    PreferredMonitorDeviceName = state.PreferredMonitorDeviceName,
                    MonitorPlacements = state.MonitorPlacements
                        .Where(placement => !string.IsNullOrWhiteSpace(placement.MonitorDeviceName))
                        .ToList()
                };

                Widgets.Add(model);

                if (model.IsVisible)
                {
                    var hasPersistedGeometry =
                        model.Left.HasValue
                        || model.Top.HasValue
                        || model.WidgetWidth.HasValue
                        || model.WidgetHeight.HasValue;
                    enqueueWindowCreation(model, hasPersistedGeometry);
                }
            }
        }
        finally
        {
            _isRestoring = false;
        }
    }

    public WidgetModel AddWidget(
        string filePath,
        bool isVisible,
        Action<WidgetModel, bool> enqueueWindowCreation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(enqueueWindowCreation);

        var managedReferences = _importService.ImportWidgetSource(filePath);
        WidgetModel? lastModel = null;

        foreach (var managedReference in managedReferences)
        {
            lastModel = AddWidgetFromManagedReference(managedReference, isVisible, enqueueWindowCreation);
        }

        return lastModel ?? throw new InvalidOperationException("No widgets were added.");
    }

    public WidgetModel CreateWidgetWithEditor(
        string fileName,
        bool isVisible,
        Action<WidgetModel, bool> enqueueWindowCreation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(enqueueWindowCreation);

        var managedReference = _importService.CreateManagedWidget(fileName, DefaultNewWidgetTemplate);
        return AddWidgetFromManagedReference(managedReference, isVisible, enqueueWindowCreation);
    }

    public void RemoveWidget(
        WidgetModel model,
        Action<string> closeEditor,
        Action<string> untrackWindow)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(closeEditor);
        ArgumentNullException.ThrowIfNull(untrackWindow);

        closeEditor(model.Id);
        untrackWindow(model.Id);

        UnregisterModelChangeHandler(model);
        _importService.DeleteManagedWidgetFile(model.FilePath);

        Widgets.Remove(model);
    }

    public void RegisterModelChangeHandler(WidgetModel model, Action<WidgetModel, string> onPropertyChanged)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(onPropertyChanged);

        UnregisterModelChangeHandler(model);

        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (_isRestoring)
            {
                return;
            }

            onPropertyChanged(model, e.PropertyName ?? string.Empty);
        };

        _modelPropertyChangedHandlers[model.Id] = handler;
        model.PropertyChanged += handler;
    }

    public void UnregisterModelChangeHandler(WidgetModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!_modelPropertyChangedHandlers.TryGetValue(model.Id, out var handler))
        {
            return;
        }

        model.PropertyChanged -= handler;
        _modelPropertyChangedHandlers.Remove(model.Id);
    }

    public void UnregisterAllModelChangeHandlers()
    {
        foreach (var model in Widgets.ToList())
        {
            UnregisterModelChangeHandler(model);
        }
    }

    public void CloseAll()
    {
        UnregisterAllModelChangeHandlers();
    }

    private WidgetModel AddWidgetFromManagedReference(
        WidgetManagedReference managedReference,
        bool isVisible,
        Action<WidgetModel, bool> enqueueWindowCreation)
    {
        ArgumentNullException.ThrowIfNull(managedReference);
        ArgumentNullException.ThrowIfNull(enqueueWindowCreation);

        var model = new WidgetModel
        {
            Name = managedReference.WidgetName,
            FilePath = managedReference.EntryFilePath,
            IsVisible = isVisible,
            IsLocked = false,
            IsPinned = false
        };

        Widgets.Add(model);

        if (model.IsVisible)
        {
            enqueueWindowCreation(model, false);
        }

        return model;
    }
}
