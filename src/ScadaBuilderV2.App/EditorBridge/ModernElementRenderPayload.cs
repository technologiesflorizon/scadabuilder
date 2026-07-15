using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App.EditorBridge;

/// <summary>Editor-only projection consumed by the WebView scene canvas.</summary>
/// <remarks>
/// Decisions: DEC-0041. Contracts: docs/superpowers/specs/2026-07-15-table-lock-interaction-regression-correction-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ModernElementRenderPayloadFactoryTests.cs.
/// </remarks>
internal sealed class ModernElementRenderPayload
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Kind { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsSelected { get; set; }
    public bool IsGroupContextSelected { get; set; }
    public bool IsLocked { get; set; }
    public int RenderIndex { get; set; }
    public ScadaElementStyle? Style { get; set; }
    public ScadaElementData? Data { get; set; }
    public ScadaButtonBehavior? ButtonBehavior { get; set; }
    public string? ShapeKind { get; set; }
    public string? ButtonKind { get; set; }
    public ScadaTableDefinition? Table { get; set; }
    public IReadOnlyList<ModernElementRenderPayload> Children { get; set; } = [];
}
