using System.Text.Json;

using HTWind.Services.PowerShellExecution;

namespace HTWind.Services.WebView;

public sealed class WidgetHostApiService : IWidgetHostApiService
{
    public const string PowerShellCommand = "powershell.exec";

    private readonly PowerShellExecutionEngine _powerShellEngine = new();
    private readonly SystemMetricsService _systemMetricsService = new();
    private readonly AudioVisualizerService _audioVisualizerService = new();
    private readonly MediaSessionService _mediaSessionService = new();
    private readonly WidgetStorageService _storageService = new();

    public event EventHandler<WidgetHostThemePayload>? ThemeChanged;

    public void PublishThemeChanged(WidgetHostThemePayload? payload = null)
    {
        ThemeChanged?.Invoke(this, payload ?? WidgetHostThemePayload.Build());
    }

    public string? GetWidgetSettingsJson(string? widgetFilePath)
    {
        return _storageService.GetSettings(widgetFilePath)?.ToJsonString(WidgetStorageService.SerializerOptions);
    }

    public void SaveWidgetSettings(string? widgetFilePath, System.Text.Json.Nodes.JsonObject settings)
    {
        _storageService.SaveSettings(widgetFilePath, settings);
    }

    public async Task<WidgetHostApiExecutionResult> ExecuteAsync(
        string command,
        JsonElement? args,
        WidgetExecutionContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return Fail("Command is required.");
        }

        try
        {
            return command switch
            {
                PowerShellCommand => Ok(await ExecutePowerShellAsync(args, context)),
                "system.metrics" => Ok(await _systemMetricsService.GetSnapshotAsync()),
                "audio.visualizer" => Ok(_audioVisualizerService.GetSnapshot(args)),
                "media.session" => Ok(await _mediaSessionService.HandleAsync(args)),
                "storage.get" => Ok(_storageService.Get(ResolveWidgetFilePath(context), args)),
                "storage.set" => Ok(_storageService.Set(ResolveWidgetFilePath(context), args)),
                "theme.get" => Ok(WidgetHostThemePayload.Build()),
                "theme.onChanged" => Ok(WidgetHostThemePayload.Build()),
                _ => Fail($"Unsupported command: {command}")
            };
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private static string? ResolveWidgetFilePath(WidgetExecutionContext? context)
    {
        var widgetFilePath = context?.WidgetFilePath;
        return string.IsNullOrWhiteSpace(widgetFilePath) ? null : widgetFilePath;
    }

    private async Task<object> ExecutePowerShellAsync(JsonElement? args, WidgetExecutionContext? context)
    {
        if (context?.PowerShellAuthorization is null)
        {
            throw new InvalidOperationException("PowerShell execution requires per-widget authorization.");
        }

        var script = ReadStringArg(args, "script");
        if (string.IsNullOrWhiteSpace(script))
        {
            throw new InvalidOperationException("'script' is required for powershell.exec.");
        }

        var timeoutMs = ReadIntArg(args, "timeoutMs") ?? 5000;
        timeoutMs = Math.Clamp(timeoutMs, 500, 120000);

        var maxOutputCharsArg = ReadIntArg(args, "maxOutputChars");
        int? maxOutputChars = maxOutputCharsArg.HasValue
            ? Math.Max(1, maxOutputCharsArg.Value)
            : null;

        var shell = (ReadStringArg(args, "shell") ?? "powershell").Trim().ToLowerInvariant();
        var shellExecutable = shell == "pwsh" ? "pwsh.exe" : "powershell.exe";

        var output = await _powerShellEngine.ExecuteAsync(new PowerShellExecutionRequest
        {
            Script = script,
            TimeoutMilliseconds = timeoutMs,
            MaxOutputChars = maxOutputChars,
            Shell = shellExecutable,
            WorkingDirectory = ReadStringArg(args, "workingDirectory"),
            Mode = context.PowerShellAuthorization == PowerShellExecutionAuthorization.ReadOnly
                ? PowerShellExecutionMode.ReadOnly
                : PowerShellExecutionMode.Full
        });

        return new
        {
            output.TimedOut,
            output.ExitCode,
            output.Output,
            output.Error,
            output.OutputTruncated,
            output.ErrorTruncated,
            Shell = output.Shell,
            output.WorkingDirectory
        };
    }

    private static string? ReadStringArg(JsonElement? args, string key)
    {
        return WidgetHostApiArgs.ReadStringArg(args, key);
    }

    private static int? ReadIntArg(JsonElement? args, string key)
    {
        return WidgetHostApiArgs.ReadIntArg(args, key);
    }

    private static WidgetHostApiExecutionResult Ok(object? result)
    {
        return new WidgetHostApiExecutionResult
        {
            Success = true,
            Result = result
        };
    }

    private static WidgetHostApiExecutionResult Fail(string error)
    {
        return new WidgetHostApiExecutionResult
        {
            Success = false,
            Error = error
        };
    }
}
