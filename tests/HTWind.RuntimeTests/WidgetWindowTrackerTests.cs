using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;

using HTWind.Model;
using HTWind.Services.Widget;
using HTWind.RuntimeTests.Support;

using Xunit;

namespace HTWind.RuntimeTests;

public sealed class WidgetWindowTrackerTests
{
    [Fact]
    public async Task CancelQueuedCreation_DoesNotInvokeFactoryWhenQueueDrains()
    {
        using var sta = new StaRunner();
        var creationCount = 0;
        var widgetFile = Path.Combine(TestPaths.CreateTempDirectory("queued-widget"), "widget.html");
        await File.WriteAllTextAsync(widgetFile, "<html></html>", TestContext.Current.CancellationToken);

        await sta.RunAsync(async () =>
        {
            var tracker = new WidgetWindowTracker();
            var canceled = new WidgetModel { Id = "canceled", FilePath = widgetFile, IsVisible = true };
            var queueField = typeof(WidgetWindowTracker).GetField(
                "_visibleWindowCreateQueue",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var queuedIdsField = typeof(WidgetWindowTracker).GetField(
                "_queuedVisibleWindowIds",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var queue = queueField.GetValue(tracker)!;
            queueField.FieldType.GetMethod("Enqueue")!.Invoke(queue, [(canceled, false, (Func<WidgetModel, bool, WidgetWindow>)((WidgetModel _, bool _) =>
            {
                Interlocked.Increment(ref creationCount);
                return null!;
            }))]);
            ((HashSet<string>)queuedIdsField.GetValue(tracker)!).Add(canceled.Id);

            // Model the in-flight queue worker so the requested item stays queued until
            // cancellation, then trigger the normal processor with a missing-file sentinel.
            typeof(WidgetWindowTracker).GetField(
                "_isProcessingVisibleWindowQueue",
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(tracker, true);
            await tracker.EnqueueWindowCreationAsync(
                new WidgetModel { Id = "drain", FilePath = null, IsVisible = true },
                hasPersistedGeometry: false,
                (_, _) => throw new Xunit.Sdk.XunitException("The sentinel must have no file path."));
            tracker.CancelQueuedCreation(canceled.Id);
            typeof(WidgetWindowTracker).GetField(
                "_isProcessingVisibleWindowQueue",
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(tracker, false);
            await (Task)typeof(WidgetWindowTracker).GetMethod(
                "ProcessVisibleWindowQueueAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(tracker, null)!;

            Assert.Equal(0, Volatile.Read(ref creationCount));
            Assert.Empty(tracker.GetAllWindows());
        });
    }
}
