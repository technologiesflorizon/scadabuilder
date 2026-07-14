using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Rendering;

/// <summary>Resolved imported HTML projection used by preview and export.</summary>
public sealed record PageSourceProjection(
    string RootPath,
    string RelativeHtmlSource,
    string Kind)
{
    /// <summary>Gets the absolute imported HTML path after confinement to <see cref="RootPath"/>.</summary>
    public string GetSourcePath()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(RootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(RelativeHtmlSource);
        var root = Path.GetFullPath(RootPath);
        var source = Path.GetFullPath(Path.Combine(root, RelativeHtmlSource));
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!source.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Imported page projection escapes its source root.");
        }

        return source;
    }
}

/// <summary>Modern page, scene and optional imported projection consumed by document pipelines.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/NativePageDocumentTests.cs.
/// </remarks>
public sealed record PageDocumentInput(
    ScadaSceneReference Page,
    ScadaScene Scene,
    PageSourceProjection? ImportedProjection = null)
{
    /// <summary>Gets whether the page must be rendered without an imported source layer.</summary>
    public bool IsNative => Page.EffectiveOrigin == PageOrigin.Native && ImportedProjection is null;
}
