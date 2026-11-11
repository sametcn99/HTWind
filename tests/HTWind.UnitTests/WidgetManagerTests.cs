using System.Collections.ObjectModel;
using System.IO;

using HTWind.Model;
using HTWind.Services.Application;
using HTWind.Services.Editor;
using HTWind.Services.Widget;

using NSubstitute;

using Xunit;

namespace HTWind.UnitTests;

/// <summary>
///     WidgetManager behavior with substituted collaborators: workspace profiles, save
///     debouncing, reset semantics, and suppression-aware visibility.
/// </summary>
public sealed class WidgetManagerTests : IClassFixture<Fixtures.TempDirectoryFixture>, IDisposable
{
    private readonly Fixtures.TempDirectoryFixture _temp;
    private readonly IWidgetWindowFactory _windowFactory;
    private readonly IWidgetStateStore _stateStore;
    private readonly IWidgetLifecycleService _lifecycleService;
    private readonly IWidgetWindowTracker _windowTracker;
    private readonly IWidgetEditorManager _editorManager;
    private readonly IWidgetSuppressionMonitor _suppressionMonitor;
    private readonly IWidgetGeometryService _geometryService;
    private readonly IHtmlEditorService _htmlEditorService;
    private readonly IWidgetExportService _exportService;
    private readonly WidgetPermissionStateService _permissionStateService;
    private readonly WidgetManager _manager;

    public WidgetManagerTests(Fixtures.TempDirectoryFixture temp)
    {
        _temp = temp;
        _windowFactory = Substitute.For<IWidgetWindowFactory>();
        _stateStore = Substitute.For<IWidgetStateStore>();
        // NSubstitute returns null for unstaged reference-type calls; the real contract is an
        // empty snapshot for a missing state file.
        _stateStore.Load().Returns(new WidgetStateSnapshot());
        _stateStore.HasStateFile().Returns(false);
        _lifecycleService = Substitute.For<IWidgetLifecycleService>();
        _lifecycleService.Widgets.Returns(new ObservableCollection<WidgetModel>());
        _windowTracker = Substitute.For<IWidgetWindowTracker>();
        _editorManager = Substitute.For<IWidgetEditorManager>();
        _suppressionMonitor = Substitute.For<IWidgetSuppressionMonitor>();
        _suppressionMonitor.ShouldSuppressWidget(Arg.Any<WidgetModel>()).Returns(false);
        _geometryService = Substitute.For<IWidgetGeometryService>();
        _htmlEditorService = Substitute.For<IHtmlEditorService>();
        _exportService = Substitute.For<IWidgetExportService>();
        _permissionStateService = new WidgetPermissionStateService(_temp.Path);

        _manager = new WidgetManager(
            _windowFactory,
            _stateStore,
            _lifecycleService,
            _windowTracker,
            _editorManager,
            _suppressionMonitor,
            _geometryService,
            _htmlEditorService,
            _exportService,
            _permissionStateService,
            workspaceProfileStore: new WorkspaceProfileStore(_temp.Path),
            saveDebounceInterval: TimeSpan.FromMilliseconds(50));
    }

    public void Dispose()
    {
        // CloseAll stops timers and persists state; safe with substituted collaborators.
        _manager.Dispose();
    }

    private WidgetModel CreateWidgetModel(string name = "widget-a")
    {
        var file = _temp.CreateFile($"{name}.html", "<html></html>");
        return new WidgetModel { Id = Guid.NewGuid().ToString(), Name = name, FilePath = file };
    }

    #region Workspace profiles

    [Fact]
    public void CaptureAndApplyWorkspaceProfile_RestoresVisibilityAndHidesUnlistedWidgets()
    {
        var memberA = CreateWidgetModel("profile-a");
        var memberB = CreateWidgetModel("profile-b");
        _lifecycleService.Widgets.Add(memberA);
        _lifecycleService.Widgets.Add(memberB);
        memberA.IsVisible = true;
        memberB.IsVisible = true;

        _manager.CaptureWorkspaceProfile("work");
        Assert.Contains("work", _manager.GetWorkspaceProfileNames());

        // A widget added after the capture is not part of the profile.
        var outsider = CreateWidgetModel("outsider");
        _lifecycleService.Widgets.Add(outsider);
        outsider.IsVisible = true;
        memberA.IsVisible = false;

        var applied = _manager.ApplyWorkspaceProfile("work");

        Assert.True(applied);
        Assert.True(memberA.IsVisible);
        Assert.True(memberB.IsVisible);
        // Widgets not part of the profile are hidden so switching layouts is deterministic.
        Assert.False(outsider.IsVisible);
    }

