namespace HTWind.Services.Widget;

/// <summary>
///     In-memory console channel per widget. Widget windows publish WebView2 console messages so
///     the code editor can show a live console/error output panel.
/// </summary>
public static class WidgetRuntimeConsole
{
    private const int MaxEntriesPerChannel = 200;

    private sealed class Channel
    {
        public readonly List<string> Entries = new();

        public event EventHandler<string>? EntryPublished;

        public EventHandler<string>? SnapshotHandler()
        {
            return EntryPublished;
        }
    }

    private static readonly Dictionary<string, Channel> Channels = new(StringComparer.Ordinal);
    private static readonly object Sync = new();

    public static void Publish(string widgetId, string level, string message)
    {
        if (string.IsNullOrWhiteSpace(widgetId) || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var entry = $"{DateTime.Now:HH:mm:ss} [{level}] {message}";
        EventHandler<string>? handler = null;

        lock (Sync)
        {
            var channel = GetOrCreateChannel(widgetId);
            channel.Entries.Add(entry);
            if (channel.Entries.Count > MaxEntriesPerChannel)
            {
                channel.Entries.RemoveAt(0);
            }

            handler = channel.SnapshotHandler();
        }

        handler?.Invoke(null, entry);
    }

    public static IReadOnlyList<string> GetEntries(string widgetId)
    {
        lock (Sync)
        {
            return Channels.TryGetValue(widgetId, out var channel)
                ? channel.Entries.ToArray()
                : [];
        }
    }

    /// <summary>Subscribes to future console entries for the widget (no replay of history).</summary>
    public static void Subscribe(string widgetId, EventHandler<string> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetId);

        lock (Sync)
        {
            GetOrCreateChannel(widgetId).EntryPublished += handler;
        }
    }

    public static void Unsubscribe(string widgetId, EventHandler<string> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetId);

        lock (Sync)
        {
            if (Channels.TryGetValue(widgetId, out var channel))
            {
                channel.EntryPublished -= handler;
            }
        }
    }

    public static void Clear(string widgetId)
    {
        lock (Sync)
        {
            Channels.Remove(widgetId);
        }
    }

    private static Channel GetOrCreateChannel(string widgetId)
    {
        if (!Channels.TryGetValue(widgetId, out var channel))
        {
            channel = new Channel();
            Channels[widgetId] = channel;
        }

        return channel;
    }
}
