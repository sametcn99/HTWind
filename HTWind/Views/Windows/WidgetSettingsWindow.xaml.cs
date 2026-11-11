using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

using HTWind.Localization;
using HTWind.Model;
using HTWind.Services.WebView;

using Wpf.Ui.Controls;

using UiTextBlock = System.Windows.Controls.TextBlock;
using UiTextBox = Wpf.Ui.Controls.TextBox;

namespace HTWind;

/// <summary>
///     Auto-generates a settings form from the widget manifest's <c>settingsSchema</c> field so
///     users can configure widgets without editing code. Values are persisted in the widget's
///     own folder (.htwind-settings.json, key "settings") and injected into the widget document
///     as window.HTWind.__settings.
/// </summary>
public partial class WidgetSettingsWindow : FluentWindow
{
    private sealed class SettingField
    {
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string Type { get; set; } = "text";

        public JsonNode? Default { get; set; }

        public List<string> Options { get; set; } = [];
    }

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly WidgetModel _model;
    private readonly IWidgetHostApiService _widgetHostApiService;
    private readonly Action? _onSaved;
    private readonly List<SettingField> _fields = [];
    private readonly Dictionary<string, FrameworkElement> _inputControls = [];

    public WidgetSettingsWindow(
        WidgetModel model,
        IWidgetHostApiService widgetHostApiService,
        Action? onSaved)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _widgetHostApiService = widgetHostApiService ?? throw new ArgumentNullException(nameof(widgetHostApiService));
        _onSaved = onSaved;

