using System.Text.Json;

using Windows.Foundation;

using Windows.Media.Control;
using Windows.Storage.Streams;

namespace HTWind.Services.WebView;

/// <summary>
///     Exposes GlobalSystemMediaTransportControls (Windows SMTC) now-playing metadata, album art
///     and transport control to widgets through the zero-process <c>media.session</c> host API.
/// </summary>
public sealed class MediaSessionService
{
    private const int SessionDiscoveryDelayMilliseconds = 700;

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private readonly object _sync = new();
    private string? _cachedArtSignature;
    private string? _cachedArtDataUrl;

    public async Task<object?> HandleAsync(JsonElement? args)
    {
        var action = (ReadStringArg(args, "action") ?? "get").Trim().ToLowerInvariant();
        var includeArt = ReadBoolArg(args, "includeArt") ?? true;

        var session = await GetActiveSessionAsync();
        if (session is null)
        {
            return new { HasSession = false, Action = action };
        }

        switch (action)
        {
            case "play":
                return new { HasSession = true, Succeeded = await SafeTryAsync(session.TryPlayAsync()) };
            case "pause":
                return new { HasSession = true, Succeeded = await SafeTryAsync(session.TryPauseAsync()) };
            case "toggle":
                return new { HasSession = true, Succeeded = await SafeTryAsync(session.TryTogglePlayPauseAsync()) };
            case "next":
                return new { HasSession = true, Succeeded = await SafeTryAsync(session.TrySkipNextAsync()) };
            case "previous":
                return new { HasSession = true, Succeeded = await SafeTryAsync(session.TrySkipPreviousAsync()) };
        }

        return await BuildSessionPayloadAsync(session, includeArt, action);
    }

    private static async Task<bool> SafeTryAsync(IAsyncOperation<bool> operation)
    {
        try
        {
            return await operation;
        }
        catch
        {
            return false;
        }
    }

    private async Task<GlobalSystemMediaTransportControlsSession?> GetActiveSessionAsync()
    {
        var manager = await GetManagerAsync();
        if (manager is null)
        {
            return null;
        }

        var sessions = manager.GetSessions();
        if (sessions.Count == 0)
        {
            // The manager can enumerate an empty list right after first request; retry once.
            await Task.Delay(SessionDiscoveryDelayMilliseconds);
            sessions = manager.GetSessions();
        }

        if (sessions.Count == 0)
        {
            return null;
        }

        foreach (var session in sessions)
        {
            if (session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                return session;
            }
        }

        return sessions[0];
    }

    private async Task<GlobalSystemMediaTransportControlsSessionManager?> GetManagerAsync()
    {
        lock (_sync)
        {
            if (_manager is not null)
            {
                return _manager;
            }
        }

        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

            lock (_sync)
            {
                _manager ??= manager;
                return _manager;
            }
        }
        catch
        {
            return null;
        }
    }

    private async Task<object> BuildSessionPayloadAsync(
        GlobalSystemMediaTransportControlsSession session,
        bool includeArt,
        string action)
    {
        var mediaProperties = await session.TryGetMediaPropertiesAsync();
        var playbackInfo = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();

        var playbackStatus = playbackInfo.PlaybackStatus switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed => "Closed",
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Opened => "Opened",
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => "Changing",
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => "Stopped",
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => "Playing",
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => "Paused",
            _ => "Unknown"
        };

        var artSignature = $"{session.SourceAppUserModelId}|{mediaProperties.Title}|{mediaProperties.Artist}";
        string? albumArtDataUrl = null;

        if (includeArt && mediaProperties.Thumbnail is not null)
        {
            lock (_sync)
            {
                if (string.Equals(_cachedArtSignature, artSignature, StringComparison.Ordinal))
                {
                    albumArtDataUrl = _cachedArtDataUrl;
                }
            }

            albumArtDataUrl ??= await TryReadThumbnailAsync(mediaProperties.Thumbnail, artSignature);
        }

        return new
        {
            HasSession = true,
            Action = action,
            Media = new
            {
                Title = mediaProperties.Title,
                Artist = mediaProperties.Artist,
                AlbumArtist = mediaProperties.AlbumArtist,
                AlbumTitle = mediaProperties.AlbumTitle,
                Subtitle = mediaProperties.Subtitle,
                PlaybackStatus = playbackStatus,
                PositionSeconds = Math.Max(0, timeline.Position.TotalSeconds),
                EndTimeSeconds = Math.Max(0, timeline.EndTime.TotalSeconds),
                AlbumArtDataUrl = albumArtDataUrl
            },
            Controls = new
            {
                CanPlay = playbackInfo.Controls.IsPlayEnabled,
                CanPause = playbackInfo.Controls.IsPauseEnabled,
                CanNext = playbackInfo.Controls.IsNextEnabled,
                CanPrevious = playbackInfo.Controls.IsPreviousEnabled
            },
            SourceAppId = session.SourceAppUserModelId
        };
    }

    private async Task<string?> TryReadThumbnailAsync(
        Windows.Storage.Streams.IRandomAccessStreamReference thumbnail,
        string artSignature)
    {
        try
        {
            using var stream = await thumbnail.OpenReadAsync();
            var size = stream.Size;
            if (size == 0 || size > 8 * 1024 * 1024)
            {
                return null;
            }

            using var reader = new DataReader(stream);
            var loaded = await reader.LoadAsync((uint)size);
            if (loaded != size)
            {
                return null;
            }

            var bytes = new byte[loaded];
            reader.ReadBytes(bytes);
            reader.DetachStream();

            var contentType = string.IsNullOrWhiteSpace(stream.ContentType) ? "image/jpeg" : stream.ContentType;
            var dataUrl = $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";

            lock (_sync)
            {
                _cachedArtSignature = artSignature;
                _cachedArtDataUrl = dataUrl;
            }

            return dataUrl;
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadStringArg(JsonElement? args, string key)
    {
        if (!args.HasValue || args.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return args.Value.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool? ReadBoolArg(JsonElement? args, string key)
    {
        if (!args.HasValue || args.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!args.Value.TryGetProperty(key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }
}
