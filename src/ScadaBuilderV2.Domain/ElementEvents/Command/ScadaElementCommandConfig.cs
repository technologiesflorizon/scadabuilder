namespace ScadaBuilderV2.Domain.ElementEvents.Command;

/// <summary>
/// Element+ command configuration: an unordered set of independent <see cref="ScadaCommandBinding"/>,
/// each bound to its own trigger.
/// </summary>
/// <remarks>
/// Decisions: DEC-0036.
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementCommandConfigTests.cs.
/// </remarks>
public sealed record ScadaElementCommandConfig(IReadOnlyList<ScadaCommandBinding> Commands)
{
    /// <summary>Gets the default configuration: no commands.</summary>
    public static ScadaElementCommandConfig Default { get; } = new(Array.Empty<ScadaCommandBinding>());
}
