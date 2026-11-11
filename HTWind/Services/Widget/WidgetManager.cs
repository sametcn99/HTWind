using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;

using HTWind.Localization;
using HTWind.Model;
using HTWind.Services.Application;
using HTWind.Services.Editor;
using HTWind.Services.WebView;

using Microsoft.Win32;

namespace HTWind.Services.Widget;

public class WidgetManager : IWidgetManager, IDisposable
{
    private const double DefaultWidgetWidth = 300;
    private const double DefaultWidgetHeight = 300;

    private readonly DispatcherTimer _saveDebounceTimer;
    private readonly EventHandler _saveDebounceTickHandler;
    private readonly IWidgetStateStore _stateStore;
    private readonly IWidgetLifecycleService _lifecycleService;
    private readonly IWidgetWindowTracker _windowTracker;
    private readonly IWidgetEditorManager _editorManager;
    private readonly IWidgetSuppressionMonitor _suppressionMonitor;
    private readonly IWidgetGeometryService _geometryService;
    private readonly IHtmlEditorService _htmlEditorService;
    private readonly IWidgetWindowFactory _windowFactory;
    private readonly IWidgetExportService _exportService;
    private readonly IWidgetPermissionStateService _widgetPermissionStateService;
    private readonly IWidgetHostApiService _widgetHostApiService;
    private readonly IDialogService _dialogService;
    private readonly WorkspaceProfileStore _workspaceProfileStore;
    private ThemeOption? _preferredTheme;
    private string? _preferredCulture;
    private bool _isDisposed;

    public WidgetManager(
        IWidgetWindowFactory windowFactory,
        IWidgetStateStore stateStore,
        IWidgetLifecycleService lifecycleService,
        IWidgetWindowTracker windowTracker,
        IWidgetEditorManager editorManager,
        IWidgetSuppressionMonitor suppressionMonitor,
        IWidgetGeometryService geometryService,
        IHtmlEditorService htmlEditorService,
        IWidgetExportService exportService,
        IWidgetPermissionStateService widgetPermissionStateService,
        IWidgetHostApiService? widgetHostApiService = null,
        WorkspaceProfileStore? workspaceProfileStore = null,
        TimeSpan? saveDebounceInterval = null,
        IDialogService? dialogService = null)
    {
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
        _windowTracker = windowTracker ?? throw new ArgumentNullException(nameof(windowTracker));
        _editorManager = editorManager ?? throw new ArgumentNullException(nameof(editorManager));
        _suppressionMonitor = suppressionMonitor ?? throw new ArgumentNullException(nameof(suppressionMonitor));
        _geometryService = geometryService ?? throw new ArgumentNullException(nameof(geometryService));
        _htmlEditorService = htmlEditorService ?? throw new ArgumentNullException(nameof(htmlEditorService));
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _widgetPermissionStateService = widgetPermissionStateService ?? throw new ArgumentNullException(nameof(widgetPermissionStateService));
        _widgetHostApiService = widgetHostApiService ?? new HTWind.Services.WebView.WidgetHostApiService();
        _workspaceProfileStore = workspaceProfileStore ?? new WorkspaceProfileStore();
        _dialogService = dialogService ?? MessageBoxDialogService.Instance;

        _saveDebounceTimer = new DispatcherTimer
        {
            Interval = saveDebounceInterval ?? TimeSpan.FromMilliseconds(350)
        };
        _saveDebounceTickHandler = SaveDebounceTimer_Tick;
        _saveDebounceTimer.Tick += _saveDebounceTickHandler;

        _suppressionMonitor.Start(ReconcileRuntimeSuppressedWidgets);
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
    }

    public ObservableCollection<WidgetModel> Widgets => _lifecycleService.Widgets;
    public bool HasPersistedState => _lifecycleService.HasPersistedState;

    public bool IsFullscreenSuppressionEnabled
    {
        get => _suppressionMonitor.IsFullscreenSuppressionEnabled;
        set
        {
            if (_suppressionMonitor.IsFullscreenSuppressionEnabled == value)
            {
                return;
            }

            _suppressionMonitor.IsFullscreenSuppressionEnabled = value;
            ReconcileRuntimeSuppressedWidgets();
            ScheduleSave();
        }
    }

