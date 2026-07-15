using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Tables;

/// <summary>Identifies whether Element+ or table-cell gestures own pointer input.</summary>
public enum TableInteractionMode { Object, Cells }

/// <summary>Stores contextual table-authoring UI state without owning scene models.</summary>
/// <remarks>Decisions: DEC-0040. The session carries stable ids and immutable value snapshots only.</remarks>
public sealed class TableAuthoringSession
{
    /// <summary>Gets whether the contextual Table ribbon is open.</summary>
    public bool IsSurfaceOpen { get; private set; }
    /// <summary>Gets whether the next canvas point creates a table.</summary>
    public bool IsPlacementArmed { get; private set; }
    /// <summary>Gets the selected Table Element+ id.</summary>
    public string? TableElementId { get; private set; }
    /// <summary>Gets the configured creation row count.</summary>
    public int CreationRows { get; private set; } = 8;
    /// <summary>Gets the configured creation column count.</summary>
    public int CreationColumns { get; private set; } = 6;
    /// <summary>Gets whether the initial first row is a header.</summary>
    public bool FirstRowIsHeader { get; private set; } = true;
    /// <summary>Gets the active interaction mode.</summary>
    public TableInteractionMode Mode { get; private set; } = TableInteractionMode.Object;
    /// <summary>Gets the physical cell selection.</summary>
    public ScadaTableRange Selection { get; private set; } = new(0, 0, 0, 0);
    /// <summary>Gets the active formatting scope.</summary>
    public ScadaTableFormatScopeKind FormatScope { get; private set; } = ScadaTableFormatScopeKind.Cells;

    /// <summary>Opens the contextual surface without arming placement.</summary>
    public void OpenSurface() => IsSurfaceOpen = true;
    /// <summary>Closes the surface and cancels transient interaction state.</summary>
    public void CloseSurface() { IsSurfaceOpen = false; IsPlacementArmed = false; Mode = TableInteractionMode.Object; }
    /// <summary>Configures the next 1..64 by 1..64 table creation.</summary>
    public void ConfigureCreation(int rows, int columns, bool header)
    {
        if (rows is < 1 or > 64 || columns is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(rows));
        CreationRows = rows; CreationColumns = columns; FirstRowIsHeader = header;
    }
    /// <summary>Arms point placement.</summary>
    public void BeginPlacement() { IsSurfaceOpen = true; IsPlacementArmed = true; }
    /// <summary>Cancels point placement without closing the surface.</summary>
    public void CancelPlacement() => IsPlacementArmed = false;
    /// <summary>Updates the selected table id.</summary>
    public void SelectTable(string? id)
    {
        TableElementId = id;
        if (id is null) Mode = TableInteractionMode.Object;
    }
    /// <summary>Completes placement and enters Cells mode.</summary>
    public void CompletePlacement(string id) { IsPlacementArmed = false; TableElementId = id; Mode = TableInteractionMode.Cells; Selection = new(0, 0, 0, 0); }
    /// <summary>Changes the mutually exclusive interaction mode.</summary>
    public void SetMode(TableInteractionMode mode) { if (mode == TableInteractionMode.Cells && TableElementId is null) throw new InvalidOperationException("A table must be selected."); Mode = mode; }
    /// <summary>Updates the immutable physical cell range snapshot.</summary>
    public void SetSelection(ScadaTableRange range) => Selection = range;
    /// <summary>Updates the formatting scope without changing physical selection.</summary>
    public void SetFormatScope(ScadaTableFormatScopeKind scope) => FormatScope = scope;
}
