namespace ScadaBuilderV2.Domain.ElementEvents.State;

/// <summary>
/// Single cumulative animation applied by an Element+ state effect.
/// </summary>
public enum ScadaAnimation
{
    None,
    Blink,
    Pulse,
    Halo,
    Spin
}

/// <summary>
/// Describes an optional, cumulative set of visual overrides applied when an Element+ state matches.
/// Every property is optional; a null property leaves the design-time appearance unchanged.
/// </summary>
/// <remarks>
/// Decisions: DEC-0036.
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaEffectBlockTests.cs.
/// </remarks>
public sealed record ScadaEffectBlock(
    string? BackgroundColor = null,
    string? BorderColor = null,
    double? BorderWidth = null,
    string? TextColor = null,
    string? TextContent = null,
    bool? TextVisible = null,
    bool? ElementVisible = null,
    double? Opacity = null,
    double? Rotation = null,
    ScadaAnimation? Animation = null,
    string? ColorFilterColor = null,
    double? ColorFilterOpacity = null,
    bool? ColorFilterHalo = null,
    string? ColorFilterHaloColor = null)
{
    /// <summary>
    /// Gets an effect block with every property unset.
    /// </summary>
    public static ScadaEffectBlock Empty { get; } = new();
}
