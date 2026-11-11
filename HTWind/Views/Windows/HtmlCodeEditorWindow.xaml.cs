using System.IO;
using System.Windows;
using System.Windows.Controls;

using HTWind.Localization;
using HTWind.Model;
using HTWind.Services.Editor;
using HTWind.Services.Widget;

using Microsoft.Web.WebView2.Core;

using Wpf.Ui.Appearance;

using Wpf.Ui.Controls;

using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace HTWind;

public sealed class LivePreviewChangedEventArgs : EventArgs
{
    public LivePreviewChangedEventArgs(string? htmlContent)
    {
        HtmlContent = htmlContent;
    }

    public string? HtmlContent { get; }
}

public partial class HtmlCodeEditorWindow : FluentWindow
{
    private static readonly string[] EditableExtensions =
    [
        ".html", ".htm", ".css", ".js", ".mjs", ".ts", ".json", ".txt", ".md"
    ];

    private readonly IHtmlEditorService _htmlEditorService;
    private readonly bool _isInitialHotReloadEnabled;
    private readonly WidgetModel _model;
    private readonly Dictionary<string, string> _documentPathsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<EditorDocumentState> _documentStates = [];
    private bool _hasEditorEnvironmentLease;
    private bool _isHotReloadEnabled;
    private bool _isEditorInitialized;
    private bool _isSwitchingDocument;
    private EventHandler<string>? _consoleHandler;

