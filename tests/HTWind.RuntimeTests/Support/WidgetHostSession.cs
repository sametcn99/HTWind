using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;

using HTWind.RuntimeTests.Support;
using HTWind.Services.PowerShellExecution;
using HTWind.Services.WebView;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

using Xunit;

namespace HTWind.RuntimeTests.Support;

/// <summary>
///     A real, offscreen WebView2 session wired the same way WidgetWindow wires it: the exact
///     production bridge and console-hook scripts are injected on document creation, and the
///     host side answers window.HTWind.invoke calls through the real WidgetHostApiService.
///     One session is shared by all tests in the WebView2 collection (STA-serialized).
/// </summary>
public sealed class WidgetHostSession : IAsyncLifetime
{
    private static readonly JsonSerializerOptions HostBridgeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly StaRunner _sta = new();
    private readonly TaskCompletionSource _bridgeReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Window? _window;
    private WebView2? _webView;
    private CoreWebView2? _core;
    private string? _currentWidgetId;
    private string? _currentWidgetFilePath;

    public WidgetHostApiService Api { get; } = new();

    /// <summary>Per-widget PowerShell authorization used by the host glue for powershell.exec.</summary>
    public PowerShellExecutionAuthorization? PowerShellAuthorization { get; set; } =
        PowerShellExecutionAuthorization.ReadOnly;

    public string UserDataFolder { get; } =
        TestPaths.CreateTempDirectory("webview2-userdata");

    /// <summary>An existing folder backing storage.set/get + settings injection tests.</summary>
    public string WidgetFolder { get; } =
        TestPaths.CreateTempDirectory("widgets");

    public string WidgetFilePath =>
        Path.Combine(WidgetFolder, "test-widget.html");

    public string WidgetId =>
        _currentWidgetId ?? throw new InvalidOperationException("Session not initialized.");

    /// <summary>Executes a raw host script (e.g. production settings injection) in the page.</summary>
    public Task ExecuteHostScriptAsync(string script)
    {
        return _sta.RunAsync(async () =>
        {
            await RequireCore().ExecuteScriptAsync(script);
            return true;
        });
    }

    /// <summary>Runs arbitrary async work on the session's STA thread.</summary>
    public Task<T> RunOnStaAsync<T>(Func<Task<T>> action) => _sta.RunAsync(action);

    /// <summary>
    ///     WidgetRuntimeConsole entries forwarded through the production console hook since the
    ///     last navigation (console.log/error, uncaught errors, unhandled rejections).
    /// </summary>
    public IReadOnlyList<string> RuntimeConsoleEntries =>
        HTWind.Services.Widget.WidgetRuntimeConsole.GetEntries(WidgetId);

