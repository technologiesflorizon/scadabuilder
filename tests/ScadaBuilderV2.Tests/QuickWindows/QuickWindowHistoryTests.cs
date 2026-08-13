using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

[TestClass]
public sealed class QuickWindowHistoryTests
{
    [TestMethod]
    public async Task CallerDeletionUndoRestoresElementCommandInvocationBindingsSelectionAndDirtyState()
    {
        var member = PublicStringMember();
        var definition = Definition("motor", member);
        var caller = Caller("caller", "open");
        var page = Page("page", caller);
        var before = Snapshot(page, [definition]);
        var invocationService = new QuickWindowInvocationService();
        var invocationMutation = invocationService.Upsert(before, new UpsertQuickWindowInvocationRequest(
            page.PageKey,
            caller.Id,
            "open",
            definition.DefinitionKey,
            [QuickWindowBinding.FromLiteral(member.MemberKey, "M101")]));
        var invocation = invocationMutation.After.Project.EffectiveQuickWindowInvocations.Single();
        var deletion = invocationService.DeleteCaller(invocationMutation.After, page.PageKey, caller.Id);
        var beforeUi = Ui(page.PageKey, caller.Id, definition.DefinitionKey);
        var afterUi = Ui(page.PageKey, null, definition.DefinitionKey);
        var history = new EditorHistoryService();
        history.Push(deletion.ToHistoryAction(beforeUi, afterUi, beforeWasDirty: false));
        var workspace = new TestWorkspace(ToHistorySnapshot(deletion.After, afterUi, isDirty: true));

        Assert.IsTrue(await history.UndoAsync(workspace.CreateContext()));
        var restoredCaller = workspace.Scenes[page.PageKey].FindElementRecursive(caller.Id);
        Assert.IsNotNull(restoredCaller);
        Assert.AreEqual(invocation.InvocationKey, restoredCaller.EffectiveCommandConfig.Commands.Single().QuickWindowInvocationKey);
        var restoredInvocation = workspace.Project.EffectiveQuickWindowInvocations.Single();
        Assert.AreEqual("M101", restoredInvocation.Bindings.Single().LiteralValue);
        CollectionAssert.AreEqual(new[] { caller.Id }, workspace.Ui.PageSelections[page.PageKey].ElementIds.ToArray());
        Assert.AreEqual(definition.DefinitionKey, workspace.Ui.QuickWindowSelection?.ActiveDefinitionKey);
        Assert.IsFalse(workspace.IsDirty);

        Assert.IsTrue(await history.RedoAsync(workspace.CreateContext()));
        Assert.IsNull(workspace.Scenes[page.PageKey].FindElementRecursive(caller.Id));
        Assert.AreEqual(0, workspace.Project.EffectiveQuickWindowInvocations.Count);
        Assert.IsTrue(workspace.IsDirty);

        Assert.IsTrue(await history.UndoAsync(workspace.CreateContext()));
        Assert.IsNotNull(workspace.Scenes[page.PageKey].FindElementRecursive(caller.Id));
        Assert.AreEqual("M101", workspace.Project.EffectiveQuickWindowInvocations.Single().Bindings.Single().LiteralValue);
    }

