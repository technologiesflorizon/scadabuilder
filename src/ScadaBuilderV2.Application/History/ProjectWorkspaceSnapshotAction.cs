using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.History;

/// <summary>Element selection retained for one page editor tab.</summary>
public sealed record EditorPageSelectionSnapshot(
    IReadOnlyList<string> ElementIds,
    string? PrimaryElementId = null);

/// <summary>Restorable project-tree and editor-tab state.</summary>
public sealed record ProjectWorkspaceUiSnapshot(
    IReadOnlyList<Guid> OpenPageKeys,
    Guid? SelectedPageKey,
    Guid? ActivePageKey,
    IReadOnlyDictionary<Guid, EditorPageSelectionSnapshot> PageSelections);

/// <summary>Immutable in-memory state used by project-scoped undo/redo.</summary>
/// <remarks>No filesystem, WPF or WebView state is captured.</remarks>
public sealed record ProjectWorkspaceHistorySnapshot(
    ScadaProject Project,
    IReadOnlyDictionary<Guid, ScadaScene> Scenes,
    ProjectWorkspaceUiSnapshot Ui,
    bool IsDirty,
    IReadOnlyList<Guid> PendingDeletedPageKeys);

/// <summary>Reversible project mutation that restores complete before/after workspace snapshots.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/04_editor/STATE_MANAGEMENT_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/ProjectWorkspaceHistoryTests.cs.
/// </remarks>
public sealed record ProjectWorkspaceSnapshotAction(
    ProjectWorkspaceHistorySnapshot Before,
    ProjectWorkspaceHistorySnapshot After,
    string Label) : IEditorHistoryAction
{
    /// <inheritdoc />
    public EditorHistoryTarget Target => EditorHistoryTarget.Project;

    /// <inheritdoc />
    public bool CanMergeWith(IEditorHistoryAction next) => false;

    /// <inheritdoc />
    public IEditorHistoryAction MergeWith(IEditorHistoryAction next)
    {
        throw new InvalidOperationException("Project workspace snapshot actions do not support merge.");
    }

    /// <inheritdoc />
    public Task UndoAsync(EditorHistoryContext context) =>
        ApplyAsync(context, Before, $"Undo {Label}.");

    /// <inheritdoc />
    public Task RedoAsync(EditorHistoryContext context) =>
        ApplyAsync(context, After, $"Redo {Label}.");

    private static async Task ApplyAsync(
        EditorHistoryContext context,
        ProjectWorkspaceHistorySnapshot snapshot,
        string status)
    {
        if (!context.CanResolve(EditorHistoryTarget.Project))
        {
            throw new InvalidOperationException("The history context cannot restore a project workspace snapshot.");
        }

        var snapshotKeys = snapshot.Scenes.Keys.ToHashSet();
        foreach (var pageKey in context.GetWorkspaceSceneKeys!().Where(key => !snapshotKeys.Contains(key)).ToArray())
        {
            context.RemoveSceneByPageKey!(pageKey);
        }

        foreach (var (pageKey, scene) in snapshot.Scenes)
        {
            context.ReplaceSceneByPageKey!(pageKey, scene);
        }

        context.ReplaceProject!(snapshot.Project);
        context.SetPendingDeletedPageKeys!(snapshot.PendingDeletedPageKeys);
        context.RestoreWorkspaceUi!(snapshot.Ui);
        context.SetWorkspaceDirty!(snapshot.IsDirty);
        await context.RefreshAsync(EditorHistoryTarget.Project);
        context.SetStatus(status);
    }
}
