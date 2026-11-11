using Xunit;

namespace HTWind.E2ETests;

/// <summary>Smoke test verifying the E2E test project compiles against HTWind internals.</summary>
public class InternalsVisibleToSmokeTests
{
    [Fact]
    public void PathProvider_DataDirEnvironmentVariable_IsDefined()
    {
        Assert.Equal(
            "HTWIND_DATA_DIR",
            HTWind.Services.Application.HTWindPathProvider.DataDirectoryEnvironmentVariable
        );
    }
}
