using System.IO;
using System.Text.Json;

using HTWind.RuntimeTests.Support;
using HTWind.Services.WebView;

using Xunit;

namespace HTWind.RuntimeTests;

[Collection(WebView2Collection.CollectionName)]
public sealed class AppLauncherTests(WidgetHostSession session)
{
    [Fact]
    public async Task Run_LaunchesCustomCommandFromPathContainingSpacesAndApostrophe()
    {
        Assert.SkipUnless(
            OperatingSystem.IsWindows() && WebView2Runtime.IsAvailable,
            "This regression requires Windows and the WebView2 Runtime.");

        var originalAuthorization = session.PowerShellAuthorization;
        var appDirectory = Path.Combine(TestPaths.CreateTempDirectory("launcher's apps"), "test app folder");
        Directory.CreateDirectory(appDirectory);
        var markerPath = Path.Combine(appDirectory, "started.marker");
        var commandPath = Path.Combine(appDirectory, "launcher's test.cmd");
        await File.WriteAllTextAsync(commandPath, $"@echo launched>{QuoteForCmd(markerPath)}\r\n", TestContext.Current.CancellationToken);

        try
        {
            session.PowerShellAuthorization = PowerShellExecutionAuthorization.Full;
            await session.NavigateFileAsync(
                Path.Combine(TestPaths.TemplatesDirectory, "app-launcher.html"),
                "app-launcher-regression");

            var runInvocation = $"run({JsonSerializer.Serialize(commandPath)}); 'invoked';";
            await session.EvaluateAsync(runInvocation);

            var deadline = DateTime.UtcNow.AddSeconds(20);
            string? message = null;
            while (DateTime.UtcNow < deadline)
            {
                message = (await session.EvaluateAsync("document.getElementById('msg').textContent")).GetString();
                if (File.Exists(markerPath) && message?.Contains(" started", StringComparison.Ordinal) == true)
                {
                    break;
                }

                await Task.Delay(100, TestContext.Current.CancellationToken);
            }

            Assert.True(File.Exists(markerPath), $"The app-launcher template did not start the custom command: {message}");
            Assert.Equal("launched", (await File.ReadAllTextAsync(markerPath, TestContext.Current.CancellationToken)).Trim());
            Assert.Contains(" started", message, StringComparison.Ordinal);
        }
        finally
        {
            session.PowerShellAuthorization = originalAuthorization;
        }
    }

    private static string QuoteForCmd(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
