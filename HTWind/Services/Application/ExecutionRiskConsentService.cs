using System.IO;

namespace HTWind.Services.Application;

public sealed class ExecutionRiskConsentService : IExecutionRiskConsentService
{
    private readonly string _consentFilePath;

    public ExecutionRiskConsentService(string? basePath = null)
    {
        _consentFilePath = HTWindPathProvider.GetRiskConsentFilePath(basePath);
    }

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
