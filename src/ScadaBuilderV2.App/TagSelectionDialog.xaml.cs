using System;
using System.Linq;
using System.Windows;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App;

public partial class TagSelectionDialog : Window
{
    private readonly ScadaTagCatalog? _catalog;

    public sealed record TagSelectionItem(string DisplayName, string TagId, string DataType, string DisplayLabel);

    public TagSelectionItem? SelectedTag { get; private set; }

    public TagSelectionDialog(ScadaTagCatalog? catalog)
    {
        InitializeComponent();
        _catalog = catalog;
        PopulateTags();
    }

    private void PopulateTags()
    {
        var enabledTags = (_catalog?.Tags ?? [])
            .Where(tag => tag.Enabled)
            .ToArray();
        // Shared guard (fix round 1, DEC-0052): a QuickWindow page's `#N` catalog is already numerically
        // ordered and must not be re-sorted by DisplayName, which would scramble it back to the lexical
        // #1, #10, #100... order this fixes.
        var orderedTags = QuickWindowPagePlaceholderCatalog.OrderForSelector(enabledTags, StringComparer.OrdinalIgnoreCase);
        var isPlaceholderCatalog = QuickWindowPagePlaceholderCatalog.IsPlaceholderCatalog(enabledTags);
        TagListBox.ItemsSource = orderedTags
            .Select(tag => new TagSelectionItem(
                tag.DisplayName,
                tag.Id,
                tag.Datatype ?? string.Empty,
                // Fix round 2 (bug report): a placeholder's DisplayName is now exactly its Id ("#1") - never
                // the sheet's source description, which composing `{tag.DisplayName}` into an expression
                // would have broken ("Caractere inattendu : '}'" on `{#1 — {BP_1A_AUTO}}`). The source
                // description lives in Device instead, shown here next to the #N so the operator still sees
                // what they are picking. A normal project tag keeps the original three-part label.
                isPlaceholderCatalog
                    ? $"{tag.DisplayName}  ·  {tag.Device}"
                    : $"{tag.DisplayName}  ·  {tag.Id}  ·  {tag.Datatype ?? "type inconnu"}"))
            .ToArray();
    }

    private void OnTagDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => SelectCurrentTag();

    private void OnSelectClick(object sender, RoutedEventArgs e) => SelectCurrentTag();

    private void SelectCurrentTag()
    {
        if (TagListBox.SelectedItem is not TagSelectionItem tag)
            return;

        SelectedTag = tag;
        DialogResult = true;
        Close();
    }
}
