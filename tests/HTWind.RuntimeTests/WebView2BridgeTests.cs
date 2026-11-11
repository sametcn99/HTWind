using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

using HTWind.RuntimeTests.Support;
using HTWind.Services.PowerShellExecution;
using HTWind.Services.WebView;

using Xunit;

namespace HTWind.RuntimeTests;

/// <summary>
///     Drives the real production bridge script inside a real WebView2 runtime and routes
///     widget calls through the real WidgetHostApiService — no mocks.
/// </summary>
[Collection(WebView2Collection.CollectionName)]
public sealed class WebView2BridgeTests(WidgetHostSession session)
{
    [Fact]
    public async Task Bridge_IsReadyAfterDocumentLoad()
    {
        Assert.True(await WaitForBridgeReadyAsync());
    }

    [Fact]
    public async Task StorageSetGet_RoundTripsThroughRealFiles()
    {
        Assert.True(await WaitForBridgeReadyAsync());

        // Other classes in this collection navigate real templates through the same shared
        // session, which repoints the current widget file (and thus the storage folder).
        // Re-anchor the session to the probe widget so this test is order-independent.
        await session.NavigateFileAsync(session.WidgetFilePath, "bridge-probe");
        Assert.True(await WaitForBridgeReadyAsync());

        var setResult = await session.InvokeAsync(
            "storage.set", new { key = "greeting", value = "hello-runtime" });
        Assert.True(GetBool(setResult, "ok"), GetError(setResult));

        var settingsFile = Directory.GetFiles(session.WidgetFolder, "*.htwind-settings.json");
        Assert.NotEmpty(settingsFile);

        var getResult = await session.InvokeAsync(
            "storage.get", new { key = "greeting" });
        Assert.True(GetBool(getResult, "ok"), GetError(getResult));
        Assert.Equal(
            "hello-runtime",
            getResult.GetProperty("result").GetString());
    }

    [Fact]
    public async Task ThemeGet_ReturnsAWellFormedPayload()
    {
        Assert.True(await WaitForBridgeReadyAsync());

        var theme = await session.InvokeAsync("theme.get");
        Assert.True(GetBool(theme, "ok"), GetError(theme));

        var result = theme.GetProperty("result");
        Assert.True(result.TryGetProperty("appTheme", out _));
        Assert.True(result.TryGetProperty("systemTheme", out _));
        Assert.True(result.TryGetProperty("accentColor", out var accent));
        Assert.Matches("^#[0-9A-Fa-f]{6}$", accent.GetString() ?? string.Empty);
        Assert.True(
            result.GetProperty("isDark").ValueKind is JsonValueKind.True or JsonValueKind.False,
            "isDark must be a boolean.");
    }

    [Fact]
    public async Task PublishThemeChanged_ReachesThePageAsEventAndLastTheme()
    {
        Assert.True(await WaitForBridgeReadyAsync());

        await session.EvaluateAsync(
            "(() => { window.__themeEvents = []; " +
            "window.addEventListener('htwindtheme', (e) => window.__themeEvents.push(e.detail)); return true; })()");

        session.Api.PublishThemeChanged();

        await WaitUntilAsync(async () =>
            (await session.EvaluateAsync("(window.__themeEvents || []).length")).GetInt32() > 0);

        var events = await session.EvaluateAsync("window.__themeEvents[0]");
        Assert.True(events.TryGetProperty("accentColor", out var accentColor));
        Assert.Matches("^#[0-9A-Fa-f]{6}$", accentColor.GetString() ?? string.Empty);

        var lastTheme = await session.EvaluateAsync(
            "window.HTWind.__lastTheme ? window.HTWind.__lastTheme.appTheme : null");
        Assert.Equal(JsonValueKind.String, lastTheme.ValueKind);
    }

    [Fact]
    public async Task SystemMetrics_ReturnsValidTelemetry()
    {
        Assert.True(await WaitForBridgeReadyAsync());

        var metrics = await session.InvokeAsync("system.metrics");
        Assert.True(GetBool(metrics, "ok"), GetError(metrics));

        var result = metrics.GetProperty("result");
        Assert.True(result.GetProperty("available").GetBoolean());
        Assert.True(result.TryGetProperty("memoryTotalBytes", out _));
        Assert.True(result.GetProperty("processCount").GetInt32() > 0);

        var cpu = result.GetProperty("cpuLoadPercent").GetDouble();
        Assert.InRange(cpu, 0, 100);

        var memoryPercent = result.GetProperty("memoryUsedPercent").GetDouble();
        Assert.InRange(memoryPercent, 0, 100);

        Assert.Equal(JsonValueKind.Array, result.GetProperty("disks").ValueKind);
    }

