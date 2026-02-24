namespace HTWind.Services;

public interface IWidgetGeometryService
{
    void CaptureGeometry(WidgetWindow window, WidgetModel model);

    void ApplyPersistedGeometry(
        WidgetWindow window,
        WidgetModel model,
        double defaultWidth,
        double defaultHeight
    );

    void ApplyDefaultGeometry(WidgetWindow window, double defaultWidth, double defaultHeight);

    bool EnsureVisibleOnAvailableDisplay(WidgetWindow window, WidgetModel model);
}
