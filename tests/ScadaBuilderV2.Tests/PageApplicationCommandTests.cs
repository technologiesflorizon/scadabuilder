using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Commands.Pages;
using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class PageApplicationCommandTests
{
    [TestMethod]
    public async Task AllPageSurfacesCanRegisterTheSameStableCommandIds()
    {
        var coordinator = new PageCommandCoordinator();
        IApplicationCommand[] commands =
        [
            new NewPageCommand(coordinator), new RenamePageCommand(coordinator), new DuplicatePageCommand(coordinator),
            new DeletePageCommand(coordinator), new ChangePageCodeCommand(coordinator), new OpenPageCommand(coordinator),
            new ShowPagePropertiesCommand(coordinator), new SetPageBuildInclusionCommand(coordinator),
            new SetHomePageCommand(coordinator), new SetPageTypeCommand(coordinator),
            new SetPageCompositionCommand(coordinator), new SetPageCanvasCommand(coordinator),
            new SetPageBackgroundCommand(coordinator), new ValidatePagesCommand(coordinator)
        ];
        var registry = new CommandRegistry();
        foreach (var command in commands) registry.Register(command);

        CollectionAssert.AreEquivalent(
            new[] { "page.new", "page.rename", "page.duplicate", "page.delete", "page.change-code", "page.open",
                "page.properties", "page.set-build-inclusion", "page.set-home", "page.set-type", "page.set-composition",
                "page.set-canvas", "page.set-background", "page.validate" },
            registry.Commands.Select(command => command.Id).ToArray());

        var context = Context();
        context.PageCommandRequest = new NewPageRequest("new_page");
        var result = await registry.ExecuteAsync("page.new", context);

        Assert.AreEqual(CommandResultStatus.Succeeded, result.Status);
        Assert.IsTrue(context.IsPageWorkspaceDirty);
        Assert.AreEqual(1, context.WorkspaceHistory!.UndoCount);
        Assert.IsNotNull(context.PageWorkspace!.Project.Scenes.Single(page => page.EffectivePageCode == "new_page"));
    }

    [TestMethod]
    public async Task BlockedCommandDoesNotApplyMutationOrPushHistory()
    {
        var context = Context();
        var existing = context.PageWorkspace!.Project.Scenes.Single();
        context.PageCommandRequest = new NewPageRequest(existing.EffectivePageCode);
        var applied = 0;
        context.ApplyPageWorkspaceMutation = _ => applied++;
        var registry = new CommandRegistry();
        registry.Register(new NewPageCommand(new PageCommandCoordinator()));

        var result = await registry.ExecuteAsync("page.new", context);

        Assert.AreEqual(CommandResultStatus.Blocked, result.Status);
        Assert.AreEqual(0, applied);
        Assert.AreEqual(0, context.WorkspaceHistory!.UndoCount);
        Assert.IsFalse(context.IsPageWorkspaceDirty);
        Assert.AreEqual(1, context.PageWorkspace.Project.Scenes.Count);
    }

    [TestMethod]
    public async Task CancellationBeforeExecutionLeavesWorkspaceUnchanged()
    {
        var context = Context();
        context.PageCommandRequest = new NewPageRequest("cancelled_page");
        var registry = new CommandRegistry();
        registry.Register(new NewPageCommand(new PageCommandCoordinator()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await registry.ExecuteAsync("page.new", context, cancellation.Token);

        Assert.AreEqual(CommandResultStatus.Cancelled, result.Status);
        Assert.AreEqual(1, context.PageWorkspace!.Project.Scenes.Count);
        Assert.AreEqual(0, context.WorkspaceHistory!.UndoCount);
    }

    [TestMethod]
    public async Task OpenRoutesWithoutDirtyingOrAddingHistory()
    {
        var context = Context();
        var page = context.PageWorkspace!.Project.Scenes.Single();
        context.PageCommandRequest = new OpenPageRequest(page.PageKey);
        var registry = new CommandRegistry();
        registry.Register(new OpenPageCommand(new PageCommandCoordinator()));

        var result = await registry.ExecuteAsync("page.open", context);

        Assert.IsFalse(result.Changed);
        Assert.AreEqual(page.PageKey, result.PageToOpenKey);
        Assert.IsFalse(context.IsPageWorkspaceDirty);
        Assert.AreEqual(0, context.WorkspaceHistory!.UndoCount);
    }

    [TestMethod]
    public async Task PagePropertyCommandsUpdateReferenceAndSceneThroughOneCoordinator()
    {
        var context = Context();
        var page = context.PageWorkspace!.Project.Scenes.Single();
        var coordinator = new PageCommandCoordinator();
        var registry = new CommandRegistry();
        registry.Register(new SetPageCanvasCommand(coordinator));
        registry.Register(new SetPageBackgroundCommand(coordinator));
        context.ApplyPageWorkspaceMutation = _ => { };

        context.PageCommandRequest = new SetPageCanvasRequest(page.PageKey, new CanvasSize(1440, 900));
        var canvasResult = await registry.ExecuteAsync("page.set-canvas", context);
        context.PageCommandRequest = new SetPageBackgroundRequest(page.PageKey, new SceneBackgroundStyle("#123456", Size: "contain"));
        var backgroundResult = await registry.ExecuteAsync("page.set-background", context);

        Assert.AreEqual(CommandResultStatus.Succeeded, canvasResult.Status);
        Assert.AreEqual(CommandResultStatus.Succeeded, backgroundResult.Status);
        Assert.AreEqual(new CanvasSize(1440, 900), context.PageWorkspace.Project.Scenes.Single().EffectiveCanvasSize);
        Assert.AreEqual(new CanvasSize(1440, 900), context.PageWorkspace.Scenes[page.PageKey].CanvasSize);
        Assert.AreEqual("#123456", context.PageWorkspace.Project.Scenes.Single().EffectiveBackground.Color);
        Assert.AreEqual("contain", context.PageWorkspace.Scenes[page.PageKey].EffectiveBackground.Size);
        Assert.AreEqual(2, context.WorkspaceHistory!.UndoCount);
    }

    private static ApplicationContext Context()
    {
        var key = Guid.NewGuid();
        var page = new ScadaSceneReference("home", "Home", $"scenes/{key:N}.scene.json", PageKey: key, PageCode: "home");
        var scene = ScadaScene.CreateEmpty("home", "Home", CanvasSize.DefaultDesktop) with { PageKey = key, PageCode = "home" };
        var project = ScadaProject.CreateDefault("Pages") with { Scenes = [page] };
        return new ApplicationContext
        {
            CurrentProject = project,
            PageWorkspace = new PageWorkspaceSnapshot(1, project, new Dictionary<Guid, ScadaScene> { [key] = scene }, []),
            PageWorkspaceUi = new ProjectWorkspaceUiSnapshot([key], key, key, new Dictionary<Guid, EditorPageSelectionSnapshot>()),
            SelectedPageKey = key,
            ActiveEditorPageKey = key,
            WorkspaceHistory = new EditorHistoryService()
        };
    }
}
