namespace HTWind.Services.PowerShellExecution;

public enum PowerShellExecutionMode
{
    /// <summary>Unrestricted script execution.</summary>
    Full,

    /// <summary>Sandboxed execution restricted to read-only cmdlets in constrained language mode.</summary>
    ReadOnly
}

public sealed class PowerShellExecutionRequest
{
    public string Script { get; init; } = string.Empty;

    public int TimeoutMilliseconds { get; init; } = 5000;

    public int? MaxOutputChars { get; init; }

    public string Shell { get; init; } = "powershell";

    public string? WorkingDirectory { get; init; }

    public PowerShellExecutionMode Mode { get; init; } = PowerShellExecutionMode.Full;
}

public sealed class PowerShellExecutionOutput
{
    public bool TimedOut { get; init; }

    public int? ExitCode { get; init; }

    public string Output { get; init; } = string.Empty;

    public string Error { get; init; } = string.Empty;

    public bool OutputTruncated { get; init; }

    public bool ErrorTruncated { get; init; }

    public string Shell { get; init; } = string.Empty;

    public string WorkingDirectory { get; init; } = string.Empty;
}
