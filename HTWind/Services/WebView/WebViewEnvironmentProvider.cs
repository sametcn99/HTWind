using System.IO;

using Microsoft.Web.WebView2.Core;

namespace HTWind.Services.WebView;

public sealed class WebViewEnvironmentProvider : IWebViewEnvironmentProvider
{
    private readonly Dictionary<string, EnvironmentEntry> _environments =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot = new();

    public Task<CoreWebView2Environment> GetWidgetsEnvironmentAsync()
    {
        return GetEnvironmentAsync("Widgets");
    }

    public Task<CoreWebView2Environment> GetEditorEnvironmentAsync()
    {
        return GetEnvironmentAsync("Editor");
    }

    public void ReleaseWidgetsEnvironment()
    {
        ReleaseEnvironment("Widgets");
    }

    public void ReleaseEditorEnvironment()
    {
        ReleaseEnvironment("Editor");
    }

    private Task<CoreWebView2Environment> GetEnvironmentAsync(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        lock (_syncRoot)
        {
            if (!_environments.TryGetValue(profileName, out var entry))
            {
                entry = new EnvironmentEntry(CreateEnvironmentWithRetryOnFailureAsync(profileName));
                _environments[profileName] = entry;
            }

            entry.ReferenceCount++;
            return entry.EnvironmentTask;
        }
    }

    private void ReleaseEnvironment(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        lock (_syncRoot)
        {
            if (!_environments.TryGetValue(profileName, out var entry))
            {
                return;
            }

            if (entry.ReferenceCount > 0)
            {
                entry.ReferenceCount--;
            }

            if (entry.ReferenceCount == 0)
            {
                _environments.Remove(profileName);
            }
        }
    }

    private async Task<CoreWebView2Environment> CreateEnvironmentWithRetryOnFailureAsync(
        string profileName
    )
    {
        try
        {
            var userDataFolder = BuildUserDataFolder(profileName);
            var options = new CoreWebView2EnvironmentOptions(
                additionalBrowserArguments: string.Join(' ', new[]
                {
                    "--disable-extensions",
                    "--disable-component-extensions-with-background-pages",
                    "--enable-low-end-device-mode"
                })
            );

            return await CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: userDataFolder, options: options);
        }
        catch
        {
            lock (_syncRoot)
            {
                _environments.Remove(profileName);
            }

            throw;
        }
    }

    private sealed class EnvironmentEntry(Task<CoreWebView2Environment> environmentTask)
    {
        public Task<CoreWebView2Environment> EnvironmentTask { get; } = environmentTask;

        public int ReferenceCount { get; set; }
    }

    private static string BuildUserDataFolder(string profileName)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HTWind",
            "WebView2",
            profileName
        );

        Directory.CreateDirectory(path);
        return path;
    }
}
