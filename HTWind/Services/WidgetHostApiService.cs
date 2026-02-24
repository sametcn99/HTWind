using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace HTWind.Services;

public sealed class WidgetHostApiService : IWidgetHostApiService
{
    public async Task<WidgetHostApiExecutionResult> ExecuteAsync(string command, JsonElement? args)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return Fail("Command is required.");
        }

        try
        {
            return command switch
            {
                "powershell.exec" => Ok(await ExecutePowerShellAsync(args)),
                _ => Fail($"Unsupported command: {command}")
            };
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private static async Task<object> ExecutePowerShellAsync(JsonElement? args)
    {
        var script = ReadStringArg(args, "script");
        if (string.IsNullOrWhiteSpace(script))
        {
            throw new InvalidOperationException("'script' is required for powershell.exec.");
        }

        var timeoutMs = ReadIntArg(args, "timeoutMs") ?? 5000;
        timeoutMs = Math.Clamp(timeoutMs, 500, 120000);

        var maxOutputChars = ReadIntArg(args, "maxOutputChars") ?? 12000;
        maxOutputChars = Math.Clamp(maxOutputChars, 256, 200000);

        var shell = (ReadStringArg(args, "shell") ?? "powershell").Trim().ToLowerInvariant();
        var shellExecutable = shell == "pwsh" ? "pwsh.exe" : "powershell.exe";

        var workingDirectoryArg = ReadStringArg(args, "workingDirectory");
        var workingDirectory = string.IsNullOrWhiteSpace(workingDirectoryArg)
            ? Environment.CurrentDirectory
            : Environment.ExpandEnvironmentVariables(workingDirectoryArg.Trim());

        if (!Directory.Exists(workingDirectory))
        {
            workingDirectory = Environment.CurrentDirectory;
        }

        var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        var startInfo = new ProcessStartInfo
        {
            FileName = shellExecutable,
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        var waitForExitTask = process.WaitForExitAsync();

        var timedOut = await Task.WhenAny(waitForExitTask, Task.Delay(timeoutMs)) != waitForExitTask;
        if (timedOut)
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
                // Ignore kill race conditions.
            }
        }

        var output = await outputTask;
        var error = await errorTask;

        var clippedOutput = output.Length > maxOutputChars
            ? output[..maxOutputChars]
            : output;
        var clippedError = error.Length > maxOutputChars
            ? error[..maxOutputChars]
            : error;

        return new
        {
            TimedOut = timedOut,
            ExitCode = timedOut ? (int?)null : process.ExitCode,
            Output = clippedOutput,
            Error = clippedError,
            OutputTruncated = output.Length > maxOutputChars,
            ErrorTruncated = error.Length > maxOutputChars,
            Shell = shellExecutable,
            WorkingDirectory = workingDirectory
        };
    }

    private static string? ReadStringArg(JsonElement? args, string key)
    {
        if (!args.HasValue || args.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return args.Value.TryGetProperty(key, out var value)
            ? value.GetString()
            : null;
    }

    private static int? ReadIntArg(JsonElement? args, string key)
    {
        if (!args.HasValue || args.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!args.Value.TryGetProperty(key, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
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
