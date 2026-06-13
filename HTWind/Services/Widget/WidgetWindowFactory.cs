using HTWind.Model;
using HTWind.Services.Application;
using HTWind.Services.WebView;

namespace HTWind.Services.Widget;

public class WidgetWindowFactory : IWidgetWindowFactory
{
    private readonly IDeveloperModeService _developerModeService;
    private readonly IWidgetHostApiService _widgetHostApiService;
    private readonly IWidgetPermissionStateService _widgetPermissionStateService;
    private readonly IWebViewEnvironmentProvider _webViewEnvironmentProvider;

    public WidgetWindowFactory(
        IWidgetHostApiService widgetHostApiService,
        IWebViewEnvironmentProvider webViewEnvironmentProvider,
        IDeveloperModeService developerModeService,
        IWidgetPermissionStateService widgetPermissionStateService
    )
    {
        _widgetHostApiService =
            widgetHostApiService ?? throw new ArgumentNullException(nameof(widgetHostApiService));
        _webViewEnvironmentProvider =
            webViewEnvironmentProvider
            ?? throw new ArgumentNullException(nameof(webViewEnvironmentProvider));
        _developerModeService =
            developerModeService
            ?? throw new ArgumentNullException(nameof(developerModeService));
        _widgetPermissionStateService =
            widgetPermissionStateService
            ?? throw new ArgumentNullException(nameof(widgetPermissionStateService));
    }

    public WidgetWindow Create(WidgetModel model)
    {
        return new WidgetWindow(
            model,
            _widgetHostApiService,
            _webViewEnvironmentProvider,
            _developerModeService,
            _widgetPermissionStateService
        );
    }
}
