using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Commands;

/// <summary>Describes how an insertion tool acquires its initial bounds.</summary>
public enum InsertPlacementMode { Point, TwoPoint, ContextualSurface }

/// <summary>Defines one insertion tool independently of its WPF presentation.</summary>
public sealed record InsertToolDescriptor(
    string Id, string Label, string IconKey, string ToolTip, string FamilyId,
    string GroupLabel, bool IsEnabled, ScadaElementKind? ElementKind,
    InsertPlacementMode PlacementMode, string? Variant = null, string? DisabledReason = null);

/// <summary>Defines a stable level-one family in the Insert ribbon.</summary>
public sealed record RibbonFamilyDefinition(
    string Id, string Label, string IconKey, IReadOnlyList<InsertToolDescriptor> Tools);

/// <summary>Canonical catalog for all visible insertion families and tools.</summary>
public static class InsertToolCatalog
{
    private const string PlannedReason = "Outil planifie; implementation a venir";

    /// <summary>Creates the eight stable insertion families exposed by the editor.</summary>
    public static IReadOnlyList<RibbonFamilyDefinition> CreateDefault() =>
    [
        Family("text-values", "Texte et valeurs", "Icon.InsertFamily.TextValues",
            Enabled("insert.text", "Champ texte", "Icon.Tool.Text", "Inserer un champ texte statique", "Champs", ScadaElementKind.Text),
            Enabled("insert.input-text", "Entree texte", "Icon.Tool.Text", "Inserer un champ d'entree texte", "Champs", ScadaElementKind.InputText),
            Enabled("insert.input-numeric", "Entree num.", "Icon.Field.Numeric", "Inserer un champ d'entree numerique", "Champs", ScadaElementKind.InputNumeric),
            Planned("insert.numeric-display", "Affichage num.", "Icon.Field.NumericDisplay", "Champs"),
            Planned("insert.date-time", "Date/heure", "Icon.Field.DateTime", "Champs")),
        Family("shapes", "Formes", "Icon.InsertFamily.Shapes",
            Shape("insert.shape.rectangle", "Rectangle", "Icon.Shape.Rectangle", "Rectangle"),
            Shape("insert.shape.ellipse", "Ellipse", "Icon.Shape.Ellipse", "Ellipse"),
            Shape("insert.shape.circle", "Cercle", "Icon.Shape.Circle", "Circle"),
            Shape("insert.shape.triangle", "Triangle", "Icon.Shape.Triangle", "Triangle"),
            Shape("insert.shape.star", "Etoile", "Icon.Shape.Star", "Star"),
            Shape("insert.shape.line", "Ligne", "Icon.Shape.Line", "Line", InsertPlacementMode.TwoPoint),
            Shape("insert.shape.arrow", "Fleche", "Icon.Shape.Arrow", "Arrow", InsertPlacementMode.TwoPoint),
            Planned("insert.shape.rounded-rectangle", "Rectangle arrondi", "Icon.Shape.RoundedRectangle", "Formes"),
            Planned("insert.shape.arc", "Arc", "Icon.Shape.Arc", "Formes"),
            Planned("insert.shape.polyline", "Polyligne", "Icon.Shape.Polyline", "Formes"),
            Planned("insert.shape.polygon", "Polygone", "Icon.Shape.Polygon", "Formes")),
        Family("process", "Process", "Icon.InsertFamily.Process",
            Hmi("insert.hmi.indicator-lamp", "Voyant", "Icon.Hmi.IndicatorLamp", "IndicatorLamp"),
            Hmi("insert.hmi.bar-horizontal", "Barre H", "Icon.Hmi.BarHorizontal", "HorizontalBar"),
            Hmi("insert.hmi.bar-vertical", "Barre V", "Icon.Hmi.BarVertical", "VerticalBar"),
            Hmi("insert.hmi.tank", "Reservoir", "Icon.Hmi.Tank", "Tank"),
            Hmi("insert.hmi.pipe-horizontal", "Tuyau H", "Icon.Hmi.PipeHorizontal", "PipeHorizontal"),
            Hmi("insert.hmi.pipe-vertical", "Tuyau V", "Icon.Hmi.PipeVertical", "PipeVertical"),
            Hmi("insert.hmi.valve", "Vanne", "Icon.Hmi.Valve", "Valve"),
            Hmi("insert.hmi.pump", "Pompe", "Icon.Hmi.Pump", "Pump"),
            Hmi("insert.hmi.motor", "Moteur", "Icon.Hmi.Motor", "Motor"),
            Hmi("insert.hmi.fan", "Ventil.", "Icon.Hmi.Fan", "Fan"),
            Hmi("insert.hmi.conveyor", "Convoy.", "Icon.Hmi.Conveyor", "Conveyor"),
            Hmi("insert.hmi.gauge", "Jauge", "Icon.Hmi.Gauge", "Gauge")),
        Family("electrical", "Electrique", "Icon.InsertFamily.Electrical",
            Hmi("insert.hmi.switch", "Interrup.", "Icon.Hmi.Switch", "Switch", "HMI electrique"),
            Hmi("insert.hmi.breaker", "Disjonct.", "Icon.Hmi.Breaker", "Breaker", "HMI electrique"),
            Hmi("insert.hmi.transformer", "Transfo", "Icon.Hmi.Transformer", "Transformer", "HMI electrique"),
            Hmi("insert.hmi.alarm-beacon", "Alarme", "Icon.Hmi.AlarmBeacon", "AlarmBeacon", "HMI electrique")),
        Family("commands", "Commandes", "Icon.InsertFamily.Commands",
            Button("insert.button.command", "Bouton", "Icon.Button.Command", "Command"),
            Button("insert.button.toggle", "Bascule", "Icon.Button.Toggle", "Toggle"),
            Button("insert.button.navigation", "Nav", "Icon.Button.Navigation", "Navigation"),
            Button("insert.button.alarm-ack", "Acquitter", "Icon.Button.AlarmAck", "AlarmAcknowledge"),
            Button("insert.button.emergency-stop", "Arret", "Icon.Button.EmergencyStop", "EmergencyStop"),
            Planned("insert.slider", "Curseur", "Icon.Command.Slider", "Boutons"),
            Planned("insert.checkbox", "Case", "Icon.Command.CheckBox", "Boutons"),
            Planned("insert.radio", "Radio", "Icon.Command.Radio", "Boutons")),
        Family("data", "Donnees", "Icon.InsertFamily.Data",
            Enabled("insert.table", "Tableau", "Icon.Data.Table", "Ouvrir les outils Tableau", "Tableaux", ScadaElementKind.Table, InsertPlacementMode.ContextualSurface),
            Planned("insert.list", "Liste", "Icon.Data.List", "Donnees"),
            Planned("insert.recipe", "Recette", "Icon.Data.Recipe", "Donnees"),
            Planned("insert.messages", "Messages", "Icon.Data.Messages", "Donnees")),
        Family("charts", "Graphiques", "Icon.InsertFamily.Charts",
            Planned("insert.trend", "Tendance", "Icon.Chart.Trend", "Graphiques"),
            Planned("insert.chart", "Graphique", "Icon.Chart.General", "Graphiques"),
            Planned("insert.histogram", "Histogramme", "Icon.Chart.Histogram", "Graphiques"),
            Planned("insert.alarms", "Alarmes", "Icon.Chart.Alarms", "Graphiques")),
        Family("media", "Media", "Icon.InsertFamily.Media",
            Planned("insert.image", "Image", "Icon.Media.Image", "Media"),
            Planned("insert.panel", "Panneau", "Icon.Media.Panel", "Media"),
            Planned("insert.web-browser", "Navigateur Web", "Icon.Media.WebBrowser", "Media"))
    ];

