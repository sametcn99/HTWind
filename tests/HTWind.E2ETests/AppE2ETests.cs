using System.IO;
using System.Windows;

using HTWind.E2ETests.Support;
using HTWind.Model;
using HTWind.Services.Application;
using HTWind.Services.Widget;
using HTWind.Views.Pages;

using Xunit;

namespace HTWind.E2ETests;

/// <summary>
///     Launches the real application in-process with an isolated data directory and walks the
///     primary user flows: the first-run risk consent gate, main-window navigation, the widget
///     lifecycle (add → show → lock → pin → workspace profile → delete), and the tray close /
///     exit behavior.
/// </summary>
public sealed class AppE2ETests
{
    [Fact]
    public async Task FullApplicationLifecycle_FlowsEndToEnd()
    {
        var result = StaAppSession.Run(async app =>
        {
            // ---- First-run risk consent gate -------------------------------------------
            await StaAppSession.WaitForAsync(
                () => app.Windows.OfType<RiskConsentWindow>().Any(),
                "the risk consent window",
                TimeSpan.FromSeconds(20));
            var consent = app.Windows.OfType<RiskConsentWindow>().First();
            Assert.False(new ExecutionRiskConsentService().HasAccepted());

            StaAppSession.RaiseClick(consent, "Accept_Click");

            await StaAppSession.WaitForAsync(
                () => new ExecutionRiskConsentService().HasAccepted(),
                "the risk-consent.accepted marker file",
                TimeSpan.FromSeconds(5));

            // ---- Main window ------------------------------------------------------------
            await StaAppSession.WaitForAsync(
                () => app.MainWindow is MainWindow mainWindow && mainWindow.IsLoaded,
                "the main window",
                TimeSpan.FromSeconds(30));
            var main = (MainWindow)app.MainWindow;

            // ---- Navigation: Home / Gallery / Settings / About ---------------------------
            await NavigateTo(main, main.HomeNavItem, "Home", typeof(HomePage));

            await NavigateTo(main, main.GalleryNavItem, "Gallery", typeof(GalleryPage));
            var galleryPage = (GalleryPage)GetCachedPage(main, "Gallery")!;

            await NavigateTo(main, main.SettingsNavItem, "Settings", typeof(SettingsPage));

            await NavigateTo(main, main.AboutNavItem, "About", typeof(AboutPage));

            await NavigateTo(main, main.HomeNavItem, "Home", typeof(HomePage));

            // ---- Widget manager via the real Gallery page -------------------------------
            var manager = FindWidgetManager(galleryPage);

            // The bootstrapper records 21 default built-in widgets (hidden).
            await StaAppSession.WaitForAsync(
                () => manager.Widgets.Count >= 21,
                "the 21 default built-in widgets",
                TimeSpan.FromSeconds(60));

            var templatePath = Directory
                .EnumerateFiles(HTWindPathProvider.GetTemplateDirectory(), "clock.html", SearchOption.AllDirectories)
                .First();

            // ---- Widget lifecycle: add → show → lock → pin → profile → delete ----------
            var countBefore = manager.Widgets.Count;
            manager.AddWidget(templatePath, isVisible: true);

            WidgetModel? model = null;
            await StaAppSession.WaitForAsync(
                () =>
                {
                    if (manager.Widgets.Count == countBefore + 1)
                    {
                        model = manager.Widgets[^1];
                        return true;
                    }

                    return false;
                },
                "the added clock widget model",
                TimeSpan.FromSeconds(10));
            Assert.True(model!.IsVisible);

            WidgetWindow? widgetWindow = null;
            await StaAppSession.WaitForAsync(
                () =>
                {
                    widgetWindow = app.Windows.OfType<WidgetWindow>()
                        .FirstOrDefault(w => w.Model.Id == model.Id);
                    return widgetWindow is not null;
                },
                "the clock widget window",
                TimeSpan.FromSeconds(15));
            Assert.Equal(Visibility.Visible, widgetWindow!.Visibility);

            // Lock: interaction overlay disabled.
            model.IsLocked = true;
            manager.ApplyVisibility(model);
            manager.ApplyPinState(model);
            Assert.True(widgetWindow.Model.IsLocked);

            // Pin mode: topmost.
            model.PinMode = WidgetPinMode.Topmost;
            manager.ApplyPinState(model);
            Assert.True(widgetWindow.Topmost);

            // Workspace profile capture → mutate → restore.
            manager.CaptureWorkspaceProfile("e2e-profile");
            Assert.Contains("e2e-profile", manager.GetWorkspaceProfileNames());

            model.IsVisible = false;
            manager.ApplyVisibility(model);
            Assert.False(model.IsVisible);

            Assert.True(manager.ApplyWorkspaceProfile("e2e-profile"));
            await StaAppSession.WaitForAsync(
                () => model.IsVisible,
                "the workspace profile restoring widget visibility",
                TimeSpan.FromSeconds(10));

            // Delete.
            manager.RemoveWidget(model);
            await StaAppSession.WaitForAsync(
                () => manager.Widgets.Count == countBefore
                    && app.Windows.OfType<WidgetWindow>().All(w => w.Model.Id != model.Id),
                "the widget removal",
                TimeSpan.FromSeconds(15));

            // ---- Tray behavior: close hides, exit shuts down -----------------------------
            main.Close();
            await StaAppSession.WaitForAsync(
                () => !main.IsVisible,
                "the main window hiding instead of closing",
                TimeSpan.FromSeconds(10));
            Assert.True(app.Windows.OfType<MainWindow>().Any(), "a hidden main window stays alive.");

            // Tray Exit is the final action: it shuts the dispatcher down, so mark completion
            // first and let the application exit itself.
            StaAppSession.MarkScenarioComplete();
            StaAppSession.RaiseClick(main, "TrayIcon_Exit_Click");
        });

        Assert.True(result.Succeeded, $"E2E scenario failed:{Environment.NewLine}{result.Failure}");
    }

