using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Tables;

/// <summary>Builds the spreadsheet-style context menu for a selected table range.</summary>
public static class TableContextMenuProvider
{
    /// <summary>Creates the commands and enablement state for one rectangular table selection.</summary>
    /// <remarks>Decisions: DEC-0039. Contracts: docs/04_editor/COMMANDS_CONTRACT_V2.md. Tests: tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs.</remarks>
    public static IReadOnlyList<EditorCommandDescriptor> Build(
        ScadaTableDefinition table,
        ScadaTableRange range,
        bool canPaste)
    {
        var containsMergedCells = ScadaTableStructureOperations.ContainsMergedCells(table, range);
        var canMerge = range.RowCount > 1 || range.ColumnCount > 1;

        return
        [
            new("table.copy", "Copier", "table"),
            new("table.paste", "Coller", "table", IsEnabled: canPaste, DisabledReason: "Le presse-papiers ne contient aucune plage compatible."),
            new("table.tracks", "Inserer", "table", Children:
            [
                new EditorCommandDescriptor("table.row.insert", "Inserer une rangee", "table"),
                new EditorCommandDescriptor("table.column.insert", "Inserer une colonne", "table")
            ]),
            new("table.delete-tracks", "Supprimer", "table", Children:
            [
                new EditorCommandDescriptor("table.row.delete", "Supprimer la rangee", "table", table.EffectiveRows.Count > 1),
                new EditorCommandDescriptor("table.column.delete", "Supprimer la colonne", "table", table.EffectiveColumns.Count > 1)
            ]),
            new("table.clear", "Effacer le contenu", "table"),
            new("table.format", "Format de cellule...", "table"),
            new("table.content.properties", "Type et contenu...", "table"),
            new("table.borders", "Bordures...", "table"),
            new("table.row.height", "Hauteur de rangee...", "table"),
            new("table.column.width", "Largeur de colonne...", "table"),
            new("table.equalize", "Uniformiser les pistes", "table"),
            new("table.distribute", "Distribuer proportionnellement", "table", Children:
            [
                new EditorCommandDescriptor("table.distribute.rows", "Distribuer les rangees...", "table"),
                new EditorCommandDescriptor("table.distribute.columns", "Distribuer les colonnes...", "table")
            ]),
            new("table.headers", "Rangees d'en-tete...", "table"),
            new("table.header.mark", "Marquer comme en-tete", "table", range.StartRow >= table.EffectiveRows.TakeWhile(row => row.IsHeader).Count()),
            new("table.header.unmark", "Demarquer l'en-tete", "table", range.StartRow < table.EffectiveRows.TakeWhile(row => row.IsHeader).Count()),
            new("table.merge-toggle", containsMergedCells ? "Defusionner les cellules" : "Fusionner les cellules", "table", containsMergedCells || canMerge,
                DisabledReason: "Selectionnez plusieurs cellules."),
            new("table.properties", "Proprietes du tableau...", "table")
        ];
    }
}
