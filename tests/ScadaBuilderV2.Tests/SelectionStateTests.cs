using ScadaBuilderV2.Application.Commands;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class SelectionStateTests
{
    [TestMethod]
    public void LockedSelectionIgnoresNormalSelectionChanges()
    {
        var state = new SelectionState();
        state.SetSelection(["element-795"], "element-795");
        state.SetSelectionLocked(true);

        state.SetSelection(["element-118"], "element-118");

        Assert.AreEqual("element-795", state.PrimaryElementId);
        CollectionAssert.AreEqual(new[] { "element-795" }, state.SelectedElementIds.ToArray());
    }

    [TestMethod]
    public void ForceSelectionCanOverrideLockedSelection()
    {
        var state = new SelectionState();
        state.SetSelection(["element-795"], "element-795");
        state.SetSelectionLocked(true);

        state.SetSelection(["element-118"], "element-118", force: true);

        Assert.AreEqual("element-118", state.PrimaryElementId);
        CollectionAssert.AreEqual(new[] { "element-118" }, state.SelectedElementIds.ToArray());
    }
}
