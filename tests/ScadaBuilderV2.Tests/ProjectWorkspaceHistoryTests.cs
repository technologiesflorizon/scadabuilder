using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ProjectWorkspaceHistoryTests
{
    [TestMethod]
    public async Task ProjectActionRestoresDeletedPageAfterItsTabWasClosed()
    {
        var pageOneKey = Guid.NewGuid();
        var pageTwoKey = Guid.NewGuid();
        var pageOne = ScadaScene.CreateEmpty("page_one", "One", CanvasSize.DefaultDesktop) with
        {
            PageKey = pageOneKey,
            PageCode = "page_one"
        };
        var pageTwo = ScadaScene.CreateEmpty("page_two", "Two", CanvasSize.DefaultDesktop) with
        {
            PageKey = pageTwoKey,
            PageCode = "page_two"
        };
        var beforeProject = CreateProject(pageOne, pageTwo);
        var afterProject = beforeProject with
        {
            Scenes = beforeProject.Scenes.Where(page => page.PageKey != pageTwoKey).ToArray()
        };
        var before = new ProjectWorkspaceHistorySnapshot(
            beforeProject,
            new Dictionary<Guid, ScadaScene> { [pageOneKey] = pageOne, [pageTwoKey] = pageTwo },
            new ProjectWorkspaceUiSnapshot(
                [pageOneKey, pageTwoKey],
                pageTwoKey,
                pageTwoKey,
                new Dictionary<Guid, EditorPageSelectionSnapshot>
                {
                    [pageTwoKey] = new(["pump"], "pump")
                }),
            IsDirty: false,
            PendingDeletedPageKeys: []);
        var after = new ProjectWorkspaceHistorySnapshot(
            afterProject,
            new Dictionary<Guid, ScadaScene> { [pageOneKey] = pageOne },
            new ProjectWorkspaceUiSnapshot(
                [pageOneKey],
                pageOneKey,
                pageOneKey,
                new Dictionary<Guid, EditorPageSelectionSnapshot>()),
            IsDirty: true,
            PendingDeletedPageKeys: [pageTwoKey]);
        var workspace = new TestWorkspace(after);
        var history = new EditorHistoryService();
        history.Push(new ProjectWorkspaceSnapshotAction(before, after, "supprimer page"));

        workspace.Ui = workspace.Ui with
        {
            OpenPageKeys = [],
            SelectedPageKey = null,
            ActivePageKey = null
        };

        Assert.IsTrue(await history.UndoAsync(workspace.CreateContext()));
        Assert.AreEqual(2, workspace.Project.Scenes.Count);
        Assert.IsTrue(workspace.Scenes.ContainsKey(pageTwoKey));
        CollectionAssert.AreEqual(new[] { pageOneKey, pageTwoKey }, workspace.Ui.OpenPageKeys.ToArray());
        Assert.AreEqual(pageTwoKey, workspace.Ui.ActivePageKey);
        CollectionAssert.AreEqual(new[] { "pump" }, workspace.Ui.PageSelections[pageTwoKey].ElementIds.ToArray());
        Assert.IsFalse(workspace.IsDirty);
        Assert.AreEqual(0, workspace.PendingDeletedPageKeys.Count);
    }

    [TestMethod]
    public async Task ProjectActionRemainsUndoableAfterSuccessfulSave()
    {
        var pageKey = Guid.NewGuid();
        var beforeScene = ScadaScene.CreateEmpty("page_one", "Before", CanvasSize.DefaultDesktop) with
        {
            PageKey = pageKey,
            PageCode = "page_one"
        };
        var afterScene = beforeScene with { Title = "After" };
        var beforeProject = CreateProject(beforeScene);
        var afterProject = CreateProject(afterScene);
        var ui = new ProjectWorkspaceUiSnapshot(
            [pageKey],
            pageKey,
            pageKey,
            new Dictionary<Guid, EditorPageSelectionSnapshot>());
        var before = new ProjectWorkspaceHistorySnapshot(
            beforeProject,
            new Dictionary<Guid, ScadaScene> { [pageKey] = beforeScene },
            ui,
            IsDirty: false,
            PendingDeletedPageKeys: []);
        var after = new ProjectWorkspaceHistorySnapshot(
            afterProject,
            new Dictionary<Guid, ScadaScene> { [pageKey] = afterScene },
            ui,
            IsDirty: true,
            PendingDeletedPageKeys: []);
        var workspace = new TestWorkspace(after) { IsDirty = false };
        var history = new EditorHistoryService();
        history.Push(new ProjectWorkspaceSnapshotAction(before, after, "renommer page"));

        Assert.IsTrue(await history.UndoAsync(workspace.CreateContext()));
        Assert.AreEqual("Before", workspace.Scenes[pageKey].Title);
        Assert.IsFalse(workspace.IsDirty);

        Assert.IsTrue(await history.RedoAsync(workspace.CreateContext()));
        Assert.AreEqual("After", workspace.Scenes[pageKey].Title);
        Assert.IsTrue(workspace.IsDirty);
    }

    [TestMethod]
    public async Task SceneActionUsesPageKeyWhenSceneTabIsNotActive()
    {
        var pageKey = Guid.NewGuid();
        var scene = ScadaScene.CreateEmpty("legacy_code", "Page", CanvasSize.DefaultDesktop) with
        {
            PageKey = pageKey,
            PageCode = "page_code",
            BackgroundColor = "#2090A0"
        };
        var project = CreateProject(scene);
        var workspace = new TestWorkspace(new ProjectWorkspaceHistorySnapshot(
            project,
            new Dictionary<Guid, ScadaScene> { [pageKey] = scene },
            new ProjectWorkspaceUiSnapshot([], null, null, new Dictionary<Guid, EditorPageSelectionSnapshot>()),
            IsDirty: true,
            PendingDeletedPageKeys: []));
        var history = new EditorHistoryService();
        history.Push(new SceneBackgroundChangedAction(
            "legacy_code",
            "#000000",
            "#2090A0",
            pageKey));

        Assert.IsTrue(await history.UndoAsync(workspace.CreateContext()));
        Assert.AreEqual("#000000", workspace.Scenes[pageKey].BackgroundColor);
        Assert.IsTrue(workspace.IsDirty);
    }

    private static ScadaProject CreateProject(params ScadaScene[] scenes)
    {
        return ScadaProject.CreateDefault("History") with
        {
            Scenes = scenes.Select(scene => new ScadaSceneReference(
                scene.Id,
                scene.Title,
                $"scenes/{scene.PageKey:N}.scene.json",
                PageKey: scene.PageKey,
                PageCode: scene.EffectivePageCode)).ToArray()
        };
    }

    private sealed class TestWorkspace(ProjectWorkspaceHistorySnapshot snapshot)
    {
        public ScadaProject Project { get; set; } = snapshot.Project;

        public Dictionary<Guid, ScadaScene> Scenes { get; } = snapshot.Scenes.ToDictionary(item => item.Key, item => item.Value);

        public ProjectWorkspaceUiSnapshot Ui { get; set; } = snapshot.Ui;

        public bool IsDirty { get; set; } = snapshot.IsDirty;

        public IReadOnlyList<Guid> PendingDeletedPageKeys { get; set; } = snapshot.PendingDeletedPageKeys;

        public int RefreshCount { get; private set; }

        public EditorHistoryContext CreateContext()
        {
            return new EditorHistoryContext
            {
                ActiveSceneId = string.Empty,
                GetActiveScene = () => null,
                ReplaceActiveScene = _ => throw new AssertFailedException("No active scene should be required."),
                GetSceneByPageKey = pageKey => Scenes.GetValueOrDefault(pageKey),
                ReplaceSceneByPageKey = (pageKey, scene) => Scenes[pageKey] = scene,
                RemoveSceneByPageKey = pageKey => Scenes.Remove(pageKey),
                GetWorkspaceSceneKeys = () => Scenes.Keys.ToArray(),
                GetProject = () => Project,
                ReplaceProject = project => Project = project,
                RestoreWorkspaceUi = ui => Ui = ui,
                SetPendingDeletedPageKeys = pageKeys => PendingDeletedPageKeys = pageKeys.ToArray(),
                SetWorkspaceDirty = isDirty => IsDirty = isDirty,
                MarkDirty = () => IsDirty = true,
                RefreshPreviewAsync = () => Task.CompletedTask,
                RefreshTargetAsync = _ =>
                {
                    RefreshCount++;
                    return Task.CompletedTask;
                },
                SetStatus = _ => { }
            };
        }
    }
}
