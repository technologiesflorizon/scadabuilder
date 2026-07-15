using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Tables;

public enum TableInteractionMode { Object, Cells }

/// <summary>Stores contextual table-authoring UI state without owning scene models.</summary>
/// <remarks>Decisions: DEC-0040. The session carries stable ids and immutable value snapshots only.</remarks>
public sealed class TableAuthoringSession
{
    public bool IsSurfaceOpen { get; private set; }
    public bool IsPlacementArmed { get; private set; }
    public string? TableElementId { get; private set; }
    public int CreationRows { get; private set; } = 8;
    public int CreationColumns { get; private set; } = 6;
    public bool FirstRowIsHeader { get; private set; } = true;
    public TableInteractionMode Mode { get; private set; } = TableInteractionMode.Object;
    public ScadaTableRange Selection { get; private set; } = new(0, 0, 0, 0);
    public ScadaTableFormatScopeKind FormatScope { get; private set; } = ScadaTableFormatScopeKind.Cells;

    public void OpenSurface() => IsSurfaceOpen = true;
    public void CloseSurface() { IsSurfaceOpen = false; IsPlacementArmed = false; Mode = TableInteractionMode.Object; }
    public void ConfigureCreation(int rows, int columns, bool header)
    {
        if (rows is < 1 or > 64 || columns is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(rows));
        CreationRows = rows; CreationColumns = columns; FirstRowIsHeader = header;
    }
    public void BeginPlacement() { IsSurfaceOpen = true; IsPlacementArmed = true; }
    public void CancelPlacement() => IsPlacementArmed = false;
    public void SelectTable(string? id)
    {
        TableElementId = id;
        if (id is null) Mode = TableInteractionMode.Object;
    }
    public void CompletePlacement(string id) { IsPlacementArmed = false; TableElementId = id; Mode = TableInteractionMode.Cells; Selection = new(0, 0, 0, 0); }
    public void SetMode(TableInteractionMode mode) { if (mode == TableInteractionMode.Cells && TableElementId is null) throw new InvalidOperationException("A table must be selected."); Mode = mode; }
    public void SetSelection(ScadaTableRange range) => Selection = range;
    public void SetFormatScope(ScadaTableFormatScopeKind scope) => FormatScope = scope;
}
