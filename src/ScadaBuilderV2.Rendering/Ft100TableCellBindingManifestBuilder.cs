using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Rendering;

/// <summary>Manifest data for one numeric table-cell runtime target.</summary>
public sealed record Ft100TableCellBindingData(
    string Placeholder,
    string? DisplayFormat,
    bool IsReadOnly,
    double? Min,
    double? Max,
    double? Step);

/// <summary>Manifest value bindings for one numeric table-cell runtime target.</summary>
public sealed record Ft100TableCellValueBindings(string? ReadTagId, string? WriteTagId);

/// <summary>One entry in an Element+ table object's <c>TableCellBindings</c> collection.</summary>
public sealed record Ft100TableCellBindingManifestEntry(
    int Row,
    int Column,
    string TargetId,
    string Kind,
    Ft100TableCellBindingData Data,
    Ft100TableCellValueBindings ValueBindings);

/// <summary>Builds manifest entries for bound numeric table-cell anchors.</summary>
/// <remarks>Decisions: DEC-0042. Contracts: docs/superpowers/specs/2026-07-15-table-cell-numeric-input-tf100web-design.md. Tests: tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.</remarks>
public static class Ft100TableCellBindingManifestBuilder
{
    /// <summary>Builds the bound numeric anchors of one table without creating synthetic manifest objects.</summary>
    public static IReadOnlyList<Ft100TableCellBindingManifestEntry> Build(
        ScadaElement tableElement,
        ScadaTagCatalog? tagCatalog,
        ICollection<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(tableElement);
        ArgumentNullException.ThrowIfNull(warnings);
        if (tableElement.Kind != ScadaElementKind.Table || tableElement.Table is null)
        {
            return [];
        }

        var tags = (tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag.Id))
            .GroupBy(tag => tag.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var result = new List<Ft100TableCellBindingManifestEntry>();
        foreach (var cell in tableElement.Table.EffectiveCells
                     .Where(cell => cell.ValueBindings is not null)
                     .OrderBy(cell => cell.Row)
                     .ThenBy(cell => cell.Column))
        {
            var binding = cell.ValueBindings!;
            var readTagId = Normalize(binding.ReadTagId);
            var writeTagId = Normalize(binding.WriteTagId);
            if (readTagId is null && writeTagId is null)
            {
                continue;
            }

            if (cell.EffectiveContent.Kind != ScadaTableCellContentKind.InputNumeric)
            {
                warnings.Add($"Tableau '{tableElement.Id}', cellule [{cell.Row},{cell.Column}]: binding ignore car la cible n'est pas InputNumeric.");
                continue;
            }

            WarnForTag(tableElement.Id, cell, readTagId, write: false, tags, warnings);
            WarnForTag(tableElement.Id, cell, writeTagId, write: true, tags, warnings);
            var content = cell.EffectiveContent;
            result.Add(new Ft100TableCellBindingManifestEntry(
                cell.Row,
                cell.Column,
                $"{CssIdentifier(tableElement.Id)}__cell-{cell.Row}-{cell.Column}",
                nameof(ScadaTableCellContentKind.InputNumeric),
                new Ft100TableCellBindingData(
                    content.Placeholder,
                    Normalize(content.DisplayFormat),
                    content.IsReadOnly,
                    content.Minimum,
                    content.Maximum,
                    content.Step),
                new Ft100TableCellValueBindings(readTagId, writeTagId)));
        }

        return result;
    }

    private static void WarnForTag(
        string tableId,
        ScadaTableCell cell,
        string? tagId,
        bool write,
        IReadOnlyDictionary<string, ScadaTagDefinition> tags,
        ICollection<string> warnings)
    {
        if (tagId is null)
        {
            return;
        }

        if (!tags.TryGetValue(tagId, out var tag) || !tag.Enabled)
        {
            warnings.Add($"Tableau '{tableId}', cellule [{cell.Row},{cell.Column}]: tag {(write ? "d'ecriture" : "de lecture")} actif '{tagId}' introuvable.");
        }
        else if (write && !tag.Writeable)
        {
            warnings.Add($"Tableau '{tableId}', cellule [{cell.Row},{cell.Column}]: tag d'ecriture '{tagId}' non ecrivable.");
        }
    }

    private static string CssIdentifier(string value) =>
        string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
