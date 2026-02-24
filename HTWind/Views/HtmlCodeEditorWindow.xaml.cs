using System.IO;
using System.Windows;

using HTWind.Localization;
using HTWind.Services;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

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
    private readonly IHtmlEditorService _htmlEditorService;
    private readonly bool _isInitialHotReloadEnabled;
    private readonly WidgetModel _model;
    private bool _isHotReloadEnabled;

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
        EditorWebView.CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = GetWebViewUserDataFolder()
        };
        FilePathText.Text = _model.FilePath;
        Loaded += HtmlCodeEditorWindow_Loaded;
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

        await _htmlEditorService.InitializeEditorAsync(
            EditorWebView,
            filePath,
            CoreWebView2_WebMessageReceived
        );

        if (_isInitialHotReloadEnabled)
        {
            HotReloadToggle.IsChecked = true;
        }
    }

    private void CoreWebView2_WebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e
    )
    {
        if (!_isHotReloadEnabled)
        {
            return;
        }

        if (_htmlEditorService.TryReadContentChangedPayload(e.WebMessageAsJson, out var htmlContent))
        {
            LivePreviewChanged?.Invoke(this, new LivePreviewChangedEventArgs(htmlContent));
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_model.FilePath))
        {
            return;
        }

        try
        {
            await _htmlEditorService.SaveEditorContentAsync(EditorWebView, _model.FilePath);

            ContentSaved?.Invoke(this, EventArgs.Empty);
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
        var content = await _htmlEditorService.GetEditorContentAsync(EditorWebView);
        LivePreviewChanged?.Invoke(this, new LivePreviewChangedEventArgs(content));
    }

    protected override void OnClosed(EventArgs e)
    {
        if (EditorWebView.CoreWebView2 is not null)
        {
            EditorWebView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
        }

        LivePreviewChanged?.Invoke(this, new LivePreviewChangedEventArgs(null));
        base.OnClosed(e);
    }

    private static string GetWebViewUserDataFolder()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HTWind",
            "WebView2",
            "Editor"
        );

        Directory.CreateDirectory(path);
        return path;
    }
}
