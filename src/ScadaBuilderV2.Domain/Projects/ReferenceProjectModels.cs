using System.Text.Json.Serialization;

namespace ScadaBuilderV2.Domain.Legacy;

public sealed class ReferenceProject
{
    [JsonConstructor]
    public ReferenceProject(
        string name,
        string version,
        ReferenceProjectTarget? target,
        string? theme,
        IReadOnlyList<string>? pagePaths,
        ReferenceProjectAssets? assets,
        ReferenceProjectImports? imports,
        ReferenceProjectBuild? build)
    {
        Name = name;
        Version = version;
        Target = target;
        Theme = theme;
        PagePaths = ReferenceModelCollections.CopyReadOnly(pagePaths);
        PageIds = ReferenceModelCollections.CopyReadOnly(PagePaths.Select(ReferencePagePath.GetPageId));
        Assets = assets ?? ReferenceProjectAssets.Empty;
        Imports = imports;
        Build = build;
    }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("version")]
    public string Version { get; }

    [JsonPropertyName("target")]
    public ReferenceProjectTarget? Target { get; }

    [JsonPropertyName("theme")]
    public string? Theme { get; }

    [JsonPropertyName("pages")]
    public IReadOnlyList<string> PagePaths { get; }

    [JsonIgnore]
    public IReadOnlyList<string> PageIds { get; }

    [JsonPropertyName("assets")]
    public ReferenceProjectAssets Assets { get; }

    [JsonPropertyName("imports")]
    public ReferenceProjectImports? Imports { get; }

    [JsonPropertyName("build")]
    public ReferenceProjectBuild? Build { get; }

}

public sealed class ReferenceProjectTarget
{
    [JsonConstructor]
    public ReferenceProjectTarget(string platform, string basePath, bool offline)
    {
        Platform = platform;
        BasePath = basePath;
        Offline = offline;
    }

    [JsonPropertyName("platform")]
    public string Platform { get; }

    [JsonPropertyName("basePath")]
    public string BasePath { get; }

    [JsonPropertyName("offline")]
    public bool Offline { get; }
}

public sealed class ReferenceProjectAssets
{
    public static ReferenceProjectAssets Empty { get; } = new(Array.Empty<string>());

    [JsonConstructor]
    public ReferenceProjectAssets(IReadOnlyList<string>? paths)
    {
        Paths = ReferenceModelCollections.CopyReadOnly(paths);
    }

    [JsonPropertyName("paths")]
    public IReadOnlyList<string> Paths { get; }
}

public sealed class ReferenceProjectImports
{
    [JsonConstructor]
    public ReferenceProjectImports(
        string? updatedWebRoot,
        string? updatedPagesRoot,
        string? updatedPagesPattern,
        string? sourceReference,
        DateOnly? importedAt,
        int updatedImportedCount)
    {
        UpdatedWebRoot = updatedWebRoot;
        UpdatedPagesRoot = updatedPagesRoot;
        UpdatedPagesPattern = updatedPagesPattern;
        SourceReference = sourceReference;
        ImportedAt = importedAt;
        UpdatedImportedCount = updatedImportedCount;
    }

    [JsonPropertyName("updated_web_root")]
    public string? UpdatedWebRoot { get; }

    [JsonPropertyName("updated_pages_root")]
    public string? UpdatedPagesRoot { get; }

    [JsonPropertyName("updated_pages_pattern")]
    public string? UpdatedPagesPattern { get; }

    [JsonPropertyName("source_reference")]
    public string? SourceReference { get; }

    [JsonPropertyName("imported_at")]
    public DateOnly? ImportedAt { get; }

    [JsonPropertyName("updated_imported_count")]
    public int UpdatedImportedCount { get; }
}

public sealed class ReferenceProjectBuild
{
    [JsonConstructor]
    public ReferenceProjectBuild(bool minify, bool sourcemaps)
    {
        Minify = minify;
        Sourcemaps = sourcemaps;
    }

    [JsonPropertyName("minify")]
    public bool Minify { get; }

    [JsonPropertyName("sourcemaps")]
    public bool Sourcemaps { get; }
}

