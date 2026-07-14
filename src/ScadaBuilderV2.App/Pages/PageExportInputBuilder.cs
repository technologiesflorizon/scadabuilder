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
    public async Task<IReadOnlyList<Ft100ProjectPageExportInput>> BuildAsync(
        string repositoryRoot,
        ScadaProject project,
        IEnumerable<SceneWorkspaceTab> openTabs,
        SceneWorkspaceTab? activeTab,
        ScadaScene? activeScene,
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
            var source = projectionResolver.Resolve(page, repositoryRoot);
            inputs.Add(new Ft100ProjectPageExportInput(
                Synchronize(snapshot.Scenes[page.PageKey], page),
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
