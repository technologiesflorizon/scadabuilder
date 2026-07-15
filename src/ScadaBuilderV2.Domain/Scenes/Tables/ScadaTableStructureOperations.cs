namespace ScadaBuilderV2.Domain.Scenes;

/// <summary>Provides the structural table-editing boundary introduced by DEC-0040.</summary>
public static class ScadaTableStructureOperations
{
    /// <summary>Returns whether the selection intersects at least one merged cell.</summary>
    public static bool ContainsMergedCells(ScadaTableDefinition table, ScadaTableRange range) =>
        table.EffectiveCells.Any(cell =>
            (cell.RowSpan > 1 || cell.ColumnSpan > 1) &&
            cell.Row <= range.EndRow && cell.Row + cell.RowSpan - 1 >= range.StartRow &&
            cell.Column <= range.EndColumn && cell.Column + cell.ColumnSpan - 1 >= range.StartColumn);

    /// <summary>Merges an unmerged range, or unmerges every merged cell intersecting the range.</summary>
    public static ScadaTableDefinition ToggleMerge(ScadaTableDefinition table, ScadaTableRange range)
    {
        var mergedCells = table.EffectiveCells
            .Where(cell =>
                (cell.RowSpan > 1 || cell.ColumnSpan > 1) &&
                cell.Row <= range.EndRow && cell.Row + cell.RowSpan - 1 >= range.StartRow &&
                cell.Column <= range.EndColumn && cell.Column + cell.ColumnSpan - 1 >= range.StartColumn)
            .Select(cell => (cell.Row, cell.Column))
            .ToArray();

        if (mergedCells.Length == 0)
        {
            if (range.RowCount == 1 && range.ColumnCount == 1)
                throw new InvalidOperationException("Selectionnez plusieurs cellules a fusionner.");
            return Merge(table, range);
        }

        return mergedCells.Aggregate(table, (current, cell) => Unmerge(current, cell.Row, cell.Column));
    }

    /// <summary>Merges a rectangular range.</summary>
    public static ScadaTableDefinition Merge(ScadaTableDefinition table, ScadaTableRange range) => ScadaTableOperations.Merge(table, range);
    /// <summary>Unmerges the cell covering a coordinate.</summary>
    public static ScadaTableDefinition Unmerge(ScadaTableDefinition table, int row, int column) => ScadaTableOperations.Unmerge(table, row, column);
    /// <summary>Inserts a row.</summary>
    public static ScadaTableDefinition InsertRow(ScadaTableDefinition table, int row) => ScadaTableOperations.InsertRow(table, row);
    /// <summary>Inserts a column.</summary>
    public static ScadaTableDefinition InsertColumn(ScadaTableDefinition table, int column) => ScadaTableOperations.InsertColumn(table, column);
    /// <summary>Deletes a row.</summary>
    public static ScadaTableDefinition DeleteRow(ScadaTableDefinition table, int row) => ScadaTableOperations.DeleteRow(table, row);
    /// <summary>Deletes a column.</summary>
    public static ScadaTableDefinition DeleteColumn(ScadaTableDefinition table, int column) => ScadaTableOperations.DeleteColumn(table, column);
}
