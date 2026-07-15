using ScadaBuilderV2.Application.Tables;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class TablePropertiesInspectorTests
{
    [TestMethod]
    public void InspectorDistinguishesInheritedCustomAndMixedValues()
    {
        var table = ScadaTableDefinition.CreateDefault(2, 2, firstRowIsHeader: false);
        var inherited = TablePropertiesInspector.Inspect(table, new(ScadaTableFormatScopeKind.Cells, new(0, 0, 0, 0)));
        Assert.AreEqual(TablePropertyValueState.Inherited, inherited.State);
        Assert.AreEqual("#FFFFFF", inherited.EffectiveFormat.Background);

        table = ScadaTableFormatOperations.ApplyFormat(table, new(ScadaTableFormatScopeKind.Cells, new(0, 0, 1, 1)), new(Background: "#112233"));
        var custom = TablePropertiesInspector.Inspect(table, new(ScadaTableFormatScopeKind.Cells, new(0, 0, 1, 1)));
        Assert.AreEqual(TablePropertyValueState.Custom, custom.State);
        Assert.AreEqual(TablePropertyValueState.Custom, custom.PropertyStates[nameof(ScadaTableFormat.Background)]);

        table = ScadaTableFormatOperations.ApplyFormat(table, new(ScadaTableFormatScopeKind.Cells, new(1, 1, 1, 1)), new(Background: "#445566"));
        var mixed = TablePropertiesInspector.Inspect(table, new(ScadaTableFormatScopeKind.Cells, new(0, 0, 1, 1)));
        Assert.AreEqual(TablePropertyValueState.Mixed, mixed.State);
        Assert.AreEqual(TablePropertyValueState.Mixed, mixed.PropertyStates[nameof(ScadaTableFormat.Background)]);
        Assert.IsNull(mixed.EffectiveFormat.Background);
    }

    [TestMethod]
    public void ResetPropertyPreservesOtherOverridesAndDistinctSelectedStyles()
    {
        var table = ScadaTableDefinition.CreateDefault(2, 2, firstRowIsHeader: false) with
        {
            Cells =
            [
                new(0, 0, Style: new(Background: "#111111", Foreground: "#AAAAAA")),
                new(0, 1, Style: new(Background: "#222222", Foreground: "#BBBBBB")),
                new(1, 0), new(1, 1)
            ]
        };

        var updated = ScadaTableFormatOperations.ResetProperty(
            table,
            new(ScadaTableFormatScopeKind.Cells, new(0, 0, 0, 1)),
            nameof(ScadaTableFormat.Background));

        var first = updated.EffectiveCells.Single(cell => cell.Row == 0 && cell.Column == 0).Style;
        var second = updated.EffectiveCells.Single(cell => cell.Row == 0 && cell.Column == 1).Style;
        Assert.IsNull(first!.Background);
        Assert.IsNull(second!.Background);
        Assert.AreEqual("#AAAAAA", first.Foreground);
        Assert.AreEqual("#BBBBBB", second.Foreground);
    }
}
