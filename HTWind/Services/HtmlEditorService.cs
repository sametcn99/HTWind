using System.IO;
using System.Text;
using System.Text.Json;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace HTWind.Services;

public sealed class HtmlEditorService : IHtmlEditorService
{
    public async Task InitializeEditorAsync(
        WebView2 editorWebView,
        string filePath,
        EventHandler<CoreWebView2WebMessageReceivedEventArgs> webMessageReceivedHandler
    )
    {
        ArgumentNullException.ThrowIfNull(editorWebView);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(webMessageReceivedHandler);

        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: GetWebViewUserDataFolder());
        await editorWebView.EnsureCoreWebView2Async(env);
        editorWebView.CoreWebView2.WebMessageReceived += webMessageReceivedHandler;

        editorWebView.NavigateToString(GetMonacoHostHtml());
        await WaitEditorReadyAsync(editorWebView);
        await PushFileContentToEditorAsync(editorWebView, filePath);
    }

    public async Task<string> GetEditorContentAsync(WebView2 editorWebView)
    {
        ArgumentNullException.ThrowIfNull(editorWebView);

        var scriptResult = await editorWebView.ExecuteScriptAsync(
            "window.getEditorContent ? window.getEditorContent() : '';"
        );
        return JsonSerializer.Deserialize<string>(scriptResult) ?? string.Empty;
    }

    public async Task SaveEditorContentAsync(WebView2 editorWebView, string filePath)
    {
        ArgumentNullException.ThrowIfNull(editorWebView);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var content = await GetEditorContentAsync(editorWebView);
        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
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

    private static async Task WaitEditorReadyAsync(WebView2 editorWebView)
    {
        for (var i = 0; i < 80; i++)
        {
            var result = await editorWebView.ExecuteScriptAsync(
                "window.isEditorReady ? window.isEditorReady() : false;"
            );
            if (string.Equals(result, "true", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Task.Delay(100);
        }
    }

    private static async Task PushFileContentToEditorAsync(WebView2 editorWebView, string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
        await editorWebView.ExecuteScriptAsync($"window.setEditorContent('{encoded}');");
    }

    private static string GetMonacoHostHtml()
    {
        return """
               <!DOCTYPE html>
               <html>
               <head>
                 <meta charset="utf-8" />
                 <style>
                   html, body, #container {
                     width: 100%;
                     height: 100%;
                     margin: 0;
                     padding: 0;
                     overflow: hidden;
                     background: #1e1e1e;
                   }
                   #fallback {
                     display: none;
                     width: 100%;
                     height: 100%;
                     box-sizing: border-box;
                     border: none;
                     resize: none;
                     padding: 12px;
                     background: #1e1e1e;
                     color: #d4d4d4;
                     font: 14px Consolas, 'Courier New', monospace;
                   }
                 </style>
                 <script src="https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.52.2/min/vs/loader.min.js"></script>
               </head>
               <body>
                 <div id="container"></div>
                 <textarea id="fallback"></textarea>

                 <script>
                   let editor = null;
                   let useFallback = false;
                   let publishTimer = null;

                   function b64ToUtf8(b64) {
                     const bytes = Uint8Array.from(atob(b64), c => c.charCodeAt(0));
                     return new TextDecoder().decode(bytes);
                   }

                   window.isEditorReady = function () {
                     return editor !== null || useFallback;
                   };

                   window.setEditorContent = function (base64) {
                     const content = b64ToUtf8(base64 || '');
                     if (useFallback) {
                       document.getElementById('fallback').value = content;
                       return;
                     }
                     if (editor) {
                       editor.setValue(content);
                     }
                   };

                   window.getEditorContent = function () {
                     if (useFallback) {
                       return document.getElementById('fallback').value;
                     }
                     return editor ? editor.getValue() : '';
                   };

                   function publishContentChanged() {
                     if (!window.chrome || !window.chrome.webview || !window.chrome.webview.postMessage) {
                       return;
                     }

                     window.chrome.webview.postMessage({
                       type: 'contentChanged',
                       content: window.getEditorContent()
                     });
                   }

                   function schedulePublish() {
                     if (publishTimer) {
                       clearTimeout(publishTimer);
                     }

                     publishTimer = setTimeout(function () {
                       publishContentChanged();
                     }, 180);
                   }

                   function configureLanguageServices() {
                     monaco.languages.html.htmlDefaults.setOptions({
                       validate: true,
                       format: {
                         tabSize: 2,
                         insertSpaces: true,
                         wrapLineLength: 120
                       },
                       suggest: {
                         html5: true
                       }
                     });

                     monaco.languages.css.cssDefaults.setOptions({
                       validate: true,
                       lint: {
                         unknownProperties: 'warning',
                         duplicateProperties: 'warning',
                         emptyRules: 'warning',
                         important: 'ignore'
                       }
                     });

                     monaco.languages.typescript.javascriptDefaults.setEagerModelSync(true);
                     monaco.languages.typescript.javascriptDefaults.setCompilerOptions({
                       allowNonTsExtensions: true,
                       target: monaco.languages.typescript.ScriptTarget.ES2020,
                       checkJs: true,
                       noEmit: true
                     });
                     monaco.languages.typescript.javascriptDefaults.setDiagnosticsOptions({
                       noSemanticValidation: false,
                       noSyntaxValidation: false
                     });
                   }

                   function validateHtmlStructure() {
                     if (!editor || useFallback) {
                       return;
                     }

                     const value = editor.getValue();
                     const model = editor.getModel();
                     if (!model) {
                       return;
                     }

                     const parser = new DOMParser();
                     const parsed = parser.parseFromString(value, 'text/html');
                     const parserError = parsed.querySelector('parsererror');
                     if (!parserError) {
                       monaco.editor.setModelMarkers(model, 'HTWind-html-parse', []);
                       return;
                     }

                     const text = parserError.textContent || 'HTML parse error';
                     const lineMatch = text.match(/line\s*(\d+)/i);
                     const colMatch = text.match(/column\s*(\d+)/i);
                     const line = lineMatch ? Number(lineMatch[1]) : 1;
                     const column = colMatch ? Number(colMatch[1]) : 1;

                     monaco.editor.setModelMarkers(model, 'HTWind-html-parse', [
                       {
                         severity: monaco.MarkerSeverity.Error,
                         message: text,
                         startLineNumber: line,
                         startColumn: column,
                         endLineNumber: line,
                         endColumn: column + 1
                       }
                     ]);
                   }

                   function scheduleValidation() {
                     if (!editor || useFallback) {
                       return;
                     }

                     setTimeout(validateHtmlStructure, 0);
                   }

                   function onEditorContentChanged() {
                     schedulePublish();
                     scheduleValidation();
                   }

                   function activateFallback() {
                     useFallback = true;
                     document.getElementById('container').style.display = 'none';
                     const fallback = document.getElementById('fallback');
                     fallback.style.display = 'block';
                     fallback.addEventListener('input', schedulePublish);
                   }

                   if (typeof require === 'function') {
                     require.config({ paths: { 'vs': 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.52.2/min/vs' } });
                     require(['vs/editor/editor.main'], function () {
                       configureLanguageServices();
                       editor = monaco.editor.create(document.getElementById('container'), {
                         value: '',
                         language: 'html',
                         theme: 'vs-dark',
                         automaticLayout: true,
                         minimap: { enabled: true },
                         scrollBeyondLastLine: false,
                         quickSuggestions: {
                           other: true,
                           comments: false,
                           strings: true
                         },
                         suggestOnTriggerCharacters: true,
                         wordBasedSuggestions: 'currentDocument',
                         parameterHints: { enabled: true },
                         autoClosingBrackets: 'always',
                         autoClosingQuotes: 'always',
                         formatOnPaste: true,
                         formatOnType: true
                       });
                       editor.onDidChangeModelContent(onEditorContentChanged);
                       scheduleValidation();
                     }, activateFallback);
                   } else {
                     activateFallback();
                   }

                   setTimeout(function () {
                     if (!editor && !useFallback) {
                       activateFallback();
                     }
                   }, 4000);
                 </script>
               </body>
               </html>
               """;
    }

    private static string GetWebViewUserDataFolder()
    {
        var path = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
          "HTWind",
          "WebView2",
          "Editor"
        );

        Directory.CreateDirectory(path);
        return path;
    }
}
