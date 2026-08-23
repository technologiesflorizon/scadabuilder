using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// Projects one quick-window definition onto the shared editor canvas without turning it into a page.
/// </summary>
/// <remarks>
/// The projection is editor-only: its scene reference is never added to <see cref="ScadaProject.Scenes"/>,
/// is excluded from build, carries the native origin so the preview is materialized from the model, and
/// keeps the definition key as routing key. It exists so pages and quick windows share one canvas by
/// composition, as required by the bounded `VisualContent` contract, and never as an export surface.
///
/// Decisions: DEC-0050, FR-001, FR-019, FR-UI-13, FR-UI-14.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §§8.2, 9.1.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowShellContractTests.cs.
/// </remarks>
/// <param name="Reference">Synthetic, editor-only page reference used by the preview materializer.</param>
/// <param name="Scene">Scene shell carrying the definition visual content.</param>
public sealed record QuickWindowPreviewProjection(ScadaSceneReference Reference, ScadaScene Scene)
{
    /// <summary>Preview sub-directory hosting every quick-window projection.</summary>
    public const string PreviewDirectoryName = "quick-windows";

    /// <summary>Prefix applied to the projected page code so it can never collide with a real page.</summary>
    public const string ProjectedCodePrefix = "qw-";

    /// <summary>Gets the projected, collision-free code used by the preview document.</summary>
    public string ProjectedCode => Reference.EffectivePageCode;

    /// <summary>Projects one definition into an editor-only scene and reference.</summary>
    public static QuickWindowPreviewProjection Create(QuickWindowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var code = $"{ProjectedCodePrefix}{definition.EffectiveCode}";
        var content = definition.EffectiveContent;
        var scene = content.ToScene(code, definition.EffectiveTitle, definition.DefinitionKey, code);
        var reference = new ScadaSceneReference(
            code,
            definition.EffectiveTitle,
            RelativePath: string.Empty,
            Type: ScadaPageType.Default,
            CanvasSize: content.EffectiveCanvasSize,
            Background: content.EffectiveBackground,
            IncludeInBuild: false,
            PageKey: definition.DefinitionKey,
            PageCode: code,
            Origin: PageOrigin.Native);
        return new QuickWindowPreviewProjection(reference, scene);
    }
}
