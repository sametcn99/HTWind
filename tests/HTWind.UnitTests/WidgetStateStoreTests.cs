using System.IO;

using HTWind.Model;
using HTWind.Services.Widget;

using Xunit;

namespace HTWind.UnitTests;

/// <summary>Persistence loading rules: legacy formats, corruption tolerance, source-gen fidelity.</summary>
public sealed class WidgetStateStoreTests : IClassFixture<Fixtures.TempDirectoryFixture>
{
    private readonly Fixtures.TempDirectoryFixture _temp;

    public WidgetStateStoreTests(Fixtures.TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    private WidgetStateStore CreateStore()
    {
        return new WidgetStateStore(_temp.Path);
    }

    private string StateFilePath => Path.Combine(_temp.Path, "widgets-state.json");

    [Fact]
    public void HasStateFile_ReflectsFileExistence()
    {
        var store = CreateStore();
        Assert.False(store.HasStateFile());

        store.Save(new WidgetStateSnapshot());
        Assert.True(store.HasStateFile());
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaultSnapshot()
    {
        var store = CreateStore();

        var snapshot = store.Load();

        Assert.Empty(snapshot.Widgets);
    }

    [Fact]
    public void Load_LegacyRootArrayJson_IsMigratedToSnapshot()
    {
        File.WriteAllText(
            StateFilePath,
            """
            [
              {
                "Id": "legacy-1",
                "Name": "clock.html",
                "FilePath": "C:\\widgets\\clock.html",
                "IsVisible": true,
                "IsLocked": false,
                "IsPinned": true
              }
            ]
            """);

        var snapshot = CreateStore().Load();

        var record = Assert.Single(snapshot.Widgets);
        Assert.Equal("legacy-1", record.Id);
        Assert.True(record.IsVisible);
        Assert.True(record.IsPinned);
        // Legacy installs had no suppression flags; fullscreen suppression is defaulted on.
        Assert.True(snapshot.SuppressWidgetsOnFullscreen);
        Assert.False(snapshot.SuppressWidgetsOnMaximized);
    }

    [Theory]
    [InlineData("{\"widgets\": [")]
    [InlineData("not json at all")]
    [InlineData("[] trailing garbage")]
    public void Load_CorruptedJson_ReturnsDefaultSnapshotWithoutCrash(string corruptedJson)
    {
        File.WriteAllText(StateFilePath, corruptedJson);

        var snapshot = CreateStore().Load();

        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.Widgets);
    }

    [Fact]
    public void Load_JsonNullRoot_ReturnsDefaultSnapshot()
    {
        File.WriteAllText(StateFilePath, "null");

        var snapshot = CreateStore().Load();

        Assert.Empty(snapshot.Widgets);
    }

    [Fact]
    public void SaveLoad_RoundTrip_PreservesSnapshotViaSourceGeneratedSerializer()
    {
        var store = CreateStore();
        var placement = new WidgetMonitorPlacement
        {
            MonitorDeviceName = "DISPLAY1",
            Left = 10,
            Top = 20,
            Width = 800,
            Height = 600,
            LastSeenAtUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)
        };
        var snapshot = new WidgetStateSnapshot
        {
            SuppressWidgetsOnFullscreen = true,
            SuppressWidgetsOnMaximized = true,
            ThemeOption = "Dark",
            Culture = "tr-TR",
            Widgets =
            [
                new WidgetStateRecord
                {
                    Id = "w1",
                    Name = "clock.html",
                    FilePath = "C:\\widgets\\clock.html",
                    IsVisible = true,
                    IsLocked = true,
                    IsPinned = false,
                    PinMode = "desktop",
                    WidgetOpacityPercent = 80,
                    ZoomFactorPercent = 120,
                    Left = 1.5,
                    Top = 2.5,
                    WidgetWidth = 300,
                    WidgetHeight = 200,
                    MonitorDeviceName = "DISPLAY1",
                    PreferredMonitorDeviceName = "DISPLAY1",
                    MonitorPlacements = [placement]
                }
            ]
        };

        store.Save(snapshot);
        var loaded = CreateStore().Load();

        Assert.Equal(snapshot.SuppressWidgetsOnFullscreen, loaded.SuppressWidgetsOnFullscreen);
        Assert.Equal(snapshot.SuppressWidgetsOnMaximized, loaded.SuppressWidgetsOnMaximized);
        Assert.Equal(snapshot.ThemeOption, loaded.ThemeOption);
        Assert.Equal(snapshot.Culture, loaded.Culture);
        var record = Assert.Single(loaded.Widgets);
        Assert.Equal("w1", record.Id);
        Assert.Equal("clock.html", record.Name);
        Assert.True(record.IsVisible);
        Assert.True(record.IsLocked);
        Assert.Equal("desktop", record.PinMode);
        Assert.Equal(80, record.WidgetOpacityPercent);
        Assert.Equal(120, record.ZoomFactorPercent);
        Assert.Equal(1.5, record.Left);
        Assert.Equal("DISPLAY1", record.MonitorDeviceName);
        var loadedPlacement = Assert.Single(record.MonitorPlacements);
        Assert.Equal("DISPLAY1", loadedPlacement.MonitorDeviceName);
        Assert.Equal(800, loadedPlacement.Width);
        Assert.Equal(placement.LastSeenAtUtc, loadedPlacement.LastSeenAtUtc);
    }

    [Fact]
    public void Save_IsAtomic_NoTempResidue()
    {
        var store = CreateStore();

        store.Save(new WidgetStateSnapshot { ThemeOption = "Light" });

        Assert.Empty(Directory.GetFiles(_temp.Path, "*.tmp", SearchOption.AllDirectories));
        Assert.Equal("Light", CreateStore().Load().ThemeOption);
    }
}