    [TestMethod]
    public async Task DefinitionAndBindingChangeUndoRedoRestoresExactInvocation()
    {
        var member = PublicStringMember();
        var definitionA = Definition("motor_a", member);
        var definitionB = Definition("motor_b", member);
        var caller = Caller("caller", "open");
        var page = Page("page", caller);
        var snapshot = Snapshot(page, [definitionA, definitionB]);
        var service = new QuickWindowInvocationService();
        var created = service.Upsert(snapshot, new UpsertQuickWindowInvocationRequest(
            page.PageKey,
            caller.Id,
            "open",
            definitionA.DefinitionKey,
            [QuickWindowBinding.FromLiteral(member.MemberKey, "M101")]));
        var invocationKey = created.After.Project.EffectiveQuickWindowInvocations.Single().InvocationKey;
        var changed = service.Upsert(created.After, new UpsertQuickWindowInvocationRequest(
            page.PageKey,
            caller.Id,
            "open",
            definitionB.DefinitionKey,
            [QuickWindowBinding.FromLiteral(member.MemberKey, "M102")],
            invocationKey));
        var ui = Ui(page.PageKey, caller.Id, definitionB.DefinitionKey);
        var history = new EditorHistoryService();
        history.Push(changed.ToHistoryAction(ui, ui, beforeWasDirty: true));
        var workspace = new TestWorkspace(ToHistorySnapshot(changed.After, ui, isDirty: true));

        Assert.IsTrue(await history.UndoAsync(workspace.CreateContext()));
        var undone = workspace.Project.EffectiveQuickWindowInvocations.Single();
        Assert.AreEqual(definitionA.DefinitionKey, undone.DefinitionKey);
        Assert.AreEqual("M101", undone.Bindings.Single().LiteralValue);
        Assert.AreEqual(invocationKey, workspace.Scenes[page.PageKey].FindElementRecursive(caller.Id)!
            .EffectiveCommandConfig.Commands.Single().QuickWindowInvocationKey);

        Assert.IsTrue(await history.RedoAsync(workspace.CreateContext()));
        var redone = workspace.Project.EffectiveQuickWindowInvocations.Single();
        Assert.AreEqual(definitionB.DefinitionKey, redone.DefinitionKey);
        Assert.AreEqual("M102", redone.Bindings.Single().LiteralValue);
    }

    [TestMethod]
    public async Task TwoInvocationActionsRemainIndependentAcrossRepeatedUndoRedo()
    {
        var member = PublicStringMember();
        var definition = Definition("motor", member);
        var callerA = Caller("caller-a", "open-a");
        var callerB = Caller("caller-b", "open-b");
        var page = Page("page", callerA, callerB);
        var initial = Snapshot(page, [definition]);
        var service = new QuickWindowInvocationService();
        var first = service.Upsert(initial, new UpsertQuickWindowInvocationRequest(
            page.PageKey,
            callerA.Id,
            "open-a",
            definition.DefinitionKey,
            [QuickWindowBinding.FromLiteral(member.MemberKey, "M101")]));
        var second = service.Upsert(first.After, new UpsertQuickWindowInvocationRequest(
            page.PageKey,
            callerB.Id,
            "open-b",
            definition.DefinitionKey,
            [QuickWindowBinding.FromLiteral(member.MemberKey, "M102")]));
        var ui = Ui(page.PageKey, callerB.Id, definition.DefinitionKey);
        var history = new EditorHistoryService();
        history.Push(first.ToHistoryAction(ui, ui, beforeWasDirty: false));
        history.Push(second.ToHistoryAction(ui, ui, beforeWasDirty: true));
        var workspace = new TestWorkspace(ToHistorySnapshot(second.After, ui, isDirty: true));

        Assert.IsTrue(await history.UndoAsync(workspace.CreateContext()));
        CollectionAssert.AreEqual(new[] { "M101" }, BindingValues(workspace.Project));
        Assert.IsNull(workspace.Scenes[page.PageKey].FindElementRecursive(callerB.Id)!
            .EffectiveCommandConfig.Commands.Single().QuickWindowInvocationKey);

        Assert.IsTrue(await history.UndoAsync(workspace.CreateContext()));
        Assert.AreEqual(0, workspace.Project.EffectiveQuickWindowInvocations.Count);

        Assert.IsTrue(await history.RedoAsync(workspace.CreateContext()));
        CollectionAssert.AreEqual(new[] { "M101" }, BindingValues(workspace.Project));

        Assert.IsTrue(await history.RedoAsync(workspace.CreateContext()));
        CollectionAssert.AreEquivalent(new[] { "M101", "M102" }, BindingValues(workspace.Project));
        Assert.AreEqual(2, workspace.Project.EffectiveQuickWindowInvocations.Select(invocation => invocation.InvocationKey).Distinct().Count());
    }

    private static string[] BindingValues(ScadaProject project) => project.EffectiveQuickWindowInvocations
        .Select(invocation => invocation.Bindings.Single().LiteralValue!)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static ProjectWorkspaceHistorySnapshot ToHistorySnapshot(
        PageWorkspaceSnapshot snapshot,
        ProjectWorkspaceUiSnapshot ui,
        bool isDirty) => new(
            snapshot.Project,
            snapshot.Scenes,
            ui,
            isDirty,
            snapshot.PendingDeletions.Select(deletion => deletion.PageKey).ToArray());

