namespace ScadaBuilderV2.Application.Selection;

/// <summary>Aggregates persistent lock state for the active Element+ selection closure.</summary>
/// <remarks>Decisions: DEC-0040. Tests: tests/ScadaBuilderV2.Tests/ElementLockCoordinatorTests.cs.</remarks>
public sealed record ElementLockSelectionState(
    bool HasSelection,
    bool AllLocked,
    bool IsMixed,
    IReadOnlyList<string> TargetIds)
{
    /// <summary>Gets the empty selection state.</summary>
    public static ElementLockSelectionState Empty { get; } = new(false, false, false, Array.Empty<string>());
}
