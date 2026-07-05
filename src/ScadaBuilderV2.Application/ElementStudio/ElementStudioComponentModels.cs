using System.Text.Json.Serialization;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.ElementStudio;

public enum ElementStudioComponentVisualKind
{
    Svg,
    Image,
    Html,
    Composite,
    Typed
}

public enum ElementStudioComponentPartKind
{
    Line,
    Polyline,
    Rectangle,
    Polygon,
    Path,
    Text,
    Image,
    Group,
    Html,
    Custom
}

/// <summary>
/// Whether a `.sep` component's current artwork is the untouched legacy import
/// or has been redrawn under the interactive icon-modernization loop.
/// </summary>
/// <remarks>
/// Decisions: DEC-0034. Contracts: STUDIO_ELEMENT_PLUS_SEP_CONTRACT_V2.md.
/// Tests: StudioElementPlusContractTests.cs.
/// </remarks>
public enum ElementStudioComponentProvenance
{
    /// <summary>Untouched legacy import - original artwork, not redrawn.</summary>
    Legacy,

    /// <summary>Artwork redrawn under the interactive icon-modernization loop (docs/07_legacy_migration/MODERNIZATION_WORKFLOW_V2.md).</summary>
    AiModernized
}

public sealed record ElementStudioComponentMetadata(
    string Schema,
    int SchemaVersion,
    string Format,
    string CreatedByVersion)
{
    public const string CurrentSchema = "scada-builder-v2.element-studio.component";
    public const int CurrentSchemaVersion = 1;
    public const string CurrentFormat = "json.sep";

    public static ElementStudioComponentMetadata Current(string createdByVersion)
    {
        return new ElementStudioComponentMetadata(
            CurrentSchema,
            CurrentSchemaVersion,
            CurrentFormat,
            createdByVersion);
    }
}

public sealed record ElementStudioComponentPackage(
    ElementStudioComponentMetadata Metadata,
    ElementStudioComponent Component);

public sealed record ElementStudioComponent(
    string ComponentId,
    string Name,
    SceneBounds Bounds,
    ElementStudioComponentVisual Visual,
    IReadOnlyList<ElementStudioComponentPart> Parts,
    IReadOnlyList<ElementStudioEmbeddedAsset> Assets,
    IReadOnlyList<ElementStudioComponentBinding> Bindings,
    IReadOnlyList<ElementStudioComponentEvent> Events,
    ElementStudioComponentSourceTrace? SourceTrace,
    string? Category = null,
    string? Description = null,
    IReadOnlyList<string>? Tags = null,
    ElementStudioComponentProvenance? Provenance = null)
{
    [JsonIgnore]
    public IReadOnlyList<string> TagList => Tags ?? Array.Empty<string>();
}

public sealed record ElementStudioComponentVisual(
    ElementStudioComponentVisualKind Kind,
    string? SvgMarkup = null,
    string? ImageAssetId = null,
    string? HtmlMarkup = null,
    string? CssCode = null,
    string? JsCode = null,
    string? TypedElementKind = null);

public sealed record ElementStudioComponentPart(
    string PartId,
    string Name,
    ElementStudioComponentPartKind Kind,
    SceneBounds Bounds,
    ElementStudioStyleSnapshot Style,
    string? Geometry = null,
    string? Text = null,
    string? ImageAssetId = null,
    string? HtmlMarkup = null,
    string? CssCode = null,
    IReadOnlyList<ElementStudioComponentPart>? Children = null,
    ElementStudioComponentSourceTrace? SourceTrace = null)
{
    [JsonIgnore]
    public IReadOnlyList<ElementStudioComponentPart> ChildParts => Children ?? Array.Empty<ElementStudioComponentPart>();
}

public sealed record ElementStudioEmbeddedAsset(
    string AssetId,
    string FileName,
    string MediaType,
    string Base64Data,
    long SizeBytes,
    string? Sha256 = null);

public sealed record ElementStudioComponentBinding(
    string BindingId,
    string Target,
    string BindingType,
    string? TagName,
    string? Expression);

public sealed record ElementStudioComponentEvent(
    string EventId,
    string Target,
    string EventName,
    string? Action,
    string? Script);

public sealed record ElementStudioComponentSourceTrace(
    string? SourceProjectId,
    string? SourceSceneId,
    string? SourcePagePath,
    IReadOnlyList<string> SourceElementIds,
    string? Notes = null);
