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
                    Disabled("object.lock", "Verrou", "Icon.Object.Lock", "Verrouiller l'objet a venir")),
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
                    Disabled("tool.settings", "Configurer", "Icon.Tool.Settings", "Configurer les outils a venir"))
            ],
            ["Insert"] =
            [
                Group("Champs",
                    Enabled("insert.text", "Champ texte", "Icon.Tool.Text", "Inserer un champ texte statique"),
                    Enabled("insert.input-text", "Entree texte", "Icon.Tool.Text", "Inserer un champ d'entree texte"),
                    Enabled("insert.input-numeric", "Entree num.", "Icon.Field.Numeric", "Inserer un champ d'entree numerique")),
                Group("Formes",
                    Enabled("insert.shape.rectangle", "Rectangle", "Icon.Shape.Rectangle", "Inserer un rectangle Element+"),
                    Enabled("insert.shape.ellipse", "Ellipse", "Icon.Shape.Ellipse", "Inserer une ellipse Element+"),
                    Enabled("insert.shape.line", "Ligne", "Icon.Shape.Line", "Inserer une ligne Element+"),
                    Enabled("insert.shape.arrow", "Fleche", "Icon.Shape.Arrow", "Inserer une fleche Element+")),
                Group("HMI process",
                    Enabled("insert.hmi.indicator-lamp", "Voyant", "Icon.Hmi.IndicatorLamp", "Inserer un voyant HMI Element+"),
                    Enabled("insert.hmi.bar-horizontal", "Barre H", "Icon.Hmi.BarHorizontal", "Inserer une barre horizontale HMI Element+"),
                    Enabled("insert.hmi.bar-vertical", "Barre V", "Icon.Hmi.BarVertical", "Inserer une barre verticale HMI Element+"),
                    Enabled("insert.hmi.tank", "Reservoir", "Icon.Hmi.Tank", "Inserer un reservoir HMI Element+"),
                    Enabled("insert.hmi.pipe-horizontal", "Tuyau H", "Icon.Hmi.PipeHorizontal", "Inserer un tuyau horizontal HMI Element+"),
                    Enabled("insert.hmi.pipe-vertical", "Tuyau V", "Icon.Hmi.PipeVertical", "Inserer un tuyau vertical HMI Element+"),
                    Enabled("insert.hmi.valve", "Vanne", "Icon.Hmi.Valve", "Inserer une vanne HMI Element+"),
                    Enabled("insert.hmi.pump", "Pompe", "Icon.Hmi.Pump", "Inserer une pompe HMI Element+"),
                    Enabled("insert.hmi.motor", "Moteur", "Icon.Hmi.Motor", "Inserer un moteur HMI Element+"),
                    Enabled("insert.hmi.fan", "Ventil.", "Icon.Hmi.Fan", "Inserer un ventilateur HMI Element+"),
                    Enabled("insert.hmi.conveyor", "Convoy.", "Icon.Hmi.Conveyor", "Inserer un convoyeur HMI Element+"),
                    Enabled("insert.hmi.gauge", "Jauge", "Icon.Hmi.Gauge", "Inserer une jauge HMI Element+")),
                Group("HMI electrique",
                    Enabled("insert.hmi.switch", "Interrup.", "Icon.Hmi.Switch", "Inserer un interrupteur electrique HMI Element+"),
                    Enabled("insert.hmi.breaker", "Disjonct.", "Icon.Hmi.Breaker", "Inserer un disjoncteur HMI Element+"),
                    Enabled("insert.hmi.transformer", "Transfo", "Icon.Hmi.Transformer", "Inserer un transformateur HMI Element+"),
                    Enabled("insert.hmi.alarm-beacon", "Alarme", "Icon.Hmi.AlarmBeacon", "Inserer une balise alarme HMI Element+")),
                Group("Boutons",
                    Enabled("insert.button.command", "Bouton", "Icon.Button.Command", "Inserer un bouton Element+"),
                    Enabled("insert.button.toggle", "Bascule", "Icon.Button.Toggle", "Inserer un bouton bascule Element+"),
                    Enabled("insert.button.navigation", "Nav", "Icon.Button.Navigation", "Inserer un bouton de navigation Element+"),
                    Enabled("insert.button.alarm-ack", "Acquitter", "Icon.Button.AlarmAck", "Inserer un bouton acquittement alarme Element+"),
                    Enabled("insert.button.emergency-stop", "Arret", "Icon.Button.EmergencyStop", "Inserer un bouton arret d'urgence Element+"))
            ]
        };
    }

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

    private static RibbonCommandDefinition Enabled(string id, string label, string iconKey, string toolTip)
    {
        return new RibbonCommandDefinition(id, label, iconKey, toolTip, true);
    }

    private static RibbonCommandDefinition Disabled(string id, string label, string iconKey, string disabledReason)
    {
        return new RibbonCommandDefinition(id, label, iconKey, disabledReason, false, disabledReason);
    }
}
