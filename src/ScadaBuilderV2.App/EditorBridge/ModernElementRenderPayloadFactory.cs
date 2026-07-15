using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App.EditorBridge;

/// <summary>Builds the recursive, editor-only WebView projection of an Element+.</summary>
/// <remarks>
/// Decisions: DEC-0041. Contracts: docs/superpowers/specs/2026-07-15-table-lock-interaction-regression-correction-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ModernElementRenderPayloadFactoryTests.cs.
/// </remarks>
internal static class ModernElementRenderPayloadFactory
{
    /// <summary>Creates a render payload without mutating the scene model.</summary>
    public static ModernElementRenderPayload Create(
        ScadaElement element,
        IReadOnlySet<string> selectedIds,
        int renderIndex)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(selectedIds);

        return new ModernElementRenderPayload
        {
            Id = element.Id,
            DisplayName = element.DisplayName,
            Kind = element.Kind.ToString(),
            X = element.Bounds.X,
            Y = element.Bounds.Y,
            Width = element.Bounds.Width,
            Height = element.Bounds.Height,
            IsSelected = selectedIds.Contains(element.Id),
            IsGroupContextSelected = element.Kind == ScadaElementKind.Group &&
                element.ChildElements.Any(child => selectedIds.Any(selectedId => ContainsElement(child, selectedId))),
            IsLocked = element.IsLocked,
            RenderIndex = renderIndex,
            Style = element.Style,
            Data = element.Data,
            ButtonBehavior = element.ButtonBehavior,
            ShapeKind = element.Kind == ScadaElementKind.Shape ? element.EffectiveShapeKind.ToString() : null,
            ButtonKind = element.Kind == ScadaElementKind.Button ? element.EffectiveButtonKind.ToString() : null,
            Table = element.Table,
            Children = element.ChildElements
                .Select((child, childIndex) => Create(child, selectedIds, childIndex))
                .ToArray()
        };
    }

    private static bool ContainsElement(ScadaElement element, string elementId)
        => element.Id == elementId || element.ChildElements.Any(child => ContainsElement(child, elementId));
}
