using System.Windows;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App;

public partial class ElementReadVariableDialog : Window
{
    private sealed record TagItem(string TagId, string DisplayName);

    private readonly ScadaTagCatalog? _tagCatalog;

    public ElementReadVariableDialog(ScadaReadVariableRule? existing, ScadaTagCatalog? tagCatalog)
    {
        InitializeComponent();
        _tagCatalog = tagCatalog;

        var enabledTags = (tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
            .Where(tag => tag.Enabled)
            .ToArray();
        // Shared guard (fix round 1, DEC-0052): a QuickWindow page's `#N` catalog is already numerically
        // ordered and must not be re-sorted by DisplayName, which would scramble it back to the lexical
        // #1, #10, #100... order this fixes.
        var orderedTags = QuickWindowPagePlaceholderCatalog.OrderForSelector(enabledTags, StringComparer.CurrentCultureIgnoreCase);
        var items = orderedTags
            .Select(tag => new TagItem(tag.Id, TagLabel(tag)))
            .ToArray();
        TagComboBox.ItemsSource = items;

        if (existing is not null)
        {
            TagComboBox.SelectedItem = items.FirstOrDefault(t => t.TagId == existing.TagId);
            DisplayFormatTextBox.Text = existing.DisplayFormat ?? string.Empty;
        }
        else if (items.Length > 0)
        {
            TagComboBox.SelectedIndex = 0;
        }
    }

    private static string TagLabel(ScadaTagDefinition tag)
    {
        var name = tag.KeywordLabel ?? tag.DisplayName ?? tag.Id;
        return string.IsNullOrWhiteSpace(tag.Device) ? name : $"{name} ({tag.Device})";
    }

    public ScadaReadVariableRule? Result { get; private set; }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (TagComboBox.SelectedItem is not TagItem tag)
        {
            ValidationText.Text = "Selectionnez un tag.";
            return;
        }

        Result = new ScadaReadVariableRule(
            tag.TagId,
            string.IsNullOrWhiteSpace(DisplayFormatTextBox.Text) ? null : DisplayFormatTextBox.Text.Trim());

        DialogResult = true;
    }
}
