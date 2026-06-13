namespace HTWind.Services.Widget;

public interface IWidgetGeometryService
{
    void CaptureGeometry(WidgetWindow window, WidgetModel model, bool updatePreferredMonitor = true);

    void ApplyPersistedGeometry(
        WidgetWindow window,
        WidgetModel model,
        double defaultWidth,
        double defaultHeight
    );

    void ApplyDefaultGeometry(WidgetWindow window, double defaultWidth, double defaultHeight);

    void ResetToPrimaryDisplayCenter(
        WidgetWindow window,
        WidgetModel model,
        double defaultWidth,
        double defaultHeight
    );

    bool EnsureVisibleOnAvailableDisplay(WidgetWindow window, WidgetModel model);
}
