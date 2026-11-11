namespace HTWind;

public enum WidgetPinMode
{
    /// <summary>No z-order pinning; the window follows normal z-order.</summary>
    None,

    /// <summary>Window stays above all other windows (Topmost).</summary>
    Topmost,

    /// <summary>Window is pinned to the desktop layer (HWND_BOTTOM, above desktop icons).</summary>
    Desktop
}

public static class WidgetPinModeExtensions
{
    public static string ToPersistedValue(this WidgetPinMode mode)
    {
        return mode.ToString();
    }

    public static WidgetPinMode FromPersistedValue(string? value, bool isPinnedFallback)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && Enum.TryParse<WidgetPinMode>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return isPinnedFallback ? WidgetPinMode.Topmost : WidgetPinMode.None;
    }
}
