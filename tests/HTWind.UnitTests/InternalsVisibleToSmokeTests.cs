using Xunit;

namespace HTWind.UnitTests;

/// <summary>Smoke tests verifying the test projects can see internal HTWind members.</summary>
public class InternalsVisibleToSmokeTests
{
    [Fact]
    public void ManifestHelper_IsVisibleFromTestProject()
    {
        Assert.NotNull(typeof(HTWind.Services.Widget.WidgetManifestHelper));
    }

    [Fact]
    public void SnapMath_MinimumSizeConstants_AreExposed()
    {
        Assert.Equal(160, WidgetWindowMath.MinWidgetWidth);
        Assert.Equal(100, WidgetWindowMath.MinWidgetHeight);
    }
}
