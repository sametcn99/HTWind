using System.IO;

using HTWind.RuntimeTests.Support;

using Xunit;

namespace HTWind.RuntimeTests;

/// <summary>
///     Loads every built-in template and every example package widget in a real WebView2
///     runtime with the production bridge and console hook, then asserts the pages settle with
///     zero error-level console output.
/// </summary>
[Collection(WebView2Collection.CollectionName)]
public sealed class TemplateSmokeTests(WidgetHostSession session)
{
    public static TheoryData<string, string> TemplatePages()
    {
        var data = new TheoryData<string, string>();
        foreach (var file in Directory.GetFiles(TestPaths.TemplatesDirectory, "*.html"))
        {
            data.Add(Path.GetFileName(file), file);
        }

        var examplesDirectory = Path.Combine(
            TestPaths.TemplatesDirectory,
            "examples",
            "multi-widget-package",
            "widgets");
        if (Directory.Exists(examplesDirectory))
        {
            foreach (var file in Directory.GetFiles(examplesDirectory, "index.html", SearchOption.AllDirectories))
            {
                data.Add($"example:{Path.GetFileName(Path.GetDirectoryName(file))}", file);
            }
        }

        return data;
    }

    [Fact]
    public void TemplateCatalog_HasTheFullBuiltInSet()
    {
        var templates = Directory.GetFiles(TestPaths.TemplatesDirectory, "*.html");
        Assert.Equal(22, templates.Length);

        var exampleWidgets = Directory.GetFiles(
            Path.Combine(TestPaths.TemplatesDirectory, "examples", "multi-widget-package", "widgets"),
            "index.html",
            SearchOption.AllDirectories);
        Assert.Equal(2, exampleWidgets.Length);
    }

    [Theory]
    [MemberData(nameof(TemplatePages))]
    public async Task Template_LoadsWithoutConsoleErrors(string name, string filePath)
    {
        Assert.SkipUnless(
            WebView2Runtime.IsAvailable,
            "WebView2 Runtime is not installed on this machine.");

        await session.NavigateFileAsync(
            filePath,
            $"smoke-{name}",
            settleTime: TimeSpan.FromMilliseconds(300));

        var hookEntries = session.RuntimeConsoleEntries;
        var hookErrors = hookEntries.Where(entry => entry.Contains("[error]", StringComparison.Ordinal)).ToList();
        Assert.True(
            hookErrors.Count == 0,
            $"'{Path.GetFileName(filePath)}' produced error-level console entries:{Environment.NewLine}{string.Join(Environment.NewLine, hookErrors)}");
    }
}

/// <summary>
///     Verifies the production console hook surfaces console.error, uncaught exceptions, and
///     rejected promises through WidgetRuntimeConsole.
/// </summary>
[Collection(WebView2Collection.CollectionName)]
public sealed class WidgetRuntimeConsoleCaptureTests(WidgetHostSession session)
{
    [Fact]
    public async Task ConsoleHook_CapturesErrorsUncaughtThrowsAndRejections()
    {
        Assert.SkipUnless(
            WebView2Runtime.IsAvailable,
            "WebView2 Runtime is not installed on this machine.");

        var pagePath = Path.Combine(
            TestPaths.CreateTempDirectory("console-capture"),
            "noisy.html");
        await File.WriteAllTextAsync(
            pagePath,
            """
            <!DOCTYPE html>
            <html>
            <head><meta charset="utf-8"><title>noisy</title></head>
            <body>
            <script>
              console.log('informational line');
              console.error('explicit console error');
              Promise.reject(new Error('unhandled promise rejection'));
              setTimeout(() => { notDefinedFunction(); }, 0);
            </script>
            </body>
            </html>
            """,
            TestContext.Current.CancellationToken);

        await session.NavigateFileAsync(
            pagePath,
            "console-capture-probe",
            settleTime: TimeSpan.FromMilliseconds(500));

        // Give the uncaught throw (setTimeout) and the rejection handlers a moment to surface.
        await Task.Delay(300, TestContext.Current.CancellationToken);

        var entries = session.RuntimeConsoleEntries;
        Assert.Contains(entries, entry => entry.Contains("[log] informational line", StringComparison.Ordinal));
        Assert.Contains(entries, entry => entry.Contains("[error] explicit console error", StringComparison.Ordinal));
        Assert.Contains(
            entries,
            entry => entry.Contains("[error]", StringComparison.Ordinal)
                && (entry.Contains("unhandled promise rejection", StringComparison.Ordinal)
                    || entry.Contains("notDefinedFunction", StringComparison.Ordinal)));
        Assert.Contains(
            entries,
            entry => entry.Contains("[error]", StringComparison.Ordinal)
                && entry.Contains("notDefinedFunction", StringComparison.Ordinal));
    }
}
