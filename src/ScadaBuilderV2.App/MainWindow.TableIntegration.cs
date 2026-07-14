using System.Globalization;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App;

// High-level shell wiring only. Table rules, clipboard, menus and dialogs are owned
// by Application.Tables and App.TableEditor.
public partial class MainWindow
{
    private void BeginInsertToolPlacement(InsertToolDescriptor tool)
    {
        if (tool.ElementKind is null) return;
        if (tool.ElementKind == ScadaElementKind.Table)
        {
            _pendingTableCreation = _tableEditorController.RequestCreationOptions();
            if (_pendingTableCreation is null) return;
        }

        if (tool.ElementKind == ScadaElementKind.Shape && Enum.TryParse<ScadaShapeKind>(tool.Variant, out var shapeKind))
        {
            BeginShapePlacement(shapeKind, tool.Id);
            return;
        }
        if (tool.ElementKind == ScadaElementKind.Button && Enum.TryParse<ScadaButtonKind>(tool.Variant, out var buttonKind))
        {
            BeginButtonPlacement(buttonKind, tool.Id);
            return;
        }
        BeginModernElementPlacement(tool.ElementKind.Value, tool.Id);
    }

    private void CommitTableElement(ScadaElement updated, string label)
    {
        if (_activeScene is null) return;
        var current = _activeScene.FindElementRecursive(updated.Id);
        if (current is null || Equals(current, updated)) return;
        _activeScene = _activeScene.WithReplacedElementRecursive(updated);
        _selectedSceneObject = updated;
        _selectedSceneObjectIds.Add(updated.Id);
        _activeSceneTab?.History.Push(new ScadaBuilderV2.Application.History.ModernElementChangedAction(_activeScene.Id, current, updated, label));
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        RefreshTablePropertiesPanel();
        _ = RenderModernSceneAsync();
    }

    private void EditTableCell(LegacyViewerMessage message)
    {
        var element = _activeScene?.FindElementRecursive(message.Id ?? "");
        if (element?.Table is null) return;
        var kind = Enum.TryParse<ScadaTableCellContentKind>(message.ContentKind, out var parsed) ? parsed : ScadaTableCellContentKind.Text;
        var content = kind == ScadaTableCellContentKind.InputNumeric
            ? new ScadaTableCellContent(kind, NumericValue: double.TryParse(message.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null)
            : new ScadaTableCellContent(kind, message.Text ?? "");
        _tableEditorController.SetCellContent(element, message.Row, message.Column, content);
    }

    private void RefreshTablePropertiesPanel()
    {
        if (TablePropertiesPanel is null || TableSelectionSummaryText is null) return;
        var element = _selectedSceneObject?.Kind == ScadaElementKind.Table ? _selectedSceneObject : null;
        TablePropertiesPanel.Visibility = element is null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        if (element?.Table is null) return;
        var range = _tableEditorController.Selection;
        TableSelectionSummaryText.Text = range.RowCount == 1 && range.ColumnCount == 1
            ? $"Cellule {range.StartRow + 1},{range.StartColumn + 1}"
            : $"Plage {range.StartRow + 1},{range.StartColumn + 1}:{range.EndRow + 1},{range.EndColumn + 1}";
        TableDimensionSummaryText.Text = $"{element.Table.EffectiveRows.Count} rangees x {element.Table.EffectiveColumns.Count} colonnes";
    }

    private void OnOpenTablePropertiesClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_selectedSceneObject?.Kind == ScadaElementKind.Table) _tableEditorController.Execute("table.properties", _selectedSceneObject);
    }

    private void OnOpenCellFormatClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_selectedSceneObject?.Kind == ScadaElementKind.Table) _tableEditorController.Execute("table.format", _selectedSceneObject);
    }

    private void OnMergeTableCellsClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_selectedSceneObject?.Kind == ScadaElementKind.Table) _tableEditorController.Execute("table.merge", _selectedSceneObject);
    }

    private void OnUnmergeTableCellsClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_selectedSceneObject?.Kind == ScadaElementKind.Table) _tableEditorController.Execute("table.unmerge", _selectedSceneObject);
    }
}
