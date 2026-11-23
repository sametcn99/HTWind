using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

using HTWind.Model;
using HTWind.RuntimeTests.Support;
using HTWind.Services.WebView;

using UiButton = Wpf.Ui.Controls.Button;
using UiTextBox = Wpf.Ui.Controls.TextBox;
using UiTextBlock = System.Windows.Controls.TextBlock;

using Xunit;

namespace HTWind.RuntimeTests;

[Collection(WebView2Collection.CollectionName)]
public sealed class WidgetSettingsWindowCoverageTests
{
    [Fact]
    public async Task LoadedWindow_ShowsSavedValuesAndSavePersistsAllControlTypes()
    {
        var widget = CreateWidget("settings-values", out var widgetFile, out var api);
        const string SavedText = "O'Brien — \"hello\" \\ café";
        const string EditedText = "Updated \"value\" \\";
        api.SaveWidgetSettings(widgetFile, new JsonObject
        {
            ["title"] = SavedText,
            ["enabled"] = true,
            ["count"] = 17,
            ["theme"] = "dark",
        });

        using var sta = new StaRunner();
        await sta.RunAsync<bool>(() =>
        {
            var callbackCount = 0;
            var window = CreateWindow(widget, api, () => callbackCount++);
            Exception? loadFailure = null;
            window.Loaded += (_, _) =>
            {
                try
                {
                    var controls = GetControlsByLabel(window);
                    Assert.Equal(SavedText, Assert.IsType<UiTextBox>(controls["Title"]).Text);
                    Assert.True(Assert.IsType<CheckBox>(controls["Enabled"]).IsChecked);
                    Assert.Equal("17", Assert.IsType<UiTextBox>(controls["Count"]).Text);
                    Assert.Equal("dark", Assert.IsType<ComboBox>(controls["Theme"]).SelectedItem);

                    Assert.IsType<UiTextBox>(controls["Title"]).Text = EditedText;
                    Assert.IsType<CheckBox>(controls["Enabled"]).IsChecked = false;
                    Assert.IsType<UiTextBox>(controls["Count"]).Text = "23.5";
                    Assert.IsType<ComboBox>(controls["Theme"]).SelectedItem = "light";

                    var save = Assert.IsType<UiButton>(window.FindName("SaveButton"));
                    save.RaiseEvent(new RoutedEventArgs(UiButton.ClickEvent));
                }
                catch (Exception exception)
                {
                    loadFailure = exception;
                    window.Close();
                }
            };

            try
            {
                var dialogResult = window.ShowDialog();
                Assert.Null(loadFailure);
                Assert.True(dialogResult);
                Assert.Equal(1, callbackCount);

                var persisted = api.GetWidgetSettingsJson(widgetFile);
                var settings = JsonNode.Parse(persisted!)!.AsObject();
                Assert.Equal(EditedText, settings["title"]!.GetValue<string>());
                Assert.False(settings["enabled"]!.GetValue<bool>());
                Assert.Equal(23.5, settings["count"]!.GetValue<double>());
                Assert.Equal("light", settings["theme"]!.GetValue<string>());
                return Task.FromResult(true);
            }
            finally
            {
                if (window.IsVisible)
                {
                    window.Close();
                }
            }
        });
    }

    [Fact]
    public async Task LoadedWindow_WithoutSchemaShowsMessageAndDisablesSave()
    {
        var folder = TestPaths.CreateTempDirectory("settings-no-schema");
        var widgetFile = Path.Combine(folder, "widget.html");
        await File.WriteAllTextAsync(widgetFile, "<html></html>", TestContext.Current.CancellationToken);
        var widget = new WidgetModel { Id = "no-schema", FilePath = widgetFile };
        var api = new WidgetHostApiService();

        using var sta = new StaRunner();
        await sta.RunAsync(async () =>
        {
            var window = CreateWindow(widget, api);
            try
            {
                await ShowAndWaitForLoadedAsync(window);
                var save = Assert.IsType<UiButton>(window.FindName("SaveButton"));
                Assert.False(save.IsEnabled);
                var settingsHost = Assert.IsType<StackPanel>(window.FindName("SettingsHost"));
                Assert.Contains(settingsHost.Children.OfType<UiTextBlock>(), text => !string.IsNullOrWhiteSpace(text.Text));
                return true;
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static WidgetModel CreateWidget(string purpose, out string widgetFile, out WidgetHostApiService api)
    {
        var folder = TestPaths.CreateTempDirectory(purpose);
        widgetFile = Path.Combine(folder, "widget.html");
        File.WriteAllText(widgetFile, "<html></html>");
        File.WriteAllText(Path.Combine(folder, WidgetPackageConstants.ManifestFileName), """
            {
              "schemaVersion": 1,
              "name": "settings-test",
              "widgets": [{ "widgetId": "settings-test", "name": "widget", "relativePath": ".", "entryFile": "widget.html" }],
              "settingsSchema": [
                { "key": "title", "label": "Title", "type": "text", "default": "default" },
                { "key": "enabled", "label": "Enabled", "type": "boolean", "default": false },
                { "key": "count", "label": "Count", "type": "number", "default": 1 },
                { "key": "theme", "label": "Theme", "type": "select", "options": ["light", "dark"], "default": "light" }
              ]
            }
            """);
        api = new WidgetHostApiService();
        return new WidgetModel { Id = purpose, FilePath = widgetFile };
    }

    private static WidgetSettingsWindow CreateWindow(WidgetModel model, WidgetHostApiService api, Action? onSaved = null) =>
        new(model, api, onSaved)
        {
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -32000,
            Top = 0,
        };

    private static Task ShowAndWaitForLoadedAsync(Window window)
    {
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Loaded += (_, _) => loaded.TrySetResult();
        window.Show();
        return loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static Dictionary<string, FrameworkElement> GetControlsByLabel(WidgetSettingsWindow window)
    {
        var result = new Dictionary<string, FrameworkElement>();
        var settingsHost = Assert.IsType<StackPanel>(window.FindName("SettingsHost"));
        foreach (var stack in settingsHost.Children.OfType<StackPanel>())
        {
            if (stack.Children.Count < 2 || stack.Children[0] is not UiTextBlock label)
            {
                continue;
            }

            result[label.Text] = stack.Children[1] as FrameworkElement
                ?? throw new InvalidOperationException($"No control for setting '{label.Text}'.");
        }

        return result;
    }
}
