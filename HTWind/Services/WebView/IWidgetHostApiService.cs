using System.Text.Json;

namespace HTWind.Services.WebView;

public interface IWidgetHostApiService
{
    /// <summary>Raised whenever the application or system theme changes, so widgets can be notified.</summary>
    event EventHandler<WidgetHostThemePayload>? ThemeChanged;

    Task<WidgetHostApiExecutionResult> ExecuteAsync(
        string command,
        JsonElement? args,
        WidgetExecutionContext? context = null);

    /// <summary>Publishes the current theme payload to every ThemeChanged subscriber.</summary>
    void PublishThemeChanged(WidgetHostThemePayload? payload = null);

    /// <summary>Returns the serialized "settings" values stored for the widget, or null.</summary>
    string? GetWidgetSettingsJson(string? widgetFilePath);

    /// <summary>Persists the settings form values for the widget under the reserved key.</summary>
    void SaveWidgetSettings(string? widgetFilePath, System.Text.Json.Nodes.JsonObject settings);
}

public sealed class WidgetHostApiExecutionResult
{
    public bool Success { get; init; }

    public object? Result { get; init; }

    public string? Error { get; init; }
}
