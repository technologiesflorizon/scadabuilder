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

    [TestMethod]
    public void RibbonTogglesEditorGuidesAndMergeActionFromSelectionState()
    {
        var session = new TableAuthoringSession();
        session.SelectTable("table-1");
        session.SetSelection(new(0, 0, 1, 1), containsMergedCells: false);

        var commands = TableRibbonStateProvider.Create(session).SelectMany(group => group.Commands).ToDictionary(command => command.Id);
        Assert.AreEqual("Masquer A/1", commands["table.editor-guides"].Label);
        Assert.AreEqual("Fusionner", commands["table.merge-toggle"].Label);

        session.ToggleEditorGuides();
        session.SetSelection(new(0, 0, 1, 1), containsMergedCells: true);
        commands = TableRibbonStateProvider.Create(session).SelectMany(group => group.Commands).ToDictionary(command => command.Id);
        Assert.AreEqual("Afficher A/1", commands["table.editor-guides"].Label);
        Assert.AreEqual("Defusionner", commands["table.merge-toggle"].Label);
    }
}
