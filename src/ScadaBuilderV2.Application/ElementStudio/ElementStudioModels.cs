using ScadaBuilderV2.Domain.Editor;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.ElementStudio;

public sealed record ElementStudioPackageMetadata(
    string Schema,
    int SchemaVersion,
    string Format,
    string CreatedByVersion)
{
    public const string CurrentSchema = "scada-builder-v2.element-studio.import";
    public const int CurrentSchemaVersion = 1;
    public const string CurrentFormat = "json.ft1";

    public static ElementStudioPackageMetadata Current(string createdByVersion)
    {
        return new ElementStudioPackageMetadata(
            CurrentSchema,
            CurrentSchemaVersion,
            CurrentFormat,
            createdByVersion);
    }
}

public sealed record ElementStudioImportPackage(
    ElementStudioPackageMetadata Metadata,
    string PackageId,
    string SourceProjectId,
    string SourceSceneId,
    string SourcePagePath,
    string? TargetLibraryPath,
    SceneBounds Bounds,
    IReadOnlyList<ElementStudioLegacyItem> Items);

public sealed record ElementStudioLegacyItem(
    string SourceElementId,
    string SourceName,
    string LegacyType,
    SceneBounds BoundsAbsolute,
    SceneBounds BoundsRelativeToPackage,
    string? Geometry,
    string? LegacyMarkup,
    string? Text,
    ElementStudioStyleSnapshot Style,
    int ZIndex,
    string? RawMetadataJson);

public sealed record ElementStudioStyleSnapshot(
    string FontFamily,
    double FontSize,
    string Foreground,
    string Background,
    string BorderColor,
    double BorderWidth,
    string BorderStyle,
    double Opacity)
{
    public static ElementStudioStyleSnapshot Default { get; } = new(
        "Segoe UI",
        12,
        "#000000",
        "Transparent",
        "Transparent",
        0,
        "None",
        1);

    public static ElementStudioStyleSnapshot FromLegacyStyle(LegacyObjectStyle style)
    {
        return Default with
        {
            FontFamily = string.IsNullOrWhiteSpace(style.FontFamily) ? Default.FontFamily : style.FontFamily,
            FontSize = style.FontSize > 0 ? style.FontSize : Default.FontSize,
            Foreground = string.IsNullOrWhiteSpace(style.Foreground) ? Default.Foreground : style.Foreground,
            Background = string.IsNullOrWhiteSpace(style.Background) ? Default.Background : style.Background
        };
    }
}
