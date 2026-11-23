using System.IO;

using HTWind.RuntimeTests.Support;
using HTWind.Services.PowerShellExecution;

using Xunit;

namespace HTWind.RuntimeTests;

public sealed class PowerShellEngineEdgeCaseTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n\t")]
    public async Task InvalidScript_IsRejectedBeforeExecution(string? script)
    {
        using var engine = new PowerShellExecutionEngine();
        await Assert.ThrowsAsync<ArgumentException>(() => engine.ExecuteAsync(new PowerShellExecutionRequest { Script = script! }));
    }

    [Fact]
    public async Task NullRequestAndRepeatedDispose_HavePredictableBehavior()
    {
        var engine = new PowerShellExecutionEngine();
        await Assert.ThrowsAsync<ArgumentNullException>(() => engine.ExecuteAsync(null!));
        engine.Dispose();
        engine.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.ExecuteAsync(new PowerShellExecutionRequest { Script = "'unreachable'" }));
    }

    [Theory]
    [InlineData(PowerShellExecutionMode.Full)]
    [InlineData(PowerShellExecutionMode.ReadOnly)]
    public async Task OutputLimit_ZeroSuppressesBothStreamsAndKeepsNonTerminatingErrorStatus(PowerShellExecutionMode mode)
    {
        using var engine = new PowerShellExecutionEngine();
        var result = await engine.ExecuteAsync(new PowerShellExecutionRequest
        {
            Script = "'visible'; Write-Error 'failed'", Mode = mode, MaxOutputChars = 0
        });

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Empty(result.Output);
        Assert.Empty(result.Error);
        Assert.True(result.OutputTruncated);
        Assert.True(result.ErrorTruncated);
    }

    [Fact]
    public async Task OutputLimit_ExactBoundaryDoesNotReportTruncation()
    {
        using var engine = new PowerShellExecutionEngine();
        var request = new PowerShellExecutionRequest { Script = "'boundary'", Mode = PowerShellExecutionMode.ReadOnly };
        var full = await engine.ExecuteAsync(request);
        var limited = await engine.ExecuteAsync(new PowerShellExecutionRequest
        {
            Script = request.Script, Mode = request.Mode, MaxOutputChars = full.Output.Length
        });

        Assert.Equal(0, limited.ExitCode);
        Assert.Equal(full.Output, limited.Output);
        Assert.False(limited.OutputTruncated);
        Assert.False(limited.ErrorTruncated);
    }

    [Fact]
    public async Task FailedAndTimedOutCalls_DoNotPoisonFollowingCalls()
    {
        using var engine = new PowerShellExecutionEngine();
        var failed = await engine.ExecuteAsync(new PowerShellExecutionRequest { Script = "throw 'expected failure'" });
        var timedOut = await engine.ExecuteAsync(new PowerShellExecutionRequest { Script = "Start-Sleep -Seconds 30", TimeoutMilliseconds = 500 });
        var recovered = await engine.ExecuteAsync(new PowerShellExecutionRequest
        {
            Script = "'recovered'", Mode = PowerShellExecutionMode.ReadOnly
        });

        Assert.Equal(-1, failed.ExitCode);
        Assert.Contains("expected failure", failed.Error);
        Assert.True(timedOut.TimedOut);
        Assert.Null(timedOut.ExitCode);
        Assert.Equal(0, recovered.ExitCode);
        Assert.Equal("recovered", recovered.Output.Trim());
        Assert.Empty(recovered.Error);
    }

    [Fact]
    public async Task ConcurrentCalls_KeepVariablesAndModesIsolated()
    {
        using var engine = new PowerShellExecutionEngine();
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => engine.ExecuteAsync(new PowerShellExecutionRequest
        {
            Script = $"$global:identity = '{index}'; \"$global:identity|$($ExecutionContext.SessionState.LanguageMode)\"",
            Mode = index % 2 == 0 ? PowerShellExecutionMode.Full : PowerShellExecutionMode.ReadOnly,
            TimeoutMilliseconds = 15000
        })));

        for (var index = 0; index < results.Length; index++)
        {
            Assert.Equal(0, results[index].ExitCode);
            Assert.Equal($"{index}|{(index % 2 == 0 ? "FullLanguage" : "ConstrainedLanguage")}", results[index].Output.Trim());
            Assert.Empty(results[index].Error);
        }
        var next = await engine.ExecuteAsync(new PowerShellExecutionRequest { Script = "$global:identity" });
        Assert.Equal(0, next.ExitCode);
        Assert.Empty(next.Output.Trim());
    }

    [Theory]
    [InlineData(PowerShellExecutionMode.Full)]
    [InlineData(PowerShellExecutionMode.ReadOnly)]
    public async Task WorkingDirectory_WithQuotesBracketsAndSpacesIsUsedLiterally(PowerShellExecutionMode mode)
    {
        var root = TestPaths.CreateTempDirectory("literal-cwd");
        var directory = Directory.CreateDirectory(Path.Combine(root, "widget's [draft] folder")).FullName;
        try
        {
            using var engine = new PowerShellExecutionEngine();
            var result = await engine.ExecuteAsync(new PowerShellExecutionRequest
            {
                Script = "(Get-Location).Path", WorkingDirectory = directory, Mode = mode
            });
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(directory, result.Output.Trim());
            Assert.Equal(directory, result.WorkingDirectory);
            Assert.Empty(result.Error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
