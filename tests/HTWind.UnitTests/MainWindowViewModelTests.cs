using System.Collections.ObjectModel;
using System.IO;

using HTWind.Localization;
using HTWind.Model;
using HTWind.Services.Application;
using HTWind.Services.Templates;
using HTWind.Services.Widget;
using HTWind.ViewModels;

using NSubstitute;

using Xunit;

namespace HTWind.UnitTests;

/// <summary>Relay command wiring, dialog usage, culture fallback, and theme events.</summary>
public sealed class MainWindowViewModelTests
{
    private readonly IFileDialogService _fileDialogService;
    private readonly IWidgetManager _widgetManager;
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly IDeveloperModeService _developerModeService;
    private readonly RecordingDialogService _dialogService;
    private readonly MainWindowViewModel _viewModel;

    public MainWindowViewModelTests()
    {
        _fileDialogService = Substitute.For<IFileDialogService>();
        _widgetManager = Substitute.For<IWidgetManager>();
        _widgetManager.Widgets.Returns(new ObservableCollection<WidgetModel>());
        _startupRegistrationService = Substitute.For<IStartupRegistrationService>();
        _developerModeService = Substitute.For<IDeveloperModeService>();
        _dialogService = new RecordingDialogService();

        _viewModel = new MainWindowViewModel(
            _fileDialogService,
            _widgetManager,
            _startupRegistrationService,
            _developerModeService,
            dialogService: _dialogService);
    }

    /// <summary>Records dialog calls so no test ever opens a real modal window.</summary>
    private sealed class RecordingDialogService : IDialogService
    {
        public List<(string Message, string Title, bool IsWarning)> Calls { get; } = [];

        public void ShowInformation(string message, string title)
        {
            Calls.Add((message, title, IsWarning: false));
        }

        public void ShowWarning(string message, string title)
        {
            Calls.Add((message, title, IsWarning: true));
        }
    }

    #region Commands

    [Fact]
    public void AddWidgetCommand_WhenFilesPicked_AddsEveryFile()
    {
        var filePaths = new List<string> { @"C:\w\a.html", @"C:\w\b.html" };
        _fileDialogService.TryPickHtmlFiles(out Arg.Any<IReadOnlyList<string>>())
            .Returns(callInfo =>
            {
                callInfo[0] = filePaths;
                return true;
            });

        _viewModel.AddWidgetCommand.Execute(null);

        Received.InOrder(() =>
        {
            _widgetManager.AddWidget(@"C:\w\a.html");
            _widgetManager.AddWidget(@"C:\w\b.html");
        });
        Assert.Empty(_dialogService.Calls);
    }

    [Fact]
    public void AddWidgetCommand_WhenCancelled_DoesNothing()
    {
        _fileDialogService.TryPickHtmlFiles(out Arg.Any<IReadOnlyList<string>>()).Returns(false);

        _viewModel.AddWidgetCommand.Execute(null);

        _widgetManager.DidNotReceive().AddWidget(Arg.Any<string>());
    }

    [Fact]
    public void AddWidgetCommand_WhenImportFails_ShowsWarningDialog()
    {
        _fileDialogService.TryPickHtmlFiles(out Arg.Any<IReadOnlyList<string>>())
            .Returns(callInfo =>
            {
                callInfo[0] = new List<string> { @"C:\w\broken.html" };
                return true;
            });
        _widgetManager
            .When(manager => manager.AddWidget(@"C:\w\broken.html"))
            .Do(_ => throw new InvalidOperationException("bad widget"));

        _viewModel.AddWidgetCommand.Execute(null);

        var call = Assert.Single(_dialogService.Calls);
        Assert.True(call.IsWarning);
        Assert.Contains("bad widget", call.Message);
    }

