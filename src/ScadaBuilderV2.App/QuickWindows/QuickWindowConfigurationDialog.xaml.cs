using System.Windows;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// Modal editor of one `OpenQuickWindow` command's target: a parameter sheet and a page of type Fenêtre
/// rapide, paired by <see cref="ScadaBuilderV2.Domain.ElementEvents.Command.ScadaCommandBinding.ParameterSheetKey"/>
/// and <c>QuickWindowPageKey</c>.
/// </summary>
/// <remarks>
/// Replaces the former single "fenêtre rapide cible" combo box and the `Liaisons` tab: the pairing itself is
/// authored here, compiled later by <c>ParameterSheetLowering</c> into the DEC-0050 structures at build time.
///
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowConfigurationRulesTests.cs (save-enable rule);
/// no WPF unit coverage is possible for this `Window` itself (see the remarks on `ElementCommandDialog`).
/// </remarks>
public partial class QuickWindowConfigurationDialog : Window
{
    public QuickWindowConfigurationDialog(
        IReadOnlyList<ParameterSheet> parameterSheets,
        IReadOnlyList<ScadaSceneReference> quickWindowPages,
        Guid? selectedParameterSheetKey,
        Guid? selectedQuickWindowPageKey)
    {
        InitializeComponent();

        ParameterSheetComboBox.ItemsSource = parameterSheets;
        QuickWindowPageComboBox.ItemsSource = quickWindowPages;

        ParameterSheetComboBox.SelectedItem = parameterSheets.FirstOrDefault(sheet => sheet.SheetKey == selectedParameterSheetKey);
        QuickWindowPageComboBox.SelectedItem = quickWindowPages.FirstOrDefault(page => page.PageKey == selectedQuickWindowPageKey);

        UpdateSaveButtonState();
    }

    /// <summary>Gets the parameter sheet chosen by the operator, once "Enregistrer" has been clicked.</summary>
    public Guid? SelectedParameterSheetKey { get; private set; }

    /// <summary>Gets the quick-window page chosen by the operator, once "Enregistrer" has been clicked.</summary>
    public Guid? SelectedQuickWindowPageKey { get; private set; }

    private void OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        UpdateSaveButtonState();

    private void UpdateSaveButtonState()
    {
        var sheetKey = (ParameterSheetComboBox.SelectedItem as ParameterSheet)?.SheetKey;
        var pageKey = (QuickWindowPageComboBox.SelectedItem as ScadaSceneReference)?.PageKey;
        SaveButton.IsEnabled = QuickWindowConfigurationRules.CanSaveConfiguration(sheetKey, pageKey);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var sheet = ParameterSheetComboBox.SelectedItem as ParameterSheet;
        var page = QuickWindowPageComboBox.SelectedItem as ScadaSceneReference;
        if (!QuickWindowConfigurationRules.CanSaveConfiguration(sheet?.SheetKey, page?.PageKey))
        {
            ValidationText.Text = "Sélectionnez une feuille de paramètres et une page Fenêtre rapide.";
            return;
        }

        SelectedParameterSheetKey = sheet!.SheetKey;
        SelectedQuickWindowPageKey = page!.PageKey;
        DialogResult = true;
    }
}
