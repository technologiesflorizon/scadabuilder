using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Selection;

/// <summary>Builds aggregated lock state and immutable recursive lock mutations.</summary>
/// <remarks>Decisions: DEC-0040. Tests: tests/ScadaBuilderV2.Tests/ElementLockCoordinatorTests.cs.</remarks>
public sealed class ElementLockCoordinator
{
    /// <summary>Aggregates the current selection according to the group-closure contract.</summary>
    public ElementLockSelectionState BuildState(ScadaScene scene, IEnumerable<string> selectedIds)
    {
        var ids = ScadaSceneElementLockOperations.ExpandSelectionClosure(scene, selectedIds);
        if (ids.Count == 0) return ElementLockSelectionState.Empty;
        var locked = ids.Count(id => scene.FindElementRecursive(id)?.IsLocked == true);
        return new(true, locked == ids.Count, locked > 0 && locked < ids.Count, ids);
    }

    /// <summary>Locks the whole closure when any target is unlocked; otherwise unlocks the whole closure.</summary>
    public ElementLockMutation Toggle(ScadaScene scene, IEnumerable<string> selectedIds)
    {
        var state = BuildState(scene, selectedIds);
        if (!state.HasSelection) return new(scene, scene, Array.Empty<ElementLockMutationItem>());
        var next = !state.AllLocked;
        var items = state.TargetIds.Select(id => new ElementLockMutationItem(id, scene.FindElementRecursive(id)!.IsLocked, next)).ToArray();
        return new(scene, ScadaSceneElementLockOperations.ApplyRecursive(scene, state.TargetIds, next), items);
    }
}

/// <summary>Records one element lock transition.</summary>
public sealed record ElementLockMutationItem(string ElementId, bool Before, bool After);

/// <summary>Contains the before/after scenes and exact element transitions for one lock command.</summary>
public sealed record ElementLockMutation(ScadaScene BeforeScene, ScadaScene AfterScene, IReadOnlyList<ElementLockMutationItem> Items)
{
    /// <summary>Gets whether the command changes persistent state.</summary>
    public bool Changed => Items.Any(item => item.Before != item.After);
}
