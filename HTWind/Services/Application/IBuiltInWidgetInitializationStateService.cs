namespace HTWind.Services.Application;

public interface IBuiltInWidgetInitializationStateService
{
    bool HasInitialized();

    void MarkInitialized();
}
