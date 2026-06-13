using HTWind.Model;

namespace HTWind.Services.Widget;

public interface IWidgetStateStore
{
    bool HasStateFile();
    WidgetStateSnapshot Load();
    void Save(WidgetStateSnapshot snapshot);
}