public sealed class ReferencePage
{
    [JsonConstructor]
    public ReferencePage(
        string id,
        string title,
        ReferenceCanvasSize size,
        string background,
        IReadOnlyList<ReferencePageLayer>? layers,
        ReferencePageLayout? layout,
        ReferencePageLegacy? legacy)
    {
        Id = id;
        Title = title;
        Size = size;
        Background = background;
        Layers = ReferenceModelCollections.CopyReadOnly(layers);
        Layout = layout;
        Legacy = legacy;
    }

    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("title")]
    public string Title { get; }

    [JsonPropertyName("size")]
    public ReferenceCanvasSize Size { get; }

    [JsonPropertyName("background")]
    public string Background { get; }

    [JsonPropertyName("layers")]
    public IReadOnlyList<ReferencePageLayer> Layers { get; }

    [JsonPropertyName("layout")]
    public ReferencePageLayout? Layout { get; }

    [JsonPropertyName("legacy")]
    public ReferencePageLegacy? Legacy { get; }
}

public sealed class ReferenceCanvasSize
{
    [JsonConstructor]
    public ReferenceCanvasSize(int width, int height)
    {
        Width = width;
        Height = height;
    }

    [JsonPropertyName("width")]
    public int Width { get; }

    [JsonPropertyName("height")]
    public int Height { get; }
}

public sealed class ReferencePageLayer
{
    [JsonConstructor]
    public ReferencePageLayer(
        string id,
        string type,
        int x,
        int y,
        int width,
        int height,
        string? src,
        string? sandbox,
        string? scrolling)
    {
        Id = id;
        Type = type;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Src = src;
        Sandbox = sandbox;
        Scrolling = scrolling;
    }

    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("type")]
    public string Type { get; }

    [JsonPropertyName("x")]
    public int X { get; }

    [JsonPropertyName("y")]
    public int Y { get; }

    [JsonPropertyName("width")]
    public int Width { get; }

    [JsonPropertyName("height")]
    public int Height { get; }

    [JsonPropertyName("src")]
    public string? Src { get; }

    [JsonPropertyName("sandbox")]
    public string? Sandbox { get; }

    [JsonPropertyName("scrolling")]
    public string? Scrolling { get; }
}

public sealed class ReferencePageLayout
{
    [JsonConstructor]
    public ReferencePageLayout(string mode)
    {
        Mode = mode;
    }

    [JsonPropertyName("mode")]
    public string Mode { get; }
}

public sealed class ReferencePageLegacy
{
    [JsonConstructor]
    public ReferencePageLegacy(string? sourceHtml, ReferenceLegacyInventory? inventory)
    {
        SourceHtml = sourceHtml;
        Inventory = inventory;
    }

    [JsonPropertyName("source_html")]
    public string? SourceHtml { get; }

    [JsonPropertyName("inventory")]
    public ReferenceLegacyInventory? Inventory { get; }
}

public sealed class ReferenceLegacyInventory
{
    [JsonConstructor]
    public ReferenceLegacyInventory(LegacyInventorySummary? summary)
    {
        Summary = summary;
    }

    [JsonPropertyName("summary")]
    public LegacyInventorySummary? Summary { get; }
}

public sealed class LegacyInventorySummary
{
    [JsonConstructor]
    public LegacyInventorySummary(
        int shapeLayers,
        int scripts,
        int layersTotal,
        int typeImage,
        int typeText,
        int svgShapesTotal)
    {
        ShapeLayers = shapeLayers;
        Scripts = scripts;
        LayersTotal = layersTotal;
        TypeImage = typeImage;
        TypeText = typeText;
        SvgShapesTotal = svgShapesTotal;
    }

    [JsonPropertyName("shape_layers")]
    public int ShapeLayers { get; }

    [JsonPropertyName("scripts")]
    public int Scripts { get; }

    [JsonPropertyName("layers_total")]
    public int LayersTotal { get; }

    [JsonPropertyName("type_Image")]
    public int TypeImage { get; }

    [JsonPropertyName("type_Text")]
    public int TypeText { get; }

    [JsonPropertyName("svg_shapes_total")]
    public int SvgShapesTotal { get; }
}

public static class ReferencePagePath
{
    public static string GetPageId(string pagePath)
    {
        var normalized = pagePath.Replace('\\', '/');
        var fileName = normalized[(normalized.LastIndexOf('/') + 1)..];
        var extensionIndex = fileName.LastIndexOf('.');

        return extensionIndex < 0 ? fileName : fileName[..extensionIndex];
    }
}

internal static class ReferenceModelCollections
{
    public static IReadOnlyList<T> CopyReadOnly<T>(IEnumerable<T>? values)
    {
        return Array.AsReadOnly(values?.ToArray() ?? Array.Empty<T>());
    }
}
