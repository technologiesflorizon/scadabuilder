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
    public void CreateDefaultRejectsInvalidCapacity()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ScadaTableDefinition.CreateDefault(0, 8));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ScadaTableDefinition.CreateDefault(6, 65));
    }

    [TestMethod]
    public void TableRoundTripsThroughSceneJson()
    {
        var element = ScadaElement.CreateTable("table-1", "Tableau1", 20, 30, 3, 4);
        var json = JsonSerializer.Serialize(element);
        var restored = JsonSerializer.Deserialize<ScadaElement>(json);

        Assert.IsNotNull(restored?.Table);
        Assert.AreEqual(3, restored.Table.EffectiveRows.Count);
        Assert.AreEqual(4, restored.Table.EffectiveColumns.Count);
        Assert.AreEqual(ScadaElementKind.Table, restored.Kind);
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
