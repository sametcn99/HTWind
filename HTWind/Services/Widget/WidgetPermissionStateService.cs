using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using HTWind.Model;
using HTWind.Services.Application;
using Microsoft.Web.WebView2.Core;

namespace HTWind.Services.Widget;

public sealed partial class WidgetPermissionStateService : IWidgetPermissionStateService
{
    private const string PowerShellPermissionKindName = "PowerShellExecution";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly PermissionStateJsonContext JsonContext = new(JsonOptions);

    private readonly object _sync = new();
    private readonly string _stateFilePath;

    public WidgetPermissionStateService(string? basePath = null)
    {
        _stateFilePath = HTWindPathProvider.GetPermissionsFilePath(basePath);
    }

    private Dictionary<string, CoreWebView2PermissionState>? _decisions;
    private Dictionary<string, PowerShellExecutionDecision>? _powerShellDecisions;

    public void ClearAll()
    {
        lock (_sync)
        {
            _decisions = new Dictionary<string, CoreWebView2PermissionState>(
                StringComparer.OrdinalIgnoreCase
            );
            _powerShellDecisions = new Dictionary<string, PowerShellExecutionDecision>(
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

    public bool TryGetPowerShellDecision(
        string widgetFilePath,
        out PowerShellExecutionDecision decision
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetFilePath);

        lock (_sync)
        {
            EnsureLoaded();
            return _powerShellDecisions!.TryGetValue(
                ResolveIdentityPath(widgetFilePath),
                out decision
            );
        }
    }

    public void SavePowerShellDecision(
        string widgetFilePath,
        PowerShellExecutionDecision decision
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetFilePath);

        lock (_sync)
        {
            EnsureLoaded();
            _powerShellDecisions![ResolveIdentityPath(widgetFilePath)] = decision;
            SaveUnsafe();
        }
    }

    private void EnsureLoaded()
    {
        if (_decisions is not null && _powerShellDecisions is not null)
        {
            return;
        }

        var (decisions, powerShellDecisions) = LoadFromDisk();
        _decisions = decisions;
        _powerShellDecisions = powerShellDecisions;
    }

    private (Dictionary<string, CoreWebView2PermissionState>, Dictionary<string, PowerShellExecutionDecision>) LoadFromDisk()
    {
        var decisions = new Dictionary<string, CoreWebView2PermissionState>(StringComparer.OrdinalIgnoreCase);
        var powerShellDecisions = new Dictionary<string, PowerShellExecutionDecision>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(_stateFilePath))
        {
            return (decisions, powerShellDecisions);
        }

        try
        {
            var json = File.ReadAllText(_stateFilePath);
            var records = JsonSerializer.Deserialize(json, JsonContext.ListPermissionDecisionRecord)
                ?? [];

            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.WidgetFilePath))
                {
                    continue;
                }

                if (
                    string.Equals(
                        record.PermissionKind,
                        PowerShellPermissionKindName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    if (
                        Enum.TryParse<PowerShellExecutionDecision>(
                            record.State,
                            ignoreCase: true,
                            out var parsedDecision
                        )
                    )
                    {
                        powerShellDecisions[record.WidgetFilePath] = parsedDecision;
                    }

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

                decisions[BuildKey(record.WidgetFilePath, parsedKind)] = parsedState;
            }

            return (decisions, powerShellDecisions);
        }
        catch
        {
            return (decisions, powerShellDecisions);
        }
    }

    private void SaveUnsafe()
    {
        try
        {
            var records = new List<PermissionDecisionRecord>();

            foreach (var entry in _decisions!)
            {
                var record = ToRecord(entry.Key, entry.Value);
                if (record is not null)
                {
                    records.Add(record);
                }
            }

            foreach (var entry in _powerShellDecisions!)
            {
                records.Add(new PermissionDecisionRecord
                {
                    WidgetFilePath = entry.Key,
                    PermissionKind = PowerShellPermissionKindName,
                    State = entry.Value.ToString()
                });
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
            var json = JsonSerializer.Serialize(records, JsonContext.ListPermissionDecisionRecord);

            // Atomic write so a crash mid-save cannot corrupt the permission store.
            HTWind.Services.Application.AtomicFile.WriteAllText(_stateFilePath, json);
        }
        catch
        {
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
