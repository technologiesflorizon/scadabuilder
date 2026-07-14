using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.History;

public sealed record SceneSnapshotChangedAction(
    string SceneId,
    ScadaScene BeforeScene,
    ScadaScene AfterScene,
    string Label,
    Guid? PageKey = null) : IEditorHistoryAction
{
    public EditorHistoryTarget Target => EditorHistoryTarget.ForScene(SceneId, PageKey);

    public bool CanMergeWith(IEditorHistoryAction next)
    {
        return false;
    }

    public IEditorHistoryAction MergeWith(IEditorHistoryAction next)
    {
        throw new InvalidOperationException("Scene snapshot actions do not support merge.");
    }

    public async Task UndoAsync(EditorHistoryContext context)
    {
        await ApplyAsync(context, BeforeScene, $"Undo {Label}.");
    }

    public async Task RedoAsync(EditorHistoryContext context)
    {
        await ApplyAsync(context, AfterScene, $"Redo {Label}.");
    }

    private async Task ApplyAsync(EditorHistoryContext context, ScadaScene scene, string status)
    {
        context.ReplaceScene(Target, scene);
        context.MarkTargetDirty(Target);
        await context.RefreshAsync(Target);
        context.SetStatus(status);
    }
}
