using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Tables;

/// <summary>Identifies one model-backed table edit operation.</summary>
public enum TableEditKind
{
    Merge,
    Unmerge,
    SetContent,
    ClearContent,
    SetCellFormat,
    SetRowFormat,
    SetColumnFormat,
    SetRowHeight,
    SetColumnWidth,
    ResizeProportionally,
    InsertRow,
    InsertColumn,
    DeleteRow,
    DeleteColumn,
    ConvertContentKind,
    EqualizeRows,
    EqualizeColumns,
    DistributeRows,
    DistributeColumns,
    ApplyAutoFit,
    SetHeaderRowCount,
    ApplyFormatScope,
    ResetFormatScope,
    ApplyBorderPreset
}

/// <summary>Describes one typed table edit request independent of WPF and WebView.</summary>
public sealed record TableEditRequest(
    TableEditKind Kind,
    ScadaTableRange? Range = null,
    int? Row = null,
    int? Column = null,
    double? Width = null,
    double? Height = null,
    ScadaTableCellContent? Content = null,
    ScadaTableFormat? Format = null,
    ScadaTableCellContentKind? ContentKind = null,
    ScadaTableFormatScope? FormatScope = null,
    ScadaTableBorderPreset? BorderPreset = null,
    ScadaTableBorder? Border = null,
    int? Count = null,
    IReadOnlyList<double>? ColumnSizes = null,
    IReadOnlyList<double>? RowSizes = null);

/// <summary>Returns the result and diagnostic of one table edit.</summary>
public sealed record TableEditResult(bool Succeeded, ScadaElement Element, string Label, string? Error = null);

/// <summary>Coordinates validated immutable table edits for all editor surfaces.</summary>
/// <remarks>Decisions: DEC-0039. Contracts: docs/04_editor/COMMANDS_CONTRACT_V2.md. Tests: tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs.</remarks>
public sealed class TableEditCoordinator
{
    /// <summary>Applies one request and returns an updated Element+ without mutating the source.</summary>
    public TableEditResult Apply(ScadaElement element, TableEditRequest request)
    {
        if (element.Kind != ScadaElementKind.Table || element.Table is null)
        {
            return new TableEditResult(false, element, "Edition tableau", "La selection active n'est pas un tableau Element+.");
        }

        try
        {
            var updated = Apply(element.Table, request);
            ScadaTableOperations.ValidateDefinition(updated);
            return new TableEditResult(
                true,
                element with
                {
                    Table = updated,
                    Bounds = element.Bounds with { Width = updated.Width, Height = updated.Height }
                },
                ResolveLabel(request.Kind));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return new TableEditResult(false, element, ResolveLabel(request.Kind), exception.Message);
        }
    }

