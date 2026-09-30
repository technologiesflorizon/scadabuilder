using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.QuickWindows;
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

        public List<string> Statuses { get; } = [];

        public Task ActivateQuickWindowAsync(QuickWindowEditorContext context, QuickWindowDefinition definition) => Task.CompletedTask;

        public Task<string?> RequestQuickWindowNameAsync(string title, string proposedName) => Task.FromResult(NameToReturn);

        public Task<bool> ConfirmQuickWindowDeletionAsync(QuickWindowDefinition definition, IReadOnlyList<QuickWindowUsage> usages) =>
            Task.FromResult(true);

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
