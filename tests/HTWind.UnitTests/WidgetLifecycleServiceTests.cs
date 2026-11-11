using System.IO;

using HTWind.Model;
using HTWind.Services.Widget;

using Xunit;

namespace HTWind.UnitTests;

/// <summary>Widget collection loading rules and deletion cleanup.</summary>
public sealed class WidgetLifecycleServiceTests : IClassFixture<Fixtures.TempDirectoryFixture>
{
    private readonly Fixtures.TempDirectoryFixture _temp;

    public WidgetLifecycleServiceTests(Fixtures.TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    private WidgetStateStore CreateStateStore()
    {
        return new WidgetStateStore(_temp.Path);
    }

    private WidgetImportService CreateImportService()
    {
        return new WidgetImportService(_temp.Path);
    }

    private WidgetLifecycleService CreateLifecycle()
    {
        return new WidgetLifecycleService(CreateStateStore(), CreateImportService());
    }

    private WidgetStateSnapshot CreateSnapshot(params WidgetStateRecord[] records)
    {
        return new WidgetStateSnapshot { Widgets = [.. records] };
    }

    [Fact]
    public void LoadPersistedWidgets_SkipsDeletedFiles()
    {
        var keptFile = _temp.CreateFile("kept.html", "<html></html>");
        CreateStateStore().Save(CreateSnapshot(
            new WidgetStateRecord { Id = "a", FilePath = keptFile, IsVisible = true },
            new WidgetStateRecord { Id = "b", FilePath = Path.Combine(_temp.Path, "missing.html"), IsVisible = true }));

        var lifecycle = CreateLifecycle();
        var created = new List<WidgetModel>();

        lifecycle.LoadPersistedWidgets(
            (_, _) => false,
            (model, _) => created.Add(model));

        var loaded = Assert.Single(lifecycle.Widgets);
        Assert.Equal(keptFile, loaded.FilePath);
        Assert.Single(created);
    }

    [Fact]
    public void LoadPersistedWidgets_EnqueuesOnlyVisibleWidgets()
    {
        var visibleFile = _temp.CreateFile("visible.html", "<html></html>");
        var hiddenFile = _temp.CreateFile("hidden.html", "<html></html>");
        var snapshot = CreateSnapshot(
            new WidgetStateRecord { Id = "v", FilePath = visibleFile, IsVisible = true, Left = 10, Top = 20 },
            new WidgetStateRecord { Id = "h", FilePath = hiddenFile, IsVisible = false });

        CreateStateStore().Save(snapshot);

        var lifecycle = CreateLifecycle();
        var created = new List<(WidgetModel Model, bool HasGeometry)>();

        lifecycle.LoadPersistedWidgets(
            (_, _) => false,
            (model, hasGeometry) => created.Add((model, hasGeometry)));

        Assert.Equal(2, lifecycle.Widgets.Count);
        var creation = Assert.Single(created);
        Assert.Equal("v", creation.Model.Id);
        Assert.True(creation.HasGeometry);
        Assert.True(lifecycle.HasPersistedState);
    }

    [Fact]
    public void ChangeHandler_SuppressedDuringRestore_ActiveAfterwards()
    {
        var widgetFile = _temp.CreateFile("restore.html", "<html></html>");
        CreateStateStore().Save(CreateSnapshot(
            new WidgetStateRecord { Id = "r", FilePath = widgetFile, IsVisible = true }));

        var lifecycle = CreateLifecycle();
        var handlerInvocations = new List<string>();

        lifecycle.LoadPersistedWidgets(
            (_, _) => false,
            // Registering + mutating the model while still restoring must not invoke the handler.
            (model, _) =>
            {
                lifecycle.RegisterModelChangeHandler(
                    model,
                    (_, propertyName) => handlerInvocations.Add(propertyName));
                model.Name = "mutated-during-restore";
            });

        Assert.Empty(handlerInvocations);

        var model = Assert.Single(lifecycle.Widgets);
        model.Name = "mutated-after-restore";
        // The Name setter fires PropertyChanged for both Name and DisplayName.
        Assert.Contains("Name", handlerInvocations);
    }

    [Fact]
    public void AddWidget_ImportsSourceAndAddsModel()
    {
        var sourceFile = _temp.CreateFile("added.html", "<html></html>");
        var lifecycle = CreateLifecycle();

        var model = lifecycle.AddWidget(sourceFile, isVisible: false, (_, _) => { });

        Assert.Single(lifecycle.Widgets);
        Assert.NotEqual(sourceFile, model.FilePath);
        Assert.True(File.Exists(model.FilePath));
        Assert.False(model.IsVisible);
    }

    [Fact]
    public void RemoveWidget_UnsubscribesUntracksAndCleansManagedFiles()
    {
        var sourceFile = _temp.CreateFile("removable.html", "<html></html>");
        var lifecycle = CreateLifecycle();
        var model = lifecycle.AddWidget(sourceFile, isVisible: false, (_, _) => { });
        var handlerCalls = 0;
        lifecycle.RegisterModelChangeHandler(model, (_, _) => handlerCalls++);

        var managedRoot = Path.GetDirectoryName(model.FilePath)!;
        var untrackedIds = new List<string>();
        var closedIds = new List<string>();

        lifecycle.RemoveWidget(model, id => closedIds.Add(id), id => untrackedIds.Add(id));

        Assert.Single(closedIds);
        Assert.Single(untrackedIds);
        Assert.Equal(model.Id, closedIds[0]);
        Assert.DoesNotContain(model, lifecycle.Widgets);
        Assert.False(Directory.Exists(managedRoot));

        // The change handler no longer fires after removal.
        model.Name = "post-removal";
        Assert.Equal(0, handlerCalls);
    }

    [Fact]
    public void CreateWidgetWithEditor_CreatesNewManagedWidgetWithTemplate()
    {
        var lifecycle = CreateLifecycle();

        var model = lifecycle.CreateWidgetWithEditor("scratch", isVisible: false, (_, _) => { });

        Assert.True(File.Exists(model.FilePath));
        Assert.Contains("<html", File.ReadAllText(model.FilePath), StringComparison.OrdinalIgnoreCase);
        // New managed widgets always use the default entry file name inside their own folder.
        Assert.Equal(
            HTWind.Model.WidgetPackageConstants.DefaultEntryFileName,
            Path.GetFileName(model.FilePath));
    }

    [Fact]
    public void UnregisterAllModelChangeHandlers_StopsNotifications()
    {
        var sourceFile = _temp.CreateFile("unsubscribe.html", "<html></html>");
        var lifecycle = CreateLifecycle();
        var model = lifecycle.AddWidget(sourceFile, isVisible: false, (_, _) => { });
        var calls = 0;
        lifecycle.RegisterModelChangeHandler(model, (_, _) => calls++);

        lifecycle.UnregisterAllModelChangeHandlers();
        model.Name = "changed";

        Assert.Equal(0, calls);
    }
}
