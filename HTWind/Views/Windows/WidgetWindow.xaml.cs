using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

using HTWind.Model;
using HTWind.Services.Application;
using HTWind.Services.WebView;
using HTWind.Services.Widget;

using Microsoft.Web.WebView2.Core;

namespace HTWind;

public partial class WidgetWindow : Window
{
    private readonly IDeveloperModeService _developerModeService;
    private readonly IWidgetPermissionStateService _widgetPermissionStateService;
    private const int SharedInteractionStateIntervalMilliseconds = 180;
    private const string LivePreviewFileSuffix = ".htwind-live-preview.html";
    private const string LivePreviewHostName = "widget.preview.htwind";
    private const uint EventSystemForeground = 0x0003;
    private const uint WinEventOutOfContext = 0;
    private const int ObjectIdWindow = 0;
    private static readonly IntPtr HwndBottom = new(1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private const uint SwpZOrderFlags = SwpNoSize | SwpNoMove | SwpNoActivate | SwpNoOwnerZOrder;
    private readonly IWidgetHostApiService _widgetHostApiService;
    private readonly IWebViewEnvironmentProvider _webViewEnvironmentProvider;
    private static readonly HashSet<WidgetWindow> SharedInteractionStateWindows = [];
    private static DispatcherTimer? AltInteractionPollTimer;
    private static bool IsDesktopFocusedCached;
    private static IntPtr ForegroundWinEventHook;
    private static readonly WinEventDelegate SharedWinEventCallbackDelegate = SharedWinEventCallback;
    private static readonly JsonSerializerOptions HostBridgeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
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
    private WidgetPinMode _appliedPinMode = WidgetPinMode.None;

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
        Activated += WidgetWindow_Activated;
        StateChanged += WidgetWindow_StateChanged;

        UpdateOverlay();
        ApplyWidgetOpacity();
        ApplyPinMode(Model.PinMode);
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

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags
    );

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate proc,
        uint idProcess,
        uint idThread,
        uint dwFlags
    );

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime
    );

    private void WidgetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RegisterForSharedInteractionStateUpdates();
        ApplyPinMode(Model.PinMode);
    }

    private void WidgetWindow_Activated(object? sender, EventArgs e)
    {
        // A widget pinned to the desktop layer must return behind normal windows even after the
        // user briefly interacts with it.
        if (_appliedPinMode == WidgetPinMode.Desktop)
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ApplyDesktopLayer));
        }
    }

    #region Shared desktop interaction monitoring (event-driven)

    private static void SharedWinEventCallback(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime
    )
    {
        _ = hWinEventHook;
        _ = hwnd;
        _ = idChild;
        _ = dwEventThread;
        _ = dwmsEventTime;

        if (eventType != EventSystemForeground || idObject != ObjectIdWindow)
        {
            return;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            OnForegroundWindowChanged();
        }
        else
        {
            _ = dispatcher.BeginInvoke(new Action(OnForegroundWindowChanged));
        }
    }

    private static void OnForegroundWindowChanged()
    {
        IsDesktopFocusedCached = IsDesktopFocused();

        foreach (var window in SharedInteractionStateWindows.ToList())
        {
            window.OnForegroundWindowChangedForWindow();
        }

        UpdateAltInteractionPolling();
    }

    private void OnForegroundWindowChangedForWindow()
    {
        if (!IsLoaded || !IsVisible || !Model.IsLocked)
        {
            return;
        }

        if (_isDragging || _isResizing)
        {
            return;
        }

        RefreshDesktopInteractionState();
    }

    private static void UpdateAltInteractionPolling()
    {
        var isPollingNeeded =
            IsDesktopFocusedCached
            && SharedInteractionStateWindows.Any(window => window.IsInteractionPollingNeeded());

        if (isPollingNeeded && AltInteractionPollTimer is null)
        {
            AltInteractionPollTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(SharedInteractionStateIntervalMilliseconds)
            };
            AltInteractionPollTimer.Tick += AltInteractionPollTimer_Tick;
            AltInteractionPollTimer.Start();
            return;
        }

        if (!isPollingNeeded && AltInteractionPollTimer is not null)
        {
            AltInteractionPollTimer.Stop();
            AltInteractionPollTimer.Tick -= AltInteractionPollTimer_Tick;
            AltInteractionPollTimer = null;
        }
    }

    private bool IsInteractionPollingNeeded()
    {
        return IsLoaded && IsVisible && Model.IsLocked;
    }

    private static void AltInteractionPollTimer_Tick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        // The poll timer only tracks the Alt key while the desktop itself is focused; focus
        // changes arrive through the foreground WinEvent hook instead.
        if (!IsDesktopFocusedCached || !SharedInteractionStateWindows.Any(w => w.IsInteractionPollingNeeded()))
        {
            UpdateAltInteractionPolling();
            return;
        }

        foreach (var window in SharedInteractionStateWindows.ToList())
        {
            window.OnSharedInteractionStateTick();
        }
    }

    private static void EnsureForegroundHookInstalled()
    {
        if (ForegroundWinEventHook != IntPtr.Zero)
        {
            return;
        }

        ForegroundWinEventHook = SetWinEventHook(
            EventSystemForeground,
            EventSystemForeground,
            IntPtr.Zero,
            SharedWinEventCallbackDelegate,
            0,
            0,
            WinEventOutOfContext);
    }

    private void RegisterForSharedInteractionStateUpdates()
    {
        EnsureForegroundHookInstalled();

        SharedInteractionStateWindows.Add(this);
        IsDesktopFocusedCached = IsDesktopFocused();
        UpdateAltInteractionPolling();
    }

    private void UnregisterFromSharedInteractionStateUpdates()
    {
        SharedInteractionStateWindows.Remove(this);
        UpdateAltInteractionPolling();
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

    #endregion

    private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(WidgetModel.IsLocked):
                UpdateOverlay();
                UpdateAltInteractionPolling();
                break;
            case nameof(WidgetModel.IsPinned):
            case nameof(WidgetModel.PinMode):
                ApplyPinMode(Model.PinMode);
                break;
            case nameof(WidgetModel.IsVisible):
                SetRuntimeVisibility(Model.IsVisible);
                UpdateAltInteractionPolling();
                break;
            case nameof(WidgetModel.WidgetOpacityPercent):
                ApplyWidgetOpacity();
                break;
            case nameof(WidgetModel.ZoomFactorPercent):
                ApplyZoomFactor();
                break;
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

        if (Topmost != (_appliedPinMode == WidgetPinMode.Topmost))
        {
            Topmost = _appliedPinMode == WidgetPinMode.Topmost;
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
            webView.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
            _widgetHostApiService.ThemeChanged += WidgetHostApiService_ThemeChanged;
            _isWebViewReady = true;

            ApplyZoomFactor();
            ApplyPinMode(Model.PinMode);

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
            ApplyPinMode(Model.PinMode);
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
            NavigateCurrentContent();
        }
    }

    public void ClearLivePreview()
    {
        _pendingLivePreviewHtml = null;
        DeleteLivePreviewFile();

        if (_isWebViewReady && Model.IsVisible)
        {
            NavigateCurrentContent();
        }
    }

    /// <summary>Re-injects manifest settings into the widget document after they change.</summary>
    public void ApplyInjectedSettings()
    {
        if (!_isWebViewReady || _isClosingOrClosed)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(async () =>
            {
                if (_isClosingOrClosed || !_isWebViewReady)
                {
                    return;
                }

                try
                {
                    await InjectSettingsScriptAsync();
                }
                catch
                {
                    // The document may be navigating; the next DOMContentLoaded re-injects.
                }
            }));
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

            // Prefer a virtual-host navigation so multi-file packages keep relative assets and
            // avoid the NavigateToString size limit (about:blank origin breaks styles.css/app.js).
            if (TryNavigateLivePreview(_pendingLivePreviewHtml, livePreviewHash))
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

    private bool TryNavigateLivePreview(string htmlContent, int livePreviewHash)
    {
        var widgetRootPath = GetWidgetRootDirectory();
        if (widgetRootPath is null || webView.CoreWebView2 is null)
        {
            return false;
        }

        try
        {
            var previewFileName = GetLivePreviewFileName();
            if (previewFileName is null)
            {
                return false;
            }

            var previewFilePath = Path.Combine(widgetRootPath, previewFileName);
            File.WriteAllText(previewFilePath, htmlContent, Encoding.UTF8);

            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                LivePreviewHostName,
                widgetRootPath,
                CoreWebView2HostResourceAccessKind.Allow);
            webView.CoreWebView2.Navigate($"https://{LivePreviewHostName}/{previewFileName}");

            _isLastNavigationLivePreview = true;
            _lastLivePreviewHash = livePreviewHash;
            _lastNavigatedFilePath = null;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void DeleteLivePreviewFile()
    {
        var widgetRootPath = GetWidgetRootDirectory();
        var previewFileName = GetLivePreviewFileName();
        if (widgetRootPath is null || previewFileName is null)
        {
            return;
        }

        try
        {
            var previewFilePath = Path.Combine(widgetRootPath, previewFileName);
            if (File.Exists(previewFilePath))
            {
                File.Delete(previewFilePath);
            }
        }
        catch
        {
        }
    }

    /// <summary>Per-widget preview file name so widgets sharing a folder never collide.</summary>
    private string? GetLivePreviewFileName()
    {
        var entryFileName = Path.GetFileName(Model.FilePath);
        return string.IsNullOrWhiteSpace(entryFileName) ? null : entryFileName + LivePreviewFileSuffix;
    }

    private string? GetWidgetRootDirectory()
    {
        if (string.IsNullOrWhiteSpace(Model.FilePath))
        {
            return null;
        }

        try
        {
            var directoryPath = Path.GetDirectoryName(Path.GetFullPath(Model.FilePath));
            return string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath)
                ? null
                : directoryPath;
        }
        catch
        {
            return null;
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

        var nextLeft = _dragStartWindow.X + deltaX;
        var nextTop = _dragStartWindow.Y + deltaY;
        ApplyMagneticSnapping(ref nextLeft, ref nextTop);

        Left = nextLeft;
        Top = nextTop;
    }

    private void ApplyMagneticSnapping(ref double left, ref double top)
    {
        var width = double.IsNaN(ActualWidth) || ActualWidth <= 0 ? Width : ActualWidth;
        var height = double.IsNaN(ActualHeight) || ActualHeight <= 0 ? Height : ActualHeight;

        (left, top) = WidgetWindowMath.SnapToScreenEdges(
            left,
            top,
            width,
            height,
            WidgetGeometryService.GetWorkAreasForSnapping()
        );

        foreach (var other in SharedInteractionStateWindows)
        {
            if (ReferenceEquals(other, this) || !other.IsVisible)
            {
                continue;
            }

            var otherRect = new WidgetWindowRect(other.Left, other.Top, other.ActualWidth, other.ActualHeight);
            if (!otherRect.IsValid)
            {
                continue;
            }

            (left, top) = WidgetWindowMath.SnapToOtherWidget(left, top, width, height, otherRect);
        }
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
        ApplyPinMode(Model.PinMode);
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
        var next = WidgetWindowMath.ApplyCornerResize(
            _resizeMode,
            _resizeStartWindow.X,
            _resizeStartWindow.Y,
            _resizeStartSize.Width,
            _resizeStartSize.Height,
            deltaX,
            deltaY
        );

        Left = next.Left;
        Top = next.Top;
        Width = next.Width;
        Height = next.Height;
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
        Activated -= WidgetWindow_Activated;
        StateChanged -= WidgetWindow_StateChanged;
        Model.PropertyChanged -= Model_PropertyChanged;
        _developerModeService.Changed -= DeveloperModeService_Changed;
        _widgetHostApiService.ThemeChanged -= WidgetHostApiService_ThemeChanged;
        if (webView.CoreWebView2 is not null)
        {
            webView.CoreWebView2.WebMessageReceived -= WebView_CoreWebView2_WebMessageReceived;
            webView.CoreWebView2.PermissionRequested -= WebView_CoreWebView2_PermissionRequested;
            webView.CoreWebView2.DOMContentLoaded -= CoreWebView2_DOMContentLoaded;
        }

        DeleteLivePreviewFile();
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
        await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(GetConsoleHookScript());
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

    #region Widget runtime console forwarding (used by the multi-file editor console panel)

    internal static string GetConsoleHookScript()
    {
        return """
               (() => {
                   if (window.__htwindConsoleHooked) {
                       return;
                   }

                   window.__htwindConsoleHooked = true;

                   function send(level, args) {
                       try {
                           const text = Array.from(args).map((item) => {
                               try {
                                   return typeof item === 'string' ? item : JSON.stringify(item);
                               } catch {
                                   return String(item);
                               }
                           }).join(' ');

                           if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
                               window.chrome.webview.postMessage({ type: 'htwindConsole', level, message: text });
                           }
                       } catch {
                       }
                   }

                   ['log', 'info', 'warn', 'error', 'debug'].forEach((level) => {
                       const original = console[level] ? console[level].bind(console) : () => {};
                       console[level] = function () {
                           send(level, arguments);
                           original.apply(null, arguments);
                       };
                   });

                   window.addEventListener('error', (event) => {
                       send('error', [`${event.message || 'Script error'} (${event.filename || ''}:${event.lineno || 0})`]);
                   });

                   window.addEventListener('unhandledrejection', (event) => {
                       try {
                           const reason = event.reason;
                           send('error', [`Unhandled rejection: ${reason && reason.message ? reason.message : String(reason)}`]);
                       } catch {
                       }
                   });
               })();
               """;
    }

    private bool TryHandleConsoleHookMessage(string webMessageAsJson)
    {
        try
        {
            using var document = JsonDocument.Parse(webMessageAsJson);
            var root = document.RootElement;
            if (
                !root.TryGetProperty("type", out var typeElement)
                || !string.Equals(typeElement.GetString(), "htwindConsole", StringComparison.Ordinal)
            )
            {
                return false;
            }

            var level = root.TryGetProperty("level", out var levelElement) && levelElement.ValueKind == JsonValueKind.String
                ? levelElement.GetString() ?? "log"
                : "log";
            var message = root.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString() ?? string.Empty
                : string.Empty;

            WidgetRuntimeConsole.Publish(Model.Id, level, message);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    private async void CoreWebView2_DOMContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isClosingOrClosed || !_isWebViewReady)
        {
            return;
        }

        try
        {
            await InjectSettingsScriptAsync();
        }
        catch
        {
            // The document can navigate away mid-injection; the next load retries.
        }
    }

    private async Task InjectSettingsScriptAsync()
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        var settingsJson = _widgetHostApiService.GetWidgetSettingsJson(Model.FilePath) ?? "null";
        await webView.CoreWebView2.ExecuteScriptAsync(BuildSettingsInjectionScript(settingsJson));
    }

    /// <summary>
    ///     Builds the DOMContentLoaded settings-injection script exposed to the widget document
    ///     (window.HTWind.__settings plus the htwindsettings event). Internal so runtime tests
    ///     exercise the exact script shipped in the app.
    /// </summary>
    internal static string BuildSettingsInjectionScript(string settingsJson)
    {
        return
            "window.HTWind = window.HTWind || {};"
            + $"window.HTWind.__settings = {settingsJson};"
            + "if (typeof window.dispatchEvent === 'function') { window.dispatchEvent(new CustomEvent('htwindsettings', { detail: window.HTWind.__settings })); }";
    }

    private async void WidgetHostApiService_ThemeChanged(object? sender, WidgetHostThemePayload payload)
    {
        if (_isClosingOrClosed || !_isWebViewReady || webView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(payload, HostBridgeJsonOptions);
            var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            await webView.CoreWebView2.ExecuteScriptAsync(
                $"window.HTWind && window.HTWind.__themeChanged && window.HTWind.__themeChanged('{payloadBase64}');"
            );
        }
        catch (ObjectDisposedException)
        {
            // Window/WebView can be disposed while a broadcast is in flight.
        }
        catch (InvalidOperationException)
        {
            // CoreWebView2 may be in teardown; ignore late broadcasts.
        }
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

        // Console-hook messages (forwarded to the editor console panel) are handled separately.
        if (TryHandleConsoleHookMessage(e.WebMessageAsJson))
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

        var executionContext = BuildExecutionContext(request.Command, out var authorizationError);
        if (executionContext is null)
        {
            await RespondToWidgetAsync(
                coreWebView,
                request.RequestId,
                new WidgetHostApiExecutionResult
                {
                    Success = false,
                    Error = authorizationError ?? "The command is not authorized for this widget."
                });
            return;
        }

        var executionResult = await _widgetHostApiService.ExecuteAsync(
            request.Command,
            request.Args,
            executionContext);

        await RespondToWidgetAsync(coreWebView, request.RequestId, executionResult);
    }

    private WidgetExecutionContext? BuildExecutionContext(string command, out string? error)
    {
        error = null;

        if (!string.Equals(command, WidgetHostApiService.PowerShellCommand, StringComparison.Ordinal))
        {
            return new WidgetExecutionContext { WidgetFilePath = Model.FilePath };
        }

        var decision = ResolvePowerShellDecision();
        if (decision is null)
        {
            error = "PowerShell execution is not authorized for this widget.";
            return null;
        }

        return new WidgetExecutionContext
        {
            WidgetFilePath = Model.FilePath,
            PowerShellAuthorization = decision == PowerShellExecutionDecision.Full
                ? PowerShellExecutionAuthorization.Full
                : PowerShellExecutionAuthorization.ReadOnly
        };
    }

    /// <summary>
    ///     Resolves the per-widget PowerShell authorization. Returns null when execution must be
    ///     blocked (the user denied access, or the manifest declares no PowerShell requirement).
    /// </summary>
    private PowerShellExecutionDecision? ResolvePowerShellDecision()
    {
        if (string.IsNullOrWhiteSpace(Model.FilePath))
        {
            return null;
        }

        if (
            _widgetPermissionStateService.TryGetPowerShellDecision(
                Model.FilePath,
                out var savedDecision
            )
        )
        {
            return savedDecision == PowerShellExecutionDecision.Deny ? null : savedDecision;
        }

        var requirement = ReadManifestPowerShellRequirement();
        if (requirement == ManifestPowerShellRequirement.None)
        {
            // The manifest explicitly opts out of PowerShell; block without prompting.
            return null;
        }

        var decision = ShowPowerShellPermissionDialog(requirement);
        if (decision is null)
        {
            return PowerShellExecutionDecision.Deny;
        }

        _widgetPermissionStateService.SavePowerShellDecision(Model.FilePath, decision.Value);
        return decision;
    }

    private enum ManifestPowerShellRequirement
    {
        Unspecified,

        None,

        ReadOnly,

        Full
    }

    private ManifestPowerShellRequirement ReadManifestPowerShellRequirement()
    {
        var widgetRootPath = GetWidgetRootDirectory();
        if (widgetRootPath is null)
        {
            return ManifestPowerShellRequirement.Unspecified;
        }

        try
        {
            var manifest = WidgetManifestHelper.LoadManifestIfPresent(widgetRootPath, ManifestJsonOptions);
            if (manifest?.Requirements is not JsonElement requirements
                || requirements.ValueKind != JsonValueKind.Object
                || !requirements.TryGetProperty("powershell", out var powershellElement))
            {
                return ManifestPowerShellRequirement.Unspecified;
            }

            var value = powershellElement.ValueKind == JsonValueKind.String
                ? powershellElement.GetString()
                : powershellElement.ValueKind == JsonValueKind.False
                    ? "none"
                    : powershellElement.ValueKind == JsonValueKind.True
                        ? "full"
                        : null;

            return (value?.Trim().ToLowerInvariant()) switch
            {
                "none" or "false" or "deny" => ManifestPowerShellRequirement.None,
                "readonly" or "read-only" or "read" => ManifestPowerShellRequirement.ReadOnly,
                "full" or "true" => ManifestPowerShellRequirement.Full,
                _ => ManifestPowerShellRequirement.Unspecified
            };
        }
        catch
        {
            return ManifestPowerShellRequirement.Unspecified;
        }
    }

    private PowerShellExecutionDecision? ShowPowerShellPermissionDialog(
        ManifestPowerShellRequirement requirement)
    {
        var widgetName = Path.GetFileName(Model.FilePath);
        if (string.IsNullOrWhiteSpace(widgetName))
        {
            widgetName = string.IsNullOrWhiteSpace(Model.FilePath) ? "Unknown widget" : Model.FilePath!;
        }

        var permissionWindow = new PowerShellPermissionWindow(
            widgetName,
            requirement == ManifestPowerShellRequirement.Full
        )
        {
            Owner = this
        };

        permissionWindow.ShowDialog();
        return permissionWindow.Decision;
    }

    private static async Task RespondToWidgetAsync(
        CoreWebView2 coreWebView,
        string requestId,
        WidgetHostApiExecutionResult executionResult)
    {
        var response = new WidgetHostApiResponse
        {
            RequestId = requestId,
            Success = executionResult.Success,
            Result = executionResult.Result,
            Error = executionResult.Error
        };

        // Keep source-generated deserialization for request payloads, but use runtime options for
        // response serialization because Result can carry polymorphic/anonymous objects.
        var json = JsonSerializer.Serialize(response, HostBridgeJsonOptions);
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

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

    internal static string GetHostBridgeScript()
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
                       getSettings() {
                           return window.HTWind.__settings || null;
                       },
                       invoke(command, args) {
                           if (!window.chrome || !window.chrome.webview || !window.chrome.webview.postMessage) {
                               return Promise.reject(new Error('HTWind host bridge is not available.'));
                           }

                           const requestId = createRequestId();
                           const payload = {
                               type: 'HTWind-api-request',
                               requestId,
                               command,
                               args: args || {}
                           };

                           // Client-side watchdog: the promise is rejected and removed from the
                           // pending map if the host never responds, so a failed host call cannot
                           // leak promises forever.
                           const hostTimeoutMs = (args && typeof args.timeoutMs === 'number' && args.timeoutMs > 0)
                               ? args.timeoutMs
                               : 10000;
                           // powershell.exec may first show a per-widget permission dialog; keep a
                           // 60s floor for it so the watchdog cannot fire while the user is still
                           // answering. The host-side engine timeout enforces the real script
                           // timeout regardless.
                           const clientTimeoutMs = (command === 'powershell.exec')
                               ? Math.max(hostTimeoutMs + 2000, 60000)
                               : hostTimeoutMs + 2000;

                           return new Promise((resolve, reject) => {
                               const entry = { resolve, reject, timer: null };
                               entry.timer = setTimeout(() => {
                                   pending.delete(requestId);
                                   entry.timer = null;
                                   reject(new Error(`HTWind host call timed out: ${command}`));
                               }, clientTimeoutMs);

                               pending.set(requestId, entry);
                               try {
                                   window.chrome.webview.postMessage(payload);
                               } catch (error) {
                                   if (entry.timer) {
                                       clearTimeout(entry.timer);
                                       entry.timer = null;
                                   }
                                   pending.delete(requestId);
                                   reject(error);
                               }
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
                           if (entry.timer) {
                               clearTimeout(entry.timer);
                               entry.timer = null;
                           }

                           if (message.success) {
                               entry.resolve(message.result);
                               return;
                           }

                           entry.reject(new Error(message.error || 'Unknown HTWind host error.'));
                       },
                       __themeChanged(base64Payload) {
                           const raw = decodeBase64(base64Payload);
                           if (!raw) {
                               return;
                           }

                           let theme;
                           try {
                               theme = JSON.parse(raw);
                           } catch {
                               return;
                           }

                           window.HTWind.__lastTheme = theme;
                           if (typeof window.dispatchEvent === 'function') {
                               window.dispatchEvent(new CustomEvent('htwindtheme', { detail: theme }));
                           }
                       }
                   };
               })();
               """;
    }

    #region Per-widget window experience (pin mode, opacity, zoom)

    public void ApplyWidgetOpacity()
    {
        var percent = Math.Clamp(Model.WidgetOpacityPercent ?? 100, 20, 100);
        Opacity = percent / 100.0;
    }

    public void ApplyZoomFactor()
    {
        if (!_isWebViewReady || webView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            var percent = Math.Clamp(Model.ZoomFactorPercent ?? 100, 50, 200);
            webView.ZoomFactor = percent / 100.0;
        }
        catch
        {
            // ZoomFactor cannot change while a navigation is in flight; retries on next change.
        }
    }

    public void ApplyPinMode(WidgetPinMode mode)
    {
        _appliedPinMode = mode;

        var isTopmost = mode == WidgetPinMode.Topmost;
        if (Topmost != isTopmost)
        {
            Topmost = isTopmost;
        }

        if (mode == WidgetPinMode.Desktop)
        {
            ApplyDesktopLayer();
        }
    }

    private void ApplyDesktopLayer()
    {
        try
        {
            var windowHandle = new WindowInteropHelper(this).Handle;
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            SetWindowPos(windowHandle, HwndBottom, 0, 0, 0, 0, SwpZOrderFlags);
        }
        catch
        {
        }
    }

    #endregion

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
}