    /// <summary>Finds one tool by its stable command id.</summary>
    public static InsertToolDescriptor? Find(string commandId) => CreateDefault()
        .SelectMany(family => family.Tools)
        .FirstOrDefault(tool => string.Equals(tool.Id, commandId, StringComparison.Ordinal));

    private static RibbonFamilyDefinition Family(string id, string label, string iconKey, params InsertToolDescriptor[] tools) => new(id, label, iconKey, tools);

    private static InsertToolDescriptor Enabled(string id, string label, string iconKey, string toolTip, string group,
        ScadaElementKind kind, InsertPlacementMode placementMode = InsertPlacementMode.Point, string? variant = null) =>
        new(id, label, iconKey, toolTip, FamilyId(id), group, true, kind, placementMode, variant);

    private static InsertToolDescriptor Shape(string id, string label, string iconKey, string variant,
        InsertPlacementMode placementMode = InsertPlacementMode.Point) =>
        Enabled(id, label, iconKey, $"Inserer une forme {label.ToLowerInvariant()} Element+", "Formes", ScadaElementKind.Shape, placementMode, variant);

    private static InsertToolDescriptor Hmi(string id, string label, string iconKey, string variant, string group = "HMI process") =>
        Enabled(id, label, iconKey, $"Inserer {label} HMI Element+", group, ScadaElementKind.Shape, InsertPlacementMode.Point, variant);

    private static InsertToolDescriptor Button(string id, string label, string iconKey, string variant) =>
        Enabled(id, label, iconKey, $"Inserer {label} Element+", "Boutons", ScadaElementKind.Button, InsertPlacementMode.Point, variant);

    private static InsertToolDescriptor Planned(string id, string label, string iconKey, string group) =>
        new(id, label, iconKey, PlannedReason, FamilyId(id), group, false, null, InsertPlacementMode.Point, DisabledReason: PlannedReason);

    private static string FamilyId(string id)
    {
        if (id.StartsWith("insert.shape.", StringComparison.Ordinal)) return "shapes";
        if (id.StartsWith("insert.hmi.", StringComparison.Ordinal)) return id is "insert.hmi.switch" or "insert.hmi.breaker" or "insert.hmi.transformer" or "insert.hmi.alarm-beacon" ? "electrical" : "process";
        if (id.StartsWith("insert.button.", StringComparison.Ordinal) || id is "insert.slider" or "insert.checkbox" or "insert.radio") return "commands";
        if (id is "insert.table" or "insert.list" or "insert.recipe" or "insert.messages") return "data";
        if (id is "insert.trend" or "insert.chart" or "insert.histogram" or "insert.alarms") return "charts";
        if (id is "insert.image" or "insert.panel" or "insert.web-browser") return "media";
        return "text-values";
    }
}
