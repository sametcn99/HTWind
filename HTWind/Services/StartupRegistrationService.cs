using System.Reflection;

using Microsoft.Win32;

namespace HTWind.Services;

public sealed class StartupRegistrationService : IStartupRegistrationService
{
    private const string RunRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "HTWind";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, false);
            return key?.GetValue(AppName) is not null;
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
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                key.SetValue(AppName, BuildExecutablePathValue());
                return;
            }

            key.DeleteValue(AppName, false);
        }
        catch
        {
            // Startup registration should not crash the main UI flow.
        }
    }

    private static string BuildExecutablePathValue()
    {
        var path = Assembly.GetExecutingAssembly().Location;
        if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            path = string.Concat(path.AsSpan(0, path.Length - 4), ".exe");
        }

        return $"\"{path}\"";
    }
}