    private static ScadaTableDefinition Apply(ScadaTableDefinition table, TableEditRequest request) => request.Kind switch
    {
        TableEditKind.Merge => ScadaTableStructureOperations.Merge(table, RequireRange(request)),
        TableEditKind.Unmerge => ScadaTableStructureOperations.Unmerge(table, Require(request.Row, "row"), Require(request.Column, "column")),
        TableEditKind.SetContent => ScadaTableContentOperations.SetContent(table, Require(request.Row, "row"), Require(request.Column, "column"), request.Content ?? ScadaTableCellContent.EmptyText),
        TableEditKind.ClearContent => ScadaTableContentOperations.ClearContent(table, RequireRange(request)),
        TableEditKind.SetCellFormat => ScadaTableFormatOperations.ApplyFormat(table, new(ScadaTableFormatScopeKind.Cells, RequireRange(request)), request.Format),
        TableEditKind.SetRowFormat => ScadaTableFormatOperations.ApplyFormat(table, new(ScadaTableFormatScopeKind.Rows, RequireRange(request)), request.Format),
        TableEditKind.SetColumnFormat => ScadaTableFormatOperations.ApplyFormat(table, new(ScadaTableFormatScopeKind.Columns, RequireRange(request)), request.Format),
        TableEditKind.SetRowHeight => ScadaTableTrackOperations.SetRowHeight(table, Rows(RequireRange(request)), Require(request.Height, "height")),
        TableEditKind.SetColumnWidth => ScadaTableTrackOperations.SetColumnWidth(table, Columns(RequireRange(request)), Require(request.Width, "width")),
        TableEditKind.ResizeProportionally => ScadaTableTrackOperations.ResizeProportionally(table, Require(request.Width, "width"), Require(request.Height, "height")),
        TableEditKind.InsertRow => ScadaTableStructureOperations.InsertRow(table, Require(request.Row, "row")),
        TableEditKind.InsertColumn => ScadaTableStructureOperations.InsertColumn(table, Require(request.Column, "column")),
        TableEditKind.DeleteRow => ScadaTableStructureOperations.DeleteRow(table, Require(request.Row, "row")),
        TableEditKind.DeleteColumn => ScadaTableStructureOperations.DeleteColumn(table, Require(request.Column, "column")),
        TableEditKind.ConvertContentKind => ScadaTableContentOperations.ConvertKind(table, RequireRange(request), Require(request.ContentKind, "content kind")),
        TableEditKind.EqualizeRows => ScadaTableTrackOperations.EqualizeRows(table, Rows(RequireRange(request))),
        TableEditKind.EqualizeColumns => ScadaTableTrackOperations.EqualizeColumns(table, Columns(RequireRange(request))),
        TableEditKind.DistributeRows => ScadaTableTrackOperations.DistributeRows(table, Rows(RequireRange(request)), Require(request.Height, "height")),
        TableEditKind.DistributeColumns => ScadaTableTrackOperations.DistributeColumns(table, Columns(RequireRange(request)), Require(request.Width, "width")),
        TableEditKind.ApplyAutoFit => ScadaTableTrackOperations.ApplySizes(table, request.ColumnSizes ?? throw new ArgumentException("Column sizes are required."), request.RowSizes ?? throw new ArgumentException("Row sizes are required.")),
        TableEditKind.SetHeaderRowCount => ScadaTableHeaderOperations.SetHeaderRowCount(table, Require(request.Count, "count")),
        TableEditKind.ApplyFormatScope => ScadaTableFormatOperations.ApplyFormat(table, request.FormatScope ?? throw new ArgumentException("A format scope is required."), request.Format),
        TableEditKind.ResetFormatScope => ScadaTableFormatOperations.ResetScope(table, request.FormatScope ?? throw new ArgumentException("A format scope is required.")),
        TableEditKind.ApplyBorderPreset => ScadaTableBorderOperations.ApplyPreset(table, RequireRange(request), Require(request.BorderPreset, "border preset"), request.Border),
        _ => throw new InvalidOperationException("Unsupported table edit request.")
    };

    private static ScadaTableRange RequireRange(TableEditRequest request) =>
        request.Range ?? throw new ArgumentException("A rectangular selection is required.");

    private static T Require<T>(T? value, string name) where T : struct =>
        value ?? throw new ArgumentException($"The {name} value is required.");

    private static IEnumerable<int> Rows(ScadaTableRange range) => Enumerable.Range(range.StartRow, range.RowCount);

    private static IEnumerable<int> Columns(ScadaTableRange range) => Enumerable.Range(range.StartColumn, range.ColumnCount);

    private static string ResolveLabel(TableEditKind kind) => kind switch
    {
        TableEditKind.Merge => "Fusion cellules",
        TableEditKind.Unmerge => "Defusion cellules",
        TableEditKind.SetContent => "Contenu cellule",
        TableEditKind.ClearContent => "Effacement contenu",
        TableEditKind.SetCellFormat or TableEditKind.SetRowFormat or TableEditKind.SetColumnFormat => "Format tableau",
        TableEditKind.SetRowHeight or TableEditKind.SetColumnWidth or TableEditKind.ResizeProportionally => "Dimensions tableau",
        TableEditKind.ConvertContentKind => "Type de cellule",
        TableEditKind.EqualizeRows or TableEditKind.EqualizeColumns or TableEditKind.DistributeRows or TableEditKind.DistributeColumns or TableEditKind.ApplyAutoFit => "Dimensions tableau",
        TableEditKind.SetHeaderRowCount => "En-tetes tableau",
        TableEditKind.ApplyFormatScope or TableEditKind.ResetFormatScope => "Format tableau",
        TableEditKind.ApplyBorderPreset => "Bordures tableau",
        TableEditKind.InsertRow => "Insertion rangee",
        TableEditKind.InsertColumn => "Insertion colonne",
        TableEditKind.DeleteRow => "Suppression rangee",
        TableEditKind.DeleteColumn => "Suppression colonne",
        _ => "Edition tableau"
    };
}
