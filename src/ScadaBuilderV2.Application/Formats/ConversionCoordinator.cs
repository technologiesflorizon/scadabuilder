using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Application.Formats;

/// <summary>Result of preparing a conversion, before anything is written.</summary>
public sealed record ConversionOutcome(
    bool CanProceed,
    ConversionPlan Plan,
    IReadOnlyList<ScadaBuildValidationIssue> Diagnostics);

/// <summary>Decides whether a set of artifacts can be opened, and on what terms.</summary>
/// <remarks>
/// The order matters and is the point of this type. A chain that cannot run is refused before the operator is
/// asked, because there is nothing to consent to. An artifact already current asks nothing. And a refusal
/// stops the transition outright: C5 allows convert or do not open, so nothing here can produce a session on
/// an unconverted artifact.
///
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C4, C5.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/ConversionCoordinatorTests.cs.
/// </remarks>
public sealed class ConversionCoordinator(ArtifactConverterRegistry registry, IConversionConsent consent)
{
    /// <summary>Builds the plan, validates every chain and obtains consent when there is something to convert.</summary>
    public async Task<ConversionOutcome> PrepareAsync(
        IReadOnlyList<ArtifactToConvert> artifacts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifacts);

        var entries = new List<ConversionPlanEntry>();
        var diagnostics = new List<ScadaBuildValidationIssue>();

        foreach (var artifact in artifacts.Where(item => item.FromVersion < item.ToVersion))
        {
            var chain = registry.ResolveChain(artifact.Module, artifact.FromVersion, artifact.ToVersion);
            if (!chain.IsComplete)
            {
                diagnostics.Add(new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Error,
                    "project.conversion-chain-incomplete",
                    $"Aucun convertisseur ne prend le format {chain.MissingFromVersion} du module "
                    + $"{artifact.Module}. La conversion est refusée plutôt qu'exécutée à moitié.",
                    PropertyPath: artifact.FilePath));
                continue;
            }

            entries.Add(new ConversionPlanEntry(
                artifact.Module,
                artifact.FilePath,
                artifact.FromVersion,
                artifact.ToVersion,
                chain.Steps.Select(step => step.StepDescription).ToArray()));
        }

        var plan = new ConversionPlan(entries);
        if (diagnostics.Count > 0)
        {
            return new ConversionOutcome(false, plan, diagnostics);
        }
        if (plan.IsEmpty)
        {
            return new ConversionOutcome(true, plan, []);
        }

        var decision = await consent.RequestAsync(plan, cancellationToken);
        if (decision == ConversionDecision.Cancel)
        {
            return new ConversionOutcome(false, plan, [new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Warning,
                "project.conversion-declined",
                "La conversion a été refusée; le projet n'est pas ouvert.")]);
        }

        return new ConversionOutcome(true, plan, []);
    }
}
