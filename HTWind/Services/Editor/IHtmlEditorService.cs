using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace HTWind.Services.Editor;

public interface IHtmlEditorService
{
    Task InitializeEditorAsync(
        WebView2 editorWebView,
        string filePath,
        EventHandler<CoreWebView2WebMessageReceivedEventArgs> webMessageReceivedHandler
    );

    /// <summary>Switches the Monaco instance to the given file's document (multi-tab editing).</summary>
    Task SetEditorDocumentAsync(WebView2 editorWebView, string filePath);

    /// <summary>Returns per-document dirty/language state for the tab UI.</summary>
    Task<IReadOnlyList<EditorDocumentState>> GetEditorDocumentStatesAsync(WebView2 editorWebView);

    Task<string> GetEditorContentAsync(WebView2 editorWebView);

    Task<string> GetEditorDocumentContentAsync(WebView2 editorWebView, string documentName);

    Task SaveEditorContentAsync(WebView2 editorWebView, string filePath);

    Task MarkEditorDocumentSavedAsync(WebView2 editorWebView, string documentName);

    bool TryReadContentChangedPayload(string webMessageAsJson, out string htmlContent);

    bool TryReadContentChangedDocumentName(string webMessageAsJson, out string? documentName);

    void ReleaseEditorEnvironment();
}
