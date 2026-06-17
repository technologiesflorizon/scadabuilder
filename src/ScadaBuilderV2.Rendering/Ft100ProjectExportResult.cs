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
