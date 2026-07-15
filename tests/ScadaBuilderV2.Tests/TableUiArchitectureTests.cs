using ScadaBuilderV2.Application.Tables;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class TableUiArchitectureTests
{
    [TestMethod]
    public void ContextMenuExposesSpreadsheetOperationsAndCorrectEnablement()
    {
        var table = ScadaTableDefinition.CreateDefault(3, 3);
        var commands = TableContextMenuProvider.Build(table, new ScadaTableRange(0, 0, 1, 1), canPaste: false);
        var flat = Flatten(commands).ToDictionary(command => command.Id, StringComparer.Ordinal);

        Assert.IsFalse(flat["table.paste"].IsEnabled);
        Assert.IsTrue(flat["table.merge-toggle"].IsEnabled);
        Assert.AreEqual("Fusionner les cellules", flat["table.merge-toggle"].Label);
        CollectionAssert.IsSubsetOf(
            new[] { "table.copy", "table.paste", "table.row.insert", "table.column.insert", "table.row.delete", "table.column.delete", "table.clear", "table.format", "table.row.height", "table.column.width", "table.merge-toggle" },
            flat.Keys.ToArray());

        var merged = ScadaTableStructureOperations.ToggleMerge(table, new(0, 0, 1, 1));
        var mergedCommands = Flatten(TableContextMenuProvider.Build(merged, new(0, 0, 1, 1), canPaste: false)).ToDictionary(command => command.Id);
        Assert.AreEqual("Defusionner les cellules", mergedCommands["table.merge-toggle"].Label);
        Assert.IsFalse(mergedCommands.ContainsKey("table.merge"));
        Assert.IsFalse(mergedCommands.ContainsKey("table.unmerge"));
    }

    [TestMethod]
    public void TableUiLogicIsSplitFromMainWindowAndInsertDispatchIsCatalogDriven()
    {
        var main = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");
        var tableIntegration = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.TableIntegration.cs");
        var web = ReadProjectFile("src", "ScadaBuilderV2.App", "TableEditor", "TableWebViewScript.cs");
        var adapter = ReadProjectFile("src", "ScadaBuilderV2.App", "TableEditor", "TableWebViewMessageAdapter.cs");
        var propertiesViewModel = ReadProjectFile("src", "ScadaBuilderV2.App", "TableEditor", "TablePropertiesViewModel.cs");
        var ribbonViewModel = ReadProjectFile("src", "ScadaBuilderV2.App", "TableEditor", "TableRibbonViewModel.cs");
        var controller = ReadProjectFile("src", "ScadaBuilderV2.App", "TableEditor", "TableEditorController.cs");
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml");

        StringAssert.Contains(main, "InsertToolCatalog.Find(commandId)");
        StringAssert.Contains(main, "insertTool.PlacementMode == InsertPlacementMode.ContextualSurface");
        StringAssert.Contains(main, "OpenTableAuthoringSurface();");
        StringAssert.Contains(main, "\"table.merge-toggle\" => _tableAuthoringSession.SelectionContainsMergedCells");
        StringAssert.Contains(tableIntegration, "_tableRibbonViewModel.Open();");
        StringAssert.Contains(tableIntegration, "SyncTableEditorStateInWebViewAsync");
        StringAssert.Contains(tableIntegration, "TableEditorWebViewStateFactory.Create");
        StringAssert.Contains(tableIntegration, "TableEditorWebViewStateFactory.BuildApplyScript");
        Assert.IsFalse(tableIntegration.Contains("ShowDialog", StringComparison.Ordinal), "Inserer > Donnees > Tableau must open the contextual ribbon without a modal dialog.");
        Assert.IsFalse(main.Contains("case \"insert.shape.", StringComparison.Ordinal));
        Assert.IsFalse(main.Contains("case \"insert.hmi.", StringComparison.Ordinal));
        Assert.IsFalse(main.Contains("case \"insert.button.", StringComparison.Ordinal));
        Assert.IsFalse(main.Contains("ScadaTableOperations.", StringComparison.Ordinal));
        StringAssert.Contains(web, "tableTrackResize");
        StringAssert.Contains(web, "tableCellEdit");
        StringAssert.Contains(web, "setGuides");
        StringAssert.Contains(web, "setEditorState");
        StringAssert.Contains(web, "tableInteractionModeChanged");
        StringAssert.Contains(web, "data-editor-guides=\"hidden\"");
        StringAssert.Contains(web, "data-mode=\"object\"");
        StringAssert.Contains(web, "createDocumentFragment");
        StringAssert.Contains(web, "replaceChildren(grid)");
        Assert.IsFalse(web.Contains("node.addEventListener", StringComparison.Ordinal), "Cell interaction must use grid-level event delegation for 64 x 64 tables.");
        StringAssert.Contains(adapter, "tableAutoFitMeasured");
        StringAssert.Contains(adapter, "double.IsFinite");
        StringAssert.Contains(propertiesViewModel, "TablePropertiesInspector.Inspect");
        StringAssert.Contains(propertiesViewModel, "TableEditRequest");
        StringAssert.Contains(ribbonViewModel, "TableRibbonStateProvider.Create(session)");
        StringAssert.Contains(ribbonViewModel, "Input numerique");
        StringAssert.Contains(ribbonViewModel, "table.numeric.properties");
        StringAssert.Contains(controller, "OpenNumericInputProperties");
        StringAssert.Contains(controller, "SetCellBinding");
        StringAssert.Contains(controller, "RemoveCellBinding");
        StringAssert.Contains(controller, "TableBindingSafetyPolicy.Evaluate");
        StringAssert.Contains(controller, "tagCatalogProvider");
        Assert.IsFalse(controller.Contains("element.Table! with { Style = dialog.Result }", StringComparison.Ordinal), "Dialogs must submit typed table requests instead of constructing a complete definition directly.");
        Assert.IsFalse(tableIntegration.Contains(".Where(tag", StringComparison.Ordinal), "Tag filtering belongs to the inspector/view model, not MainWindow integration.");
        Assert.IsFalse(tableIntegration.Contains("ScadaTableCellBindingOperations", StringComparison.Ordinal));
        StringAssert.Contains(xaml, "x:Name=\"InsertFamilySurface\"");
        StringAssert.Contains(xaml, "x:Name=\"TableCreationConfigurationSurface\"");
        StringAssert.Contains(xaml, "x:Name=\"ElementPositionLockCheckBox\"");
        StringAssert.Contains(xaml, "x:Name=\"TopElementLockIndicator\"");
        StringAssert.Contains(xaml, "Text=\"{Binding ElementLockState.IndicatorLabel}\"");
        StringAssert.Contains(xaml, "Header=\"Tableau\"");
        StringAssert.Contains(xaml, "x:Name=\"TableMergeToggleButton\"");
        StringAssert.Contains(xaml, "x:Name=\"TableNumericInputPropertiesButton\"");
        StringAssert.Contains(xaml, "x:Name=\"TableNumericReadBindingText\"");
        StringAssert.Contains(xaml, "x:Name=\"TableNumericWriteBindingText\"");
        StringAssert.Contains(xaml, "Supprimer les surcharges de format");
    }

    [TestMethod]
    public void TableDialogLayoutKeepsConcreteWpfControlsVisible()
    {
        var dialogs = ReadProjectFile("src", "ScadaBuilderV2.App", "TableEditor", "TableDialogs.cs");

        StringAssert.Contains(dialogs, "params (string Label, FrameworkElement Control)[] fields");
        StringAssert.Contains(dialogs, "foreach (var field in fields)");
        StringAssert.Contains(dialogs, "panel.Children.Add(field.Control)");
        StringAssert.Contains(dialogs, "CellContentDialog");
        StringAssert.Contains(dialogs, "TableBorderDialog");
        StringAssert.Contains(dialogs, "ColorPickerField");
        StringAssert.Contains(dialogs, "Hériter / Réinitialiser la propriété");
        Assert.IsFalse(
            dialogs.Contains("params object[] entries", StringComparison.Ordinal),
            "Concrete TextBox, CheckBox, ComboBox, and TextBlock tuples must not be filtered through an exact boxed ValueTuple type.");
    }

    [TestMethod]
    public void NumericCellDialogDelegatesValidationAndReturnsTypedIntentions()
    {
        var viewModel = ReadProjectFile("src", "ScadaBuilderV2.App", "TableEditor", "TableNumericInputPropertiesViewModel.cs");
        var dialog = ReadProjectFile("src", "ScadaBuilderV2.App", "TableEditor", "TableNumericInputPropertiesDialog.xaml.cs");
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "TableEditor", "TableNumericInputPropertiesDialog.xaml");

        StringAssert.Contains(viewModel, "TableCellNumericInputInspector.ValidateContent");
        StringAssert.Contains(viewModel, "TableEditKind.SetNumericInputProperties");
        StringAssert.Contains(viewModel, "TableEditKind.SetCellValueBinding");
        StringAssert.Contains(viewModel, "TableEditKind.RemoveCellValueBinding");
        StringAssert.Contains(dialog, "viewModel.TryBuildRequests");
        StringAssert.Contains(dialog, "Result = requests;");
        Assert.IsFalse(dialog.Contains("ScadaTableCellBindingOperations", StringComparison.Ordinal));
        Assert.IsFalse(dialog.Contains("Writeable", StringComparison.Ordinal));
        Assert.IsFalse(dialog.Contains("ScadaTableDefinition", StringComparison.Ordinal));
        StringAssert.Contains(xaml, "Valeur initiale");
        StringAssert.Contains(xaml, "Minimum");
        StringAssert.Contains(xaml, "Maximum");
        StringAssert.Contains(xaml, "Format d'affichage");
        StringAssert.Contains(xaml, "Ajouter / Modifier");
        StringAssert.Contains(xaml, "Supprimer");
    }

    private static IEnumerable<ScadaBuilderV2.Application.Commands.EditorCommandDescriptor> Flatten(
        IEnumerable<ScadaBuilderV2.Application.Commands.EditorCommandDescriptor> commands) =>
        commands.SelectMany(command => new[] { command }.Concat(command.Children is null ? [] : Flatten(command.Children)));

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }
        Assert.Fail($"Unable to locate project file: {Path.Combine(parts)}");
        return "";
    }
}
