namespace ScadaBuilderV2.Application.Commands;

/// <summary>
/// Defines one command group displayed by a ribbon tab.
/// </summary>
public sealed record RibbonGroupDefinition(
    string Label,
    IReadOnlyList<RibbonCommandDefinition> Commands);

/// <summary>
/// Defines one command displayed by a ribbon surface.
/// </summary>
public sealed record RibbonCommandDefinition(
    string Id,
    string Label,
    string IconKey,
    string ToolTip,
    bool IsEnabled,
    string? DisabledReason = null);

/// <summary>
/// Provides the default top-ribbon command metadata without depending on WPF controls.
/// </summary>
public static class RibbonCommandCatalog
{
    /// <summary>
    /// Creates the default top-ribbon tabs consumed by the WPF shell.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<RibbonGroupDefinition>> CreateDefault()
    {
        return new Dictionary<string, IReadOnlyList<RibbonGroupDefinition>>(StringComparer.Ordinal)
        {
            ["File"] =
            [
                Group("Projet",
                    Disabled("project.new", "Nouveau", "Icon.Project.New", "Creation de projet a venir"),
                    Disabled("project.open", "Ouvrir", "Icon.Project.Open", "Ouverture de projet a venir"),
                    Enabled("project.save", "Enregistrer", "Icon.Project.Save", "Enregistrer la scene active")),
                Group("Import",
                    Disabled("import.legacy", "Legacy", "Icon.Import.Legacy", "Import legacy dedie a venir"),
                    Enabled("import.tags", "Tags", "Icon.Import.Tags", "Importer les tags SCADA")),
                Group("Export",
                    Enabled("export.ft100.folder", "Dossier", "Icon.Export.Folder", "Exporter dossier FT100"),
                    Enabled("export.ft100.sb2", ".sb2", "Icon.Export.Package", "Exporter package FT100 .sb2"))
            ],
            ["Edit"] =
            [
                Group("Historique",
                    Enabled("edit.undo", "Annuler", "Icon.Edit.Undo", "Annuler la derniere operation"),
                    Enabled("edit.redo", "Retablir", "Icon.Edit.Redo", "Retablir la derniere operation annulee")),
                Group("Presse-papiers",
                    Disabled("edit.copy", "Copier", "Icon.Edit.Copy", "Commande a venir"),
                    Disabled("edit.paste", "Coller", "Icon.Edit.Paste", "Commande a venir")),
                Group("Interface",
                    Disabled("panel.restore", "Panneaux", "Icon.Panel.Restore", "Commande a venir"))
            ],
            ["Pages"] =
            [
                Group("Gestion",
                    Enabled("page.new", "Nouveau", "Icon.Page.New", "Créer une nouvelle page native"),
                    Enabled("page.rename", "Renommer", "Icon.Page.Rename", "Modifier le titre de la page sélectionnée"),
                    Enabled("page.duplicate", "Dupliquer", "Icon.Page.Duplicate", "Dupliquer complètement la page sélectionnée"),
                    Enabled("page.delete", "Supprimer", "Icon.Page.Delete", "Supprimer la page après vérification de ses dépendances")),
                Group("Inspection",
                    Enabled("page.properties", "Propriétés", "Icon.Page.Properties", "Afficher les propriétés de la page sélectionnée"),
                    Enabled("page.validate", "Valider", "Icon.Page.Validate", "Valider les pages et leurs dépendances"))
            ],
            ["Screen"] =
            [
                Group("Apercu",
                    Disabled("view.desktop", "Desktop", "Icon.View.Desktop", "Mode desktop a venir"),
                    Disabled("view.tablet", "Tablette", "Icon.View.Tablet", "Mode tablette a venir"),
                    Disabled("view.mobile", "Mobile", "Icon.View.Mobile", "Mode mobile a venir"),
                    Disabled("view.rotate", "Rotation", "Icon.View.Rotate", "Rotation a venir")),
                Group("Mesure",
                    Disabled("view.measure", "Mesures", "Icon.View.Measure", "Mesures a venir"))
            ],
            ["Selection"] =
            [
                Group("Objets",
                    Enabled("object.group", "Grouper", "Icon.Selection.Group", "Grouper les Element+ selectionnes"),
                    Enabled("object.ungroup", "Degrouper", "Icon.Selection.Ungroup", "Degrouper le groupe Element+ selectionne"),
                    Enabled("object.lock", "Verrou", "Icon.Object.Lock", "Verrouiller ou deverrouiller la position des Element+ selectionnes")),
                Group("Ordre",
                    Disabled("layer.forward", "Avant", "Icon.Layer.Forward", "Avancer a venir"),
                    Disabled("layer.backward", "Arriere", "Icon.Layer.Backward", "Reculer a venir"))
            ],
            ["Tools"] =
            [
                Group("Edition",
                    Disabled("tool.select", "Selection", "Icon.Tool.Select", "Selection outillee a venir"),
                    Disabled("tool.move", "Deplacer", "Icon.Tool.Move", "Deplacement outille a venir"),
                    Disabled("tool.text", "Texte", "Icon.Tool.Text", "Outil texte a venir"),
                    Disabled("tool.image", "Image", "Icon.Tool.Image", "Outil image a venir"),
                    Disabled("tool.zoom", "Zoom", "Icon.Tool.Zoom", "Zoom outille a venir")),
                Group("Configuration",
                    Enabled("tool.settings", "Configurer", "Icon.Tool.Settings", "Ouvrir la configuration (librairies)"),
                    Enabled("tool.element-studio", "Studio E+", "Icon.Tool.ElementStudio", "Ouvrir Studio Element+ (editeur de composants Element+)"))
            ],
            ["Insert"] = CreateInsertGroups()
        };
    }

