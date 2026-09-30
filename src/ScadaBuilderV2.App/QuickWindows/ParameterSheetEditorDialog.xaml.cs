using System.Windows;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// Editor of one parameter sheet: a monospace textarea bound to <see cref="ParameterSheetEditorViewModel.Text"/>,
/// line diagnostics refreshed on every edit, and a header "+" opening <see cref="ParameterRuleDialog"/> to
/// append one rule in the existing expression grammar (DEC-0052).
/// </summary>
/// <remarks>
/// "Enregistrer" hands the edited <see cref="ParameterSheet"/> back to the caller, which applies
/// <c>QuickWindowWorkspaceController.UpdateSheetTextAsync</c> as a single undoable transition; this dialog
/// never mutates the project itself.
///
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// </remarks>
public partial class ParameterSheetEditorDialog : Window
{
    private readonly ParameterSheetEditorViewModel viewModel;
    private bool isLoadingText = true;

    /// <summary>Creates the dialog on one sheet's editor view-model.</summary>
    public ParameterSheetEditorDialog(ParameterSheetEditorViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        this.viewModel = viewModel;
        InitializeComponent();

        TitleText.Text = viewModel.DisplayName;
        isLoadingText = true;
        TextEditor.Text = viewModel.Text;
        isLoadingText = false;
        RefreshDiagnostics();
        Loaded += (_, _) => TextEditor.Focus();
    }

    /// <summary>Gets the sheet with its saved text once "Enregistrer" was accepted.</summary>
    public ParameterSheet? SavedSheet { get; private set; }

    private void OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (isLoadingText) return;
        viewModel.Text = TextEditor.Text;
        RefreshDiagnostics();
    }

    private void RefreshDiagnostics()
    {
        DiagnosticsList.ItemsSource = viewModel.Diagnostics
            .Select(diagnostic => $"Ligne {diagnostic.Line} : {diagnostic.Message}")
            .ToArray();
    }

    private void OnAddRuleClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ParameterRuleDialog(viewModel.NextFreeNumber, viewModel.Catalog) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        try
        {
            viewModel.AppendRule(dialog.Number, dialog.Kind, dialog.Value);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(this, ex.Message, "Règle refusée", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        isLoadingText = true;
        TextEditor.Text = viewModel.Text;
        TextEditor.CaretIndex = TextEditor.Text.Length;
        isLoadingText = false;
        RefreshDiagnostics();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        SavedSheet = viewModel.ToSheet();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
