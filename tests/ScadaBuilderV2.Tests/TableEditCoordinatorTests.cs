using ScadaBuilderV2.Application.Tables;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class TableEditCoordinatorTests
{
    private readonly TableEditCoordinator _coordinator = new();

    [TestMethod]
    public void MergeReturnsUpdatedElementAndKeepsSourceImmutable()
    {
        var source = ScadaElement.CreateTable("t1", "Tableau", 10, 20, 3, 3);
        var result = _coordinator.Apply(source, new TableEditRequest(
            TableEditKind.Merge,
            Range: new ScadaTableRange(0, 0, 1, 1)));

        Assert.IsTrue(result.Succeeded, result.Error);
        Assert.AreEqual(9, source.Table!.EffectiveCells.Count);
        Assert.AreEqual(6, result.Element.Table!.EffectiveCells.Count);
        Assert.AreEqual("Fusion cellules", result.Label);
    }

    [TestMethod]
    public void ToggleMergeUsesCurrentSelectionState()
    {
        var source = ScadaElement.CreateTable("t1", "Tableau", 10, 20, 3, 3);
        var range = new ScadaTableRange(0, 0, 1, 1);

        var merged = _coordinator.Apply(source, new TableEditRequest(TableEditKind.ToggleMerge, Range: range));
        Assert.IsTrue(merged.Succeeded, merged.Error);
        Assert.IsTrue(ScadaTableStructureOperations.ContainsMergedCells(merged.Element.Table!, range));

        var unmerged = _coordinator.Apply(merged.Element, new TableEditRequest(TableEditKind.ToggleMerge, Range: range));
        Assert.IsTrue(unmerged.Succeeded, unmerged.Error);
        Assert.IsFalse(ScadaTableStructureOperations.ContainsMergedCells(unmerged.Element.Table!, range));
        Assert.AreEqual(9, unmerged.Element.Table!.EffectiveCells.Count);
    }

    [TestMethod]
    public void InvalidRequestReturnsDiagnosticWithoutMutation()
    {
        var source = ScadaElement.CreateTable("t1", "Tableau", 0, 0, 2, 2);
        var result = _coordinator.Apply(source, new TableEditRequest(TableEditKind.DeleteRow, Row: 8));
        Assert.IsFalse(result.Succeeded);
        Assert.AreSame(source, result.Element);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Error));
    }

    [TestMethod]
    public void TrackSizeUpdatesElementBounds()
    {
        var source = ScadaElement.CreateTable("t1", "Tableau", 0, 0, 2, 2);
        var result = _coordinator.Apply(source, new TableEditRequest(
            TableEditKind.SetColumnWidth,
            Range: new ScadaTableRange(0, 0, 1, 0),
            Width: 150));
        Assert.IsTrue(result.Succeeded, result.Error);
        Assert.AreEqual(246, result.Element.Bounds.Width, 0.001);
    }

    [TestMethod]
    public void NonTableIsRejected()
    {
        var source = ScadaElement.CreateText("x", "Texte", 0, 0);
        var result = _coordinator.Apply(source, new TableEditRequest(TableEditKind.ClearContent, Range: new ScadaTableRange(0, 0, 0, 0)));
        Assert.IsFalse(result.Succeeded);
    }

    [TestMethod]
    public void TablePropertiesRequestUpdatesStyleAndExactDimensionsAtomically()
    {
        var source = ScadaElement.CreateTable("t1", "Tableau", 12, 18, 2, 2);
        var style = source.Table!.EffectiveStyle with { Base = source.Table.EffectiveStyle.Base! with { Background = "#102030" } };
        var result = _coordinator.Apply(source, new TableEditRequest(
            TableEditKind.SetTableProperties,
            Width: 300,
            Height: 120,
            TableStyle: style));

        Assert.IsTrue(result.Succeeded, result.Error);
        Assert.AreEqual(300, result.Element.Table!.Width, 0.001);
        Assert.AreEqual(120, result.Element.Table.Height, 0.001);
        Assert.AreEqual("#102030", result.Element.Table.EffectiveStyle.Base!.Background);
        Assert.AreEqual(source.Bounds.X, result.Element.Bounds.X);
        Assert.AreEqual(source.Bounds.Y, result.Element.Bounds.Y);
    }

    [TestMethod]
    public void ResetFormatPropertyClearsOnlyRequestedProperty()
    {
        var source = ScadaElement.CreateTable("t1", "Tableau", 0, 0, 2, 2) with
        {
            Table = ScadaTableFormatOperations.ApplyFormat(
                ScadaTableDefinition.CreateDefault(2, 2),
                new(ScadaTableFormatScopeKind.Cells, new(0, 0, 0, 0)),
                new(Background: "#102030", Foreground: "#FFFFFF"))
        };
        var result = _coordinator.Apply(source, new TableEditRequest(
            TableEditKind.ResetFormatProperty,
            FormatScope: new(ScadaTableFormatScopeKind.Cells, new(0, 0, 0, 0)),
            PropertyName: nameof(ScadaTableFormat.Background)));

        Assert.IsTrue(result.Succeeded, result.Error);
        var style = result.Element.Table!.EffectiveCells.Single(cell => cell.Row == 0 && cell.Column == 0).Style;
        Assert.IsNull(style!.Background);
        Assert.AreEqual("#FFFFFF", style.Foreground);
    }

    [TestMethod]
    public void BindingRequestsValidateActiveReadableAndWritableTags()
    {
        var source = NumericTableElement();
        var catalog = Catalog();
        var read = _coordinator.Apply(source, new TableEditRequest(
            TableEditKind.SetCellValueBinding,
            Row: 0,
            Column: 0,
            BindingKind: TableCellBindingKind.Read,
            TagId: "tag.read"), catalog);
        Assert.IsTrue(read.Succeeded, read.Error);
        Assert.AreEqual("tag.read", ScadaTableCellBindingOperations.GetBinding(read.Element.Table!, 0, 0)?.ReadTagId);

        var rejected = _coordinator.Apply(read.Element, new TableEditRequest(
            TableEditKind.SetCellValueBinding,
            Row: 0,
            Column: 0,
            BindingKind: TableCellBindingKind.Write,
            TagId: "tag.read"), catalog);
        Assert.IsFalse(rejected.Succeeded);
        Assert.AreSame(read.Element, rejected.Element);

        var write = _coordinator.Apply(read.Element, new TableEditRequest(
            TableEditKind.SetCellValueBinding,
            Range: new ScadaTableRange(0, 0, 0, 0),
            BindingKind: TableCellBindingKind.Write,
            TagId: "tag.write"), catalog);
        Assert.IsTrue(write.Succeeded, write.Error);

        var removed = _coordinator.Apply(write.Element, new TableEditRequest(
            TableEditKind.RemoveCellValueBinding,
            Row: 0,
            Column: 0,
            BindingKind: TableCellBindingKind.Read));
        Assert.IsTrue(removed.Succeeded, removed.Error);
        Assert.IsNull(ScadaTableCellBindingOperations.GetBinding(removed.Element.Table!, 0, 0)?.ReadTagId);
        Assert.AreEqual("tag.write", ScadaTableCellBindingOperations.GetBinding(removed.Element.Table!, 0, 0)?.WriteTagId);
    }

    [TestMethod]
    public void DestructiveEditsRequireConfirmationAndThenCommitAtomically()
    {
        var source = NumericTableElement() with
        {
            Table = TableCellBindingOperationsTests.Bind(NumericTableElement().Table!, 0, 0)
        };
        var denied = _coordinator.Apply(source, new TableEditRequest(TableEditKind.DeleteRow, Row: 0));
        Assert.IsFalse(denied.Succeeded);
        Assert.AreSame(source, denied.Element);
        StringAssert.Contains(denied.Error!, "1 binding");

        var confirmed = _coordinator.Apply(source, new TableEditRequest(
            TableEditKind.DeleteRow,
            Row: 0,
            ConfirmedBindingRemoval: true));
        Assert.IsTrue(confirmed.Succeeded, confirmed.Error);
        Assert.AreEqual(1, confirmed.Element.Table!.EffectiveRows.Count);

        var conversionDenied = _coordinator.Apply(source, new TableEditRequest(
            TableEditKind.ConvertContentKind,
            Range: new ScadaTableRange(0, 0, 0, 0),
            ContentKind: ScadaTableCellContentKind.Text));
        Assert.IsFalse(conversionDenied.Succeeded);
        var converted = _coordinator.Apply(source, new TableEditRequest(
            TableEditKind.ConvertContentKind,
            Range: new ScadaTableRange(0, 0, 0, 0),
            ContentKind: ScadaTableCellContentKind.Text,
            ConfirmedBindingRemoval: true));
        Assert.IsTrue(converted.Succeeded, converted.Error);
        Assert.IsNull(converted.Element.Table!.EffectiveCells.Single(cell => cell.Row == 0 && cell.Column == 0).ValueBindings);
    }

    [TestMethod]
    public void ReadOnlyPropertiesRequireConfirmationAndRemoveWriteBinding()
    {
        var source = NumericTableElement();
        source = _coordinator.Apply(source, new TableEditRequest(
            TableEditKind.SetCellValueBinding,
            Row: 0,
            Column: 0,
            BindingKind: TableCellBindingKind.Write,
            TagId: "tag.write"), Catalog()).Element;
        var content = source.Table!.EffectiveCells.Single(cell => cell.Row == 0 && cell.Column == 0).EffectiveContent with
        {
            IsReadOnly = true,
            Minimum = 0,
            Maximum = 10,
            Step = 1,
            DisplayFormat = "##.#"
        };

        var denied = _coordinator.Apply(source, new TableEditRequest(
            TableEditKind.SetNumericInputProperties,
            Row: 0,
            Column: 0,
            Content: content));
        Assert.IsFalse(denied.Succeeded);
        Assert.AreSame(source, denied.Element);

        var confirmed = _coordinator.Apply(source, new TableEditRequest(
            TableEditKind.SetNumericInputProperties,
            Row: 0,
            Column: 0,
            Content: content,
            ConfirmedBindingRemoval: true));
        Assert.IsTrue(confirmed.Succeeded, confirmed.Error);
        Assert.IsTrue(confirmed.Element.Table!.EffectiveCells.Single(cell => cell.Row == 0 && cell.Column == 0).EffectiveContent.IsReadOnly);
        Assert.IsNull(ScadaTableCellBindingOperations.GetBinding(confirmed.Element.Table, 0, 0));
    }

    [TestMethod]
    public void InspectorRedirectsMergedCoordinateAndFiltersTagChoices()
    {
        var element = NumericTableElement();
        element = element with { Table = ScadaTableOperations.Merge(element.Table!, new ScadaTableRange(0, 0, 0, 1)) };
        var inspection = TableCellNumericInputInspector.Inspect(element, element.Id, new ScadaTableRange(0, 1, 0, 1), Catalog());
        Assert.IsTrue(inspection.HasSingleAnchor);
        Assert.AreEqual(0, inspection.AnchorRow);
        Assert.AreEqual(0, inspection.AnchorColumn);
        Assert.AreEqual("A1", inspection.CellAddress);
        Assert.AreEqual(2, inspection.ReadTags.Count);
        Assert.AreEqual(1, inspection.WriteTags.Count);
    }

    private static ScadaElement NumericTableElement()
    {
        var element = ScadaElement.CreateTable("t1", "Tableau", 0, 0, 2, 2);
        return element with
        {
            Table = element.Table! with
            {
                Cells = element.Table.EffectiveCells.Select(cell => cell with
                {
                    Content = new ScadaTableCellContent(ScadaTableCellContentKind.InputNumeric)
                }).ToArray()
            }
        };
    }

    private static ScadaTagCatalog Catalog() => new(
        "tf100web-scada-tags-v1",
        [
            new ScadaTagDefinition("tag.read", "Lecture", Writeable: false),
            new ScadaTagDefinition("tag.write", "Ecriture", Writeable: true),
            new ScadaTagDefinition("tag.disabled", "Inactive", Writeable: true, Enabled: false)
        ]);
}
