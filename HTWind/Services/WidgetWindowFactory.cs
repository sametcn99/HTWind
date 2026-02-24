namespace HTWind.Services;

public class WidgetWindowFactory : IWidgetWindowFactory
{
    private readonly IWidgetHostApiService _widgetHostApiService;

    public WidgetWindowFactory(IWidgetHostApiService widgetHostApiService)
    {
        _widgetHostApiService =
            widgetHostApiService ?? throw new ArgumentNullException(nameof(widgetHostApiService));
    }

    public WidgetWindow Create(WidgetModel model)
    {
        return new WidgetWindow(model, _widgetHostApiService);
    }
}
