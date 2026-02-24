using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;

using Microsoft.Win32;

using HTWind.Localization;

namespace HTWind.Services;

public class WidgetManager : IWidgetManager
{
    private const double DefaultWidgetWidth = 300;
    private const double DefaultWidgetHeight = 300;
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
    private readonly Dictionary<string, HtmlCodeEditorWindow> _editorWindowsById = new();
    private readonly IHtmlEditorService _htmlEditorService;
    private readonly IWidgetGeometryService _geometryService;
    private readonly Dictionary<string, PropertyChangedEventHandler> _modelPropertyChangedHandlers =
        new();
    private readonly DispatcherTimer _saveDebounceTimer;
    private readonly IWidgetStateRepository _stateRepository;
    private readonly IWidgetWindowFactory _windowFactory;
    private readonly Dictionary<string, WidgetWindow> _windowsById = new();
    private bool _isRestoring;

    public WidgetManager(
        IWidgetWindowFactory windowFactory,
        IWidgetStateRepository stateRepository,
        IWidgetGeometryService geometryService,
        IHtmlEditorService htmlEditorService
    )
    {
        _windowFactory =
            windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
        _stateRepository =
            stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _geometryService =
            geometryService ?? throw new ArgumentNullException(nameof(geometryService));
        _htmlEditorService =
            htmlEditorService ?? throw new ArgumentNullException(nameof(htmlEditorService));

        _saveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _saveDebounceTimer.Tick += (_, _) =>
        {
            _saveDebounceTimer.Stop();
            SaveStateToDisk();
        };

        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
    }

    public ObservableCollection<WidgetModel> Widgets { get; } = new();
    public bool HasPersistedState { get; private set; }

    public void LoadPersistedWidgets()
    {
        HasPersistedState = _stateRepository.HasStateFile();

        var states = _stateRepository.Load();
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
                    MonitorDeviceName = state.MonitorDeviceName
                };