    [Fact]
    public void ExportAllWidgetsCommand_WithoutWidgets_ShowsInformation()
    {
        _viewModel.ExportAllWidgetsCommand.Execute(null);

        var call = Assert.Single(_dialogService.Calls);
        Assert.False(call.IsWarning);
        _widgetManager.DidNotReceive().ExportWidgets(
            Arg.Any<IEnumerable<WidgetModel>>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Fact]
    public void ExportAllWidgetsCommand_WithWidgets_ExportsToPickedDirectory()
    {
        var model = new WidgetModel { Name = "a.html" };
        _widgetManager.Widgets.Add(model);
        _fileDialogService.TryPickDirectory(out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[0] = @"C:\exports";
                return true;
            });
        _widgetManager
            .ExportWidgets(Arg.Any<IEnumerable<WidgetModel>>(), @"C:\exports", Arg.Any<string>())
            .Returns(@"C:\exports\bundle");

        _viewModel.ExportAllWidgetsCommand.Execute(null);

        Received.InOrder(() =>
        {
            _widgetManager.ExportWidgets(
                Arg.Any<IEnumerable<WidgetModel>>(),
                @"C:\exports",
                Arg.Any<string>());
        });
        var call = Assert.Single(_dialogService.Calls);
        Assert.False(call.IsWarning);
        Assert.Contains(@"C:\exports\bundle", call.Message);
    }

    [Fact]
    public void ExportAllWidgetsCommand_WhenExportFails_ShowsWarning()
    {
        var model = new WidgetModel { Name = "a.html" };
        _widgetManager.Widgets.Add(model);
        _fileDialogService.TryPickDirectory(out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[0] = @"C:\exports";
                return true;
            });
        _widgetManager.When(manager =>
                manager.ExportWidgets(Arg.Any<IEnumerable<WidgetModel>>(), @"C:\exports", Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("disk full"));

        _viewModel.ExportAllWidgetsCommand.Execute(null);

        var call = Assert.Single(_dialogService.Calls);
        Assert.True(call.IsWarning);
        Assert.Contains("disk full", call.Message);
    }

    [Fact]
    public void ExportWidgetCommand_WhenDirectoryPicked_ExportsAndShowsSuccess()
    {
        var model = new WidgetModel { Name = "a.html" };
        _fileDialogService.TryPickDirectory(out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[0] = @"C:\exports";
                return true;
            });
        _widgetManager.ExportWidget(model, @"C:\exports").Returns(@"C:\exports\a");

        _viewModel.ExportWidgetCommand.Execute(model);

