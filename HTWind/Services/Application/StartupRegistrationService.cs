using System.Diagnostics;
using System.Reflection;

using Microsoft.Win32;

namespace HTWind.Services.Application;

public sealed class StartupRegistrationService : IStartupRegistrationService
{
    private const string RunRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "HTWind";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, false);
            var startupCommand = key?.GetValue(AppName) as string;
            return !string.IsNullOrWhiteSpace(startupCommand);
        }
        catch
        {
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true);
                if (key is null)
                {
                    return;
                }

                var startupCommand = BuildExecutablePathValue();
                if (string.IsNullOrWhiteSpace(startupCommand))
                {
                    return;
                }

                key.SetValue(AppName, startupCommand);
                return;
            }

            DeleteRunValue(Registry.CurrentUser);
            DeleteRunValue(Registry.LocalMachine);
        }
        catch
        {
        }
    }

    private static void DeleteRunValue(RegistryKey root)
    {
        try
        {
            using var key = root.OpenSubKey(RunRegistryPath, true);
            key?.DeleteValue(AppName, false);
        }
        catch
        {
        }
    }

    private static string BuildExecutablePathValue()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            return $"\"{processPath}\"";
        }

        var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrWhiteSpace(entryAssemblyPath))
        {
            return BuildLaunchCommand(entryAssemblyPath);
        }

        using var process = Process.GetCurrentProcess();
        var modulePath = process.MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(modulePath))
        {
            return BuildLaunchCommand(modulePath);
        }

        return string.Empty;
    }

    private static string BuildLaunchCommand(string path)
    {
        if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return $"dotnet \"{path}\"";
        }

        return $"\"{path}\"";
    }
}
