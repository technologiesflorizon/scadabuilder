using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.Application.History;

/// <summary>
/// Reversible mutation of one quick-window definition visual content.
/// </summary>
/// <remarks>
/// Quick windows do not own a second history service: the action carries a
/// <see cref="EditorHistoryScope.QuickWindow"/> target on the single workspace stack, and the host
/// activates that context through <see cref="EditorHistoryContext.RefreshAsync"/> before showing the
/// restored state.
///
/// Decisions: DEC-0050, FR-035, FR-UI-26.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §9.4.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowShellContractTests.cs.
/// </remarks>
public sealed record QuickWindowContentChangedAction(
    Guid DefinitionKey,
    VisualContent Before,
    VisualContent After,
    string Label) : IEditorHistoryAction
{
    /// <inheritdoc />
    public EditorHistoryTarget Target { get; } = EditorHistoryTarget.ForQuickWindow(DefinitionKey);

    /// <inheritdoc />
    public bool CanMergeWith(IEditorHistoryAction next) => false;

    /// <inheritdoc />
    public IEditorHistoryAction MergeWith(IEditorHistoryAction next) =>
        throw new InvalidOperationException("Quick-window content actions do not support merge.");

    /// <inheritdoc />
    public Task UndoAsync(EditorHistoryContext context) => ApplyAsync(context, Before, $"Undo {Label}.");

    /// <inheritdoc />
    public Task RedoAsync(EditorHistoryContext context) => ApplyAsync(context, After, $"Redo {Label}.");

    private async Task ApplyAsync(EditorHistoryContext context, VisualContent content, string status)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.GetQuickWindowDefinition is null || context.ReplaceQuickWindowDefinition is null)
            throw new InvalidOperationException("The editor context cannot resolve quick-window definitions.");

        var definition = context.GetQuickWindowDefinition(DefinitionKey)
            ?? throw new InvalidOperationException($"Quick-window definition '{DefinitionKey}' is not loaded.");

        context.ReplaceQuickWindowDefinition(definition with { Content = content });
        context.MarkDirty();
        context.SetStatus(status);
        await context.RefreshAsync(Target);
    }
}