    [Fact]
    public void ApplyWorkspaceProfile_UnknownProfile_ReturnsFalse()
    {
        Assert.False(_manager.ApplyWorkspaceProfile("no-such-profile"));
    }

    [Fact]
    public void ApplyWorkspaceProfile_RestoresGeometryAndPinMode()
    {
        var model = CreateWidgetModel("geometry-widget");
        _lifecycleService.Widgets.Add(model);
        model.IsVisible = false;
        model.Left = 42;
        model.Top = 24;
        model.WidgetWidth = 320;
        model.WidgetHeight = 240;
        model.PinMode = WidgetPinMode.Topmost;

        _manager.CaptureWorkspaceProfile("layout");
        model.Left = 0;
        model.Top = 0;
        model.WidgetWidth = null;
        model.PinMode = WidgetPinMode.None;

        _manager.ApplyWorkspaceProfile("layout");

        Assert.Equal(42, model.Left);
        Assert.Equal(24, model.Top);
        Assert.Equal(320, model.WidgetWidth);
        Assert.Equal(WidgetPinMode.Topmost, model.PinMode);
    }

    [Fact]
    public void DeleteWorkspaceProfile_RemovesProfile()
    {
        _manager.CaptureWorkspaceProfile("temp-profile");

        Assert.True(_manager.DeleteWorkspaceProfile("temp-profile"));
        Assert.DoesNotContain("temp-profile", _manager.GetWorkspaceProfileNames());
        Assert.False(_manager.DeleteWorkspaceProfile("temp-profile"));
    }

    #endregion

    #region State persistence

    [Fact]
    public void FlushPendingSave_PersistsStateImmediately()
    {
        var model = CreateWidgetModel("flushed");
        _lifecycleService.Widgets.Add(model);
        model.IsVisible = false;

        _manager.SetPreferredTheme(ThemeOption.Dark);
        _manager.FlushPendingSave();

        _stateStore.Received(1).Save(Arg.Is<WidgetStateSnapshot>(snapshot =>
            snapshot.ThemeOption == nameof(ThemeOption.Dark)
            && snapshot.Widgets.Count == 1
            && snapshot.Widgets[0].Id == model.Id));
    }

    [Fact]
    public void SetPreferredCulture_PersistsAfterFlush()
    {
        _manager.SetPreferredCulture("tr-TR");

        _manager.FlushPendingSave();

        _stateStore.Received().Save(Arg.Is<WidgetStateSnapshot>(snapshot =>
            snapshot.Culture == "tr-TR"));
    }

    [Fact]
    public void ModelPropertyChange_TriggersDebouncedSave()
    {
        var model = CreateWidgetModel("reactive");
        model.IsVisible = false;
        _lifecycleService.Widgets.Add(model);

        Action<WidgetModel, string>? registeredHandler = null;
        _lifecycleService
            .When(lifecycle => lifecycle.RegisterModelChangeHandler(model, Arg.Any<Action<WidgetModel, string>>()))
            .Do(callInfo => registeredHandler = callInfo.Arg<Action<WidgetModel, string>>());

        _manager.LoadPersistedWidgets();

        Assert.NotNull(registeredHandler);
        _stateStore.ClearReceivedCalls();

        // The manager's change handler schedules a save that FlushPendingSave completes.
        registeredHandler!(model, nameof(WidgetModel.IsVisible));
        _manager.FlushPendingSave();

        _stateStore.Received().Save(Arg.Is<WidgetStateSnapshot>(snapshot =>
            snapshot.Widgets.Count == 1
            && snapshot.Widgets[0].Id == model.Id));
    }

    #endregion

    #region Reset semantics

