using System.IO;

namespace HTWind.E2ETests.Support;

/// <summary>
///     Gives every E2E run a fully isolated data directory (via HTWIND_DATA_DIR) so the real
///     application never touches the user profile.
/// </summary>
public static class E2EPaths
{
    public const string DataDirEnvironmentVariable =
        HTWind.Services.Application.HTWindPathProvider.DataDirectoryEnvironmentVariable;

    private static readonly object Sync = new();
    private static bool _initialized;

    public static string DataRoot { get; private set; } = string.Empty;

    public static void Initialize()
    {
        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            DataRoot = Path.Combine(
                Path.GetTempPath(),
                "htwind-e2e-tests",
                $"run-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DataRoot);
            _initialized = true;
        }

        Environment.SetEnvironmentVariable(DataDirEnvironmentVariable, DataRoot);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => TryDelete(DataRoot);
    }

    public static string CreateTempDirectory(string purpose)
    {
        var path = Path.Combine(DataRoot, purpose, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Files may still be locked by the WebView2 browser process at exit; best effort.
        }
    }
}
