namespace ScadaBuilderV2.Application.History;

public sealed record SceneBackgroundChangedAction(
    string SceneId,
    string BeforeColor,
    string AfterColor) : IEditorHistoryAction
{
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

    private static async Task ApplyAsync(EditorHistoryContext context, string color, string status)
    {
        var scene = context.GetActiveScene();
        if (scene is null)
        {
            context.SetStatus("Historique ignore: aucune scene active.");
            return;
        }

        context.ReplaceActiveScene(scene.WithBackgroundColor(color));
        context.MarkDirty();
        await context.RefreshPreviewAsync();
        context.SetStatus(status);
    }
}
