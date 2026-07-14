namespace ScadaBuilderV2.Application.History;

public sealed record SceneBackgroundChangedAction(
    string SceneId,
    string BeforeColor,
    string AfterColor,
    Guid? PageKey = null) : IEditorHistoryAction
{
    public EditorHistoryTarget Target => EditorHistoryTarget.ForScene(SceneId, PageKey);

    public string Label => "Modifier le fond de scene";

    public bool CanMergeWith(IEditorHistoryAction next)
    {
        return false;
    }

    public IEditorHistoryAction MergeWith(IEditorHistoryAction next)
    {
        throw new InvalidOperationException("Committed background changes do not support merge.");
    }

    public async Task UndoAsync(EditorHistoryContext context)
    {
        await ApplyAsync(context, BeforeColor, $"Undo fond de scene: {BeforeColor}.");
    }

    public async Task RedoAsync(EditorHistoryContext context)
    {
        await ApplyAsync(context, AfterColor, $"Redo fond de scene: {AfterColor}.");
    }

    private async Task ApplyAsync(EditorHistoryContext context, string color, string status)
    {
        var scene = context.ResolveScene(Target);
        if (scene is null)
        {
            context.SetStatus("Historique ignore: aucune scene active.");
            return;
        }

        context.ReplaceScene(Target, scene.WithBackgroundColor(color));
        context.MarkTargetDirty(Target);
        await context.RefreshAsync(Target);
        context.SetStatus(status);
    }
}
