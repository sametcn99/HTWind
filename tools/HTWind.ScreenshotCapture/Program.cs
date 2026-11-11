using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HTWind.ScreenshotCapture;

/// <summary>
///     Retakes the marketing screenshots (page_home / page_gallery / page_settings / page_about)
///     by launching the real application in-process with an isolated data directory, navigating to
///     each page, and rendering the main window to PNG at the shipped 150% scale (920x660 → 1380x990).
///     Everything goes through reflection so no InternalsVisibleTo is required.
///     Usage: dotnet run --project tools/HTWind.ScreenshotCapture -c Release
/// </summary>
internal static class Program
{
    private const double RenderScale = 1.5;
    private const int RenderSettleMilliseconds = 600;

    private static bool _scenarioMarkedComplete;

    private static int Main()
    {
        try
        {
            File.Delete(Path.Combine(Path.GetTempPath(), "htwind-screenshot-trace.txt"));
            File.Delete(Path.Combine(Path.GetTempPath(), "htwind-screenshot-error.txt"));
        }
        catch
        {
            // Fresh diagnostics are best effort.
        }

        var repoRoot = FindRepoRoot();
        var outputDirectories = new[]
        {
            Path.Combine(repoRoot, "assets", "app_screenshots"),
            Path.Combine(repoRoot, "web", "landing", "public", "app_screenshots"),
        };
        foreach (var directory in outputDirectories)
        {
            Directory.CreateDirectory(directory);
        }

        var dataDir = Path.Combine(Path.GetTempPath(), $"htwind-screenshots-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDir);
        Environment.SetEnvironmentVariable("HTWIND_DATA_DIR", dataDir);

        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            Trace("STA thread start");
            try
            {
                var htwindAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "HTWind", StringComparison.Ordinal))
                    ?? Assembly.Load("HTWind");
                var appType = htwindAssembly.GetType("HTWind.App")
                    ?? throw new InvalidOperationException("HTWind.App type was not found.");
                var app = (Application)(Activator.CreateInstance(appType)
                    ?? throw new InvalidOperationException("Could not create the HTWind App instance."));
                appType.GetMethod("InitializeComponent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(app, null);
                // The consent window closes before the main window opens; never let
                // OnLastWindowClose end the run between the two.
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                _ = app.Dispatcher.InvokeAsync(async () =>
                {
                    Trace("scenario delegate start");
                    try
                    {
                        await RunScenario(app, outputDirectories);
                        Trace("scenario completed");
                        completion.TrySetResult(0);
                    }
                    catch (Exception exception)
                    {
                        Trace("scenario failed");
                        completion.TrySetResult(1);
                        WriteFailure(exception);
                    }
                    finally
                    {
                        app.Shutdown(0);
                    }
                });
                app.Dispatcher.UnhandledException += (_, args) =>
                {
                    Trace($"dispatcher unhandled: {args.Exception}");
                    args.Handled = true;
                };
                Trace("queueing done, calling app.Run");

                app.Run();
                Trace($"app.Run returned (shutdownStarted={app.Dispatcher.HasShutdownStarted}, completed={completion.Task.IsCompleted}, marked={_scenarioMarkedComplete})");
                if (!completion.Task.IsCompleted)
                {
                    completion.TrySetResult(_scenarioMarkedComplete ? 0 : 1);
                }
            }
            catch (Exception exception)
            {
                Trace("STA outer catch entered");
                completion.TrySetResult(1);
                WriteFailure(exception);
            }
        })
        {
            Name = "HTWind.ScreenshotCapture.STA",
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var exitCode = completion.Task.WaitAsync(TimeSpan.FromMinutes(3)).GetAwaiter().GetResult();
        try
        {
            Directory.Delete(dataDir, recursive: true);
        }
        catch
        {
            // WebView2 may still hold locks; best effort.
        }

        Console.WriteLine(exitCode == 0 ? "Screenshots captured." : "Screenshot capture FAILED.");
        return exitCode;
    }

    private static async Task RunScenario(Application app, string[] outputDirectories)
    {
        // ---- First-run risk consent gate ------------------------------------------------
        Trace("scenario start");
        var consent = await WaitForWindow(app, "RiskConsentWindow", TimeSpan.FromSeconds(20));
        Trace("consent window found");
        RaiseClick(consent, "Accept_Click");
        Trace("consent accepted");
        await WaitForAsync(
            () => app.Windows.OfType<Window>().Any(w => w.GetType().Name == "MainWindow" && w.IsLoaded),
            "the main window",
            TimeSpan.FromSeconds(30));
        var main = app.Windows.OfType<Window>().First(w => w.GetType().Name == "MainWindow");
        Trace("main window loaded");

        // ---- Home (widgets collapsed by default; expand the first card to showcase) ----
        await NavigateTo(main, "HomeNavItem", "Home", "HomePage");
        var home = GetCachedPage(main, "Home");
        await WaitForAsync(
            () => GetWidgets(home).Count > 0,
            "the default widget list",
            TimeSpan.FromSeconds(60));
        TryExpandFirstWidget(home);
        await LetRenderSettle();
        await CaptureAsync(main, outputDirectories, "page_home.png");

        // ---- Gallery ---------------------------------------------------------------------
        await NavigateTo(main, "GalleryNavItem", "Gallery", "GalleryPage");
        await LetRenderSettle();
        await CaptureAsync(main, outputDirectories, "page_gallery.png");

        // ---- Settings --------------------------------------------------------------------
        await NavigateTo(main, "SettingsNavItem", "Settings", "SettingsPage");
        await LetRenderSettle();
        await CaptureAsync(main, outputDirectories, "page_settings.png");

        // ---- About -----------------------------------------------------------------------
        await NavigateTo(main, "AboutNavItem", "About", "AboutPage");
        await LetRenderSettle();
        await CaptureAsync(main, outputDirectories, "page_about.png");

        // Tray Exit is the final action; it shuts the dispatcher down itself.
        _scenarioMarkedComplete = true;
        Trace("raising tray exit");
        RaiseClick(main, "TrayIcon_Exit_Click");
    }

    private static System.Collections.IList GetWidgets(object? page)
    {
        var dataContext = page?.GetType().GetProperty("DataContext")?.GetValue(page);
        var widgets = dataContext?.GetType().GetProperty("Widgets")?.GetValue(dataContext) as System.Collections.IList;
        return widgets ?? Array.Empty<object>();
    }

    private static void TryExpandFirstWidget(object? page)
    {
        var widgets = GetWidgets(page);
        if (widgets.Count == 0)
        {
            return;
        }

        widgets[0]!.GetType().GetProperty("IsExpanded")?.SetValue(widgets[0], true);
    }

    private static async Task<Window> WaitForWindow(Application app, string typeName, TimeSpan timeout)
    {
        Window? found = null;
        await WaitForAsync(
            () => (found = app.Windows.OfType<Window>().FirstOrDefault(w => w.GetType().Name == typeName)) is not null,
            $"the {typeName}",
            timeout);
        return found!;
    }

    private static async Task NavigateTo(Window main, string navFieldName, string cacheKey, string expectedPageTypeName)
    {
        var navItem = main.GetType().GetField(navFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(main)
            ?? throw new InvalidOperationException($"{navFieldName} was not found on the main window.");

        main.GetType().GetMethod("NavigateUsingItem", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(main, [navItem, cacheKey]);

        await WaitForAsync(
            () => string.Equals(GetCachedPage(main, cacheKey)?.GetType().Name, expectedPageTypeName, StringComparison.Ordinal),
            $"navigation to {expectedPageTypeName}",
            TimeSpan.FromSeconds(10));
    }

    private static object? GetCachedPage(Window main, string tag)
    {
        var cacheField = main.GetType().GetField("_pageCache", BindingFlags.Instance | BindingFlags.NonPublic);
        if (cacheField?.GetValue(main) is not System.Collections.IDictionary cache)
        {
            throw new InvalidOperationException("MainWindow._pageCache was not found.");
        }

        return cache.Contains(tag) ? cache[tag] : null;
    }

    private static void RaiseClick(object handlerOwner, string handlerName)
    {
        var method = handlerOwner.GetType().GetMethod(
            handlerName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Handler '{handlerName}' was not found on {handlerOwner.GetType().Name}.");

        method.Invoke(handlerOwner, [handlerOwner, new RoutedEventArgs()]);
    }

    private static async Task LetRenderSettle()
    {
        await Task.Delay(RenderSettleMilliseconds);
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Delay(100);
    }

    private static async Task CaptureAsync(Window window, string[] outputDirectories, string fileName)
    {
        var width = (int)Math.Round(window.ActualWidth * RenderScale);
        var height = (int)Math.Round(window.ActualHeight * RenderScale);
        var dpi = 96 * RenderScale;

        var bitmap = new RenderTargetBitmap(width, height, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(window);

        // BitmapEncoder.Save may only be called once; encode to memory, then write both copies.
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var memoryStream = new MemoryStream();
        encoder.Save(memoryStream);
        var pngBytes = memoryStream.ToArray();

        foreach (var directory in outputDirectories)
        {
            File.WriteAllBytes(Path.Combine(directory, fileName), pngBytes);
        }

        Console.WriteLine($"Captured {fileName} ({width}x{height}).");
        bitmap.Clear();
    }

    private static async Task WaitForAsync(Func<bool> condition, string description, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for: {description}");
    }

    private static void Trace(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "htwind-screenshot-trace.txt"),
                $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics only.
        }
    }

    private static void WriteFailure(Exception exception)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "htwind-screenshot-error.txt"),
                exception.ToString());
            Console.Error.WriteLine(exception);
        }
        catch
        {
            // Diagnostics only; never mask the original failure.
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !directory.GetDirectories(".git").Any())
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root (.git).");
    }
}
