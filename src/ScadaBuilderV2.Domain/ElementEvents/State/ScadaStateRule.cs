using ScadaBuilderV2.Domain.ElementEvents.Expressions;

namespace ScadaBuilderV2.Domain.ElementEvents.State;

/// <summary>
/// One user-named state in an Element+ state list. Evaluated top-to-bottom; the first rule
/// whose expression is true wins (first-match-wins). A rule whose expression references a
/// tag with an unavailable (null) value is skipped rather than treated as false.
/// </summary>
/// <remarks>
/// Decisions: DEC-0036.
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementStateConfigTests.cs.
/// </remarks>
public sealed record ScadaStateRule(
    string Id,
    string Name,
    bool Enabled,
    ScadaExpression Expression,
    ScadaEffectBlock Effect);
