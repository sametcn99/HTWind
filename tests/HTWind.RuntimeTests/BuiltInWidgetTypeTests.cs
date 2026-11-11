using System.IO;
using System.Reflection;

using HTWind.Services.Templates;

using Xunit;

namespace HTWind.RuntimeTests;

/// <summary>
///     Contract tests over the built-in widget catalog: every BuiltInWidgetType member maps to
///     an embedded template resource that exists and is real HTML, and the shipped widget
///     package schema is valid JSON Schema.
/// </summary>
public sealed class BuiltInWidgetTypeTests
{
    private static readonly Lazy<IReadOnlyDictionary<BuiltInWidgetType, string>> EmbeddedTemplateMap =
        new(() =>
            (IReadOnlyDictionary<BuiltInWidgetType, string>)(typeof(WidgetTemplateService)
                .GetField("EmbeddedTemplateMap", BindingFlags.NonPublic | BindingFlags.Static)
                ?.GetValue(null)
            ?? throw new InvalidOperationException(
                "WidgetTemplateService.EmbeddedTemplateMap was not found via reflection.")));

    public static TheoryData<string, string> TemplateResources()
    {
        var data = new TheoryData<string, string>();
        foreach (var (widgetType, resourceName) in EmbeddedTemplateMap.Value)
        {
            data.Add(widgetType.ToString(), resourceName);
        }

        return data;
    }

    [Fact]
    public void EveryBuiltInWidgetTypeHasAnEmbeddedTemplate()
    {
        var map = EmbeddedTemplateMap.Value;
        foreach (var widgetType in Enum.GetValues<BuiltInWidgetType>())
        {
            Assert.True(
                map.ContainsKey(widgetType),
                $"BuiltInWidgetType.{widgetType} has no embedded template mapping.");
        }

        Assert.Equal(Enum.GetValues<BuiltInWidgetType>().Length, map.Count);
    }

    [Theory]
    [MemberData(nameof(TemplateResources))]
    public void EmbeddedTemplateResource_ExistsAndIsHtml(string widgetTypeName, string resourceName)
    {
        _ = widgetTypeName;
        var assembly = typeof(WidgetTemplateService).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);

        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.DoesNotContain("\0", content, StringComparison.Ordinal);
        Assert.Contains(
            "<html",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WidgetPackageSchema_IsValidJsonSchema()
    {
        var assembly = typeof(WidgetTemplateService).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("htwind.widget.schema.json", StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("$schema", out var schemaElement));
        Assert.Contains(
            "json-schema.org",
            schemaElement.GetString(),
            StringComparison.OrdinalIgnoreCase);
        Assert.True(root.TryGetProperty("type", out var typeElement));
        Assert.Equal("object", typeElement.GetString());
        Assert.True(root.TryGetProperty("properties", out var propertiesElement));
        Assert.Equal(System.Text.Json.JsonValueKind.Object, propertiesElement.ValueKind);
    }
}
