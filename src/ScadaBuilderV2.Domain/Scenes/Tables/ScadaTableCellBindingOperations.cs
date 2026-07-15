namespace ScadaBuilderV2.Domain.Scenes;

/// <summary>Provides immutable operations for numeric bindings owned by anchored table cells.</summary>
/// <remarks>Decisions: DEC-0042. Contracts: docs/superpowers/specs/2026-07-15-table-cell-numeric-input-tf100web-design.md. Tests: tests/ScadaBuilderV2.Tests/TableCellBindingOperationsTests.cs.</remarks>
public static class ScadaTableCellBindingOperations
{
    /// <summary>Sets the binding of the numeric anchor covering the supplied coordinate.</summary>
    public static ScadaTableDefinition SetBinding(
        ScadaTableDefinition table,
        int row,
        int column,
        ScadaTableCellValueBindings binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        var target = ValidateEditableNumericTarget(table, row, column);
        var normalized = Normalize(binding);
        if (normalized is null)
        {
            return RemoveBinding(table, row, column);
        }

        return ReplaceCell(table, target, target with { ValueBindings = normalized });
    }

    /// <summary>Removes the binding from the anchor covering the supplied coordinate.</summary>
    public static ScadaTableDefinition RemoveBinding(ScadaTableDefinition table, int row, int column)
    {
        var target = FindAnchor(table, row, column);
        return target.ValueBindings is null
            ? table
            : ReplaceCell(table, target, target with { ValueBindings = null });
    }

    /// <summary>Gets the binding owned by the anchor covering the supplied coordinate.</summary>
    public static ScadaTableCellValueBindings? GetBinding(ScadaTableDefinition table, int row, int column) =>
        FindAnchor(table, row, column).ValueBindings;

    /// <summary>Enumerates every bound anchor in stable row-major order.</summary>
    public static IEnumerable<(ScadaTableCell Cell, ScadaTableCellValueBindings Binding)> EnumerateBindings(ScadaTableDefinition table) =>
        table.EffectiveCells
            .Where(cell => cell.ValueBindings is not null)
            .OrderBy(cell => cell.Row)
            .ThenBy(cell => cell.Column)
            .Select(cell => (cell, cell.ValueBindings!));

    /// <summary>Counts bound anchors, optionally restricted to anchors whose covered area intersects a range.</summary>
    public static int CountBindings(ScadaTableDefinition table, ScadaTableRange? range = null) =>
        EnumerateBindings(table).Count(item => range is null || Intersects(item.Cell, range));

    /// <summary>Returns the numeric anchor covering a coordinate or rejects a non-numeric target.</summary>
    public static ScadaTableCell ValidateEditableNumericTarget(ScadaTableDefinition table, int row, int column)
    {
        var target = FindAnchor(table, row, column);
        if (target.EffectiveContent.Kind != ScadaTableCellContentKind.InputNumeric)
        {
            throw new InvalidOperationException("La cellule doit etre de type InputNumeric pour porter un binding.");
        }

        return target;
    }

    private static ScadaTableCell FindAnchor(ScadaTableDefinition table, int row, int column)
    {
        if (row < 0 || column < 0 || row >= table.EffectiveRows.Count || column >= table.EffectiveColumns.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(row), "La coordonnee de cellule est hors du tableau.");
        }

        return table.EffectiveCells.FirstOrDefault(cell => cell.Covers(row, column))
            ?? throw new InvalidOperationException("La cellule selectionnee ne possede aucune ancre.");
    }

    private static ScadaTableCellValueBindings? Normalize(ScadaTableCellValueBindings binding)
    {
        var read = string.IsNullOrWhiteSpace(binding.ReadTagId) ? null : binding.ReadTagId.Trim();
        var write = string.IsNullOrWhiteSpace(binding.WriteTagId) ? null : binding.WriteTagId.Trim();
        return read is null && write is null ? null : new ScadaTableCellValueBindings(read, write);
    }

    private static bool Intersects(ScadaTableCell cell, ScadaTableRange range) =>
        cell.Row <= range.EndRow && cell.Row + cell.RowSpan - 1 >= range.StartRow &&
        cell.Column <= range.EndColumn && cell.Column + cell.ColumnSpan - 1 >= range.StartColumn;

    private static ScadaTableDefinition ReplaceCell(ScadaTableDefinition table, ScadaTableCell before, ScadaTableCell after) =>
        table with { Cells = table.EffectiveCells.Select(cell => cell == before ? after : cell).ToArray() };
}
