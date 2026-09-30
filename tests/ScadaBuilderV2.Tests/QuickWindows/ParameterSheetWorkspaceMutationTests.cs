using ScadaBuilderV2.App.QuickWindows;
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
/// Locks the parameter-sheet workspace mutations wired to the "Fenêtres rapides" panel: create/rename/
/// duplicate/delete/edit are prepared as a single undoable <see cref="QuickWindowWorkspaceMutation"/>,
/// exactly like the existing quick-window front door (DEC-0052).
/// </summary>
/// <remarks>
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class ParameterSheetWorkspaceMutationTests
{
    [TestMethod]
    public async Task CreateSheetAsyncWithAnAccentedSpacedNameSucceedsAndDerivesTheCode()
    {
        var host = new RecordingHost { NameToReturn = "Ventilateur 3" };
        var controller = new QuickWindowWorkspaceController(host);

        var mutation = await controller.CreateSheetAsync(CreateSnapshot());

        Assert.IsNotNull(mutation);
        Assert.AreEqual(CommandResultStatus.Succeeded, mutation!.Result.Status);
        var sheet = mutation.After.Project.EffectiveParameterSheets.Single();
        Assert.AreEqual("Ventilateur 3", sheet.DisplayName);
        Assert.AreEqual("ventilateur_3", sheet.Code);
        Assert.AreEqual(string.Empty, sheet.Text);
    }

    [TestMethod]
    public async Task RenameSheetAsyncChangesOnlyTheDisplayNameAndKeepsTheCode()
    {
        var host = new RecordingHost { NameToReturn = "Ventilateur 3" };
        var controller = new QuickWindowWorkspaceController(host);
        var created = await controller.CreateSheetAsync(CreateSnapshot());
        var sheetKey = created!.After.Project.EffectiveParameterSheets.Single().SheetKey;
        var codeBeforeRename = created.After.Project.EffectiveParameterSheets.Single().Code;

        host.NameToReturn = "Ventilateur 3 Halle B";
        var renamed = await controller.RenameSheetAsync(created.After, sheetKey);

        Assert.IsNotNull(renamed);
        var sheet = renamed!.After.Project.EffectiveParameterSheets.Single();
        Assert.AreEqual("Ventilateur 3 Halle B", sheet.DisplayName);
        Assert.AreEqual(codeBeforeRename, sheet.Code);
    }

    [TestMethod]
    public async Task DuplicateSheetAsyncCopiesTheTextIntoAnIndependentSheet()
    {
        var host = new RecordingHost { NameToReturn = "Ventilateur 3" };
        var controller = new QuickWindowWorkspaceController(host);
        var created = await controller.CreateSheetAsync(CreateSnapshot());
        var sheetKey = created!.After.Project.EffectiveParameterSheets.Single().SheetKey;
        var withText = await controller.UpdateSheetTextAsync(created.After, sheetKey, "#1 := \"Ventilateur 3\"");

        host.NameToReturn = "Ventilateur 4";
        var duplicated = await controller.DuplicateSheetAsync(withText!.After, sheetKey);

        Assert.IsNotNull(duplicated);
        var sheets = duplicated!.After.Project.EffectiveParameterSheets;
        Assert.AreEqual(2, sheets.Count);
        var copy = sheets.Single(sheet => sheet.SheetKey != sheetKey);
        Assert.AreEqual("Ventilateur 4", copy.DisplayName);
        Assert.AreEqual("#1 := \"Ventilateur 3\"", copy.Text);
        Assert.AreNotEqual(sheetKey, copy.SheetKey);
    }

    [TestMethod]
    public async Task DeleteSheetAsyncRemovesTheSheet()
    {
        var host = new RecordingHost { NameToReturn = "Ventilateur 3" };
        var controller = new QuickWindowWorkspaceController(host);
        var created = await controller.CreateSheetAsync(CreateSnapshot());
        var sheetKey = created!.After.Project.EffectiveParameterSheets.Single().SheetKey;

        var deleted = await controller.DeleteSheetAsync(created.After, sheetKey);

        Assert.IsNotNull(deleted);
        Assert.AreEqual(0, deleted!.After.Project.EffectiveParameterSheets.Count);
    }

    [TestMethod]
    public async Task DeleteSheetAsyncWithoutUsagesDoesNotAskForConfirmation()
    {
        var host = new RecordingHost { NameToReturn = "Ventilateur 3" };
        var controller = new QuickWindowWorkspaceController(host);
        var created = await controller.CreateSheetAsync(CreateSnapshot());
        var sheetKey = created!.After.Project.EffectiveParameterSheets.Single().SheetKey;

        var deleted = await controller.DeleteSheetAsync(created.After, sheetKey);

        Assert.IsNotNull(deleted);
        Assert.IsFalse(host.WasAskedToConfirmSheetDeletion, "a sheet with no caller must delete without asking");
    }

    [TestMethod]
    public async Task DeleteSheetAsyncWithUsagesAndAHostThatRefusesLeavesTheProjectUnchangedWithNoHistoryEntry()
    {
        var host = new RecordingHost { NameToReturn = "Ventilateur 3", ConfirmSheetDeletion = false };
        var controller = new QuickWindowWorkspaceController(host);
        var created = await controller.CreateSheetAsync(CreateSnapshot());
        var sheetKey = created!.After.Project.EffectiveParameterSheets.Single().SheetKey;
        var snapshotWithCaller = AddCallerCommand(created.After, sheetKey, "win00001", "top", "open-top", "Ouvrir");

        var deleted = await controller.DeleteSheetAsync(snapshotWithCaller, sheetKey);

        Assert.IsNull(deleted, "a refused confirmation must produce no mutation, hence no history entry");
        Assert.IsTrue(host.WasAskedToConfirmSheetDeletion);
        Assert.AreEqual(1, host.LastSheetDeletionUsages!.Count);
        Assert.AreEqual("win00001", host.LastSheetDeletionUsages[0].PageCode);
        Assert.AreEqual(1, snapshotWithCaller.Project.EffectiveParameterSheets.Count, "the sheet must still exist: nothing was applied");
    }

    [TestMethod]
    public async Task DeleteSheetAsyncWithUsagesAndAHostThatAcceptsDeletesAndIsUndoable()
    {
        var host = new RecordingHost { NameToReturn = "Ventilateur 3", ConfirmSheetDeletion = true };
        var controller = new QuickWindowWorkspaceController(host);
        var created = await controller.CreateSheetAsync(CreateSnapshot());
        var sheetKey = created!.After.Project.EffectiveParameterSheets.Single().SheetKey;
        var snapshotWithCaller = AddCallerCommand(created.After, sheetKey, "win00001", "top", "open-top", "Ouvrir");

        var mutation = await controller.DeleteSheetAsync(snapshotWithCaller, sheetKey);

        Assert.IsNotNull(mutation);
        Assert.IsTrue(host.WasAskedToConfirmSheetDeletion);
        Assert.AreEqual(0, mutation!.After.Project.EffectiveParameterSheets.Count);

        var ui = Ui();
        var history = new EditorHistoryService();
        history.Push(mutation.ToHistoryAction(ui, ui, beforeWasDirty: false));
        var workspace = new TestWorkspace(ToHistorySnapshot(mutation.After, ui, isDirty: true));

        Assert.IsTrue(await history.UndoAsync(workspace.CreateContext()));
        Assert.AreEqual(1, workspace.Project.EffectiveParameterSheets.Count, "undo must restore the deleted sheet");

        Assert.IsTrue(await history.RedoAsync(workspace.CreateContext()));
        Assert.AreEqual(0, workspace.Project.EffectiveParameterSheets.Count);
    }

    [TestMethod]
    public async Task CreateSheetAsyncThenHistoryUndoRemovesTheSheetAndRedoRecreatesIt()
    {
        var host = new RecordingHost { NameToReturn = "Ventilateur 3" };
        var controller = new QuickWindowWorkspaceController(host);
        var snapshot = CreateSnapshot();

        var mutation = await controller.CreateSheetAsync(snapshot);
        Assert.IsNotNull(mutation);

        var ui = Ui();
        var history = new EditorHistoryService();
        history.Push(mutation!.ToHistoryAction(ui, ui, beforeWasDirty: false));
        var workspace = new TestWorkspace(ToHistorySnapshot(mutation.After, ui, isDirty: true));

        Assert.AreEqual(1, workspace.Project.EffectiveParameterSheets.Count);

        Assert.IsTrue(await history.UndoAsync(workspace.CreateContext()));
        Assert.AreEqual(0, workspace.Project.EffectiveParameterSheets.Count);

        Assert.IsTrue(await history.RedoAsync(workspace.CreateContext()));
        Assert.AreEqual(1, workspace.Project.EffectiveParameterSheets.Count);
        Assert.AreEqual("Ventilateur 3", workspace.Project.EffectiveParameterSheets.Single().DisplayName);
    }

    [TestMethod]
    public async Task RenameSheetAsyncThenHistoryUndoRestoresThePreviousName()
    {
        var host = new RecordingHost { NameToReturn = "Ventilateur 3" };
        var controller = new QuickWindowWorkspaceController(host);
        var created = await controller.CreateSheetAsync(CreateSnapshot());
        var sheetKey = created!.After.Project.EffectiveParameterSheets.Single().SheetKey;

        host.NameToReturn = "Ventilateur 3 Halle B";
        var mutation = await controller.RenameSheetAsync(created.After, sheetKey);
        Assert.IsNotNull(mutation);

        var ui = Ui();
        var history = new EditorHistoryService();
        history.Push(mutation!.ToHistoryAction(ui, ui, beforeWasDirty: false));
        var workspace = new TestWorkspace(ToHistorySnapshot(mutation.After, ui, isDirty: true));

        Assert.AreEqual("Ventilateur 3 Halle B", workspace.Project.EffectiveParameterSheets.Single().DisplayName);

        Assert.IsTrue(await history.UndoAsync(workspace.CreateContext()));
        Assert.AreEqual("Ventilateur 3", workspace.Project.EffectiveParameterSheets.Single().DisplayName);

        Assert.IsTrue(await history.RedoAsync(workspace.CreateContext()));
        Assert.AreEqual("Ventilateur 3 Halle B", workspace.Project.EffectiveParameterSheets.Single().DisplayName);
    }

    [TestMethod]
    public async Task DuplicateSheetAsyncThenHistoryUndoRemovesTheCopyAndRedoRecreatesIt()
    {
        var host = new RecordingHost { NameToReturn = "Ventilateur 3" };
        var controller = new QuickWindowWorkspaceController(host);
        var created = await controller.CreateSheetAsync(CreateSnapshot());
        var sheetKey = created!.After.Project.EffectiveParameterSheets.Single().SheetKey;

        host.NameToReturn = "Ventilateur 4";
        var mutation = await controller.DuplicateSheetAsync(created.After, sheetKey);
        Assert.IsNotNull(mutation);

        var ui = Ui();
        var history = new EditorHistoryService();
        history.Push(mutation!.ToHistoryAction(ui, ui, beforeWasDirty: false));
        var workspace = new TestWorkspace(ToHistorySnapshot(mutation.After, ui, isDirty: true));

        Assert.AreEqual(2, workspace.Project.EffectiveParameterSheets.Count);

        Assert.IsTrue(await history.UndoAsync(workspace.CreateContext()));
        Assert.AreEqual(1, workspace.Project.EffectiveParameterSheets.Count);
        Assert.AreEqual(sheetKey, workspace.Project.EffectiveParameterSheets.Single().SheetKey);

        Assert.IsTrue(await history.RedoAsync(workspace.CreateContext()));
        Assert.AreEqual(2, workspace.Project.EffectiveParameterSheets.Count);
    }

    [TestMethod]
    public async Task UpdateSheetTextAsyncThenHistoryUndoRestoresThePreviousText()
    {
        var host = new RecordingHost { NameToReturn = "Ventilateur 3" };
        var controller = new QuickWindowWorkspaceController(host);
        var created = await controller.CreateSheetAsync(CreateSnapshot());
        var sheetKey = created!.After.Project.EffectiveParameterSheets.Single().SheetKey;

        var mutation = await controller.UpdateSheetTextAsync(created.After, sheetKey, "#1 := {FAN_03_RUN}");
        Assert.IsNotNull(mutation);
        Assert.AreEqual(CommandResultStatus.Succeeded, mutation!.Result.Status);

        var ui = Ui();
        var history = new EditorHistoryService();
        history.Push(mutation.ToHistoryAction(ui, ui, beforeWasDirty: false));
        var workspace = new TestWorkspace(ToHistorySnapshot(mutation.After, ui, isDirty: true));

        Assert.AreEqual("#1 := {FAN_03_RUN}", workspace.Project.EffectiveParameterSheets.Single().Text);

        Assert.IsTrue(await history.UndoAsync(workspace.CreateContext()));
        Assert.AreEqual(string.Empty, workspace.Project.EffectiveParameterSheets.Single().Text);

        Assert.IsTrue(await history.RedoAsync(workspace.CreateContext()));
        Assert.AreEqual("#1 := {FAN_03_RUN}", workspace.Project.EffectiveParameterSheets.Single().Text);
    }

    private static ProjectWorkspaceUiSnapshot Ui() => new([], null, null, new Dictionary<Guid, EditorPageSelectionSnapshot>());

    private static ProjectWorkspaceHistorySnapshot ToHistorySnapshot(
        PageWorkspaceSnapshot snapshot,
        ProjectWorkspaceUiSnapshot ui,
        bool isDirty) => new(
            snapshot.Project,
            snapshot.Scenes,
            ui,
            isDirty,
            snapshot.PendingDeletions.Select(deletion => deletion.PageKey).ToArray());

    private static PageWorkspaceSnapshot CreateSnapshot()
    {
        var project = ScadaProject.CreateDefault("ParameterSheetWorkspaceMutationTests") with
        {
            ManifestVersion = "2.3"
        };
        return new PageWorkspaceSnapshot(1, project, new Dictionary<Guid, ScadaScene>(), []);
    }

    /// <summary>Adds one page with a single element whose `OpenQuickWindow` command references <paramref name="sheetKey"/>.</summary>
    private static PageWorkspaceSnapshot AddCallerCommand(
        PageWorkspaceSnapshot snapshot,
        Guid sheetKey,
        string pageCode,
        string elementId,
        string commandId,
        string commandName)
    {
        var command = new ScadaCommandBinding(
            commandId, commandName, true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
            ParameterSheetKey: sheetKey);
        var element = ScadaElement.CreateButton(elementId, elementId, 0, 0, ScadaButtonKind.Command) with
        {
            CommandConfig = new ScadaElementCommandConfig([command])
        };
        var page = (ScadaScene.CreateEmpty(pageCode, pageCode, CanvasSize.DefaultDesktop) with
        {
            PageKey = Guid.NewGuid(),
            PageCode = pageCode
        }).WithElement(element);
        var reference = new ScadaSceneReference(
            page.Id, page.Title, $"scenes/{page.PageKey:N}.scene.json", PageKey: page.PageKey, PageCode: page.EffectivePageCode);

        return snapshot with
        {
            Project = snapshot.Project with { Scenes = snapshot.Project.Scenes.Append(reference).ToArray() },
            Scenes = new Dictionary<Guid, ScadaScene>(snapshot.Scenes) { [page.PageKey] = page }
        };
    }

    private sealed class TestWorkspace(ProjectWorkspaceHistorySnapshot snapshot)
    {
        public ScadaProject Project { get; private set; } = snapshot.Project;

        private Dictionary<Guid, ScadaScene> Scenes { get; set; } = snapshot.Scenes.ToDictionary(item => item.Key, item => item.Value);

        private bool IsDirty { get; set; } = snapshot.IsDirty;

        private IReadOnlyList<Guid> PendingDeletedPageKeys { get; set; } = snapshot.PendingDeletedPageKeys;

        public EditorHistoryContext CreateContext() => new()
        {
            ActiveSceneId = string.Empty,
            GetActiveScene = () => null,
            ReplaceActiveScene = _ => throw new AssertFailedException("Parameter-sheet history must use an atomic project restore."),
            RestoreProjectWorkspaceSnapshot = Restore,
            MarkDirty = () => IsDirty = true,
            RefreshPreviewAsync = () => Task.CompletedTask,
            SetStatus = _ => { }
        };

        private void Restore(ProjectWorkspaceHistorySnapshot restored)
        {
            Project = restored.Project;
            Scenes = restored.Scenes.ToDictionary(item => item.Key, item => item.Value);
            IsDirty = restored.IsDirty;
            PendingDeletedPageKeys = restored.PendingDeletedPageKeys.ToArray();
        }
    }

    private sealed class RecordingHost : IQuickWindowWorkspaceHost
    {
        public string? NameToReturn { get; set; }

        public bool ConfirmSheetDeletion { get; set; } = true;

        public bool WasAskedToConfirmSheetDeletion { get; private set; }

        public IReadOnlyList<ParameterSheetUsage>? LastSheetDeletionUsages { get; private set; }

        public List<string> Statuses { get; } = [];

        public Task ActivateQuickWindowAsync(QuickWindowEditorContext context, QuickWindowDefinition definition) => Task.CompletedTask;

        public Task<string?> RequestQuickWindowNameAsync(string title, string proposedName) => Task.FromResult(NameToReturn);

        public Task<bool> ConfirmQuickWindowDeletionAsync(QuickWindowDefinition definition, IReadOnlyList<QuickWindowUsage> usages) =>
            Task.FromResult(true);

        public Task<bool> ConfirmParameterSheetDeletionAsync(ParameterSheet sheet, IReadOnlyList<ParameterSheetUsage> usages)
        {
            WasAskedToConfirmSheetDeletion = true;
            LastSheetDeletionUsages = usages;
            return Task.FromResult(ConfirmSheetDeletion);
        }

        public Task<QuickWindowInterfaceMember?> RequestInterfaceMemberAsync(
            string title,
            QuickWindowInterfaceMemberDraft draft,
            IReadOnlyList<QuickWindowInterfaceMember> siblings) => Task.FromResult<QuickWindowInterfaceMember?>(null);

        public Task<bool> ConfirmInterfaceMemberDeletionAsync(
            QuickWindowInterfaceMember member,
            IReadOnlyList<QuickWindowUsage> usages) => Task.FromResult(false);

        public Task<QuickWindowPasteDecision> ResolveQuickWindowPasteAsync(QuickWindowClipboardAnalysis analysis) =>
            Task.FromResult(QuickWindowPasteDecision.Cancel);

        public Task<bool> ConfirmInterfaceVersionImpactAsync(
            QuickWindowDefinition definition,
            IReadOnlyList<QuickWindowOutdatedInvocation> impacted) => Task.FromResult(true);

        public void ReportQuickWindowStatus(string message) => Statuses.Add(message);
    }
}