        InitializeComponent();
        Loaded += WidgetSettingsWindow_Loaded;
    }

    private async void WidgetSettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= WidgetSettingsWindow_Loaded;

        var schema = TryLoadSettingsSchema();
        if (schema is null)
        {
            var noSchema = new UiTextBlock
            {
                Text = LocalizationService.Get("WidgetSettings_NoSchema"),
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8,
                Margin = new Thickness(0, 4, 0, 4)
            };
            SettingsHost.Children.Add(noSchema);
            SaveButton.IsEnabled = false;
            return;
        }

        var savedValues = _widgetHostApiService.GetWidgetSettingsJson(_model.FilePath);
        JsonObject? savedObject = null;
        try
        {
            savedObject = string.IsNullOrWhiteSpace(savedValues)
                ? null
                : JsonNode.Parse(savedValues) as JsonObject;
        }
        catch
        {
            savedObject = null;
        }

        JsonObject? savedSettings = null;
        if (savedObject is not null)
        {
            savedSettings = savedObject[WidgetStorageService.SettingsKey] as JsonObject;
        }

        foreach (var field in _fields)
        {
            var currentValue = savedSettings is not null && savedSettings.ContainsKey(field.Key)
                ? savedSettings[field.Key]
                : field.Default;
            SettingsHost.Children.Add(BuildFieldControl(field, currentValue));
        }
    }

    private List<SettingField>? TryLoadSettingsSchema()
    {
        if (string.IsNullOrWhiteSpace(_model.FilePath))
        {
            return null;
        }

        try
        {
            var widgetRootPath = Path.GetDirectoryName(Path.GetFullPath(_model.FilePath));
            if (string.IsNullOrWhiteSpace(widgetRootPath) || !Directory.Exists(widgetRootPath))
            {
                return null;
            }

            var manifest = global::HTWind.Services.Widget.WidgetManifestHelper.LoadManifestIfPresent(widgetRootPath, ManifestJsonOptions);
            if (manifest?.SettingsSchema is not JsonElement schemaElement)
            {
                return null;
            }

            var fields = new List<SettingField>();

            if (schemaElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in schemaElement.EnumerateArray())
                {
                    var field = ParseSchemaEntry(entry);
                    if (field is not null)
                    {
                        fields.Add(field);
                    }
                }
            }
            else if (schemaElement.ValueKind == JsonValueKind.Object && schemaElement.TryGetProperty("properties", out var propertiesElement)
                && propertiesElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in propertiesElement.EnumerateObject())
                {
                    var field = ParseSchemaEntry(property.Value);
                    if (field is not null)
                    {
                        field.Key = property.Name;
                        fields.Add(field);
                    }
                }
            }

            _fields.AddRange(fields);
            return fields.Count > 0 ? fields : null;
        }
        catch
        {
            return null;
        }
    }

    private static SettingField? ParseSchemaEntry(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? key = null;
        if (entry.TryGetProperty("key", out var keyElement) && keyElement.ValueKind == JsonValueKind.String)
        {
            key = keyElement.GetString();
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var field = new SettingField
        {
            Key = key,
            Label = key,
            Type = "text"
        };

        if (entry.TryGetProperty("label", out var labelElement) && labelElement.ValueKind == JsonValueKind.String)
        {
            var label = labelElement.GetString();
            if (!string.IsNullOrWhiteSpace(label))
            {
                field.Label = label;
            }
        }

        if (entry.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String)
        {
            var type = typeElement.GetString()?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(type))
            {
                field.Type = type;
            }
        }

        if (entry.TryGetProperty("default", out var defaultElement) && defaultElement.ValueKind != JsonValueKind.Undefined)
        {
            try
            {
                field.Default = JsonNode.Parse(defaultElement.GetRawText());
            }
            catch
            {
            }
        }

        if (entry.TryGetProperty("options", out var optionsElement) && optionsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var option in optionsElement.EnumerateArray())
            {
                var optionValue = option.ValueKind == JsonValueKind.String
                    ? option.GetString()
                    : option.ValueKind == JsonValueKind.Number ? option.GetRawText() : null;
                if (!string.IsNullOrWhiteSpace(optionValue))
                {
                    field.Options.Add(optionValue);
                }
            }
        }

        return field;
    }

    private FrameworkElement BuildFieldControl(SettingField field, JsonNode? currentValue)
    {
        var container = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };

        var label = new UiTextBlock
        {
            Text = field.Label,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        };
        container.Children.Add(label);

        switch (field.Type)
        {
            case "number":
            {
                var textBox = new UiTextBox { MinHeight = 32 };
                textBox.Text = currentValue is null ? string.Empty : currentValue.ToJsonString().Trim('"');
                _inputControls[field.Key] = textBox;
                container.Children.Add(textBox);
                break;
            }
            case "boolean" or "bool" or "checkbox":
            {
                var checkBox = new System.Windows.Controls.CheckBox { MinHeight = 32 };
                checkBox.IsChecked = currentValue is not null && currentValue.ToJsonString() == "true";
                _inputControls[field.Key] = new BooleanValueHost(checkBox);
                container.Children.Add(checkBox);
                break;
            }
            case "select" when field.Options.Count > 0:
            {
                var comboBox = new System.Windows.Controls.ComboBox { MinHeight = 32, MinWidth = 220 };
                foreach (var option in field.Options)
                {
                    comboBox.Items.Add(option);
                }

                var currentText = currentValue is null ? null : currentValue.ToJsonString().Trim('"');
                comboBox.SelectedItem = field.Options.FirstOrDefault(option => option == currentText) ?? field.Options[0];
                _inputControls[field.Key] = comboBox;
                container.Children.Add(comboBox);
                break;
            }
            default:
            {
                var textBox = new UiTextBox { MinHeight = 32 };
                textBox.Text = currentValue is null ? string.Empty : currentValue.ToJsonString().Trim('"');
                _inputControls[field.Key] = textBox;
                container.Children.Add(textBox);
                break;
            }
        }

        return container;
    }

    private sealed class BooleanValueHost : FrameworkElement
    {
        public System.Windows.Controls.CheckBox CheckBox { get; }

        public BooleanValueHost(System.Windows.Controls.CheckBox checkBox)
        {
            CheckBox = checkBox;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = new JsonObject();

        foreach (var field in _fields)
        {
            if (!_inputControls.TryGetValue(field.Key, out var control))
            {
                continue;
            }

            switch (control)
            {
                case BooleanValueHost booleanHost:
                    settings[field.Key] = booleanHost.CheckBox.IsChecked == true;
                    break;
                case System.Windows.Controls.ComboBox comboBox:
                    settings[field.Key] = comboBox.SelectedItem as string ?? string.Empty;
                    break;
                case UiTextBox textBox:
                {
                    var text = textBox.Text ?? string.Empty;
                    if (field.Type == "number" && double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var number))
                    {
                        settings[field.Key] = number;
                    }
                    else
                    {
                        settings[field.Key] = text;
                    }

                    break;
                }
            }
        }

        try
        {
            _widgetHostApiService.SaveWidgetSettings(_model.FilePath, settings);
            _onSaved?.Invoke();
            DialogResult = true;
            Close();
        }
        catch (Exception exception)
        {
            var errorText = new UiTextBlock
            {
                Text = exception.Message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.IndianRed)
            };
            SettingsHost.Children.Add(errorText);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
