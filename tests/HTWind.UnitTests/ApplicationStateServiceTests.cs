using System.IO;

using HTWind.Services.Application;
using Xunit;

namespace HTWind.UnitTests;

public sealed class ApplicationStateServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "htwind-state-", Guid.NewGuid().ToString("N"));

    [Fact]
    public void DeveloperMode_UsesDiskStateAndEmitsOnlyOnTransitions()
    {
        var service = new DeveloperModeService(_root);
        var changes = 0;
        service.Changed += (_, _) => changes++;

        Assert.False(service.IsEnabled());
        service.SetEnabled(false);
        Assert.Equal(0, changes);

        service.SetEnabled(true);
        Assert.True(service.IsEnabled());
        Assert.True(File.Exists(HTWindPathProvider.GetDeveloperModeFilePath(_root)));
        Assert.Equal(1, changes);
        Assert.Matches("^\\d{4}-\\d{2}-\\d{2}T", File.ReadAllText(HTWindPathProvider.GetDeveloperModeFilePath(_root)));

        // Repeated requests are no-ops, including when observed through a fresh instance.
        new DeveloperModeService(_root).SetEnabled(true);
        service.SetEnabled(true);
        Assert.Equal(1, changes);

        service.SetEnabled(false);
        Assert.False(service.IsEnabled());
        Assert.Equal(2, changes);
        service.SetEnabled(false);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void DeveloperMode_FileSystemFailureIsSwallowedButTransitionStillRaisesChanged()
    {
        Directory.CreateDirectory(_root);
        var blockingFile = Path.Combine(_root, "not-a-directory");
        File.WriteAllText(blockingFile, "block");
        var service = new DeveloperModeService(Path.Combine(blockingFile, "state"));
        var changes = 0;
        service.Changed += (_, _) => changes++;

        service.SetEnabled(true);

        Assert.False(service.IsEnabled());
        Assert.Equal(1, changes);
    }

    [Fact]
    public void BuiltInInitializationState_PersistsAcrossInstancesAndCanBeReaffirmed()
    {
        var service = new BuiltInWidgetInitializationStateService(_root);
        Assert.False(service.HasInitialized());

        service.MarkInitialized();

        Assert.True(service.HasInitialized());
        var path = HTWindPathProvider.GetBuiltInWidgetsInitializationFilePath(_root);
        Assert.True(File.Exists(path));
        Assert.NotEmpty(File.ReadAllText(path));
        Assert.True(new BuiltInWidgetInitializationStateService(_root).HasInitialized());

        service.MarkInitialized();
        Assert.True(service.HasInitialized());
    }

    [Fact]
    public void BuiltInInitializationState_ReportsDiskWriteFailure()
    {
        Directory.CreateDirectory(_root);
        var blocker = Path.Combine(_root, "blocker");
        File.WriteAllText(blocker, "file");
        var service = new BuiltInWidgetInitializationStateService(Path.Combine(blocker, "child"));

        Assert.ThrowsAny<IOException>(service.MarkInitialized);
        Assert.False(service.HasInitialized());
    }

    [Fact]
    public void ExecutionRiskConsent_PersistsAcceptancePerDataDirectory()
    {
        var service = new ExecutionRiskConsentService(_root);
        var other = new ExecutionRiskConsentService(_root + "-other");

        Assert.False(service.HasAccepted());
        Assert.False(other.HasAccepted());
        service.MarkAccepted();
        Assert.True(service.HasAccepted());
        Assert.False(other.HasAccepted());
        Assert.True(File.Exists(HTWindPathProvider.GetRiskConsentFilePath(_root)));
        Assert.NotEmpty(File.ReadAllText(HTWindPathProvider.GetRiskConsentFilePath(_root)));

        // Acceptance is idempotent at the observable contract: it remains accepted after repeats/reload.
        service.MarkAccepted();
        Assert.True(new ExecutionRiskConsentService(_root).HasAccepted());
    }

    [Fact]
    public void ExecutionRiskConsent_ReportsDiskWriteFailure()
    {
        Directory.CreateDirectory(_root);
        var blocker = Path.Combine(_root, "blocker");
        File.WriteAllText(blocker, "file");
        var service = new ExecutionRiskConsentService(Path.Combine(blocker, "child"));

        Assert.ThrowsAny<IOException>(service.MarkAccepted);
        Assert.False(service.HasAccepted());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        if (Directory.Exists(_root + "-other")) Directory.Delete(_root + "-other", recursive: true);
    }
}
