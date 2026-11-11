using System.Text.Json;

using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace HTWind.Services.WebView;

/// <summary>Per-widget context passed to the host API by the widget window.</summary>
public sealed class WidgetExecutionContext
{
    public string? WidgetFilePath { get; init; }

    public PowerShellExecutionAuthorization? PowerShellAuthorization { get; init; }
}

public enum PowerShellExecutionAuthorization
{
    ReadOnly,

    Full
}

/// <summary>Theme snapshot exposed to widgets via <c>theme.get</c> / <c>theme.onChanged</c>.</summary>
public sealed class WidgetHostThemePayload
{
    public string AppTheme { get; init; } = "light";

    public string SystemTheme { get; init; } = "light";

    public string AccentColor { get; init; } = "#0078D4";

    public bool IsDark { get; init; }

    public static WidgetHostThemePayload Build()
    {
        var appTheme = ApplicationThemeManager.GetAppTheme();
        var isAppDark = appTheme == ApplicationTheme.Dark
            || appTheme == ApplicationTheme.HighContrast
            || (appTheme == ApplicationTheme.Unknown
                && ApplicationThemeManager.GetSystemTheme() is Wpf.Ui.Appearance.SystemTheme.Dark
                    or Wpf.Ui.Appearance.SystemTheme.CapturedMotion
                    or Wpf.Ui.Appearance.SystemTheme.Glow);
        var isSystemDark = ReadSystemPrefersDark();
        var accent = ReadAccentColor();

        return new WidgetHostThemePayload
        {
            AppTheme = isAppDark ? "dark" : "light",
            SystemTheme = isSystemDark ? "dark" : "light",
            AccentColor = accent,
            IsDark = isAppDark
        };
    }

    private static bool ReadSystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                return value == 0;
            }
        }
        catch
        {
        }

        return true;
    }

    private static string ReadAccentColor()
    {
        const string fallback = "#0078D4";

        try
        {
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var accent = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
            return $"#{accent.R:X2}{accent.G:X2}{accent.B:X2}";
        }
        catch
        {
            return fallback;
        }
    }
}

/// <summary>
///     Shared helpers for reading simple arguments out of a host API JSON payload.
/// </summary>
public static class WidgetHostApiArgs
{
    public static string? ReadStringArg(JsonElement? args, string key)
    {
        if (!args.HasValue || args.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return args.Value.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    public static int? ReadIntArg(JsonElement? args, string key)
    {
        if (!args.HasValue || args.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!args.Value.TryGetProperty(key, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