    public async ValueTask InitializeAsync()
    {
        // The production scripts run before any document script.
        await _sta.RunAsync(async () =>
        {
            Environment.SetEnvironmentVariable(TestPaths.DataDirEnvironmentVariable, TestPaths.DataRoot);

            File.WriteAllText(WidgetFilePath, "<html><body>probe</body></html>", Encoding.UTF8);

            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: UserDataFolder,
                options: new CoreWebView2EnvironmentOptions("--disable-extensions"));

            _window = new Window
            {
                Width = 480,
                Height = 360,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -32000,
                Top = 0,
            };
            _webView = new WebView2();
            _window.Content = _webView;
            new WindowInteropHelper(_window).EnsureHandle();

            // WebView2 (HwndHost) builds its child HWND during layout, which only runs while
            // the window is shown; positioned far offscreen so the harness stays invisible.
            _window.Show();
            await _webView.EnsureCoreWebView2Async(environment);
            _core = _webView.CoreWebView2 ?? throw new InvalidOperationException("CoreWebView2 was not created.");

            await _core.AddScriptToExecuteOnDocumentCreatedAsync(WidgetWindow.GetHostBridgeScript());
            await _core.AddScriptToExecuteOnDocumentCreatedAsync(WidgetWindow.GetConsoleHookScript());
            _core.WebMessageReceived += OnWebMessageReceived;
            _core.DOMContentLoaded += OnDOMContentLoadedForBridge;

            Api.ThemeChanged += OnApiThemeChanged;

            _core.Navigate("about:blank");
            await _bridgeReady.Task.WaitAsync(TimeSpan.FromSeconds(30));

            // Default execution context so bridge probes on about:blank still resolve a real
            // widget file for storage APIs.
            _currentWidgetId = "bridge-probe";
            _currentWidgetFilePath = WidgetFilePath;
            return true;
        });
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _sta.RunAsync(async () =>
            {
                if (_core is not null)
                {
                    _core.WebMessageReceived -= OnWebMessageReceived;
                }

                if (_webView is not null)
                {
                    _webView.Dispose();
                    _webView = null;
                    _core = null;
                }

                _window?.Close();
                _window = null;
                return true;
            });
        }
        finally
        {
            _sta.Dispose();
        }
    }

    /// <summary>Navigates the session to a file, waits for DOMContentLoaded + settle time.</summary>
    public Task NavigateFileAsync(string filePath, string widgetId, TimeSpan? settleTime = null)
    {
        return _sta.RunAsync(async () =>
        {
            HTWind.Services.Widget.WidgetRuntimeConsole.Clear(widgetId);
            _currentWidgetId = widgetId;
            _currentWidgetFilePath = filePath;

            var core = RequireCore();
            var navigated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            void Handler(object? sender, CoreWebView2DOMContentLoadedEventArgs args) =>
                navigated.TrySetResult();

            core.DOMContentLoaded += Handler;
            try
            {
                core.Navigate(new Uri(filePath).AbsoluteUri);
                await navigated.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
            finally
            {
                core.DOMContentLoaded -= Handler;
            }

            await Task.Delay(settleTime ?? TimeSpan.FromMilliseconds(300));
            return true;
        });
    }

    /// <summary>Evaluates a script in the page and returns the JSON-decoded result.</summary>
    public Task<JsonElement> EvaluateAsync(string script)
    {
        return _sta.RunAsync(async () =>
        {
            var json = await RequireCore().ExecuteScriptAsync(script);
            return JsonSerializer.Deserialize<JsonElement>(json);
        });
    }

    /// <summary>
    ///     Invokes a host API command exactly like a widget would. ExecuteScriptAsync does not
    ///     await promise results in this WebView2 version, so the result is parked in a page
    ///     global and polled.
    /// </summary>
    public async Task<JsonElement> InvokeAsync(string command, object? args = null)
    {
        var argsJson = args is null ? "{}" : JsonSerializer.Serialize(args);
        var script =
            "window.__htwindProbeResult = undefined; " +
            "window.HTWind.invoke('" +
            command +
            "', " +
            argsJson +
            ").then((r) => { window.__htwindProbeResult = { ok: true, result: r }; })" +
            ".catch((e) => { window.__htwindProbeResult = { ok: false, error: (e && e.message) ? e.message : String(e) }; });";
        await EvaluateAsync(script);

        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var probe = await EvaluateAsync(
                "window.__htwindProbeResult === undefined ? null : window.__htwindProbeResult");
            if (probe.ValueKind == JsonValueKind.Object)
            {
                return probe;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"The host API call '{command}' did not answer within 20s.");
    }

    private CoreWebView2 RequireCore() =>
        _core ?? throw new InvalidOperationException("The WebView2 session is not initialized.");

    private void OnDOMContentLoadedForBridge(object? sender, CoreWebView2DOMContentLoadedEventArgs args)
    {
        _bridgeReady.TrySetResult();
    }

    private void OnApiThemeChanged(object? sender, WidgetHostThemePayload payload)
    {
        _ = sender;
        _ = _sta.RunAsync(async () =>
        {
            var json = JsonSerializer.Serialize(payload, HostBridgeJsonOptions);
            var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            await RequireCore().ExecuteScriptAsync(
                $"window.HTWind && window.HTWind.__themeChanged && window.HTWind.__themeChanged('{payloadBase64}');");
            return true;
        });
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!TryReadJson(args.WebMessageAsJson, out var message))
        {
            return;
        }

        if (
            message.TryGetProperty("type", out var typeElement)
            && typeElement.ValueKind == JsonValueKind.String
            && typeElement.GetString() == "htwindConsole"
        )
        {
            var level = message.TryGetProperty("level", out var levelElement) ? levelElement.GetString() ?? "log" : "log";
            var text = message.TryGetProperty("message", out var messageElement) ? messageElement.GetString() ?? string.Empty : string.Empty;
            HTWind.Services.Widget.WidgetRuntimeConsole.Publish(_currentWidgetId ?? "unknown", level, text);
            return;
        }

        if (
            !message.TryGetProperty("type", out var requestType)
            || requestType.GetString() != "HTWind-api-request"
            || !message.TryGetProperty("requestId", out var requestIdElement)
            || requestIdElement.GetString() is not { Length: > 0 } requestId
            || !message.TryGetProperty("command", out var commandElement)
            || commandElement.GetString() is not { Length: > 0 } command
        )
        {
            return;
        }

        JsonElement? argsElement = message.TryGetProperty("args", out var parsedArgs)
            && parsedArgs.ValueKind == JsonValueKind.Object
                ? parsedArgs
                : null;

        var context = command == WidgetHostApiService.PowerShellCommand
            ? PowerShellAuthorization.HasValue
                ? new WidgetExecutionContext
                {
                    WidgetFilePath = _currentWidgetFilePath,
                    PowerShellAuthorization = PowerShellAuthorization.Value,
                }
                : new WidgetExecutionContext { WidgetFilePath = _currentWidgetFilePath }
            : new WidgetExecutionContext { WidgetFilePath = _currentWidgetFilePath };

        _ = _sta.RunAsync(async () =>
        {
            var result = await Api.ExecuteAsync(command, argsElement, context);
            var response = JsonSerializer.Serialize(
                new { requestId, success = result.Success, result = result.Result, error = result.Error },
                HostBridgeJsonOptions);
            var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(response));
            await RequireCore().ExecuteScriptAsync(
                $"window.HTWind && window.HTWind.__resolveFromHost('{payloadBase64}');");
            return true;
        });
    }

    private static bool TryReadJson(string text, out JsonElement element)
    {
        try
        {
            element = JsonSerializer.Deserialize<JsonElement>(text);
            return true;
        }
        catch
        {
            element = default;
            return false;
        }
    }
}

[CollectionDefinition(WebView2Collection.CollectionName)]
public sealed class WebView2Collection : ICollectionFixture<WidgetHostSession>
{
    public const string CollectionName = "WebView2 Runtime";
}
