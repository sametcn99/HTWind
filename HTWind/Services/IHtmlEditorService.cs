using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace HTWind.Services;

public interface IHtmlEditorService
{
    Task InitializeEditorAsync(
        WebView2 editorWebView,
        string filePath,
        EventHandler<CoreWebView2WebMessageReceivedEventArgs> webMessageReceivedHandler
    );

    Task<string> GetEditorContentAsync(WebView2 editorWebView);

    Task SaveEditorContentAsync(WebView2 editorWebView, string filePath);

    bool TryReadContentChangedPayload(string webMessageAsJson, out string htmlContent);
}
