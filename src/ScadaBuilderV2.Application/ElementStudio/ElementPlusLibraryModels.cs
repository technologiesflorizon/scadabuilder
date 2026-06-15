using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.ElementStudio;

public sealed record ElementPlusLibraryItem(
    string ComponentId,
    string Name,
    string? Category,
    ElementStudioComponentVisualKind VisualKind,
    SceneBounds Bounds,
    int PartCount,
    string FilePath,
    string? PreviewMarkup,
    DateTimeOffset ModifiedAt,
    IReadOnlyList<string> Tags)
{
    public string FileName => Path.GetFileName(FilePath);

    public string IconText => string.IsNullOrWhiteSpace(Name)
        ? "E+"
        : string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0])));

    public string DisplayName => string.IsNullOrWhiteSpace(Category)
        ? Name
        : $"{Name} ({Category})";

    public string DetailText => $"{VisualKind} - {PartCount} composant(s)";
}

public sealed record ElementPlusLibrarySnapshot(
    IReadOnlyList<ElementPlusLibraryItem> Items,
    IReadOnlyList<string> Diagnostics);
