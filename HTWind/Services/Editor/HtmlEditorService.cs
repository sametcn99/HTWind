using System.IO;
using System.Text;
using System.Text.Json;

using HTWind.Services.WebView;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace HTWind.Services.Editor;

public sealed class EditorDocumentState
{
    public string Name { get; init; } = string.Empty;

    public string Language { get; init; } = "plaintext";

    public bool IsDirty { get; init; }
}

public sealed class HtmlEditorService : IHtmlEditorService
{
    public const string EditorHostName = "editor.htwind.local";
    private const string EditorHostPageName = "htwind-editor.html";

    private static readonly JsonSerializerOptions DocumentStateJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IWebViewEnvironmentProvider _webViewEnvironmentProvider;

    public HtmlEditorService(IWebViewEnvironmentProvider webViewEnvironmentProvider)
    {
        _webViewEnvironmentProvider =
          webViewEnvironmentProvider
          ?? throw new ArgumentNullException(nameof(webViewEnvironmentProvider));
    }

    public async Task InitializeEditorAsync(
        WebView2 editorWebView,
        string filePath,
        EventHandler<CoreWebView2WebMessageReceivedEventArgs> webMessageReceivedHandler
    )
    {
        ArgumentNullException.ThrowIfNull(editorWebView);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(webMessageReceivedHandler);

        var env = await _webViewEnvironmentProvider.GetEditorEnvironmentAsync();
        await editorWebView.EnsureCoreWebView2Async(env);
        editorWebView.CoreWebView2.WebMessageReceived += webMessageReceivedHandler;

        // Monaco is served from the app bundle over a virtual host so the editor works fully
        // offline (no CDN dependency).
        var editorRoot = EnsureEditorContentRoot();
        editorWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            EditorHostName,
            editorRoot,
            CoreWebView2HostResourceAccessKind.Allow);

