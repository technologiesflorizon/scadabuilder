using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Tables;

namespace ScadaBuilderV2.App.TableEditor;

/// <summary>Projects the application-owned Table authoring session into the level-two ribbon surface.</summary>
internal sealed class TableRibbonViewModel
{
    private readonly TableAuthoringSession session;

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

    public void Refresh() => Groups = TableRibbonStateProvider.Create(session);
}
