using System.ComponentModel;
using System.IO;
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
            // Keep the pin mode consistent when the classic "pin on top" flag is toggled.
            if (value && PinMode != WidgetPinMode.Topmost)
            {
                PinMode = WidgetPinMode.Topmost;
            }
            else if (!value && PinMode == WidgetPinMode.Topmost)
            {
                PinMode = WidgetPinMode.None;
            }
        }
    }

    public WidgetPinMode PinMode
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSentToBackDesktopLayer));
            var shouldBePinned = value == WidgetPinMode.Topmost;
            if (IsPinned != shouldBePinned)
            {
                IsPinned = shouldBePinned;
            }
        }
    } = WidgetPinMode.None;

    /// <summary>Binding-friendly flag for the "send to back (desktop layer)" pin mode.</summary>
    public bool IsSentToBackDesktopLayer
    {
        get => PinMode == WidgetPinMode.Desktop;
        set
        {
            var nextMode = value ? WidgetPinMode.Desktop : WidgetPinMode.None;
            if (PinMode == nextMode)
            {
                OnPropertyChanged();
                return;
            }

            PinMode = nextMode;
            OnPropertyChanged();
        }
    }

    /// <summary>Per-widget window opacity in percent (20-100).</summary>
    public double? WidgetOpacityPercent
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Per-widget WebView2 zoom factor in percent (50-200).</summary>
    public double? ZoomFactorPercent
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Whether the widget's inline settings panel is expanded on the home page (UI-only, not persisted).</summary>
    public bool IsExpanded
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Cached file creation timestamp (refreshed on import/save, not persisted).</summary>
    public DateTime? FileCreatedUtc { get; private set; }

    /// <summary>Cached file last-write timestamp (refreshed on import/save, not persisted).</summary>
    public DateTime? FileUpdatedUtc { get; private set; }

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

    public string? PreferredMonitorDeviceName
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public List<WidgetMonitorPlacement> MonitorPlacements
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Refreshes the cached file timestamps used for sorting so the comparer never performs
    ///     synchronous disk I/O. Call after import, save, or file replacement.
    /// </summary>
    public void RefreshFileTimestamps()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            FileCreatedUtc = null;
            FileUpdatedUtc = null;
            return;
        }

        try
        {
            var fileInfo = new FileInfo(FilePath);
            if (fileInfo.Exists)
            {
                FileCreatedUtc = fileInfo.CreationTimeUtc;
                FileUpdatedUtc = fileInfo.LastWriteTimeUtc;
            }
            else
            {
                FileCreatedUtc = null;
                FileUpdatedUtc = null;
            }
        }
        catch
        {
            FileCreatedUtc = null;
            FileUpdatedUtc = null;
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