    private static ProjectWorkspaceUiSnapshot Ui(Guid pageKey, string? selectedElementId, Guid definitionKey)
    {
        var pageSelections = selectedElementId is null
            ? new Dictionary<Guid, EditorPageSelectionSnapshot>()
            : new Dictionary<Guid, EditorPageSelectionSnapshot>
            {
                [pageKey] = new([selectedElementId], selectedElementId)
            };
        return new ProjectWorkspaceUiSnapshot(
            [pageKey],
            pageKey,
            pageKey,
            pageSelections,
            new QuickWindowEditorSelectionSnapshot(
                definitionKey,
                definitionKey,
                selectedElementId is null ? [] : [selectedElementId],
                selectedElementId));
    }

    private static PageWorkspaceSnapshot Snapshot(
        ScadaScene page,
        IReadOnlyList<QuickWindowDefinition> definitions)
    {
        var reference = new ScadaSceneReference(
            page.Id,
            page.Title,
            $"scenes/{page.PageKey:N}.scene.json",
            PageKey: page.PageKey,
            PageCode: page.EffectivePageCode);
        var project = ScadaProject.CreateDefault("History") with
        {
            ManifestVersion = "2.3",
            Scenes = [reference],
            QuickWindows = definitions,
            QuickWindowInvocations = []
        };
        return new PageWorkspaceSnapshot(1, project, new Dictionary<Guid, ScadaScene> { [page.PageKey] = page }, []);
    }

    private static ScadaScene Page(string code, params ScadaElement[] elements)
    {
        var page = ScadaScene.CreateEmpty(code, code, CanvasSize.DefaultDesktop) with
        {
            PageKey = Guid.NewGuid(),
            PageCode = code
        };
        return elements.Aggregate(page, (current, element) => current.WithElement(element));
    }

    private static ScadaElement Caller(string elementId, string commandId)
    {
        var command = new ScadaCommandBinding(
            commandId,
            commandId,
            true,
            ScadaCommandTrigger.OnClick,
            ScadaCommandKind.OpenQuickWindow);
        return ScadaElement.CreateButton(elementId, elementId, 10, 20, ScadaButtonKind.Command) with
        {
            CommandConfig = new ScadaElementCommandConfig([command])
        };
    }

    private static QuickWindowDefinition Definition(string code, QuickWindowInterfaceMember member) => new(
        Guid.NewGuid(),
        code,
        code,
        1,
        new VisualContent(new CanvasSize(480, 320)),
        [member],
        new QuickWindowPresentationDefaults());

    private static QuickWindowInterfaceMember PublicStringMember() => new(
        Guid.NewGuid(),
        "MotorName",
        QuickWindowInterfaceFamily.PublicParameter,
        QuickWindowDataType.String,
        QuickWindowMemberAccess.Read);

    private sealed class TestWorkspace(ProjectWorkspaceHistorySnapshot snapshot)
    {
        public ScadaProject Project { get; private set; } = snapshot.Project;

        public Dictionary<Guid, ScadaScene> Scenes { get; private set; } = snapshot.Scenes.ToDictionary(item => item.Key, item => item.Value);

        public ProjectWorkspaceUiSnapshot Ui { get; private set; } = snapshot.Ui;

        public bool IsDirty { get; private set; } = snapshot.IsDirty;

        public IReadOnlyList<Guid> PendingDeletedPageKeys { get; private set; } = snapshot.PendingDeletedPageKeys;

        public EditorHistoryContext CreateContext() => new()
        {
            ActiveSceneId = string.Empty,
            GetActiveScene = () => null,
            ReplaceActiveScene = _ => throw new AssertFailedException("Quick-window history must use an atomic project restore."),
            RestoreProjectWorkspaceSnapshot = Restore,
            MarkDirty = () => IsDirty = true,
            RefreshPreviewAsync = () => Task.CompletedTask,
            SetStatus = _ => { }
        };

        private void Restore(ProjectWorkspaceHistorySnapshot restored)
        {
            Project = restored.Project;
            Scenes = restored.Scenes.ToDictionary(item => item.Key, item => item.Value);
            Ui = restored.Ui;
            IsDirty = restored.IsDirty;
            PendingDeletedPageKeys = restored.PendingDeletedPageKeys.ToArray();
        }
    }
}
