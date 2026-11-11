using System.IO;
using System.Windows;
using System.Windows.Interop;

using HTWind.RuntimeTests.Support;
using HTWind.Services.Editor;
using HTWind.Services.WebView;

using Microsoft.Web.WebView2.Wpf;

using Xunit;

namespace HTWind.RuntimeTests;

/// <summary>
///     Boots the real HtmlEditorService over a real WebView2 runtime: Monaco is served from the
///     bundled virtual host, the editor reports readiness, and documents round-trip.
/// </summary>
[Collection(WebView2Collection.CollectionName)]
public sealed class HtmlEditorServiceTests(WidgetHostSession session)
{
    [Fact]
    public async Task InitializeEditor_ReadiesMonacoAndRoundTripsDocuments()
    {
        Assert.SkipUnless(
            WebView2Runtime.IsAvailable,
            "WebView2 Runtime is not installed on this machine.");

        var documents = TestPaths.CreateTempDirectory("editor-documents");
        var firstDocument = Path.Combine(documents, "index.html");
        var secondDocument = Path.Combine(documents, "app.js");
        const string FirstContent = "<html><body><h1>HTWind editor round trip</h1></body></html>";
        const string SecondContent = "console.log('second document');";
        await File.WriteAllTextAsync(
            firstDocument,
            FirstContent,
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            secondDocument,
            SecondContent,
            TestContext.Current.CancellationToken);

        var service = new HtmlEditorService(new WebViewEnvironmentProvider());

        await session.RunOnStaAsync(async () =>
        {
            var editorWindow = new Window
            {
                Width = 640,
                Height = 480,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -32000,
                Top = 0,
            };
            var editorWebView = new WebView2();
            editorWindow.Content = editorWebView;
            new WindowInteropHelper(editorWindow).EnsureHandle();

            // HwndHost-backed WebView2 controls need a shown window for layout to create the
            // child HWND; the window is positioned far offscreen.
            editorWindow.Show();

            try
            {
                await service.InitializeEditorAsync(
                    editorWebView,
                    firstDocument,
                    (_, _) => { });

                var content = await service.GetEditorContentAsync(editorWebView);
                Assert.Equal(FirstContent, content);

                var states = await service.GetEditorDocumentStatesAsync(editorWebView);
                var state = Assert.Single(states);
                Assert.Equal("index.html", state.Name);
                Assert.Equal("html", state.Language);
                Assert.False(state.IsDirty);

                await service.SetEditorDocumentAsync(editorWebView, secondDocument);
                var secondContent = await service.GetEditorContentAsync(editorWebView);
                Assert.Equal(SecondContent, secondContent);

                var secondByDocument = await service.GetEditorDocumentContentAsync(
                    editorWebView,
                    "app.js");
                Assert.Equal(SecondContent, secondByDocument);

                Assert.Equal("css", HtmlEditorService.GetLanguageForFile("styles.css"));
                Assert.Equal("plaintext", HtmlEditorService.GetLanguageForFile("README"));
                return true;
            }
            finally
            {
                editorWebView.Dispose();
                editorWindow.Close();
                service.ReleaseEditorEnvironment();
            }
        });
    }
}
