using System.Diagnostics;
using System.IO;

using HTWind.RuntimeTests.Support;
using HTWind.Services.PowerShellExecution;

using Xunit;

namespace HTWind.RuntimeTests;

/// <summary>
///     Exercises the real in-process PowerShell execution engine in read-only sandbox mode:
///     allowed read cmdlets run, mutating commands are rejected, and the session is locked to
///     constrained language mode.
/// </summary>
public sealed class PowerShellSandboxTests
{
    private static async Task<PowerShellExecutionOutput> ExecuteAsync(
        string script,
        string? workingDirectory = null)
    {
        using var engine = new PowerShellExecutionEngine();
        return await engine.ExecuteAsync(new PowerShellExecutionRequest
        {
            Script = script,
            TimeoutMilliseconds = 15000,
            WorkingDirectory = workingDirectory,
            Mode = PowerShellExecutionMode.ReadOnly,
        });
    }

    [Theory]
    [InlineData("Get-Date -Format 'yyyy-MM-dd'")]
    [InlineData("Get-Process | Select-Object -First 1 | Out-String")]
    [InlineData("@{ a = 1; b = 2 } | ConvertTo-Json")]
    [InlineData("1..5 | Measure-Object -Sum | Out-String")]
    [InlineData("1..10 | Where-Object { $_ % 2 -eq 0 } | Sort-Object -Descending | Out-String")]
    [InlineData("Test-Path 'C:\\Windows'")]
    [InlineData("Resolve-Path 'C:\\Windows' | Out-String")]
    [InlineData("'{}' | ConvertFrom-Json | Out-String")]
    [InlineData("Get-ChildItem Env: | Select-Object -First 1 | Out-String")]
    public async Task ReadOnlyMode_AllowsReadCmdlets(string script)
    {
        var output = await ExecuteAsync(script);

        Assert.False(output.TimedOut);
        Assert.Equal(0, output.ExitCode);
        Assert.Equal("inproc-readonly", output.Shell);
        Assert.Equal(string.Empty, output.Error);
        Assert.NotEqual(string.Empty, output.Output);
    }

