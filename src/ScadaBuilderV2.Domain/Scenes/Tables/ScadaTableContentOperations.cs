using System.Globalization;

namespace ScadaBuilderV2.Domain.Scenes;

/// <summary>Applies deterministic table-cell content conversion and editing.</summary>
/// <remarks>Decisions: DEC-0040, DEC-0042. Contracts: docs/superpowers/specs/2026-07-15-table-cell-numeric-input-tf100web-design.md. Tests: TableContentOperationsTests.</remarks>
public static class ScadaTableContentOperations
{
    /// <summary>Sets one anchor content through the compatibility facade.</summary>
    public static ScadaTableDefinition SetContent(ScadaTableDefinition table, int row, int column, ScadaTableCellContent content) => ScadaTableOperations.SetContent(table, row, column, content);
    /// <summary>Clears every anchor in a range.</summary>
    public static ScadaTableDefinition ClearContent(ScadaTableDefinition table, ScadaTableRange range) => ScadaTableOperations.ClearContent(table, range);
    /// <summary>Converts every anchor in a range to the requested content kind.</summary>
    public static ScadaTableDefinition ConvertKind(ScadaTableDefinition table, ScadaTableRange range, ScadaTableCellContentKind kind)
    {
        if (kind != ScadaTableCellContentKind.InputNumeric &&
            table.EffectiveCells.Any(cell => range.Contains(cell.Row, cell.Column) && cell.ValueBindings is not null))
        {
            throw new InvalidOperationException("La conversion exige d'abord la suppression confirmee des bindings cellule.");
        }

        return table with
        {
            Cells = table.EffectiveCells.Select(cell => range.Contains(cell.Row, cell.Column)
                ? cell with { Content = Convert(cell.EffectiveContent, kind) }
                : cell).ToArray()
        };
    }

    /// <summary>Converts one content value without retaining incompatible hidden fields.</summary>
    public static ScadaTableCellContent Convert(ScadaTableCellContent source, ScadaTableCellContentKind target)
    {
        var text = source.Kind == ScadaTableCellContentKind.InputNumeric
            ? source.NumericValue?.ToString(CultureInfo.InvariantCulture) ?? ""
            : source.Text;
        if (target == ScadaTableCellContentKind.Text)
            return new(target, Text: text);
        if (target == ScadaTableCellContentKind.InputText)
            return new(target, Text: text, Placeholder: source.Kind == ScadaTableCellContentKind.Text ? "" : source.Placeholder, IsReadOnly: source.Kind != ScadaTableCellContentKind.Text && source.IsReadOnly);
        var numeric = source.Kind == ScadaTableCellContentKind.InputNumeric
            ? source.NumericValue
            : double.TryParse(source.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
        return new(target, Placeholder: source.Kind == ScadaTableCellContentKind.Text ? "" : source.Placeholder, NumericValue: numeric,
            Minimum: source.Kind == ScadaTableCellContentKind.InputNumeric ? source.Minimum : null,
            Maximum: source.Kind == ScadaTableCellContentKind.InputNumeric ? source.Maximum : null,
            Step: source.Kind == ScadaTableCellContentKind.InputNumeric ? source.Step : null,
            IsReadOnly: source.Kind != ScadaTableCellContentKind.Text && source.IsReadOnly);
    }
}
