using System.Windows;
using ScadaBuilderV2.App.Shell;
using ScadaBuilderV2.Application.Formats;

namespace ScadaBuilderV2.App.Projects;

/// <summary>Presents a conversion plan through the WPF dialog.</summary>
/// <remarks>
/// WPF owns the presentation and nothing else. Whether the question is worth asking belongs to
/// <see cref="ConversionCoordinator"/>.
///
/// The consent question is raised in the middle of an opening gesture, so the shell's busy veil is up when it
/// arrives. It is taken down for the length of the dialog: a spinning ring behind a question reads as a frozen
/// application, and the operator has no reason to believe the answer will be taken. `IConversionConsent` stays
/// as it is - the suspension is a WPF shell concern and has no business in the Application layer - so the
/// capability arrives by constructor instead.
///
/// Decisions: `DEC-0049` (cycle de vie autonome des projets V2).
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C5, C7;
/// `docs/06_ui_ux/UI_ARCHITECTURE_V2.md` sections 1 and 2 for the veil.
/// Tests: `tests/ScadaBuilderV2.Tests/ProjectLoadingOverlayContractTests.cs`.
/// </remarks>
public sealed class WpfConversionConsent(Window owner, IBusyOverlaySuspender overlaySuspender) : IConversionConsent
{
    /// <inheritdoc />
    public Task<ConversionDecision> RequestAsync(ConversionPlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dialog = new ConversionPlanDialog(plan) { Owner = owner };
        using (overlaySuspender.Suspend())
        {
            dialog.ShowDialog();
        }

        return Task.FromResult(dialog.Decision);
    }
}
