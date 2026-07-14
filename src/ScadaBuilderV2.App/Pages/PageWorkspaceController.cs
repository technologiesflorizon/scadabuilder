using System.Collections.ObjectModel;
using ScadaBuilderV2.App.Workspace;
using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ModernProjects;

namespace ScadaBuilderV2.App.Pages;

/// <summary>Owns the modern page inventory, open tabs, coherent snapshots and atomic saves outside MainWindow.</summary>
public sealed class PageWorkspaceController(
    ModernProjectStore store,
    IPageWorkspaceHost host)
{
    private string? repositoryRoot;
    private ScadaProject? project;
    private IReadOnlyList<PendingPageDeletion> pendingDeletions = Array.Empty<PendingPageDeletion>();

    public ObservableCollection<SceneWorkspaceTab> OpenTabs { get; } = [];
    public EditorHistoryService History { get; } = new();
    public SceneWorkspaceTab? ActiveTab { get; private set; }
    public ScadaProject? Project => project;
    public bool IsProjectDirty { get; private set; }
    public IReadOnlyList<PendingPageDeletion> PendingDeletions => pendingDeletions;

    public void Initialize(string root, ScadaProject modernProject)
    {
        repositoryRoot = string.IsNullOrWhiteSpace(root) ? throw new ArgumentException("Repository root is required.", nameof(root)) : root;
        project = modernProject ?? throw new ArgumentNullException(nameof(modernProject));
    }

    public void ReplaceProject(ScadaProject modernProject, bool markDirty = false)
    {
        project = modernProject ?? throw new ArgumentNullException(nameof(modernProject));
        foreach (var tab in OpenTabs)
        {
            var page = project.Scenes.FirstOrDefault(item => item.PageKey == tab.PageKey);
            if (page is not null) tab.UpdatePage(page);
        }
        IsProjectDirty |= markDirty;
    }

    public async Task<SceneWorkspaceTab> OpenAsync(Guid pageKey, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var existing = OpenTabs.FirstOrDefault(tab => tab.PageKey == pageKey);
        if (existing is not null)
        {
            await ActivateAsync(existing);
            return existing;
        }

        var page = project!.Scenes.SingleOrDefault(item => item.PageKey == pageKey)
            ?? throw new InvalidOperationException("The requested page does not exist in the modern project.");
        var scene = await store.LoadOrCreateSceneAsync(repositoryRoot!, page, cancellationToken);
        var tab = new SceneWorkspaceTab(new PageWorkspaceEntry(page), scene);
        OpenTabs.Add(tab);
        await ActivateAsync(tab);
        return tab;
    }

    public async Task ActivateAsync(SceneWorkspaceTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (!OpenTabs.Contains(tab)) throw new InvalidOperationException("The page tab is not part of this workspace.");
        ActiveTab = tab;
        await host.ActivatePageAsync(tab);
    }

    public async Task<bool> CloseAsync(SceneWorkspaceTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (tab.IsDirty && !await host.ConfirmCloseDirtyPageAsync(tab)) return false;
        var index = OpenTabs.IndexOf(tab);
        var wasActive = ReferenceEquals(ActiveTab, tab);
        OpenTabs.Remove(tab);
        if (!wasActive)
        {
            host.ReportPageWorkspaceStatus($"Scene fermée: {tab.SceneId}");
            return true;
        }

        ActiveTab = null;
        if (OpenTabs.Count == 0)
        {
            host.ClearActivePage(tab);
        }
        else
        {
            await ActivateAsync(OpenTabs[Math.Clamp(index, 0, OpenTabs.Count - 1)]);
        }
        host.ReportPageWorkspaceStatus($"Scene fermée: {tab.SceneId}");
        return true;
    }

    public async Task<PageWorkspaceSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var overrides = OpenTabs.ToDictionary(
            tab => tab.PageKey,
            tab => tab.Scene);
        return await store.ReadWorkspaceSnapshotAsync(
            repositoryRoot!,
            new PageWorkspaceReadContext(
                overrides,
                OpenTabs.Select(tab => tab.PageKey).ToArray(),
                project,
                pendingDeletions),
            cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await CaptureSnapshotAsync(cancellationToken);
        await store.SaveWorkspaceSnapshotAsync(repositoryRoot!, snapshot, cancellationToken);
        project = snapshot.Project;
        foreach (var tab in OpenTabs) tab.IsDirty = false;
        IsProjectDirty = false;
        pendingDeletions = Array.Empty<PendingPageDeletion>();
    }

    public ProjectWorkspaceUiSnapshot CaptureUiSnapshot(Guid? selectedPageKey) => new(
        OpenTabs.Select(tab => tab.PageKey).ToArray(),
        selectedPageKey,
        ActiveTab?.PageKey,
        OpenTabs.ToDictionary(
            tab => tab.PageKey,
            tab => new EditorPageSelectionSnapshot(tab.SelectedModernElementIds, tab.PrimaryModernElementId)));

    public async Task ApplyMutationAsync(PageWorkspaceMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        project = mutation.After.Project;
        pendingDeletions = mutation.After.PendingDeletions;
        var openKeys = mutation.AfterUi.OpenPageKeys.ToHashSet();
        foreach (var tab in OpenTabs.Where(tab => !openKeys.Contains(tab.PageKey)).ToArray())
        {
            OpenTabs.Remove(tab);
        }

        foreach (var pageKey in mutation.AfterUi.OpenPageKeys)
        {
            var page = project.Scenes.FirstOrDefault(item => item.PageKey == pageKey);
            if (page is null || !mutation.After.Scenes.TryGetValue(pageKey, out var scene)) continue;
            var tab = OpenTabs.FirstOrDefault(item => item.PageKey == pageKey);
            if (tab is null)
            {
                tab = new SceneWorkspaceTab(new PageWorkspaceEntry(page), scene);
                OpenTabs.Add(tab);
            }
            else
            {
                tab.UpdatePage(page);
                tab.Scene = scene;
            }

            if (mutation.AfterUi.PageSelections.TryGetValue(pageKey, out var selection))
            {
                tab.SelectedModernElementIds = selection.ElementIds;
                tab.PrimaryModernElementId = selection.PrimaryElementId;
            }
        }

        IsProjectDirty |= mutation.Result.WorkspaceDirty;
        var active = mutation.AfterUi.ActivePageKey is { } activeKey
            ? OpenTabs.FirstOrDefault(tab => tab.PageKey == activeKey)
            : null;
        if (active is not null)
        {
            await ActivateAsync(active);
        }
        else if (ActiveTab is { } previous)
        {
            ActiveTab = null;
            host.ClearActivePage(previous);
        }
    }

    public void UpdateActiveScene(ScadaScene scene, bool markDirty)
    {
        if (ActiveTab is null) return;
        ActiveTab.Scene = scene;
        ActiveTab.IsDirty |= markDirty;
    }

    public ScadaProject ReconcileProjectFromOpenScenes(ScadaScene? activeScene = null)
    {
        EnsureInitialized();
        var references = project!.Scenes.ToList();
        foreach (var tab in OpenTabs)
        {
            var scene = ReferenceEquals(tab, ActiveTab) && activeScene is not null ? activeScene : tab.Scene;
            var existing = references.FirstOrDefault(item => item.PageKey == tab.PageKey) ?? tab.Page;
            var reference = ToSceneReference(scene, existing);
            var index = references.FindIndex(item => item.PageKey == reference.PageKey);
            if (index >= 0) references[index] = reference;
        }
        project = EnsureHomePageStillValid(project with { Scenes = references });
        return project;
    }

    public static IReadOnlyList<ScadaSceneReference> CreateImportedPageReferences(
        string projectName,
        IEnumerable<ImportedPageDescriptor> pages) => pages.Select(page =>
            new ScadaSceneReference(
                page.PageCode,
                page.Title,
                $"scenes/{PageKeyFactory.CreateDeterministic(projectName, page.PageCode):N}.scene.json",
                PageKey: PageKeyFactory.CreateDeterministic(projectName, page.PageCode),
                PageCode: page.PageCode,
                Origin: PageOrigin.Imported,
                ImportProvenance: new ImportProvenance("Wonderware", projectName, page.PageCode, page.SourcePath)))
            .ToArray();

    public static ScadaProject SetHomePage(ScadaProject project, Guid? pageKey)
    {
        var page = pageKey is { } key ? project.Scenes.FirstOrDefault(item => item.PageKey == key) : null;
        return project with { HomePageKey = page?.PageKey, HomePageId = page?.EffectivePageCode };
    }

    private static ScadaSceneReference ToSceneReference(ScadaScene scene, ScadaSceneReference existing) => existing with
    {
        Id = scene.EffectivePageCode,
        Title = scene.Title,
        Type = scene.PageType,
        CanvasSize = scene.CanvasSize,
        Background = scene.EffectiveBackground,
        IncludeInBuild = scene.IncludeInBuild,
        HeaderPageId = scene.HeaderPageId,
        FooterPageId = scene.FooterPageId,
        PageKey = scene.PageKey,
        PageCode = scene.EffectivePageCode,
        Origin = scene.EffectiveOrigin,
        ImportProvenance = scene.ImportProvenance,
        HeaderPageKey = scene.HeaderPageKey,
        FooterPageKey = scene.FooterPageKey
    };

    private static ScadaProject EnsureHomePageStillValid(ScadaProject project)
    {
        if (project.HomePageKey is not { } key || key == Guid.Empty) return project;
        var home = project.Scenes.FirstOrDefault(page => page.PageKey == key);
        return home is { Type: ScadaPageType.Default, IncludeInBuild: true }
            ? project with { HomePageId = home.EffectivePageCode }
            : project with { HomePageKey = null, HomePageId = null };
    }

    private void EnsureInitialized()
    {
        if (repositoryRoot is null || project is null) throw new InvalidOperationException("The page workspace is not initialized.");
    }
}
