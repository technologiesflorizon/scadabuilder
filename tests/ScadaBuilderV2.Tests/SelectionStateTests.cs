using ScadaBuilderV2.Application.Commands;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class SelectionStateTests
{
    [TestMethod]
    public void SelectionCanChangeIndependentlyOfPersistentElementLock()
    {
        var state = new SelectionState();
        state.SetSelection(["element-795"], "element-795");
        state.SetSelection(["element-118"], "element-118");

        Assert.AreEqual("element-118", state.PrimaryElementId);
        CollectionAssert.AreEqual(new[] { "element-118" }, state.SelectedElementIds.ToArray());
    }

    [TestMethod]
    public void SelectionNormalizesDuplicatesAndPrimaryElement()
    {
        var state = new SelectionState();
        state.SetSelection([" element-118 ", "element-118", "element-795"], "missing");

        Assert.AreEqual("element-118", state.PrimaryElementId);
        CollectionAssert.AreEqual(new[] { "element-118", "element-795" }, state.SelectedElementIds.ToArray());
    }
}
