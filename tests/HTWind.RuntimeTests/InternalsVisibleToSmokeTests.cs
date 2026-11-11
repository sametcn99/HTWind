using Xunit;

namespace HTWind.RuntimeTests;

/// <summary>Smoke test verifying the runtime test project compiles against HTWind internals.</summary>
public class InternalsVisibleToSmokeTests
{
    [Fact]
    public void PowerShellEngine_IsVisibleFromTestProject()
    {
        Assert.NotNull(typeof(HTWind.Services.PowerShellExecution.PowerShellExecutionEngine));
    }
}
