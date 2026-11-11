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
        // FluentWindow uses a DWM-composited Mica backdrop, which RenderTargetBitmap renders
        // as fully transparent. Capturing from the screen DC gets the real composited pixels
        // (Mica included), exactly what a user sees.
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        var pngBytes = await Task.Run(() => CaptureWindowBytes(hwnd));

        foreach (var directory in outputDirectories)
        {
            File.WriteAllBytes(Path.Combine(directory, fileName), pngBytes);
        }

        Console.WriteLine($"Captured {fileName} ({pngBytes.Length / 1024} KB).");
    }

    private static byte[] CaptureWindowBytes(IntPtr hwnd)
    {
        // Bring the window to the front so nothing occludes the capture.
        _ = SetForegroundWindow(hwnd);
        Thread.Sleep(400);

        if (!GetClientRect(hwnd, out var clientRect))
        {
            throw new InvalidOperationException("GetClientRect failed.");
        }

        var point = new POINT { X = 0, Y = 0 };
        if (!ClientToScreen(hwnd, ref point))
        {
            throw new InvalidOperationException("ClientToScreen failed.");
        }

        var width = clientRect.Right - clientRect.Left;
        var height = clientRect.Bottom - clientRect.Top;

        var screenDc = GetDC(IntPtr.Zero);
        var memoryDc = CreateCompatibleDC(screenDc);
        var bitmap = CreateCompatibleBitmap(screenDc, width, height);
        var previous = SelectObject(memoryDc, bitmap);
        try
        {
            if (!BitBlt(memoryDc, 0, 0, width, height, screenDc, point.X, point.Y, SRCCOPY))
            {
                throw new InvalidOperationException("BitBlt failed.");
            }

            var headerSize = System.Runtime.InteropServices.Marshal.SizeOf<BITMAPINFOHEADER>();
            var header = new BITMAPINFOHEADER
            {
                BiSize = (uint)headerSize,
                BiWidth = width,
                BiHeight = -height, // top-down rows
                BiPlanes = 1,
                BiBitCount = 32,
                BiCompression = 0, // BI_RGB
            };

            var pixels = new byte[width * height * 4];
            var headerPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(headerSize);
            try
            {
                System.Runtime.InteropServices.Marshal.StructureToPtr(header, headerPtr, false);
                if (GetDIBits(memoryDc, bitmap, 0, (uint)height, pixels, headerPtr, 0) == 0)
                {
                    throw new InvalidOperationException("GetDIBits failed.");
                }
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(headerPtr);
            }

            // BGRA → BGRA with opaque alpha so no pixel stays transparent.
            for (var index = 3; index < pixels.Length; index += 4)
            {
                pixels[index] = 255;
            }

            return EncodePng(pixels, width, height);
        }
        finally
        {
            _ = SelectObject(memoryDc, previous);
            _ = DeleteObject(bitmap);
            _ = DeleteDC(memoryDc);
            _ = ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    /// <summary>Encodes the captured pixels, normalizing to the shipped 1380x990 size.</summary>
    private static byte[] EncodePng(byte[] bgraPixels, int width, int height)
    {
        const int targetWidth = 1380;
        const int targetHeight = 990;
        BitmapSource source = BitmapSource.Create(
            width, height, 96, 96, PixelFormats.Bgra32, null, bgraPixels, width * 4);

        if (width != targetWidth || height != targetHeight)
        {
            var scale = Math.Min((double)targetWidth / width, (double)targetHeight / height);
            var scaled = new TransformedBitmap(
                source,
                new System.Windows.Media.ScaleTransform(scale, scale));
            scaled.Freeze();
            source = new CroppedBitmap(
                scaled,
                new System.Windows.Int32Rect(0, 0, targetWidth, targetHeight));
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private const uint SRCCOPY = 0x00CC0020;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool BitBlt(
        IntPtr destination, int x, int y, int width, int height,
        IntPtr source, int sourceX, int sourceY, uint rasterOperation);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern int GetDIBits(
        IntPtr dc, IntPtr bitmap, uint start, uint lines, byte[] bits, IntPtr header, uint usage);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint BiSize;
        public int BiWidth;
        public int BiHeight;
        public ushort BiPlanes;
        public ushort BiBitCount;
        public uint BiCompression;
        public uint BiSizeImage;
        public int BiXPelsPerMeter;
        public int BiYPelsPerMeter;
        public uint BiClrUsed;
        public uint BiClrImportant;
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
