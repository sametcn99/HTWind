using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HTWind;

public class WidgetModel : INotifyPropertyChanged
{
    private string? _filePath;
    private bool _isLocked;
    private bool _isPinned;
    private bool _isVisible = true;
    private double? _left;
    private string? _monitorDeviceName;
    private string? _name;
    private double? _top;
    private double? _widgetHeight;
    private double? _widgetWidth;

    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string? Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
        }
    }

    public string? FilePath
    {
        get => _filePath;
        set
        {
            _filePath = value;
            OnPropertyChanged();
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
            OnPropertyChanged();
        }
    }

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            _isLocked = value;
            OnPropertyChanged();
        }
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            _isPinned = value;
            OnPropertyChanged();
        }
    }

    public double? Left
    {
        get => _left;
        set
        {
            _left = value;
            OnPropertyChanged();
        }
    }

    public double? Top
    {
        get => _top;
        set
        {
            _top = value;
            OnPropertyChanged();
        }
    }

    public double? WidgetWidth
    {
        get => _widgetWidth;
        set
        {
            _widgetWidth = value;
            OnPropertyChanged();
        }
    }

    public double? WidgetHeight
    {
        get => _widgetHeight;
        set
        {
            _widgetHeight = value;
            OnPropertyChanged();
        }
    }

    public string? MonitorDeviceName
    {
        get => _monitorDeviceName;
        set
        {
            _monitorDeviceName = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
