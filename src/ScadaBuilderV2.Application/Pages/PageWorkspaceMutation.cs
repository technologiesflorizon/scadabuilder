using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.History;

namespace ScadaBuilderV2.Application.Pages;

/// <summary>One fully prepared page-workspace transition and its common UI routing result.</summary>
public sealed record PageWorkspaceMutation(
    PageWorkspaceSnapshot Before,
    PageWorkspaceSnapshot After,
    ProjectWorkspaceUiSnapshot BeforeUi,
    ProjectWorkspaceUiSnapshot AfterUi,
    CommandResult Result,
    string HistoryLabel)
{
    /// <summary>Creates the project-scoped history action for this transition.</summary>
    public ProjectWorkspaceSnapshotAction ToHistoryAction(bool beforeWasDirty = false) => new(
        ToHistorySnapshot(Before, BeforeUi, beforeWasDirty),
        ToHistorySnapshot(After, AfterUi, true),
        HistoryLabel);

    private static ProjectWorkspaceHistorySnapshot ToHistorySnapshot(
        PageWorkspaceSnapshot snapshot,
        ProjectWorkspaceUiSnapshot ui,
        bool dirty) => new(
            snapshot.Project,
            snapshot.Scenes,
            ui,
            dirty,
            snapshot.PendingDeletions.Select(deletion => deletion.PageKey).ToArray());
}
