using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class TableCellBindingOperationsTests
{
    [TestMethod]
    public void SetGetRemoveNormalizeAndRedirectMergedCoordinate()
    {
        var table = NumericTable();
        table = ScadaTableOperations.Merge(table, new ScadaTableRange(0, 0, 0, 1));

        table = ScadaTableCellBindingOperations.SetBinding(
            table,
            0,
            1,
            new ScadaTableCellValueBindings(" read ", "write"));

        var binding = ScadaTableCellBindingOperations.GetBinding(table, 0, 0);
        Assert.AreEqual("read", binding?.ReadTagId);
        Assert.AreEqual("write", binding?.WriteTagId);
        Assert.AreEqual(1, ScadaTableCellBindingOperations.CountBindings(table));

        table = ScadaTableCellBindingOperations.RemoveBinding(table, 0, 1);
        Assert.IsNull(ScadaTableCellBindingOperations.GetBinding(table, 0, 0));
    }

    [TestMethod]
    public void EmptyBindingRemovesExistingBinding()
    {
        var table = Bind(NumericTable(), 0, 0);
        table = ScadaTableCellBindingOperations.SetBinding(table, 0, 0, new ScadaTableCellValueBindings(" ", null));
        Assert.AreEqual(0, ScadaTableCellBindingOperations.CountBindings(table));
    }

    [TestMethod]
    public void NonNumericAndOutOfRangeTargetsAreRejected()
    {
        var table = ScadaTableDefinition.CreateDefault(2, 2);
        Assert.ThrowsException<InvalidOperationException>(() =>
            ScadaTableCellBindingOperations.SetBinding(table, 0, 0, new ScadaTableCellValueBindings("tag")));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            ScadaTableCellBindingOperations.GetBinding(table, 5, 0));
    }

    [TestMethod]
    public void EnumerationAndRangeCountAreStable()
    {
        var table = NumericTable();
        table = Bind(table, 1, 1);
        table = Bind(table, 0, 0);

        CollectionAssert.AreEqual(
            new[] { (0, 0), (1, 1) },
            ScadaTableCellBindingOperations.EnumerateBindings(table)
                .Select(item => (item.Cell.Row, item.Cell.Column))
                .ToArray());
        Assert.AreEqual(1, ScadaTableCellBindingOperations.CountBindings(table, new ScadaTableRange(1, 0, 1, 1)));
    }

    internal static ScadaTableDefinition NumericTable(int rows = 3, int columns = 3)
    {
        var table = ScadaTableDefinition.CreateDefault(rows, columns);
        return table with
        {
            Cells = table.EffectiveCells
                .Select(cell => cell with { Content = new ScadaTableCellContent(ScadaTableCellContentKind.InputNumeric) })
                .ToArray()
        };
    }

    internal static ScadaTableDefinition Bind(ScadaTableDefinition table, int row, int column, string read = "tag.read") =>
        ScadaTableCellBindingOperations.SetBinding(table, row, column, new ScadaTableCellValueBindings(read, "tag.write"));
}
