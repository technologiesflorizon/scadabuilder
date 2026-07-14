using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.History;

public sealed record ModernElementBoundsChangedAction(
    string SceneId,
    string ElementId,
    SceneBounds BeforeBounds,
    SceneBounds AfterBounds,
    Guid? PageKey = null) : IEditorHistoryAction
{
    public EditorHistoryTarget Target => EditorHistoryTarget.ForScene(SceneId, PageKey);

    public string Label => "Modifier la geometrie Element+";

    public bool CanMergeWith(IEditorHistoryAction next)
    {
        return next is ModernElementBoundsChangedAction other &&
               other.ElementId == ElementId;
    }

    public IEditorHistoryAction MergeWith(IEditorHistoryAction next)
    {
        var other = (ModernElementBoundsChangedAction)next;
        return this with { AfterBounds = other.AfterBounds };
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
        var scene = context.ResolveScene(Target);
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

        context.ReplaceScene(Target, scene.WithReplacedElementRecursive(current with { Bounds = bounds }));
        context.MarkTargetDirty(Target);
        await context.RefreshAsync(Target);
        context.SetStatus(status);
    }
}
