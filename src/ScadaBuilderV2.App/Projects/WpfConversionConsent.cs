using System.Windows;
using ScadaBuilderV2.Application.Formats;

namespace ScadaBuilderV2.App.Projects;

/// <summary>Presents a conversion plan through the WPF dialog.</summary>
/// <remarks>
/// WPF owns the presentation and nothing else. Whether the question is worth asking belongs to
/// <see cref="ConversionCoordinator"/>.
/// </remarks>
public sealed class WpfConversionConsent(Window owner) : IConversionConsent
{
    /// <inheritdoc />
    public Task<ConversionDecision> RequestAsync(ConversionPlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dialog = new ConversionPlanDialog(plan) { Owner = owner };
        dialog.ShowDialog();
        return Task.FromResult(dialog.Decision);
    }
}
