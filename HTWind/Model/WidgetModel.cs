using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HTWind;

public class WidgetModel : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string? Name
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string? DisplayName =>
        string.IsNullOrWhiteSpace(Name) ? Name : Path.GetFileNameWithoutExtension(Name);

    public string? FilePath
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsVisible
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = true;

    public bool IsLocked
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsPinned
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public double? Left
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public double? Top
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public double? WidgetWidth
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public double? WidgetHeight
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public string? MonitorDeviceName
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