    public bool IsMaximizedSuppressionEnabled
    {
        get => _suppressionMonitor.IsMaximizedSuppressionEnabled;
        set
        {
            if (_suppressionMonitor.IsMaximizedSuppressionEnabled == value)
            {
                return;
            }

            _suppressionMonitor.IsMaximizedSuppressionEnabled = value;
            ReconcileRuntimeSuppressedWidgets();
            ScheduleSave();
        }
    }

    /// <summary>Theme restored from widgets-state.json, if any.</summary>
    public ThemeOption? PreferredTheme => _preferredTheme;

    /// <summary>Culture restored from widgets-state.json, if any.</summary>
    public string? PreferredCulture => _preferredCulture;

    public void SetPreferredTheme(ThemeOption option)
    {
        if (_preferredTheme == option)
        {
            return;
        }

        _preferredTheme = option;
        ScheduleSave();
    }

    public void SetPreferredCulture(string cultureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);

        if (string.Equals(_preferredCulture, cultureName, StringComparison.Ordinal))
        {
            return;
        }

        _preferredCulture = cultureName;
        ScheduleSave();
    }

    public void LoadPersistedWidgets()
    {
        var snapshot = _stateStore.Load();
        _suppressionMonitor.IsFullscreenSuppressionEnabled = snapshot.SuppressWidgetsOnFullscreen;
        _suppressionMonitor.IsMaximizedSuppressionEnabled = snapshot.SuppressWidgetsOnMaximized;

        if (
            !string.IsNullOrWhiteSpace(snapshot.ThemeOption)
            && Enum.TryParse<ThemeOption>(snapshot.ThemeOption, ignoreCase: true, out var parsedTheme)
        )
        {
            _preferredTheme = parsedTheme;
        }

        _preferredCulture = snapshot.Culture;

        _lifecycleService.LoadPersistedWidgets(
            (model, hasPersistedGeometry) => ShouldSuppressWidget(model),
            (model, hasPersistedGeometry) => EnqueueVisibleWindowCreation(model, hasPersistedGeometry));

        foreach (var model in _lifecycleService.Widgets)
        {
            _lifecycleService.RegisterModelChangeHandler(model, OnModelPropertyChanged);
        }
    }

    public void AddWidget(string filePath)
    {
        AddWidget(filePath, true);
    }

    public void AddWidget(string filePath, bool isVisible)
    {
        var model = _lifecycleService.AddWidget(filePath, isVisible, EnqueueVisibleWindowCreation);
        _lifecycleService.RegisterModelChangeHandler(model, OnModelPropertyChanged);
        model.RefreshFileTimestamps();
        ScheduleSave();
    }

    public void AddWidgets(IEnumerable<string> filePaths, bool isVisible)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        var addedAny = false;

        foreach (var filePath in filePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            try
            {
                var model = _lifecycleService.AddWidget(
                    filePath,
                    isVisible,
                    EnqueueVisibleWindowCreation);
                _lifecycleService.RegisterModelChangeHandler(model, OnModelPropertyChanged);
                model.RefreshFileTimestamps();
                addedAny = true;
            }
            catch
            {
                // A single broken source must not abort the batch.
            }
        }

        if (addedAny)
        {
            // Single debounced state save for the entire batch.
            ScheduleSave();
        }
    }

    public void CreateWidgetWithEditor(string fileName, bool isVisible, bool enableHotReload)
    {
        var model = _lifecycleService.CreateWidgetWithEditor(fileName, isVisible, EnqueueVisibleWindowCreation);
        _lifecycleService.RegisterModelChangeHandler(model, OnModelPropertyChanged);
        model.RefreshFileTimestamps();
        OpenEditorCore(model, enableHotReload);
        ScheduleSave();
    }

    public string ExportWidget(WidgetModel model, string destinationDirectory)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        if (string.IsNullOrWhiteSpace(model.FilePath) || !File.Exists(model.FilePath))
        {
            throw new InvalidOperationException("The widget file could not be found for export.");
        }

        return _exportService.ExportWidgetToDirectory(model.FilePath, destinationDirectory);
    }

    public string ExportWidgets(
        IEnumerable<WidgetModel> models,
        string destinationDirectory,
        string exportName)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportName);

        var exportPaths = models
            .Select(model => model?.FilePath)
            .Where(filePath => !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (exportPaths.Count == 0)
        {
            throw new InvalidOperationException("No exportable widgets were found.");
        }

        return _exportService.ExportWidgetsToDirectory(exportPaths, destinationDirectory, exportName);
    }

    public void OpenEditor(WidgetModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        OpenEditorCore(model, true);
    }

    public void ApplyVisibility(WidgetModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var window = _windowTracker.GetWindow(model.Id);

        if (window is null)
        {
            if (!model.IsVisible)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(model.FilePath) || !File.Exists(model.FilePath))
            {
                return;
            }

            if (_suppressionMonitor.ShouldSuppressWidget(model))
            {
                return;
            }

            var hasPersistedGeometry =
                model.Left.HasValue
                || model.Top.HasValue
                || model.WidgetWidth.HasValue
                || model.WidgetHeight.HasValue;
            EnqueueVisibleWindowCreation(model, hasPersistedGeometry);
            return;
        }

        if (!model.IsVisible)
        {
            _geometryService.CaptureGeometry(window, model);
            ScheduleSave();
            window.Close();
            return;
        }

        if (_suppressionMonitor.ShouldSuppressWidget(model))
        {
            // Suppression must not destroy the WebView2: hide + suspend so state is preserved
            // and un-suppressing is instant (no re-creation on Alt+Tab transitions).
            _geometryService.CaptureGeometry(window, model);
            ScheduleSave();
            window.SetRuntimeVisibility(false);
            return;
        }

        RestoreWindowGeometryFromModel(window, model);
        if (_geometryService.EnsureVisibleOnAvailableDisplay(window, model))
        {
            _geometryService.CaptureGeometry(window, model);
            ScheduleSave();
        }

        window.SetRuntimeVisibility(true);
    }

    public void ApplyPinState(WidgetModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var window = _windowTracker.GetWindow(model.Id);
        if (window is null)
        {
            return;
        }

        window.ApplyPinMode(model.PinMode);
    }

    public void ResetWidgetPosition(WidgetModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var window = _windowTracker.GetWindow(model.Id);

        if (window is null)
        {
            if (string.IsNullOrWhiteSpace(model.FilePath) || !File.Exists(model.FilePath))
            {
                return;
            }

            var hasPersistedGeometry =
                model.Left.HasValue
                || model.Top.HasValue
                || model.WidgetWidth.HasValue
                || model.WidgetHeight.HasValue;
            window = CreateAndTrackWidgetWindow(model, hasPersistedGeometry);

            if (window is null)
            {
                return;
            }
        }

        _geometryService.ResetToPrimaryDisplayCenter(
            window,
            model,
            DefaultWidgetWidth,
            DefaultWidgetHeight
        );
        ScheduleSave();
    }

    public void ResetAllWidgetsToDefaultState()
    {
        _widgetPermissionStateService.ClearAll();

        foreach (var model in Widgets.ToList())
        {
            model.PinMode = WidgetPinMode.None;
            model.IsPinned = false;
            ApplyPinState(model);

            var window = _windowTracker.GetWindow(model.Id);
            if (window is not null)
            {
                _geometryService.ResetToPrimaryDisplayCenter(
                    window,
                    model,
                    DefaultWidgetWidth,
                    DefaultWidgetHeight
                );
            }
            else
            {
                model.Left = null;
                model.Top = null;
                model.WidgetWidth = null;
                model.WidgetHeight = null;
                model.MonitorDeviceName = null;
                model.PreferredMonitorDeviceName = null;
                model.MonitorPlacements = [];
            }

            model.IsVisible = false;
            ApplyVisibility(model);
        }

        ScheduleSave();
    }

    public void RemoveWidget(WidgetModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        _lifecycleService.RemoveWidget(
            model,
            widgetId => _editorManager.CloseEditor(widgetId),
            widgetId => _windowTracker.UntrackWindow(widgetId));

        WidgetRuntimeConsole.Clear(model.Id);
        ScheduleSave();
    }

    #region Workspace profiles (layouts)

    public IReadOnlyList<string> GetWorkspaceProfileNames()
    {
        return _workspaceProfileStore.GetProfileNames();
    }

    public void CaptureWorkspaceProfile(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        var profile = new WorkspaceProfile
        {
            Name = profileName,
            CreatedUtc = DateTimeOffset.UtcNow.ToString("O")
        };

        foreach (var model in Widgets)
        {
            profile.Widgets.Add(new WorkspaceProfileWidget
            {
                Name = model.Name,
                FilePath = model.FilePath,
                IsVisible = model.IsVisible,
                IsLocked = model.IsLocked,
                PinMode = model.PinMode.ToPersistedValue(),
                WidgetOpacityPercent = model.WidgetOpacityPercent,
                ZoomFactorPercent = model.ZoomFactorPercent,
                Left = model.Left,
                Top = model.Top,
                Width = model.WidgetWidth,
                Height = model.WidgetHeight,
                MonitorDeviceName = model.MonitorDeviceName,
                PreferredMonitorDeviceName = model.PreferredMonitorDeviceName
            });
        }

        _workspaceProfileStore.Save(profile);
    }

    public bool ApplyWorkspaceProfile(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        var profile = _workspaceProfileStore.TryLoad(profileName);
        if (profile is null)
        {
            return false;
        }

        var matchedWidgetIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var profileWidget in profile.Widgets)
        {
            if (string.IsNullOrWhiteSpace(profileWidget.FilePath) || !File.Exists(profileWidget.FilePath))
            {
                continue;
            }

            var model = Widgets.FirstOrDefault(candidate =>
                string.Equals(candidate.FilePath, profileWidget.FilePath, StringComparison.OrdinalIgnoreCase))
                ?? Widgets.FirstOrDefault(candidate =>
                    !string.IsNullOrWhiteSpace(profileWidget.Name)
                    && string.Equals(candidate.Name, profileWidget.Name, StringComparison.OrdinalIgnoreCase));

            if (model is null)
            {
                var countBefore = Widgets.Count;
                AddWidget(profileWidget.FilePath, isVisible: false);
                model = Widgets.Count > countBefore ? Widgets[^1] : null;
            }

            if (model is null)
            {
                continue;
            }

            matchedWidgetIds.Add(model.Id);

            model.IsLocked = profileWidget.IsLocked;
            model.PinMode = WidgetPinModeExtensions.FromPersistedValue(profileWidget.PinMode, profileWidget.IsPinnedFallback());
            model.WidgetOpacityPercent = profileWidget.WidgetOpacityPercent ?? 100;
            model.ZoomFactorPercent = profileWidget.ZoomFactorPercent ?? 100;
            model.Left = profileWidget.Left;
            model.Top = profileWidget.Top;
            model.WidgetWidth = profileWidget.Width;
            model.WidgetHeight = profileWidget.Height;
            model.MonitorDeviceName = profileWidget.MonitorDeviceName;
            model.PreferredMonitorDeviceName = profileWidget.PreferredMonitorDeviceName;
            model.IsVisible = profileWidget.IsVisible;

            ApplyPinState(model);
            ApplyVisibility(model);
        }

        // Widgets not part of this profile are hidden so switching layouts is deterministic.
        foreach (var model in Widgets.Where(candidate => !matchedWidgetIds.Contains(candidate.Id)).ToList())
        {
            if (!model.IsVisible)
            {
                continue;
            }

            model.IsVisible = false;
            ApplyVisibility(model);
        }

        ScheduleSave();
        return true;
    }

    public bool DeleteWorkspaceProfile(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        return _workspaceProfileStore.Delete(profileName);
    }

    #endregion

    #region Widget settings (manifest settingsSchema)

    public void OpenWidgetSettings(WidgetModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var settingsWindow = new WidgetSettingsWindow(
            model,
            _widgetHostApiService,
            () => _windowTracker.GetWindow(model.Id)?.ApplyInjectedSettings())
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        settingsWindow.ShowDialog();
    }

    #endregion

    public void CloseAll()
    {
        if (_isDisposed)
        {
            return;
        }

        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        _saveDebounceTimer.Stop();
        _suppressionMonitor.Stop();

        _editorManager.CloseAll();

        foreach (var window in _windowTracker.GetAllWindows().Values.ToList())
        {
            _geometryService.CaptureGeometry(window, window.Model);
            window.Close();
        }

        _windowTracker.ClearAll();
        _lifecycleService.CloseAll();

        SaveStateToDisk();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed || !disposing)
        {
            return;
        }

        CloseAll();
        (_suppressionMonitor as IDisposable)?.Dispose();
        _isDisposed = true;
    }

    private void SaveDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        _saveDebounceTimer.Stop();
        SaveStateToDisk();
    }

    /// <summary>Stops the debounce timer and persists the state immediately (test seam).</summary>
    internal void FlushPendingSave()
    {
        _saveDebounceTimer.Stop();
        SaveStateToDisk();
    }

    private void ScheduleSave()
    {
        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Start();
    }

    private void SaveStateToDisk()
    {
        var snapshot = new WidgetStateSnapshot
        {
            SuppressWidgetsOnFullscreen = IsFullscreenSuppressionEnabled,
            SuppressWidgetsOnMaximized = IsMaximizedSuppressionEnabled,
            ThemeOption = _preferredTheme?.ToString(),
            Culture = _preferredCulture,
            Widgets = Widgets
                .Select(model => new WidgetStateRecord
                {
                    Id = model.Id,
                    Name = model.Name,
                    FilePath = model.FilePath,
                    IsVisible = model.IsVisible,
                    IsLocked = model.IsLocked,
                    IsPinned = model.IsPinned,
                    PinMode = model.PinMode.ToPersistedValue(),
                    WidgetOpacityPercent = model.WidgetOpacityPercent,
                    ZoomFactorPercent = model.ZoomFactorPercent,
                    Left = model.Left,
                    Top = model.Top,
                    WidgetWidth = model.WidgetWidth,
                    WidgetHeight = model.WidgetHeight,
                    MonitorDeviceName = model.MonitorDeviceName,
                    PreferredMonitorDeviceName = model.PreferredMonitorDeviceName,
                    MonitorPlacements = model.MonitorPlacements
                        .Where(placement => !string.IsNullOrWhiteSpace(placement.MonitorDeviceName))
                        .Select(placement => new WidgetMonitorPlacement
                        {
                            MonitorDeviceName = placement.MonitorDeviceName,
                            Left = placement.Left,
                            Top = placement.Top,
                            Width = placement.Width,
                            Height = placement.Height,
                            LastSeenAtUtc = placement.LastSeenAtUtc
                        })
                        .ToList()
                })
                .ToList()
        };

        _stateStore.Save(snapshot);
    }

    private void OnModelPropertyChanged(WidgetModel model, string propertyName)
    {
        if (
            propertyName == nameof(WidgetModel.Name)
            || propertyName == nameof(WidgetModel.FilePath)
            || propertyName == nameof(WidgetModel.IsVisible)
            || propertyName == nameof(WidgetModel.IsLocked)
            || propertyName == nameof(WidgetModel.IsPinned)
            || propertyName == nameof(WidgetModel.PinMode)
            || propertyName == nameof(WidgetModel.WidgetOpacityPercent)
            || propertyName == nameof(WidgetModel.ZoomFactorPercent)
            || propertyName == nameof(WidgetModel.Left)
            || propertyName == nameof(WidgetModel.Top)
            || propertyName == nameof(WidgetModel.WidgetWidth)
            || propertyName == nameof(WidgetModel.WidgetHeight)
            || propertyName == nameof(WidgetModel.MonitorDeviceName)
            || propertyName == nameof(WidgetModel.PreferredMonitorDeviceName)
            || propertyName == nameof(WidgetModel.MonitorPlacements)
        )
        {
            ScheduleSave();
        }
    }

    private bool ShouldSuppressWidget(WidgetModel model)
    {
        return _suppressionMonitor.ShouldSuppressWidget(model);
    }

    private void EnqueueVisibleWindowCreation(WidgetModel model, bool hasPersistedGeometry)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (_suppressionMonitor.ShouldSuppressWidget(model))
        {
            return;
        }

        _ = _windowTracker.EnqueueWindowCreationAsync(
            model,
            hasPersistedGeometry,
            (m, hasGeometry) => CreateAndTrackWidgetWindow(m, hasGeometry)!);
    }

    private WidgetWindow? CreateAndTrackWidgetWindow(WidgetModel model, bool hasPersistedGeometry)
    {
        if (_windowTracker.HasWindow(model.Id))
        {
            return _windowTracker.GetWindow(model.Id);
        }

        NormalizePersistertGeometry(model);

        var window = _windowFactory.Create(model);
        _windowTracker.TrackWindow(model, window);

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
            _windowTracker.ScheduleGeometryCapture(
                model.Id,
                window,
                model,
                () =>
                {
                    _geometryService.CaptureGeometry(window, model);
                    ScheduleSave();
                });
        };
        window.SizeChanged += (_, _) =>
        {
            _windowTracker.ScheduleGeometryCapture(
                model.Id,
                window,
                model,
                () =>
                {
                    _geometryService.CaptureGeometry(window, model);
                    ScheduleSave();
                });
        };

        window.Closed += (_, _) =>
        {
            _geometryService.CaptureGeometry(window, model);
            ScheduleSave();
            _windowTracker.UntrackWindow(model.Id);
        };

        window.SetRuntimeVisibility(false);
        _geometryService.CaptureGeometry(window, model);

        window.ApplyPinMode(model.PinMode);
        window.ApplyWidgetOpacity();
        window.ApplyZoomFactor();

        return window;
    }

    private void OpenEditorCore(WidgetModel model, bool enableHotReload)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrWhiteSpace(model.FilePath) || !File.Exists(model.FilePath))
        {
            _dialogService.ShowWarning(
                LocalizationService.Get("EditorWindow_LoadError"),
                LocalizationService.Get("EditorWindow_Title")
            );
            return;
        }

        if (!model.IsVisible)
        {
            model.IsVisible = true;
            ApplyVisibility(model);
        }

        _editorManager.OpenEditor(model, enableHotReload, (m, hotReload) =>
        {
            var editor = new HtmlCodeEditorWindow(m, _htmlEditorService, hotReload);
            editor.ContentSaved += (_, _) =>
            {
                m.RefreshFileTimestamps();
                var widgetWindow = _windowTracker.GetWindow(m.Id);
                widgetWindow?.RefreshContent();
            };
            editor.LivePreviewChanged += (_, e) =>
            {
                var widgetWindow = _windowTracker.GetWindow(m.Id);
                if (widgetWindow is null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(e.HtmlContent))
                {
                    widgetWindow.ClearLivePreview();
                    return;
                }

                widgetWindow.ApplyLivePreviewContent(e.HtmlContent);
            };
            editor.Closed += (_, _) =>
            {
                var widgetWindow = _windowTracker.GetWindow(m.Id);
                widgetWindow?.ClearLivePreview();
            };

            return editor;
        });
    }

    private void ReconcileRuntimeSuppressedWidgets()
    {
        if (_isDisposed || Widgets.Count == 0)
        {
            return;
        }

        foreach (var model in Widgets)
        {
            var window = _windowTracker.GetWindow(model.Id);

            if (!model.IsVisible)
            {
                continue;
            }

            if (_suppressionMonitor.ShouldSuppressWidget(model))
            {
                if (window is not null)
                {
                    _geometryService.CaptureGeometry(window, model);
                    ScheduleSave();
                    // Hide + suspend instead of closing so the WebView2 (and widget state) survives.
                    window.SetRuntimeVisibility(false);
                }

                continue;
            }

            if (window is null)
            {
                var hasPersistedGeometry =
                    model.Left.HasValue
                    || model.Top.HasValue
                    || model.WidgetWidth.HasValue
                    || model.WidgetHeight.HasValue;
                EnqueueVisibleWindowCreation(model, hasPersistedGeometry);
            }
            else if (!window.IsVisible)
            {
                // Widget was runtime-suppressed; restore it now that suppression ended.
                window.SetRuntimeVisibility(true);
            }
        }
    }

    private static void NormalizePersistertGeometry(WidgetModel model)
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

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        WidgetGeometryService.InvalidateMonitorCache();

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
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

        foreach (var window in _windowTracker.GetAllWindows().Values)
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

        ReconcileRuntimeSuppressedWidgets();
    }

    private static void RestoreWindowGeometryFromModel(WidgetWindow window, WidgetModel model)
    {
        if (model.WidgetWidth.HasValue && model.WidgetWidth.Value > 0)
        {
            window.Width = model.WidgetWidth.Value;
        }

        if (model.WidgetHeight.HasValue && model.WidgetHeight.Value > 0)
        {
            window.Height = model.WidgetHeight.Value;
        }

        if (model.Left.HasValue)
        {
            window.Left = model.Left.Value;
        }

        if (model.Top.HasValue)
        {
            window.Top = model.Top.Value;
        }
    }
}
