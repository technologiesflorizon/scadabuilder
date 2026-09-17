namespace ScadaBuilderV2.Application.Formats;

/// <summary>Asks the operator to accept a conversion plan.</summary>
/// <remarks>
/// A conversion does not undo, so it is never implicit. The implementation owns the presentation and nothing
/// else; the coordinator owns whether the question is worth asking.
///
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C5, C7.
/// </remarks>
public interface IConversionConsent
{
    /// <summary>Returns the operator's answer for one plan.</summary>
    Task<ConversionDecision> RequestAsync(ConversionPlan plan, CancellationToken cancellationToken);
}
