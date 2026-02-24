using System.Text.Json;

namespace HTWind.Services;

public interface IWidgetHostApiService
{
    Task<WidgetHostApiExecutionResult> ExecuteAsync(string command, JsonElement? args);
}

public sealed class WidgetHostApiExecutionResult
{
    public bool Success { get; init; }

    public object? Result { get; init; }

    public string? Error { get; init; }
}
