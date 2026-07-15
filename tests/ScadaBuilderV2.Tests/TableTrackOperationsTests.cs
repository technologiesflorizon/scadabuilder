using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class TableTrackOperationsTests
{
    [TestMethod]
    public void EqualizePreservesTotalAndDistributePreservesRatios()
    {
        var table = ScadaTableDefinition.CreateDefault(2,3) with { Columns = [new(50), new(100), new(150)] };
        var equal = ScadaTableTrackOperations.EqualizeColumns(table, [0,1,2]);
        CollectionAssert.AreEqual(new[] {100d,100d,100d}, equal.EffectiveColumns.Select(x=>x.Width).ToArray());
        var distributed = ScadaTableTrackOperations.DistributeColumns(table,[0,1,2],600);
        CollectionAssert.AreEqual(new[] {100d,200d,300d}, distributed.EffectiveColumns.Select(x=>x.Width).ToArray());
    }

    [TestMethod]
    public void HeaderRowsAlwaysFormLeadingPrefix()
    {
        var table = ScadaTableHeaderOperations.SetHeaderRowCount(ScadaTableDefinition.CreateDefault(4,2), 3);
        CollectionAssert.AreEqual(new[] {true,true,true,false}, table.EffectiveRows.Select(x=>x.IsHeader).ToArray());
    }
}
