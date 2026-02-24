using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;

using HTWind.Localization;

using Wpf.Ui.Controls;

using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace HTWind;

public partial class AboutWindow : FluentWindow
{
    private const string RepositoryUrl = "https://github.com/sametcn99/HTWind";
    private const string DeveloperSiteUrl = "https://sametcc.me";
    private const string IssuesUrl = "https://github.com/sametcn99/HTWind/issues";
    private const string DiscussionsUrl = "https://github.com/sametcn99/HTWind/discussions";
    private const string ReleasesUrl = "https://github.com/sametcn99/HTWind/releases";
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/sametcn99/HTWind/releases/latest";
    private static readonly HttpClient UpdateHttpClient = CreateUpdateHttpClient();
    private string _latestReleaseUrl = ReleasesUrl;

    public AboutWindow()
    {
        InitializeComponent();

        var assemblyName = Assembly.GetExecutingAssembly().GetName();
        var version = GetCurrentVersionDisplay();

        AppNameText.Text = assemblyName.Name ?? "HTWind";
        VersionText.Text = version;
        RepositoryText.Text = RepositoryUrl;
        DeveloperSiteText.Text = DeveloperSiteUrl;
        IssuesText.Text = IssuesUrl;
        DiscussionsText.Text = DiscussionsUrl;
    }

    private static HttpClient CreateUpdateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HTWind/UpdateChecker");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static string GetCurrentVersionDisplay()
    {
        var infoVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            var plusIndex = infoVersion.IndexOf('+');
            return plusIndex >= 0 ? infoVersion[..plusIndex] : infoVersion;
        }

        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "-";
    }

    private static Version? TryParseVersion(string? rawVersion)
    {
        if (string.IsNullOrWhiteSpace(rawVersion))
        {
            return null;
        }

        var normalized = rawVersion.Trim();

        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        var plusIndex = normalized.IndexOf('+');
        if (plusIndex >= 0)
        {
            normalized = normalized[..plusIndex];
        }

        var dashIndex = normalized.IndexOf('-');
        if (dashIndex >= 0)
        {
            normalized = normalized[..dashIndex];
        }

        return Version.TryParse(normalized, out var parsed) ? parsed : null;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            MessageBox.Show(
                LocalizationService.Get("AboutWindow_OpenUrlError"),
                LocalizationService.Get("AboutWindow_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void OpenRepository_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(RepositoryUrl);
    }

    private void OpenDeveloperSite_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(DeveloperSiteUrl);
    }

    private void OpenIssues_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(IssuesUrl);
    }

    private void OpenDiscussions_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(DiscussionsUrl);
    }

    private void OpenLatestRelease_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(_latestReleaseUrl);
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatusText.Text = LocalizationService.Get("AboutWindow_UpdateChecking");

        try
        {
            using var response = await UpdateHttpClient.GetAsync(LatestReleaseApiUrl);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);

            var root = json.RootElement;
            var tagName = root.TryGetProperty("tag_name", out var tagElement)
                ? tagElement.GetString()
                : null;

            var htmlUrl = root.TryGetProperty("html_url", out var htmlElement)
                ? htmlElement.GetString()
                : null;

            if (!string.IsNullOrWhiteSpace(htmlUrl))
            {
                _latestReleaseUrl = htmlUrl;
            }

            var currentVersion = TryParseVersion(VersionText.Text);
            var latestVersion = TryParseVersion(tagName);

            if (currentVersion is not null && latestVersion is not null)
            {
                UpdateStatusText.Text = latestVersion > currentVersion
                    ? string.Format(LocalizationService.Get("AboutWindow_UpdateAvailable"), tagName)
                    : LocalizationService.Get("AboutWindow_UpToDate");
                return;
            }

            if (!string.IsNullOrWhiteSpace(tagName))
            {
                UpdateStatusText.Text = string.Format(LocalizationService.Get("AboutWindow_LatestReleaseTag"), tagName);
                return;
            }

            UpdateStatusText.Text = LocalizationService.Get("AboutWindow_UpdateUnknown");
        }
        catch
        {
            UpdateStatusText.Text = LocalizationService.Get("AboutWindow_UpdateCheckFailed");
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
