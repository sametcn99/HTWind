using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using HTWind.Services;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace HTWind;

public partial class WidgetWindow : Window
{
    private const double MinWidgetWidth = 160;
    private const double MinWidgetHeight = 100;
    private readonly IWidgetHostApiService _widgetHostApiService;
    private static readonly JsonSerializerOptions HostBridgeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private Point _dragStartScreen;
    private Point _dragStartWindow;

    private bool _isDragging;
    private Task? _initializationTask;
    private bool _isSuspended;
    private bool _isWebViewReady;

    private bool _isResizing;
    private string? _pendingLivePreviewHtml;
    private CornerResizeMode _resizeMode;
    private Point _resizeStartScreen;
    private Size _resizeStartSize;
    private Point _resizeStartWindow;

    public WidgetWindow(WidgetModel model, IWidgetHostApiService widgetHostApiService)
    {
        InitializeComponent();
        webView.CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = GetWebViewUserDataFolder()
        };
        Model = model;
        _widgetHostApiService =
            widgetHostApiService ?? throw new ArgumentNullException(nameof(widgetHostApiService));
        Model.PropertyChanged += Model_PropertyChanged;

        UpdateOverlay();
        SetRuntimeVisibility(Model.IsVisible);
    }

    public WidgetModel Model { get; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WidgetModel.IsLocked))
        {
            UpdateOverlay();
        }

        if (e.PropertyName == nameof(WidgetModel.IsPinned))
        {
            Topmost = Model.IsPinned;
        }

        if (e.PropertyName == nameof(WidgetModel.IsVisible))
        {
            SetRuntimeVisibility(Model.IsVisible);
        }
    }

    private void UpdateOverlay()
    {
        if (Model.IsLocked)
        {
            DragOverlay.Visibility = Visibility.Collapsed;
            ResizeHandles.Visibility = Visibility.Collapsed;

            // Locked mode should show only HTML content without widget chrome.
            WidgetChrome.Background = Brushes.Transparent;
            WidgetChrome.CornerRadius = new CornerRadius(0);
            WebViewHost.Margin = new Thickness(0);
        }
        else
        {
            DragOverlay.Visibility = Visibility.Visible;
            ResizeHandles.Visibility = Visibility.Visible;

            WidgetChrome.Background = new SolidColorBrush(
                Color.FromArgb(176, 26, 26, 26)
            );
            WidgetChrome.CornerRadius = new CornerRadius(10);
            WebViewHost.Margin = new Thickness(8, 28, 8, 8);
        }

        if (Model.IsPinned != Topmost)
        {
            Topmost = Model.IsPinned;
        }
    }

    private async Task EnsureInitializedAsync()
    {
        _initializationTask ??= InitializeCoreAsync();
        await _initializationTask;
    }

    private async Task InitializeCoreAsync()
    {
        if (_isWebViewReady)
        {
            return;
        }

        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: GetWebViewUserDataFolder());
        await webView.EnsureCoreWebView2Async(env);
        await RegisterHostBridgeAsync();
        _isWebViewReady = true;

        if (Model.IsVisible)
        {
            NavigateCurrentContent();
        }
        else
        {
            webView.CoreWebView2.Navigate("about:blank");
            _isSuspended = true;
        }
    }

    public void SetRuntimeVisibility(bool isVisible)
    {
        if (isVisible)
        {
            Show();
            _ = ResumeExecutionAsync();
            return;
        }

        _ = SuspendExecutionAsync();
        Hide();
    }

    public void RefreshContent()
    {
        _pendingLivePreviewHtml = null;

        if (!_isWebViewReady || !Model.IsVisible)
        {
            return;
        }

        NavigateCurrentContent();
    }

    public void ApplyLivePreviewContent(string htmlContent)
    {
        _pendingLivePreviewHtml = htmlContent;

        if (_isWebViewReady && Model.IsVisible)
        {
            webView.NavigateToString(htmlContent);
        }
    }

    public void ClearLivePreview()
    {
        _pendingLivePreviewHtml = null;
        if (_isWebViewReady && Model.IsVisible)
        {
            NavigateCurrentContent();
        }
    }

    private async Task SuspendExecutionAsync()
    {
        if (!_isWebViewReady || webView.CoreWebView2 is null || _isSuspended)
        {
            return;
        }

        webView.Stop();
        webView.CoreWebView2.Navigate("about:blank");
        _isSuspended = true;
        await Task.CompletedTask;
    }

    private async Task ResumeExecutionAsync()
    {
        await EnsureInitializedAsync();
        if (!_isWebViewReady)
        {
            return;
        }

        if (!_isSuspended)
        {
            if (Model.IsVisible)
            {
                NavigateCurrentContent();
            }

            return;
        }

        if (Model.IsVisible)
        {
            NavigateCurrentContent();
            _isSuspended = false;
        }
    }

    private void NavigateCurrentContent()
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_pendingLivePreviewHtml))
        {
            webView.NavigateToString(_pendingLivePreviewHtml);
            return;
        }

        if (!string.IsNullOrEmpty(Model.FilePath))
        {
            webView.Source = new Uri(Model.FilePath);
        }
    }

    private void DragOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Model.IsLocked || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        _isDragging = true;
        _dragStartScreen = GetCursorScreenDip();
        _dragStartWindow = new Point(Left, Top);
        DragOverlay.CaptureMouse();
        e.Handled = true;
    }

    private void DragOverlay_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentScreen = GetCursorScreenDip();
        Left = _dragStartWindow.X + (currentScreen.X - _dragStartScreen.X);
        Top = _dragStartWindow.Y + (currentScreen.Y - _dragStartScreen.Y);
    }

    private void DragOverlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        DragOverlay.ReleaseMouseCapture();
    }

    private void TopLeftGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        StartResize(CornerResizeMode.TopLeft, e);
    }

    private void TopRightGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        StartResize(CornerResizeMode.TopRight, e);
    }

    private void BottomLeftGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        StartResize(CornerResizeMode.BottomLeft, e);
    }

    private void BottomRightGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        StartResize(CornerResizeMode.BottomRight, e);
    }

    private void StartResize(CornerResizeMode mode, MouseButtonEventArgs e)
    {
        if (Model.IsLocked || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        _isResizing = true;
        _resizeMode = mode;
        _resizeStartScreen = GetCursorScreenDip();
        _resizeStartWindow = new Point(Left, Top);
        _resizeStartSize = new Size(Width, Height);

        if (e.Source is UIElement sourceElement)
        {
            sourceElement.CaptureMouse();
        }

        e.Handled = true;
    }

    private void ResizeGrip_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isResizing || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentScreen = GetCursorScreenDip();
        var deltaX = currentScreen.X - _resizeStartScreen.X;
        var deltaY = currentScreen.Y - _resizeStartScreen.Y;

        ApplyResize(deltaX, deltaY);
    }

    private void ResizeGrip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isResizing)
        {
            return;
        }

        _isResizing = false;
        _resizeMode = CornerResizeMode.None;

        if (e.Source is UIElement sourceElement)
        {
            sourceElement.ReleaseMouseCapture();
        }
    }

    private void ApplyResize(double deltaX, double deltaY)
    {
        var nextLeft = _resizeStartWindow.X;
        var nextTop = _resizeStartWindow.Y;
        var nextWidth = _resizeStartSize.Width;
        var nextHeight = _resizeStartSize.Height;

        if (
            _resizeMode == CornerResizeMode.TopLeft
            || _resizeMode == CornerResizeMode.BottomLeft
        )
        {
            nextWidth = _resizeStartSize.Width - deltaX;
            nextLeft = _resizeStartWindow.X + deltaX;
        }

        if (
            _resizeMode == CornerResizeMode.TopRight
            || _resizeMode == CornerResizeMode.BottomRight
        )
        {
            nextWidth = _resizeStartSize.Width + deltaX;
        }

        if (_resizeMode == CornerResizeMode.TopLeft || _resizeMode == CornerResizeMode.TopRight)
        {
            nextHeight = _resizeStartSize.Height - deltaY;
            nextTop = _resizeStartWindow.Y + deltaY;
        }

        if (
            _resizeMode == CornerResizeMode.BottomLeft
            || _resizeMode == CornerResizeMode.BottomRight
        )
        {
            nextHeight = _resizeStartSize.Height + deltaY;
        }

        if (nextWidth < MinWidgetWidth)
        {
            nextWidth = MinWidgetWidth;
            if (
                _resizeMode == CornerResizeMode.TopLeft
                || _resizeMode == CornerResizeMode.BottomLeft
            )
            {
                nextLeft = _resizeStartWindow.X + (_resizeStartSize.Width - MinWidgetWidth);
            }
        }

        if (nextHeight < MinWidgetHeight)
        {
            nextHeight = MinWidgetHeight;
            if (
                _resizeMode == CornerResizeMode.TopLeft
                || _resizeMode == CornerResizeMode.TopRight
            )
            {
                nextTop = _resizeStartWindow.Y + (_resizeStartSize.Height - MinWidgetHeight);
            }
        }

        Left = nextLeft;
        Top = nextTop;
        Width = nextWidth;
        Height = nextHeight;
    }

    private Point GetCursorScreenDip()
    {
        if (!GetCursorPos(out var cursorPoint))
        {
            return new Point(Left, Top);
        }

        // Convert device pixels to WPF device-independent pixels to avoid DPI drift.
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
        {
            return new Point(cursorPoint.X, cursorPoint.Y);
        }

        return source.CompositionTarget.TransformFromDevice.Transform(
            new Point(cursorPoint.X, cursorPoint.Y)
        );
    }

    protected override void OnClosed(EventArgs e)
    {
        Model.PropertyChanged -= Model_PropertyChanged;
        if (webView.CoreWebView2 is not null)
        {
            webView.CoreWebView2.WebMessageReceived -= WebView_CoreWebView2_WebMessageReceived;
        }

        webView.Dispose();
        base.OnClosed(e);
    }

    private async Task RegisterHostBridgeAsync()
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        webView.CoreWebView2.WebMessageReceived -= WebView_CoreWebView2_WebMessageReceived;
        webView.CoreWebView2.WebMessageReceived += WebView_CoreWebView2_WebMessageReceived;
        await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(GetHostBridgeScript());
    }

    private async void WebView_CoreWebView2_WebMessageReceived(
            object? sender,
            CoreWebView2WebMessageReceivedEventArgs e
    )
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        WidgetHostApiRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<WidgetHostApiRequest>(
                e.WebMessageAsJson,
                HostBridgeJsonOptions
            );
        }
        catch
        {
            return;
        }

        if (
                request is null
                || !string.Equals(request.Type, "HTWind-api-request", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(request.RequestId)
                || string.IsNullOrWhiteSpace(request.Command)
        )
        {
            return;
        }

        var executionResult = await _widgetHostApiService.ExecuteAsync(request.Command, request.Args);

        var response = new WidgetHostApiResponse
        {
            RequestId = request.RequestId,
            Success = executionResult.Success,
            Result = executionResult.Result,
            Error = executionResult.Error
        };

        var json = JsonSerializer.Serialize(response, HostBridgeJsonOptions);
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        await webView.CoreWebView2.ExecuteScriptAsync(
                $"window.HTWind && window.HTWind.__resolveFromHost('{payloadBase64}');"
        );
    }

    private static string GetHostBridgeScript()
    {
        return """
                             (() => {
                                 if (window.HTWind && window.HTWind.__bridgeReady) {
                                     return;
                                 }

                                 const pending = new Map();

                                 function createRequestId() {
                                     try {
                                         if (window.crypto && typeof window.crypto.randomUUID === 'function') {
                                             return window.crypto.randomUUID();
                                         }
                                     } catch {
                                         // Ignore and use fallback id generation below.
                                     }

                                     return `HTWind-${Date.now()}-${Math.random().toString(16).slice(2, 12)}`;
                                 }

                                 function decodeBase64(base64) {
                                     try {
                                         const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
                                         return new TextDecoder().decode(bytes);
                                     } catch {
                                         return '';
                                     }
                                 }

                                 window.HTWind = {
                                     __bridgeReady: true,
                                     invoke(command, args) {
                                         if (!window.chrome || !window.chrome.webview || !window.chrome.webview.postMessage) {
                                             return Promise.reject(new Error('HTWind host bridge is not available.'));
                                         }

                                         if (command !== 'powershell.exec') {
                                             return Promise.reject(new Error('Only powershell.exec is supported.'));
                                         }

                                         const requestId = createRequestId();
                                         const payload = {
                                             type: 'HTWind-api-request',
                                             requestId,
                                             command,
                                             args: args || {}
                                         };

                                         return new Promise((resolve, reject) => {
                                             pending.set(requestId, { resolve, reject });
                                             window.chrome.webview.postMessage(payload);
                                         });
                                     },
                                     __resolveFromHost(base64Payload) {
                                         const raw = decodeBase64(base64Payload);
                                         if (!raw) {
                                             return;
                                         }

                                         let message;
                                         try {
                                             message = JSON.parse(raw);
                                         } catch {
                                             return;
                                         }

                                         const entry = pending.get(message.requestId);
                                         if (!entry) {
                                             return;
                                         }

                                         pending.delete(message.requestId);
                                         if (message.success) {
                                             entry.resolve(message.result);
                                             return;
                                         }

                                         entry.reject(new Error(message.error || 'Unknown HTWind host error.'));
                                     }
                                 };
                             })();
                             """;
    }

    private static string GetWebViewUserDataFolder()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HTWind",
            "WebView2",
            "Widgets"
        );

        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class WidgetHostApiRequest
    {
        public string? Type { get; set; }

        public string? RequestId { get; set; }

        public string? Command { get; set; }

        public JsonElement? Args { get; set; }
    }

    private sealed class WidgetHostApiResponse
    {
        public string? RequestId { get; set; }

        public bool Success { get; set; }

        public object? Result { get; set; }

        public string? Error { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private enum CornerResizeMode
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
}
