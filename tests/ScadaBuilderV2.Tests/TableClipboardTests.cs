using ScadaBuilderV2.Application.Tables;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class TableClipboardTests
{
    [TestMethod]
    public void CopyAndPastePreserveContentFormatAndMerge()
    {
        var table = ScadaTableDefinition.CreateDefault(4, 4);
        table = ScadaTableOperations.SetContent(table, 0, 0, new ScadaTableCellContent(Text: "A"));
        table = ScadaTableOperations.SetCellFormat(table, new ScadaTableRange(0, 0, 0, 1), new ScadaTableFormat(Background: "#123456"));
        table = ScadaTableOperations.Merge(table, new ScadaTableRange(0, 0, 0, 1));

        var payload = TableClipboard.Copy(table, new ScadaTableRange(0, 0, 0, 1));
        var pasted = TableClipboard.Paste(table, 2, 1, payload);
        var anchor = pasted.EffectiveCells.Single(cell => cell.Row == 2 && cell.Column == 1);
        Assert.AreEqual("A", anchor.EffectiveContent.Text);
        Assert.AreEqual("#123456", anchor.Style?.Background);
        Assert.AreEqual(2, anchor.ColumnSpan);
    }

    [TestMethod]
    public void CopyRejectsPartialMerge()
    {
        var table = ScadaTableOperations.Merge(ScadaTableDefinition.CreateDefault(2, 2), new ScadaTableRange(0, 0, 0, 1));
        Assert.ThrowsException<InvalidOperationException>(() => TableClipboard.Copy(table, new ScadaTableRange(0, 0, 0, 0)));
    }

    [TestMethod]
    public void ParseTsvCreatesTextCells()
    {
        var payload = TableClipboard.ParseTsv("A\tB\r\nC\tD");
        Assert.AreEqual(2, payload.Rows);
        Assert.AreEqual(2, payload.Columns);
        Assert.AreEqual("D", payload.Cells.Single(cell => cell.Row == 1 && cell.Column == 1).EffectiveContent.Text);
    }

    [TestMethod]
    public void PasteRejectsOverflow()
    {
        var table = ScadaTableDefinition.CreateDefault(2, 2);
        var payload = TableClipboard.ParseTsv("A\tB\nC\tD");
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => TableClipboard.Paste(table, 1, 1, payload));
    }
}
