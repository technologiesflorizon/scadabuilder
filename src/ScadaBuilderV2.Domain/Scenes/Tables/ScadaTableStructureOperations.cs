namespace ScadaBuilderV2.Domain.Scenes;

/// <summary>Provides the structural table-editing boundary introduced by DEC-0040.</summary>
public static class ScadaTableStructureOperations
{
    public static ScadaTableDefinition Merge(ScadaTableDefinition table, ScadaTableRange range) => ScadaTableOperations.Merge(table, range);
    public static ScadaTableDefinition Unmerge(ScadaTableDefinition table, int row, int column) => ScadaTableOperations.Unmerge(table, row, column);
    public static ScadaTableDefinition InsertRow(ScadaTableDefinition table, int row) => ScadaTableOperations.InsertRow(table, row);
    public static ScadaTableDefinition InsertColumn(ScadaTableDefinition table, int column) => ScadaTableOperations.InsertColumn(table, column);
    public static ScadaTableDefinition DeleteRow(ScadaTableDefinition table, int row) => ScadaTableOperations.DeleteRow(table, row);
    public static ScadaTableDefinition DeleteColumn(ScadaTableDefinition table, int column) => ScadaTableOperations.DeleteColumn(table, column);
}
