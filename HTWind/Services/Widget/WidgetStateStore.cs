using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using HTWind.Model;
using HTWind.Services.Application;

namespace HTWind.Services.Widget;

public sealed partial class WidgetStateStore : IWidgetStateStore
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private static readonly WidgetStateJsonContext _jsonContext = new(_jsonOptions);

    private readonly string _stateFilePath;

    public WidgetStateStore(string? basePath = null)
    {
        _stateFilePath = HTWindPathProvider.GetStateFilePath(basePath);
    }

    public bool HasStateFile()
    {
        return File.Exists(_stateFilePath);
    }

    public WidgetStateSnapshot Load()
    {
        if (!File.Exists(_stateFilePath))
        {
            return new WidgetStateSnapshot();
        }

        try
        {
            var json = File.ReadAllText(_stateFilePath);

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var legacyStates = JsonSerializer.Deserialize(json, _jsonContext.ListWidgetStateRecord);
                return new WidgetStateSnapshot
                {
                    Widgets = NormalizeRecords(legacyStates),
                    SuppressWidgetsOnFullscreen = true,
                    SuppressWidgetsOnMaximized = false
                };
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                var snapshot = JsonSerializer.Deserialize(json, _jsonContext.WidgetStateSnapshot);
                if (snapshot is null)
                {
                    return new WidgetStateSnapshot();
                }

                snapshot.Widgets = NormalizeRecords(snapshot.Widgets);
                return snapshot;
            }

            return new WidgetStateSnapshot();
        }
        catch
        {
            return new WidgetStateSnapshot();
        }
    }

    private static List<WidgetStateRecord> NormalizeRecords(List<WidgetStateRecord>? records)
    {
        var normalized = records?.Where(record => record is not null).ToList() ?? [];
        foreach (var record in normalized)
        {
            record.MonitorPlacements = record.MonitorPlacements?.Where(placement => placement is not null).ToList() ?? [];
        }

        return normalized;
    }

    public void Save(WidgetStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
            var json = JsonSerializer.Serialize(snapshot, _jsonContext.WidgetStateSnapshot);

            // Atomic write so an abrupt shutdown or power loss cannot corrupt the state file.
            HTWind.Services.Application.AtomicFile.WriteAllText(_stateFilePath, json);
        }
        catch
        {
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(WidgetStateSnapshot))]
    [JsonSerializable(typeof(List<WidgetStateRecord>))]
    private sealed partial class WidgetStateJsonContext : JsonSerializerContext;
}
