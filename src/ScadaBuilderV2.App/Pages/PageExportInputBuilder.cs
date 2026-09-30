using ScadaBuilderV2.App.Workspace;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ModernProjects;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.App.Pages;

/// <summary>Builds coherent FT100 export inputs from modern page identity and optional imported provenance.</summary>
public sealed class PageExportInputBuilder(
    IPageWorkspaceReader workspaceReader,
    PageSourceProjectionResolver projectionResolver)
{
    /// <summary>
    /// Builds one export input per compiled page. When <paramref name="loweredScenes"/> carries a page's
    /// key, its scene is used as is: it is the lowered projection (parameter-sheet lowering rewrites open
    /// commands with their invocation key), and the fresh on-disk read below would return the pre-lowering
    /// scene instead. Every page missing from <paramref name="loweredScenes"/> — a caller that predates
    /// this parameter, or a page lowering left untouched — keeps reading its live workspace snapshot.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0050, DEC-0052.
    /// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §12.4.
    /// Tests: tests/ScadaBuilderV2.Tests/PageWorkspaceExtractionContractTests.cs.
    /// </remarks>
    public async Task<IReadOnlyList<Ft100ProjectPageExportInput>> BuildAsync(
        string repositoryRoot,
        ScadaProject project,
        IEnumerable<SceneWorkspaceTab> openTabs,
        SceneWorkspaceTab? activeTab,
        ScadaScene? activeScene,
        string? importedSourceBaseRoot = null,
        IReadOnlyDictionary<Guid, ScadaScene>? loweredScenes = null,
        CancellationToken cancellationToken = default)
    {
        var overrides = openTabs.ToDictionary(
            tab => tab.PageKey,
            tab => ReferenceEquals(tab, activeTab) && activeScene is not null ? activeScene : tab.Scene);
        var snapshot = await workspaceReader.ReadWorkspaceSnapshotAsync(
            repositoryRoot,
            new PageWorkspaceReadContext(overrides, ProjectOverride: project),
            cancellationToken);
        var inputs = new List<Ft100ProjectPageExportInput>();
        foreach (var page in project.Scenes.Where(page => page.IncludeInBuild).OrderBy(page => page.EffectivePageCode, StringComparer.Ordinal))
        {
            var source = projectionResolver.Resolve(page, repositoryRoot, importedSourceBaseRoot);
            var scene = loweredScenes is not null && loweredScenes.TryGetValue(page.PageKey, out var lowered)
                ? lowered
                : snapshot.Scenes[page.PageKey];
            inputs.Add(new Ft100ProjectPageExportInput(
                Synchronize(scene, page),
                source?.GetSourcePath(),
                page));
        }
        return inputs;
    }

    private static ScadaScene Synchronize(ScadaScene scene, ScadaSceneReference page) =>
        scene.WithPageType(page.Type)
            .WithIncludeInBuild(page.IncludeInBuild)
            .WithCanvasSize(page.EffectiveCanvasSize)
            .WithBackground(page.EffectiveBackground)
            .WithPageComposition(page.HeaderPageId, page.FooterPageId) with
        {
            PageKey = page.PageKey,
            PageCode = page.EffectivePageCode,
            Origin = page.EffectiveOrigin,
            ImportProvenance = page.ImportProvenance,
            HeaderPageKey = page.HeaderPageKey,
            FooterPageKey = page.FooterPageKey
        };
}
