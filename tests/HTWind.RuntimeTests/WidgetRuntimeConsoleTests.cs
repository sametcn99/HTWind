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
}
