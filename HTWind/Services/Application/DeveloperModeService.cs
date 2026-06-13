using System.IO;

namespace HTWind.Services.Application;

public sealed class DeveloperModeService : IDeveloperModeService
{
    private readonly string _stateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HTWind",
        "developer-mode.enabled"
    );

    public event EventHandler? Changed;

    public bool IsEnabled()
    {
        return File.Exists(_stateFilePath);
    }

    public void SetEnabled(bool enabled)
    {
        var currentlyEnabled = IsEnabled();
        if (currentlyEnabled == enabled)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);

            if (enabled)
            {
                File.WriteAllText(_stateFilePath, DateTime.UtcNow.ToString("O"));
            }
            else if (File.Exists(_stateFilePath))
            {
                File.Delete(_stateFilePath);
            }
        }
        catch
        {
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
