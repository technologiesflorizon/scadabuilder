using System.Windows;
using ScadaBuilderV2.Application.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// Lists every reference a boundary-crossing paste cannot honour, naming the object and the property, and
/// offers exactly two issues: `Annuler` or `Coller sans liaisons` (FR-UI-23).
/// </summary>
/// <remarks>
/// The dialog never proposes a silent partial paste and never offers to promote a refused reference into
/// the local interface of the target definition.
///
/// Decisions: DEC-0050, FR-031, FR-034, FR-UI-23.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowClipboardTests.cs.
/// </remarks>
public partial class QuickWindowPasteDiagnosticsDialog : Window
{
    /// <summary>Creates the dialog for one refused analysis.</summary>
    public QuickWindowPasteDiagnosticsDialog(QuickWindowClipboardAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        InitializeComponent();
        IssuesDataGrid.ItemsSource = analysis.Issues;
        SummaryText.Text = analysis.Issues.Count == 1
            ? "1 référence ne peut pas être résolue dans le contexte cible :"
            : $"{analysis.Issues.Count} références ne peuvent pas être résolues dans le contexte cible :";
    }

    /// <summary>Gets the operator decision; cancelling is the default.</summary>
    public QuickWindowPasteDecision Decision { get; private set; } = QuickWindowPasteDecision.Cancel;

    private void OnPasteWithoutBindingsClick(object sender, RoutedEventArgs e)
    {
        Decision = QuickWindowPasteDecision.PasteWithoutBindings;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Decision = QuickWindowPasteDecision.Cancel;
        DialogResult = false;
    }
}
