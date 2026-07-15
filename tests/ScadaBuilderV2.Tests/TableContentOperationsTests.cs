using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class TableContentOperationsTests
{
    [TestMethod]
    public void ConversionMatrixRemovesIncompatibleFields()
    {
        var numeric = new ScadaTableCellContent(ScadaTableCellContentKind.InputNumeric, Placeholder:"p", NumericValue:12.5, Minimum:1, Maximum:20, Step:.5, IsReadOnly:true);
        var text = ScadaTableContentOperations.Convert(numeric, ScadaTableCellContentKind.Text);
        Assert.AreEqual("12.5", text.Text); Assert.IsNull(text.NumericValue); Assert.AreEqual("", text.Placeholder); Assert.IsFalse(text.IsReadOnly);
        var input = ScadaTableContentOperations.Convert(numeric, ScadaTableCellContentKind.InputText);
        Assert.AreEqual("12.5", input.Text); Assert.AreEqual("p", input.Placeholder); Assert.IsNull(input.Minimum); Assert.IsTrue(input.IsReadOnly);
    }

    [TestMethod]
    public void InvalidTextConvertsToEmptyNumericWithoutBlocking()
    {
        var result = ScadaTableContentOperations.Convert(new(ScadaTableCellContentKind.Text, "not-number"), ScadaTableCellContentKind.InputNumeric);
        Assert.IsNull(result.NumericValue);
    }

    [TestMethod]
    public void FormatResetOnlyClearsRequestedProperty()
    {
        var table = ScadaTableDefinition.CreateDefault(2,2);
        var scope = new ScadaTableFormatScope(ScadaTableFormatScopeKind.Cells, new(0,0,0,0));
        table = ScadaTableFormatOperations.ApplyFormat(table, scope, new(Background:"#123456", FontSize:18, TextWrap:true));
        table = ScadaTableFormatOperations.ResetProperty(table, scope, nameof(ScadaTableFormat.Background));
        var local = table.EffectiveCells.Single(c=>c.Row==0&&c.Column==0).Style!;
        Assert.IsNull(local.Background); Assert.AreEqual(18d, local.FontSize); Assert.AreEqual(true, local.TextWrap);
    }

    [TestMethod]
    public void ClearBoundCellPreservesNumericPropertiesAndBindingButClearsInitialValue()
    {
        var table = TableCellBindingOperationsTests.NumericTable();
        table = ScadaTableOperations.SetContent(table, 0, 0, new ScadaTableCellContent(
            ScadaTableCellContentKind.InputNumeric,
            Placeholder: "Pression",
            NumericValue: 12.5,
            Minimum: 0,
            Maximum: 20,
            Step: 0.5,
            DisplayFormat: "0.0"));
        table = TableCellBindingOperationsTests.Bind(table, 0, 0);

        var cleared = ScadaTableContentOperations.ClearContent(table, new ScadaTableRange(0, 0, 0, 0));
        var cell = cleared.EffectiveCells.Single(candidate => candidate.Row == 0 && candidate.Column == 0);

        Assert.AreEqual(ScadaTableCellContentKind.InputNumeric, cell.EffectiveContent.Kind);
        Assert.IsNull(cell.EffectiveContent.NumericValue);
        Assert.AreEqual(0d, cell.EffectiveContent.Minimum);
        Assert.AreEqual("0.0", cell.EffectiveContent.DisplayFormat);
        Assert.IsNotNull(cell.ValueBindings);
    }

    [TestMethod]
    public void BoundCellCannotConvertUntilBindingIsRemoved()
    {
        var table = TableCellBindingOperationsTests.Bind(TableCellBindingOperationsTests.NumericTable(), 0, 0);
        var range = new ScadaTableRange(0, 0, 0, 0);

        Assert.ThrowsException<InvalidOperationException>(() =>
            ScadaTableContentOperations.ConvertKind(table, range, ScadaTableCellContentKind.Text));

        var unbound = ScadaTableCellBindingOperations.RemoveBinding(table, 0, 0);
        var converted = ScadaTableContentOperations.ConvertKind(unbound, range, ScadaTableCellContentKind.Text);
        Assert.AreEqual(ScadaTableCellContentKind.Text, converted.EffectiveCells[0].EffectiveContent.Kind);
    }
}
