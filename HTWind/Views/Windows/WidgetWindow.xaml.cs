using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using HTWind.Services;

using Microsoft.Web.WebView2.Core;

namespace HTWind;

public partial class WidgetWindow : Window
{
    private readonly IDeveloperModeService _developerModeService;
    private readonly IWidgetPermissionStateService _widgetPermissionStateService;
    private const double MinWidgetWidth = 160;
    private const double MinWidgetHeight = 100;
    private const int SharedInteractionStateIntervalMilliseconds = 180;
    private readonly IWidgetHostApiService _widgetHostApiService;
    private readonly IWebViewEnvironmentProvider _webViewEnvironmentProvider;
    private static readonly HashSet<WidgetWindow> SharedInteractionStateWindows = [];
    private static DispatcherTimer? SharedInteractionStateTimer;
    private static readonly JsonSerializerOptions HostBridgeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private static readonly HostBridgeJsonContext HostBridgeJsonContextInstance =
        HostBridgeJsonContext.Default;
    private bool _isDragging;
    private Task? _initializationTask;
    private bool _isDesktopInteractionModifierActive;
    private bool _isSuspended;
    private bool _isWebViewReady;
    private bool _hasWidgetsEnvironmentLease;

    private bool _isResizing;
    private bool _isClosingOrClosed;
    private string? _pendingLivePreviewHtml;
    private bool _isLastNavigationLivePreview;
    private int _lastLivePreviewHash;
    private string? _lastNavigatedFilePath;
    private CornerResizeMode _resizeMode;
    private Point _dragStartScreen;
    private Point _dragStartWindow;
    private Point _resizeStartScreen;
    private Size _resizeStartSize;
    private Point _resizeStartWindow;
    private const int VirtualKeyAlt = 0x12;
    private const int AsyncKeyStatePressedMask = 0x8000;

    public WidgetWindow(
        WidgetModel model,
        IWidgetHostApiService widgetHostApiService,
        IWebViewEnvironmentProvider webViewEnvironmentProvider,
        IDeveloperModeService developerModeService,
        IWidgetPermissionStateService widgetPermissionStateService
    )
    {
        InitializeComponent();
        Model = model;
        _widgetHostApiService =
            widgetHostApiService ?? throw new ArgumentNullException(nameof(widgetHostApiService));
        _webViewEnvironmentProvider =
            webViewEnvironmentProvider
            ?? throw new ArgumentNullException(nameof(webViewEnvironmentProvider));
        _developerModeService =
            developerModeService
            ?? throw new ArgumentNullException(nameof(developerModeService));
        _widgetPermissionStateService =
            widgetPermissionStateService
            ?? throw new ArgumentNullException(nameof(widgetPermissionStateService));
        Model.PropertyChanged += Model_PropertyChanged;
        _developerModeService.Changed += DeveloperModeService_Changed;

        Loaded += WidgetWindow_Loaded;
        StateChanged += WidgetWindow_StateChanged;

        UpdateOverlay();
        SetRuntimeVisibility(Model.IsVisible);
    }

