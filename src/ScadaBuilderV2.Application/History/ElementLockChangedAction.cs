using ScadaBuilderV2.Application.Selection;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.History;

/// <summary>Restores exact persistent lock values for one Element+ selection closure.</summary>
/// <remarks>Decisions: DEC-0040. Tests: tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs.</remarks>
public sealed record ElementLockChangedAction(
    string SceneId,
    IReadOnlyList<ElementLockMutationItem> Items,
    Guid? PageKey = null) : IEditorHistoryAction
{
    public EditorHistoryTarget Target => EditorHistoryTarget.ForScene(SceneId, PageKey);
    public string Label => "Verrouillage Element+";
    public bool CanMergeWith(IEditorHistoryAction next) => false;
    public IEditorHistoryAction MergeWith(IEditorHistoryAction next) => throw new InvalidOperationException("Lock actions do not merge.");
    public Task UndoAsync(EditorHistoryContext context) => ApplyAsync(context, before: true, "Undo verrouillage Element+.");
    public Task RedoAsync(EditorHistoryContext context) => ApplyAsync(context, before: false, "Redo verrouillage Element+.");

    private async Task ApplyAsync(EditorHistoryContext context, bool before, string status)
    {
        var scene = context.ResolveScene(Target);
        if (scene is null) return;
        var updated = scene;
        foreach (var item in Items)
        {
            var element = updated.FindElementRecursive(item.ElementId);
            if (element is not null) updated = updated.WithReplacedElementRecursive(element with { IsLocked = before ? item.Before : item.After });
        }
        context.ReplaceScene(Target, updated);
        context.MarkTargetDirty(Target);
        await context.RefreshAsync(Target);
        context.SetStatus(status);
    }
}
