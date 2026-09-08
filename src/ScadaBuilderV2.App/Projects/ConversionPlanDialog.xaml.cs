using System.Windows;
using ScadaBuilderV2.Application.Formats;

namespace ScadaBuilderV2.App.Projects;

/// <summary>Shows what a conversion will do and collects the operator's answer.</summary>
/// <remarks>
/// C5 allows two outcomes: convert, or do not open. There is no read-only consultation mode - the generations
/// diverge too much for a half-migrated session to be faithful to what the operator believes they see.
///
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C5, C7.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/ConversionDialogContractTests.cs.
/// </remarks>
public partial class ConversionPlanDialog : Window
{
    /// <summary>Gets the operator's answer; `Cancel` unless a button says otherwise.</summary>
    public ConversionDecision Decision { get; private set; } = ConversionDecision.Cancel;

    /// <summary>Creates the dialog for one plan.</summary>
    public ConversionPlanDialog(ConversionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        InitializeComponent();
        PlanItems.ItemsSource = plan.Entries;
    }

    private void OnConvertClick(object sender, RoutedEventArgs e) => Complete(ConversionDecision.Convert);

    private void OnCancelClick(object sender, RoutedEventArgs e) => Complete(ConversionDecision.Cancel);

    private void Complete(ConversionDecision decision)
    {
        Decision = decision;
        DialogResult = decision == ConversionDecision.Convert;
        Close();
    }
}
