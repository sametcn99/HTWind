using Microsoft.Web.WebView2.Core;

namespace HTWind.Services.WebView;

public interface IWebViewEnvironmentProvider
{
    Task<CoreWebView2Environment> GetWidgetsEnvironmentAsync();

    Task<CoreWebView2Environment> GetEditorEnvironmentAsync();

    void ReleaseWidgetsEnvironment();

    void ReleaseEditorEnvironment();
}
