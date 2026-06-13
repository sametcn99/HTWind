using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using HTWind.Model;

namespace HTWind.Services.Widget;

public sealed partial class WidgetStateStore : IWidgetStateStore
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private static readonly WidgetStateJsonContext _jsonContext = new(_jsonOptions);

    private readonly string _stateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HTWind",
        "widgets-state.json"
    );

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
                    Widgets = legacyStates ?? [],
                    SuppressWidgetsOnFullscreen = true,
                    SuppressWidgetsOnMaximized = false
                };
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                var snapshot = JsonSerializer.Deserialize(json, _jsonContext.WidgetStateSnapshot);
                return snapshot ?? new WidgetStateSnapshot();
            }

            return new WidgetStateSnapshot();
        }
        catch
        {
            return new WidgetStateSnapshot();
        }
    }

    public void Save(WidgetStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
            var json = JsonSerializer.Serialize(snapshot, _jsonContext.WidgetStateSnapshot);
            File.WriteAllText(_stateFilePath, json);
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
