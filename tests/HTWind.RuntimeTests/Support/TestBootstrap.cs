using System.IO;
using System.Management.Automation.Runspaces;
using System.Runtime.CompilerServices;

namespace HTWind.RuntimeTests.Support;

/// <summary>
///     Process-wide test bootstrap that runs before any test code.
/// </summary>
internal static class TestBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        TestPaths.Initialize();
        EnsurePowerShellBuiltInModules();
    }

    /// <summary>
    ///     The System.Management.Automation NuGet package ships no built-in modules, and the
    ///     engine resolves built-in commands exclusively from $PSHOME\Modules where $PSHOME is
    ///     the SMA assembly directory. Stage a minimal $PSHOME next to SMA.dll from the
    ///     installed pwsh (command assemblies plus the Modules tree), which is exactly the
    ///     deployment shape a shipped HTWind bundle provides. Without this, the read-only
    ///     sandbox is effectively limited to the cmdlets compiled into SMA itself.
    /// </summary>
    private static void EnsurePowerShellBuiltInModules()
    {
        try
        {
            var smaDirectory = Path.GetDirectoryName(typeof(Runspace).Assembly.Location);
            if (string.IsNullOrEmpty(smaDirectory))
            {
                return;
            }

            var stagedMarker = Path.Combine(
                smaDirectory,
                "Modules",
                "Microsoft.PowerShell.Management");
            if (!Directory.Exists(stagedMarker))
            {
                var powerShellHome = ResolvePowerShellHome();
                if (powerShellHome is null || !Directory.Exists(Path.Combine(powerShellHome, "Modules")))
                {
                    return;
                }

                CopyDirectory(
                    Path.Combine(powerShellHome, "Modules"),
                    Path.Combine(smaDirectory, "Modules"));
            }

            // Keep the root command assemblies fresh on every run (cheap existence checks);
            // module manifests reference them relative to the staged $PSHOME.
            var powerShellHomeDirectory = ResolvePowerShellHome();
            if (powerShellHomeDirectory is not null)
            {
                foreach (var fileName in new[]
                         {
                             "Microsoft.PowerShell.Commands.Diagnostics.dll",
                             "Microsoft.PowerShell.Commands.Management.dll",
                             "Microsoft.PowerShell.Commands.Utility.dll",
                             "Microsoft.PowerShell.Security.dll",
                             "Microsoft.PowerShell.ConsoleHost.dll",
                             "Microsoft.PowerShell.MarkdownRender.dll",
                         })
                {
                    CopyIfExists(
                        Path.Combine(powerShellHomeDirectory, fileName),
                        Path.Combine(smaDirectory, fileName));
                }
            }

            // Prepend the staged Modules directory (idempotent, every process) so Core-edition
            // discovery wins over the Desktop-edition manifests on the inherited PSModulePath
            // (System32's manifests match by name first and fail to load against the Core
            // engine).
            var existing = Environment.GetEnvironmentVariable("PSModulePath") ?? string.Empty;
            var stagedModules = Path.Combine(smaDirectory, "Modules");
            var segments = existing.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!segments.Contains(stagedModules, StringComparer.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable(
                    "PSModulePath",
                    string.IsNullOrEmpty(existing)
                        ? stagedModules
                        : $"{stagedModules}{Path.PathSeparator}{existing}");
            }
        }
        catch
        {
            // Bootstrap must never break the test run; missing built-ins surface through the
            // engine output where the real cause stays visible.
        }
    }

    private static void CopyIfExists(string source, string destination)
    {
        try
        {
            if (File.Exists(source) && !File.Exists(destination))
            {
                File.Copy(source, destination, overwrite: false);
            }
        }
        catch
        {
            // A missing copy degrades command coverage but does not corrupt the run.
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (!File.Exists(target))
            {
                CopyIfExists(file, target);
            }
        }
    }

    /// <summary>Locates the installed pwsh's $PSHOME via a one-shot subprocess probe.</summary>
    private static string? _cachedPowerShellHome;

    /// <summary>
    ///     Locates an installed PowerShell 7 home directory without spawning a process.
    ///     Covers MSI installs, the PATH, and the Microsoft Store package layout.
    /// </summary>
    private static string? ResolvePowerShellHome()
    {
        if (_cachedPowerShellHome is not null)
        {
            return _cachedPowerShellHome;
        }

        foreach (var candidate in EnumeratePowerShellHomes())
        {
            if (File.Exists(Path.Combine(candidate, "pwsh.exe"))
                && Directory.Exists(Path.Combine(candidate, "Modules")))
            {
                _cachedPowerShellHome = candidate;
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumeratePowerShellHomes()
    {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PowerShell",
            "7");

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var segment in path.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (File.Exists(Path.Combine(segment, "pwsh.exe")))
            {
                yield return segment;
            }
        }

        var windowsApps = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "WindowsApps");
        if (Directory.Exists(windowsApps))
        {
            string[] packageDirectories;
            try
            {
                packageDirectories = Directory.GetDirectories(
                    windowsApps,
                    "Microsoft.PowerShell_*",
                    SearchOption.TopDirectoryOnly);
            }
            catch
            {
                packageDirectories = [];
            }

            foreach (var packageDirectory in packageDirectories.Reverse())
            {
                yield return packageDirectory;
            }
        }
    }
}