        Received.InOrder(() => _widgetManager.ExportWidget(model, @"C:\exports"));
        var call = Assert.Single(_dialogService.Calls);
        Assert.False(call.IsWarning);
    }

    [Fact]
    public void ExportWidgetCommand_WhenDirectoryPickCancelled_DoesNothing()
    {
        var model = new WidgetModel { Name = "a.html" };
        _fileDialogService.TryPickDirectory(out Arg.Any<string>()).Returns(false);

        _viewModel.ExportWidgetCommand.Execute(model);

        _widgetManager.DidNotReceive().ExportWidget(Arg.Any<WidgetModel>(), Arg.Any<string>());
        Assert.Empty(_dialogService.Calls);
    }

    [Fact]
    public void ChangeVisibilityCommand_ForwardsModelToManager()
    {
        var model = new WidgetModel();

        _viewModel.ChangeVisibilityCommand.Execute(model);

        Received.InOrder(() => _widgetManager.ApplyVisibility(model));
    }

    [Fact]
    public void Commands_WithNullParameter_AreNoOps()
    {
        _viewModel.ChangeVisibilityCommand.Execute(null);
        _viewModel.ChangePinStateCommand.Execute(null);
        _viewModel.ResetWidgetPositionCommand.Execute(null);
        _viewModel.EditWidgetCommand.Execute(null);
        _viewModel.ExportWidgetCommand.Execute(null);
        _viewModel.RemoveWidgetCommand.Execute(null);
        _viewModel.OpenWidgetSettingsCommand.Execute(null);

        _widgetManager.DidNotReceive().ApplyVisibility(Arg.Any<WidgetModel>());
        _widgetManager.DidNotReceive().ApplyPinState(Arg.Any<WidgetModel>());
        _widgetManager.DidNotReceive().ResetWidgetPosition(Arg.Any<WidgetModel>());
        _widgetManager.DidNotReceive().OpenEditor(Arg.Any<WidgetModel>());
        _widgetManager.DidNotReceive().RemoveWidget(Arg.Any<WidgetModel>());
        _widgetManager.DidNotReceive().OpenWidgetSettings(Arg.Any<WidgetModel>());
        Assert.Empty(_dialogService.Calls);
    }

    [Fact]
    public void ResetAllWidgetsCommand_InvokesManagerReset()
    {
        _viewModel.ResetAllWidgetsCommand.Execute(null);

        Received.InOrder(() => _widgetManager.ResetAllWidgetsToDefaultState());
    }

    #endregion

    #region Theme

    [Fact]
    public void SelectedThemeOption_FiresThemeRequestedAndPersists()
    {
        var received = new List<ThemeOption>();
        _viewModel.ThemeRequested += (_, option) => received.Add(option);

        _viewModel.SelectedThemeOption = ThemeOption.Light;

        Assert.Equal(ThemeOption.Light, _viewModel.SelectedThemeOption);
        var request = Assert.Single(received);
        Assert.Equal(ThemeOption.Light, request);
        Received.InOrder(() => _widgetManager.SetPreferredTheme(ThemeOption.Light));
    }

    [Fact]
    public void SelectedThemeOption_SameValue_DoesNotFireAgain()
    {
        var count = 0;
        _viewModel.ThemeRequested += (_, _) => count++;

        _viewModel.SelectedThemeOption = _viewModel.SelectedThemeOption;

        Assert.Equal(0, count);
    }

    #endregion

    #region Culture

    [Fact]
    public void SelectedCultureName_ValidCulture_UpdatesAndPersists()
    {
        LocalizationService.SetCulture("en-US");

        try
        {
            _viewModel.SelectedCultureName = "tr-TR";

            Assert.Equal("tr-TR", LocalizationService.CurrentCulture.Name);
            Received.InOrder(() => _widgetManager.SetPreferredCulture("tr-TR"));
        }
        finally
        {
            LocalizationService.SetCulture("en-US");
        }
    }

    [Fact]
    public void SelectedCultureName_InvalidCulture_FallsBackSafely()
    {
        LocalizationService.SetCulture("en-US");

        try
        {
            _viewModel.SelectedCultureName = "bad!culture-name";

            // CultureNotFoundException path: current culture stays unchanged and nothing is
            // persisted to the widget manager.
            Assert.Equal("en-US", LocalizationService.CurrentCulture.Name);
            _widgetManager.DidNotReceive().SetPreferredCulture(Arg.Any<string>());
        }
        finally
        {
            LocalizationService.SetCulture("en-US");
        }
    }

    [Fact]
    public void SelectedCultureName_Whitespace_IsIgnored()
    {
        LocalizationService.SetCulture("en-US");

        try
        {
            _viewModel.SelectedCultureName = "   ";

            Assert.Equal("en-US", LocalizationService.CurrentCulture.Name);
            _widgetManager.DidNotReceive().SetPreferredCulture(Arg.Any<string>());
        }
        finally
        {
            LocalizationService.SetCulture("en-US");
        }
    }

    #endregion

    #region Toggles

    [Fact]
    public void IsRunOnStartupEnabled_ForwardsToStartupService()
    {
        _startupRegistrationService.IsEnabled().Returns(false);

        _viewModel.IsRunOnStartupEnabled = true;

        Received.InOrder(() => _startupRegistrationService.SetEnabled(true));
    }

    [Fact]
    public void IsDeveloperModeEnabled_ForwardsToDeveloperModeService()
    {
        _developerModeService.IsEnabled().Returns(false);

        _viewModel.IsDeveloperModeEnabled = true;

        Received.InOrder(() => _developerModeService.SetEnabled(true));
    }

    [Fact]
    public void SuppressionToggles_ForwardToManager()
    {
        _viewModel.IsFullscreenSuppressionEnabled = true;
        _viewModel.IsMaximizedSuppressionEnabled = true;

        Assert.True(_widgetManager.IsFullscreenSuppressionEnabled);
        Assert.True(_widgetManager.IsMaximizedSuppressionEnabled);
    }

    #endregion
}
