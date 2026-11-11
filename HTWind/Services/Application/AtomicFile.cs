using System.IO;

namespace HTWind.Services.Application;

/// <summary>
///     Writes files atomically via a temp file plus an overwrite move so crashes or power loss
///     can never leave a truncated JSON state file behind.
/// </summary>
internal static class AtomicFile
{
    public static void WriteAllText(string path, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, path, overwrite: true);
    }

    public static async Task WriteAllTextAsync(string path, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, content);
        File.Move(tempPath, path, overwrite: true);
    }
}
