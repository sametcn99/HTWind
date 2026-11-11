using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security;
using System.Text;

using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;

namespace HTWind.Services.PowerShellExecution;

/// <summary>
///     Executes PowerShell scripts in-process on transient runspaces (one per call) instead of
///     spawning a new OS process per call, keeping per-call session isolation at a fraction of
///     the process cost. A dedicated sandboxed session state (constrained language mode plus a
///     read-only cmdlet allowlist, with module auto-loading disabled) is used for widgets that
///     were only granted read-only access.
/// </summary>
public sealed class PowerShellExecutionEngine : IDisposable
{
    private const int TimeoutGracePeriodMilliseconds = 2000;
    private const int OutStringWidth = 8192;

    private static readonly string[] ReadOnlyCommandPrefixes =
    [
        "Get-",
        "Test-",
        "Resolve-",
        "Measure-",
        "Format-",
        "Read-",
        "Compare-",
        "Group-",
        "Select-",
        "Sort-",
        "Where-",
        "Write-",
        "ConvertTo-",
        "ConvertFrom-",
        "Trace-"
    ];

    private static readonly string[] ReadOnlyCommandNames =
    [
        "ForEach-Object",
        "Set-Location",
        "Push-Location",
        "Pop-Location",
        "Select-String",
        // Explicit Out-* entries only: Out-File would allow writes, so the "Out-" prefix is
        // deliberately not used.
        "Out-String",
        "Out-Default",
        "Out-Host",
        "Out-Null"
    ];

    private readonly object _sync = new();
    private static readonly EmbeddedHost SharedEmbeddedHost = new();
    private InitialSessionState? _fullTrustSessionState;
    private InitialSessionState? _readOnlySessionState;
    private bool _isDisposed;

    public PowerShellExecutionEngine()
    {
        // The embedded engine must never send telemetry from widget scripts.
        Environment.SetEnvironmentVariable("POWERSHELL_TELEMETRY_OPTOUT", "1");
    }

