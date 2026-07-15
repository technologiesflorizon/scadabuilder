using ScadaBuilderV2.Domain.Scenes;
using System.Text.Json;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ScadaTableModelTests
{
    [TestMethod]
    public void CreateDefaultBuildsSixByEightTableWithHeader()
    {
        var table = ScadaTableDefinition.CreateDefault();

        Assert.AreEqual(6, table.EffectiveRows.Count);
        Assert.AreEqual(8, table.EffectiveColumns.Count);
        Assert.AreEqual(48, table.EffectiveCells.Count);
        Assert.IsTrue(table.EffectiveRows[0].IsHeader);
        Assert.AreEqual(768d, table.Width);
        Assert.AreEqual(192d, table.Height);
    }

    [TestMethod]
    public void CreateDefaultSupportsMaximumCapacity()
    {
        var table = ScadaTableDefinition.CreateDefault(64, 64);
        Assert.AreEqual(4096, table.EffectiveCells.Count);
        ScadaTableOperations.ValidateDefinition(table);
    }

    [TestMethod]
    public void CreateDefaultCoversWin00012ScaleScenario()
    {
        var table = ScadaTableDefinition.CreateDefault(16, 10);

        Assert.AreEqual(160, table.EffectiveCells.Count);
        Assert.AreEqual(960d, table.Width);
        Assert.AreEqual(512d, table.Height);
        ScadaTableOperations.ValidateDefinition(table);
    }

    [TestMethod]
    public void CreateDefaultRejectsInvalidCapacity()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ScadaTableDefinition.CreateDefault(0, 8));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ScadaTableDefinition.CreateDefault(6, 65));
    }

    [TestMethod]
    public void TableRoundTripsThroughSceneJson()
    {
        var element = ScadaElement.CreateTable("table-1", "Tableau1", 20, 30, 3, 4);
        var boundCell = element.Table!.EffectiveCells.Single(cell => cell.Row == 1 && cell.Column == 2) with
        {
            Content = new ScadaTableCellContent(
                ScadaTableCellContentKind.InputNumeric,
                NumericValue: 17.5,
                Minimum: 0,
                Maximum: 100,
                Step: 0.1,
                DisplayFormat: "##.#"),
            ValueBindings = new ScadaTableCellValueBindings("tf100.mapping.159", "tf100.mapping.160")
        };
        element = element with
        {
            Table = element.Table with
            {
                Cells = element.Table.EffectiveCells.Select(cell =>
                    cell.Row == boundCell.Row && cell.Column == boundCell.Column ? boundCell : cell).ToArray()
            }
        };
        var json = JsonSerializer.Serialize(element);
        var restored = JsonSerializer.Deserialize<ScadaElement>(json);

        Assert.IsNotNull(restored?.Table);
        Assert.AreEqual(3, restored.Table.EffectiveRows.Count);
        Assert.AreEqual(4, restored.Table.EffectiveColumns.Count);
        Assert.AreEqual(ScadaElementKind.Table, restored.Kind);
        var restoredCell = restored.Table.EffectiveCells.Single(cell => cell.Row == 1 && cell.Column == 2);
        Assert.AreEqual("##.#", restoredCell.EffectiveContent.DisplayFormat);
        Assert.AreEqual("tf100.mapping.159", restoredCell.ValueBindings?.ReadTagId);
        Assert.AreEqual("tf100.mapping.160", restoredCell.ValueBindings?.WriteTagId);
    }

    [TestMethod]
    public void OldTableJsonWithoutDisplayFormatOrBindingsRemainsReadable()
    {
        const string json = """
            {"Row":0,"Column":0,"Content":{"Kind":2,"Text":"","Placeholder":"0","NumericValue":12.5,"Minimum":0,"Maximum":100,"Step":1,"IsReadOnly":false}}
            """;

        var restored = JsonSerializer.Deserialize<ScadaTableCell>(json);

        Assert.IsNotNull(restored);
        Assert.IsNull(restored.ValueBindings);
        Assert.IsNull(restored.EffectiveContent.DisplayFormat);
        Assert.AreEqual(12.5, restored.EffectiveContent.NumericValue);
    }

    [TestMethod]
    public void OldElementJsonWithoutTableRemainsReadable()
    {
        const string json = """
            {"Id":"text-1","DisplayName":"Text","Kind":0,"Bounds":{"X":0,"Y":0,"Width":10,"Height":10},"LegacySource":null}
            """;
        var restored = JsonSerializer.Deserialize<ScadaElement>(json);
        Assert.IsNotNull(restored);
        Assert.IsNull(restored.Table);
    }
}
