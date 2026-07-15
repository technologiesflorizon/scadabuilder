namespace ScadaBuilderV2.Domain.Scenes;

/// <summary>Provides the structural table-editing boundary introduced by DEC-0040.</summary>
public static class ScadaTableStructureOperations
{
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
