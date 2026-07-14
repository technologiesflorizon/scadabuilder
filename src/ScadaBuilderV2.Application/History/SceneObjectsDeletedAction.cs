using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.History;

public sealed record DeletedSceneObjectSnapshot(
    ScadaElement Element,
    string? ParentElementId,
    int SiblingIndex);

public sealed record SceneObjectsDeletedAction(
    string SceneId,
    IReadOnlyList<DeletedSceneObjectSnapshot> DeletedObjects,
    IReadOnlyList<string>? DeletedSourceElementIds = null,
    Guid? PageKey = null) : IEditorHistoryAction
{
    public EditorHistoryTarget Target => EditorHistoryTarget.ForScene(SceneId, PageKey);

    public string Label => "Supprimer objets";

    public bool CanMergeWith(IEditorHistoryAction next)
    {
        return false;
    }

    public IEditorHistoryAction MergeWith(IEditorHistoryAction next)
    {
        throw new InvalidOperationException("Delete actions do not support merge.");
    }

    public async Task UndoAsync(EditorHistoryContext context)
    {
        var scene = context.ResolveScene(Target);
        if (scene is null)
        {
            context.SetStatus("Historique ignore: aucune scene active.");
            return;
        }

        foreach (var snapshot in DeletedObjects.OrderBy(snapshot => snapshot.SiblingIndex))
        {
            scene = RestoreObject(scene, snapshot);
        }

        scene = scene.WithoutRemovedSourceElementIds(GetSourceElementIds());
        context.ReplaceScene(Target, scene);
        context.MarkTargetDirty(Target);
        await context.RefreshAsync(Target);
        context.SetStatus($"{GetDeletedItemCount()} suppression(s) annulee(s).");
    }

    public async Task RedoAsync(EditorHistoryContext context)
    {
        var scene = context.ResolveScene(Target);
        if (scene is null)
        {
            context.SetStatus("Historique ignore: aucune scene active.");
            return;
        }

        scene = scene
            .WithoutSceneObjects(DeletedObjects.Select(snapshot => snapshot.Element.Id))
            .WithRemovedSourceElementIds(GetSourceElementIds());

        context.ReplaceScene(Target, scene);
        context.MarkTargetDirty(Target);
        await context.RefreshAsync(Target);
        context.SetStatus($"{GetDeletedItemCount()} suppression(s) retablie(s).");
    }

    private IEnumerable<string> GetSourceElementIds()
    {
        var sourceIds = DeletedObjects
            .Select(snapshot => snapshot.Element.LegacySource?.SourceElementId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!);

        return sourceIds
            .Concat(DeletedSourceElementIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal);
    }

    private int GetDeletedItemCount()
    {
        var sourceOnlyCount = (DeletedSourceElementIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Except(
                DeletedObjects
                    .Select(snapshot => snapshot.Element.LegacySource?.SourceElementId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id!),
                StringComparer.Ordinal)
            .Count();
        return DeletedObjects.Count + sourceOnlyCount;
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
