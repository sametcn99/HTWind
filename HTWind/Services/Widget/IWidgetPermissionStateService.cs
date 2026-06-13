using Microsoft.Web.WebView2.Core;

namespace HTWind.Services.Widget;

public interface IWidgetPermissionStateService
{
    void ClearAll();

    bool TryGetDecision(
        string widgetFilePath,
        CoreWebView2PermissionKind permissionKind,
        out CoreWebView2PermissionState state
    );

    void SaveDecision(
        string widgetFilePath,
        CoreWebView2PermissionKind permissionKind,
        CoreWebView2PermissionState state
    );
}
