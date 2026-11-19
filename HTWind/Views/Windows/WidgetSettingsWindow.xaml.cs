using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

using HTWind.Localization;
using HTWind.Model;
using HTWind.Services.WebView;
using HTWind.Services.Widget;

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
    internal sealed class SettingField
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

    private void WidgetSettingsWindow_Loaded(object sender, RoutedEventArgs e)
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
        var fields = LoadSettingsSchemaForWidgetFile(_model.FilePath);
        if (fields is null || fields.Count == 0)
        {
            return null;
        }

        _fields.AddRange(fields);
        return _fields;
    }

    internal static List<SettingField>? LoadSettingsSchemaForWidgetFile(string? widgetFilePath)
    {
        if (string.IsNullOrWhiteSpace(widgetFilePath))
        {
            return null;
        }

        try
        {
            var fullFilePath = Path.GetFullPath(widgetFilePath);
            var widgetRootPath = Path.GetDirectoryName(fullFilePath);
            if (string.IsNullOrWhiteSpace(widgetRootPath) || !Directory.Exists(widgetRootPath))
            {
                return null;
            }

            var manifest = WidgetManifestHelper.LoadManifestIfPresent(widgetRootPath, ManifestJsonOptions);
            if (manifest is null)
            {
                return null;
            }

            var normalizedEntryRelative = WidgetManifestHelper.NormalizeRelativePath(
                Path.GetRelativePath(widgetRootPath, fullFilePath));
            var matchingItem = manifest.Widgets?.FirstOrDefault(item =>
                string.Equals(
                    WidgetManifestHelper.NormalizeRelativePath(item.EntryFile),
                    normalizedEntryRelative,
                    StringComparison.OrdinalIgnoreCase))
                ?? (manifest.Widgets is { Count: 1 } ? manifest.Widgets[0] : null);

            var schemaElement = matchingItem?.SettingsSchema ?? manifest.SettingsSchema;
            if (schemaElement is not JsonElement resolvedSchema)
            {
                return null;
            }

            var fields = ParseSettingsSchema(resolvedSchema);
            return fields.Count > 0 ? fields : null;
        }
        catch
        {
            return null;
        }
    }

    internal static List<SettingField> ParseSettingsSchema(JsonElement schemaElement)
    {
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

            return fields;
        }

        if (schemaElement.ValueKind != JsonValueKind.Object)
        {
            return fields;
        }

        // Support JSON Schema style: { "type": "object", "properties": { "foo": { ... } } }
        // or wrapper object: { "fields": [ ... ] }
        if (TryGetPropertyCaseInsensitive(schemaElement, "fields", out var fieldsArrayElement)
            && fieldsArrayElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in fieldsArrayElement.EnumerateArray())
            {
                var field = ParseSchemaEntry(entry);
                if (field is not null)
                {
                    fields.Add(field);
                }
            }

            return fields;
        }

        var objectContainer = TryGetPropertyCaseInsensitive(schemaElement, "properties", out var propertiesElement)
            && propertiesElement.ValueKind == JsonValueKind.Object
            ? propertiesElement
            : schemaElement;

        foreach (var property in objectContainer.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var field = ParseSchemaEntry(property.Value, fallbackKey: property.Name);
            if (field is not null)
            {
                fields.Add(field);
            }
        }

        return fields;
    }

    internal static SettingField? ParseSchemaEntry(JsonElement entry, string? fallbackKey = null)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? key = null;
        if (TryGetPropertyCaseInsensitive(entry, "key", out var keyElement) && keyElement.ValueKind == JsonValueKind.String)
        {
            key = keyElement.GetString();
        }
        else if (TryGetPropertyCaseInsensitive(entry, "id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
        {
            key = idElement.GetString();
        }
        else if (TryGetPropertyCaseInsensitive(entry, "name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
        {
            key = nameElement.GetString();
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            key = fallbackKey;
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

        if ((TryGetPropertyCaseInsensitive(entry, "label", out var labelElement)
                || TryGetPropertyCaseInsensitive(entry, "title", out labelElement))
            && labelElement.ValueKind == JsonValueKind.String)
        {
            var label = labelElement.GetString();
            if (!string.IsNullOrWhiteSpace(label))
            {
                field.Label = label;
            }
        }

        if (TryGetPropertyCaseInsensitive(entry, "type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String)
        {
            var type = typeElement.GetString()?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(type))
            {
                field.Type = NormalizeFieldType(type);
            }
        }

        if ((TryGetPropertyCaseInsensitive(entry, "default", out var defaultElement)
                || TryGetPropertyCaseInsensitive(entry, "defaultValue", out defaultElement))
            && defaultElement.ValueKind != JsonValueKind.Undefined)
        {
            try
            {
                field.Default = JsonNode.Parse(defaultElement.GetRawText());
            }
            catch
            {
            }
        }

        if ((TryGetPropertyCaseInsensitive(entry, "options", out var optionsElement)
                || TryGetPropertyCaseInsensitive(entry, "enum", out optionsElement)
                || TryGetPropertyCaseInsensitive(entry, "choices", out optionsElement))
            && optionsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var option in optionsElement.EnumerateArray())
            {
                string? optionValue = option.ValueKind switch
                {
                    JsonValueKind.String => option.GetString(),
                    JsonValueKind.Number => option.GetRawText(),
                    JsonValueKind.Object when TryGetPropertyCaseInsensitive(option, "value", out var valElem) =>
                        valElem.ValueKind == JsonValueKind.String ? valElem.GetString() : valElem.GetRawText(),
                    _ => null
                };

                if (!string.IsNullOrWhiteSpace(optionValue))
                {
                    field.Options.Add(optionValue);
                }
            }

            if (field.Options.Count > 0 && field.Type == "text")
            {
                field.Type = "select";
            }
        }

        return field;
    }

    internal static string NormalizeFieldType(string rawType)
    {
        return rawType.Trim().ToLowerInvariant() switch
        {
            "number" or "int" or "integer" or "float" or "double" => "number",
            "boolean" or "bool" or "checkbox" or "toggle" or "switch" => "boolean",
            "select" or "dropdown" or "enum" or "choice" => "select",
            _ => "text"
        };
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(propertyName, out value))
            {
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
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
            case "boolean" or "bool" or "checkbox" or "toggle" or "switch":
            {
                var checkBox = new System.Windows.Controls.CheckBox { MinHeight = 32 };
                checkBox.IsChecked = currentValue is not null
                    && string.Equals(currentValue.ToJsonString().Trim('"'), "true", StringComparison.OrdinalIgnoreCase);
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
