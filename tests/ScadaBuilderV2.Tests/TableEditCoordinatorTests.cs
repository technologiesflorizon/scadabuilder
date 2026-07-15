using ScadaBuilderV2.Application.Tables;
using ScadaBuilderV2.Domain.Scenes;

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
}
