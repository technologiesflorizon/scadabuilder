using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Pins the migration cleanup of controller fix round 1 on Task 10: editing an `OpenQuickWindow` command
/// from a legacy `QuickWindowInvocationKey` to a `ParameterSheetKey`/`QuickWindowPageKey` pair must retire
/// the now-orphaned invocation, as one atomic workspace transition.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, DEC-0052.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class QuickWindowInvocationMigrationTests
{
    private static readonly Guid SheetKey = Guid.NewGuid();
    private static readonly Guid QuickWindowPageKey = Guid.NewGuid();
    private static readonly Guid InvocationKey = Guid.NewGuid();

    // ---- QuickWindowInvocationMigration.InvocationKeyToRetire -------------------------------------------

    [TestMethod]
    public void InvocationKeyToRetireDetectsASheetPageMigrationFromALegacyInvocation()
    {
        var original = LegacyCommand();
        var migrated = MigratedCommand();

        Assert.AreEqual(InvocationKey, QuickWindowInvocationMigration.InvocationKeyToRetire(original, migrated));
    }

    [TestMethod]
    public void InvocationKeyToRetireIsNullWhenTheOriginalHadNoInvocationKey()
    {
        var original = LegacyCommand() with { QuickWindowInvocationKey = null };
        var migrated = MigratedCommand();

        Assert.IsNull(QuickWindowInvocationMigration.InvocationKeyToRetire(original, migrated));
    }

    [TestMethod]
    public void InvocationKeyToRetireIsNullWhenTheMigratedCommandIsMissingTheSheetOrThePage()
    {
        var original = LegacyCommand();

        Assert.IsNull(QuickWindowInvocationMigration.InvocationKeyToRetire(original, MigratedCommand() with { ParameterSheetKey = null }));
        Assert.IsNull(QuickWindowInvocationMigration.InvocationKeyToRetire(original, MigratedCommand() with { QuickWindowPageKey = null }));
    }

    [TestMethod]
    public void InvocationKeyToRetireIsNullWhenTheMigratedCommandStillCarriesAnInvocationKey()
    {
        // Re-saving a legacy command unchanged (QuickWindowConfigurationRules.IsUnmodifiedLegacyInvocation)
        // is not a migration: the invocation key survives on the saved command and must not be retired.
        var original = LegacyCommand();
        var unchanged = MigratedCommand() with { QuickWindowInvocationKey = InvocationKey, ParameterSheetKey = null, QuickWindowPageKey = null };

        Assert.IsNull(QuickWindowInvocationMigration.InvocationKeyToRetire(original, unchanged));
    }

    [TestMethod]
    public void InvocationKeyToRetireIsNullForNonQuickWindowCommands()
    {
        var original = LegacyCommand() with { Kind = ScadaCommandKind.WriteTag };
        var migrated = MigratedCommand();

        Assert.IsNull(QuickWindowInvocationMigration.InvocationKeyToRetire(original, migrated));
    }

    // ---- QuickWindowInvocationMigration.WithoutInvocation ------------------------------------------------

    [TestMethod]
    public void WithoutInvocationRemovesExactlyThatInvocation()
    {
        var kept = QuickWindowInvocation.CreateEmpty(Guid.NewGuid());
        var project = ScadaProject.CreateDefault("migration-tests") with
        {
            QuickWindowInvocations = [new QuickWindowInvocation(InvocationKey, Guid.NewGuid(), []), kept]
        };

        var result = QuickWindowInvocationMigration.WithoutInvocation(project, InvocationKey);

        Assert.AreEqual(1, result.EffectiveQuickWindowInvocations.Count);
        Assert.AreSame(kept, result.EffectiveQuickWindowInvocations.Single());
    }

    [TestMethod]
    public void WithoutInvocationIsANoOpWhenTheKeyIsAbsent()
    {
        var project = ScadaProject.CreateDefault("migration-tests") with
        {
            QuickWindowInvocations = [QuickWindowInvocation.CreateEmpty(Guid.NewGuid())]
        };

        var result = QuickWindowInvocationMigration.WithoutInvocation(project, Guid.NewGuid());

        Assert.AreSame(project, result);
    }

    // ---- QuickWindowInvocationService.RetireMigratedInvocation (one atomic transition) -------------------

    [TestMethod]
    public void RetireMigratedInvocationAppliesTheMigratedCommandAndDropsTheOrphanInProjectAndSceneTogether()
    {
        var caller = CreateCaller("caller", "open", InvocationKey);
        var page = CreatePage("page", caller);
        var definitionKey = Guid.NewGuid();
        var orphan = new QuickWindowInvocation(InvocationKey, definitionKey, []);
        var baseSnapshot = CreateSnapshot(page);
        var snapshot = baseSnapshot with { Project = baseSnapshot.Project with { QuickWindowInvocations = [orphan] } };
        var migrated = MigratedCommand();
        var service = new QuickWindowInvocationService();

        var mutation = service.RetireMigratedInvocation(snapshot, page.PageKey, caller.Id, "open", migrated, InvocationKey);

        Assert.AreEqual(CommandResultStatus.Succeeded, mutation.Result.Status);
        Assert.IsTrue(mutation.Result.Changed);
        Assert.AreEqual(InvocationKey, mutation.AffectedInvocationKey);
        Assert.IsFalse(mutation.After.Project.EffectiveQuickWindowInvocations.Any(item => item.InvocationKey == InvocationKey),
            "the orphaned invocation must be gone from the project");
        var savedCommand = mutation.After.Scenes[page.PageKey].FindElementRecursive(caller.Id)!.EffectiveCommandConfig.Commands.Single();
        Assert.AreEqual(SheetKey, savedCommand.ParameterSheetKey);
        Assert.AreEqual(QuickWindowPageKey, savedCommand.QuickWindowPageKey);
        Assert.IsNull(savedCommand.QuickWindowInvocationKey);

        // One atomic transition: Before still has the legacy command and the orphan invocation, so undo
        // (restoring Before) brings back both together.
        var beforeCommand = mutation.Before.Scenes[page.PageKey].FindElementRecursive(caller.Id)!.EffectiveCommandConfig.Commands.Single();
        Assert.AreEqual(InvocationKey, beforeCommand.QuickWindowInvocationKey);
        Assert.IsTrue(mutation.Before.Project.EffectiveQuickWindowInvocations.Any(item => item.InvocationKey == InvocationKey));
    }

    [TestMethod]
    public async Task RetiringAMigratedInvocationIsOneUndoableHistoryEntryThatRestoresInvocationAndCommandTogether()
    {
        // Same path as MainWindow.RetireMigratedQuickWindowInvocationAsync: the mutation goes through
        // QuickWindowWorkspaceMutation.ToHistoryAction onto the single project-scoped stack.
        var caller = CreateCaller("caller", "open", InvocationKey);
        var page = CreatePage("page", caller);
        var orphan = new QuickWindowInvocation(InvocationKey, Guid.NewGuid(), []);
        var baseSnapshot = CreateSnapshot(page);
        var snapshot = baseSnapshot with { Project = baseSnapshot.Project with { QuickWindowInvocations = [orphan] } };
        var mutation = new QuickWindowInvocationService().RetireMigratedInvocation(
            snapshot, page.PageKey, caller.Id, "open", MigratedCommand(), InvocationKey);
        Assert.AreEqual(CommandResultStatus.Succeeded, mutation.Result.Status);

        var ui = new ProjectWorkspaceUiSnapshot([], null, null, new Dictionary<Guid, EditorPageSelectionSnapshot>());
        var history = new EditorHistoryService();
        history.Push(mutation.ToHistoryAction(ui, ui, beforeWasDirty: false));
        Assert.AreEqual(1, history.UndoCount, "the migration is exactly one history entry");
        var workspace = new MigrationWorkspace(mutation.After.Project, mutation.After.Scenes);

        Assert.IsTrue(await history.UndoAsync(workspace.CreateContext()));
        Assert.IsTrue(workspace.Project.EffectiveQuickWindowInvocations.Any(item => item.InvocationKey == InvocationKey),
            "undo restores the retired invocation");
        var restored = workspace.Scenes[page.PageKey].FindElementRecursive(caller.Id)!.EffectiveCommandConfig.Commands.Single();
        Assert.AreEqual(InvocationKey, restored.QuickWindowInvocationKey, "undo restores the legacy caller command in the same step");
        Assert.IsNull(restored.ParameterSheetKey);
        Assert.IsFalse(workspace.IsDirty, "undo restores the dirty state the migration started from");

        Assert.IsTrue(await history.RedoAsync(workspace.CreateContext()));
        Assert.IsFalse(workspace.Project.EffectiveQuickWindowInvocations.Any(item => item.InvocationKey == InvocationKey));
        var redone = workspace.Scenes[page.PageKey].FindElementRecursive(caller.Id)!.EffectiveCommandConfig.Commands.Single();
        Assert.IsNull(redone.QuickWindowInvocationKey);
        Assert.AreEqual(SheetKey, redone.ParameterSheetKey);
        Assert.AreEqual(QuickWindowPageKey, redone.QuickWindowPageKey);
    }

    private sealed class MigrationWorkspace(ScadaProject project, IReadOnlyDictionary<Guid, ScadaScene> scenes)
    {
        public ScadaProject Project { get; private set; } = project;

        public Dictionary<Guid, ScadaScene> Scenes { get; private set; } = scenes.ToDictionary(item => item.Key, item => item.Value);

        public bool IsDirty { get; private set; } = true;

        public EditorHistoryContext CreateContext() => new()
        {
            ActiveSceneId = string.Empty,
            GetActiveScene = () => null,
            ReplaceActiveScene = _ => throw new AssertFailedException("The migration history must use an atomic project restore."),
            RestoreProjectWorkspaceSnapshot = restored =>
            {
                Project = restored.Project;
                Scenes = restored.Scenes.ToDictionary(item => item.Key, item => item.Value);
                IsDirty = restored.IsDirty;
            },
            MarkDirty = () => IsDirty = true,
            RefreshPreviewAsync = () => Task.CompletedTask,
            SetStatus = _ => { }
        };
    }

    [TestMethod]
    public void RetireMigratedInvocationBlocksOnAnUnknownOwnerCommand()
    {
        var caller = CreateCaller("caller", "open", InvocationKey);
        var page = CreatePage("page", caller);
        var snapshot = CreateSnapshot(page);
        var service = new QuickWindowInvocationService();

        var mutation = service.RetireMigratedInvocation(snapshot, page.PageKey, caller.Id, "not-the-command", MigratedCommand(), InvocationKey);

        Assert.AreEqual(CommandResultStatus.Blocked, mutation.Result.Status);
        Assert.AreSame(snapshot, mutation.After);
    }

    private static ScadaCommandBinding LegacyCommand() => new(
        "open", "Ouvrir", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
        QuickWindowInvocationKey: InvocationKey);

    private static ScadaCommandBinding MigratedCommand() => new(
        "open", "Ouvrir", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
        ParameterSheetKey: SheetKey, QuickWindowPageKey: QuickWindowPageKey);

    private static ScadaElement CreateCaller(string elementId, string commandId, Guid? invocationKey) =>
        ScadaElement.CreateButton(elementId, elementId, 10, 20, ScadaButtonKind.Command) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding(commandId, commandId, true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
                    QuickWindowInvocationKey: invocationKey)
            ])
        };

    private static ScadaScene CreatePage(string code, params ScadaElement[] elements)
    {
        var pageKey = Guid.NewGuid();
        return elements.Aggregate(
            ScadaScene.CreateEmpty(code, code, CanvasSize.DefaultDesktop) with { PageKey = pageKey, PageCode = code },
            (scene, element) => scene.WithElement(element));
    }

    private static PageWorkspaceSnapshot CreateSnapshot(params ScadaScene[] scenes)
    {
        var references = scenes.Select(scene => new ScadaSceneReference(
            scene.Id, scene.Title, $"scenes/{scene.PageKey:N}.scene.json",
            PageKey: scene.PageKey, PageCode: scene.EffectivePageCode)).ToArray();
        var project = ScadaProject.CreateDefault("QuickWindowInvocationMigrationTests") with
        {
            ManifestVersion = "2.3",
            Scenes = references
        };
        return new PageWorkspaceSnapshot(1, project, scenes.ToDictionary(scene => scene.PageKey), []);
    }
}
