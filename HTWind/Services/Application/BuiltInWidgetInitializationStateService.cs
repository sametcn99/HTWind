using System.IO;

namespace HTWind.Services.Application;

public sealed class BuiltInWidgetInitializationStateService : IBuiltInWidgetInitializationStateService
{
    private readonly string _stateFilePath;

    public BuiltInWidgetInitializationStateService(string? basePath = null)
    {
        _stateFilePath = HTWindPathProvider.GetBuiltInWidgetsInitializationFilePath(basePath);
    }

    public bool HasInitialized()
    {
        return File.Exists(_stateFilePath);
    }

    public void MarkInitialized()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
        File.WriteAllText(_stateFilePath, DateTime.UtcNow.ToString("O"));
    }
}
