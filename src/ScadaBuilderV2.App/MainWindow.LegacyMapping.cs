using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Conversion;
using ScadaBuilderV2.Application.Selection;
using ScadaBuilderV2.Domain.Editor;
using ScadaBuilderV2.Domain.Elements;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App;

// Stateless legacy<->Element+ projection helpers (list items, detected objects,
// conversion target labels/command descriptors, id/color sanitizers). Extracted
// from MainWindow.xaml.cs as a behavior-preserving split; no UI/field dependencies.
public partial class MainWindow
{
    private static IReadOnlyList<ElementPlusConversionTarget> GetPlausibleConversionTargets(LegacyElementListItem element)
    {
        return ElementPlusLegacyConverter.GetPlausibleTargets(ToLegacyDetectedObject(element));
    }

    private static IReadOnlyList<ElementPlusConversionTarget> GetPlausibleConversionTargets(LegacyViewerElementMessage element)
    {
        return ElementPlusLegacyConverter.GetPlausibleTargets(ToLegacyDetectedObject(ToLegacyElementListItem(element)));
    }

    private static LegacyDetectedObject ToLegacyDetectedObject(LegacyElementListItem legacy)
    {
        return new LegacyDetectedObject(
            legacy.Id,
            legacy.DisplayName,
            legacy.ElementType,
            legacy.Text,
            legacy.IsTextLike,
            new SceneBounds(legacy.X, legacy.Y, legacy.Width, legacy.Height),
            new LegacyObjectStyle(legacy.FontFamily, legacy.FontSize, legacy.Foreground, legacy.Background));
    }

    private static LegacyElementListItem ToLegacyElementListItem(LegacyDetectedObject legacy)
    {
        return new LegacyElementListItem(
            legacy.RuntimeId,
            legacy.DisplayName,
            legacy.LegacyType,
            legacy.Bounds.X,
            legacy.Bounds.Y,
            legacy.Bounds.Width,
            legacy.Bounds.Height,
            legacy.Text,
            legacy.IsTextLike,
            legacy.Style.FontFamily,
            legacy.Style.FontSize,
            legacy.Style.Foreground,
            legacy.Style.Background,
            "",
            "");
    }

    private static LegacyElementListItem ToLegacyElementListItem(LegacyViewerElementMessage item)
    {
        var id = item.Id.Trim();
        return new LegacyElementListItem(
            id,
            string.IsNullOrWhiteSpace(item.Name) ? id : item.Name.Trim(),
            string.IsNullOrWhiteSpace(item.ElementType) ? "Legacy" : item.ElementType.Trim(),
            item.X,
            item.Y,
            item.Width,
            item.Height,
            item.Text ?? "",
            item.IsTextLike,
            item.FontFamily ?? "",
            item.FontSize,
            item.Foreground ?? "",
            item.Background ?? "",
            item.LegacyMarkup ?? "",
            item.RawMetadataJson ?? "",
            item.RenderOrder);
    }

    private static LegacyElementListItem ToLegacyElementListItem(ScadaElement element)
    {
        var payload = element.LegacyPayload;
        var sourceId = element.LegacySource?.SourceElementId ?? element.Id;
        return new LegacyElementListItem(
            sourceId,
            string.IsNullOrWhiteSpace(element.LegacySource?.SourceElementName)
                ? element.DisplayName
                : element.LegacySource.SourceElementName,
            payload?.LegacyType ?? "Legacy",
            element.Bounds.X,
            element.Bounds.Y,
            element.Bounds.Width,
            element.Bounds.Height,
            payload?.Text ?? element.Data?.Text ?? "",
            payload?.IsTextLike ?? false,
            payload?.FontFamily ?? element.Style?.FontFamily ?? "",
            payload?.FontSize ?? element.Style?.FontSize ?? 0,
            payload?.Foreground ?? element.Style?.Foreground ?? "",
            payload?.Background ?? element.Style?.Background ?? "",
            payload?.LegacyMarkup ?? "",
            payload?.RawMetadataJson ?? "");
    }

    private static string GetConversionTargetLabel(ElementPlusConversionTarget target)
    {
        return target switch
        {
            ElementPlusConversionTarget.Text => "Texte",
            ElementPlusConversionTarget.TextInput => "Champ d'entree texte",
            ElementPlusConversionTarget.NumericReadOnly => "Affichage numerique",
            ElementPlusConversionTarget.NumericEditable => "Champ numerique editable",
            ElementPlusConversionTarget.Button => "Bouton",
            _ => target.ToString()
        };
    }

    private static EditorCommandDescriptor CreateConversionCommandDescriptor(ElementPlusConversionTarget target)
    {
        return new EditorCommandDescriptor(
            $"source.convert-to-element-plus.{GetConversionTargetCommandSuffix(target)}",
            GetConversionTargetLabel(target),
            "conversion");
    }

    private static string GetConversionTargetCommandSuffix(ElementPlusConversionTarget target)
    {
        return target switch
        {
            ElementPlusConversionTarget.Text => "text",
            ElementPlusConversionTarget.TextInput => "input-text",
            ElementPlusConversionTarget.NumericReadOnly => "numeric-readonly",
            ElementPlusConversionTarget.NumericEditable => "numeric-editable",
            ElementPlusConversionTarget.Button => "button",
            _ => target.ToString().ToLowerInvariant()
        };
    }

    private static string SanitizeElementIdPart(string value)
    {
        var chars = value
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '_')
            .ToArray();
        var sanitized = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "legacy" : sanitized;
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