                CreateAndTrackWidgetWindow(model, true);
                Widgets.Add(model);
            }
        }
        finally
        {
            _isRestoring = false;
        }
    }

    public void AddWidget(string filePath)
    {
        AddWidget(filePath, true);
    }

    public void AddWidget(string filePath, bool isVisible)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        var storedPath = _stateRepository.IsManagedWidgetPath(filePath)
            ? filePath
            : _stateRepository.CopyWidgetToManagedStorage(filePath);

        AddWidgetFromManagedPath(storedPath, isVisible);
    }

    public void CreateWidgetWithEditor(string fileName, bool isVisible, bool enableHotReload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var managedPath = _stateRepository.CreateManagedWidgetFile(fileName, DefaultNewWidgetTemplate);
        var model = AddWidgetFromManagedPath(managedPath, isVisible);
        OpenEditorCore(model, enableHotReload);
    }

    public void OpenEditor(WidgetModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        OpenEditorCore(model, true);
    }

    private WidgetModel AddWidgetFromManagedPath(string managedPath, bool isVisible)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedPath);

        var model = new WidgetModel
        {
            Name = Path.GetFileName(managedPath),
            FilePath = managedPath,
            IsVisible = isVisible,
            IsLocked = false,
            IsPinned = false
        };

        CreateAndTrackWidgetWindow(model, false);
        Widgets.Add(model);
        ScheduleSave();
        return model;
    }

    private void OpenEditorCore(WidgetModel model, bool enableHotReload)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrWhiteSpace(model.FilePath) || !File.Exists(model.FilePath))
        {
            MessageBox.Show(
                LocalizationService.Get("EditorWindow_LoadError"),
                LocalizationService.Get("EditorWindow_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        if (_editorWindowsById.TryGetValue(model.Id, out var existingEditor))
        {
            existingEditor.Activate();
            return;
        }

        var editor = new HtmlCodeEditorWindow(model, _htmlEditorService, enableHotReload);
        editor.ContentSaved += (_, _) =>
        {
            if (_windowsById.TryGetValue(model.Id, out var window))
            {
                window.RefreshContent();
            }
        };
        editor.LivePreviewChanged += (_, e) =>
        {
            if (!_windowsById.TryGetValue(model.Id, out var window))
            {
                return;
            }

            if (string.IsNullOrEmpty(e.HtmlContent))
            {
                window.ClearLivePreview();
                return;
            }

            window.ApplyLivePreviewContent(e.HtmlContent);
        };
        editor.Closed += (_, _) =>
        {
            if (_windowsById.TryGetValue(model.Id, out var window))
            {
                window.ClearLivePreview();
            }

            _editorWindowsById.Remove(model.Id);
        };

        _editorWindowsById[model.Id] = editor;
        editor.Show();
    }

    public void ApplyVisibility(WidgetModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!_windowsById.TryGetValue(model.Id, out var window))
        {
            if (!model.IsVisible)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(model.FilePath) || !File.Exists(model.FilePath))
            {
                return;
            }

            var hasPersistedGeometry =
                model.Left.HasValue
                || model.Top.HasValue
                || model.WidgetWidth.HasValue
                || model.WidgetHeight.HasValue;
            CreateAndTrackWidgetWindow(model, hasPersistedGeometry);
            return;
        }

        if (model.IsVisible && _geometryService.EnsureVisibleOnAvailableDisplay(window, model))
        {
            _geometryService.CaptureGeometry(window, model);
            ScheduleSave();
        }

        window.SetRuntimeVisibility(model.IsVisible);
    }

    public void ApplyPinState(WidgetModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!_windowsById.TryGetValue(model.Id, out var window))
        {
            return;
        }

        window.Topmost = model.IsPinned;
    }

    public void RemoveWidget(WidgetModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (_editorWindowsById.TryGetValue(model.Id, out var editor))
        {
            editor.Close();
            _editorWindowsById.Remove(model.Id);
        }

        if (_windowsById.TryGetValue(model.Id, out var window))
        {
            window.Close();
            _windowsById.Remove(model.Id);
        }

        UnregisterModelChangeHandler(model);
        _stateRepository.DeleteManagedWidgetFile(model.FilePath);

        Widgets.Remove(model);
        ScheduleSave();
    }

    public void CloseAll()
    {
        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;

        foreach (var editor in _editorWindowsById.Values.ToList())
        {
            editor.Close();
        }

        _editorWindowsById.Clear();

        foreach (var window in _windowsById.Values.ToList())
        {
            window.Close();
        }

        _windowsById.Clear();
        foreach (var model in Widgets.ToList())
        {
            UnregisterModelChangeHandler(model);
        }

        SaveStateToDisk();
    }

    private void CreateAndTrackWidgetWindow(WidgetModel model, bool hasPersistedGeometry)
    {
        NormalizePersistedGeometry(model);

        var window = _windowFactory.Create(model);
        _windowsById[model.Id] = window;

        if (hasPersistedGeometry)
        {
            _geometryService.ApplyPersistedGeometry(
                window,
                model,
                DefaultWidgetWidth,
                DefaultWidgetHeight
            );
        }
        else
        {
            _geometryService.ApplyDefaultGeometry(window, DefaultWidgetWidth, DefaultWidgetHeight);
        }

        window.LocationChanged += (_, _) =>
        {
            _geometryService.CaptureGeometry(window, model);
            ScheduleSave();
        };
        window.SizeChanged += (_, _) =>
        {
            _geometryService.CaptureGeometry(window, model);
            ScheduleSave();
        };
        RegisterModelChangeHandler(model);

        window.Closed += (_, _) =>
        {
            _windowsById.Remove(model.Id);
            UnregisterModelChangeHandler(model);
        };

        window.SetRuntimeVisibility(model.IsVisible);
        _geometryService.CaptureGeometry(window, model);

        window.Topmost = model.IsPinned;
    }

    private static void NormalizePersistedGeometry(WidgetModel model)
    {
        const double minimumPersistedSize = 32;

        if (model.WidgetWidth.HasValue && model.WidgetWidth.Value < minimumPersistedSize)
        {
            model.WidgetWidth = null;
        }

        if (model.WidgetHeight.HasValue && model.WidgetHeight.Value < minimumPersistedSize)
        {
            model.WidgetHeight = null;
        }

        if (model.Left.HasValue && double.IsNaN(model.Left.Value))
        {
            model.Left = null;
        }

        if (model.Top.HasValue && double.IsNaN(model.Top.Value))
        {
            model.Top = null;
        }
    }

    private void OnModelPropertyChanged(WidgetModel model, PropertyChangedEventArgs e)
    {
        if (_isRestoring)
        {
            return;
        }

        if (
            e.PropertyName == nameof(WidgetModel.Name)
            || e.PropertyName == nameof(WidgetModel.FilePath)
            || e.PropertyName == nameof(WidgetModel.IsVisible)
            || e.PropertyName == nameof(WidgetModel.IsLocked)
            || e.PropertyName == nameof(WidgetModel.IsPinned)
            || e.PropertyName == nameof(WidgetModel.Left)
            || e.PropertyName == nameof(WidgetModel.Top)
            || e.PropertyName == nameof(WidgetModel.WidgetWidth)
            || e.PropertyName == nameof(WidgetModel.WidgetHeight)
            || e.PropertyName == nameof(WidgetModel.MonitorDeviceName)
        )
        {
            ScheduleSave();
        }
    }

    private void RegisterModelChangeHandler(WidgetModel model)
    {
        UnregisterModelChangeHandler(model);

        PropertyChangedEventHandler handler = (_, e) => OnModelPropertyChanged(model, e);
        _modelPropertyChangedHandlers[model.Id] = handler;
        model.PropertyChanged += handler;
    }

    private void UnregisterModelChangeHandler(WidgetModel model)
    {
        if (!_modelPropertyChangedHandlers.TryGetValue(model.Id, out var handler))
        {
            return;
        }

        model.PropertyChanged -= handler;
        _modelPropertyChangedHandlers.Remove(model.Id);
    }

    private void ScheduleSave()
    {
        if (_isRestoring)
        {
            return;
        }

        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Start();
    }

    private void SaveStateToDisk()
    {
        _stateRepository.Save(
            Widgets.Select(model => new WidgetStateRecord
            {
                Id = model.Id,
                Name = model.Name,
                FilePath = model.FilePath,
                IsVisible = model.IsVisible,
                IsLocked = model.IsLocked,
                IsPinned = model.IsPinned,
                Left = model.Left,
                Top = model.Top,
                WidgetWidth = model.WidgetWidth,
                WidgetHeight = model.WidgetHeight,
                MonitorDeviceName = model.MonitorDeviceName
            })
        );
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        _ = dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(RecoverWidgetsAfterDisplayConfigurationChanged)
        );
    }

    private void RecoverWidgetsAfterDisplayConfigurationChanged()
    {
        var hasRelocatedWidget = false;

        foreach (var window in _windowsById.Values)
        {
            var model = window.Model;
            if (!_geometryService.EnsureVisibleOnAvailableDisplay(window, model))
            {
                continue;
            }

            _geometryService.CaptureGeometry(window, model);
            hasRelocatedWidget = true;
        }

        if (hasRelocatedWidget)
        {
            ScheduleSave();
        }
    }
}
