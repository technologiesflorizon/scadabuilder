using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Rendering;

/// <summary>
/// Input page compiled into a project-level FT100 export. Runtime paths and ids are derived
/// from the scene's human page code at the export boundary.
/// </summary>
public sealed record Ft100ProjectPageExportInput(
    ScadaScene Scene,
    string SourceHtmlPath);

/// <summary>
/// Describes a generated FT100 project package directory.
/// </summary>
public sealed record Ft100ProjectExportResult(
    string ExportDirectory,
    string ManifestPath,
    IReadOnlyList<Ft100SceneExportResult> PageResults,
    int CopiedImageCount,
    IReadOnlyList<string> Warnings)
{
    /// <summary>
    /// Backward-compatible constructor for callers that don't provide project warnings.
    /// </summary>
    public Ft100ProjectExportResult(
        string exportDirectory,
        string manifestPath,
        IReadOnlyList<Ft100SceneExportResult> pageResults,
        int copiedImageCount)
        : this(
            exportDirectory,
            manifestPath,
            pageResults,
            copiedImageCount,
            pageResults.SelectMany(page => page.Warnings).Distinct(StringComparer.Ordinal).ToArray())
    { }
}

/// <summary>
/// Describes a SCADA Builder V2 FT100 package exported as a FT100-compatible .sb2 archive.
/// </summary>
/// <param name="ArchivePath">Absolute path to the generated .sb2 archive.</param>
/// <param name="PackageRootName">Root directory name stored at the archive top level.</param>
/// <param name="ManifestRelativePath">Manifest path relative to the archive root.</param>
/// <param name="PageCount">Number of compiled pages included in the archive.</param>
/// <param name="CopiedImageCount">Number of source image assets copied into the package.</param>
/// <param name="Validation">Validation result produced before archive creation.</param>
/// <remarks>
/// Decisions: DEC-0003, DEC-0007, DEC-0026, DEC-0027.
/// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md.
/// </remarks>
public sealed record Ft100ProjectArchiveExportResult(
    string ArchivePath,
    string PackageRootName,
    string ManifestRelativePath,
    int PageCount,
    int CopiedImageCount,
    Ft100PackageValidationResult Validation);
