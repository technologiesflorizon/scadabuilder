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
}
