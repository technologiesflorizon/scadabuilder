using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.History;

public sealed record SceneObjectsAddedAction(
    string SceneId,
    IReadOnlyList<DeletedSceneObjectSnapshot> AddedObjects,
    Guid? PageKey = null) : IEditorHistoryAction
{
    public EditorHistoryTarget Target => EditorHistoryTarget.ForScene(SceneId, PageKey);

    public string Label => "Coller objets";

    public bool CanMergeWith(IEditorHistoryAction next) => false;

    public IEditorHistoryAction MergeWith(IEditorHistoryAction next)
    {
        throw new InvalidOperationException("Add actions do not support merge.");
    }

    public async Task UndoAsync(EditorHistoryContext context)
    {
        var scene = context.ResolveScene(Target);
        if (scene is null)
        {
            context.SetStatus("Historique ignore: aucune scene active.");
            return;
        }

        scene = scene.WithoutSceneObjects(AddedObjects.Select(snapshot => snapshot.Element.Id));
        context.ReplaceScene(Target, scene);
        context.MarkTargetDirty(Target);
        await context.RefreshAsync(Target);
        context.SetStatus($"{AddedObjects.Count} collage(s) annule(s).");
    }

    public async Task RedoAsync(EditorHistoryContext context)
    {
        var scene = context.ResolveScene(Target);
        if (scene is null)
        {
            context.SetStatus("Historique ignore: aucune scene active.");
            return;
        }

        foreach (var snapshot in AddedObjects.OrderBy(snapshot => snapshot.SiblingIndex))
        {
            scene = RestoreObject(scene, snapshot);
        }

        context.ReplaceScene(Target, scene);
        context.MarkTargetDirty(Target);
        await context.RefreshAsync(Target);
        context.SetStatus($"{AddedObjects.Count} collage(s) retabli(s).");
    }

    private static ScadaScene RestoreObject(ScadaScene scene, DeletedSceneObjectSnapshot snapshot)
    {
        if (scene.FindElementRecursive(snapshot.Element.Id) is not null)
        {
            return scene;
        }

        if (string.IsNullOrWhiteSpace(snapshot.ParentElementId))
        {
            return scene with
            {
                Elements = InsertAt(scene.Elements, snapshot.Element, snapshot.SiblingIndex)
            };
        }

        var parent = scene.FindElementRecursive(snapshot.ParentElementId);
        if (parent is null)
        {
            return scene with
            {
                Elements = InsertAt(scene.Elements, snapshot.Element, snapshot.SiblingIndex)
            };
        }

        var restoredParent = parent with
        {
            Children = InsertAt(parent.ChildElements, snapshot.Element, snapshot.SiblingIndex)
        };

        return scene.WithReplacedElementRecursive(restoredParent);
    }

    private static IReadOnlyList<ScadaElement> InsertAt(
        IReadOnlyList<ScadaElement> elements,
        ScadaElement element,
        int index)
    {
        var normalizedIndex = Math.Clamp(index, 0, elements.Count);
        return elements
            .Where(existing => existing.Id != element.Id)
            .Take(normalizedIndex)
            .Concat([element])
            .Concat(elements.Where(existing => existing.Id != element.Id).Skip(normalizedIndex))
            .ToArray();
    }
}
