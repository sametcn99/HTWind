using Microsoft.Web.WebView2.Core;

namespace HTWind.Services.Widget;

public enum PowerShellExecutionDecision
{
    ReadOnly,

    Full,

    Deny
}

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

    /// <summary>
    ///     Returns the persisted per-widget decision for <c>powershell.exec</c>, if the user has
    ///     already been asked.
    /// </summary>
    bool TryGetPowerShellDecision(
        string widgetFilePath,
        out PowerShellExecutionDecision decision
    );

    void SavePowerShellDecision(
        string widgetFilePath,
        PowerShellExecutionDecision decision
    );
}
