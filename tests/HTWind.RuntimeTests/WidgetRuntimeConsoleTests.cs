using HTWind.Services.Widget;

using Xunit;

namespace HTWind.RuntimeTests;

/// <summary>Contract tests for the per-widget in-memory console channel.</summary>
public sealed class WidgetRuntimeConsoleTests : IDisposable
{
    private const string WidgetId = "runtime-console-tests";

    public WidgetRuntimeConsoleTests()
    {
        WidgetRuntimeConsole.Clear(WidgetId);
    }

    public void Dispose()
    {
        WidgetRuntimeConsole.Clear(WidgetId);
    }

    [Fact]
    public void Publish_StoresTimestampedLevelPrefixedEntries()
    {
        WidgetRuntimeConsole.Publish(WidgetId, "log", "hello");
        WidgetRuntimeConsole.Publish(WidgetId, "error", "boom");

        var entries = WidgetRuntimeConsole.GetEntries(WidgetId);

        Assert.Equal(2, entries.Count);
        Assert.Multiple(
            () => Assert.Contains("[log] hello", entries[0]),
            () => Assert.Contains("[error] boom", entries[1]));
    }

    [Fact]
    public void Publish_NotifiesSubscribers()
    {
        var received = new List<string>();
        void Handler(object? sender, string entry) => received.Add(entry);

        WidgetRuntimeConsole.Subscribe(WidgetId, Handler);
        try
        {
            WidgetRuntimeConsole.Publish(WidgetId, "log", "via-event");
            WidgetRuntimeConsole.Publish(WidgetId, "log", "via-event-2");
        }
        finally
        {
            WidgetRuntimeConsole.Unsubscribe(WidgetId, Handler);
        }

        Assert.Equal(2, received.Count);
        Assert.Contains("via-event", received[0]);

        WidgetRuntimeConsole.Publish(WidgetId, "log", "after-unsubscribe");
        Assert.Equal(2, received.Count);
    }

    [Fact]
    public void Publish_DropsEmptyEntries()
    {
        WidgetRuntimeConsole.Publish(WidgetId, "log", "  ");
        WidgetRuntimeConsole.Publish(WidgetId, "log", "");
        WidgetRuntimeConsole.Publish("", "log", "no widget id");

        Assert.Empty(WidgetRuntimeConsole.GetEntries(WidgetId));
    }

    [Fact]
    public void Channels_KeepAtMost200Entries()
    {
        for (var i = 0; i < 205; i++)
        {
            WidgetRuntimeConsole.Publish(WidgetId, "log", $"entry-{i}");
        }

        var entries = WidgetRuntimeConsole.GetEntries(WidgetId);

        Assert.Equal(200, entries.Count);
        Assert.Contains("entry-5", entries[0]);
        Assert.Contains("entry-204", entries[^1]);
    }

    [Fact]
    public void Clear_RemovesTheChannel()
    {
        WidgetRuntimeConsole.Publish(WidgetId, "log", "will be cleared");
        WidgetRuntimeConsole.Clear(WidgetId);

        Assert.Empty(WidgetRuntimeConsole.GetEntries(WidgetId));
    }

    [Fact]
    public void GetEntries_ReturnsEmptyForUnknownWidget()
    {
        Assert.Empty(WidgetRuntimeConsole.GetEntries("never-published-widget"));
    }

    [Fact]
    public void Snapshot_DoesNotChangeWhenNewEntriesArePublishedOrCleared()
    {
        WidgetRuntimeConsole.Publish(WidgetId, "log", "original");
        var snapshot = WidgetRuntimeConsole.GetEntries(WidgetId);

        WidgetRuntimeConsole.Publish(WidgetId, "log", "new");
        WidgetRuntimeConsole.Clear(WidgetId);

        Assert.Contains("[log] original", Assert.Single(snapshot));
        Assert.Empty(WidgetRuntimeConsole.GetEntries(WidgetId));
    }

    [Fact]
    public void Subscription_DoesNotReplayHistoryAndCanUnsubscribeItself()
    {
        WidgetRuntimeConsole.Publish(WidgetId, "log", "before-subscribe");
        var received = new List<string>();
        EventHandler<string>? handler = null;
        handler = (_, entry) =>
        {
            received.Add(entry);
            WidgetRuntimeConsole.Unsubscribe(WidgetId, handler!);
        };
        WidgetRuntimeConsole.Subscribe(WidgetId, handler);
        try
        {
            WidgetRuntimeConsole.Publish(WidgetId, "log", "first");
            WidgetRuntimeConsole.Publish(WidgetId, "log", "second");
            Assert.Contains("[log] first", Assert.Single(received));
        }
        finally
        {
            WidgetRuntimeConsole.Unsubscribe(WidgetId, handler);
        }
    }

    [Fact]
    public void Clear_RemovesSubscriptionsAndDoesNotAffectOtherChannels()
    {
        var otherId = WidgetId + "-other";
        var received = new List<string>();
        void Handler(object? sender, string entry) => received.Add(entry);
        WidgetRuntimeConsole.Subscribe(WidgetId, Handler);
        try
        {
            WidgetRuntimeConsole.Publish(otherId, "log", "other");
            WidgetRuntimeConsole.Clear(WidgetId);
            WidgetRuntimeConsole.Publish(WidgetId, "log", "new channel");

            Assert.Empty(received);
            Assert.Contains("[log] other", Assert.Single(WidgetRuntimeConsole.GetEntries(otherId)));
        }
        finally
        {
            WidgetRuntimeConsole.Unsubscribe(WidgetId, Handler);
            WidgetRuntimeConsole.Clear(otherId);
        }
    }

    [Fact]
    public async Task ConcurrentPublishers_DeliverEveryEventAndKeepBoundedDistinctHistory()
    {
        var received = new System.Collections.Concurrent.ConcurrentBag<string>();
        void Handler(object? sender, string entry) => received.Add(entry);
        WidgetRuntimeConsole.Subscribe(WidgetId, Handler);
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 8).Select(producer => Task.Run(() =>
            {
                for (var index = 0; index < 100; index++)
                {
                    WidgetRuntimeConsole.Publish(WidgetId, "log", $"producer-{producer}-entry-{index}");
                }
            }, TestContext.Current.CancellationToken)));

            Assert.Equal(800, received.Count);
            Assert.Equal(800, received.Distinct().Count());
            var history = WidgetRuntimeConsole.GetEntries(WidgetId);
            Assert.Equal(200, history.Count);
            Assert.Equal(200, history.Distinct().Count());
            Assert.All(history, entry => Assert.Contains(entry, received));
        }
        finally
        {
            WidgetRuntimeConsole.Unsubscribe(WidgetId, Handler);
        }
    }
}