    /// <summary>
    /// Creates the default left-side editor tool palette consumed by the WPF shell.
    /// </summary>
    public static IReadOnlyList<RibbonCommandDefinition> CreateToolPalette()
    {
        return
        [
            Disabled("tool.select", "Select", "Icon.Tool.Select", "Selection outillee a venir"),
            Disabled("tool.move", "Move", "Icon.Tool.Move", "Deplacement outille a venir"),
            Disabled("tool.text", "Texte", "Icon.Tool.Text", "Outil texte a venir"),
            Disabled("tool.image", "Image", "Icon.Tool.Image", "Outil image a venir"),
            Disabled("tool.group", "Groupe", "Icon.Tool.Group", "Outil groupe a venir"),
            Disabled("tool.zoom", "Zoom", "Icon.Tool.Zoom", "Zoom outille a venir")
        ];
    }

    /// <summary>Creates the stable level-one insertion families.</summary>
    public static IReadOnlyList<RibbonFamilyDefinition> CreateInsertFamilies() => InsertToolCatalog.CreateDefault();

    /// <summary>
    /// Enumerates every command from a ribbon tab collection.
    /// </summary>
    public static IEnumerable<RibbonCommandDefinition> EnumerateCommands(
        IReadOnlyDictionary<string, IReadOnlyList<RibbonGroupDefinition>> tabs)
    {
        return tabs.Values.SelectMany(groups => groups).SelectMany(group => group.Commands);
    }

    private static RibbonGroupDefinition Group(string label, params RibbonCommandDefinition[] commands)
    {
        return new RibbonGroupDefinition(label, commands);
    }

    private static IReadOnlyList<RibbonGroupDefinition> CreateInsertGroups() => InsertToolCatalog.CreateDefault()
        .SelectMany(family => family.Tools)
        .GroupBy(tool => tool.GroupLabel, StringComparer.Ordinal)
        .Select(group => new RibbonGroupDefinition(
            group.Key,
            group.Select(tool => new RibbonCommandDefinition(
                tool.Id, tool.Label, tool.IconKey, tool.ToolTip, tool.IsEnabled, tool.DisabledReason)).ToArray()))
        .ToArray();

    private static RibbonCommandDefinition Enabled(string id, string label, string iconKey, string toolTip)
    {
        return new RibbonCommandDefinition(id, label, iconKey, toolTip, true);
    }

    private static RibbonCommandDefinition Disabled(string id, string label, string iconKey, string disabledReason)
    {
        return new RibbonCommandDefinition(id, label, iconKey, disabledReason, false, disabledReason);
    }
}
