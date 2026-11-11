using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;

using HTWind.Localization;
using HTWind.Services.Widget;

namespace HTWind.Views.Pages;

/// <summary>
///     In-app community gallery: installs widgets from a URL (.htwind/.zip archive or a single
///     HTML file) or from a local archive/HTML file, replacing the old external "Find More" link.
/// </summary>
public partial class GalleryPage : UserControl
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    private static readonly string[] ArchiveExtensions = [".htwind", ".zip"];
    private static readonly string[] HtmlExtensions = [".html", ".htm"];

    private readonly IWidgetManager _widgetManager;
    private bool _isInstalling;

    public GalleryPage(IWidgetManager widgetManager)
    {
        ArgumentNullException.ThrowIfNull(widgetManager);
        _widgetManager = widgetManager;

        InitializeComponent();
    }

    private async void InstallFromUrl_Click(object sender, RoutedEventArgs e)
    {
        if (_isInstalling)
        {
            return;
        }

        var sourceUrl = GalleryUrlTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(sourceUrl)
            || !Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            SetStatus(LocalizationService.Get("Gallery_InvalidUrl"), isError: true);
            return;
        }

        _isInstalling = true;
        InstallFromUrlButton.IsEnabled = false;

        try
        {
            SetStatus(LocalizationService.Get("Gallery_Downloading"), isError: false);

            var tempFilePath = await DownloadToTempFileAsync(uri);
            try
            {
                var widgetName = InstallWidgetSource(tempFilePath, suggestName: Path.GetFileName(uri.LocalPath));
                SetStatus(
                    string.Format(LocalizationService.Get("Gallery_InstallSuccess"), widgetName),
                    isError: false);
                GalleryUrlTextBox.Text = string.Empty;
            }
            finally
            {
                TryDelete(tempFilePath);
            }
        }
        catch (Exception exception)
        {
            SetStatus(
                string.Format(LocalizationService.Get("Gallery_InstallFailed"), exception.Message),
                isError: true);
        }
        finally
        {
            _isInstalling = false;
            InstallFromUrlButton.IsEnabled = true;
        }
    }

    private async void InstallFromFile_Click(object sender, RoutedEventArgs e)
    {
        if (_isInstalling)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = LocalizationService.Get("Gallery_InstallFile"),
            Filter = "HTWind widget (*.htwind;*.zip;*.html)|*.htwind;*.zip;*.html|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FileName))
        {
            return;
        }

        _isInstalling = true;
        InstallFromFileButton.IsEnabled = false;

        try
        {
            SetStatus(LocalizationService.Get("Gallery_Installing"), isError: false);
            var widgetName = await Task.Run(() => InstallWidgetSource(dialog.FileName, suggestName: Path.GetFileName(dialog.FileName)));
            SetStatus(
                string.Format(LocalizationService.Get("Gallery_InstallSuccess"), widgetName),
                isError: false);
        }
        catch (Exception exception)
        {
            SetStatus(
                string.Format(LocalizationService.Get("Gallery_InstallFailed"), exception.Message),
                isError: true);
        }
        finally
        {
            _isInstalling = false;
            InstallFromFileButton.IsEnabled = true;
        }
    }

    /// <summary>
    ///     Installs a widget from an archive (.htwind/.zip: extracted and imported via its
    ///     manifest) or a single HTML file. Returns the installed widget's display name.
    /// </summary>
    private string InstallWidgetSource(string sourcePath, string suggestName)
    {
        var extension = Path.GetExtension(sourcePath);

        if (ArchiveExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return InstallArchive(sourcePath, suggestName);
        }

        if (HtmlExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) && File.Exists(sourcePath))
        {
            _widgetManager.AddWidget(sourcePath, isVisible: false);
            return Path.GetFileNameWithoutExtension(sourcePath);
        }

        throw new InvalidOperationException(LocalizationService.Get("Gallery_UnsupportedFile"));
    }

    private string InstallArchive(string archivePath, string suggestName)
    {
        var extractionRoot = Path.Combine(
            Path.GetTempPath(),
            "HTWind",
            "gallery",
            $"install-{Guid.NewGuid():N}");
        Directory.CreateDirectory(extractionRoot);

        try
        {
            ZipFile.ExtractToDirectory(archivePath, extractionRoot, overwriteFiles: true);

            // Prefer the deepest widget manifest inside the archive.
            var manifestPath = Directory
                .EnumerateFiles(extractionRoot, "htwind.widget.json", SearchOption.AllDirectories)
                .OrderBy(path => path.Count(character => character is '/' or '\\'))
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(manifestPath))
            {
                _widgetManager.AddWidgets([manifestPath], isVisible: false);
                var manifestName = Path.GetFileName(Path.GetDirectoryName(manifestPath));
                return string.IsNullOrWhiteSpace(manifestName) ? suggestName : manifestName;
            }

            var htmlPath = Directory
                .EnumerateFiles(extractionRoot, "*.html", SearchOption.AllDirectories)
                .OrderBy(path => path.Count(character => character is '/' or '\\'))
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(htmlPath))
            {
                _widgetManager.AddWidget(htmlPath, isVisible: false);
                return Path.GetFileNameWithoutExtension(htmlPath);
            }

            throw new InvalidOperationException(LocalizationService.Get("Gallery_ArchiveEmpty"));
        }
        finally
        {
            try
            {
                Directory.Delete(extractionRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static async Task<string> DownloadToTempFileAsync(Uri uri)
    {
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "HTWind", "gallery"));

        var fileName = Path.GetFileName(uri.LocalPath);
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            fileName = "widget-download.htwind";
        }

        var tempFilePath = Path.Combine(
            Path.GetTempPath(),
            "HTWind",
            "gallery",
            $"download-{Guid.NewGuid():N}-{SanitizeFileName(fileName)}");

        using var response = await HttpClient.GetAsync(uri);
        response.EnsureSuccessStatusCode();
        await using var contentStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = File.Create(tempFilePath);
        await contentStream.CopyToAsync(fileStream);

        return tempFilePath;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "widget" : sanitized;
    }

    private void SetStatus(string message, bool isError)
    {
        GalleryStatusText.Text = message;
        GalleryStatusText.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
        GalleryStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            isError ? System.Windows.Media.Colors.IndianRed : System.Windows.Media.Colors.Gray);
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
        }
    }
}
