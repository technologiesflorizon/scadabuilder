using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Rendering;

/// <summary>Generated native page document ready to be materialized for editor preview.</summary>
public sealed record NativePageDocument(
    string PageCode,
    string Html,
    string Css,
    IReadOnlyList<string> Warnings);

/// <summary>Builds source-free page documents with the same Element+ geometry as .sb2 export.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/NativePageDocumentTests.cs.
/// </remarks>
public static class NativePageDocumentFactory
{
    /// <summary>Creates a native HTML/CSS document without editor or exported runtime artifacts.</summary>
    public static NativePageDocument Create(PageDocumentInput input, ScadaTagCatalog? tagCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.IsNative)
        {
            throw new InvalidOperationException("Native page generation requires a page without imported projection.");
        }

        var pageCode = input.Page.EffectivePageCode;
        var scene = input.Scene with
        {
            Id = pageCode,
            PageCode = pageCode,
            Title = input.Page.Title,
            PageType = input.Page.Type,
            CanvasSize = input.Page.EffectiveCanvasSize,
            Background = input.Page.EffectiveBackground,
            BackgroundColor = input.Page.EffectiveBackground.Color,
            IncludeInBuild = input.Page.IncludeInBuild
        };
        var warnings = new List<string>();
        var cssFileName = $"{pageCode}.css";
        return new NativePageDocument(
            pageCode,
            Ft100SceneExporter.BuildDocumentHtml(
                scene,
                $"css/{cssFileName}",
                sourceContent: string.Empty,
                runtimeScriptSource: null,
                tagCatalog,
                warnings),
            Ft100SceneExporter.BuildDocumentCss(scene),
            warnings);
    }
}
