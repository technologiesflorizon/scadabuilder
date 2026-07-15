using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class TableBorderOperationsTests
{
    [TestMethod]
    public void OutlineExpandsToPhysicalSegmentsAndRoundTripsThroughMerge()
    {
        var table = ScadaTableDefinition.CreateDefault(2,2);
        var border = new ScadaTableBorder(ScadaTableGridStyle.Double, "#ff0000", 3);
        table = ScadaTableBorderOperations.ApplyPreset(table, new(0,0,1,1), ScadaTableBorderPreset.Outline, border);
        Assert.AreEqual(8, table.EffectiveBorderOverrides.Count);
        var merged = ScadaTableOperations.Merge(table, new(0,0,1,1));
        Assert.AreEqual(8, merged.EffectiveBorderOverrides.Count);
        Assert.IsFalse(ScadaTableBorderResolver.IsVisible(merged, ScadaTableBorderOrientation.Horizontal, 1, 0));
        var unmerged = ScadaTableOperations.Unmerge(merged,0,0);
        Assert.IsTrue(ScadaTableBorderResolver.IsVisible(unmerged, ScadaTableBorderOrientation.Horizontal, 1, 0));
    }
}
