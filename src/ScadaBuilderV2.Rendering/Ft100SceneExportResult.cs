namespace ScadaBuilderV2.Rendering;

public sealed record Ft100SceneExportResult(
    string ExportDirectory,
    string HtmlPath,
    string CssPath,
    string ImagesDirectory,
    int CopiedImageCount);
