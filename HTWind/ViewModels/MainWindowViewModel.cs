using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

using HTWind.Commands;
using HTWind.Services;

namespace HTWind.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IDeveloperModeService _developerModeService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly IWidgetManager _widgetManager;

    public MainWindowViewModel(
        IFileDialogService fileDialogService,
        IWidgetManager widgetManager,
        IStartupRegistrationService startupRegistrationService,
        IDeveloperModeService developerModeService
    )
    {
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _widgetManager = widgetManager ?? throw new ArgumentNullException(nameof(widgetManager));
        _startupRegistrationService = startupRegistrationService ?? throw new ArgumentNullException(nameof(startupRegistrationService));
        _developerModeService = developerModeService ?? throw new ArgumentNullException(nameof(developerModeService));

        AddWidgetCommand = new RelayCommand(_ => AddWidget());
        ChangeVisibilityCommand = new RelayCommand(model => ApplyVisibility(model as WidgetModel));
        ChangePinStateCommand = new RelayCommand(model => ApplyPinState(model as WidgetModel));
        ResetWidgetPositionCommand = new RelayCommand(model => ResetWidgetPosition(model as WidgetModel));
        ResetAllWidgetsCommand = new RelayCommand(_ => ResetAllWidgets());
        EditWidgetCommand = new RelayCommand(model => OpenEditor(model as WidgetModel));
        RemoveWidgetCommand = new RelayCommand(model => RemoveWidget(model as WidgetModel));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<WidgetModel> Widgets => _widgetManager.Widgets;

    public ThemeOption SelectedThemeOption
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
            ThemeRequested?.Invoke(this, value);
        }
    } = ThemeOption.Device;

    public bool IsRunOnStartupEnabled
    {
        get => _startupRegistrationService.IsEnabled();
        set
        {
            _startupRegistrationService.SetEnabled(value);
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

    public ICommand EditWidgetCommand { get; }

    public ICommand ResetWidgetPositionCommand { get; }

    public ICommand ResetAllWidgetsCommand { get; }

    public ICommand RemoveWidgetCommand { get; }

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
                _widgetManager.AddWidget(filePath);
            }
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

    private void ResetAllWidgets()
    {
        _widgetManager.ResetAllWidgetsToDefaultState();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
