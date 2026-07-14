using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ModernProjects;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class PageDependencyAnalyzerTests
{
    [TestMethod]
    public void AnalyzeFindsProjectActionsNestedCommandsAndOpenTabsOnExcludedPages()
    {
        var sourceKey = Guid.NewGuid();
        var headerKey = Guid.NewGuid();
        var footerKey = Guid.NewGuid();
        var defaultKey = Guid.NewGuid();
        var fragmentKey = Guid.NewGuid();
        var nestedCommand = new ScadaCommandBinding(
            "open-details", "Details", true, ScadaCommandTrigger.OnClick,
            ScadaCommandKind.OpenPopup, TargetPageKey: fragmentKey);
        var child = ScadaElement.CreateText("child", "Child", 0, 0) with
        {
            CommandConfig = new ScadaElementCommandConfig([nestedCommand])
        };
        var group = ScadaElement.CreateText("group", "Group", 0, 0) with { Children = [child] };
        var sourceScene = CreateScene(sourceKey, "source", includeInBuild: false) with
        {
            Elements = [group],
            Actions = [new ScadaActionDefinition("go", ScadaActionKind.Navigate, TargetPageKey: defaultKey)]
        };
        var project = CreateProject(
            new ScadaSceneReference("source", "Source", $"scenes/{sourceKey:N}.scene.json", IncludeInBuild: false,
                HeaderPageKey: headerKey, FooterPageKey: footerKey, PageKey: sourceKey, PageCode: "source"),
            Page(headerKey, "header", ScadaPageType.Header),
            Page(footerKey, "footer", ScadaPageType.Footer),
            Page(defaultKey, "target", ScadaPageType.Default),
            Page(fragmentKey, "details", ScadaPageType.Fragment)) with
        {
            HomePageKey = defaultKey,
            HomePageId = "target"
        };
        var snapshot = Snapshot(project, sourceScene);

        var analysis = new PageDependencyAnalyzer().Analyze(snapshot, [fragmentKey]);

        CollectionAssert.IsSubsetOf(
            new[]
            {
                PageDependencyKind.Home,
                PageDependencyKind.Header,
                PageDependencyKind.Footer,
                PageDependencyKind.ActionNavigate,
                PageDependencyKind.CommandPopup,
                PageDependencyKind.OpenWorkspaceTab
            },
            analysis.Dependencies.Select(item => item.Kind).ToArray());
        Assert.IsTrue(analysis.Dependencies.Any(item =>
            item.Kind == PageDependencyKind.CommandPopup && item.ElementId == "child" && item.CommandId == "open-details"));
        Assert.AreEqual(0, analysis.Diagnostics.Count);
    }

    [TestMethod]
    public void AnalyzeReturnsStructuredLocationForMissingTarget()
    {
        var sourceKey = Guid.NewGuid();
        var missingKey = Guid.NewGuid();
        var scene = CreateScene(sourceKey, "source") with
        {
            Actions = [new ScadaActionDefinition("missing", ScadaActionKind.Navigate, TargetPageKey: missingKey)]
        };
        var project = CreateProject(Page(sourceKey, "source", ScadaPageType.Default));

        var issue = new PageDependencyAnalyzer().Analyze(Snapshot(project, scene)).Diagnostics.Single();

        Assert.AreEqual("page.reference-missing", issue.Code);
        Assert.AreEqual(sourceKey, issue.PageKey);
        Assert.AreEqual(missingKey, issue.TargetKey);
        Assert.AreEqual("missing", issue.CommandId);
        StringAssert.Contains(issue.PropertyPath, "TargetPageKey");
        Assert.IsFalse(string.IsNullOrWhiteSpace(issue.SuggestedFix));
    }

    [TestMethod]
    public async Task WorkspaceReaderUsesDirtyOverrideAndLoadsClosedSceneFromStore()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ModernProjectStore();
            var imported = await store.EnsureReferenceModernProjectAsync(root,
            [
                new ScadaSceneReference("open", "Open", "scenes/open.scene.json"),
                new ScadaSceneReference("closed", "Closed", "scenes/closed.scene.json"),
                new ScadaSceneReference("target", "Target", "scenes/target.scene.json")
            ]);
            var open = imported.Scenes.Single(page => page.EffectivePageCode == "open");
            var closed = imported.Scenes.Single(page => page.EffectivePageCode == "closed");
            var target = imported.Scenes.Single(page => page.EffectivePageCode == "target");
            var durableClosed = CreateScene(closed.PageKey, "closed") with
            {
                Actions = [new ScadaActionDefinition("closed-go", ScadaActionKind.Navigate, TargetPageKey: target.PageKey)]
            };
            var durableOpen = CreateScene(open.PageKey, "open");
            var targetScene = CreateScene(target.PageKey, "target");
            await store.SaveWorkspaceSnapshotAsync(root, new PageWorkspaceSnapshot(
                1,
                imported,
                new Dictionary<Guid, ScadaScene>
                {
                    [open.PageKey] = durableOpen,
                    [closed.PageKey] = durableClosed,
                    [target.PageKey] = targetScene
                },
                []));
            var dirtyOpen = durableOpen with
            {
                Actions = [new ScadaActionDefinition("dirty-go", ScadaActionKind.Navigate, TargetPageKey: target.PageKey)]
            };

            var snapshot = await store.ReadWorkspaceSnapshotAsync(
                root,
                new PageWorkspaceReadContext(new Dictionary<Guid, ScadaScene> { [open.PageKey] = dirtyOpen }));
            var analysis = new PageDependencyAnalyzer().Analyze(snapshot);

            Assert.IsTrue(analysis.Dependencies.Any(item => item.ActionId == "dirty-go"));
            Assert.IsTrue(analysis.Dependencies.Any(item => item.ActionId == "closed-go"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [TestMethod]
    public void BuildValidationChecksPageCommandsWithoutTagCatalog()
    {
        var sourceKey = Guid.NewGuid();
        var fragmentKey = Guid.NewGuid();
        var command = new ScadaCommandBinding(
            "bad-navigation", "Bad", true, ScadaCommandTrigger.OnClick,
            ScadaCommandKind.Navigate, TargetPageKey: fragmentKey);
        var element = ScadaElement.CreateText("button", "Button", 0, 0) with
        {
            CommandConfig = new ScadaElementCommandConfig([command])
        };
        var scene = CreateScene(sourceKey, "source") with { Elements = [element] };
        var project = CreateProject(
            Page(sourceKey, "source", ScadaPageType.Default),
            Page(fragmentKey, "fragment", ScadaPageType.Fragment));

        var issue = ScadaProjectBuildValidator.Validate(project, [scene])
            .Single(item => item.Code == "command.navigate-wrong-type");

        Assert.AreEqual(sourceKey, issue.PageKey);
        Assert.AreEqual(fragmentKey, issue.TargetKey);
        Assert.AreEqual("button", issue.ElementId);
        Assert.AreEqual("bad-navigation", issue.CommandId);
    }

    private static ScadaProject CreateProject(params ScadaSceneReference[] pages) =>
        ScadaProject.CreateDefault("Pages") with { Scenes = pages };

    private static ScadaSceneReference Page(Guid key, string code, ScadaPageType type) =>
        new(code, code, $"scenes/{key:N}.scene.json", type, PageKey: key, PageCode: code);

    private static ScadaScene CreateScene(Guid key, string code, bool includeInBuild = true) =>
        ScadaScene.CreateEmpty(code, code, CanvasSize.DefaultDesktop) with
        {
            PageKey = key,
            PageCode = code,
            IncludeInBuild = includeInBuild
        };

    private static PageWorkspaceSnapshot Snapshot(ScadaProject project, params ScadaScene[] suppliedScenes)
    {
        var supplied = suppliedScenes.ToDictionary(scene => scene.PageKey);
        var scenes = project.Scenes.ToDictionary(
            page => page.PageKey,
            page => supplied.GetValueOrDefault(page.PageKey) ?? CreateScene(page.PageKey, page.EffectivePageCode, page.IncludeInBuild));
        return new PageWorkspaceSnapshot(1, project, scenes, []);
    }
}
