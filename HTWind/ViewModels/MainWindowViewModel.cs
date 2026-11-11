using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

using HTWind.Commands;
using HTWind.Model;
using HTWind.Services.Application;
using HTWind.Services.Templates;
using HTWind.Services.Widget;

namespace HTWind.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IDeveloperModeService _developerModeService;
    private readonly IDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly IWidgetManager _widgetManager;

    public MainWindowViewModel(
        IFileDialogService fileDialogService,
        IWidgetManager widgetManager,
        IStartupRegistrationService startupRegistrationService,
        IDeveloperModeService developerModeService,
        ThemeOption initialTheme = ThemeOption.Device,
        IDialogService? dialogService = null
    )
    {
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _widgetManager = widgetManager ?? throw new ArgumentNullException(nameof(widgetManager));
        _startupRegistrationService = startupRegistrationService ?? throw new ArgumentNullException(nameof(startupRegistrationService));
        _developerModeService = developerModeService ?? throw new ArgumentNullException(nameof(developerModeService));
        _dialogService = dialogService ?? MessageBoxDialogService.Instance;

        AddWidgetCommand = new RelayCommand(_ => AddWidget());
        ExportAllWidgetsCommand = new RelayCommand(_ => ExportAllWidgets());
        ChangeVisibilityCommand = new RelayCommand(model => ApplyVisibility(model as WidgetModel));
        ChangePinStateCommand = new RelayCommand(model => ApplyPinState(model as WidgetModel));
        ResetWidgetPositionCommand = new RelayCommand(model => ResetWidgetPosition(model as WidgetModel));
        ResetAllWidgetsCommand = new RelayCommand(_ => ResetAllWidgets());
        EditWidgetCommand = new RelayCommand(model => OpenEditor(model as WidgetModel));
        ExportWidgetCommand = new RelayCommand(model => ExportWidget(model as WidgetModel));
        RemoveWidgetCommand = new RelayCommand(model => RemoveWidget(model as WidgetModel));
        OpenWidgetSettingsCommand = new RelayCommand(model => OpenWidgetSettings(model as WidgetModel));
        _selectedThemeOption = initialTheme;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<WidgetModel> Widgets => _widgetManager.Widgets;

    public ThemeOption SelectedThemeOption
    {
        get => _selectedThemeOption;
        set
        {
            if (_selectedThemeOption == value)
            {
                return;
            }

            _selectedThemeOption = value;
            OnPropertyChanged();
            ThemeRequested?.Invoke(this, value);
            _widgetManager.SetPreferredTheme(value);
        }
    }

    private ThemeOption _selectedThemeOption;

    public bool IsRunOnStartupEnabled
    {
        get => _startupRegistrationService.IsEnabled();
        set
        {
            _startupRegistrationService.SetEnabled(value);
            OnPropertyChanged();
        }
    }

    public string SelectedCultureName
    {
        get => Localization.LocalizationService.CurrentCulture.Name;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || Localization.LocalizationService.CurrentCulture.Name == value)
            {
                return;
            }

            try
            {
                Localization.LocalizationService.SetCulture(value);
            }
            catch (CultureNotFoundException)
            {
                return;
            }

            _widgetManager.SetPreferredCulture(value);
            OnPropertyChanged();
        }
    }

    public bool IsDeveloperModeEnabled
    {
        get => _developerModeService.IsEnabled();
        set
        {
            _developerModeService.SetEnabled(value);
            OnPropertyChanged();
        }
    }

    public bool IsFullscreenSuppressionEnabled
    {
        get => _widgetManager.IsFullscreenSuppressionEnabled;
        set
        {
            _widgetManager.IsFullscreenSuppressionEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool IsMaximizedSuppressionEnabled
    {
        get => _widgetManager.IsMaximizedSuppressionEnabled;
        set
        {
            _widgetManager.IsMaximizedSuppressionEnabled = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddWidgetCommand { get; }

    public ICommand ChangeVisibilityCommand { get; }

    public ICommand ChangePinStateCommand { get; }

    public ICommand ExportAllWidgetsCommand { get; }

    public ICommand EditWidgetCommand { get; }

    public ICommand ExportWidgetCommand { get; }

    public ICommand ResetWidgetPositionCommand { get; }

    public ICommand ResetAllWidgetsCommand { get; }

    public ICommand RemoveWidgetCommand { get; }

    public ICommand OpenWidgetSettingsCommand { get; }

    public event EventHandler<ThemeOption>? ThemeRequested;

    public void RefreshStartupState()
    {
        OnPropertyChanged(nameof(IsRunOnStartupEnabled));
        OnPropertyChanged(nameof(IsDeveloperModeEnabled));
        OnPropertyChanged(nameof(IsFullscreenSuppressionEnabled));
        OnPropertyChanged(nameof(IsMaximizedSuppressionEnabled));
    }

    public void SetRunOnStartup(bool enabled)
    {
        IsRunOnStartupEnabled = enabled;
    }

    public void SetDeveloperMode(bool enabled)
    {
        IsDeveloperModeEnabled = enabled;
    }

    public void SetFullscreenSuppression(bool enabled)
    {
        IsFullscreenSuppressionEnabled = enabled;
    }

    public void SetMaximizedSuppression(bool enabled)
    {
        IsMaximizedSuppressionEnabled = enabled;
    }

    private void AddWidget()
    {
        if (_fileDialogService.TryPickHtmlFiles(out var filePaths))
        {
            foreach (var filePath in filePaths)
            {
                try
                {
                    _widgetManager.AddWidget(filePath);
                }
                catch (Exception ex)
                {
                    ShowDetailedError(
                        Localization.LocalizationService.Get("MainWindow_WidgetImportErrorTitle"),
                        string.Format(
                            Localization.LocalizationService.Get("MainWindow_WidgetImportErrorWithSource"),
                            filePath
                        ),
                        ex
                    );
                }
            }
        }
    }

    private void ExportAllWidgets()
    {
        if (Widgets.Count == 0)
        {
            _dialogService.ShowInformation(
                Localization.LocalizationService.Get("MainWindow_WidgetExportAllEmpty"),
                Localization.LocalizationService.Get("MainWindow_WidgetExportErrorTitle")
            );
            return;
        }

        if (!_fileDialogService.TryPickDirectory(out var directoryPath))
        {
            return;
        }

        try
        {
            var exportedPath = _widgetManager.ExportWidgets(
                Widgets,
                directoryPath,
                Localization.LocalizationService.Get("MainWindow_WidgetWorkspaceExportDefaultName")
            );

            _dialogService.ShowInformation(
                string.Format(
                    Localization.LocalizationService.Get("MainWindow_WidgetExportSuccess"),
                    exportedPath
                ),
                Localization.LocalizationService.Get("MainWindow_WidgetExportSuccessTitle")
            );
        }
        catch (Exception ex)
        {
            ShowDetailedError(
                Localization.LocalizationService.Get("MainWindow_WidgetExportErrorTitle"),
                Localization.LocalizationService.Get("MainWindow_WidgetExportWorkspaceError"),
                ex
            );
        }
    }

    private void ApplyVisibility(WidgetModel? model)
    {
        if (model is null)
        {
            return;
        }

        _widgetManager.ApplyVisibility(model);
    }

    private void ApplyPinState(WidgetModel? model)
    {
        if (model is null)
        {
            return;
        }

        _widgetManager.ApplyPinState(model);
    }

    private void OpenEditor(WidgetModel? model)
    {
        if (model is null)
        {
            return;
        }

        _widgetManager.OpenEditor(model);
    }

    private void ResetWidgetPosition(WidgetModel? model)
    {
        if (model is null)
        {
            return;
        }

        _widgetManager.ResetWidgetPosition(model);
    }

    private void RemoveWidget(WidgetModel? model)
    {
        if (model is null)
        {
            return;
        }

        _widgetManager.RemoveWidget(model);
    }

    private void OpenWidgetSettings(WidgetModel? model)
    {
        if (model is null)
        {
            return;
        }

        _widgetManager.OpenWidgetSettings(model);
    }

    private void ExportWidget(WidgetModel? model)
    {
        if (model is null)
        {
            return;
        }

        if (!_fileDialogService.TryPickDirectory(out var directoryPath))
        {
            return;
        }

        try
        {
            var exportedPath = _widgetManager.ExportWidget(model, directoryPath);
            _dialogService.ShowInformation(
                string.Format(
                    Localization.LocalizationService.Get("MainWindow_WidgetExportSuccess"),
                    exportedPath
                ),
                Localization.LocalizationService.Get("MainWindow_WidgetExportSuccessTitle")
            );
        }
        catch (Exception ex)
        {
            ShowDetailedError(
                Localization.LocalizationService.Get("MainWindow_WidgetExportErrorTitle"),
                string.Format(
                    Localization.LocalizationService.Get("MainWindow_WidgetExportErrorWithSource"),
                    model.DisplayName ?? model.Name ?? string.Empty
                ),
                ex
            );
        }
    }

    private void ShowDetailedError(string title, string summary, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(exception);

        var detailLines = new List<string>();
        var currentException = exception;
        while (currentException is not null)
        {
            if (!string.IsNullOrWhiteSpace(currentException.Message))
            {
                detailLines.Add(currentException.Message.Trim());
            }

            currentException = currentException.InnerException;
        }

        var detailText = string.Join(
            Environment.NewLine,
            detailLines.Distinct(StringComparer.Ordinal)
        );

        _dialogService.ShowWarning(
            string.IsNullOrWhiteSpace(detailText)
                ? summary
                : $"{summary}{Environment.NewLine}{Environment.NewLine}{detailText}",
            title
        );
    }

    private void ResetAllWidgets()
    {
        _widgetManager.ResetAllWidgetsToDefaultState();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
