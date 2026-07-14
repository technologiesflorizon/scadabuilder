using System;
using System.Linq;
using System.Windows;
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
        TagListBox.ItemsSource = (_catalog?.Tags ?? [])
            .Where(tag => tag.Enabled)
            .OrderBy(tag => tag.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(tag => new TagSelectionItem(
                tag.DisplayName,
                tag.Id,
                tag.Datatype ?? string.Empty,
                $"{tag.DisplayName}  ·  {tag.Id}  ·  {tag.Datatype ?? "type inconnu"}"))
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
