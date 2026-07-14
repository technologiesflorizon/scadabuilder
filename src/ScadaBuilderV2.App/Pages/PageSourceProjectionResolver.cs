using System.IO;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.App.Pages;

/// <summary>Resolves imported page HTML exclusively from persisted import provenance.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/07_legacy_migration/LEGACY_SOURCE_POLICY_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/NativePageDocumentTests.cs.
/// </remarks>
public sealed class PageSourceProjectionResolver
{
    /// <summary>Returns no projection for native pages and a confined HTML projection for imported pages.</summary>
    public PageSourceProjection? Resolve(ScadaSceneReference page, string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        if (page.EffectiveOrigin == PageOrigin.Native)
        {
            return null;
        }

        var sourcePath = page.ImportProvenance?.SourcePath;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new InvalidOperationException($"Imported page '{page.EffectivePageCode}' has no persisted source projection path.");
        }

        var fullRepositoryRoot = Path.GetFullPath(repositoryRoot);
        var fullSourcePath = Path.IsPathRooted(sourcePath)
            ? Path.GetFullPath(sourcePath)
            : Path.GetFullPath(Path.Combine(fullRepositoryRoot, sourcePath));
        var repositoryPrefix = fullRepositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullSourcePath.StartsWith(repositoryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Imported projection for '{page.EffectivePageCode}' escapes the repository root.");
        }

        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException($"Imported projection for '{page.EffectivePageCode}' was not found.", fullSourcePath);
        }

        var sourceRoot = Path.GetDirectoryName(fullSourcePath)
            ?? throw new InvalidOperationException("Imported projection path has no parent directory.");
        return new PageSourceProjection(
            sourceRoot,
            Path.GetFileName(fullSourcePath),
            page.ImportProvenance?.SourceSystem ?? "Imported");
    }
}
