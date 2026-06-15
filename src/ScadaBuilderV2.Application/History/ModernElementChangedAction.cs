using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.History;

public sealed record ModernElementChangedAction(
    string SceneId,
    ScadaElement BeforeElement,
    ScadaElement AfterElement,
    string Label) : IEditorHistoryAction
{
    public string ElementId => BeforeElement.Id;

    public bool CanMergeWith(IEditorHistoryAction next)
    {
        return next is ModernElementChangedAction elementChange &&
            string.Equals(SceneId, elementChange.SceneId, StringComparison.Ordinal) &&
            string.Equals(ElementId, elementChange.ElementId, StringComparison.Ordinal) &&
            Equals(AfterElement, elementChange.BeforeElement);
    }

    public IEditorHistoryAction MergeWith(IEditorHistoryAction next)
    {
        if (!CanMergeWith(next) || next is not ModernElementChangedAction elementChange)
        {
            throw new InvalidOperationException("Incompatible Element+ history action.");
        }

        return this with { AfterElement = elementChange.AfterElement };
    }

    public async Task UndoAsync(EditorHistoryContext context)
    {
        await ApplyAsync(context, BeforeElement, $"Undo {Label}.");
    }

    public async Task RedoAsync(EditorHistoryContext context)
    {
        await ApplyAsync(context, AfterElement, $"Redo {Label}.");
    }

    private async Task ApplyAsync(EditorHistoryContext context, ScadaElement element, string status)
    {
        var scene = context.GetActiveScene();
        if (scene is null)
        {
            context.SetStatus("Historique ignore: aucune scene active.");
            return;
        }

        if (scene.FindElementRecursive(ElementId) is null)
        {
            context.SetStatus($"Historique ignore: element introuvable {ElementId}.");
            return;
        }

        context.ReplaceActiveScene(scene.WithReplacedElementRecursive(element));
        context.MarkDirty();
        await context.RefreshPreviewAsync();
        context.SetStatus(status);
    }
}
