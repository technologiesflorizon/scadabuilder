using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.History;

public sealed record ModernElementBoundsChangedAction(
    string SceneId,
    string ElementId,
    SceneBounds BeforeBounds,
    SceneBounds AfterBounds) : IEditorHistoryAction
{
    public string Label => "Modifier la geometrie Element+";

    public bool CanMergeWith(IEditorHistoryAction next)
    {
        return false;
    }

    public IEditorHistoryAction MergeWith(IEditorHistoryAction next)
    {
        throw new InvalidOperationException("Committed geometry changes do not support merge.");
    }

    public async Task UndoAsync(EditorHistoryContext context)
    {
        await ApplyAsync(context, BeforeBounds, "Undo geometrie Element+.");
    }

    public async Task RedoAsync(EditorHistoryContext context)
    {
        await ApplyAsync(context, AfterBounds, "Redo geometrie Element+.");
    }

    private async Task ApplyAsync(EditorHistoryContext context, SceneBounds bounds, string status)
    {
        var scene = context.GetActiveScene();
        if (scene is null)
        {
            context.SetStatus("Historique ignore: aucune scene active.");
            return;
        }

        var current = scene.FindElementRecursive(ElementId);
        if (current is null)
        {
            context.SetStatus($"Historique ignore: element introuvable {ElementId}.");
            return;
        }

        context.ReplaceActiveScene(scene.WithReplacedElementRecursive(current with { Bounds = bounds }));
        context.MarkDirty();
        await context.RefreshPreviewAsync();
        context.SetStatus(status);
    }
}