    [Fact]
    public void ResetAllWidgetsToDefaultState_ClearsPermissionsAndHidesWidgets()
    {
        var first = CreateWidgetModel("reset-a");
        var second = CreateWidgetModel("reset-b");
        _lifecycleService.Widgets.Add(first);
        _lifecycleService.Widgets.Add(second);
        first.IsVisible = true;
        first.PinMode = WidgetPinMode.Topmost;
        second.IsVisible = true;
        second.PinMode = WidgetPinMode.Desktop;
        second.Left = 500;
        second.PreferredMonitorDeviceName = "DISPLAY1";

        _permissionStateService.SavePowerShellDecision(second.FilePath!, PowerShellExecutionDecision.Full);

        _manager.ResetAllWidgetsToDefaultState();

        Assert.Equal(WidgetPinMode.None, first.PinMode);
        Assert.Equal(WidgetPinMode.None, second.PinMode);
        Assert.False(first.IsVisible);
        Assert.False(second.IsVisible);

        // Window-less models lose their stored geometry entirely.
        Assert.Null(second.Left);
        Assert.Null(second.PreferredMonitorDeviceName);
        Assert.Empty(second.MonitorPlacements);

        // Permissions were cleared.
        Assert.False(_permissionStateService.TryGetPowerShellDecision(second.FilePath!, out _));
    }

    #endregion

    #region Suppression-aware visibility (tracker-level)

    [Fact]
    public void ApplyVisibility_SuppressedWidgetWithoutWindow_CreatesNoWindow()
    {
        var model = CreateWidgetModel("suppressed");
        model.IsVisible = true;
        _lifecycleService.Widgets.Add(model);
        _windowTracker.GetWindow(model.Id).Returns((HTWind.WidgetWindow?)null);
        _suppressionMonitor.ShouldSuppressWidget(model).Returns(true);

        _manager.ApplyVisibility(model);

        // The window is not created while a fullscreen app owns the screen; creation happens
        // when suppression ends (via ReconcileRuntimeSuppressedWidgets). The WebView2 suspend /
        // resume path (SetRuntimeVisibility) is exercised by the E2E suite.
        _windowTracker.DidNotReceive().EnqueueWindowCreationAsync(
            Arg.Any<WidgetModel>(),
            Arg.Any<bool>(),
            Arg.Any<Func<WidgetModel, bool, HTWind.WidgetWindow>>());
        _windowTracker.DidNotReceive().TrackWindow(Arg.Any<WidgetModel>(), Arg.Any<HTWind.WidgetWindow>());
    }

    [Fact]
    public void ApplyVisibility_VisibleWidgetWithoutWindow_EnqueuesWindowCreation()
    {
        var model = CreateWidgetModel("normal");
        model.IsVisible = true;
        _lifecycleService.Widgets.Add(model);
        _windowTracker.GetWindow(model.Id).Returns((HTWind.WidgetWindow?)null);

        _manager.ApplyVisibility(model);

        _windowTracker.Received(1).EnqueueWindowCreationAsync(
            Arg.Is<WidgetModel>(m => m.Id == model.Id),
            Arg.Any<bool>(),
            Arg.Any<Func<WidgetModel, bool, HTWind.WidgetWindow>>());
    }

    #endregion

    #region Remove widget

    [Fact]
    public void RemoveWidget_ClosesEditorUntracksWindowAndClearsConsole()
    {
        var model = CreateWidgetModel("removed");
        _lifecycleService.Widgets.Add(model);
        _lifecycleService
            .When(lifecycle => lifecycle.RemoveWidget(
                model,
                Arg.Any<Action<string>>(),
                Arg.Any<Action<string>>()))
            .Do(callInfo =>
            {
                // The real lifecycle service invokes the close/untrack callbacks; replay them
                // here so the manager's wiring is exercised.
                callInfo.ArgAt<Action<string>>(1)(model.Id);
                callInfo.ArgAt<Action<string>>(2)(model.Id);
                _lifecycleService.Widgets.Remove(callInfo.Arg<WidgetModel>());
            });

        _manager.RemoveWidget(model);

        Received.InOrder(() =>
        {
            _editorManager.CloseEditor(model.Id);
            _windowTracker.UntrackWindow(model.Id);
        });
        _lifecycleService.Received(1).RemoveWidget(
            model,
            Arg.Any<Action<string>>(),
            Arg.Any<Action<string>>());

        _manager.FlushPendingSave();
        _stateStore.Received().Save(Arg.Is<WidgetStateSnapshot>(snapshot =>
            snapshot.Widgets.Count == 0));
    }

    #endregion
}
