using ScadaBuilderV2.Application.Selection;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ElementTransformGuardTests
{
    [TestMethod]
    public void LockedElementRejectsTranslationButAcceptsResizeWithoutTranslation()
    {
        var element = ScadaElement.CreateText("e", "E", 10, 20) with { IsLocked = true };
        var scene = ScadaScene.CreateEmpty("s", "S", new(500, 500)).WithElement(element);
        var moved = scene.WithReplacedElementRecursive(element with { Bounds = element.Bounds with { X = 11 } });
        var resized = scene.WithReplacedElementRecursive(element with { Bounds = element.Bounds with { Width = 250, Height = 80 } });
        var guard = new ElementTransformGuard();

        Assert.IsFalse(guard.CanApply(scene, moved, ["e"], out _));
        Assert.IsTrue(guard.CanApply(scene, resized, ["e"], out _));
    }

    [TestMethod]
    public void LockedDescendantRejectsGroupTranslation()
    {
        var child = ScadaElement.CreateText("c", "C", 5, 5) with { IsLocked = true };
        var group = new ScadaElement("g", "G", ScadaElementKind.Group, new(100, 100, 100, 50), null, Children: [child]);
        var scene = ScadaScene.CreateEmpty("s", "S", new(500, 500)).WithElement(group);
        var moved = scene.WithReplacedElementRecursive(group with { Bounds = group.Bounds with { X = 120 } });

        Assert.IsFalse(new ElementTransformGuard().CanApply(scene, moved, ["g"], out _));
    }
}
