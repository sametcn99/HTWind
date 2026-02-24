namespace HTWind.Services;

public interface IExecutionRiskConsentService
{
    bool HasAccepted();

    void MarkAccepted();
}
