using System.Windows;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Tables;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App.TableEditor;

/// <summary>Coordinates table dialogs, clipboard and typed mutations outside MainWindow.</summary>
internal sealed class TableEditorController
{
    private readonly Window owner;
    private readonly Action<ScadaElement, string> commit;
    private readonly Func<ScadaElement, bool> canCommitTransform;
    private readonly TableEditCoordinator coordinator = new();
    private readonly TablePropertiesViewModel properties = new();
    private TableClipboardPayload? clipboard;

    public TableEditorController(Window owner, Action<ScadaElement, string> commit, Func<ScadaElement, bool> canCommitTransform)
    {
        this.owner = owner;
        this.commit = commit;
        this.canCommitTransform = canCommitTransform;
    }

    public string? ElementId { get; private set; }
    public ScadaTableRange Selection { get; private set; } = new(0, 0, 0, 0);
    public ScadaTableFormatScopeKind FormatScopeKind { get; set; } = ScadaTableFormatScopeKind.Cells;

    public void Select(string elementId, int row, int column, int endRow, int endColumn)
    {
        ElementId = elementId;
        Selection = ScadaTableRange.Normalize(row, column, endRow, endColumn);
    }

    public IReadOnlyList<EditorCommandDescriptor> BuildContextMenu(ScadaElement element)
    {
        return element.Table is null ? [] : TableContextMenuProvider.Build(element.Table, Selection, clipboard is not null || Clipboard.ContainsText());
    }

    public bool Execute(string commandId, ScadaElement element)
    {
        if (element.Table is null) return false;
        switch (commandId)
        {
            case "table.merge-toggle": return Apply(element, new(TableEditKind.ToggleMerge, Selection));
            case "table.copy":
                clipboard = TableClipboard.Copy(element.Table, Selection);
                Clipboard.SetText(clipboard.Tsv);
                return true;
            case "table.paste":
                clipboard ??= Clipboard.ContainsText() ? TableClipboard.ParseTsv(Clipboard.GetText()) : null;
                if (clipboard is null) return false;
                Commit(element with { Table = TableClipboard.Paste(element.Table, Selection.StartRow, Selection.StartColumn, clipboard) }, "Collage cellules");
                return true;
            case "table.merge": return Apply(element, new(TableEditKind.Merge, Selection));
            case "table.unmerge": return Apply(element, new(TableEditKind.Unmerge, Row: Selection.StartRow, Column: Selection.StartColumn));
            case "table.clear": return Apply(element, new(TableEditKind.ClearContent, Selection));
            case "table.row.insert": return Apply(element, new(TableEditKind.InsertRow, Row: Selection.StartRow));
            case "table.column.insert": return Apply(element, new(TableEditKind.InsertColumn, Column: Selection.StartColumn));
            case "table.row.delete": return Apply(element, new(TableEditKind.DeleteRow, Row: Selection.StartRow));
            case "table.column.delete": return Apply(element, new(TableEditKind.DeleteColumn, Column: Selection.StartColumn));
            case "table.format": return Format(element);
            case "table.content.properties": return Content(element);
            case "table.borders": return Borders(element);
            case "table.headers": return Headers(element);
            case "table.equalize": return Equalize(element);
            case "table.distribute.rows": return Distribute(element, rows: true);
            case "table.distribute.columns": return Distribute(element, rows: false);
            case "table.header.mark": return Apply(element, new(TableEditKind.SetHeaderRowCount, Count: Selection.StartRow + 1));
            case "table.header.unmark": return Apply(element, new(TableEditKind.SetHeaderRowCount, Count: Selection.StartRow));
            case "table.format.reset": return Apply(element, new(TableEditKind.ResetFormatScope, FormatScope: new ScadaTableFormatScope(FormatScopeKind,
                FormatScopeKind is ScadaTableFormatScopeKind.Table or ScadaTableFormatScopeKind.HeaderRows or ScadaTableFormatScopeKind.AlternatingRows ? null : Selection)));
            case "table.row.height": return Size(element, row: true);
            case "table.column.width": return Size(element, row: false);
            case "table.properties": return Properties(element);
            default: return false;
        }
    }

