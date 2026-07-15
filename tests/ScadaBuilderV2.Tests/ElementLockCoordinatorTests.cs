using ScadaBuilderV2.Application.Selection;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ElementLockCoordinatorTests
{
    [TestMethod]
    public void MixedGroupLocksWholeClosureThenUnlocksWholeClosure()
    {
        var childA = ScadaElement.CreateText("a", "A", 0, 0) with { IsLocked = true };
        var childB = ScadaElement.CreateText("b", "B", 20, 0);
        var group = new ScadaElement("g", "G", ScadaElementKind.Group, new(100, 100, 100, 50), null, Children: [childA, childB]);
        var scene = ScadaScene.CreateEmpty("s", "S", new(500, 500)).WithElement(group);
        var coordinator = new ElementLockCoordinator();

        var state = coordinator.BuildState(scene, ["g"]);
        Assert.IsTrue(state.IsMixed);
        var locked = coordinator.Toggle(scene, ["g"]);
        Assert.IsTrue(locked.AfterScene.FindElementRecursive("g")!.IsLocked);
        Assert.IsTrue(locked.AfterScene.FindElementRecursive("a")!.IsLocked);
        Assert.IsTrue(locked.AfterScene.FindElementRecursive("b")!.IsLocked);

        var unlocked = coordinator.Toggle(locked.AfterScene, ["g"]);
        Assert.IsFalse(unlocked.AfterScene.FindElementRecursive("g")!.IsLocked);
        Assert.IsFalse(unlocked.AfterScene.FindElementRecursive("a")!.IsLocked);
        Assert.IsFalse(unlocked.AfterScene.FindElementRecursive("b")!.IsLocked);
    }
}
