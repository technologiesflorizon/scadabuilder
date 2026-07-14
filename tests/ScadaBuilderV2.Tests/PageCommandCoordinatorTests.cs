using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class PageCommandCoordinatorTests
{
    private readonly PageCommandCoordinator coordinator = new();

    [TestMethod]
    public void NewPageIsNativeExcludedInsertedAfterSelectionAndInheritsDefaultComposition()
    {
        var header = Page(Guid.NewGuid(), "header", ScadaPageType.Header);
        var footer = Page(Guid.NewGuid(), "footer", ScadaPageType.Footer);
        var active = Page(Guid.NewGuid(), "active", ScadaPageType.Default) with
        {
            HeaderPageKey = header.PageKey,
            FooterPageKey = footer.PageKey,
            HeaderPageId = header.EffectivePageCode,
            FooterPageId = footer.EffectivePageCode
        };
        var snapshot = Snapshot(header, active, footer);
        var ui = Ui(active.PageKey, active.PageKey, [active.PageKey]);

        var mutation = coordinator.Execute(snapshot, ui, new NewPageRequest("process_new", "Nouveau procédé"));

        Assert.IsTrue(mutation.Result.Changed);
        var created = mutation.After.Project.Scenes.Single(page => page.EffectivePageCode == "process_new");
        Assert.AreEqual(ScadaPageType.Default, created.Type);
        Assert.IsFalse(created.IncludeInBuild);
        Assert.AreEqual(PageOrigin.Native, created.EffectiveOrigin);
        Assert.AreEqual(header.PageKey, created.HeaderPageKey);
        Assert.AreEqual(footer.PageKey, created.FooterPageKey);
        Assert.AreEqual(2, mutation.After.Project.Scenes.ToList().IndexOf(created));
        Assert.AreEqual(created.PageKey, mutation.Result.PageToOpenKey);
        Assert.AreEqual($"scenes/{created.PageKey:N}.scene.json", created.RelativePath);
    }

    [TestMethod]
    public void RenameAndChangeCodePreserveStableKeyAndScenePath()
    {
        var page = Page(Guid.NewGuid(), "old_code", ScadaPageType.Default);
        var snapshot = Snapshot(page);
        var ui = Ui(page.PageKey, page.PageKey, [page.PageKey]);

        var renamed = coordinator.Execute(snapshot, ui, new RenamePageRequest(page.PageKey, "Nouveau titre"));
        var changedCode = coordinator.Execute(renamed.After, renamed.AfterUi, new ChangePageCodeRequest(page.PageKey, "new_code"));
        var reference = changedCode.After.Project.Scenes.Single();
        var scene = changedCode.After.Scenes[page.PageKey];

        Assert.AreEqual(page.PageKey, reference.PageKey);
        Assert.AreEqual(page.RelativePath, reference.RelativePath);
        Assert.AreEqual("Nouveau titre", reference.Title);
        Assert.AreEqual("new_code", reference.EffectivePageCode);
        Assert.AreEqual("new_code", scene.Id);
        Assert.AreEqual("new_code", scene.PageCode);
    }

    [TestMethod]
    public void ChangeCodeSynchronizesCompatibilityIdsWithoutChangingLogicalTargets()
    {
        var target = Page(Guid.NewGuid(), "old_target", ScadaPageType.Default);
        var source = Page(Guid.NewGuid(), "source", ScadaPageType.Default) with
        {
            HeaderPageKey = target.PageKey,
            HeaderPageId = "old_target"
        };
        var sourceScene = Scene(source) with
        {
            HeaderPageKey = target.PageKey,
            HeaderPageId = "old_target",
            Actions = [new ScadaActionDefinition("go", ScadaActionKind.Navigate,
                TargetPageKey: target.PageKey, TargetPageId: "old_target")],
            Elements = [ScadaElement.CreateText("button", "Button", 0, 0) with
            {
                CommandConfig = new ScadaElementCommandConfig([
                    new ScadaCommandBinding("go-command", "Go", true, ScadaCommandTrigger.OnClick,
                        ScadaCommandKind.Navigate, TargetPageId: "old_target", TargetPageKey: target.PageKey)])
            }]
        };
        var project = ScadaProject.CreateDefault("Pages") with
        {
            Scenes = [target, source],
            HomePageKey = target.PageKey,
            HomePageId = "old_target"
        };
        var snapshot = Snapshot(project, Scene(target), sourceScene);

        var mutation = coordinator.Execute(snapshot, Ui(target.PageKey, target.PageKey, [target.PageKey]),
            new ChangePageCodeRequest(target.PageKey, "new_target"));
        var updatedSource = mutation.After.Scenes[source.PageKey];

        Assert.AreEqual("new_target", mutation.After.Project.HomePageId);
        Assert.AreEqual("new_target", mutation.After.Project.Scenes.Single(page => page.PageKey == source.PageKey).HeaderPageId);
        Assert.AreEqual("new_target", updatedSource.HeaderPageId);
        Assert.AreEqual("new_target", updatedSource.ActionDefinitions.Single().TargetPageId);
        Assert.AreEqual("new_target", updatedSource.Elements.Single().EffectiveCommandConfig.Commands.Single().TargetPageId);
        Assert.AreEqual(target.PageKey, updatedSource.ActionDefinitions.Single().TargetPageKey);
    }

    [TestMethod]
    public void DuplicatePreservesWonderwareProjectionAndRewritesRecursiveSelfReferences()
    {
        var sourceKey = Guid.NewGuid();
        var provenance = new ImportProvenance("Wonderware", "MetroBoeuf", "win00012", @"F:\source\win00012.html");
        var source = Page(sourceKey, "win00012", ScadaPageType.Default) with
        {
            Origin = PageOrigin.Imported,
            ImportProvenance = provenance
        };
        var command = new ScadaCommandBinding("self-command", "Self", true, ScadaCommandTrigger.OnClick,
            ScadaCommandKind.Navigate, TargetPageKey: sourceKey, TargetPageId: "win00012");
        var child = ScadaElement.CreateText("child", "Child", 0, 0) with
        {
            CommandConfig = new ScadaElementCommandConfig([command]),
            Events = [new ScadaObjectEventBinding("click", "self-action")]
        };
        var scene = Scene(source) with
        {
            Elements = [ScadaElement.CreateText("group", "Group", 0, 0) with { Children = [child] }],
            Actions = [new ScadaActionDefinition("self-action", ScadaActionKind.Navigate,
                TargetPageId: "win00012", TargetPageKey: sourceKey)]
        };
        var snapshot = Snapshot([source], [scene]);

        var mutation = coordinator.Execute(snapshot, Ui(sourceKey, sourceKey, [sourceKey]),
            new DuplicatePageRequest(sourceKey, "win00012_copy", "Copie"));
        var copy = mutation.After.Project.Scenes.Single(page => page.PageKey != sourceKey);
        var copyScene = mutation.After.Scenes[copy.PageKey];
        var copiedAction = copyScene.ActionDefinitions.Single();
        var copiedChild = copyScene.Elements.Single().ChildElements.Single();
        var copiedCommand = copiedChild.EffectiveCommandConfig.Commands.Single();

        Assert.AreEqual(PageOrigin.Imported, copy.Origin);
        Assert.AreSame(provenance, copy.ImportProvenance);
        Assert.AreEqual(provenance, copyScene.ImportProvenance);
        Assert.AreEqual(copy.PageKey, copiedAction.TargetPageKey);
        Assert.AreNotEqual("self-action", copiedAction.Id);
        Assert.AreEqual(copiedAction.Id, copiedChild.EventBindings.Single().ActionId);
        Assert.AreEqual(copy.PageKey, copiedCommand.TargetPageKey);
        Assert.AreEqual("win00012_copy", copiedCommand.TargetPageId);
    }

    [TestMethod]
    public void DeleteReturnsEveryBlockingDependencyWithoutMutatingSnapshot()
    {
        var source = Page(Guid.NewGuid(), "source", ScadaPageType.Default);
        var target = Page(Guid.NewGuid(), "target", ScadaPageType.Default);
        var command = new ScadaCommandBinding("go", "Go", true, ScadaCommandTrigger.OnClick,
            ScadaCommandKind.Navigate, TargetPageKey: target.PageKey);
        var sourceScene = Scene(source) with
        {
            Elements = [ScadaElement.CreateText("button", "Button", 0, 0) with
            {
                CommandConfig = new ScadaElementCommandConfig([command])
            }],
            Actions = [new ScadaActionDefinition("go-action", ScadaActionKind.Navigate, TargetPageKey: target.PageKey)]
        };
        var snapshot = Snapshot([source, target], [sourceScene, Scene(target)]);

        var mutation = coordinator.Execute(snapshot, Ui(target.PageKey, source.PageKey, [source.PageKey, target.PageKey]),
            new DeletePageRequest(target.PageKey));

        Assert.AreEqual(ScadaBuilderV2.Application.Commands.CommandResultStatus.Blocked, mutation.Result.Status);
        Assert.AreEqual(2, mutation.Result.Diagnostics.Count(issue => issue.Code == "page.delete-dependency"));
        Assert.AreSame(snapshot, mutation.After);
        Assert.AreEqual(2, snapshot.Project.Scenes.Count);
    }

    [TestMethod]
    public void DeleteUnreferencedPageClosesTabAndQueuesDurableDeletion()
    {
        var keep = Page(Guid.NewGuid(), "keep", ScadaPageType.Default);
        var remove = Page(Guid.NewGuid(), "remove", ScadaPageType.Fragment);
        var snapshot = Snapshot(keep, remove);
        var ui = Ui(remove.PageKey, remove.PageKey, [keep.PageKey, remove.PageKey]);

        var mutation = coordinator.Execute(snapshot, ui, new DeletePageRequest(remove.PageKey));

        Assert.IsTrue(mutation.Result.Changed);
        Assert.IsFalse(mutation.After.Project.Scenes.Any(page => page.PageKey == remove.PageKey));
        Assert.AreEqual(remove.RelativePath, mutation.After.PendingDeletions.Single().RelativePath);
        Assert.AreEqual(keep.PageKey, mutation.AfterUi.SelectedPageKey);
        Assert.AreEqual(keep.PageKey, mutation.AfterUi.ActivePageKey);
        Assert.IsFalse(mutation.AfterUi.OpenPageKeys.Contains(remove.PageKey));
    }

    [TestMethod]
    public void PropertyRulesProtectHomeAndValidateCompositionTypes()
    {
        var home = Page(Guid.NewGuid(), "home", ScadaPageType.Default);
        var fragment = Page(Guid.NewGuid(), "fragment", ScadaPageType.Fragment);
        var project = ScadaProject.CreateDefault("Pages") with
        {
            Scenes = [home, fragment],
            HomePageKey = home.PageKey,
            HomePageId = home.EffectivePageCode
        };
        var snapshot = Snapshot(project, Scene(home), Scene(fragment));
        var ui = Ui(home.PageKey, home.PageKey, [home.PageKey]);

        var exclude = coordinator.Execute(snapshot, ui, new SetPageBuildInclusionRequest(home.PageKey, false));
        var compose = coordinator.Execute(snapshot, ui, new SetPageCompositionRequest(home.PageKey, fragment.PageKey, null));

        Assert.AreEqual(ScadaBuilderV2.Application.Commands.CommandResultStatus.Blocked, exclude.Result.Status);
        Assert.AreEqual(ScadaBuilderV2.Application.Commands.CommandResultStatus.Blocked, compose.Result.Status);
        Assert.AreSame(snapshot, exclude.After);
        Assert.AreSame(snapshot, compose.After);
    }

    private static ScadaSceneReference Page(Guid key, string code, ScadaPageType type) =>
        new(code, code, $"scenes/{key:N}.scene.json", type, PageKey: key, PageCode: code);

    private static ScadaScene Scene(ScadaSceneReference page) =>
        ScadaScene.CreateEmpty(page.EffectivePageCode, page.Title, page.EffectiveCanvasSize) with
        {
            PageKey = page.PageKey,
            PageCode = page.EffectivePageCode,
            PageType = page.Type,
            IncludeInBuild = page.IncludeInBuild,
            Origin = page.Origin,
            ImportProvenance = page.ImportProvenance
        };

    private static PageWorkspaceSnapshot Snapshot(params ScadaSceneReference[] pages) =>
        Snapshot(pages, pages.Select(Scene).ToArray());

    private static PageWorkspaceSnapshot Snapshot(IReadOnlyList<ScadaSceneReference> pages, IReadOnlyList<ScadaScene> scenes) =>
        Snapshot(ScadaProject.CreateDefault("Pages") with { Scenes = pages }, scenes.ToArray());

    private static PageWorkspaceSnapshot Snapshot(ScadaProject project, params ScadaScene[] scenes) =>
        new(1, project, scenes.ToDictionary(scene => scene.PageKey), []);

    private static ProjectWorkspaceUiSnapshot Ui(Guid? selected, Guid? active, IReadOnlyList<Guid> open) =>
        new(open, selected, active, new Dictionary<Guid, EditorPageSelectionSnapshot>());
}