    public bool SetCellContent(ScadaElement element, int row, int column, ScadaTableCellContent content) =>
        Apply(element, new(TableEditKind.SetContent, Row: row, Column: column, Content: content));

    public bool ConvertContent(ScadaElement element, ScadaTableCellContentKind kind) =>
        Apply(element, new(TableEditKind.ConvertContentKind, Selection, ContentKind: kind));

    public string InspectFormatState(ScadaElement element)
    {
        properties.Load(element, Selection, FormatScopeKind);
        return properties.StateLabel;
    }

    public bool SelectionContainsMergedCells(ScadaElement element) =>
        element.Table is not null && ScadaTableStructureOperations.ContainsMergedCells(element.Table, Selection);

    public void SelectAll(ScadaElement element)
    {
        if (element.Table is null) return;
        Select(element.Id, 0, 0, element.Table.EffectiveRows.Count - 1, element.Table.EffectiveColumns.Count - 1);
        FormatScopeKind = ScadaTableFormatScopeKind.Table;
    }

    public bool ApplyAutoFit(ScadaElement element, IReadOnlyList<double> columns, IReadOnlyList<double> rows) =>
        Apply(element, new(TableEditKind.ApplyAutoFit, ColumnSizes: columns, RowSizes: rows));

    public bool ResizeAndMove(ScadaElement element, double x, double y, double width, double height)
    {
        var result = coordinator.Apply(element, new(TableEditKind.ResizeProportionally, Width: width, Height: height));
        if (!result.Succeeded)
        {
            MessageBox.Show(owner, result.Error ?? "Dimensions de tableau invalides.", "Tableau", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        var updated = result.Element with { Bounds = result.Element.Bounds with { X = x, Y = y } };
        if (!canCommitTransform(updated)) return false;
        commit(updated, "Redimensionnement tableau");
        return true;
    }

    public bool SetTrackSize(ScadaElement element, bool row, int index, double size)
    {
        var range = row
            ? new ScadaTableRange(index, 0, index, Math.Max(0, element.Table!.EffectiveColumns.Count - 1))
            : new ScadaTableRange(0, index, Math.Max(0, element.Table!.EffectiveRows.Count - 1), index);
        return Apply(element, row
            ? new TableEditRequest(TableEditKind.SetRowHeight, range, Height: size)
            : new TableEditRequest(TableEditKind.SetColumnWidth, range, Width: size));
    }

    private bool Format(ScadaElement element)
    {
        properties.Load(element, Selection, FormatScopeKind);
        var dialog = new CellFormatDialog(properties.Format!) { Owner = owner };
        if (dialog.ShowDialog() != true || dialog.Result is null) return false;
        return dialog.Result.Action == TableFormatDialogAction.ResetProperty && dialog.Result.PropertyName is not null
            ? Apply(element, properties.ResetProperty(dialog.Result.PropertyName))
            : dialog.Result.Format is not null && Apply(element, properties.ApplyFormat(dialog.Result.Format));
    }

    private bool Content(ScadaElement element)
    {
        var current = element.Table!.EffectiveCells.First(cell => cell.Covers(Selection.StartRow, Selection.StartColumn)).EffectiveContent;
        var dialog = new CellContentDialog(current) { Owner = owner };
        if (dialog.ShowDialog() != true || dialog.Result is null) return false;
        return Apply(element, new(TableEditKind.SetContent, Row: Selection.StartRow, Column: Selection.StartColumn, Content: dialog.Result));
    }

    private bool Borders(ScadaElement element)
    {
        var dialog = new TableBorderDialog { Owner = owner };
        if (dialog.ShowDialog() != true || dialog.Result is null) return false;
        return Apply(element, new(TableEditKind.ApplyBorderPreset, Selection, BorderPreset: dialog.Result.Value.Preset, Border: dialog.Result.Value.Border));
    }

    private bool Headers(ScadaElement element)
    {
        var current = element.Table!.EffectiveRows.TakeWhile(row => row.IsHeader).Count();
        var dialog = new HeaderRowsDialog(current, element.Table.EffectiveRows.Count) { Owner = owner };
        return dialog.ShowDialog() == true && dialog.Result is int count && Apply(element, new(TableEditKind.SetHeaderRowCount, Count: count));
    }

    private bool Equalize(ScadaElement element)
    {
        var kind = Selection.RowCount > 1 && Selection.ColumnCount == element.Table!.EffectiveColumns.Count
            ? TableEditKind.EqualizeRows : TableEditKind.EqualizeColumns;
        return Apply(element, new(kind, Selection));
    }

    private bool Distribute(ScadaElement element, bool rows)
    {
        var current = rows
            ? Enumerable.Range(Selection.StartRow, Selection.RowCount).Sum(index => element.Table!.EffectiveRows[index].Height)
            : Enumerable.Range(Selection.StartColumn, Selection.ColumnCount).Sum(index => element.Table!.EffectiveColumns[index].Width);
        var count = rows ? Selection.RowCount : Selection.ColumnCount;
        var minimum = count * (rows ? ScadaTableDefinition.MinimumRowHeight : ScadaTableDefinition.MinimumColumnWidth);
        var dialog = new TrackSizeDialog(rows ? "Distribuer les rangees" : "Distribuer les colonnes", current, minimum) { Owner = owner };
        if (dialog.ShowDialog() != true || dialog.Result is null) return false;
        return Apply(element, rows
            ? new TableEditRequest(TableEditKind.DistributeRows, Selection, Height: dialog.Result)
            : new TableEditRequest(TableEditKind.DistributeColumns, Selection, Width: dialog.Result));
    }

    private bool Size(ScadaElement element, bool row)
    {
        var current = row ? element.Table!.EffectiveRows[Selection.StartRow].Height : element.Table!.EffectiveColumns[Selection.StartColumn].Width;
        var minimum = row ? ScadaTableDefinition.MinimumRowHeight : ScadaTableDefinition.MinimumColumnWidth;
        var dialog = new TrackSizeDialog(row ? "Hauteur de rangee" : "Largeur de colonne", current, minimum) { Owner = owner };
        if (dialog.ShowDialog() != true || dialog.Result is null) return false;
        return Apply(element, row
            ? new TableEditRequest(TableEditKind.SetRowHeight, Selection, Height: dialog.Result)
            : new TableEditRequest(TableEditKind.SetColumnWidth, Selection, Width: dialog.Result));
    }

    private bool Properties(ScadaElement element)
    {
        properties.Load(element, Selection, FormatScopeKind);
        var dialog = new TablePropertiesDialog(element) { Owner = owner };
        if (dialog.ShowDialog() != true || dialog.Result is null) return false;
        var value = dialog.Result;
        var result = coordinator.Apply(element, properties.ApplyDimensions(value.Width, value.Height, value.Style));
        if (!result.Succeeded)
        {
            MessageBox.Show(owner, result.Error ?? "Dimensions de tableau invalides.", "Tableau", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        var updated = result.Element with { Bounds = result.Element.Bounds with { X = value.X, Y = value.Y } };
        if (!canCommitTransform(updated))
        {
            MessageBox.Show(owner, "La position d'un tableau verrouille ne peut pas etre modifiee.", "Tableau", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        Commit(updated, result.Label);
        return true;
    }

    private bool Apply(ScadaElement element, TableEditRequest request)
    {
        var result = coordinator.Apply(element, request);
        if (!result.Succeeded)
        {
            MessageBox.Show(owner, result.Error ?? "La modification du tableau a echoue.", "Tableau", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        Commit(result.Element, result.Label);
        return true;
    }

    private void Commit(ScadaElement element, string label)
    {
        var table = element.Table!;
        commit(element with { Bounds = element.Bounds with { Width = table.Width, Height = table.Height } }, label);
    }
}
