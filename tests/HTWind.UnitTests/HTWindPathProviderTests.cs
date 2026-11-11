using System.IO;

using HTWind.Services.Application;

using Xunit;

namespace HTWind.UnitTests;

/// <summary>
///     The central path helper: explicit basePath (tests) &gt; HTWIND_DATA_DIR (E2E process
///     isolation) &gt; per-user application data (production default).
/// </summary>
public sealed class HTWindPathProviderTests : IDisposable
{
    private const string EnvVariable = HTWind.Services.Application.HTWindPathProvider
        .DataDirectoryEnvironmentVariable;
    private readonly string? _originalEnvironmentValue;

    public HTWindPathProviderTests()
    {
        _originalEnvironmentValue = Environment.GetEnvironmentVariable(EnvVariable);
        Environment.SetEnvironmentVariable(EnvVariable, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EnvVariable, _originalEnvironmentValue);
    }

    [Fact]
    public void GetRootDirectory_WithoutOverrides_UsesPerUserApplicationData()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HTWind");

        Assert.Equal(expected, HTWind.Services.Application.HTWindPathProvider.GetRootDirectory());
    }

    [Fact]
    public void GetRootDirectory_EnvironmentVariable_OverridesDefault()
    {
        var isolated = Path.Combine(Path.GetTempPath(), "htwind-env-override");
        Environment.SetEnvironmentVariable(EnvVariable, isolated);

        Assert.Equal(
            isolated,
            HTWind.Services.Application.HTWindPathProvider.GetRootDirectory());
        Assert.Equal(
            Path.Combine(isolated, "widgets-state.json"),
            HTWind.Services.Application.HTWindPathProvider.GetStateFilePath());
    }

    [Fact]
    public void GetRootDirectory_ExplicitBasePath_WinsOverEnvironmentVariable()
    {
        var isolated = Path.Combine(Path.GetTempPath(), "htwind-env-ignored");
        var explicitPath = Path.Combine(Path.GetTempPath(), "htwind-explicit");
        Environment.SetEnvironmentVariable(EnvVariable, isolated);

        Assert.Equal(
            explicitPath,
            HTWind.Services.Application.HTWindPathProvider.GetRootDirectory(explicitPath));
    }

    [Fact]
    public void WellKnownFiles_LiveInsideTheBasePath()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "htwind-wellknown");

        Assert.Equal(
            Path.Combine(basePath, "widgets-state.json"),
            HTWind.Services.Application.HTWindPathProvider.GetStateFilePath(basePath));
        Assert.Equal(
            Path.Combine(basePath, "Widgets"),
            HTWind.Services.Application.HTWindPathProvider.GetWidgetsDirectory(basePath));
        Assert.Equal(
            Path.Combine(basePath, "workspace-profiles.json"),
            HTWind.Services.Application.HTWindPathProvider.GetWorkspaceProfilesFilePath(basePath));
        Assert.Equal(
            Path.Combine(basePath, "BuiltInTemplates"),
            HTWind.Services.Application.HTWindPathProvider.GetTemplateDirectory(basePath));
        Assert.Equal(
            Path.Combine(basePath, "widget-permissions.json"),
            HTWind.Services.Application.HTWindPathProvider.GetPermissionsFilePath(basePath));
        Assert.Equal(
            Path.Combine(basePath, "developer-mode.enabled"),
            HTWind.Services.Application.HTWindPathProvider.GetDeveloperModeFilePath(basePath));
        Assert.Equal(
            Path.Combine(basePath, "risk-consent.accepted"),
            HTWind.Services.Application.HTWindPathProvider.GetRiskConsentFilePath(basePath));
        Assert.Equal(
            Path.Combine(basePath, "built-in-widgets.initialized"),
            HTWind.Services.Application.HTWindPathProvider.GetBuiltInWidgetsInitializationFilePath(basePath));
    }
}
