using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Tables;

/// <summary>Builds the spreadsheet-style context menu for a selected table range.</summary>
public static class TableContextMenuProvider
{
    public static IReadOnlyList<EditorCommandDescriptor> Build(
        ScadaTableDefinition table,
        ScadaTableRange range,
        bool canPaste)
    {
        var anchor = table.EffectiveCells.FirstOrDefault(cell => cell.Covers(range.StartRow, range.StartColumn));
        var isMergedAnchor = anchor is { RowSpan: > 1 } or { ColumnSpan: > 1 };
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
            new("table.row.height", "Hauteur de rangee...", "table"),
            new("table.column.width", "Largeur de colonne...", "table"),
            new("table.merge", "Fusionner les cellules", "table", canMerge && !isMergedAnchor,
                DisabledReason: canMerge ? "La plage contient deja une cellule fusionnee." : "Selectionnez plusieurs cellules."),
            new("table.unmerge", "Defusionner les cellules", "table", isMergedAnchor,
                DisabledReason: "La cellule active n'est pas fusionnee."),
            new("table.properties", "Proprietes du tableau...", "table")
        ];
    }
}
