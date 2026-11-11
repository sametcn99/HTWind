using System.IO;
using System.Text.Json;

using HTWind.Services.Application;

namespace HTWind.Services.Widget;

public sealed class WorkspaceProfile
{
    public string Name { get; set; } = string.Empty;

    public string CreatedUtc { get; set; } = string.Empty;

    public List<WorkspaceProfileWidget> Widgets { get; set; } = [];
}

public sealed class WorkspaceProfileWidget
{
    public string? Name { get; set; }

    public string? FilePath { get; set; }

    public bool IsVisible { get; set; }

    public bool IsLocked { get; set; }

    public bool IsPinned { get; set; }

    public string? PinMode { get; set; }

    public double? WidgetOpacityPercent { get; set; }

    public double? ZoomFactorPercent { get; set; }

    public double? Left { get; set; }

    public double? Top { get; set; }

    public double? Width { get; set; }

    public double? Height { get; set; }

    public string? MonitorDeviceName { get; set; }

    public string? PreferredMonitorDeviceName { get; set; }

    public bool IsPinnedFallback()
    {
        return IsPinned;
    }
}

/// <summary>
///     Persists named workspace layouts (e.g. Work / Game / Stream) so they can be switched from
///     the tray menu.
/// </summary>
public sealed class WorkspaceProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _storeFilePath;

    public WorkspaceProfileStore(string? basePath = null)
    {
        _storeFilePath = HTWindPathProvider.GetWorkspaceProfilesFilePath(basePath);
    }

    public IReadOnlyList<string> GetProfileNames()
    {
        return LoadAll().Select(profile => profile.Name).ToList();
    }

    public WorkspaceProfile? TryLoad(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        return LoadAll().FirstOrDefault(profile =>
            string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase));
    }

    public void Save(WorkspaceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Name);

        var profiles = LoadAll()
            .Where(existing => !string.Equals(existing.Name, profile.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        profiles.Add(profile);

        Directory.CreateDirectory(Path.GetDirectoryName(_storeFilePath)!);
        AtomicFile.WriteAllText(_storeFilePath, JsonSerializer.Serialize(profiles, JsonOptions));
    }

    public bool Delete(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        var profiles = LoadAll();
        var remaining = profiles
            .Where(existing => !string.Equals(existing.Name, profileName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (remaining.Count == profiles.Count)
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_storeFilePath)!);
        AtomicFile.WriteAllText(_storeFilePath, JsonSerializer.Serialize(remaining, JsonOptions));
        return true;
    }

    private List<WorkspaceProfile> LoadAll()
    {
        if (!File.Exists(_storeFilePath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<WorkspaceProfile>>(File.ReadAllText(_storeFilePath), JsonOptions)
                ?? [];
        }
        catch
        {
            return [];
        }
    }
}
