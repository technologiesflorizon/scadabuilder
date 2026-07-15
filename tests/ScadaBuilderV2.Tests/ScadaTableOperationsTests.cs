using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ScadaTableOperationsTests
{
    [TestMethod]
    public void MergeAndUnmergePreserveAnchorContent()
    {
        var table = ScadaTableDefinition.CreateDefault(4, 4);
        table = ScadaTableOperations.SetContent(table, 1, 1, new ScadaTableCellContent(Text: "Pompe"));

        var merged = ScadaTableOperations.Merge(table, new ScadaTableRange(1, 1, 2, 2));
        ScadaTableOperations.ValidateDefinition(merged);
        var anchor = merged.EffectiveCells.Single(cell => cell.Row == 1 && cell.Column == 1);
        Assert.AreEqual(2, anchor.RowSpan);
        Assert.AreEqual(2, anchor.ColumnSpan);
        Assert.AreEqual("Pompe", anchor.EffectiveContent.Text);

        var unmerged = ScadaTableOperations.Unmerge(merged, 2, 2);
        ScadaTableOperations.ValidateDefinition(unmerged);
        Assert.AreEqual(16, unmerged.EffectiveCells.Count);
        Assert.AreEqual("Pompe", unmerged.EffectiveCells.Single(cell => cell.Row == 1 && cell.Column == 1).EffectiveContent.Text);
    }

    [TestMethod]
    public void PartialMergeIntersectionIsRejected()
    {
        var table = ScadaTableOperations.Merge(
            ScadaTableDefinition.CreateDefault(4, 4),
            new ScadaTableRange(1, 1, 2, 2));

        Assert.ThrowsException<InvalidOperationException>(() =>
            ScadaTableOperations.ClearContent(table, new ScadaTableRange(0, 0, 1, 1)));
    }

    [TestMethod]
    public void InsertAndDeleteTracksKeepDefinitionValid()
    {
        var table = ScadaTableOperations.Merge(
            ScadaTableDefinition.CreateDefault(3, 3),
            new ScadaTableRange(0, 0, 1, 1));
        table = ScadaTableOperations.InsertRow(table, 1);
        table = ScadaTableOperations.InsertColumn(table, 1);
        ScadaTableOperations.ValidateDefinition(table);
        Assert.AreEqual(4, table.EffectiveRows.Count);
        Assert.AreEqual(4, table.EffectiveColumns.Count);

        table = ScadaTableOperations.DeleteRow(table, 1);
        table = ScadaTableOperations.DeleteColumn(table, 1);
        ScadaTableOperations.ValidateDefinition(table);
        Assert.AreEqual(3, table.EffectiveRows.Count);
        Assert.AreEqual(3, table.EffectiveColumns.Count);
    }

    [TestMethod]
    public void ResizeProportionallyPreservesRequestedBounds()
    {
        var table = ScadaTableDefinition.CreateDefault(2, 2);
        var resized = ScadaTableOperations.ResizeProportionally(table, 400, 100);
        Assert.AreEqual(400, resized.Width, 0.001);
        Assert.AreEqual(100, resized.Height, 0.001);
    }

    [TestMethod]
    public void StyleResolverUsesPropertyByPropertyPrecedence()
    {
        var table = ScadaTableDefinition.CreateDefault(4, 3) with
        {
            Columns = ScadaTableDefinition.CreateDefault(4, 3).EffectiveColumns
                .Select((column, index) => index == 1 ? column with { Style = new ScadaTableFormat(Foreground: "#0000FF") } : column)
                .ToArray()
        };
        table = ScadaTableOperations.SetRowFormat(table, [2], new ScadaTableFormat(Background: "#FFA500"));
        table = ScadaTableOperations.SetCellFormat(table, new ScadaTableRange(2, 1, 2, 1), new ScadaTableFormat(FontWeight: "Bold"));

        var format = ScadaTableStyleResolver.Resolve(table, 2, 1);
        Assert.AreEqual("#FFA500", format.Background);
        Assert.AreEqual("#0000FF", format.Foreground);
        Assert.AreEqual("Bold", format.FontWeight);
    }

    [TestMethod]
    public void ClearContentPreservesFormatAndMerge()
    {
        var table = ScadaTableDefinition.CreateDefault(2, 2);
        table = ScadaTableOperations.SetCellFormat(table, new ScadaTableRange(0, 0, 0, 1), new ScadaTableFormat(Background: "#123456"));
        table = ScadaTableOperations.Merge(table, new ScadaTableRange(0, 0, 0, 1));
        table = ScadaTableOperations.SetContent(table, 0, 0, new ScadaTableCellContent(Text: "X"));

        var cleared = ScadaTableOperations.ClearContent(table, new ScadaTableRange(0, 0, 0, 1));
        var anchor = cleared.EffectiveCells.Single(cell => cell.Row == 0 && cell.Column == 0);
        Assert.AreEqual(string.Empty, anchor.EffectiveContent.Text);
        Assert.AreEqual("#123456", anchor.Style?.Background);
        Assert.AreEqual(2, anchor.ColumnSpan);
    }

    [TestMethod]
    public void InsertDeleteAndUnmergeKeepBindingOnCurrentAnchor()
    {
        var table = TableCellBindingOperationsTests.Bind(TableCellBindingOperationsTests.NumericTable(), 1, 1);
        table = ScadaTableOperations.InsertRow(table, 0);
        table = ScadaTableOperations.InsertColumn(table, 0);
        Assert.IsNotNull(ScadaTableCellBindingOperations.GetBinding(table, 2, 2));

        table = ScadaTableOperations.Merge(table, new ScadaTableRange(2, 2, 2, 3));
        table = ScadaTableOperations.Unmerge(table, 2, 3);
        Assert.IsNotNull(ScadaTableCellBindingOperations.GetBinding(table, 2, 2));
        Assert.IsNull(ScadaTableCellBindingOperations.GetBinding(table, 2, 3));

        table = ScadaTableOperations.DeleteRow(table, 0);
        table = ScadaTableOperations.DeleteColumn(table, 0);
        Assert.IsNotNull(ScadaTableCellBindingOperations.GetBinding(table, 1, 1));
    }

    [TestMethod]
    public void MergeRejectsAbsorbedBindingButAllowsBoundTopLeftAnchor()
    {
        var table = TableCellBindingOperationsTests.Bind(TableCellBindingOperationsTests.NumericTable(), 0, 1);
        Assert.ThrowsException<InvalidOperationException>(() =>
            ScadaTableOperations.Merge(table, new ScadaTableRange(0, 0, 0, 1)));

        table = ScadaTableCellBindingOperations.RemoveBinding(table, 0, 1);
        table = TableCellBindingOperationsTests.Bind(table, 0, 0);
        var merged = ScadaTableOperations.Merge(table, new ScadaTableRange(0, 0, 0, 1));
        Assert.IsNotNull(ScadaTableCellBindingOperations.GetBinding(merged, 0, 1));
    }
}
