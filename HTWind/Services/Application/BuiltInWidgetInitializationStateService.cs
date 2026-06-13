using System.IO;

namespace HTWind.Services.Application;

public sealed class BuiltInWidgetInitializationStateService : IBuiltInWidgetInitializationStateService
{
    private readonly string _stateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HTWind",
        "built-in-widgets.initialized"
    );

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
