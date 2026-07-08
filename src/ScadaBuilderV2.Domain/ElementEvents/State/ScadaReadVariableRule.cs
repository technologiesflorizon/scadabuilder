namespace ScadaBuilderV2.Domain.ElementEvents.State;

/// <summary>
/// Independent, continuous tag-value display for an Element+ — evaluated separately from
/// <see cref="ScadaElementStateConfig.States"/> and never affected by which state rule matches.
/// </summary>
/// <remarks>
/// Decisions: DEC-0036.
/// Contracts: docs/superpowers/specs/2026-07-09-element-plus-appearance-and-read-variable-design.md.
/// </remarks>
/// <param name="TagId">The tag whose value is displayed.</param>
/// <param name="DisplayFormat">
/// Optional format string using the literal token <c>{valeur}</c> (e.g. <c>"Debit: {valeur} L/min"</c>).
/// Null or a format without <c>{valeur}</c> displays the raw tag value.
/// </param>
public sealed record ScadaReadVariableRule(
    string TagId,
    string? DisplayFormat = null);
