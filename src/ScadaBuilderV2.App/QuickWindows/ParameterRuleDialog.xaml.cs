using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>One nature option offered by the rule dialog's "Nature" combo box.</summary>
public sealed record ParameterRuleKindOption(string Label, ParameterValueKind Value);

/// <summary>
/// Rule dialog opened from the parameter-sheet editor header's "+": proposes the next free `#N`, lets the
/// operator pick the nature (Tag, Littéral, Expression) and either a catalog tag or a validated value, then
/// hands the result to <see cref="ParameterSheetEditorViewModel.AppendRule"/> (DEC-0052).
/// </summary>
/// <remarks>
/// The dialog never mutates the sheet text itself: it only prepares the (number, kind, value) triple that
/// the caller passes to <see cref="ParameterSheetEditorViewModel.AppendRule"/>, so an un-escapable literal
/// quote is refused exactly where the grammar decision lives.
///
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// </remarks>
public partial class ParameterRuleDialog : Window
{
    private static readonly IReadOnlyList<ParameterRuleKindOption> KindOptions =
    [
        new("Tag", ParameterValueKind.Tag),
        new("Littéral", ParameterValueKind.Literal),
        new("Expression", ParameterValueKind.Expression)
    ];

    private readonly ScadaTagCatalog? catalog;
    private bool isLoading = true;

    /// <summary>Creates the dialog proposing <paramref name="nextFreeNumber"/> against an optional tag catalog.</summary>
    public ParameterRuleDialog(int nextFreeNumber, ScadaTagCatalog? catalog)
    {
        this.catalog = catalog;
        InitializeComponent();

        NumberTextBox.Text = nextFreeNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        KindComboBox.ItemsSource = KindOptions;
        TagComboBox.ItemsSource = catalog?.Tags ?? Array.Empty<ScadaTagDefinition>();
        isLoading = false;
        KindComboBox.SelectedIndex = 0;
        Loaded += (_, _) => NumberTextBox.Focus();
    }

    /// <summary>Gets the parameter number accepted by the operator.</summary>
    public int Number { get; private set; }

    /// <summary>Gets the rule nature accepted by the operator.</summary>
    public ParameterValueKind Kind { get; private set; }

    /// <summary>Gets the tag name, literal text or expression text accepted by the operator.</summary>
    public string Value { get; private set; } = string.Empty;

    private void OnKindChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoading) return;
        RefreshValueEditor();
    }

    private void RefreshValueEditor()
    {
        var isTag = KindComboBox.SelectedValue is ParameterValueKind.Tag;
        TagComboBox.Visibility = isTag ? Visibility.Visible : Visibility.Collapsed;
        ValueTextBox.Visibility = isTag ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        if (KindComboBox.SelectedValue is not ParameterValueKind kind)
        {
            ShowError("Choisissez une nature pour la règle.");
            return;
        }

        if (!int.TryParse(NumberTextBox.Text, out var number) || number < 1 || number > 999)
        {
            ShowError("Le numéro doit être un entier entre 1 et 999.");
            return;
        }

        string value;
        if (kind == ParameterValueKind.Tag)
        {
            if (TagComboBox.SelectedItem is not ScadaTagDefinition tag)
            {
                ShowError("Choisissez un tag du catalogue.");
                return;
            }

            value = tag.Id;
        }
        else
        {
            value = ValueTextBox.Text?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                ShowError("La valeur ne peut pas être vide.");
                return;
            }

            if (kind == ParameterValueKind.Literal && value.Contains('"'))
            {
                ShowError("Un littéral ne peut pas contenir de guillemet : la grammaire des feuilles n'a pas d'échappement.");
                return;
            }
        }

        Number = number;
        Kind = kind;
        Value = value;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
