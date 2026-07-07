namespace ScadaBuilderV2.Domain.ElementEvents.State;

/// <summary>
/// Element+ display-state configuration: an ordered, first-match-wins list of
/// <see cref="ScadaStateRule"/>, plus two editable fallbacks (quality and default/rest).
/// </summary>
/// <remarks>
/// Decisions: DEC-0036.
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementStateConfigTests.cs.
/// </remarks>
public sealed record ScadaElementStateConfig(
    ScadaEffectBlock QualityFallback,
    ScadaEffectBlock DefaultEffect,
    IReadOnlyList<ScadaStateRule> States)
{
    /// <summary>
    /// Gets the default configuration: no states, empty rest appearance, and the standard
    /// "no data" quality fallback (semi-transparent, black border).
    /// </summary>
    public static ScadaElementStateConfig Default { get; } = new(
        QualityFallback: ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
        DefaultEffect: ScadaEffectBlock.Empty,
        States: Array.Empty<ScadaStateRule>());
}
