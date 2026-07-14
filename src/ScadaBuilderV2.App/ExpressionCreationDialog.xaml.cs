using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App;

public partial class ExpressionCreationDialog : Window
{
    private readonly ScadaTagCatalog? _tagCatalog;

    public string? ResultExpression { get; private set; }

    public ExpressionCreationDialog(string initialExpression, ScadaTagCatalog? tagCatalog, int? initialCaretIndex = null)
    {
        InitializeComponent();
        _tagCatalog = tagCatalog;
        ExpressionTextBox.Text = initialExpression ?? string.Empty;
        ExpressionTextBox.CaretIndex = ResolveInitialCaret(initialCaretIndex, ExpressionTextBox.Text.Length);
        ValidateInline();
    }

    private static int ResolveInitialCaret(int? caretIndex, int textLength)
    {
        if (caretIndex is null || caretIndex.Value <= 0)
            return 0;

        return Math.Min(caretIndex.Value, textLength);
    }

    private void OnVariableClick(object sender, RoutedEventArgs e)
    {
        var dialog = new TagSelectionDialog(_tagCatalog) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedTag is { } tag)
            InsertAtCaret($"{{{tag.DisplayName}}}");
    }

    private void OnOperatorClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string fragment })
            InsertAtCaret(fragment);
    }

    private void OnParenthesesClick(object sender, RoutedEventArgs e)
    {
        var caret = Math.Clamp(ExpressionTextBox.CaretIndex, 0, ExpressionTextBox.Text.Length);
        var text = ExpressionTextBox.Text;
        ExpressionTextBox.Text = text.Insert(caret, "()");
        ExpressionTextBox.CaretIndex = caret + 1;
        ExpressionTextBox.Focus();
        ValidateInline();
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        ExpressionTextBox.Clear();
        ExpressionTextBox.Focus();
    }

    private void OnExpressionTextChanged(object sender, TextChangedEventArgs e) => ValidateInline();

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        ResultExpression = ExpressionTextBox.Text;
        DialogResult = true;
        Close();
    }

    private void InsertAtCaret(string fragment)
    {
        var caret = Math.Clamp(ExpressionTextBox.CaretIndex, 0, ExpressionTextBox.Text.Length);
        var text = ExpressionTextBox.Text;
        ExpressionTextBox.Text = text.Insert(caret, fragment);
        ExpressionTextBox.CaretIndex = caret + fragment.Length;
        ExpressionTextBox.Focus();
        ValidateInline();
    }

    private void ValidateInline()
    {
        var source = ExpressionTextBox?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source))
        {
            ValidationText.Text = string.Empty;
            return;
        }

        var validation = ScadaExpressionValidator.Validate(source, _tagCatalog);
        ValidationText.Text = validation.IsValid
            ? "✓ Expression valide"
            : validation.Errors.FirstOrDefault() ?? "Expression invalide.";
        ValidationText.Foreground = validation.IsValid ? Brushes.ForestGreen : Brushes.Firebrick;
    }
}