    [Theory]
    [InlineData("Remove-Item")]
    [InlineData("New-Item")]
    [InlineData("Set-Content")]
    [InlineData("Out-File")]
    [InlineData("Start-Process")]
    [InlineData("Invoke-WebRequest")]
    [InlineData("Invoke-Expression")]
    [InlineData("Invoke-RestMethod")]
    [InlineData("Copy-Item")]
    [InlineData("Stop-Process")]
    public async Task ReadOnlyMode_BlocksMutatingCommands(string commandName)
    {
        var output = await ExecuteAsync(commandName);

        Assert.Equal(-1, output.ExitCode);
        Assert.Contains(commandName, output.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadOnlyMode_NewItemDoesNotCreateTheFile()
    {
        var target = Path.Combine(TestPaths.CreateTempDirectory("sandbox"), "should-not-exist.txt");

        var output = await ExecuteAsync($"New-Item -Path '{target}' -ItemType File");

        Assert.Equal(-1, output.ExitCode);
        Assert.False(File.Exists(target), "ReadOnly sandbox must not allow file creation.");
    }

    [Fact]
    public async Task ReadOnlyMode_OutFileDoesNotWrite()
    {
        var target = Path.Combine(TestPaths.CreateTempDirectory("sandbox"), "out-file.txt");

        var output = await ExecuteAsync($"'hello' | Out-File -FilePath '{target}'");

        Assert.Equal(-1, output.ExitCode);
        Assert.False(File.Exists(target), "Out-File is deliberately excluded from the allowlist.");
    }

    [Fact]
    public async Task ReadOnlyMode_UsesConstrainedLanguageMode()
    {
        var output = await ExecuteAsync("$ExecutionContext.SessionState.LanguageMode");

        Assert.Equal(0, output.ExitCode);
        Assert.Contains("Constrained", output.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadOnlyMode_BlocksDotNetTypeInvocation()
    {
        var target = Path.Combine(TestPaths.CreateTempDirectory("sandbox"), "cl-block.txt");

        var output = await ExecuteAsync(
            $"[System.IO.File]::WriteAllText('{target}', 'this must never be written')");

        Assert.Equal(-1, output.ExitCode);
        Assert.NotEqual(string.Empty, output.Error);
        Assert.False(File.Exists(target), "Constrained language must block .NET type invocation.");
    }

    [Fact]
    public async Task ReadOnlyMode_BlocksAddType()
    {
        var output = await ExecuteAsync("Add-Type -TypeDefinition 'public class X {}'");

        Assert.Equal(-1, output.ExitCode);
        Assert.NotEqual(string.Empty, output.Error);
    }

    [Fact]
    public async Task ReadOnlyMode_DisablesModuleAutoLoading()
    {
        var output = await ExecuteAsync("$PSModuleAutoLoadingPreference");

        Assert.Equal(0, output.ExitCode);
        Assert.Contains("None", output.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadOnlyMode_DoesNotAutoLoadRemovedModuleCmdlets()
    {
        // Start-Transcript lives in Microsoft.PowerShell.Host; auto-loading would resurrect it.
        var output = await ExecuteAsync("Start-Transcript");

        Assert.Equal(-1, output.ExitCode);
        Assert.Contains("Start-Transcript", output.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FullMode_UsesFullLanguageMode()
    {
        using var engine = new PowerShellExecutionEngine();
        var output = await engine.ExecuteAsync(new PowerShellExecutionRequest
        {
            Script = "$ExecutionContext.SessionState.LanguageMode",
            Mode = PowerShellExecutionMode.Full,
        });

        Assert.Equal(0, output.ExitCode);
        Assert.Equal("inproc-pwsh", output.Shell);
        Assert.Contains("Full", output.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FullMode_CanWriteFilesInsideItsWorkingDirectory()
    {
        var workingDirectory = TestPaths.CreateTempDirectory("full-mode");
        var target = Path.Combine(workingDirectory, "written.txt");

        using var engine = new PowerShellExecutionEngine();
        var output = await engine.ExecuteAsync(new PowerShellExecutionRequest
        {
            Script = $"Set-Content -LiteralPath '{target}' -Value 'ok'",
            TimeoutMilliseconds = 15000,
            WorkingDirectory = workingDirectory,
            Mode = PowerShellExecutionMode.Full,
        });

        Assert.Equal(0, output.ExitCode);
        Assert.True(File.Exists(target), "Full mode must retain normal file capabilities.");
    }

    [Fact]
    public async Task ExecuteAsync_RejectsEmptyScripts()
    {
        using var engine = new PowerShellExecutionEngine();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            engine.ExecuteAsync(new PowerShellExecutionRequest { Script = "  " }));
    }
}

/// <summary>
///     Isolation and robustness behavior of the real engine: transient runspaces, timeouts,
///     output truncation, and immediate rejection of interactive prompts.
/// </summary>
public sealed class PowerShellIsolationTests
{
    [Fact]
    public async Task GlobalVariables_DoNotLeakBetweenCalls()
    {
        using var engine = new PowerShellExecutionEngine();

        var first = await engine.ExecuteAsync(new PowerShellExecutionRequest
        {
            Script = "$global:leak = 'sentinel-value'",
            Mode = PowerShellExecutionMode.ReadOnly,
        });

        var second = await engine.ExecuteAsync(new PowerShellExecutionRequest
        {
            Script = "if ($global:leak) { $global:leak }",
            Mode = PowerShellExecutionMode.ReadOnly,
        });

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.Equal(string.Empty, second.Output.Trim('\r', '\n'));
    }

    [Fact]
    public async Task TimeoutMilliseconds_TimesOutPromptly()
    {
        using var engine = new PowerShellExecutionEngine();
        var stopwatch = Stopwatch.StartNew();

        var output = await engine.ExecuteAsync(new PowerShellExecutionRequest
        {
            // Full mode on purpose: Start-Sleep is not on the read-only allowlist.
            Script = "Start-Sleep -Seconds 30",
            TimeoutMilliseconds = 600,
            Mode = PowerShellExecutionMode.Full,
        });
        stopwatch.Stop();

        Assert.True(output.TimedOut, "A 30s script under a 600ms budget must time out.");
        Assert.Null(output.ExitCode);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Timeout handling took {stopwatch.Elapsed}; expected a prompt return.");
    }

    [Fact]
    public async Task MaxOutputChars_TruncatesStdout()
    {
        using var engine = new PowerShellExecutionEngine();

        var output = await engine.ExecuteAsync(new PowerShellExecutionRequest
        {
            Script = "Write-Output ('a' * 5000)",
            MaxOutputChars = 100,
            Mode = PowerShellExecutionMode.ReadOnly,
        });

        Assert.Equal(100, output.Output.Length);
        Assert.True(output.OutputTruncated);
    }

    [Fact]
    public async Task MaxOutputChars_TruncatesStderr()
    {
        using var engine = new PowerShellExecutionEngine();

        var output = await engine.ExecuteAsync(new PowerShellExecutionRequest
        {
            Script = "1..3 | ForEach-Object { Write-Error ('E' * 4000) }",
            MaxOutputChars = 200,
            Mode = PowerShellExecutionMode.ReadOnly,
        });

        Assert.Equal(200, output.Error.Length);
        Assert.True(output.ErrorTruncated);
    }

    [Theory]
    [InlineData("Read-Host 'name'")]
    [InlineData("$cred = Get-Credential")]
    [InlineData("Read-Host -AsSecureString 'password'")]
    public async Task InteractivePrompts_AreRejectedImmediately(string script)
    {
        using var engine = new PowerShellExecutionEngine();
        var stopwatch = Stopwatch.StartNew();

        var output = await engine.ExecuteAsync(new PowerShellExecutionRequest
        {
            Script = script,
            TimeoutMilliseconds = 30000,
            Mode = PowerShellExecutionMode.ReadOnly,
        });
        stopwatch.Stop();

        Assert.Equal(-1, output.ExitCode);
        Assert.NotEqual(string.Empty, output.Error);
        Assert.False(output.TimedOut, "Prompts must be rejected by the host, not by the timeout.");
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Prompt rejection took {stopwatch.Elapsed}; expected an immediate failure.");
    }

    [Fact]
    public async Task WorkingDirectory_IsAppliedToTheTransientRunspace()
    {
        var workingDirectory = TestPaths.CreateTempDirectory("cwd");

        using var engine = new PowerShellExecutionEngine();
        var output = await engine.ExecuteAsync(new PowerShellExecutionRequest
        {
            Script = "(Get-Location).Path",
            WorkingDirectory = workingDirectory,
            Mode = PowerShellExecutionMode.ReadOnly,
        });

        Assert.Equal(0, output.ExitCode);
        Assert.Contains(
            output.WorkingDirectory,
            output.Output,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidWorkingDirectory_FallsBackToCurrentDirectory()
    {
        using var engine = new PowerShellExecutionEngine();
        var output = await engine.ExecuteAsync(new PowerShellExecutionRequest
        {
            Script = "(Get-Location).Path",
            WorkingDirectory = "Z:\\definitely\\missing",
            Mode = PowerShellExecutionMode.ReadOnly,
        });

        Assert.Equal(0, output.ExitCode);
        Assert.Equal(Environment.CurrentDirectory, output.WorkingDirectory);
    }
}
