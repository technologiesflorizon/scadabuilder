using System.Globalization;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Tables;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.App.TableEditor;

namespace ScadaBuilderV2.App;

// High-level shell wiring only. Table rules, clipboard, menus and dialogs are owned
// by Application.Tables and App.TableEditor.
public partial class MainWindow
{
    private bool ForwardTableWebViewMessage(string json)
    {
        if (!TableWebViewMessageAdapter.TryParse(json, out var request, out var error) || request is null)
        {
            if (!TableWebViewMessageAdapter.IsTableMessage(json)) return false;
            SetStatus(error ?? "Message Tableau invalide.");
            return true;
        }
        var element = _activeScene?.FindElementRecursive(request.ElementId);
        if (element?.Table is null) return true;
        switch (request)
        {
            case TableSelectionRequest selection:
                _tableEditorController.Select(selection.ElementId, selection.Row, selection.Column, selection.EndRow, selection.EndColumn);
                _tableEditorController.FormatScopeKind = selection.Scope switch { "table" => ScadaTableFormatScopeKind.Table, "row" => ScadaTableFormatScopeKind.Rows, "column" => ScadaTableFormatScopeKind.Columns, _ => ScadaTableFormatScopeKind.Cells };
                _tableAuthoringSession.SelectTable(selection.ElementId);
                _tableAuthoringSession.SetSelection(_tableEditorController.Selection, _tableEditorController.SelectionContainsMergedCells(element));
                SelectModernElement(selection.ElementId); RefreshTablePropertiesPanel();
                RefreshTableRibbonSurface();
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
            case TableInteractionModeChangedRequest modeChanged:
                _tableAuthoringSession.SelectTable(modeChanged.ElementId);
                _tableAuthoringSession.SetMode(modeChanged.Mode == "cells" ? TableInteractionMode.Cells : TableInteractionMode.Object);
                RefreshTableRibbonSurface();
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
        _tableRibbonViewModel.Open();
        _activeInsertFamilyId = "data";
        SetActiveRibbon("Insert");
    }

    private void CloseTableAuthoringSurface()
    {
        _tableRibbonViewModel.BackToDataTools();
        SetActiveRibbon("Insert");
        _ = SyncTableEditorStateInWebViewAsync();
    }

    private void RefreshTableRibbonSurface()
    {
        ActiveRibbonGroups.Clear();
        var table = _selectedSceneObject?.Kind == ScadaElementKind.Table ? _selectedSceneObject : null;
        _tableRibbonViewModel.UpdateNumericInspection(table is null ? null : _tableEditorController.InspectNumericInput(table));
        foreach (var group in _tableRibbonViewModel.Groups)
            ActiveRibbonGroups.Add(CreateRibbonGroupViewModel(group));
        if (TableCreationConfigurationSurface is not null)
        {
            TableCreationConfigurationSurface.Visibility = System.Windows.Visibility.Visible;
            TableRowsTextBox.Text = _tableRibbonViewModel.CreationRows.ToString(CultureInfo.InvariantCulture);
            TableColumnsTextBox.Text = _tableRibbonViewModel.CreationColumns.ToString(CultureInfo.InvariantCulture);
            TableFirstHeaderCheckBox.IsChecked = _tableRibbonViewModel.FirstRowIsHeader;
        }
    }

    private void BeginConfiguredTablePlacement()
    {
        if (!int.TryParse(TableRowsTextBox.Text, out var rows) || !int.TryParse(TableColumnsTextBox.Text, out var columns) || rows is < 1 or > 64 || columns is < 1 or > 64)
        {
            SetStatus("Tableau: les rangees et colonnes doivent etre comprises entre 1 et 64.");
            return;
        }
        _tableRibbonViewModel.ConfigureCreation(rows, columns, TableFirstHeaderCheckBox.IsChecked == true);
        _tableAuthoringSession.BeginPlacement();
        BeginModernElementPlacement(ScadaElementKind.Table, "table.add");
    }

    private async Task SetTableModeAsync(TableInteractionMode mode)
    {
        if (_selectedSceneObject?.Kind != ScadaElementKind.Table) return;
        _tableAuthoringSession.SelectTable(_selectedSceneObject.Id);
        _tableAuthoringSession.SetMode(mode);
        await SyncTableEditorStateInWebViewAsync();
        RefreshTableRibbonSurface();
    }

    // Keeps the application-owned Object/Cells and guide state atomic across WebView rerenders.
    private async Task SyncTableEditorStateInWebViewAsync()
    {
        if (PreviewWebView?.CoreWebView2 is null) return;
        var state = TableEditorWebViewStateFactory.Create(_tableAuthoringSession);
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync(TableEditorWebViewStateFactory.BuildApplyScript(state));
    }

    private async Task ToggleTableEditorGuidesAsync()
    {
        if (_selectedSceneObject?.Kind != ScadaElementKind.Table) return;
        _tableAuthoringSession.ToggleEditorGuides();
        await SyncTableEditorStateInWebViewAsync();
        RefreshTableRibbonSurface();
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
        if (updated.Kind == ScadaElementKind.Table)
        {
            _tableAuthoringSession.SetSelection(_tableEditorController.Selection, _tableEditorController.SelectionContainsMergedCells(updated));
            if (_tableAuthoringSession.IsSurfaceOpen) RefreshTableRibbonSurface();
        }
        _ = RenderModernSceneAsync();
    }

    private bool CanCommitTableTransform(ScadaElement updated)
    {
        if (_activeScene is null) return false;
        var current = _activeScene.FindElementRecursive(updated.Id);
        if (current is null) return false;
        if (Math.Abs(current.Bounds.X - updated.Bounds.X) < 0.001 && Math.Abs(current.Bounds.Y - updated.Bounds.Y) < 0.001)
            return true;
        return CanApplyElementTransform(_activeScene.WithReplacedElementRecursive(updated), [updated.Id]);
    }

    private void RefreshTablePropertiesPanel()
    {
        if (TablePropertiesPanel is null || TableSelectionSummaryText is null) return;
        var element = _selectedSceneObject?.Kind == ScadaElementKind.Table ? _selectedSceneObject : null;
        var previousTableId = _tableAuthoringSession.TableElementId;
        var previousMode = _tableAuthoringSession.Mode;
        _tableAuthoringSession.SelectTable(element?.Id);
        if (!string.Equals(previousTableId, _tableAuthoringSession.TableElementId, StringComparison.Ordinal) || previousMode != _tableAuthoringSession.Mode)
            _ = SyncTableEditorStateInWebViewAsync();
        TablePropertiesPanel.Visibility = element is null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        if (element?.Table is null) return;
        var range = _tableEditorController.Selection;
        var containsMergedCells = _tableEditorController.SelectionContainsMergedCells(element);
        _tableAuthoringSession.SetSelection(range, containsMergedCells);
        TableSelectionSummaryText.Text = range.RowCount == 1 && range.ColumnCount == 1
            ? $"Cellule {range.StartRow + 1},{range.StartColumn + 1}"
            : $"Plage {range.StartRow + 1},{range.StartColumn + 1}:{range.EndRow + 1},{range.EndColumn + 1}";
        TableDimensionSummaryText.Text = $"{element.Table.EffectiveRows.Count} rangees x {element.Table.EffectiveColumns.Count} colonnes";
        TableFormatStateSummaryText.Text = _tableEditorController.InspectFormatState(element);
        var numericInput = _tableEditorController.InspectNumericInput(element);
        TableNumericInputStateText.Text = numericInput.IsNumericInput
            ? "Input numerique"
            : numericInput.Diagnostic ?? "Cellule non numerique";
        TableNumericReadBindingText.Text = $"Lire valeur : {numericInput.ReadBindingSummary}";
        TableNumericWriteBindingText.Text = $"Ecrire valeur : {numericInput.WriteBindingSummary}";
        TableNumericInputPropertiesButton.IsEnabled = numericInput.CanEditProperties;
        TableMergeToggleButton.Content = containsMergedCells ? "Défusionner les cellules" : "Fusionner les cellules";
        TableMergeToggleButton.IsEnabled = containsMergedCells || range.RowCount > 1 || range.ColumnCount > 1;
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

    private void OnOpenTableNumericInputPropertiesClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_selectedSceneObject?.Kind == ScadaElementKind.Table)
            _tableEditorController.OpenNumericInputProperties(_selectedSceneObject);
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
            if (_selectedSceneObject?.Kind == ScadaElementKind.Table)
                TableFormatStateSummaryText.Text = _tableEditorController.InspectFormatState(_selectedSceneObject);
        }
    }

    private void OnToggleMergeTableCellsClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_selectedSceneObject?.Kind == ScadaElementKind.Table) _tableEditorController.Execute("table.merge-toggle", _selectedSceneObject);
    }

    private void OnResetTableFormatScopeClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_selectedSceneObject?.Kind == ScadaElementKind.Table) _tableEditorController.Execute("table.format.reset", _selectedSceneObject);
    }

    private void OnDistributeTableRowsClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_selectedSceneObject?.Kind == ScadaElementKind.Table) _tableEditorController.Execute("table.distribute.rows", _selectedSceneObject);
    }

    private void OnDistributeTableColumnsClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_selectedSceneObject?.Kind == ScadaElementKind.Table) _tableEditorController.Execute("table.distribute.columns", _selectedSceneObject);
    }

    private void OnMarkTableHeaderClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_selectedSceneObject?.Kind == ScadaElementKind.Table) _tableEditorController.Execute("table.header.mark", _selectedSceneObject);
    }

    private void OnUnmarkTableHeaderClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_selectedSceneObject?.Kind == ScadaElementKind.Table) _tableEditorController.Execute("table.header.unmark", _selectedSceneObject);
    }
}
