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
    private readonly TableEditCoordinator coordinator = new();
    private TableClipboardPayload? clipboard;

    public TableEditorController(Window owner, Action<ScadaElement, string> commit)
    {
        this.owner = owner;
        this.commit = commit;
    }

    public string? ElementId { get; private set; }
    public ScadaTableRange Selection { get; private set; } = new(0, 0, 0, 0);

    public TableCreationOptions? RequestCreationOptions()
    {
        var dialog = new TableCreationDialog { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

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
            case "table.row.height": return Size(element, row: true);
            case "table.column.width": return Size(element, row: false);
            case "table.properties": return Properties(element);
            default: return false;
        }
    }

    public bool SetCellContent(ScadaElement element, int row, int column, ScadaTableCellContent content) =>
        Apply(element, new(TableEditKind.SetContent, Row: row, Column: column, Content: content));

    public bool ResizeAndMove(ScadaElement element, double x, double y, double width, double height)
    {
        var result = coordinator.Apply(element, new(TableEditKind.ResizeProportionally, Width: width, Height: height));
        if (!result.Succeeded)
        {
            MessageBox.Show(owner, result.Error ?? "Dimensions de tableau invalides.", "Tableau", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        commit(result.Element with { Bounds = result.Element.Bounds with { X = x, Y = y } }, "Redimensionnement tableau");
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
        var current = ScadaTableStyleResolver.Resolve(element.Table!, Selection.StartRow, Selection.StartColumn);
        var dialog = new CellFormatDialog(current) { Owner = owner };
        return dialog.ShowDialog() == true && dialog.Result is not null &&
            Apply(element, new(TableEditKind.SetCellFormat, Selection, Format: dialog.Result));
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
        var dialog = new TablePropertiesDialog(element.Table!) { Owner = owner };
        if (dialog.ShowDialog() != true || dialog.Result is null) return false;
        Commit(element with { Table = element.Table! with { Style = dialog.Result } }, "Proprietes tableau");
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