    public WidgetModel Model { get; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKeyCode);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    private void WidgetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RegisterForSharedInteractionStateUpdates();
    }

    private static void SharedInteractionStateTimer_Tick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        foreach (var window in SharedInteractionStateWindows)
        {
            window.OnSharedInteractionStateTick();
        }
    }

    private void OnSharedInteractionStateTick()
    {
        if (!IsLoaded || !IsVisible)
        {
            return;
        }

        if (!Model.IsLocked)
        {
            if (_isDesktopInteractionModifierActive)
            {
                _isDesktopInteractionModifierActive = false;
                UpdateOverlay();
            }

            return;
        }

        // Keep the interaction mode stable while pointer gesture is active.
        if (_isDragging || _isResizing)
        {
            return;
        }

        RefreshDesktopInteractionState();
    }

    private void RegisterForSharedInteractionStateUpdates()
    {
        if (SharedInteractionStateTimer is null)
        {
            SharedInteractionStateTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(SharedInteractionStateIntervalMilliseconds)
            };
            SharedInteractionStateTimer.Tick += SharedInteractionStateTimer_Tick;
        }

        SharedInteractionStateWindows.Add(this);
        if (!SharedInteractionStateTimer.IsEnabled)
        {
            SharedInteractionStateTimer.Start();
        }
    }

    private void UnregisterFromSharedInteractionStateUpdates()
    {
        SharedInteractionStateWindows.Remove(this);
        if (SharedInteractionStateTimer is null || SharedInteractionStateWindows.Count > 0)
        {
            return;
        }

        SharedInteractionStateTimer.Stop();
        SharedInteractionStateTimer.Tick -= SharedInteractionStateTimer_Tick;
        SharedInteractionStateTimer = null;
    }

    private void WidgetWindow_StateChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (WindowState == WindowState.Minimized)
        {
            _ = SuspendExecutionAsync();
            return;
        }

        if (Model.IsVisible)
        {
            _ = ResumeExecutionAsync();
        }
    }

    private void RefreshDesktopInteractionState()
    {
        var isDesktopModifierActive = IsDesktopModifierActive();
        if (_isDesktopInteractionModifierActive == isDesktopModifierActive)
        {
            return;
        }

        _isDesktopInteractionModifierActive = isDesktopModifierActive;
        UpdateOverlay();
    }

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
        var isInteractionOverlayEnabled = IsInteractionOverlayEnabled();

        if (!isInteractionOverlayEnabled)
        {
            DragOverlay.Visibility = Visibility.Collapsed;
            ResizeHandles.Visibility = Visibility.Collapsed;
            webView.IsHitTestVisible = true;

            WidgetChrome.Background = Brushes.Transparent;
            WidgetChrome.CornerRadius = new CornerRadius(0);
            WebViewHost.Margin = new Thickness(0);
        }
        else
        {
            DragOverlay.Visibility = Visibility.Visible;
            ResizeHandles.Visibility = Visibility.Visible;
            webView.IsHitTestVisible = false;

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

        var env = await _webViewEnvironmentProvider.GetWidgetsEnvironmentAsync();
        _hasWidgetsEnvironmentLease = true;

        try
        {
            await webView.EnsureCoreWebView2Async(env);
            ApplyDeveloperModePolicy();
            RegisterPermissionHandling();
            await RegisterHostBridgeAsync();
            _isWebViewReady = true;

            if (Model.IsVisible && WindowState != WindowState.Minimized)
            {
                NavigateCurrentContent();
            }
            else
            {
                await SuspendExecutionAsync();
            }
        }
        catch
        {
            if (_hasWidgetsEnvironmentLease)
            {
                _webViewEnvironmentProvider.ReleaseWidgetsEnvironment();
                _hasWidgetsEnvironmentLease = false;
            }

            throw;
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

    public async Task ShowAndInitializeAsync()
    {
        Show();
        await ResumeExecutionAsync();
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
        var didSuspend = false;
        try
        {
            didSuspend = await webView.CoreWebView2.TrySuspendAsync();
        }
        catch
        {
            // Fall back to blank navigation when suspension fails.
        }

        if (!didSuspend)
        {
            webView.CoreWebView2.Navigate("about:blank");
            ResetNavigationTracking();
        }

        _isSuspended = true;
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
            if (Model.IsVisible && WindowState != WindowState.Minimized)
            {
                NavigateCurrentContent();
            }

            return;
        }

        if (webView.CoreWebView2 is not null)
        {
            try
            {
                webView.CoreWebView2.Resume();
            }
            catch
            {
                // Resume can fail when runtime process is recycling.
            }
        }

        if (Model.IsVisible && WindowState != WindowState.Minimized)
        {
            NavigateCurrentContent();
        }

        _isSuspended = false;
    }

    private void NavigateCurrentContent()
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_pendingLivePreviewHtml))
        {
            var livePreviewHash = StringComparer.Ordinal.GetHashCode(_pendingLivePreviewHtml);
            if (_isLastNavigationLivePreview && _lastLivePreviewHash == livePreviewHash)
            {
                return;
            }

            webView.NavigateToString(_pendingLivePreviewHtml);
            _isLastNavigationLivePreview = true;
            _lastLivePreviewHash = livePreviewHash;
            _lastNavigatedFilePath = null;
            return;
        }

        if (!string.IsNullOrEmpty(Model.FilePath))
        {
            if (
                !_isLastNavigationLivePreview
                && string.Equals(_lastNavigatedFilePath, Model.FilePath, StringComparison.OrdinalIgnoreCase)
            )
            {
                return;
            }

            webView.Source = new Uri(Model.FilePath);
            _isLastNavigationLivePreview = false;
            _lastNavigatedFilePath = Model.FilePath;
            _lastLivePreviewHash = 0;
        }
    }

    private void ResetNavigationTracking()
    {
        _isLastNavigationLivePreview = false;
        _lastLivePreviewHash = 0;
        _lastNavigatedFilePath = null;
    }

    private void DragOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsInteractionOverlayEnabled() || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        _isDragging = true;
        _dragStartScreen = GetCursorScreenDip();
        _dragStartWindow = new Point(Left, Top);

        if (e.Source is UIElement sourceElement)
        {
            sourceElement.CaptureMouse();
        }

        e.Handled = true;
    }

    private void DragOverlay_MouseMove(object sender, MouseEventArgs e)
    {
        _ = sender;

        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentScreen = GetCursorScreenDip();
        var deltaX = currentScreen.X - _dragStartScreen.X;
        var deltaY = currentScreen.Y - _dragStartScreen.Y;

        Left = _dragStartWindow.X + deltaX;
        Top = _dragStartWindow.Y + deltaY;
    }

    private void DragOverlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;

        if (e.Source is UIElement sourceElement)
        {
            sourceElement.ReleaseMouseCapture();
        }

        RefreshDesktopInteractionState();
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
        if (!IsInteractionOverlayEnabled() || e.LeftButton != MouseButtonState.Pressed)
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

        RefreshDesktopInteractionState();
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
        _isClosingOrClosed = true;

        UnregisterFromSharedInteractionStateUpdates();
        Loaded -= WidgetWindow_Loaded;
        StateChanged -= WidgetWindow_StateChanged;
        Model.PropertyChanged -= Model_PropertyChanged;
        _developerModeService.Changed -= DeveloperModeService_Changed;
        if (webView.CoreWebView2 is not null)
        {
            webView.CoreWebView2.WebMessageReceived -= WebView_CoreWebView2_WebMessageReceived;
            webView.CoreWebView2.PermissionRequested -= WebView_CoreWebView2_PermissionRequested;
        }

        webView.Dispose();

        if (_hasWidgetsEnvironmentLease)
        {
            _webViewEnvironmentProvider.ReleaseWidgetsEnvironment();
            _hasWidgetsEnvironmentLease = false;
        }

        base.OnClosed(e);
    }

    private void DeveloperModeService_Changed(object? sender, EventArgs e)
    {
        _ = Dispatcher.BeginInvoke(new Action(() => _ = ApplyDeveloperModePolicyAsync()));
    }

    private async Task ApplyDeveloperModePolicyAsync()
    {
        // If the widget is visible, ensure the WebView is ready so policy changes apply immediately.
        if (Model.IsVisible && !_isWebViewReady)
        {
            await EnsureInitializedAsync();
        }

        ApplyDeveloperModePolicy();
    }

    private void ApplyDeveloperModePolicy()
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        var settings = webView.CoreWebView2.Settings;
        var isDeveloperModeEnabled = _developerModeService.IsEnabled();

        settings.AreDefaultContextMenusEnabled = isDeveloperModeEnabled;
        settings.AreDevToolsEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
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

    private void RegisterPermissionHandling()
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        webView.CoreWebView2.PermissionRequested -= WebView_CoreWebView2_PermissionRequested;
        webView.CoreWebView2.PermissionRequested += WebView_CoreWebView2_PermissionRequested;
    }

    private void WebView_CoreWebView2_PermissionRequested(
        object? sender,
        CoreWebView2PermissionRequestedEventArgs e
    )
    {
        if (string.IsNullOrWhiteSpace(Model.FilePath))
        {
            return;
        }

        if (
            _widgetPermissionStateService.TryGetDecision(
                Model.FilePath,
                e.PermissionKind,
                out var savedState
            )
        )
        {
            e.State = savedState;
            e.SavesInProfile = true;
            e.Handled = true;
            return;
        }

        var userDecision = ShowPermissionDecisionDialog(e.PermissionKind, e.Uri);
        if (userDecision is null)
        {
            return;
        }

        e.State = userDecision.Value;
        e.SavesInProfile = true;
        e.Handled = true;
        _widgetPermissionStateService.SaveDecision(Model.FilePath, e.PermissionKind, userDecision.Value);
        _ = RestartWidgetContentAfterPermissionDecisionAsync();
    }

    private async Task RestartWidgetContentAfterPermissionDecisionAsync()
    {
        if (!_isWebViewReady || webView.CoreWebView2 is null || !Model.IsVisible)
        {
            return;
        }

        try
        {
            webView.Stop();
            webView.CoreWebView2.Navigate("about:blank");
            ResetNavigationTracking();

            // Allow permission request pipeline to complete before reloading content.
            await Task.Delay(75);

            if (!_isWebViewReady || webView.CoreWebView2 is null || !Model.IsVisible)
            {
                return;
            }

            NavigateCurrentContent();
        }
        catch
        {
            // Restart failures should not break runtime interaction.
        }
    }

    private CoreWebView2PermissionState? ShowPermissionDecisionDialog(
        CoreWebView2PermissionKind permissionKind,
        string requestUri
    )
    {
        _ = requestUri;

        var widgetName = Path.GetFileName(Model.FilePath);
        if (string.IsNullOrWhiteSpace(widgetName))
        {
            widgetName = string.IsNullOrWhiteSpace(Model.FilePath)
                ? "Unknown widget"
                : Model.FilePath!;
        }

        var permissionWindow = new WidgetPermissionDecisionWindow(
            permissionKind.ToString(),
            widgetName
        )
        {
            Owner = this
        };

        permissionWindow.ShowDialog();

        return permissionWindow.IsAllowed
            ? CoreWebView2PermissionState.Allow
            : CoreWebView2PermissionState.Deny;
    }

    private async void WebView_CoreWebView2_WebMessageReceived(
            object? sender,
            CoreWebView2WebMessageReceivedEventArgs e
    )
    {
        if (_isClosingOrClosed || sender is not CoreWebView2 coreWebView)
        {
            return;
        }

        WidgetHostApiRequest? request;
        try
        {
            request = JsonSerializer.Deserialize(
                e.WebMessageAsJson,
                HostBridgeJsonContextInstance.WidgetHostApiRequest
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

        // Keep source-generated deserialization for request payloads, but use runtime options for
        // response serialization because Result can carry polymorphic/anonymous objects.
        var json = JsonSerializer.Serialize(response, HostBridgeJsonOptions);
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        if (_isClosingOrClosed)
        {
            return;
        }

        try
        {
            await coreWebView.ExecuteScriptAsync(
                $"window.HTWind && window.HTWind.__resolveFromHost('{payloadBase64}');"
            );
        }
        catch (ObjectDisposedException)
        {
            // Window/WebView can be disposed while an async host call is completing.
        }
        catch (InvalidOperationException)
        {
            // CoreWebView2 may be in teardown; ignore late responses.
        }
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

    private bool IsInteractionOverlayEnabled()
    {
        return !Model.IsLocked || _isDesktopInteractionModifierActive;
    }

    private bool IsDesktopModifierActive()
    {
        if (!Model.IsLocked)
        {
            return false;
        }

        var isAltDown = (GetAsyncKeyState(VirtualKeyAlt) & AsyncKeyStatePressedMask) != 0;
        if (!isAltDown)
        {
            return false;
        }

        return IsDesktopFocused();
    }

    private static bool IsDesktopFocused()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        var className = new StringBuilder(64);
        if (GetClassName(foregroundWindow, className, className.Capacity) <= 0)
        {
            return false;
        }

        return string.Equals(className.ToString(), "Progman", StringComparison.Ordinal)
            || string.Equals(className.ToString(), "WorkerW", StringComparison.Ordinal);
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

    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    )]
    [JsonSerializable(typeof(WidgetHostApiRequest))]
    [JsonSerializable(typeof(WidgetHostApiResponse))]
    private sealed partial class HostBridgeJsonContext : JsonSerializerContext;

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
