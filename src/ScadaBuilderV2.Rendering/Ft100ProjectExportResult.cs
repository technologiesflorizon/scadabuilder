using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Rendering;

public sealed record Ft100ProjectPageExportInput(
    ScadaScene Scene,
    string SourceHtmlPath);

public sealed record Ft100ProjectExportResult(
    string ExportDirectory,
    string ManifestPath,
    IReadOnlyList<Ft100SceneExportResult> PageResults,
    int CopiedImageCount);