        var readyTask = WaitEditorReadyAsync(editorWebView.CoreWebView2);
        editorWebView.CoreWebView2.Navigate($"https://{EditorHostName}/{EditorHostPageName}");
        await readyTask;
        await SetEditorDocumentAsync(editorWebView, filePath);
    }

    public async Task SetEditorDocumentAsync(WebView2 editorWebView, string filePath)
    {
        ArgumentNullException.ThrowIfNull(editorWebView);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
        var name = Path.GetFileName(filePath);
        var language = GetLanguageForFile(filePath);
        await editorWebView.ExecuteScriptAsync(
            $"window.setEditorDocument({EscapeJs(name)}, '{encoded}', {EscapeJs(language)});");
    }

    public async Task<IReadOnlyList<EditorDocumentState>> GetEditorDocumentStatesAsync(WebView2 editorWebView)
    {
        ArgumentNullException.ThrowIfNull(editorWebView);

        var scriptResult = await editorWebView.ExecuteScriptAsync(
            "window.getDocumentStates ? window.getDocumentStates() : '[]';");
        var json = JsonSerializer.Deserialize<string>(scriptResult) ?? "[]";

        try
        {
            return JsonSerializer.Deserialize<List<EditorDocumentState>>(json, DocumentStateJsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<string> GetEditorContentAsync(WebView2 editorWebView)
    {
        ArgumentNullException.ThrowIfNull(editorWebView);

        var scriptResult = await editorWebView.ExecuteScriptAsync(
            "window.getEditorContent ? window.getEditorContent() : '';");
        return JsonSerializer.Deserialize<string>(scriptResult) ?? string.Empty;
    }

    public async Task<string> GetEditorDocumentContentAsync(WebView2 editorWebView, string documentName)
    {
        ArgumentNullException.ThrowIfNull(editorWebView);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);

        var scriptResult = await editorWebView.ExecuteScriptAsync(
            $"window.getDocumentContent ? window.getDocumentContent({EscapeJs(documentName)}) : '';");
        return JsonSerializer.Deserialize<string>(scriptResult) ?? string.Empty;
    }

    public async Task SaveEditorContentAsync(WebView2 editorWebView, string filePath)
    {
        ArgumentNullException.ThrowIfNull(editorWebView);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var content = await GetEditorDocumentContentAsync(editorWebView, Path.GetFileName(filePath));
        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
        await MarkEditorDocumentSavedAsync(editorWebView, Path.GetFileName(filePath));
    }

    public async Task MarkEditorDocumentSavedAsync(WebView2 editorWebView, string documentName)
    {
        ArgumentNullException.ThrowIfNull(editorWebView);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);

        await editorWebView.ExecuteScriptAsync(
            $"window.markDocumentSaved && window.markDocumentSaved({EscapeJs(documentName)});");
    }

    public bool TryReadContentChangedPayload(string webMessageAsJson, out string htmlContent)
    {
        htmlContent = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(webMessageAsJson);
            var root = document.RootElement;

            if (
                !root.TryGetProperty("type", out var typeElement)
                || !string.Equals(typeElement.GetString(), "contentChanged", StringComparison.Ordinal)
            )
            {
                return false;
            }

            if (!root.TryGetProperty("content", out var contentElement))
            {
                return false;
            }

            htmlContent = contentElement.GetString() ?? string.Empty;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool TryReadContentChangedDocumentName(string webMessageAsJson, out string? documentName)
    {
        documentName = null;

        try
        {
            using var document = JsonDocument.Parse(webMessageAsJson);
            var root = document.RootElement;

            if (
                !root.TryGetProperty("type", out var typeElement)
                || !string.Equals(typeElement.GetString(), "contentChanged", StringComparison.Ordinal)
            )
            {
                return false;
            }

            if (root.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
            {
                documentName = nameElement.GetString();
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public void ReleaseEditorEnvironment()
    {
        _webViewEnvironmentProvider.ReleaseEditorEnvironment();
    }

    public static string GetLanguageForFile(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".html" or ".htm" => "html",
            ".css" => "css",
            ".js" or ".mjs" => "javascript",
            ".ts" => "typescript",
            ".json" => "json",
            ".xml" => "xml",
            ".md" => "markdown",
            ".yaml" or ".yml" => "yaml",
            _ => "plaintext"
        };
    }

    private static string EscapeJs(string value)
    {
        return JsonSerializer.Serialize(value);
    }

    /// <summary>
    ///     Resolves the folder served over the editor virtual host: the bundled Monaco build in
    ///     the app output directory, or a generated fallback editor when the bundle is missing.
    /// </summary>
    private static string EnsureEditorContentRoot()
    {
        var bundledRoot = Path.Combine(AppContext.BaseDirectory, "EditorMonaco");
        if (File.Exists(Path.Combine(bundledRoot, "vs", "loader.js"))
            && File.Exists(Path.Combine(bundledRoot, EditorHostPageName)))
        {
            return bundledRoot;
        }

        var fallbackRoot = HTWind.Services.Application.HTWindPathProvider.GetEditorFallbackDirectory();
        Directory.CreateDirectory(fallbackRoot);

        var fallbackPagePath = Path.Combine(fallbackRoot, EditorHostPageName);
        if (!File.Exists(fallbackPagePath))
        {
            File.WriteAllText(fallbackPagePath, BuildFallbackHostHtml(), Encoding.UTF8);
        }

        return fallbackRoot;
    }

    private static string BuildFallbackHostHtml()
    {
        return """
               <!DOCTYPE html>
               <html>
               <head>
                 <meta charset="utf-8" />
                 <style>
                   html, body, #fallback {
                     width: 100%; height: 100%; margin: 0; padding: 0; box-sizing: border-box;
                     border: none; resize: none; padding: 12px; background: #1e1e1e; color: #d4d4d4;
                     font: 14px Consolas, 'Courier New', monospace; overflow: auto;
                   }
                 </style>
               </head>
               <body>
                 <textarea id="fallback"></textarea>
                 <script>
                   let fallbackTimer = null;
                   window.isEditorReady = function () { return true; };
                   window.setEditorDocument = function (name, base64, language) {
                     const bytes = Uint8Array.from(atob(base64 || ''), c => c.charCodeAt(0));
                     document.getElementById('fallback').value = new TextDecoder().decode(bytes);
                   };
                   window.getActiveDocumentName = function () { return null; };
                   window.getDocumentStates = function () { return JSON.stringify([{ name: 'fallback', language: 'plaintext', dirty: false }]); };
                   window.getDocumentContent = function () { return document.getElementById('fallback').value; };
                   window.getDocumentContentBase64 = function () { return btoa(unescape(encodeURIComponent(document.getElementById('fallback').value))); };
                   window.markDocumentSaved = function () { };
                   window.getEditorContent = function () { return document.getElementById('fallback').value; };
                   function publish() {
                     if (!window.chrome || !window.chrome.webview || !window.chrome.webview.postMessage) { return; }
                     window.chrome.webview.postMessage({ type: 'contentChanged', name: null, content: document.getElementById('fallback').value });
                   }
                   document.getElementById('fallback').addEventListener('input', function () {
                     if (fallbackTimer) { clearTimeout(fallbackTimer); }
                     fallbackTimer = setTimeout(publish, 180);
                   });
                   if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
                     window.chrome.webview.postMessage({ type: 'editorReady' });
                   }
                 </script>
               </body>
               </html>
               """;
    }

    private static async Task WaitEditorReadyAsync(CoreWebView2 coreWebView)
    {
        var readyTaskSource = new TaskCompletionSource(
          TaskCreationOptions.RunContinuationsAsynchronously
        );

        EventHandler<CoreWebView2WebMessageReceivedEventArgs>? handler = null;
        handler = (_, e) =>
          {
              if (!IsEditorReadyMessage(e.WebMessageAsJson))
              {
                  return;
              }

              if (handler is not null)
              {
                  coreWebView.WebMessageReceived -= handler;
              }

              readyTaskSource.TrySetResult();
          };

        coreWebView.WebMessageReceived += handler;

        try
        {
            var completedTask = await Task.WhenAny(
              readyTaskSource.Task,
              Task.Delay(TimeSpan.FromSeconds(8))
            );
            if (completedTask == readyTaskSource.Task)
            {
                await readyTaskSource.Task;
            }
        }
        finally
        {
            if (handler is not null)
            {
                coreWebView.WebMessageReceived -= handler;
            }
        }
    }

    private static bool IsEditorReadyMessage(string webMessageAsJson)
    {
        try
        {
            using var document = JsonDocument.Parse(webMessageAsJson);
            var root = document.RootElement;
            return root.TryGetProperty("type", out var typeElement)
              && string.Equals(typeElement.GetString(), "editorReady", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
