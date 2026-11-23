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
///     Focused coverage for WidgetManager paths that do not require creating WPF windows.
///     Each test gets its own disk store so profile and permission state cannot leak.
/// </summary>
public sealed class WidgetManagerCoverageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "htwind-manager-coverage", Guid.NewGuid().ToString("N"));
    private readonly IWidgetWindowFactory _windowFactory = Substitute.For<IWidgetWindowFactory>();
    private readonly IWidgetStateStore _stateStore = Substitute.For<IWidgetStateStore>();
    private readonly IWidgetLifecycleService _lifecycle = Substitute.For<IWidgetLifecycleService>();
    private readonly IWidgetWindowTracker _tracker = Substitute.For<IWidgetWindowTracker>();
    private readonly IWidgetEditorManager _editor = Substitute.For<IWidgetEditorManager>();
    private readonly IWidgetSuppressionMonitor _suppression = Substitute.For<IWidgetSuppressionMonitor>();
    private readonly IWidgetGeometryService _geometry = Substitute.For<IWidgetGeometryService>();
    private readonly IHtmlEditorService _htmlEditor = Substitute.For<IHtmlEditorService>();
    private readonly IWidgetExportService _export = Substitute.For<IWidgetExportService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly WidgetManager _manager;

    public WidgetManagerCoverageTests()
    {
        Directory.CreateDirectory(_directory);
        _stateStore.Load().Returns(new WidgetStateSnapshot());
        _stateStore.HasStateFile().Returns(false);
        _lifecycle.Widgets.Returns(new ObservableCollection<WidgetModel>());
        _suppression.ShouldSuppressWidget(Arg.Any<WidgetModel>()).Returns(false);
        _manager = new WidgetManager(
            _windowFactory,
            _stateStore,
            _lifecycle,
            _tracker,
            _editor,
            _suppression,
            _geometry,
            _htmlEditor,
            _export,
            new WidgetPermissionStateService(_directory),
            workspaceProfileStore: new WorkspaceProfileStore(_directory),
            saveDebounceInterval: TimeSpan.FromHours(1),
            dialogService: _dialogs);
    }

    public void Dispose()
    {
        _manager.Dispose();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for Windows file handles released after the test.
        }
    }

    private WidgetModel Widget(string name, bool visible = false)
    {
        var file = Path.Combine(_directory, name + ".html");
        File.WriteAllText(file, "<html></html>");
        return new WidgetModel { Id = Guid.NewGuid().ToString(), Name = name, FilePath = file, IsVisible = visible };
    }

    [Fact]
    public void AddWidgets_FiltersBlankPathsAndContinuesAfterOneLifecycleFailure()
    {
        var first = Widget("batch-first");
        var last = Widget("batch-last");
        _lifecycle.AddWidget(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<Action<WidgetModel, bool>>())
            .Returns(call =>
            {
                var path = call.ArgAt<string>(0);
                if (path == "broken")
                {
                    throw new IOException("bad source");
                }

                var model = path == "first" ? first : last;
                _lifecycle.Widgets.Add(model);
                return model;
            });

        _manager.AddWidgets(["first", " ", "broken", "last"], isVisible: false);

        Received.InOrder(() =>
        {
            _lifecycle.AddWidget("first", false, Arg.Any<Action<WidgetModel, bool>>());
            _lifecycle.AddWidget("broken", false, Arg.Any<Action<WidgetModel, bool>>());
            _lifecycle.AddWidget("last", false, Arg.Any<Action<WidgetModel, bool>>());
        });
        _lifecycle.Received(2).RegisterModelChangeHandler(Arg.Any<WidgetModel>(), Arg.Any<Action<WidgetModel, string>>());
        Assert.NotNull(first.FileUpdatedUtc);
        Assert.NotNull(last.FileUpdatedUtc);

        _manager.FlushPendingSave();
        _stateStore.Received(1).Save(Arg.Is<WidgetStateSnapshot>(snapshot =>
            snapshot.Widgets.Count == 2
            && snapshot.Widgets.Any(widget => widget.Id == first.Id)
            && snapshot.Widgets.Any(widget => widget.Id == last.Id)));
    }

    [Fact]
    public void AddWidgets_EmptyBatchDoesNotChangeLifecycle()
    {
        _manager.AddWidgets(["", "  ", "\t"], isVisible: true);

        _lifecycle.DidNotReceive().AddWidget(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<Action<WidgetModel, bool>>());
    }

    [Fact]
    public void AddWidgets_NullPathsThrows()
    {
        Assert.Throws<ArgumentNullException>(() => _manager.AddWidgets(null!, isVisible: true));
    }

    [Fact]
    public void AddWidget_RegistersChangeHandlerAndRefreshesFileTimes()
    {
        var model = Widget("single-add");
        _lifecycle.AddWidget(model.FilePath!, true, Arg.Any<Action<WidgetModel, bool>>()).Returns(model);

        _manager.AddWidget(model.FilePath!);

        _lifecycle.Received(1).RegisterModelChangeHandler(model, Arg.Any<Action<WidgetModel, string>>());
        Assert.NotNull(model.FileCreatedUtc);
        Assert.NotNull(model.FileUpdatedUtc);
    }

    [Fact]
    public void CreateWidgetWithEditor_RegistersModelAndPassesHotReloadChoice()
    {
        var model = Widget("editor-created");
        _lifecycle.CreateWidgetWithEditor("editor-created.html", false, Arg.Any<Action<WidgetModel, bool>>()).Returns(_ =>
        {
            _lifecycle.Widgets.Add(model);
            return model;
        });

        _manager.CreateWidgetWithEditor("editor-created.html", isVisible: false, enableHotReload: false);

        _lifecycle.Received(1).RegisterModelChangeHandler(model, Arg.Any<Action<WidgetModel, string>>());
        _editor.Received(1).OpenEditor(model, false, Arg.Any<Func<WidgetModel, bool, HtmlCodeEditorWindow>>());
        Assert.NotNull(model.FileUpdatedUtc);
        _manager.FlushPendingSave();
        _stateStore.Received(1).Save(Arg.Is<WidgetStateSnapshot>(snapshot => snapshot.Widgets.Any(widget => widget.Id == model.Id)));
    }

    [Fact]
    public void OpenEditor_MakesValidWidgetVisibleAndRequestsHotReload()
    {
        var model = Widget("open-editor");
        model.IsVisible = false;

        _manager.OpenEditor(model);

        Assert.True(model.IsVisible);
        _tracker.Received(1).EnqueueWindowCreationAsync(model, false, Arg.Any<Func<WidgetModel, bool, WidgetWindow>>());
        _editor.Received(1).OpenEditor(model, true, Arg.Any<Func<WidgetModel, bool, HtmlCodeEditorWindow>>());
    }

    [Fact]
    public void OpenEditor_MissingSourceWarnsWithoutOpeningEditor()
    {
        var model = new WidgetModel { Id = "editor-missing", FilePath = Path.Combine(_directory, "absent.html") };

        _manager.OpenEditor(model);

        _dialogs.Received(1).ShowWarning(Arg.Any<string>(), Arg.Any<string>());
        _editor.DidNotReceive().OpenEditor(Arg.Any<WidgetModel>(), Arg.Any<bool>(), Arg.Any<Func<WidgetModel, bool, HtmlCodeEditorWindow>>());
    }

    [Fact]
    public void LoadPersistedWidgets_RestoresSettingsAndWiresSuppressionAwareCreation()
    {
        var model = Widget("loaded", visible: true);
        var snapshot = new WidgetStateSnapshot
        {
            SuppressWidgetsOnFullscreen = false,
            SuppressWidgetsOnMaximized = true,
            ThemeOption = "dArK",
            Culture = "tr-TR"
        };
        _stateStore.Load().Returns(snapshot);
        _lifecycle.When(x => x.LoadPersistedWidgets(Arg.Any<Func<WidgetModel, bool, bool>>(), Arg.Any<Action<WidgetModel, bool>>()))
            .Do(call =>
            {
                var shouldSuppress = call.ArgAt<Func<WidgetModel, bool, bool>>(0);
                var enqueue = call.ArgAt<Action<WidgetModel, bool>>(1);
                Assert.False(shouldSuppress(model, true));
                enqueue(model, true);
                _lifecycle.Widgets.Add(model);
            });

        _manager.LoadPersistedWidgets();

        Assert.Equal(ThemeOption.Dark, _manager.PreferredTheme);
        Assert.Equal("tr-TR", _manager.PreferredCulture);
        Assert.False(_manager.IsFullscreenSuppressionEnabled);
        Assert.True(_manager.IsMaximizedSuppressionEnabled);
        _tracker.Received(1).EnqueueWindowCreationAsync(model, true, Arg.Any<Func<WidgetModel, bool, WidgetWindow>>());
        _lifecycle.Received(1).RegisterModelChangeHandler(model, Arg.Any<Action<WidgetModel, string>>());
    }

    [Fact]
    public void LoadPersistedWidgets_IgnoresInvalidThemeAndSkipsCreationWhenSuppressed()
    {
        var model = Widget("suppressed-load", visible: true);
        _stateStore.Load().Returns(new WidgetStateSnapshot { ThemeOption = "invalid" });
        _suppression.ShouldSuppressWidget(model).Returns(true);
        _lifecycle.When(x => x.LoadPersistedWidgets(Arg.Any<Func<WidgetModel, bool, bool>>(), Arg.Any<Action<WidgetModel, bool>>()))
            .Do(call =>
            {
                var enqueue = call.ArgAt<Action<WidgetModel, bool>>(1);
                enqueue(model, false);
            });

        _manager.LoadPersistedWidgets();

        Assert.Null(_manager.PreferredTheme);
        _tracker.DidNotReceive().EnqueueWindowCreationAsync(Arg.Any<WidgetModel>(), Arg.Any<bool>(), Arg.Any<Func<WidgetModel, bool, WidgetWindow>>());
    }

    [Fact]
    public void FlushPendingSave_CopiesMonitorPlacementsAndFiltersInvalidDeviceNames()
    {
        var model = Widget("snapshot");
        model.MonitorPlacements =
        [
            new WidgetMonitorPlacement { MonitorDeviceName = "DISPLAY1", Left = 1, Top = 2, Width = 3, Height = 4 },
            new WidgetMonitorPlacement { MonitorDeviceName = "  ", Left = 5 }
        ];
        _lifecycle.Widgets.Add(model);

        WidgetStateSnapshot? saved = null;
        _stateStore.When(store => store.Save(Arg.Any<WidgetStateSnapshot>()))
            .Do(call => saved = call.Arg<WidgetStateSnapshot>());

        _manager.FlushPendingSave();

        _stateStore.Received(1).Save(Arg.Any<WidgetStateSnapshot>());
        var savedPlacement = Assert.Single(Assert.Single(saved!.Widgets).MonitorPlacements);
        Assert.Equal("DISPLAY1", savedPlacement.MonitorDeviceName);
        Assert.Equal(1, savedPlacement.Left);
        Assert.NotSame(model.MonitorPlacements[0], savedPlacement);
        savedPlacement.Left = 99;
        Assert.Equal(1, model.MonitorPlacements[0].Left);
    }

    [Fact]
    public void SuppressionFlagChangesReconcileAndPersistOnlyWhenValueChanges()
    {
        var model = Widget("suppression-flag", visible: true);
        _lifecycle.Widgets.Add(model);
        _manager.IsFullscreenSuppressionEnabled = true; // default substitute value is false
        _tracker.Received(1).EnqueueWindowCreationAsync(model, false, Arg.Any<Func<WidgetModel, bool, WidgetWindow>>());
        _manager.FlushPendingSave();
        _stateStore.Received(1).Save(Arg.Is<WidgetStateSnapshot>(snapshot => snapshot.SuppressWidgetsOnFullscreen));

        _tracker.ClearReceivedCalls();
        _manager.IsFullscreenSuppressionEnabled = true;
        _tracker.DidNotReceive().EnqueueWindowCreationAsync(Arg.Any<WidgetModel>(), Arg.Any<bool>(), Arg.Any<Func<WidgetModel, bool, WidgetWindow>>());
    }

    [Fact]
    public void ApplyVisibility_DoesNotCreateWindowForInvisibleOrMissingFile()
    {
        var invisible = Widget("invisible");
        var missing = new WidgetModel { Id = "missing", IsVisible = true, FilePath = Path.Combine(_directory, "absent.html") };

        _manager.ApplyVisibility(invisible);
        _manager.ApplyVisibility(missing);

        _tracker.DidNotReceive().EnqueueWindowCreationAsync(Arg.Any<WidgetModel>(), Arg.Any<bool>(), Arg.Any<Func<WidgetModel, bool, WidgetWindow>>());
    }

    [Fact]
    public void ApplyVisibility_UsesPersistedGeometryPresenceWhenCreatingWindow()
    {
        var model = Widget("geometry-queue", visible: true);
        model.MonitorDeviceName = "DISPLAY2";
        model.WidgetHeight = 400;

        _manager.ApplyVisibility(model);

        _tracker.Received(1).EnqueueWindowCreationAsync(model, true, Arg.Any<Func<WidgetModel, bool, WidgetWindow>>());
    }

    [Fact]
    public void ApplyVisibility_SuppressionPreventsWindowCreation()
    {
        var model = Widget("suppressed-now", visible: true);
        _suppression.ShouldSuppressWidget(model).Returns(true);

        _manager.ApplyVisibility(model);

        _tracker.DidNotReceive().EnqueueWindowCreationAsync(Arg.Any<WidgetModel>(), Arg.Any<bool>(), Arg.Any<Func<WidgetModel, bool, WidgetWindow>>());
    }

    [Fact]
    public void ApplyPinStateAndResetPosition_NoOpWhenWindowOrSourceIsUnavailable()
    {
        var model = new WidgetModel { Id = "no-window", FilePath = Path.Combine(_directory, "missing.html"), PinMode = WidgetPinMode.Topmost };
        _manager.ApplyPinState(model);
        _manager.ResetWidgetPosition(model);

        _windowFactory.DidNotReceive().Create(Arg.Any<WidgetModel>());
        _geometry.DidNotReceive().ResetToPrimaryDisplayCenter(Arg.Any<WidgetWindow>(), Arg.Any<WidgetModel>(), Arg.Any<double>(), Arg.Any<double>());
    }

    [Fact]
    public void CaptureAndApplyWorkspaceProfile_ImportsAvailableFilesAndSkipsMissingReferences()
    {
        var importedFile = Widget("profile-import-file").FilePath!;
        var existing = Widget("existing-visible", visible: true);
        _lifecycle.Widgets.Add(existing);
        var imported = new WidgetModel { Id = "imported", Name = "imported", FilePath = importedFile };
        _lifecycle.AddWidget(importedFile, false, Arg.Any<Action<WidgetModel, bool>>()).Returns(_ =>
        {
            _lifecycle.Widgets.Add(imported);
            return imported;
        });
        var profiles = new WorkspaceProfileStore(_directory);
        profiles.Save(new WorkspaceProfile
        {
            Name = "restore",
            Widgets =
            [
                new WorkspaceProfileWidget
                {
                    Name = "profile-import-file", FilePath = importedFile, IsVisible = true, IsLocked = true,
                    PinMode = null, IsPinned = true, WidgetOpacityPercent = 70, ZoomFactorPercent = 125,
                    Left = 50, Top = 60, Width = 500, Height = 350,
                    MonitorDeviceName = "DISPLAY1", PreferredMonitorDeviceName = "DISPLAY2"
                },
                new WorkspaceProfileWidget { Name = "gone", FilePath = Path.Combine(_directory, "gone.html"), IsVisible = true }
            ]
        });

        var applied = _manager.ApplyWorkspaceProfile("RESTORE");

        Assert.True(applied);
        Assert.True(imported.IsVisible);
        Assert.True(imported.IsLocked);
        Assert.Equal(WidgetPinMode.Topmost, imported.PinMode);
        Assert.Equal(70, imported.WidgetOpacityPercent);
        Assert.Equal(125, imported.ZoomFactorPercent);
        Assert.Equal(50, imported.Left);
        Assert.Equal(500, imported.WidgetWidth);
        Assert.Equal("DISPLAY2", imported.PreferredMonitorDeviceName);
        Assert.False(existing.IsVisible);
        _lifecycle.Received(1).AddWidget(importedFile, false, Arg.Any<Action<WidgetModel, bool>>());
        _tracker.Received(1).EnqueueWindowCreationAsync(imported, true, Arg.Any<Func<WidgetModel, bool, WidgetWindow>>());
    }

    [Fact]
    public void ApplyWorkspaceProfile_RestoresEveryWidgetWhenMultipleModelsShareOneFile()
    {
        var sharedFile = Widget("shared-instance-file").FilePath!;
        var first = new WidgetModel
        {
            Id = "shared-first", Name = "First instance", FilePath = sharedFile, IsVisible = false,
            IsLocked = true, WidgetOpacityPercent = 55, Left = 101, Top = 102, WidgetWidth = 401, WidgetHeight = 402
        };
        var second = new WidgetModel
        {
            Id = "shared-second", Name = "Second instance", FilePath = sharedFile, IsVisible = true,
            IsLocked = false, WidgetOpacityPercent = 85, Left = 201, Top = 202, WidgetWidth = 501, WidgetHeight = 502
        };
        _lifecycle.Widgets.Add(first);
        _lifecycle.Widgets.Add(second);

        _manager.CaptureWorkspaceProfile("shared-file-instances");

        first.IsVisible = true;
        first.IsLocked = false;
        first.WidgetOpacityPercent = 20;
        first.Left = -101;
        first.Top = -102;
        first.WidgetWidth = 32;
        first.WidgetHeight = 33;
        second.IsVisible = false;
        second.IsLocked = true;
        second.WidgetOpacityPercent = 30;
        second.Left = -201;
        second.Top = -202;
        second.WidgetWidth = 42;
        second.WidgetHeight = 43;

        Assert.True(_manager.ApplyWorkspaceProfile("shared-file-instances"));

        Assert.False(first.IsVisible);
        Assert.True(first.IsLocked);
        Assert.Equal(55, first.WidgetOpacityPercent);
        Assert.Equal(101, first.Left);
        Assert.Equal(401, first.WidgetWidth);
        Assert.True(second.IsVisible);
        Assert.False(second.IsLocked);
        Assert.Equal(85, second.WidgetOpacityPercent);
        Assert.Equal(201, second.Left);
        Assert.Equal(501, second.WidgetWidth);
        _tracker.Received(1).EnqueueWindowCreationAsync(second, true, Arg.Any<Func<WidgetModel, bool, WidgetWindow>>());
    }

    [Fact]
    public void ApplyWorkspaceProfile_MatchesByNameWhenPathChangedAndUsesDefaultOptionalValues()
    {
        var widgetFile = Widget("renamed").FilePath!;
        var profileFile = Widget("old-name").FilePath!;
        var model = new WidgetModel
        {
            Id = "same-name", Name = "SharedName", FilePath = widgetFile, IsVisible = true,
            WidgetOpacityPercent = 20, ZoomFactorPercent = 50, PinMode = WidgetPinMode.Topmost
        };
        _lifecycle.Widgets.Add(model);
        new WorkspaceProfileStore(_directory).Save(new WorkspaceProfile
        {
            Name = "by-name",
            Widgets = [new WorkspaceProfileWidget { Name = "sharedname", FilePath = profileFile, IsVisible = false }]
        });

        Assert.True(_manager.ApplyWorkspaceProfile("by-name"));

        Assert.Equal(100, model.WidgetOpacityPercent);
        Assert.Equal(100, model.ZoomFactorPercent);
        Assert.Equal(WidgetPinMode.None, model.PinMode);
        Assert.False(model.IsVisible);
        Assert.Equal(widgetFile, model.FilePath);
        _lifecycle.DidNotReceive().AddWidget(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<Action<WidgetModel, bool>>());
    }

    [Fact]
    public void WorkspaceProfileArgumentValidationRejectsBlankNames()
    {
        Assert.Throws<ArgumentException>(() => _manager.CaptureWorkspaceProfile(" "));
        Assert.Throws<ArgumentException>(() => _manager.ApplyWorkspaceProfile("\t"));
        Assert.Throws<ArgumentException>(() => _manager.DeleteWorkspaceProfile(""));
    }

    [Fact]
    public void ManagerStateAndWorkspaceNamesReflectTheirCollaborators()
    {
        _lifecycle.HasPersistedState.Returns(true);
        new WorkspaceProfileStore(_directory).Save(new WorkspaceProfile { Name = "Work" });

        Assert.True(_manager.HasPersistedState);
        Assert.Same(_lifecycle.Widgets, _manager.Widgets);
        Assert.Equal(["Work"], _manager.GetWorkspaceProfileNames());
    }

    [Fact]
    public void ExportWidget_ValidatesSourceAndDelegatesValidFile()
    {
        var model = Widget("export-single");
        _export.ExportWidgetToDirectory(model.FilePath!, _directory).Returns("archive.zip");

        Assert.Equal("archive.zip", _manager.ExportWidget(model, _directory));
        _export.Received(1).ExportWidgetToDirectory(model.FilePath!, _directory);
        Assert.Throws<InvalidOperationException>(() => _manager.ExportWidget(new WidgetModel { FilePath = "missing" }, _directory));
        Assert.Throws<ArgumentNullException>(() => _manager.ExportWidget(null!, _directory));
        Assert.Throws<ArgumentException>(() => _manager.ExportWidget(model, " "));
    }

    [Fact]
    public void ExportWidgets_FiltersMissingAndDuplicatePathsAndRequiresAtLeastOneFile()
    {
        var model = Widget("export-batch");
        _export.ExportWidgetsToDirectory(Arg.Any<IEnumerable<string>>(), _directory, "bundle").Returns("bundle.zip");
        var inputs = new WidgetModel?[]
        {
            model,
            new() { FilePath = model.FilePath!.ToUpperInvariant() },
            null,
            new() { FilePath = Path.Combine(_directory, "missing.html") }
        };

        Assert.Equal("bundle.zip", _manager.ExportWidgets(inputs!, _directory, "bundle"));
        _export.Received(1).ExportWidgetsToDirectory(
            Arg.Is<IEnumerable<string>>(paths => paths.Count() == 1 && paths.Single() == model.FilePath),
            _directory,
            "bundle");

        Assert.Throws<InvalidOperationException>(() => _manager.ExportWidgets([new WidgetModel()], _directory, "empty"));
        Assert.Throws<ArgumentNullException>(() => _manager.ExportWidgets(null!, _directory, "bundle"));
        Assert.Throws<ArgumentException>(() => _manager.ExportWidgets([model], _directory, " "));
    }

    [Fact]
    public void SetPreferredValues_RejectsBlankCultureAndPersistsChangedValues()
    {
        Assert.Throws<ArgumentException>(() => _manager.SetPreferredCulture(" "));
        _manager.SetPreferredTheme(ThemeOption.Light);
        _manager.SetPreferredCulture("en-US");
        _manager.SetPreferredTheme(ThemeOption.Light);
        _manager.SetPreferredCulture("en-US");

        _manager.FlushPendingSave();

        _stateStore.Received(1).Save(Arg.Is<WidgetStateSnapshot>(snapshot => snapshot.ThemeOption == "Light" && snapshot.Culture == "en-US"));
    }

    [Fact]
    public void Dispose_ClosesCollaboratorsSavesAndIsIdempotent()
    {
        _manager.Dispose();
        _manager.Dispose();

        _suppression.Received(1).Stop();
        _editor.Received(1).CloseAll();
        _tracker.Received(1).ClearAll();
        _lifecycle.Received(1).CloseAll();
        _stateStore.Received(1).Save(Arg.Any<WidgetStateSnapshot>());
    }
}
