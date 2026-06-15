using ScadaBuilderV2.Domain.Editor;
using ScadaBuilderV2.Domain.Elements;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Conversion;

public enum ElementPlusConversionTarget
{
    Text,
    TextInput,
    NumericReadOnly,
    NumericEditable
}

public sealed record ElementPlusConversionOptions(
    string ElementId,
    string DisplayName,
    string SourceSystem,
    string SourceDocumentId,
    string? SourcePath);

public static class ElementPlusLegacyConverter
{
    public static bool CanConvert(EditorObject source, ElementPlusConversionTarget target)
    {
        return source is LegacyDetectedObject legacy &&
            IsLegacyText(legacy) &&
            Enum.IsDefined(target);
    }

    public static ScadaElement Convert(LegacyDetectedObject source, ElementPlusConversionTarget target, ElementPlusConversionOptions options)
    {
        if (!CanConvert(source, target))
        {
            throw new InvalidOperationException($"Legacy object '{source.DisplayName}' cannot be converted to '{target}'.");
        }

        if (target is ElementPlusConversionTarget.NumericReadOnly or ElementPlusConversionTarget.NumericEditable)
        {
            return ConvertToElement(source, target, options).ToScadaElement();
        }

        var kind = target switch
        {
            ElementPlusConversionTarget.Text => ScadaElementKind.Text,
            ElementPlusConversionTarget.TextInput => ScadaElementKind.InputText,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };
        var text = string.IsNullOrWhiteSpace(source.Text) ? source.DisplayName : source.Text.Trim();
        var width = source.Bounds.Width > 0 ? source.Bounds.Width : DefaultWidth(kind);
        var height = source.Bounds.Height > 0 ? source.Bounds.Height : DefaultHeight(kind);
        var fontSize = source.Style.FontSize > 0 ? source.Style.FontSize : DefaultStyle(kind).FontSize;

        return new ScadaElement(
            options.ElementId,
            options.DisplayName,
            kind,
            new SceneBounds(
                Math.Max(0, Math.Round(source.Bounds.X)),
                Math.Max(0, Math.Round(source.Bounds.Y)),
                Math.Max(4, Math.Round(width)),
                Math.Max(4, Math.Round(height))),
            new LegacySourceTrace(options.SourceSystem, options.SourceDocumentId, source.RuntimeId, source.DisplayName, options.SourcePath),
            ScadaElementLayout.Absolute,
            DefaultStyle(kind) with
            {
                FontFamily = string.IsNullOrWhiteSpace(source.Style.FontFamily) ? DefaultStyle(kind).FontFamily : source.Style.FontFamily,
                FontSize = Math.Max(6, Math.Round(fontSize, 2)),
                Foreground = string.IsNullOrWhiteSpace(source.Style.Foreground) ? DefaultStyle(kind).Foreground : source.Style.Foreground,
                Background = NormalizeTransparentCssColor(source.Style.Background)
            },
            CreateData(kind, text));
    }

    public static Element ConvertToElement(LegacyDetectedObject source, ElementPlusConversionTarget target, ElementPlusConversionOptions options)
    {
        if (!CanConvert(source, target))
        {
            throw new InvalidOperationException($"Legacy object '{source.DisplayName}' cannot be converted to '{target}'.");
        }

        return target switch
        {
            ElementPlusConversionTarget.NumericReadOnly => CreateNumericInput(source, options, isReadOnly: true),
            ElementPlusConversionTarget.NumericEditable => CreateNumericInput(source, options, isReadOnly: false),
            _ => throw new NotSupportedException($"Target '{target}' has not been migrated to the Element object model yet.")
        };
    }

    public static bool IsLegacyText(LegacyDetectedObject source)
    {
        return source.IsTextLike ||
            source.LegacyType.Contains("text", StringComparison.OrdinalIgnoreCase) ||
            source.DisplayName.Contains("text", StringComparison.OrdinalIgnoreCase);
    }

    private static ScadaElementStyle DefaultStyle(ScadaElementKind kind)
    {
        return kind == ScadaElementKind.Text ? ScadaElementStyle.DefaultText : ScadaElementStyle.DefaultInput;
    }

    private static double DefaultWidth(ScadaElementKind kind)
    {
        return kind == ScadaElementKind.Text ? 180 : 180;
    }

    private static double DefaultHeight(ScadaElementKind kind)
    {
        return kind == ScadaElementKind.Text ? 28 : 32;
    }

    private static NumericInput CreateNumericInput(LegacyDetectedObject source, ElementPlusConversionOptions options, bool isReadOnly)
    {
        var text = string.IsNullOrWhiteSpace(source.Text) ? source.DisplayName : source.Text.Trim();
        var kind = ScadaElementKind.InputNumeric;
        var width = source.Bounds.Width > 0 ? source.Bounds.Width : DefaultWidth(kind);
        var height = source.Bounds.Height > 0 ? source.Bounds.Height : DefaultHeight(kind);
        var fontSize = source.Style.FontSize > 0 ? source.Style.FontSize : DefaultStyle(kind).FontSize;
        var style = DefaultStyle(kind) with
        {
            FontFamily = string.IsNullOrWhiteSpace(source.Style.FontFamily) ? DefaultStyle(kind).FontFamily : source.Style.FontFamily,
            FontSize = Math.Max(6, Math.Round(fontSize, 2)),
            Foreground = string.IsNullOrWhiteSpace(source.Style.Foreground) ? DefaultStyle(kind).Foreground : source.Style.Foreground,
            Background = NormalizeTransparentCssColor(source.Style.Background)
        };

        return new NumericInput(
            options.ElementId,
            options.DisplayName,
            new SceneBounds(
                Math.Max(0, Math.Round(source.Bounds.X)),
                Math.Max(0, Math.Round(source.Bounds.Y)),
                Math.Max(4, Math.Round(width)),
                Math.Max(4, Math.Round(height))),
            style,
            isReadOnly,
            ParseNumericValue(text),
            displayFormat: text,
            legacySource: new LegacySourceTrace(options.SourceSystem, options.SourceDocumentId, source.RuntimeId, source.DisplayName, options.SourcePath));
    }

    private static ScadaElementData CreateData(ScadaElementKind kind, string text)
    {
        return kind switch
        {
            ScadaElementKind.Text => new ScadaElementData(text, null, null, null, null, null, null, null, null, false),
            ScadaElementKind.InputText => new ScadaElementData(text, text, null, null, null, null, null, null, null, false),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static double? ParseNumericValue(string text)
    {
        return double.TryParse(text, out var value) ? value : null;
    }

    private static string NormalizeTransparentCssColor(string value)
    {
        return string.IsNullOrWhiteSpace(value) ||
            value.Equals("rgba(0, 0, 0, 0)", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("transparent", StringComparison.OrdinalIgnoreCase)
                ? "Transparent"
                : value;
    }
}
