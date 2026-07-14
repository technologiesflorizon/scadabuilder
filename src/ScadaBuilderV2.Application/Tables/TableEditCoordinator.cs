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
    DeleteColumn
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
    ScadaTableFormat? Format = null);

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
        TableEditKind.Merge => ScadaTableOperations.Merge(table, RequireRange(request)),
        TableEditKind.Unmerge => ScadaTableOperations.Unmerge(table, Require(request.Row, "row"), Require(request.Column, "column")),
        TableEditKind.SetContent => ScadaTableOperations.SetContent(table, Require(request.Row, "row"), Require(request.Column, "column"), request.Content ?? ScadaTableCellContent.EmptyText),
        TableEditKind.ClearContent => ScadaTableOperations.ClearContent(table, RequireRange(request)),
        TableEditKind.SetCellFormat => ScadaTableOperations.SetCellFormat(table, RequireRange(request), request.Format),
        TableEditKind.SetRowFormat => ScadaTableOperations.SetRowFormat(table, Rows(RequireRange(request)), request.Format),
        TableEditKind.SetColumnFormat => ScadaTableOperations.SetColumnFormat(table, Columns(RequireRange(request)), request.Format),
        TableEditKind.SetRowHeight => ScadaTableOperations.SetRowHeight(table, Rows(RequireRange(request)), Require(request.Height, "height")),
        TableEditKind.SetColumnWidth => ScadaTableOperations.SetColumnWidth(table, Columns(RequireRange(request)), Require(request.Width, "width")),
        TableEditKind.ResizeProportionally => ScadaTableOperations.ResizeProportionally(table, Require(request.Width, "width"), Require(request.Height, "height")),
        TableEditKind.InsertRow => ScadaTableOperations.InsertRow(table, Require(request.Row, "row")),
        TableEditKind.InsertColumn => ScadaTableOperations.InsertColumn(table, Require(request.Column, "column")),
        TableEditKind.DeleteRow => ScadaTableOperations.DeleteRow(table, Require(request.Row, "row")),
        TableEditKind.DeleteColumn => ScadaTableOperations.DeleteColumn(table, Require(request.Column, "column")),
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
        TableEditKind.InsertRow => "Insertion rangee",
        TableEditKind.InsertColumn => "Insertion colonne",
        TableEditKind.DeleteRow => "Suppression rangee",
        TableEditKind.DeleteColumn => "Suppression colonne",
        _ => "Edition tableau"
    };
}