    public HtmlCodeEditorWindow(
        WidgetModel model,
        IHtmlEditorService htmlEditorService,
        bool isInitialHotReloadEnabled = false
    )
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _htmlEditorService =
            htmlEditorService ?? throw new ArgumentNullException(nameof(htmlEditorService));
        _isInitialHotReloadEnabled = isInitialHotReloadEnabled;
        InitializeComponent();
        Loaded += HtmlCodeEditorWindow_Loaded;
        Closed += HtmlCodeEditorWindow_Closed;
    }

    public event EventHandler? ContentSaved;
    public event EventHandler<LivePreviewChangedEventArgs>? LivePreviewChanged;

    private async void HtmlCodeEditorWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= HtmlCodeEditorWindow_Loaded;

        if (string.IsNullOrWhiteSpace(_model.FilePath) || !File.Exists(_model.FilePath))
        {
            MessageBox.Show(
                LocalizationService.Get("EditorWindow_LoadError"),
                LocalizationService.Get("EditorWindow_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            Close();
            return;
        }

        var filePath = _model.FilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            Close();
            return;
        }

        try
        {
            _hasEditorEnvironmentLease = true;
            await _htmlEditorService.InitializeEditorAsync(
                EditorWebView,
                filePath,
                CoreWebView2_WebMessageReceived
            );
            _isEditorInitialized = true;
        }
        catch
        {
            MessageBox.Show(
                LocalizationService.Get("EditorWindow_LoadError"),
                LocalizationService.Get("EditorWindow_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            Close();
            return;
        }

        BuildDocumentTabs(filePath);
        await ApplyEditorThemeAsync();

        if (_isInitialHotReloadEnabled)
        {
            HotReloadToggle.IsChecked = true;
        }
    }

    private void HtmlCodeEditorWindow_Closed(object? sender, EventArgs e)
    {
        if (_consoleHandler is not null)
        {
            WidgetRuntimeConsole.Unsubscribe(_model.Id, _consoleHandler);
            WidgetRuntimeConsole.Clear(_model.Id);
        }

        LivePreviewChanged?.Invoke(this, new LivePreviewChangedEventArgs(null));
    }

    /// <summary>
    ///     Collects the editable files of the widget: for .htwind packages the entry file plus
    ///     declared text assets (multi-file tabbed editing); otherwise just the entry file.
    /// </summary>
    private void BuildDocumentTabs(string entryFilePath)
    {
        _documentPathsByName.Clear();

        var entryName = Path.GetFileName(entryFilePath);
        _documentPathsByName[entryName] = Path.GetFullPath(entryFilePath);

        var widgetRootPath = Path.GetDirectoryName(Path.GetFullPath(entryFilePath));
        if (!string.IsNullOrWhiteSpace(widgetRootPath))
        {
            try
            {
                var manifestJsonOptions = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };
                var manifest = WidgetManifestHelper.LoadManifestIfPresent(widgetRootPath, manifestJsonOptions);
                if (manifest is not null)
                {
                    var normalizedEntryRelative = WidgetManifestHelper.NormalizeRelativePath(
                        Path.GetRelativePath(widgetRootPath, Path.GetFullPath(entryFilePath)));
                    var widgetItem = manifest.Widgets.FirstOrDefault(item =>
                        string.Equals(
                            WidgetManifestHelper.NormalizeRelativePath(item.EntryFile),
                            normalizedEntryRelative,
                            StringComparison.OrdinalIgnoreCase))
                        ?? (manifest.Widgets.Count == 1 ? manifest.Widgets[0] : null);

                    if (widgetItem is not null)
                    {
                        foreach (var asset in widgetItem.Assets)
                        {
                            try
                            {
                                var assetPath = WidgetManifestHelper.ResolveFileInsideRoot(widgetRootPath, asset);
                                var extension = Path.GetExtension(assetPath);
                                if (File.Exists(assetPath) && EditableExtensions.Contains(extension))
                                {
                                    _documentPathsByName[Path.GetFileName(assetPath)] = assetPath;
                                }
                            }
                            catch
                            {
                                // Invalid asset paths are ignored.
                            }
                        }
                    }
                }
            }
            catch
            {
                // Manifest problems fall back to single-file editing.
            }
        }

        foreach (var documentName in _documentPathsByName.Keys)
        {
            var tab = new TabItem
            {
                Header = documentName,
                Tag = documentName
            };
            FileTabs.Items.Add(tab);
        }

        SelectDocumentTab(entryName);
    }

    private void SelectDocumentTab(string documentName)
    {
        foreach (TabItem tab in FileTabs.Items)
        {
            if (string.Equals(tab.Tag as string, documentName, StringComparison.OrdinalIgnoreCase))
            {
                FileTabs.SelectedItem = tab;
                return;
            }
        }

        if (FileTabs.Items.Count > 0)
        {
            FileTabs.SelectedIndex = 0;
        }
    }

    private async void FileTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isEditorInitialized || _isSwitchingDocument)
        {
            return;
        }

        if (FileTabs.SelectedItem is not TabItem selectedTab || selectedTab.Tag is not string documentName)
        {
            return;
        }

        if (!_documentPathsByName.TryGetValue(documentName, out var documentPath))
        {
            return;
        }

        _isSwitchingDocument = true;
        try
        {
            await _htmlEditorService.SetEditorDocumentAsync(EditorWebView, documentPath);
            Title = $"{LocalizationService.Get("EditorWindow_Title")} - {documentName}";
        }
        catch
        {
            // Tab switching failures leave the previous document active.
        }
        finally
        {
            _isSwitchingDocument = false;
        }
    }

    private void CoreWebView2_WebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e
    )
    {
        if (_htmlEditorService.TryReadContentChangedDocumentName(e.WebMessageAsJson, out var documentName)
            && _htmlEditorService.TryReadContentChangedPayload(e.WebMessageAsJson, out var htmlContent))
        {
            MarkDocumentDirty(documentName);

            var entryName = Path.GetFileName(_model.FilePath);
            if (_isHotReloadEnabled
                && (string.IsNullOrEmpty(documentName) || string.Equals(documentName, entryName, StringComparison.OrdinalIgnoreCase)))
            {
                LivePreviewChanged?.Invoke(this, new LivePreviewChangedEventArgs(htmlContent));
            }
        }
    }

    private void MarkDocumentDirty(string? documentName)
    {
        var entryName = Path.GetFileName(_model.FilePath) ?? string.Empty;
        foreach (var state in _documentStates)
        {
            var matches = string.IsNullOrEmpty(documentName)
                ? string.Equals(state.Name, entryName, StringComparison.OrdinalIgnoreCase)
                : string.Equals(state.Name, documentName, StringComparison.OrdinalIgnoreCase);
            if (matches)
            {
                _documentStates.Remove(state);
                break;
            }
        }

        var activeName = string.IsNullOrEmpty(documentName) ? entryName : documentName!;
        _documentStates.Add(new EditorDocumentState
        {
            Name = activeName,
            Language = HtmlEditorService.GetLanguageForFile(activeName),
            IsDirty = true
        });
        RefreshTabHeaders();
    }

    private void RefreshTabHeaders()
    {
        foreach (TabItem tab in FileTabs.Items)
        {
            if (tab.Tag is not string documentName)
            {
                continue;
            }

            var isDirty = _documentStates.Any(state =>
                string.Equals(state.Name, documentName, StringComparison.OrdinalIgnoreCase) && state.IsDirty);
            tab.Header = isDirty ? documentName + " *" : documentName;
        }
    }

    private string? GetActiveDocumentPath()
    {
        if (FileTabs.SelectedItem is not TabItem selectedTab || selectedTab.Tag is not string documentName)
        {
            return _model.FilePath;
        }

        return _documentPathsByName.TryGetValue(documentName, out var path) ? path : _model.FilePath;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var documentPath = GetActiveDocumentPath();
        if (string.IsNullOrWhiteSpace(documentPath) || !File.Exists(documentPath))
        {
            return;
        }

        try
        {
            await _htmlEditorService.SaveEditorContentAsync(EditorWebView, documentPath);

            ContentSaved?.Invoke(this, EventArgs.Empty);
            OnDocumentSaved(documentPath);
        }
        catch (Exception)
        {
            MessageBox.Show(
                LocalizationService.Get("EditorWindow_SaveError"),
                LocalizationService.Get("EditorWindow_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private async void SaveAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var savedAny = false;

            foreach (var state in _documentStates.Where(state => state.IsDirty).ToList())
            {
                if (!_documentPathsByName.TryGetValue(state.Name, out var documentPath) || !File.Exists(documentPath))
                {
                    continue;
                }

                var content = await _htmlEditorService.GetEditorDocumentContentAsync(EditorWebView, state.Name);
                await File.WriteAllTextAsync(documentPath, content, System.Text.Encoding.UTF8);
                await _htmlEditorService.MarkEditorDocumentSavedAsync(EditorWebView, state.Name);
                savedAny = true;
            }

            if (!savedAny)
            {
                var documentPath = GetActiveDocumentPath();
                if (!string.IsNullOrWhiteSpace(documentPath) && File.Exists(documentPath))
                {
                    await _htmlEditorService.SaveEditorContentAsync(EditorWebView, documentPath);
                    OnDocumentSaved(documentPath);
                }
            }

            ContentSaved?.Invoke(this, EventArgs.Empty);
            RefreshTabHeaders();
        }
        catch (Exception)
        {
            MessageBox.Show(
                LocalizationService.Get("EditorWindow_SaveError"),
                LocalizationService.Get("EditorWindow_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void OnDocumentSaved(string documentPath)
    {
        var documentName = Path.GetFileName(documentPath);
        _documentStates.RemoveAll(state => string.Equals(state.Name, documentName, StringComparison.OrdinalIgnoreCase));
        RefreshTabHeaders();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void HotReload_Changed(object sender, RoutedEventArgs e)
    {
        _isHotReloadEnabled = sender is ToggleSwitch toggle && toggle.IsChecked == true;

        if (_isHotReloadEnabled)
        {
            await PushCurrentContentToLivePreviewAsync();
            return;
        }

        LivePreviewChanged?.Invoke(this, new LivePreviewChangedEventArgs(null));
    }

    private async Task PushCurrentContentToLivePreviewAsync()
    {
        // Live preview always renders the widget entry file, regardless of the active editor tab.
        var entryName = Path.GetFileName(_model.FilePath) ?? string.Empty;
        var content = await _htmlEditorService.GetEditorDocumentContentAsync(EditorWebView, entryName);
        LivePreviewChanged?.Invoke(this, new LivePreviewChangedEventArgs(content));
    }

    private void ConsoleToggle_Click(object sender, RoutedEventArgs e)
    {
        if (ConsolePanel.Visibility == Visibility.Visible)
        {
            ConsolePanel.Visibility = Visibility.Collapsed;
            return;
        }

        ConsolePanel.Visibility = Visibility.Visible;
        AttachConsoleListener();
    }

    private void AttachConsoleListener()
    {
        if (_consoleHandler is not null)
        {
            return;
        }

        foreach (var entry in WidgetRuntimeConsole.GetEntries(_model.Id))
        {
            ConsoleList.Items.Add(entry);
        }

        if (ConsoleList.Items.Count > 0)
        {
            ConsoleList.ScrollIntoView(ConsoleList.Items[^1]);
        }

        _consoleHandler = (_, entry) =>
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ConsoleList.Items.Add(entry);
                if (ConsoleList.Items.Count > 200)
                {
                    ConsoleList.Items.RemoveAt(0);
                }

                ConsoleList.ScrollIntoView(ConsoleList.Items[^1]);
            }));
        };

        WidgetRuntimeConsole.Subscribe(_model.Id, _consoleHandler);
    }

    private void ConsoleClear_Click(object sender, RoutedEventArgs e)
    {
        ConsoleList.Items.Clear();
        WidgetRuntimeConsole.Clear(_model.Id);
    }

    private async Task ApplyEditorThemeAsync()
    {
        var monacoTheme = IsLightApplicationTheme() ? "vs" : "vs-dark";
        await EditorWebView.ExecuteScriptAsync(
            "(function(){"
            + "const monacoTheme='" + monacoTheme + "';"
            + "if(window.monaco&&window.monaco.editor&&window.monaco.editor.setTheme){window.monaco.editor.setTheme(monacoTheme);}"
            + "const fallback=document.getElementById('fallback');"
            + "if(fallback){"
            + "const light=monacoTheme==='vs';"
            + "fallback.style.background=light?'#ffffff':'#1e1e1e';"
            + "fallback.style.color=light?'#1f2328':'#d4d4d4';"
            + "}"
            + "})();"
        );
    }

    private static bool IsLightApplicationTheme()
    {
        var appTheme = ApplicationThemeManager.GetAppTheme();
        return appTheme == ApplicationTheme.Light;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (EditorWebView.CoreWebView2 is not null)
        {
            EditorWebView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
        }

        EditorWebView.Dispose();

        if (_hasEditorEnvironmentLease)
        {
            _htmlEditorService.ReleaseEditorEnvironment();
            _hasEditorEnvironmentLease = false;
        }

        base.OnClosed(e);
    }
}
