using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

using HTWind.Commands;
using HTWind.Services;

namespace HTWind.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IFileDialogService _fileDialogService;
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly IWidgetManager _widgetManager;
    private ThemeOption _selectedThemeOption = ThemeOption.Device;

    public MainWindowViewModel(
        IFileDialogService fileDialogService,
        IWidgetManager widgetManager,
        IStartupRegistrationService startupRegistrationService
    )
    {
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _widgetManager = widgetManager ?? throw new ArgumentNullException(nameof(widgetManager));
        _startupRegistrationService = startupRegistrationService ?? throw new ArgumentNullException(nameof(startupRegistrationService));

        AddWidgetCommand = new RelayCommand(_ => AddWidget());
        ChangeVisibilityCommand = new RelayCommand(model => ApplyVisibility(model as WidgetModel));
        ChangePinStateCommand = new RelayCommand(model => ApplyPinState(model as WidgetModel));
        EditWidgetCommand = new RelayCommand(model => OpenEditor(model as WidgetModel));
        RemoveWidgetCommand = new RelayCommand(model => RemoveWidget(model as WidgetModel));
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
        }
    }

    public bool IsRunOnStartupEnabled
    {
        get => _startupRegistrationService.IsEnabled();
        set
        {
            _startupRegistrationService.SetEnabled(value);
            OnPropertyChanged();
        }
    }

    public ICommand AddWidgetCommand { get; }

    public ICommand ChangeVisibilityCommand { get; }

    public ICommand ChangePinStateCommand { get; }

    public ICommand EditWidgetCommand { get; }

    public ICommand RemoveWidgetCommand { get; }

    public event EventHandler<ThemeOption>? ThemeRequested;

    public void RefreshStartupState()
    {
        OnPropertyChanged(nameof(IsRunOnStartupEnabled));
    }

    public void SetRunOnStartup(bool enabled)
    {
        IsRunOnStartupEnabled = enabled;
    }

    private void AddWidget()
    {
        if (_fileDialogService.TryPickHtmlFile(out var filePath))
        {
            _widgetManager.AddWidget(filePath);
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

    private void RemoveWidget(WidgetModel? model)
    {
        if (model is null)
        {
            return;
        }

        _widgetManager.RemoveWidget(model);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