    public async Task<PowerShellExecutionOutput> ExecuteAsync(PowerShellExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (string.IsNullOrWhiteSpace(request.Script))
        {
            throw new ArgumentException("The PowerShell script must not be empty.", nameof(request));
        }

        var timeoutMilliseconds = Math.Clamp(request.TimeoutMilliseconds, 500, 120000);
        var workingDirectory = ResolveWorkingDirectory(request.WorkingDirectory);
        var initialSessionState = GetSessionState(request.Mode);

        // One transient runspace per call. This keeps per-call session isolation (matching the
        // old process-per-call semantics) and prevents any state/module import from one widget
        // call leaking into another; opening an in-process runspace is still ~50x cheaper than
        // spawning powershell.exe.
        var runspace = RunspaceFactory.CreateRunspace(SharedEmbeddedHost, initialSessionState);
        runspace.Open();

        try
        {
            using var powerShell = PowerShell.Create();
            powerShell.Runspace = runspace;

            if (request.Mode == PowerShellExecutionMode.ReadOnly)
            {
                // Belt-and-braces: module auto-loading could pull in commands the sandbox ISS
                // filter removed; disable it for the read-only session.
                powerShell.AddStatement().AddScript("$PSModuleAutoLoadingPreference = 'None'");
            }

            if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
            {
                // Only relocate when the caller asked for it: a fresh runspace starts in the
                // process working directory. In read-only mode this resolves to the sandbox's
                // Set-Location session function (see BuildReadOnlySessionState).
                powerShell.AddCommand("Set-Location").AddParameter("LiteralPath", workingDirectory);
            }

            powerShell.AddScript(request.Script);

            // Preserve the console-style formatting of the previous process-based bridge; strings
            // (such as ConvertTo-Json output) pass through unchanged.
            powerShell.AddCommand("Out-String").AddParameter("Width", OutStringWidth);

            var invokeTask = Task.Run(() => InvokeCore(powerShell));
            var timeoutTask = Task.Delay(timeoutMilliseconds);
            var completedTask = await Task.WhenAny(invokeTask, timeoutTask);

            var timedOut = completedTask != invokeTask;
            var abandoned = false;

            if (timedOut)
            {
                // PowerShell.Stop() can block indefinitely when the pipeline is executing
                // synchronously on another thread (observed with Start-Sleep), so bound it and
                // treat an unanswered stop as an abandoned pipeline.
                try
                {
                    await Task
                        .Run(() => powerShell.Stop())
                        .WaitAsync(TimeSpan.FromMilliseconds(TimeoutGracePeriodMilliseconds));
                }
                catch (TimeoutException)
                {
                    abandoned = true;
                }
                catch
                {
                    // The pipeline may already be closing.
                }

                if (!invokeTask.IsCompleted)
                {
                    abandoned = true;
                }
            }

            var outcome = invokeTask.Status == TaskStatus.RanToCompletion
                ? invokeTask.Result
                : new InvocationOutcome();

            if (abandoned)
            {
                // Keep the abandoned task observed; the runspace is torn down in finally below.
                _ = invokeTask.ContinueWith(
                    static t => _ = t.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
            }

            var output = outcome.Output;
            var error = outcome.Error;
            var outputTruncated = false;
            var errorTruncated = false;

            if (request.MaxOutputChars.HasValue)
            {
                outputTruncated = output.Length > request.MaxOutputChars.Value;
                output = outputTruncated ? output[..request.MaxOutputChars.Value] : output;
                errorTruncated = error.Length > request.MaxOutputChars.Value;
                error = errorTruncated ? error[..request.MaxOutputChars.Value] : error;
            }

            return new PowerShellExecutionOutput
            {
                TimedOut = timedOut,
                ExitCode = timedOut ? null : outcome.ExitCode,
                Output = output,
                Error = error,
                OutputTruncated = outputTruncated,
                ErrorTruncated = errorTruncated,
                Shell = request.Mode == PowerShellExecutionMode.ReadOnly ? "inproc-readonly" : "inproc-pwsh",
                WorkingDirectory = workingDirectory
            };
        }
        finally
        {
            await CloseRunspaceAsync(runspace);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        lock (_sync)
        {
            _fullTrustSessionState = null;
            _readOnlySessionState = null;
            _isDisposed = true;
        }

        GC.SuppressFinalize(this);
    }

    private static string ResolveWorkingDirectory(string? requestedWorkingDirectory)
    {
        if (string.IsNullOrWhiteSpace(requestedWorkingDirectory))
        {
            return Environment.CurrentDirectory;
        }

        var expanded = Environment.ExpandEnvironmentVariables(requestedWorkingDirectory.Trim());
        return Directory.Exists(expanded) ? expanded : Environment.CurrentDirectory;
    }

    /// <summary>Returns the (cached) session state for the mode; one ISS can spawn many runspaces.</summary>
    private InitialSessionState GetSessionState(PowerShellExecutionMode mode)
    {
        lock (_sync)
        {
            if (mode == PowerShellExecutionMode.ReadOnly)
            {
                _readOnlySessionState ??= BuildReadOnlySessionState();
                return _readOnlySessionState;
            }

            _fullTrustSessionState ??= BuildFullTrustSessionState();
            return _fullTrustSessionState;
        }
    }

    private static async Task CloseRunspaceAsync(Runspace runspace)
    {
        // Close()/Dispose() can block while an abandoned pipeline drains (e.g. Start-Sleep),
        // so run them with a bounded wait. On timeout the drain continues on the background
        // task while the caller has already returned.
        try
        {
            await Task
                .Run(async () =>
                {
                    try
                    {
                        runspace.Close();
                    }
                    catch
                    {
                        // A runspace hosting an abandoned pipeline can refuse Close.
                    }

                    try
                    {
                        runspace.Dispose();
                    }
                    catch
                    {
                    }
                })
                .WaitAsync(TimeSpan.FromSeconds(3));
        }
        catch
        {
            // Timed out or failed; the background task keeps the outcome observed.
        }
    }

    private static InitialSessionState BuildFullTrustSessionState()
    {
        var initialSessionState = InitialSessionState.CreateDefault2();
        initialSessionState.ApartmentState = ApartmentState.MTA;

        try
        {
            initialSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;
        }
        catch
        {
            // ExecutionPolicy is unavailable on some platforms; the default is acceptable.
        }

        return initialSessionState;
    }

    private InitialSessionState BuildReadOnlySessionState()
    {
        var initialSessionState = InitialSessionState.CreateDefault2();
        initialSessionState.ApartmentState = ApartmentState.MTA;
        initialSessionState.LanguageMode = PSLanguageMode.ConstrainedLanguage;

        try
        {
            initialSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;
        }
        catch
        {
            // ExecutionPolicy is unavailable on some platforms; the default is acceptable.
        }

        // Set module auto-loading off at session-state level (runspace-global, applied before any
        // command discovery): auto-loading could otherwise resurrect commands that the filter
        // below removed from imported modules.
        initialSessionState.Variables.Add(new SessionStateVariableEntry(
            "PSModuleAutoLoadingPreference",
            PSModuleAutoLoadingPreference.None,
            "Disables module auto-loading inside the HTWind read-only sandbox."));

        // Strip every command that is not on the read-only allowlist: non-read cmdlets,
        // functions, aliases and scripts are all removed so the sandbox only exposes queries.
        foreach (var entry in initialSessionState.Commands.ToArray())
        {
            var isAllowedReadCmdlet =
                entry.CommandType == CommandTypes.Cmdlet && IsReadOnlyCommand(entry.Name);

            if (!isAllowedReadCmdlet)
            {
                try
                {
                    initialSessionState.Commands.Remove(entry.Name, entry.CommandType);
                }
                catch
                {
                    // Duplicate-name removal races are harmless; the entry stays out of scope.
                }
            }
        }

        // With module auto-loading disabled, commands that live in the built-in modules never
        // resolve on their own: InitialSessionState only pre-populates Core commands, and the
        // other built-ins (Management/Utility/Security) load lazily through auto-loading.
        // Register explicit session-state entries for the allowlisted cmdlets implemented in
        // the bundled command assemblies so the designed allowlist actually exists in the
        // session. Nothing outside the allowlist is added, keeping the deny-by-absence model.
        //
        // Set-Location is special: manually registered cmdlet entries are constructed without
        // a runspace session-state binding, so a state-mutating cmdlet silently no-ops. A
        // session function that calls the session-state path API behaves like Set-Location
        // -LiteralPath with a correct binding (the engine relocates through the same API).
        initialSessionState.Commands.Add(new SessionStateFunctionEntry(
            "Set-Location",
            "param([Parameter(Position = 0)][AllowEmptyString()][string] $LiteralPath)\n" +
            "$ExecutionContext.SessionState.Path.SetLocation($LiteralPath)"));

        AddReadOnlyCmdletEntries(initialSessionState);
        AddReadOnlyProviderEntries(initialSessionState);

        return initialSessionState;
    }

    /// <summary>
    ///     Bundled assemblies that implement built-in read-only cmdlets. They ship with the
    ///     app's $PSHOME deployment; when they are absent (a bare SMA-only deployment) the
    ///     sandbox degrades to Core-only cmdlets.
    /// </summary>
    private static readonly string[] ReadOnlyCommandAssemblies =
    [
        "Microsoft.PowerShell.Commands.Management.dll",
        "Microsoft.PowerShell.Commands.Utility.dll",
        "Microsoft.PowerShell.Security.dll",
        "Microsoft.PowerShell.ConsoleHost.dll",
    ];

    private static void AddReadOnlyCmdletEntries(InitialSessionState initialSessionState)
    {
        var smaDirectory = Path.GetDirectoryName(typeof(Runspace).Assembly.Location);
        if (string.IsNullOrEmpty(smaDirectory))
        {
            return;
        }

        var existingNames = new HashSet<string>(
            initialSessionState.Commands.Select(static entry => entry.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var assemblyName in ReadOnlyCommandAssemblies)
        {
            var assemblyPath = Path.Combine(smaDirectory, assemblyName);
            if (!File.Exists(assemblyPath))
            {
                continue;
            }

            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(assemblyPath);
            }
            catch
            {
                continue;
            }

            foreach (var type in SafeGetTypes(assembly))
            {
                if (!typeof(Cmdlet).IsAssignableFrom(type) || type.IsAbstract)
                {
                    continue;
                }

                var cmdletAttribute = type.GetCustomAttributes<CmdletAttribute>()
                    .FirstOrDefault();
                if (cmdletAttribute is null)
                {
                    continue;
                }

                var commandName = $"{cmdletAttribute.VerbName}-{cmdletAttribute.NounName}";
                if (!IsReadOnlyCommand(commandName) || existingNames.Contains(commandName))
                {
                    continue;
                }

                try
                {
                    initialSessionState.Commands.Add(
                        new SessionStateCmdletEntry(commandName, type, null));
                    existingNames.Add(commandName);
                }
                catch
                {
                    // A single unusable cmdlet entry must not break the sandbox assembly.
                }
            }
        }
    }

    private static Type[] SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException reflectionException)
        {
            // Partial load is expected: the bundled command assemblies reference optional
            // dependencies (MarkdownRender, CodeAnalysis). Keep every type that did load.
            return reflectionException.Types
                .Where(type => type is not null)
                .Cast<Type>()
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Providers required by the read-only navigation cmdlets.</summary>
    private static readonly (string ProviderName, string TypeName, string[] AssemblyNames)[]
        ReadOnlyProviders =
        [
            ("FileSystem", "FileSystemProvider", ["System.Management.Automation.dll", "Microsoft.PowerShell.Commands.Management.dll"]),
            ("Environment", "EnvironmentProvider", ["System.Management.Automation.dll", "Microsoft.PowerShell.Commands.Management.dll"]),
            ("Registry", "RegistryProvider", ["System.Management.Automation.dll", "Microsoft.PowerShell.Commands.Management.dll"]),
            ("Variable", "VariableProvider", ["System.Management.Automation.dll"]),
        ];

    private static void AddReadOnlyProviderEntries(InitialSessionState initialSessionState)
    {
        var smaDirectory = Path.GetDirectoryName(typeof(Runspace).Assembly.Location);
        if (string.IsNullOrEmpty(smaDirectory))
        {
            return;
        }

        foreach (var (providerName, typeName, assemblyNames) in ReadOnlyProviders)
        {
            try
            {
                var providerType = assemblyNames
                    .Select(assemblyName => ResolveTypeIn(smaDirectory, assemblyName, typeName))
                    .FirstOrDefault(type => type is not null);
                if (providerType is null)
                {
                    continue;
                }

                initialSessionState.Providers.Add(
                    new SessionStateProviderEntry(providerName, providerType, null));
            }
            catch
            {
                // Provider registration is best effort; navigation cmdlets degrade gracefully.
            }
        }
    }

    private static Type? ResolveTypeIn(string directory, string assemblyName, string typeName)
    {
        try
        {
            var assemblyPath = Path.Combine(directory, assemblyName);
            if (!File.Exists(assemblyPath))
            {
                return null;
            }

            var assembly = Assembly.LoadFrom(assemblyPath);
            return SafeGetTypes(assembly).FirstOrDefault(type => type.Name == typeName);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsReadOnlyCommand(string commandName)
    {
        foreach (var prefix in ReadOnlyCommandPrefixes)
        {
            if (commandName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var explicitName in ReadOnlyCommandNames)
        {
            if (string.Equals(commandName, explicitName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static InvocationOutcome InvokeCore(PowerShell powerShell)
    {
        var outcome = new InvocationOutcome();

        try
        {
            var results = powerShell.Invoke();
            var builder = new StringBuilder();

            foreach (var item in results)
            {
                if (item is null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                if (item.BaseObject is string text)
                {
                    builder.Append(text);
                }
                else
                {
                    builder.Append(item.ToString());
                }
            }

            outcome.Output = builder.ToString();

            var errorBuilder = new StringBuilder();
            foreach (var errorRecord in powerShell.Streams.Error)
            {
                if (string.IsNullOrWhiteSpace(errorRecord?.ToString()))
                {
                    continue;
                }

                if (errorBuilder.Length > 0)
                {
                    errorBuilder.AppendLine();
                }

                errorBuilder.Append(errorRecord.ToString());
            }

            outcome.Error = errorBuilder.ToString();
            outcome.ExitCode = powerShell.HadErrors && outcome.Error.Length > 0 && outcome.Output.Length == 0 ? -1 : 0;
        }
        catch (Exception exception)
        {
            outcome.Output = outcome.Output.Length > 0 ? outcome.Output : string.Empty;
            outcome.Error = exception.Message;
            outcome.ExitCode = -1;
        }

        return outcome;
    }

    private sealed class InvocationOutcome
    {
        public string Output { get; set; } = string.Empty;

        public string Error { get; set; } = string.Empty;

        public int? ExitCode { get; set; }
    }

    /// <summary>
    ///     Minimal non-interactive host required by RunspaceFactory.CreateRunspace (a null host
    ///     is rejected). Mirrors powershell -NonInteractive: prompts fail cleanly, console
    ///     writes are discarded, and no raw console UI is exposed.
    /// </summary>
    private sealed class EmbeddedHost : PSHost
    {
        private readonly EmbeddedHostUserInterface _ui = new();
        private readonly Guid _instanceId = Guid.NewGuid();

        public override string Name => "HTWind-Embedded";

        public override Guid InstanceId => _instanceId;

        public override Version Version => new Version(1, 0);

        public override PSHostUserInterface UI => _ui;

        public override CultureInfo CurrentCulture => CultureInfo.InvariantCulture;

        public override CultureInfo CurrentUICulture => CultureInfo.InvariantCulture;

        public override PSObject PrivateData => new();

        public override bool DebuggerEnabled { get; set; }

        public override void SetShouldExit(int exitCode)
        {
            _ = exitCode;
        }

        public override void EnterNestedPrompt()
        {
            throw new PSInvalidOperationException("Nested prompts are not supported by the HTWind embedded host.");
        }

        public override void ExitNestedPrompt()
        {
            throw new PSInvalidOperationException("Nested prompts are not supported by the HTWind embedded host.");
        }

        public override void NotifyBeginApplication()
        {
        }

        public override void NotifyEndApplication()
        {
        }
    }

    /// <summary>Non-interactive host UI: console output is discarded and prompts are rejected.</summary>
    private sealed class EmbeddedHostUserInterface : PSHostUserInterface
    {
        private const string NotSupportedMessage =
            "Interactive prompts are not supported by the HTWind embedded host.";

        private readonly EmbeddedHostRawUserInterface _rawUi = new();

        public override PSHostRawUserInterface RawUI => _rawUi;

        public override bool SupportsVirtualTerminal => false;

        public override string ReadLine()
        {
            throw new PSInvalidOperationException(NotSupportedMessage);
        }

        public override SecureString ReadLineAsSecureString()
        {
            throw new PSInvalidOperationException(NotSupportedMessage);
        }

        public override void Write(string value)
        {
            _ = value;
        }

        public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value)
        {
            _ = foregroundColor;
            _ = backgroundColor;
            _ = value;
        }

        public override void WriteLine()
        {
        }

        public override void WriteLine(string value)
        {
            _ = value;
        }

        public override void WriteLine(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value)
        {
            _ = foregroundColor;
            _ = backgroundColor;
            _ = value;
        }

        public override void WriteErrorLine(string value)
        {
            _ = value;
        }

        public override void WriteDebugLine(string value)
        {
            _ = value;
        }

        public override void WriteProgress(long sourceId, ProgressRecord record)
        {
            _ = sourceId;
            _ = record;
        }

        public override void WriteVerboseLine(string value)
        {
            _ = value;
        }

        public override void WriteWarningLine(string value)
        {
            _ = value;
        }

        public override void WriteInformation(InformationRecord informationRecord)
        {
            _ = informationRecord;
        }

        public override Dictionary<string, PSObject> Prompt(
            string caption,
            string message,
            Collection<FieldDescription> descriptions)
        {
            _ = caption;
            _ = message;
            _ = descriptions;
            throw new PSInvalidOperationException(NotSupportedMessage);
        }

        public override PSCredential PromptForCredential(
            string caption,
            string message,
            string userName,
            string targetName)
        {
            _ = caption;
            _ = message;
            _ = userName;
            _ = targetName;
            throw new PSInvalidOperationException(NotSupportedMessage);
        }

        public override PSCredential PromptForCredential(
            string caption,
            string message,
            string userName,
            string targetName,
            PSCredentialTypes allowedCredentialTypes,
            PSCredentialUIOptions options)
        {
            _ = caption;
            _ = message;
            _ = userName;
            _ = targetName;
            _ = allowedCredentialTypes;
            _ = options;
            throw new PSInvalidOperationException(NotSupportedMessage);
        }

        public override int PromptForChoice(
            string caption,
            string message,
            Collection<ChoiceDescription> choices,
            int defaultChoice)
        {
            _ = caption;
            _ = message;
            _ = choices;
            _ = defaultChoice;
            throw new PSInvalidOperationException(NotSupportedMessage);
        }
    }

    /// <summary>
    ///     Stub raw console UI with a fixed 120-column virtual console. Runspace opening
    ///     (e.g. PSStyle color initialization) queries RawUI, so it must return sane values
    ///     instead of throwing; interactive input members still fail cleanly.
    /// </summary>
    private sealed class EmbeddedHostRawUserInterface : PSHostRawUserInterface
    {
        private const string NotSupportedMessage =
            "The HTWind embedded host does not expose a raw console UI.";

        private const int ConsoleWidth = 120;

        public override ConsoleColor ForegroundColor { get; set; } = ConsoleColor.Gray;

        public override ConsoleColor BackgroundColor { get; set; } = ConsoleColor.Black;

        public override Coordinates CursorPosition { get; set; } = new(0, 0);

        public override Coordinates WindowPosition { get; set; } = new(0, 0);

        public override int CursorSize { get; set; } = 25;

        public override Size BufferSize { get; set; } = new(ConsoleWidth, 4000);

        public override Size WindowSize { get; set; } = new(ConsoleWidth, 50);

        public override Size MaxWindowSize => new(ConsoleWidth, 100);

        public override Size MaxPhysicalWindowSize => new(ConsoleWidth, 100);

        public override bool KeyAvailable => false;

        public override string WindowTitle { get; set; } = "HTWind";

        public override KeyInfo ReadKey(ReadKeyOptions options)
        {
            _ = options;
            throw new PSInvalidOperationException(NotSupportedMessage);
        }

        public override void FlushInputBuffer()
        {
        }

        public override void SetBufferContents(Coordinates origin, BufferCell[,] buffer)
        {
            _ = origin;
            _ = buffer;
        }

        public override void SetBufferContents(Rectangle rectangle, BufferCell fill)
        {
            _ = rectangle;
            _ = fill;
        }

        public override BufferCell[,] GetBufferContents(Rectangle rectangle)
        {
            _ = rectangle;
            return new BufferCell[0, 0];
        }

        public override void ScrollBufferContents(
            Rectangle source,
            Coordinates destination,
            Rectangle clip,
            BufferCell fill)
        {
            _ = source;
            _ = destination;
            _ = clip;
            _ = fill;
        }

        public override int LengthInBufferCells(string source)
        {
            return source?.Length ?? 0;
        }

        public override int LengthInBufferCells(string source, int offset)
        {
            return Math.Max(0, (source?.Length ?? 0) - Math.Max(0, offset));
        }

        public override int LengthInBufferCells(char source)
        {
            _ = source;
            return 1;
        }
    }
}