    [Fact]
    public async Task PowerShellExec_IsDeniedWithoutAuthorization()
    {
        Assert.True(await WaitForBridgeReadyAsync());
        session.PowerShellAuthorization = null;
        try
        {
            var result = await session.InvokeAsync(
                "powershell.exec", new { script = "Get-Date" });

            Assert.False(GetBool(result, "ok"));
            Assert.Contains(
                "authorization",
                GetError(result),
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            session.PowerShellAuthorization = PowerShellExecutionAuthorization.ReadOnly;
        }
    }

    [Fact]
    public async Task PowerShellExec_WorksWithReadOnlyAuthorization()
    {
        Assert.True(await WaitForBridgeReadyAsync());
        session.PowerShellAuthorization = PowerShellExecutionAuthorization.ReadOnly;
        try
        {
            var result = await session.InvokeAsync("powershell.exec", new
            {
                script = "Get-Date -Format 'yyyy-MM-dd'",
                timeoutMs = 15000,
            });

            Assert.True(GetBool(result, "ok"), GetError(result));
            var payload = result.GetProperty("result");
            Assert.False(payload.GetProperty("timedOut").GetBoolean());
            Assert.Equal("inproc-readonly", payload.GetProperty("shell").GetString());
            Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("output").GetString()));
            Assert.Equal(
                DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                payload.GetProperty("output").GetString()?.TrimEnd());
        }
        finally
        {
            session.PowerShellAuthorization = PowerShellExecutionAuthorization.ReadOnly;
        }
    }

    [Fact]
    public async Task SettingsInjection_ExposesSettingsAndRaisesHtwindsettingsEvent()
    {
        Assert.True(await WaitForBridgeReadyAsync());

        var storage = new WidgetStorageService();
        var settings = new JsonObject
        {
            ["accent"] = "#ff8800",
            ["refreshSeconds"] = 7,
        };
        storage.SaveSettings(session.WidgetFilePath, settings);

        var settingsJson = session.Api.GetWidgetSettingsJson(session.WidgetFilePath) ?? "null";
        await session.ExecuteHostScriptAsync(WidgetWindow.BuildSettingsInjectionScript(settingsJson));

        await session.EvaluateAsync(
            "(() => { window.__settingsEvents = []; " +
            "window.addEventListener('htwindsettings', (e) => window.__settingsEvents.push(e.detail)); return true; })()");

        // Re-inject as the widget window does after a settings change.
        await session.ExecuteHostScriptAsync(WidgetWindow.BuildSettingsInjectionScript(settingsJson));

        await WaitUntilAsync(async () =>
            (await session.EvaluateAsync("(window.__settingsEvents || []).length")).GetInt32() > 0);

        var readBack = await session.EvaluateAsync(
            "window.HTWind.getSettings()");
        Assert.Equal("#ff8800", readBack.GetProperty("accent").GetString());
        Assert.Equal(7, readBack.GetProperty("refreshSeconds").GetInt32());

        var eventDetail = await session.EvaluateAsync("window.__settingsEvents[0]");
        Assert.Equal(7, eventDetail.GetProperty("refreshSeconds").GetInt32());
    }

    [Fact]
    public async Task UnsupportedCommand_IsRejected()
    {
        Assert.True(await WaitForBridgeReadyAsync());

        var result = await session.InvokeAsync("not.a.command");
        Assert.False(GetBool(result, "ok"));
        Assert.Contains("Unsupported command", GetError(result));
    }

    private async Task<bool> WaitForBridgeReadyAsync()
    {
        await WaitUntilAsync(async () =>
            (await session.EvaluateAsync(
                "window.HTWind && window.HTWind.__bridgeReady === true")).GetBoolean());
        return true;
    }

    private static bool GetBool(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
    }

    private static string GetError(JsonElement element)
    {
        return element.TryGetProperty("error", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private async Task WaitUntilAsync(Func<Task<bool>> condition, int timeoutMs = 30000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(200);
        }

        Assert.Fail("Timed out waiting for the expected page state.");
    }
}
