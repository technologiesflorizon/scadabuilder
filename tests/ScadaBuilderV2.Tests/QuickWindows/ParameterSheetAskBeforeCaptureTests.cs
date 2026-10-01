using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Locks the "ask first" order of the parameter-sheet commands of the "Fenêtres rapides" panel: the name
/// dialog is shown before the workspace snapshot (a full read of every non-open scene) is captured, a
/// cancelled dialog captures nothing, and the operation stays one undoable transition whose Before is the
/// snapshot captured after the dialog.
/// </summary>
/// <remarks>
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class ParameterSheetAskBeforeCaptureTests
{
    [TestMethod]
    public async Task CreateSheetAsksForTheNameBeforeCapturingTheSnapshot()
    {
        var journal = new List<string>();
        var controller = new QuickWindowWorkspaceController(new OrderedHost(journal, "Ventilateur 3"));
        var snapshot = CreateSnapshot();

        var mutation = await controller.CreateSheetAsync(snapshot.Project, Capture(journal, snapshot));

        CollectionAssert.AreEqual(new[] { "ask", "capture" }, journal);
        Assert.IsNotNull(mutation);
        Assert.AreEqual(CommandResultStatus.Succeeded, mutation!.Result.Status);
        Assert.AreSame(snapshot, mutation.Before, "the undo target must be the snapshot captured after the dialog");
        Assert.AreEqual("ventilateur_3", mutation.After.Project.EffectiveParameterSheets.Single().Code);
    }

    [TestMethod]
    public async Task CancelledCreateSheetDialogCapturesNothing()
    {
        var journal = new List<string>();
        var controller = new QuickWindowWorkspaceController(new OrderedHost(journal, null));
        var snapshot = CreateSnapshot();

        var mutation = await controller.CreateSheetAsync(snapshot.Project, Capture(journal, snapshot));

        Assert.IsNull(mutation);
        CollectionAssert.AreEqual(new[] { "ask" }, journal, "a cancelled dialog must not read the workspace");
    }

    [TestMethod]
    public async Task RenameAndDuplicateSheetAskForTheNameBeforeCapturingTheSnapshot()
    {
        var setupJournal = new List<string>();
        var setup = new QuickWindowWorkspaceController(new OrderedHost(setupJournal, "Ventilateur 3"));
        var created = await setup.CreateSheetAsync(CreateSnapshot());
        var withSheet = created!.After;
        var sheetKey = withSheet.Project.EffectiveParameterSheets.Single().SheetKey;

        var renameJournal = new List<string>();
        var renamer = new QuickWindowWorkspaceController(new OrderedHost(renameJournal, "Ventilateur 3 Halle B"));
        var renamed = await renamer.RenameSheetAsync(withSheet.Project, sheetKey, Capture(renameJournal, withSheet));

        var duplicateJournal = new List<string>();
        var duplicator = new QuickWindowWorkspaceController(new OrderedHost(duplicateJournal, "Ventilateur 4"));
        var duplicated = await duplicator.DuplicateSheetAsync(withSheet.Project, sheetKey, Capture(duplicateJournal, withSheet));

        CollectionAssert.AreEqual(new[] { "ask", "capture" }, renameJournal);
        CollectionAssert.AreEqual(new[] { "ask", "capture" }, duplicateJournal);
        Assert.AreEqual("Ventilateur 3 Halle B", renamed!.After.Project.EffectiveParameterSheets.Single().DisplayName);
        Assert.AreEqual(2, duplicated!.After.Project.EffectiveParameterSheets.Count);
        Assert.AreSame(withSheet, renamed.Before);
        Assert.AreSame(withSheet, duplicated.Before);
    }

    [TestMethod]
    public async Task CancelledRenameAndDuplicateDialogsCaptureNothing()
    {
        var setup = new QuickWindowWorkspaceController(new OrderedHost([], "Ventilateur 3"));
        var withSheet = (await setup.CreateSheetAsync(CreateSnapshot()))!.After;
        var sheetKey = withSheet.Project.EffectiveParameterSheets.Single().SheetKey;
        var journal = new List<string>();
        var controller = new QuickWindowWorkspaceController(new OrderedHost(journal, null));

        Assert.IsNull(await controller.RenameSheetAsync(withSheet.Project, sheetKey, Capture(journal, withSheet)));
        Assert.IsNull(await controller.DuplicateSheetAsync(withSheet.Project, sheetKey, Capture(journal, withSheet)));

        CollectionAssert.AreEqual(new[] { "ask", "ask" }, journal);
    }

    [TestMethod]
    public void SheetDispatcherNeverCapturesTheSnapshotBeforeTheNameDialog()
    {
        var source = ReadAppFile("MainWindow.QuickWindows.cs").ReplaceLineEndings("\n");
        var start = source.IndexOf("private async Task ExecuteParameterSheetCommandAsync(string commandId)", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "the parameter-sheet dispatcher moved");
        var end = source.IndexOf("/// <summary>", start, StringComparison.Ordinal);
        var body = source[start..end];

        Assert.IsFalse(
            body.Contains("var snapshot = await _pageWorkspaceController.CaptureSnapshotAsync();", StringComparison.Ordinal),
            "the dispatcher must not capture the workspace before the name dialog of new/rename/duplicate");
        StringAssert.Contains(body, "QuickWindowWorkspace.CreateSheetAsync(currentProject, captureSnapshot)");
        StringAssert.Contains(body, "QuickWindowWorkspace.RenameSheetAsync(currentProject, key, captureSnapshot)");
        StringAssert.Contains(body, "QuickWindowWorkspace.DuplicateSheetAsync(currentProject, key, captureSnapshot)");
        Assert.IsFalse(
            body.Contains("OpenParameterSheetEditorAsync(await _pageWorkspaceController.CaptureSnapshotAsync()", StringComparison.Ordinal),
            "opening the editor after a creation must reuse the mutation's After snapshot, not capture again");
        StringAssert.Contains(body, "OpenParameterSheetEditorAsync(mutation.After.Project, createdKey)");
    }

    private static Func<Task<PageWorkspaceSnapshot>> Capture(List<string> journal, PageWorkspaceSnapshot snapshot) => () =>
    {
        journal.Add("capture");
        return Task.FromResult(snapshot);
    };

    private static PageWorkspaceSnapshot CreateSnapshot()
    {
        var project = ScadaProject.CreateDefault("ParameterSheetAskBeforeCaptureTests") with { ManifestVersion = "2.3" };
        return new PageWorkspaceSnapshot(1, project, new Dictionary<Guid, ScadaScene>(), []);
    }

    private static string ReadAppFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "ScadaBuilderV2.App", relativePath);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate src/ScadaBuilderV2.App/{relativePath}.");
        return string.Empty;
    }

    private sealed class OrderedHost(List<string> journal, string? name) : IQuickWindowWorkspaceHost
    {
        public Task ActivateQuickWindowAsync(QuickWindowEditorContext context, QuickWindowDefinition definition) => Task.CompletedTask;

        public Task<string?> RequestQuickWindowNameAsync(string title, string proposedName)
        {
            journal.Add("ask");
            return Task.FromResult(name);
        }

        public Task<bool> ConfirmQuickWindowDeletionAsync(QuickWindowDefinition definition, IReadOnlyList<QuickWindowUsage> usages) =>
            Task.FromResult(true);

        public Task<bool> ConfirmParameterSheetDeletionAsync(ParameterSheet sheet, IReadOnlyList<ParameterSheetUsage> usages) =>
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

        public void ReportQuickWindowStatus(string message)
        {
        }
    }
}
