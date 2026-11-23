using System.IO;

using HTWind.Services.Application;
using HTWind.Services.Widget;

using Xunit;

namespace HTWind.UnitTests;

public sealed class WorkspaceProfileStoreTests : IDisposable
{
    private readonly Fixtures.TempDirectoryFixture _temp = new();
    private WorkspaceProfileStore Store => new(_temp.Path);
    private string StorePath => HTWindPathProvider.GetWorkspaceProfilesFilePath(_temp.Path);

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void MissingStore_ReturnsNoProfilesWithoutCreatingAFile()
    {
        Assert.Empty(Store.GetProfileNames());
        Assert.Null(Store.TryLoad("missing"));
        Assert.False(Store.Delete("missing"));
        Assert.False(File.Exists(StorePath));
    }

    [Fact]
    public void SaveReload_PreservesLayoutAndReplacesNamesIgnoringCase()
    {
        Store.Save(new WorkspaceProfile { Name = "Work" });
        Store.Save(new WorkspaceProfile { Name = "Game" });
        Store.Save(new WorkspaceProfile
        {
            Name = "WORK",
            CreatedUtc = "2026-01-02T03:04:05Z",
            Widgets = [new WorkspaceProfileWidget
            {
                Name = "Clock — İstanbul",
                FilePath = Path.Combine(_temp.Path, "widget's folder", "index.html"),
                IsVisible = true,
                IsLocked = true,
                IsPinned = true,
                PinMode = "desktop",
                WidgetOpacityPercent = 0,
                ZoomFactorPercent = 175,
                Left = -1920.5,
                Top = -100,
                Width = 320.25,
                Height = 240,
                MonitorDeviceName = "DISPLAY2",
                PreferredMonitorDeviceName = "DISPLAY3"
            }]
        });

        Assert.Equal(2, Store.GetProfileNames().Count);
        Assert.Contains("Game", Store.GetProfileNames());
        var loaded = Assert.IsType<WorkspaceProfile>(Store.TryLoad("work"));
        Assert.Equal("2026-01-02T03:04:05Z", loaded.CreatedUtc);
        var widget = Assert.Single(loaded.Widgets);
        Assert.Equal("Clock — İstanbul", widget.Name);
        Assert.Equal(Path.Combine(_temp.Path, "widget's folder", "index.html"), widget.FilePath);
        Assert.True(widget.IsVisible && widget.IsLocked && widget.IsPinned);
        Assert.Equal("desktop", widget.PinMode);
        Assert.Equal(0, widget.WidgetOpacityPercent);
        Assert.Equal(175, widget.ZoomFactorPercent);
        Assert.Equal(-1920.5, widget.Left);
        Assert.Equal(-100, widget.Top);
        Assert.Equal(320.25, widget.Width);
        Assert.Equal(240, widget.Height);
        Assert.Equal("DISPLAY2", widget.MonitorDeviceName);
        Assert.Equal("DISPLAY3", widget.PreferredMonitorDeviceName);
        Assert.Empty(Directory.GetFiles(_temp.Path, "*.tmp"));
    }

    [Fact]
    public void Delete_IgnoresCaseAndPreservesOtherProfiles()
    {
        Store.Save(new WorkspaceProfile { Name = "Work" });
        Store.Save(new WorkspaceProfile { Name = "Game" });

        Assert.True(Store.Delete("wOrK"));
        Assert.False(Store.Delete("work"));
        Assert.Equal("Game", Assert.Single(Store.GetProfileNames()));
        Assert.True(Store.Delete("GAME"));
        Assert.Empty(Store.GetProfileNames());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[42]")]
    public void CorruptStore_CanBeReplacedWithAValidProfile(string json)
    {
        File.WriteAllText(StorePath, json);
        Assert.Empty(Store.GetProfileNames());

        Store.Save(new WorkspaceProfile { Name = "Recovered" });

        Assert.Equal("Recovered", Assert.Single(Store.GetProfileNames()));
    }

    [Fact]
    public void InvalidRecords_DoNotHideValidProfilesOrBreakSubsequentSaves()
    {
        File.WriteAllText(StorePath,
            """[null, {"Name":""}, {"Name":"  "}, {"Name":"Work","Widgets":null}]""");

        Assert.Equal("Work", Assert.Single(Store.GetProfileNames()));
        Assert.Empty(Assert.IsType<WorkspaceProfile>(Store.TryLoad("Work")).Widgets);
        Store.Save(new WorkspaceProfile { Name = "Game" });
        Assert.Equal(2, Store.GetProfileNames().Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void InvalidNames_AreRejectedWithoutChangingExistingData(string? name)
    {
        Store.Save(new WorkspaceProfile { Name = "Work" });
        var before = File.ReadAllText(StorePath);

        Assert.ThrowsAny<ArgumentException>(() => Store.Save(new WorkspaceProfile { Name = name! }));
        Assert.ThrowsAny<ArgumentException>(() => Store.TryLoad(name!));
        Assert.ThrowsAny<ArgumentException>(() => Store.Delete(name!));
        Assert.Equal(before, File.ReadAllText(StorePath));
    }
}
