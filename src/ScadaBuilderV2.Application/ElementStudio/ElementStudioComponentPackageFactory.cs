using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.ElementStudio;

public static class ElementStudioComponentPackageFactory
{
    public static ElementStudioComponentPackage Create(
        string componentId,
        string name,
        SceneBounds bounds,
        ElementStudioComponentVisual visual,
        IEnumerable<ElementStudioComponentPart>? parts,
        IEnumerable<ElementStudioEmbeddedAsset>? assets,
        ElementStudioComponentMetadata metadata,
        ElementStudioComponentSourceTrace? sourceTrace = null,
        string? category = null,
        string? description = null,
        IEnumerable<string>? tags = null,
        IEnumerable<ElementStudioComponentBinding>? bindings = null,
        IEnumerable<ElementStudioComponentEvent>? events = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(visual);
        ArgumentNullException.ThrowIfNull(metadata);

        var component = new ElementStudioComponent(
            componentId,
            name.Trim(),
            bounds,
            visual,
            (parts ?? Array.Empty<ElementStudioComponentPart>()).ToArray(),
            (assets ?? Array.Empty<ElementStudioEmbeddedAsset>()).ToArray(),
            (bindings ?? Array.Empty<ElementStudioComponentBinding>()).ToArray(),
            (events ?? Array.Empty<ElementStudioComponentEvent>()).ToArray(),
            sourceTrace,
            category,
            description,
            tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

        return new ElementStudioComponentPackage(metadata, component);
    }

    public static ElementStudioComponentPackage CreateSvg(
        string componentId,
        string name,
        SceneBounds bounds,
        string svgMarkup,
        ElementStudioComponentMetadata metadata,
        IEnumerable<ElementStudioComponentPart>? parts = null,
        ElementStudioComponentSourceTrace? sourceTrace = null,
        string? category = null,
        string? description = null,
        IEnumerable<string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(svgMarkup);

        return Create(
            componentId,
            name,
            bounds,
            new ElementStudioComponentVisual(ElementStudioComponentVisualKind.Svg, SvgMarkup: svgMarkup),
            parts,
            Array.Empty<ElementStudioEmbeddedAsset>(),
            metadata,
            sourceTrace,
            category,
            description,
            tags);
    }

    public static ElementStudioComponentPackage CreateImage(
        string componentId,
        string name,
        SceneBounds bounds,
        ElementStudioEmbeddedAsset imageAsset,
        ElementStudioComponentMetadata metadata,
        ElementStudioComponentSourceTrace? sourceTrace = null,
        string? category = null,
        string? description = null,
        IEnumerable<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(imageAsset);

        return Create(
            componentId,
            name,
            bounds,
            new ElementStudioComponentVisual(ElementStudioComponentVisualKind.Image, ImageAssetId: imageAsset.AssetId),
            Array.Empty<ElementStudioComponentPart>(),
            new[] { imageAsset },
            metadata,
            sourceTrace,
            category,
            description,
            tags);
    }
}