    private static async Task NavigateTo(MainWindow main, object navigationItem, string name, Type expectedPageType)
    {
        // Drive the production navigation path (item activation + page cache) directly.
        var navigate = main.GetType().GetMethod(
            "NavigateUsingItem",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (navigate is null)
        {
            throw new InvalidOperationException("MainWindow.NavigateUsingItem was not found.");
        }

        navigate.Invoke(main, [navigationItem, name]);

        // The page cache is the authoritative navigation state; WPF-UI swaps the visual frame
        // through a transition.
        await StaAppSession.WaitForAsync(
            () => expectedPageType.IsInstanceOfType(GetCachedPage(main, name)),
            $"navigation to {expectedPageType.Name}",
            TimeSpan.FromSeconds(10));
    }

    private static object? GetCachedPage(MainWindow main, string tag)
    {
        var cacheField = main.GetType().GetField(
            "_pageCache",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (cacheField?.GetValue(main) is not System.Collections.IDictionary cache)
        {
            throw new InvalidOperationException("MainWindow._pageCache was not found.");
        }

        return cache.Contains(tag) ? cache[tag] : null;
    }

    /// <summary>Reads the page hosted inside the WPF-UI navigation frame via the visual tree.</summary>
    private static object? GetNavigationViewPage(MainWindow main)
    {
        return FindFrame(main.NavigationView)?.Content;
    }

    private static System.Windows.Controls.Frame? FindFrame(DependencyObject parent)
    {
        var childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < childrenCount; index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is System.Windows.Controls.Frame frame)
            {
                return frame;
            }

            var descendant = FindFrame(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static WidgetManager FindWidgetManager(GalleryPage galleryPage)
    {
        var field = typeof(GalleryPage)
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
            .FirstOrDefault(f => typeof(IWidgetManager).IsAssignableFrom(f.FieldType)
                || f.FieldType == typeof(WidgetManager));
        if (field is null)
        {
            throw new InvalidOperationException("GalleryPage does not expose a widget manager field.");
        }

        return (WidgetManager)field.GetValue(galleryPage)!;
    }
}
