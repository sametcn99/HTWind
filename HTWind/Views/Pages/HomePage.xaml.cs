using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

using HTWind.Services;

namespace HTWind.Views.Pages;

public partial class HomePage : UserControl
{
    private const string DiscussionsUrl = "https://github.com/sametcn99/HTWind/discussions";

    private readonly IWidgetManager _widgetManager;
    private ICollectionView? _widgetsCollectionView;
    private bool _areWidgetSubscriptionsAttached;
    private SortOption _sortOption = SortOption.NameAsc;
    private VisibilityFilterOption _visibilityFilterOption = VisibilityFilterOption.All;

    private enum VisibilityFilterOption
    {
        All,
        VisibleOnly,
        HiddenOnly
    }

    private enum SortOption
    {
        NameAsc,
        NameDesc,
        CreatedNewest,
        CreatedOldest,
        UpdatedNewest,
        UpdatedOldest
    }

    public HomePage(IWidgetManager widgetManager)
    {
        ArgumentNullException.ThrowIfNull(widgetManager);
        _widgetManager = widgetManager;

        InitializeComponent();
        Loaded += HomePage_Loaded;
    }

    private void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        EnsureWidgetSubscriptions();
        EnsureWidgetsCollectionView();
        ApplyWidgetFilterAndSort();
    }

    private void HomePage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!_areWidgetSubscriptionsAttached)
        {
            return;
        }

        Unloaded -= HomePage_Unloaded;
        _widgetManager.Widgets.CollectionChanged -= Widgets_CollectionChanged;

        foreach (var widget in _widgetManager.Widgets)
        {
            widget.PropertyChanged -= Widget_PropertyChanged;
        }

        _areWidgetSubscriptionsAttached = false;
    }

    private void AddWidgetOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement target || target.ContextMenu is not ContextMenu menu)
        {
            return;
        }

        menu.DataContext = DataContext;
        menu.PlacementTarget = target;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void WidgetActionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement target || target.ContextMenu is not ContextMenu menu)
        {
            return;
        }

        menu.DataContext = DataContext;
        menu.PlacementTarget = target;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void CreateWithEditorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var createWindow = new CreateWidgetWithEditorWindow
        {
            Owner = Window.GetWindow(this),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        if (createWindow.ShowDialog() != true)
        {
            return;
        }

        _widgetManager.CreateWidgetWithEditor(
            createWindow.RequestedFileName,
            createWindow.IsVisibleByDefault,
            createWindow.EnableHotReload
        );
    }

    private void FindMore_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DiscussionsUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore failures opening external links.
        }
    }

    private void WidgetSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyWidgetFilterAndSort();
    }

    private void VisibilityFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox)
        {
            return;
        }

        _visibilityFilterOption = ParseSelection(comboBox, VisibilityFilterOption.All);
        ApplyWidgetFilterAndSort();
    }

    private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox)
        {
            return;
        }

        _sortOption = ParseSelection(comboBox, SortOption.NameAsc);
        ApplyWidgetFilterAndSort();
    }

    private static T ParseSelection<T>(ComboBox comboBox, T fallback)
        where T : struct, Enum
    {
        if (comboBox.SelectedItem is ComboBoxItem { Tag: string tagValue }
            && Enum.TryParse<T>(tagValue, out var parsedValue))
        {
            return parsedValue;
        }

        return fallback;
    }

    private void ApplyWidgetFilterAndSort()
    {
        if (!IsLoaded)
        {
            return;
        }

        if (!EnsureWidgetsCollectionView())
        {
            return;
        }

        ApplySorting();
        _widgetsCollectionView?.Refresh();
    }

    private bool EnsureWidgetsCollectionView()
    {
        if (_widgetsCollectionView != null)
        {
            return true;
        }

        if (WidgetsList is null)
        {
            return false;
        }

        if (WidgetsList.ItemsSource is null)
        {
            return false;
        }

        _widgetsCollectionView = CollectionViewSource.GetDefaultView(WidgetsList.ItemsSource);
        if (_widgetsCollectionView is null)
        {
            return false;
        }

        _widgetsCollectionView.Filter = FilterWidget;
        ApplySorting();
        return true;
    }

    private void ApplySorting()
    {
        if (_widgetsCollectionView is not ListCollectionView listCollectionView)
        {
            return;
        }

        listCollectionView.CustomSort = new WidgetComparer(_sortOption);
    }

    private bool FilterWidget(object item)
    {
        if (item is not WidgetModel widget)
        {
            return false;
        }

        if (_visibilityFilterOption == VisibilityFilterOption.VisibleOnly && !widget.IsVisible)
        {
            return false;
        }

        if (_visibilityFilterOption == VisibilityFilterOption.HiddenOnly && widget.IsVisible)
        {
            return false;
        }

        var query = WidgetSearchTextBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return (widget.DisplayName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || (widget.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void EnsureWidgetSubscriptions()
    {
        if (_areWidgetSubscriptionsAttached)
        {
            return;
        }

        _widgetManager.Widgets.CollectionChanged += Widgets_CollectionChanged;

        foreach (var widget in _widgetManager.Widgets)
        {
            widget.PropertyChanged += Widget_PropertyChanged;
        }

        Unloaded += HomePage_Unloaded;
        _areWidgetSubscriptionsAttached = true;
    }

    private void Widgets_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<WidgetModel>())
            {
                item.PropertyChanged -= Widget_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<WidgetModel>())
            {
                item.PropertyChanged += Widget_PropertyChanged;
            }
        }

        ApplyWidgetFilterAndSort();
    }

    private void Widget_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WidgetModel.IsVisible)
            or nameof(WidgetModel.Name)
            or nameof(WidgetModel.FilePath))
        {
            ApplyWidgetFilterAndSort();
        }
    }

    private sealed class WidgetComparer(SortOption sortOption) : IComparer
    {
        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is not WidgetModel left)
            {
                return -1;
            }

            if (y is not WidgetModel right)
            {
                return 1;
            }

            var comparison = sortOption switch
            {
                SortOption.NameAsc => CompareByName(left, right),
                SortOption.NameDesc => CompareByName(right, left),
                SortOption.CreatedNewest => CompareByCreated(right, left),
                SortOption.CreatedOldest => CompareByCreated(left, right),
                SortOption.UpdatedNewest => CompareByUpdated(right, left),
                SortOption.UpdatedOldest => CompareByUpdated(left, right),
                _ => CompareByName(left, right)
            };

            if (comparison != 0)
            {
                return comparison;
            }

            return CompareByName(left, right);
        }

        private static int CompareByName(WidgetModel left, WidgetModel right)
        {
            var leftName = left.DisplayName ?? left.Name ?? string.Empty;
            var rightName = right.DisplayName ?? right.Name ?? string.Empty;
            return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareByCreated(WidgetModel left, WidgetModel right)
        {
            return GetFileCreationUtc(left.FilePath).CompareTo(GetFileCreationUtc(right.FilePath));
        }

        private static int CompareByUpdated(WidgetModel left, WidgetModel right)
        {
            return GetFileUpdatedUtc(left.FilePath).CompareTo(GetFileUpdatedUtc(right.FilePath));
        }

        private static DateTime GetFileCreationUtc(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return DateTime.MinValue;
            }

            try
            {
                return File.GetCreationTimeUtc(filePath);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private static DateTime GetFileUpdatedUtc(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return DateTime.MinValue;
            }

            try
            {
                return File.GetLastWriteTimeUtc(filePath);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }
}
