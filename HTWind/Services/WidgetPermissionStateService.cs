using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Web.WebView2.Core;

namespace HTWind.Services;

public sealed partial class WidgetPermissionStateService : IWidgetPermissionStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly PermissionStateJsonContext JsonContext = new(JsonOptions);

    private readonly object _sync = new();
    private readonly string _stateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HTWind",
        "widget-permissions.json"
    );

    private Dictionary<string, CoreWebView2PermissionState>? _decisions;

    public void ClearAll()
    {
        lock (_sync)
        {
            _decisions = new Dictionary<string, CoreWebView2PermissionState>(
                StringComparer.OrdinalIgnoreCase
            );

            try
            {
                if (File.Exists(_stateFilePath))
                {
                    File.Delete(_stateFilePath);
                }
            }
            catch
            {
                // Reset failures should not block widget reset actions.
            }
        }
    }

    public bool TryGetDecision(
        string widgetFilePath,
        CoreWebView2PermissionKind permissionKind,
        out CoreWebView2PermissionState state
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetFilePath);

        lock (_sync)
        {
            EnsureLoaded();
            return _decisions!.TryGetValue(BuildKey(widgetFilePath, permissionKind), out state);
        }
    }

    public void SaveDecision(
        string widgetFilePath,
        CoreWebView2PermissionKind permissionKind,
        CoreWebView2PermissionState state
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetFilePath);

        if (
            state != CoreWebView2PermissionState.Allow
            && state != CoreWebView2PermissionState.Deny
        )
        {
            return;
        }

        lock (_sync)
        {
            EnsureLoaded();
            _decisions![BuildKey(widgetFilePath, permissionKind)] = state;
            SaveUnsafe();
        }
    }

    private void EnsureLoaded()
    {
        if (_decisions is not null)
        {
            return;
        }

        _decisions = LoadFromDisk();
    }

    private Dictionary<string, CoreWebView2PermissionState> LoadFromDisk()
    {
        if (!File.Exists(_stateFilePath))
        {
            return new Dictionary<string, CoreWebView2PermissionState>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(_stateFilePath);
            var records = JsonSerializer.Deserialize(json, JsonContext.ListPermissionDecisionRecord)
                ?? [];
            var results = new Dictionary<string, CoreWebView2PermissionState>(
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.WidgetFilePath))
                {
                    continue;
                }

                if (
                    !Enum.TryParse<CoreWebView2PermissionKind>(
                        record.PermissionKind,
                        ignoreCase: true,
                        out var parsedKind
                    )
                )
                {
                    continue;
                }

                if (
                    !Enum.TryParse<CoreWebView2PermissionState>(
                        record.State,
                        ignoreCase: true,
                        out var parsedState
                    )
                )
                {
                    continue;
                }

                if (
                    parsedState != CoreWebView2PermissionState.Allow
                    && parsedState != CoreWebView2PermissionState.Deny
                )
                {
                    continue;
                }

                results[BuildKey(record.WidgetFilePath, parsedKind)] = parsedState;
            }

            return results;
        }
        catch
        {
            return new Dictionary<string, CoreWebView2PermissionState>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveUnsafe()
    {
        try
        {
            var records = _decisions!
                .Select(entry => ToRecord(entry.Key, entry.Value))
                .Where(record => record is not null)
                .Cast<PermissionDecisionRecord>()
                .ToList();

            Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
            var json = JsonSerializer.Serialize(records, JsonContext.ListPermissionDecisionRecord);
            File.WriteAllText(_stateFilePath, json);
        }
        catch
        {
            // Persistence failures should not block the widget runtime.
        }
    }

    private static PermissionDecisionRecord? ToRecord(
        string key,
        CoreWebView2PermissionState state
    )
    {
        var parts = key.Split('|', 2, StringSplitOptions.None);
        if (parts.Length != 2)
        {
            return null;
        }

        return new PermissionDecisionRecord
        {
            WidgetFilePath = parts[0],
            PermissionKind = parts[1],
            State = state.ToString()
        };
    }

    private static string BuildKey(string widgetFilePath, CoreWebView2PermissionKind permissionKind)
    {
        var identityPath = ResolveIdentityPath(widgetFilePath);
        return $"{identityPath}|{permissionKind}";
    }

    private static string ResolveIdentityPath(string widgetFilePath)
    {
        var fullPath = Path.GetFullPath(widgetFilePath);
        var directoryPath = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return fullPath;
        }

        var manifestPath = Path.Combine(directoryPath, WidgetPackageConstants.ManifestFileName);
        return File.Exists(manifestPath)
            ? Path.GetFullPath(directoryPath)
            : fullPath;
    }

    private sealed class PermissionDecisionRecord
    {
        public string WidgetFilePath { get; set; } = string.Empty;

        public string PermissionKind { get; set; } = string.Empty;

        public string State { get; set; } = string.Empty;
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(List<PermissionDecisionRecord>))]
    private sealed partial class PermissionStateJsonContext : JsonSerializerContext;
}
