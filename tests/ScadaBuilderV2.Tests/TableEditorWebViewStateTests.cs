using ScadaBuilderV2.App.TableEditor;
using ScadaBuilderV2.Application.Tables;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class TableEditorWebViewStateTests
{
    [TestMethod]
    public void Factory_ProjectsEffectiveGuidesForObjectAndCellsModes()
    {
        var session = new TableAuthoringSession();
        session.SelectTable("table-1");

        var objectState = TableEditorWebViewStateFactory.Create(session);
        Assert.AreEqual(TableInteractionMode.Object, objectState.Mode);
        Assert.IsTrue(objectState.ShowEditorGuides);
        Assert.IsFalse(objectState.EditorGuidesVisible);
        StringAssert.Contains(TableEditorWebViewStateFactory.BuildApplyScript(objectState),
            "setEditorState(\"object\", true, \"table-1\")");

        session.SetMode(TableInteractionMode.Cells);
        var cellsState = TableEditorWebViewStateFactory.Create(session);
        Assert.IsTrue(cellsState.EditorGuidesVisible);
        StringAssert.Contains(TableEditorWebViewStateFactory.BuildApplyScript(cellsState),
            "setEditorState(\"cells\", true, \"table-1\")");

        session.ToggleEditorGuides();
        var hiddenState = TableEditorWebViewStateFactory.Create(session);
        Assert.IsFalse(hiddenState.ShowEditorGuides);
        Assert.IsFalse(hiddenState.EditorGuidesVisible);
    }

    [TestMethod]
    public void EditorInteractionStateNeverCarriesIndustrialBindingIds()
    {
        var session = new TableAuthoringSession();
        session.SelectTable("table-1");
        session.SetMode(TableInteractionMode.Cells);

        var script = TableEditorWebViewStateFactory.BuildApplyScript(TableEditorWebViewStateFactory.Create(session));

        Assert.IsFalse(script.Contains("ReadTagId", StringComparison.Ordinal));
        Assert.IsFalse(script.Contains("WriteTagId", StringComparison.Ordinal));
        Assert.IsFalse(script.Contains("ValueBindings", StringComparison.Ordinal));
        StringAssert.Contains(script, "setEditorState(\"cells\", true, \"table-1\")");
    }
}
