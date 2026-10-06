namespace ScadaBuilderV2.Application.History;

/// <summary>
/// Reversible quick-window mutation that restores the project, caller scenes, invocation descriptors,
/// editor selection and dirty state as one workspace unit.
/// </summary>
/// <remarks>
/// Statut : DEC-0050 décommissionné comme modèle d'authoring (remplacé par DEC-0052) ; conservé car DEC-0052 en
/// dépend : chaque mutation de feuille de paramètres et la migration d'une commande héritée passent par
/// QuickWindowWorkspaceMutation.ToHistoryAction, sur la pile unique en portée Project.
/// Decisions: DEC-0050 (décommissionné), DEC-0052, FR-014, FR-022.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §§8.5, 14.1.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetWorkspaceMutationTests.cs.
/// </remarks>
public sealed record QuickWindowWorkspaceSnapshotAction(
    ProjectWorkspaceHistorySnapshot Before,
    ProjectWorkspaceHistorySnapshot After,
    string Label) : IEditorHistoryAction
{
    /// <inheritdoc />
    public EditorHistoryTarget Target => EditorHistoryTarget.Project;

    /// <inheritdoc />
    public bool CanMergeWith(IEditorHistoryAction next) => false;

    /// <inheritdoc />
    public IEditorHistoryAction MergeWith(IEditorHistoryAction next) =>
        throw new InvalidOperationException("Quick-window workspace actions do not support merge.");

    /// <inheritdoc />
    public Task UndoAsync(EditorHistoryContext context) =>
        ProjectWorkspaceSnapshotAction.ApplyAsync(context, Before, $"Undo {Label}.");

    /// <inheritdoc />
    public Task RedoAsync(EditorHistoryContext context) =>
        ProjectWorkspaceSnapshotAction.ApplyAsync(context, After, $"Redo {Label}.");
}
