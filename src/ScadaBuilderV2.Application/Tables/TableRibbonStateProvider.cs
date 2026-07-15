using ScadaBuilderV2.Application.Commands;

namespace ScadaBuilderV2.Application.Tables;

/// <summary>Builds the stable level-two contextual Table command groups.</summary>
public static class TableRibbonStateProvider
{
    public static IReadOnlyList<RibbonGroupDefinition> Create(TableAuthoringSession session)
    {
        var table = session.TableElementId is not null;
        const string reason = "Selectionnez un tableau Element+";
        RibbonCommandDefinition Command(string id, string label, string icon, string tip, bool enabled = true) =>
            enabled ? new(id, label, icon, tip, true) : new(id, label, icon, tip, false, reason);
        return
        [
            new("Creation", [Command("table.add", "Ajouter", "Icon.Data.Table", "Ajouter un tableau")]),
            new("Mode", [Command("table.mode.object", "Objet", "Icon.Selection.Select", "Deplacer ou redimensionner l'objet", table), Command("table.mode.cells", "Cellules", "Icon.Data.Table", "Selectionner et modifier les cellules", table)]),
            new("Selection", [Command("table.select.all", "Tout", "Icon.Selection.SelectAll", "Selectionner tout le tableau", table)]),
            new("Contenu", [Command("table.content.text", "Texte", "Icon.Tool.Text", "Convertir en texte", table), Command("table.content.input-text", "Input texte", "Icon.Tool.Text", "Convertir en input texte", table), Command("table.content.input-numeric", "Input num.", "Icon.Field.Numeric", "Convertir en input numerique", table), Command("table.content.properties", "Valeurs", "Icon.Tool.Properties", "Valeur initiale et contraintes", table)]),
            new("Structure", [Command("table.merge", "Fusionner", "Icon.Selection.Group", "Fusionner les cellules", table), Command("table.unmerge", "Defusionner", "Icon.Selection.Ungroup", "Defusionner", table), Command("table.row.insert", "+ Rangee", "Icon.Data.Table", "Inserer une rangee", table), Command("table.column.insert", "+ Colonne", "Icon.Data.Table", "Inserer une colonne", table), Command("table.row.delete", "- Rangee", "Icon.Edit.Delete", "Supprimer une rangee", table), Command("table.column.delete", "- Colonne", "Icon.Edit.Delete", "Supprimer une colonne", table)]),
            new("Format", [Command("table.format", "Format", "Icon.Tool.Properties", "Format complet de la portee", table), Command("table.borders", "Bordures", "Icon.Shape.Rectangle", "Bordures avancees", table), Command("table.format.reset", "Heriter", "Icon.Edit.Undo", "Reinitialiser la portee", table)]),
            new("Dimensions", [Command("table.row.height", "Hauteur", "Icon.Data.Table", "Hauteur des rangees", table), Command("table.column.width", "Largeur", "Icon.Data.Table", "Largeur des colonnes", table), Command("table.equalize", "Uniformiser", "Icon.Screen.Fit", "Uniformiser les pistes", table), Command("table.autofit", "Auto", "Icon.Screen.Fit", "Ajuster au contenu", table)]),
            new("En-tetes", [Command("table.headers", "En-tetes", "Icon.Data.Table", "Configurer les rangees d'en-tete", table), Command("table.properties", "Proprietes", "Icon.Tool.Properties", "Proprietes detaillees", table)]),
            new("Navigation", [Command("table.back", "Retour Donnees", "Icon.Edit.Undo", "Retour aux outils Donnees")])
        ];
    }
}
