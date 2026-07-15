using ScadaBuilderV2.Application.Tables;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class TableAuthoringSessionTests
{
    [TestMethod]
    public void DefaultsAndPlacementFollowApprovedSixByEightPreset()
    {
        var session = new TableAuthoringSession();
        Assert.AreEqual(6, session.CreationColumns); Assert.AreEqual(8, session.CreationRows); Assert.IsTrue(session.FirstRowIsHeader);
        session.OpenSurface(); session.BeginPlacement(); session.CompletePlacement("table-1");
        Assert.IsTrue(session.IsSurfaceOpen); Assert.IsFalse(session.IsPlacementArmed); Assert.AreEqual(TableInteractionMode.Cells, session.Mode);
    }

    [TestMethod]
    public void RibbonKeepsAddEnabledWithoutSelectionAndExplainsContextCommands()
    {
        var groups = TableRibbonStateProvider.Create(new TableAuthoringSession());
        var commands = groups.SelectMany(x=>x.Commands).ToDictionary(x=>x.Id);
        Assert.IsTrue(commands["table.add"].IsEnabled); Assert.IsFalse(commands["table.format"].IsEnabled); Assert.IsFalse(string.IsNullOrWhiteSpace(commands["table.format"].DisabledReason));
    }

    [TestMethod]
    public void SessionDoesNotOwnSceneElementsOrCoordinator()
    {
        var members = typeof(TableAuthoringSession).GetMembers().Select(m=>m.ToString() ?? "");
        Assert.IsFalse(members.Any(x=>x.Contains("ScadaElement", StringComparison.Ordinal) || x.Contains("TableEditCoordinator", StringComparison.Ordinal)));
    }
}
