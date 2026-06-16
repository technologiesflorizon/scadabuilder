namespace ScadaBuilderV2.Application.Selection;

public sealed record LegacyElementListItem(
    string Id,
    string DisplayName,
    string ElementType,
    double X = 0,
    double Y = 0,
    double Width = 0,
    double Height = 0,
    string Text = "",
    bool IsTextLike = false,
    string FontFamily = "",
    double FontSize = 0,
    string Foreground = "",
    string Background = "",
    string LegacyMarkup = "",
    string RawMetadataJson = "",
    int ZIndex = 0);

public sealed record SceneElementListItem(
    string Key,
    string Id,
    string DisplayName,
    string ElementType,
    string Source,
    string ParentId = "",
    int Depth = 0)
{
    public bool IsGroupChild => !string.IsNullOrWhiteSpace(ParentId);

    public string ListDisplayName => Depth <= 0
        ? DisplayName
        : $"{new string(' ', Depth * 2)}> {DisplayName}";
}

public sealed record SceneElementInventorySnapshot(
    IReadOnlyList<SceneElementListItem> Elements,
    IReadOnlySet<string> SelectedKeys)
{
    public const string SourceObjectPrefix = "source:";
    public const string SceneObjectPrefix = "object:";
    public const string SourceObjectKind = "Source";
    public const string SceneObjectKind = "Object";

    public static SceneElementInventorySnapshot FromElements(
        IEnumerable<LegacyElementListItem> legacyElements,
        IEnumerable<SceneElementListItem> modernElements,
        IEnumerable<string> selectedLegacyElementIds,
        string? selectedModernElementId,
        IEnumerable<string>? hiddenLegacyElementIds = null,
        IEnumerable<string>? selectedModernElementIds = null)
    {
        var selectedLegacy = selectedLegacyElementIds.ToHashSet(StringComparer.Ordinal);
        var hiddenLegacy = (hiddenLegacyElementIds ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        var selectedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in selectedLegacy)
        {
            selectedKeys.Add($"{SourceObjectPrefix}{id}");
        }

        var selectedModern = (selectedModernElementIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(selectedModernElementId))
        {
            selectedModern.Add(selectedModernElementId);
        }

        foreach (var id in selectedModern)
        {
            selectedKeys.Add($"{SceneObjectPrefix}{id}");
        }

        var legacyItems = legacyElements
            .GroupBy(element => element.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .Where(element => !hiddenLegacy.Contains(element.Id))
            .Select(element => new SceneElementListItem(
                $"{SourceObjectPrefix}{element.Id}",
                element.Id,
                $"{element.DisplayName} [Source]",
                element.ElementType,
                SourceObjectKind));

        var modernItems = modernElements
            .GroupBy(element => element.Key, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var sortedLegacyItems = legacyItems
            .OrderBy(element => element.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var allElements = modernItems
            .Concat(sortedLegacyItems)
            .ToArray();

        var validKeys = allElements.Select(element => element.Key).ToHashSet(StringComparer.Ordinal);
        return new SceneElementInventorySnapshot(
            allElements,
            selectedKeys.Where(validKeys.Contains).ToHashSet(StringComparer.Ordinal));
    }
}

public sealed record LegacyElementSelectionSnapshot(
    IReadOnlyList<LegacyElementListItem> Elements,
    IReadOnlySet<string> SelectedElementIds)
{
    public IReadOnlyList<LegacyElementListItem> SelectedElements =>
        Elements.Where(element => SelectedElementIds.Contains(element.Id)).ToArray();

    public string PropertySummary => SelectedElements.Count switch
    {
        0 => "Aucun element source selectionne",
        1 => $"{SelectedElements[0].DisplayName} ({SelectedElements[0].ElementType})",
        _ => $"{SelectedElements.Count} elements source selectionnes"
    };

    public static LegacyElementSelectionSnapshot FromInventory(
        IEnumerable<LegacyElementListItem> elements,
        IEnumerable<string> selectedElementIds)
    {
        var orderedElements = elements
            .GroupBy(element => element.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(element => element.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var availableIds = orderedElements.Select(element => element.Id).ToHashSet(StringComparer.Ordinal);
        var selectedIds = selectedElementIds
            .Where(availableIds.Contains)
            .ToHashSet(StringComparer.Ordinal);

        return new LegacyElementSelectionSnapshot(orderedElements, selectedIds);
    }
}
