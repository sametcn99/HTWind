namespace HTWind.Services.Application;

public interface IExecutionRiskConsentService
{
    bool HasAccepted();

    void MarkAccepted();
}
