namespace ScadaBuilderV2.Rendering;

public sealed record Ft100SceneExportResult(
    string ExportDirectory,
    string HtmlPath,
    string CssPath,
    string ImagesDirectory,
    int CopiedImageCount,
    IReadOnlyList<string> Warnings)
{
    /// <summary>
    /// Backward-compatible constructor for callers that don't provide warnings.
    /// </summary>
    public Ft100SceneExportResult(
        string exportDirectory, string htmlPath, string cssPath,
        string imagesDirectory, int copiedImageCount)
        : this(exportDirectory, htmlPath, cssPath, imagesDirectory,
               copiedImageCount, Array.Empty<string>())
    { }
}
