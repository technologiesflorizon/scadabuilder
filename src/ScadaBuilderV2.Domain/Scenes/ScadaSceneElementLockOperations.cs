namespace ScadaBuilderV2.Domain.Scenes;

/// <summary>Provides deterministic persistent position-lock operations for Element+ scene trees.</summary>
/// <remarks>
/// Decisions: DEC-0040.
/// Contracts: docs/superpowers/specs/2026-07-15-table-ui-authoring-and-element-lock-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ScadaSceneGroupTests.cs, tests/ScadaBuilderV2.Tests/ElementLockCoordinatorTests.cs.
/// </remarks>
public static class ScadaSceneElementLockOperations
{
    /// <summary>Expands selected groups to a stable, duplicate-free group-and-descendant closure.</summary>
    public static IReadOnlyList<string> ExpandSelectionClosure(ScadaScene scene, IEnumerable<string> elementIds)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(elementIds);
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in elementIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()))
        {
            var element = scene.FindElementRecursive(id) ?? throw new ArgumentException($"Element '{id}' was not found.", nameof(elementIds));
            Add(element, result, seen);
        }
        return result;
    }

    /// <summary>Returns whether an element is locked directly, by an ancestor, or by a locked descendant when it is a group.</summary>
    public static bool ResolveEffectiveLock(ScadaScene scene, string elementId)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        var element = scene.FindElementRecursive(elementId) ?? throw new ArgumentException($"Element '{elementId}' was not found.", nameof(elementId));
        if (element.IsLocked || ContainsLockedDescendant(element)) return true;
        for (var parent = scene.FindParentOf(elementId); parent is not null; parent = scene.FindParentOf(parent.Id))
        {
            if (parent.IsLocked) return true;
        }
        return false;
    }

    /// <summary>Returns a new scene with the complete selection closure set to one lock value.</summary>
    public static ScadaScene ApplyRecursive(ScadaScene scene, IEnumerable<string> elementIds, bool isLocked)
    {
        var ids = ExpandSelectionClosure(scene, elementIds).ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0) return scene;
        return scene with { Elements = scene.Elements.Select(element => Apply(element, ids, isLocked)).ToArray() };
    }

    private static void Add(ScadaElement element, ICollection<string> result, ISet<string> seen)
    {
        if (seen.Add(element.Id)) result.Add(element.Id);
        if (element.Kind != ScadaElementKind.Group) return;
        foreach (var child in element.ChildElements) Add(child, result, seen);
    }

    private static ScadaElement Apply(ScadaElement element, IReadOnlySet<string> ids, bool isLocked)
    {
        var children = element.ChildElements.Count == 0
            ? element.Children
            : element.ChildElements.Select(child => Apply(child, ids, isLocked)).ToArray();
        return element with { IsLocked = ids.Contains(element.Id) ? isLocked : element.IsLocked, Children = children };
    }

    private static bool ContainsLockedDescendant(ScadaElement element) =>
        element.ChildElements.Any(child => child.IsLocked || ContainsLockedDescendant(child));
}
