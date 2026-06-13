using System.Runtime.InteropServices;

using HTWind.Model;

namespace HTWind.Services.Widget;

public interface IWidgetSuppressionMonitor
{
    bool IsFullscreenSuppressionEnabled { get; set; }
    bool IsMaximizedSuppressionEnabled { get; set; }

    bool ShouldSuppressWidget(WidgetModel model);
    void Start(Action reconcileCallback);
    void Stop();
}
