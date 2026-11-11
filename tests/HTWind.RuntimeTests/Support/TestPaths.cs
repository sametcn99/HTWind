using System.IO;

namespace HTWind.RuntimeTests.Support;

/// <summary>
///     Resolves repository paths from the test output directory and hands every test run a
///     fully isolated data directory (via HTWIND_DATA_DIR) so real subsystems never touch the
///     user profile. Initialized by <see cref="TestBootstrap"/> before any test runs.
/// </summary>
public static class TestPaths
{
    public const string DataDirEnvironmentVariable =
        HTWind.Services.Application.HTWindPathProvider.DataDirectoryEnvironmentVariable;

    private static readonly object Sync = new();

    public static string RepoRoot { get; private set; } = string.Empty;

    public static string TemplatesDirectory { get; private set; } = string.Empty;

    public static string DataRoot { get; private set; } = string.Empty;

    public static void Initialize()
    {
        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            RepoRoot = FindRepoRoot();
            TemplatesDirectory = Path.Combine(RepoRoot, "HTWind", "Templates");
            DataRoot = Path.Combine(
                Path.GetTempPath(),
                "htwind-runtime-tests",
                $"run-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DataRoot);
            _initialized = true;
        }

        // Set after the fields above are published; the process-wide variable keeps real
        // stores (widgets, permissions, profiles, WebView2 profiles) inside the isolated run
        // folder for every path-provider based subsystem.
        Environment.SetEnvironmentVariable(DataDirEnvironmentVariable, DataRoot);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => TryDelete(DataRoot);
    }

    private static bool _initialized;

    public static string CreateTempDirectory(string purpose)
    {
        var path = Path.Combine(DataRoot, purpose, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FindRepoRoot()
    {
        var candidate = new DirectoryInfo(AppContext.BaseDirectory);
        while (candidate is not null && candidate.Parent is not null)
        {
            var probe = Path.Combine(candidate.FullName, "HTWind", "Templates", "clock.html");
            if (File.Exists(probe))
            {
                return candidate.FullName;
            }

            candidate = candidate.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the HTWind repository root from the test output directory.");
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
