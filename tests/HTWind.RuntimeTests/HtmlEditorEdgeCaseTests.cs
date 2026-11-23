using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;

using HTWind.RuntimeTests.Support;
using HTWind.Services.Editor;
using HTWind.Services.WebView;

using Microsoft.Web.WebView2.Wpf;

using Xunit;

namespace HTWind.RuntimeTests;

[Collection(WebView2Collection.CollectionName)]
public sealed class HtmlEditorEdgeCaseTests
{
    [Theory]
    [InlineData("{\"type\":\"contentChanged\",\"content\":\"<p>ok</p>\"}", true, "<p>ok</p>")]
    [InlineData("{\"type\":\"contentChanged\",\"content\":\"\"}", true, "")]
    [InlineData("{\"type\":\"contentChanged\",\"content\":null}", true, "")]
    [InlineData("{\"type\":\"contentChanged\",\"content\":17}", false, "")]
    [InlineData("{\"type\":\"editorReady\",\"content\":\"ignored\"}", false, "")]
    [InlineData("{\"content\":\"missing type\"}", false, "")]
    [InlineData("not json", false, "")]
    public void TryReadContentChangedPayload_ValidatesShapeAndPreservesContent(
        string json,
        bool expectedSuccess,
        string expectedContent)
    {
        var service = CreateService();

        var success = service.TryReadContentChangedPayload(json, out var content);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedContent, content);
    }

    [Theory]
    [InlineData("{\"type\":\"contentChanged\",\"name\":\"café's.html\"}", true, "café's.html")]
    [InlineData("{\"type\":\"contentChanged\"}", true, null)]
    [InlineData("{\"type\":\"contentChanged\",\"name\":null}", true, null)]
    [InlineData("{\"type\":\"contentChanged\",\"name\":5}", true, null)]
    [InlineData("{\"type\":\"editorReady\",\"name\":\"x\"}", false, null)]
    [InlineData("{\"type\":5}", false, null)]
    [InlineData("{", false, null)]
    public void TryReadContentChangedDocumentName_HandlesOptionalAndInvalidNames(
        string json,
        bool expectedSuccess,
        string? expectedName)
    {
        var service = CreateService();

        var success = service.TryReadContentChangedDocumentName(json, out var name);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedName, name);
    }

    [Theory]
    [InlineData("page.HTML", "html")]
    [InlineData("page.htm", "html")]
    [InlineData("styles.CSS", "css")]
    [InlineData("app.js", "javascript")]
    [InlineData("app.mjs", "javascript")]
    [InlineData("types.ts", "typescript")]
    [InlineData("data.json", "json")]
    [InlineData("feed.xml", "xml")]
    [InlineData("notes.md", "markdown")]
    [InlineData("config.yaml", "yaml")]
    [InlineData("config.yml", "yaml")]
    [InlineData("README", "plaintext")]
    public void GetLanguageForFile_MapsSupportedAndUnknownExtensions(string fileName, string language)
    {
        Assert.Equal(language, HtmlEditorService.GetLanguageForFile(fileName));
    }

    [Fact]
    public async Task RealMonaco_TracksDirtyStateAndSavesOnlyNamedDocument()
    {
        Assert.SkipUnless(WebView2Runtime.IsAvailable, "WebView2 Runtime is not installed on this machine.");

        var documents = TestPaths.CreateTempDirectory("editor-edge-documents");
        var firstPath = Path.Combine(documents, "café's.html");
        var secondPath = Path.Combine(documents, "другой.js");
        const string originalFirst = "<main>Zażółć gęślą jaźń — café</main>";
        const string originalSecond = "const note = 'бережно';";
        const string editedFirst = "<main>Zażółć gęślą jaźń — updated 🪁</main>";
        await File.WriteAllTextAsync(firstPath, originalFirst, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(secondPath, originalSecond, TestContext.Current.CancellationToken);

        var service = CreateService();
        await RunEditorAsync(service, firstPath, async webView =>
        {
            await service.SetEditorDocumentAsync(webView, secondPath);

            // Edit the real Monaco model while another document is selected.
            await webView.ExecuteScriptAsync(
                "monaco.editor.getModels().find(m => decodeURIComponent(m.uri.path.slice(1)) === \"café's.html\").setValue(" +
                JsonSerializer.Serialize(editedFirst) + ");");

            var states = await service.GetEditorDocumentStatesAsync(webView);
            Assert.Equal(2, states.Count);
            Assert.True(Assert.Single(states, state => state.Name == "café's.html").IsDirty);
            Assert.False(Assert.Single(states, state => state.Name == "другой.js").IsDirty);

            // Save the unselected first document; the selected second document and its file
            // must retain their original content.
            await service.SaveEditorContentAsync(webView, firstPath);
            Assert.Equal(editedFirst, await File.ReadAllTextAsync(firstPath, TestContext.Current.CancellationToken));
            Assert.Equal(originalSecond, await File.ReadAllTextAsync(secondPath, TestContext.Current.CancellationToken));
            Assert.Equal(originalSecond, await service.GetEditorContentAsync(webView));

            states = await service.GetEditorDocumentStatesAsync(webView);
            Assert.False(Assert.Single(states, state => state.Name == "café's.html").IsDirty);
            Assert.False(Assert.Single(states, state => state.Name == "другой.js").IsDirty);
        });
    }

    [Fact]
    public async Task RealWebView_MalformedDocumentStateResponseReturnsEmptyAndRecovers()
    {
        Assert.SkipUnless(WebView2Runtime.IsAvailable, "WebView2 Runtime is not installed on this machine.");

        var documents = TestPaths.CreateTempDirectory("editor-malformed-state");
        var path = Path.Combine(documents, "state.html");
        await File.WriteAllTextAsync(path, "<p>state</p>", TestContext.Current.CancellationToken);
        var service = CreateService();

        await RunEditorAsync(service, path, async webView =>
        {
            await webView.ExecuteScriptAsync("window.__realGetDocumentStates = window.getDocumentStates; window.getDocumentStates = () => '{';");
            Assert.Empty(await service.GetEditorDocumentStatesAsync(webView));

            await webView.ExecuteScriptAsync("window.getDocumentStates = window.__realGetDocumentStates;");
            var state = Assert.Single(await service.GetEditorDocumentStatesAsync(webView));
            Assert.Equal("state.html", state.Name);
        });
    }

    private static HtmlEditorService CreateService() => new(new WebViewEnvironmentProvider());

    private static async Task RunEditorAsync(
        HtmlEditorService service,
        string firstPath,
        Func<WebView2, Task> action)
    {
        using var sta = new StaRunner();
        await sta.RunAsync(async () =>
        {
            var window = new Window
            {
                Width = 640,
                Height = 480,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -32000,
                Top = 0,
            };
            var webView = new WebView2();
            window.Content = webView;
            new WindowInteropHelper(window).EnsureHandle();
            window.Show();

            try
            {
                await service.InitializeEditorAsync(webView, firstPath, (_, _) => { });
                await action(webView);
                return true;
            }
            finally
            {
                webView.Dispose();
                window.Close();
                service.ReleaseEditorEnvironment();
            }
        });
    }
}
