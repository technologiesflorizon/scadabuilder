using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Selection;

/// <summary>Rejects transforms that change the effective position of locked Element+ objects.</summary>
/// <remarks>Decisions: DEC-0040. Tests: tests/ScadaBuilderV2.Tests/ElementTransformGuardTests.cs.</remarks>
public sealed class ElementTransformGuard
{
    /// <summary>Validates one proposed scene transform while allowing W/H and rotation-only changes.</summary>
    public bool CanApply(ScadaScene before, ScadaScene after, IEnumerable<string> targetIds, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var relevant = ScadaSceneElementLockOperations.ExpandSelectionClosure(before, targetIds).ToHashSet(StringComparer.Ordinal);
        var beforePositions = AbsolutePositions(before);
        var afterPositions = AbsolutePositions(after);
        foreach (var id in relevant)
        {
            if (!beforePositions.TryGetValue(id, out var oldPosition) || !afterPositions.TryGetValue(id, out var newPosition)) continue;
            if (NearlyEqual(oldPosition.X, newPosition.X) && NearlyEqual(oldPosition.Y, newPosition.Y)) continue;
            if (ScadaSceneElementLockOperations.ResolveEffectiveLock(before, id))
            {
                reason = $"L'Element+ '{id}' est verrouille en position.";
                return false;
            }
        }
        reason = null;
        return true;
    }

    private static Dictionary<string, (double X, double Y)> AbsolutePositions(ScadaScene scene)
    {
        var result = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
        foreach (var element in scene.Elements) Add(element, 0, 0, result);
        return result;
    }

    private static void Add(ScadaElement element, double parentX, double parentY, IDictionary<string, (double X, double Y)> result)
    {
        var x = parentX + element.Bounds.X;
        var y = parentY + element.Bounds.Y;
        result[element.Id] = (x, y);
        foreach (var child in element.ChildElements) Add(child, x, y, result);
    }

    private static bool NearlyEqual(double left, double right) => Math.Abs(left - right) < 0.0001;
}
