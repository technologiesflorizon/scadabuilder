using System.Globalization;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Tables;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App;

// High-level shell wiring only. Table rules, clipboard, menus and dialogs are owned
// by Application.Tables and App.TableEditor.
public partial class MainWindow
{
    private bool ForwardTableWebViewMessage(string json)
    {
        if (!TableWebViewMessageAdapter.TryParse(json, out var request) || request is null) return false;
        var element = _activeScene?.FindElementRecursive(request.ElementId);
        if (element?.Table is null) return true;
        switch (request)
        {
            case TableSelectionRequest selection:
                _tableEditorController.Select(selection.ElementId, selection.Row, selection.Column, selection.EndRow, selection.EndColumn);
                _tableEditorController.FormatScopeKind = selection.Scope switch { "table" => ScadaTableFormatScopeKind.Table, "row" => ScadaTableFormatScopeKind.Rows, "column" => ScadaTableFormatScopeKind.Columns, _ => ScadaTableFormatScopeKind.Cells };
                _tableAuthoringSession.SelectTable(selection.ElementId); _tableAuthoringSession.SetSelection(ScadaTableRange.Normalize(selection.Row, selection.Column, selection.EndRow, selection.EndColumn));
                SelectModernElement(selection.ElementId); RefreshTablePropertiesPanel();
                break;
            case TableCellEditRequest edit:
                var kind = Enum.TryParse<ScadaTableCellContentKind>(edit.ContentKind, out var parsed) ? parsed : ScadaTableCellContentKind.Text;
                var content = kind == ScadaTableCellContentKind.InputNumeric ? new ScadaTableCellContent(kind, NumericValue: double.TryParse(edit.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null) : new ScadaTableCellContent(kind, edit.Text);
                _tableEditorController.SetCellContent(element, edit.Row, edit.Column, content);
                break;
            case TableTrackResizeRequest resize:
                _tableEditorController.SetTrackSize(element, resize.Orientation == "row", resize.TrackIndex, resize.TrackSize);
                break;
            case TableAutoFitRequest fit:
                _tableEditorController.ApplyAutoFit(element, fit.ColumnSizes, fit.RowSizes);
                break;
        }
        return true;
    }
    private void BeginInsertToolPlacement(InsertToolDescriptor tool)
    {
        if (tool.ElementKind is null) return;
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

    private void OpenTableAuthoringSurface()
    {
        _tableAuthoringSession.OpenSurface();
        _activeInsertFamilyId = "data";
        SetActiveRibbon("Insert");
    }

    private void CloseTableAuthoringSurface()
    {
        _tableAuthoringSession.CloseSurface();
        SetActiveRibbon("Insert");
        _ = SetTableInteractionModeInWebViewAsync(TableInteractionMode.Object);
    }

    private void RefreshTableRibbonSurface()
    {
        ActiveRibbonGroups.Clear();
        foreach (var group in TableRibbonStateProvider.Create(_tableAuthoringSession))
            ActiveRibbonGroups.Add(CreateRibbonGroupViewModel(group));
        if (TableCreationConfigurationSurface is not null)
        {
            TableCreationConfigurationSurface.Visibility = System.Windows.Visibility.Visible;
            TableRowsTextBox.Text = _tableAuthoringSession.CreationRows.ToString(CultureInfo.InvariantCulture);
            TableColumnsTextBox.Text = _tableAuthoringSession.CreationColumns.ToString(CultureInfo.InvariantCulture);
            TableFirstHeaderCheckBox.IsChecked = _tableAuthoringSession.FirstRowIsHeader;
        }
    }

    private void BeginConfiguredTablePlacement()
    {
        if (!int.TryParse(TableRowsTextBox.Text, out var rows) || !int.TryParse(TableColumnsTextBox.Text, out var columns) || rows is < 1 or > 64 || columns is < 1 or > 64)
        {
            SetStatus("Tableau: les rangees et colonnes doivent etre comprises entre 1 et 64.");
            return;
        }
        _tableAuthoringSession.ConfigureCreation(rows, columns, TableFirstHeaderCheckBox.IsChecked == true);
        _tableAuthoringSession.BeginPlacement();
        BeginModernElementPlacement(ScadaElementKind.Table, "table.add");
    }

    private async Task SetTableModeAsync(TableInteractionMode mode)
    {
        if (_selectedSceneObject?.Kind != ScadaElementKind.Table) return;
        _tableAuthoringSession.SelectTable(_selectedSceneObject.Id);
        _tableAuthoringSession.SetMode(mode);
        await SetTableInteractionModeInWebViewAsync(mode);
        RefreshTableRibbonSurface();
    }

    private async Task SetTableInteractionModeInWebViewAsync(TableInteractionMode mode)
    {
        if (PreviewWebView?.CoreWebView2 is null) return;
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync($"window.scadaModernTable?.setMode('{(mode == TableInteractionMode.Cells ? "cells" : "object")}');");
    }

    private async Task RequestTableAutoFitAsync()
    {
        if (_selectedSceneObject?.Kind != ScadaElementKind.Table || PreviewWebView?.CoreWebView2 is null) return;
        var id = System.Text.Json.JsonSerializer.Serialize(_selectedSceneObject.Id);
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync($"window.scadaModernTable?.autoFit({id});");
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

    private void RefreshTablePropertiesPanel()
    {
        if (TablePropertiesPanel is null || TableSelectionSummaryText is null) return;
        var element = _selectedSceneObject?.Kind == ScadaElementKind.Table ? _selectedSceneObject : null;
        _tableAuthoringSession.SelectTable(element?.Id);
        TablePropertiesPanel.Visibility = element is null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        if (element?.Table is null) return;
        var range = _tableEditorController.Selection;
        TableSelectionSummaryText.Text = range.RowCount == 1 && range.ColumnCount == 1
            ? $"Cellule {range.StartRow + 1},{range.StartColumn + 1}"
            : $"Plage {range.StartRow + 1},{range.StartColumn + 1}:{range.EndRow + 1},{range.EndColumn + 1}";
        TableDimensionSummaryText.Text = $"{element.Table.EffectiveRows.Count} rangees x {element.Table.EffectiveColumns.Count} colonnes";
        if (_activeRibbonKey == "Insert" && _activeInsertFamilyId == "data" && !_tableAuthoringSession.IsSurfaceOpen)
        {
            _tableAuthoringSession.OpenSurface();
            SetActiveRibbon("Insert");
        }
    }

    private void OnOpenTablePropertiesClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_selectedSceneObject?.Kind == ScadaElementKind.Table) _tableEditorController.Execute("table.properties", _selectedSceneObject);
    }

    private void OnOpenCellFormatClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_selectedSceneObject?.Kind == ScadaElementKind.Table) _tableEditorController.Execute("table.format", _selectedSceneObject);
    }

    private void OnOpenCellContentClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_selectedSceneObject?.Kind == ScadaElementKind.Table) _tableEditorController.Execute("table.content.properties", _selectedSceneObject);
    }

    private void OnOpenTableBordersClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_selectedSceneObject?.Kind == ScadaElementKind.Table) _tableEditorController.Execute("table.borders", _selectedSceneObject);
    }

    private void OnOpenTableHeadersClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_selectedSceneObject?.Kind == ScadaElementKind.Table) _tableEditorController.Execute("table.headers", _selectedSceneObject);
    }

    private void OnTableFormatScopeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (TableFormatScopeComboBox?.SelectedItem is System.Windows.Controls.ComboBoxItem { Tag: string tag } &&
            Enum.TryParse<ScadaTableFormatScopeKind>(tag, out var scope))
        {
            _tableEditorController.FormatScopeKind = scope;
            _tableAuthoringSession.SetFormatScope(scope);
        }
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
