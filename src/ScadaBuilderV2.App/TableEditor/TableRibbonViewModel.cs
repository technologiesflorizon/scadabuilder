using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Tables;

namespace ScadaBuilderV2.App.TableEditor;

/// <summary>Projects the application-owned Table authoring session into the level-two ribbon surface.</summary>
internal sealed class TableRibbonViewModel
{
    private readonly TableAuthoringSession session;
    private TableCellNumericInputInspection? numericInspection;

    public TableRibbonViewModel(TableAuthoringSession session)
    {
        this.session = session;
        Refresh();
    }

    public IReadOnlyList<RibbonGroupDefinition> Groups { get; private set; } = [];
    public int CreationRows => session.CreationRows;
    public int CreationColumns => session.CreationColumns;
    public bool FirstRowIsHeader => session.FirstRowIsHeader;

    public void Open()
    {
        session.OpenSurface();
        Refresh();
    }

    public void BackToDataTools()
    {
        session.CloseSurface();
        Refresh();
    }

    public void ConfigureCreation(int rows, int columns, bool firstRowIsHeader)
    {
        session.ConfigureCreation(rows, columns, firstRowIsHeader);
        Refresh();
    }

    public void UpdateNumericInspection(TableCellNumericInputInspection? inspection)
    {
        numericInspection = inspection;
        Refresh();
    }

    public void Refresh()
    {
        var groups = TableRibbonStateProvider.Create(session).ToList();
        if (session.TableElementId is not null)
        {
            var enabled = numericInspection?.CanEditProperties == true;
            var reason = numericInspection?.Diagnostic ?? "Selectionnez une cellule InputNumeric unique.";
            var numericGroup = new RibbonGroupDefinition(
                "Input numerique",
                [
                    Command("table.numeric.properties", "Proprietes", "Icon.Field.Numeric", "Valeur initiale, contraintes et bindings", enabled, reason),
                    Command("table.binding.read", "Lire", "Icon.Field.Numeric", $"Lire valeur: {numericInspection?.ReadBindingSummary ?? "Aucun"}", enabled, reason),
                    Command("table.binding.write", "Ecrire", "Icon.Field.Numeric", $"Ecrire valeur: {numericInspection?.WriteBindingSummary ?? "Aucun"}", enabled, reason)
                ]);
            var index = Math.Min(groups.Count, Math.Max(0, groups.FindIndex(group => group.Label == "Contenu") + 1));
            groups.Insert(index, numericGroup);
        }
        Groups = groups;
    }

    private static RibbonCommandDefinition Command(
        string id,
        string label,
        string icon,
        string toolTip,
        bool enabled,
        string reason) =>
        new(id, label, icon, toolTip, enabled, enabled ? null : reason);
}
