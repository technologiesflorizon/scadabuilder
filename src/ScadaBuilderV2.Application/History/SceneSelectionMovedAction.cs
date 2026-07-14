using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.History;

public sealed record MovedSceneElementBounds(
    string ElementId,
    SceneBounds BeforeBounds,
    SceneBounds AfterBounds);

public sealed record SceneSelectionMovedAction(
    string SceneId,
    IReadOnlyList<MovedSceneElementBounds> ElementBounds,
    string Label = "deplacement selection",
    Guid? PageKey = null) : IEditorHistoryAction
{
    public EditorHistoryTarget Target => EditorHistoryTarget.ForScene(SceneId, PageKey);

    public bool CanMergeWith(IEditorHistoryAction next)
    {
        return false;
    }

    public IEditorHistoryAction MergeWith(IEditorHistoryAction next)
    {
        throw new InvalidOperationException("Committed selection movement changes do not support merge.");
    }

    public async Task UndoAsync(EditorHistoryContext context)
    {
        await ApplyAsync(context, useAfterBounds: false, $"Undo {Label}.");
    }

    public async Task RedoAsync(EditorHistoryContext context)
    {
        await ApplyAsync(context, useAfterBounds: true, $"Redo {Label}.");
    }

    private async Task ApplyAsync(EditorHistoryContext context, bool useAfterBounds, string status)
    {
        var scene = context.ResolveScene(Target);
        if (scene is null)
        {
            context.SetStatus("Historique ignore: aucune scene active.");
            return;
        }

        var updatedScene = scene;
        foreach (var item in ElementBounds)
        {
            var current = updatedScene.FindElementRecursive(item.ElementId);
            if (current is null)
            {
                continue;
            }

            updatedScene = updatedScene.WithReplacedElementRecursive(current with
            {
                Bounds = useAfterBounds ? item.AfterBounds : item.BeforeBounds
            });
        }

        context.ReplaceScene(Target, updatedScene);
        context.MarkTargetDirty(Target);
        await context.RefreshAsync(Target);
        context.SetStatus(status);
    }
}
