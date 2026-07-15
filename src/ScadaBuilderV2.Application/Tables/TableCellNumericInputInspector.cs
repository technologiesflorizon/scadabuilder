using System.Text.RegularExpressions;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Tables;

/// <summary>Describes the contextual numeric-input state of a table cell selection.</summary>
public sealed record TableCellNumericInputInspection(
    bool HasSingleAnchor,
    bool IsNumericInput,
    bool CanEditProperties,
    int? AnchorRow,
    int? AnchorColumn,
    ScadaTableCellContent? Content,
    ScadaTableCellValueBindings? ValueBindings,
    string ReadBindingSummary,
    string WriteBindingSummary,
    IReadOnlyList<ScadaTagDefinition> ReadTags,
    IReadOnlyList<ScadaTagDefinition> WriteTags,
    string? Diagnostic = null);

/// <summary>Builds the shared ribbon, properties-panel, and dialog state for one numeric table cell.</summary>
/// <remarks>Decisions: DEC-0042. Contracts: docs/superpowers/specs/2026-07-15-table-cell-numeric-input-tf100web-design.md. Tests: tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs.</remarks>
public static partial class TableCellNumericInputInspector
{
    /// <summary>Inspects an Element+ table selection and available active tags.</summary>
    public static TableCellNumericInputInspection Inspect(
        ScadaElement element,
        ScadaTableRange? range,
        ScadaTagCatalog? tagCatalog)
    {
        var active = (tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
            .Where(tag => tag.Enabled && !string.IsNullOrWhiteSpace(tag.Id))
            .OrderBy(tag => tag.AuthoringLabel, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var writable = active.Where(tag => tag.Writeable).ToArray();

        if (element.Kind != ScadaElementKind.Table || element.Table is null || range is null)
        {
            return Empty(active, writable, "Selectionnez une cellule d'un tableau Element+.");
        }

        var anchors = element.Table.EffectiveCells
            .Where(cell => Intersects(cell, range))
            .Distinct()
            .ToArray();
        if (anchors.Length != 1)
        {
            return Empty(active, writable, "Selectionnez une seule cellule ancre pour modifier ses bindings.");
        }

        var anchor = anchors[0];
        var content = anchor.EffectiveContent;
        var binding = anchor.ValueBindings;
        return new TableCellNumericInputInspection(
            true,
            content.Kind == ScadaTableCellContentKind.InputNumeric,
            content.Kind == ScadaTableCellContentKind.InputNumeric,
            anchor.Row,
            anchor.Column,
            content,
            binding,
            ResolveSummary(binding?.ReadTagId, active),
            ResolveSummary(binding?.WriteTagId, active),
            active,
            writable,
            content.Kind == ScadaTableCellContentKind.InputNumeric
                ? null
                : "Convertissez la cellule en InputNumeric pour configurer ses bindings.");
    }

    /// <summary>Validates numeric input properties using the standard authoring constraints.</summary>
    public static string? ValidateContent(ScadaTableCellContent content)
    {
        if (content.Kind != ScadaTableCellContentKind.InputNumeric)
        {
            return "Le contenu doit etre de type InputNumeric.";
        }

        foreach (var value in new[] { content.NumericValue, content.Minimum, content.Maximum, content.Step })
        {
            if (value.HasValue && !double.IsFinite(value.Value))
            {
                return "Les valeurs numeriques doivent etre finies.";
            }
        }

        if (content.Minimum.HasValue && content.Maximum.HasValue && content.Minimum > content.Maximum)
        {
            return "Le minimum ne peut pas depasser le maximum.";
        }

        if (content.Step.HasValue && content.Step <= 0)
        {
            return "Le pas doit etre superieur a zero.";
        }

        if (content.NumericValue.HasValue && content.Minimum.HasValue && content.NumericValue < content.Minimum)
        {
            return "La valeur initiale est inferieure au minimum.";
        }

        if (content.NumericValue.HasValue && content.Maximum.HasValue && content.NumericValue > content.Maximum)
        {
            return "La valeur initiale est superieure au maximum.";
        }

        if (!string.IsNullOrWhiteSpace(content.DisplayFormat) && !SupportedDisplayFormat().IsMatch(content.DisplayFormat.Trim()))
        {
            return "Le format d'affichage numerique n'est pas supporte.";
        }

        return null;
    }

    [GeneratedRegex("^(?:#+(?:\\.#+)?|fixed:[0-9]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SupportedDisplayFormat();

    private static TableCellNumericInputInspection Empty(
        IReadOnlyList<ScadaTagDefinition> active,
        IReadOnlyList<ScadaTagDefinition> writable,
        string diagnostic) =>
        new(false, false, false, null, null, null, null, "Aucun", "Aucun", active, writable, diagnostic);

    private static string ResolveSummary(string? tagId, IReadOnlyList<ScadaTagDefinition> tags)
    {
        if (string.IsNullOrWhiteSpace(tagId))
        {
            return "Aucun";
        }

        var tag = tags.FirstOrDefault(candidate => string.Equals(candidate.Id, tagId, StringComparison.Ordinal));
        return tag is null ? $"{tagId} (indisponible)" : tag.AuthoringLabel;
    }

    private static bool Intersects(ScadaTableCell cell, ScadaTableRange range) =>
        cell.Row <= range.EndRow && cell.Row + cell.RowSpan - 1 >= range.StartRow &&
        cell.Column <= range.EndColumn && cell.Column + cell.ColumnSpan - 1 >= range.StartColumn;
}
