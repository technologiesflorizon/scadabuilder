using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ModernProjects;

namespace ScadaBuilderV2.Tests;

/// <summary>
/// Round-trip and containment tests for <see cref="ParameterSheetStore"/> and its integration inside
/// <see cref="ModernProjectStore"/>, mirroring <c>QuickWindowStoreTests</c>.
/// </summary>
/// <remarks>
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/sdd/2026-09-30-quick-window-parameter-sheets/task-4-brief.md.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class ParameterSheetStoreTests
{
    [TestMethod]
    public void TheRelativePathIsUnderParameterSheets()
    {
        var key = Guid.Parse("11111111-2222-3333-4444-555555555555");

        Assert.AreEqual("parameter-sheets/11111111222233334444555555555555.parameter-sheet.json", ParameterSheetStore.GetRelativePath(key));
    }

    [TestMethod]
    public void APathOutsideTheFolderIsRefused()
    {
        Assert.ThrowsException<InvalidOperationException>(() =>
            ParameterSheetStore.ResolveContainedParameterSheetPath(Path.GetTempPath(), "../evil.parameter-sheet.json"));
    }

    [TestMethod]
    public async Task ASavedProjectReloadsItsSheetsUnchanged()
    {
        var root = Path.Combine(Path.GetTempPath(), "psheet-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var sheet = new ParameterSheet(Guid.NewGuid(), "ventilateur_3", "Ventilateur 3", "#1 := {FAN_03_RUN}\r\n// fin\r\n");
            var project = ScadaProject.CreateDefault("p") with { ParameterSheets = [sheet] };
            var store = new ModernProjectStore();
            var projectRoot = Path.Combine(root, "proj");
            var snapshot = new PageWorkspaceSnapshot(1, project, new Dictionary<Guid, ScadaScene>(), Array.Empty<PendingPageDeletion>());

            await store.SaveWorkspaceSnapshotToProjectRootAsync(projectRoot, snapshot);
            var reloaded = await store.LoadProjectFromRootAsync(projectRoot);

            Assert.AreEqual(sheet, reloaded!.EffectiveParameterSheets.Single());
            Assert.IsTrue(File.Exists(Path.Combine(projectRoot, "parameter-sheets", $"{sheet.SheetKey:N}.parameter-sheet.json")));
            var projectJson = await File.ReadAllTextAsync(Path.Combine(projectRoot, "project.json"));
            Assert.IsFalse(projectJson.Contains("\"ParameterSheets\"", StringComparison.Ordinal), "project.json must not carry a ParameterSheets key; the dedicated files are authoritative.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task ARemovedSheetFileIsDeletedOnSave()
    {
        var root = Path.Combine(Path.GetTempPath(), "psheet-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var sheet = new ParameterSheet(Guid.NewGuid(), "a", "A", "#1 := 1");
            var store = new ModernProjectStore();
            var projectRoot = Path.Combine(root, "proj");
            var projectWithSheet = ScadaProject.CreateDefault("p") with { ParameterSheets = [sheet] };
            var snapshotWithSheet = new PageWorkspaceSnapshot(1, projectWithSheet, new Dictionary<Guid, ScadaScene>(), Array.Empty<PendingPageDeletion>());
            await store.SaveWorkspaceSnapshotToProjectRootAsync(projectRoot, snapshotWithSheet);

            var projectWithoutSheet = ScadaProject.CreateDefault("p");
            var snapshotWithoutSheet = new PageWorkspaceSnapshot(2, projectWithoutSheet, new Dictionary<Guid, ScadaScene>(), Array.Empty<PendingPageDeletion>());
            await store.SaveWorkspaceSnapshotToProjectRootAsync(projectRoot, snapshotWithoutSheet);

            Assert.IsFalse(File.Exists(Path.Combine(projectRoot, "parameter-sheets", $"{sheet.SheetKey:N}.parameter-sheet.json")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
