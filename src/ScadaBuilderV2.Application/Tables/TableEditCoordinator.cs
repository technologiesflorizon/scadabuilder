using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Application.Tables;

/// <summary>Identifies one model-backed table edit operation.</summary>
public enum TableEditKind
{
    ToggleMerge,
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
    ResetFormatProperty,
    ApplyBorderPreset,
    SetTableProperties,
    SetNumericInputProperties,
    SetCellValueBinding,
    RemoveCellValueBinding
}

/// <summary>Identifies the read or write side of a numeric table-cell binding.</summary>
public enum TableCellBindingKind
{
    Read,
    Write
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
    ScadaTableStyle? TableStyle = null,
    string? PropertyName = null,
    int? Count = null,
    IReadOnlyList<double>? ColumnSizes = null,
    IReadOnlyList<double>? RowSizes = null,
    TableCellBindingKind? BindingKind = null,
    string? TagId = null,
    bool ConfirmedBindingRemoval = false);

/// <summary>Returns the result and diagnostic of one table edit.</summary>
public sealed record TableEditResult(bool Succeeded, ScadaElement Element, string Label, string? Error = null);

/// <summary>Coordinates validated immutable table edits for all editor surfaces.</summary>
/// <remarks>Decisions: DEC-0039, DEC-0042. Contracts: docs/04_editor/COMMANDS_CONTRACT_V2.md and docs/superpowers/specs/2026-07-15-table-cell-numeric-input-tf100web-design.md. Tests: tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs.</remarks>
public sealed class TableEditCoordinator
{
    /// <summary>Applies one request and returns an updated Element+ without mutating the source.</summary>
    public TableEditResult Apply(ScadaElement element, TableEditRequest request, ScadaTagCatalog? tagCatalog = null)
    {
        if (element.Kind != ScadaElementKind.Table || element.Table is null)
        {
            return new TableEditResult(false, element, "Edition tableau", "La selection active n'est pas un tableau Element+.");
        }

        try
        {
            var safety = TableBindingSafetyPolicy.Evaluate(request, element.Table);
            if (safety.Disposition == TableBindingSafetyDisposition.Blocked ||
                safety.Disposition == TableBindingSafetyDisposition.RequiresConfirmation && !request.ConfirmedBindingRemoval)
            {
                return new TableEditResult(false, element, ResolveLabel(request.Kind), safety.Reason);
            }

            var updated = Apply(element.Table, request, tagCatalog);
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

    private static ScadaTableDefinition Apply(ScadaTableDefinition table, TableEditRequest request, ScadaTagCatalog? tagCatalog) => request.Kind switch
    {
        TableEditKind.ToggleMerge => ScadaTableStructureOperations.ToggleMerge(table, RequireRange(request)),
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
        TableEditKind.ConvertContentKind => ConvertContentKind(table, request),
        TableEditKind.EqualizeRows => ScadaTableTrackOperations.EqualizeRows(table, Rows(RequireRange(request))),
        TableEditKind.EqualizeColumns => ScadaTableTrackOperations.EqualizeColumns(table, Columns(RequireRange(request))),
        TableEditKind.DistributeRows => ScadaTableTrackOperations.DistributeRows(table, Rows(RequireRange(request)), Require(request.Height, "height")),
        TableEditKind.DistributeColumns => ScadaTableTrackOperations.DistributeColumns(table, Columns(RequireRange(request)), Require(request.Width, "width")),
        TableEditKind.ApplyAutoFit => ScadaTableTrackOperations.ApplySizes(table, request.ColumnSizes ?? throw new ArgumentException("Column sizes are required."), request.RowSizes ?? throw new ArgumentException("Row sizes are required.")),
        TableEditKind.SetHeaderRowCount => ScadaTableHeaderOperations.SetHeaderRowCount(table, Require(request.Count, "count")),
        TableEditKind.ApplyFormatScope => ScadaTableFormatOperations.ApplyFormat(table, request.FormatScope ?? throw new ArgumentException("A format scope is required."), request.Format),
        TableEditKind.ResetFormatScope => ScadaTableFormatOperations.ResetScope(table, request.FormatScope ?? throw new ArgumentException("A format scope is required.")),
        TableEditKind.ResetFormatProperty => ScadaTableFormatOperations.ResetProperty(table, request.FormatScope ?? throw new ArgumentException("A format scope is required."), request.PropertyName ?? throw new ArgumentException("A format property is required.")),
        TableEditKind.ApplyBorderPreset => ScadaTableBorderOperations.ApplyPreset(table, RequireRange(request), Require(request.BorderPreset, "border preset"), request.Border),
        TableEditKind.SetTableProperties => ScadaTableTrackOperations.ResizeProportionally(
            table with { Style = request.TableStyle ?? table.EffectiveStyle },
            Require(request.Width, "width"),
            Require(request.Height, "height")),
        TableEditKind.SetNumericInputProperties => SetNumericInputProperties(table, request),
        TableEditKind.SetCellValueBinding => SetCellValueBinding(table, request, tagCatalog),
        TableEditKind.RemoveCellValueBinding => RemoveCellValueBinding(table, request),
        _ => throw new InvalidOperationException("Unsupported table edit request.")
    };

    private static ScadaTableDefinition SetNumericInputProperties(ScadaTableDefinition table, TableEditRequest request)
    {
        var (row, column) = RequireCoordinate(request);
        var content = request.Content ?? throw new ArgumentException("Numeric input properties are required.");
        var diagnostic = TableCellNumericInputInspector.ValidateContent(content);
        if (diagnostic is not null)
        {
            throw new ArgumentException(diagnostic);
        }

        var updated = table;
        var target = ScadaTableCellBindingOperations.ValidateEditableNumericTarget(table, row, column);
        if (content.IsReadOnly && !string.IsNullOrWhiteSpace(target.ValueBindings?.WriteTagId))
        {
            var binding = target.ValueBindings! with { WriteTagId = null };
            updated = string.IsNullOrWhiteSpace(binding.ReadTagId)
                ? ScadaTableCellBindingOperations.RemoveBinding(updated, row, column)
                : ScadaTableCellBindingOperations.SetBinding(updated, row, column, binding);
        }

        return ScadaTableContentOperations.SetContent(updated, row, column, content);
    }

    private static ScadaTableDefinition SetCellValueBinding(
        ScadaTableDefinition table,
        TableEditRequest request,
        ScadaTagCatalog? tagCatalog)
    {
        var (row, column) = RequireCoordinate(request);
        var kind = Require(request.BindingKind, "binding kind");
        var tagId = string.IsNullOrWhiteSpace(request.TagId)
            ? throw new ArgumentException("A tag id is required.")
            : request.TagId.Trim();
        var tag = tagCatalog?.Tags.FirstOrDefault(candidate =>
            candidate.Enabled && string.Equals(candidate.Id, tagId, StringComparison.Ordinal))
            ?? throw new ArgumentException($"Le tag actif '{tagId}' est absent du catalogue.");
        var target = ScadaTableCellBindingOperations.ValidateEditableNumericTarget(table, row, column);
        if (kind == TableCellBindingKind.Write && !tag.Writeable)
        {
            throw new InvalidOperationException($"Le tag '{tagId}' n'est pas ecrivable.");
        }
        if (kind == TableCellBindingKind.Write && target.EffectiveContent.IsReadOnly)
        {
            throw new InvalidOperationException("Une cellule en lecture seule ne peut pas porter un binding d'ecriture.");
        }

        var existing = target.ValueBindings ?? new ScadaTableCellValueBindings();
        return ScadaTableCellBindingOperations.SetBinding(
            table,
            row,
            column,
            kind == TableCellBindingKind.Read
                ? existing with { ReadTagId = tagId }
                : existing with { WriteTagId = tagId });
    }

    private static ScadaTableDefinition RemoveCellValueBinding(ScadaTableDefinition table, TableEditRequest request)
    {
        var (row, column) = RequireCoordinate(request);
        var kind = Require(request.BindingKind, "binding kind");
        var existing = ScadaTableCellBindingOperations.GetBinding(table, row, column);
        if (existing is null)
        {
            return table;
        }

        var updated = kind == TableCellBindingKind.Read
            ? existing with { ReadTagId = null }
            : existing with { WriteTagId = null };
        return string.IsNullOrWhiteSpace(updated.ReadTagId) && string.IsNullOrWhiteSpace(updated.WriteTagId)
            ? ScadaTableCellBindingOperations.RemoveBinding(table, row, column)
            : ScadaTableCellBindingOperations.SetBinding(table, row, column, updated);
    }

    private static ScadaTableDefinition ConvertContentKind(ScadaTableDefinition table, TableEditRequest request)
    {
        var range = RequireRange(request);
        var kind = Require(request.ContentKind, "content kind");
        var updated = table;
        if (kind != ScadaTableCellContentKind.InputNumeric && request.ConfirmedBindingRemoval)
        {
            foreach (var item in ScadaTableCellBindingOperations.EnumerateBindings(table)
                         .Where(item => range.Contains(item.Cell.Row, item.Cell.Column))
                         .ToArray())
            {
                updated = ScadaTableCellBindingOperations.RemoveBinding(updated, item.Cell.Row, item.Cell.Column);
            }
        }

        return ScadaTableContentOperations.ConvertKind(updated, range, kind);
    }

    private static ScadaTableRange RequireRange(TableEditRequest request) =>
        request.Range ?? throw new ArgumentException("A rectangular selection is required.");

    private static T Require<T>(T? value, string name) where T : struct =>
        value ?? throw new ArgumentException($"The {name} value is required.");

    private static (int Row, int Column) RequireCoordinate(TableEditRequest request)
    {
        var row = request.Row ?? request.Range?.StartRow;
        var column = request.Column ?? request.Range?.StartColumn;
        if (!row.HasValue || !column.HasValue ||
            request.Range is { } range && (range.RowCount != 1 || range.ColumnCount != 1))
        {
            throw new ArgumentException("A single table-cell selection is required.");
        }

        return (row.Value, column.Value);
    }

    private static IEnumerable<int> Rows(ScadaTableRange range) => Enumerable.Range(range.StartRow, range.RowCount);

    private static IEnumerable<int> Columns(ScadaTableRange range) => Enumerable.Range(range.StartColumn, range.ColumnCount);

    private static string ResolveLabel(TableEditKind kind) => kind switch
    {
        TableEditKind.ToggleMerge => "Fusion/defusion cellules",
        TableEditKind.Merge => "Fusion cellules",
        TableEditKind.Unmerge => "Defusion cellules",
        TableEditKind.SetContent => "Contenu cellule",
        TableEditKind.ClearContent => "Effacement contenu",
        TableEditKind.SetCellFormat or TableEditKind.SetRowFormat or TableEditKind.SetColumnFormat => "Format tableau",
        TableEditKind.SetRowHeight or TableEditKind.SetColumnWidth or TableEditKind.ResizeProportionally => "Dimensions tableau",
        TableEditKind.ConvertContentKind => "Type de cellule",
        TableEditKind.EqualizeRows or TableEditKind.EqualizeColumns or TableEditKind.DistributeRows or TableEditKind.DistributeColumns or TableEditKind.ApplyAutoFit => "Dimensions tableau",
        TableEditKind.SetHeaderRowCount => "En-tetes tableau",
        TableEditKind.ApplyFormatScope or TableEditKind.ResetFormatScope or TableEditKind.ResetFormatProperty => "Format tableau",
        TableEditKind.ApplyBorderPreset => "Bordures tableau",
        TableEditKind.SetTableProperties => "Proprietes tableau",
        TableEditKind.SetNumericInputProperties => "Proprietes input numerique",
        TableEditKind.SetCellValueBinding => "Binding cellule",
        TableEditKind.RemoveCellValueBinding => "Suppression binding cellule",
        TableEditKind.InsertRow => "Insertion rangee",
        TableEditKind.InsertColumn => "Insertion colonne",
        TableEditKind.DeleteRow => "Suppression rangee",
        TableEditKind.DeleteColumn => "Suppression colonne",
        _ => "Edition tableau"
    };
}
