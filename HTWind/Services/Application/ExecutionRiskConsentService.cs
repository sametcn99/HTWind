using System.IO;

namespace HTWind.Services.Application;

public sealed class ExecutionRiskConsentService : IExecutionRiskConsentService
{
    private readonly string _consentFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HTWind",
        "risk-consent.accepted"
    );

    public bool HasAccepted()
    {
        return File.Exists(_consentFilePath);
    }

    public void MarkAccepted()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_consentFilePath)!);
        File.WriteAllText(_consentFilePath, DateTime.UtcNow.ToString("O"));
    }
}
