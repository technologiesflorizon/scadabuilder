using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ScadaSceneGroupTests
{
    [TestMethod]
    public void SceneFindReplaceAndDeleteWorkRecursivelyForGroupChildren()
    {
        var child = CreateShape("shape-001", 5, 6);
        var group = CreateGroup("group-001", 100, 200, [child]);
        var scene = ScadaScene
            .CreateEmpty("win-test", "Test", new(1280, 873))
            .WithElement(group);

        Assert.IsNotNull(scene.FindElementRecursive("shape-001"));
        Assert.AreEqual("group-001", scene.FindParentOf("shape-001")?.Id);

        var updatedChild = child with { Bounds = child.Bounds with { X = 25, Y = 35 } };
        scene = scene.WithReplacedElementRecursive(updatedChild);

        Assert.AreEqual(25, scene.FindElementRecursive("shape-001")?.Bounds.X);
        Assert.AreEqual(35, scene.FindElementRecursive("shape-001")?.Bounds.Y);

        scene = scene.WithoutElementRecursive("shape-001");

        Assert.IsNull(scene.FindElementRecursive("shape-001"));
        Assert.AreEqual(0, scene.FindElementRecursive("group-001")?.ChildElements.Count);
    }

    [TestMethod]
    public void SceneUngroupKeepsNestedChildVisualPosition()
    {
        var child = CreateShape("shape-001", 5, 6);
        var group = CreateGroup("group-001", 100, 200, [child]);
        var scene = ScadaScene
            .CreateEmpty("win-test", "Test", new(1280, 873))
            .WithElement(group);

        scene = scene.WithUngroupedElement("group-001");

        var ungrouped = scene.FindElementRecursive("shape-001");
        Assert.IsNotNull(ungrouped);
        Assert.AreEqual(105, ungrouped.Bounds.X);
        Assert.AreEqual(206, ungrouped.Bounds.Y);
        Assert.AreEqual(ElementPositionMode.Absolute, ungrouped.Layout?.PositionMode);
        Assert.IsNull(scene.FindElementRecursive("group-001"));
    }

    [TestMethod]
    public void SceneUngroupRemovesEmptyLegacyFrameGroup()
    {
        var group = CreateGroup("group-001", 100, 200, []);
        var scene = ScadaScene
            .CreateEmpty("win-test", "Test", new(1280, 873))
            .WithElement(group);

        scene = scene.WithUngroupedElement("group-001");

        Assert.IsNull(scene.FindElementRecursive("group-001"));
    }

    [TestMethod]
    public void ConvertedLegacySourceIdsIncludeGroupChildren()
    {
        var child = CreateShape("shape-001", 5, 6) with
        {
            LegacySource = new LegacySourceTrace("Wonderware/ArchestrA", "win00008", "784", "Text22", null)
        };
        var group = CreateGroup("group-001", 100, 200, [child]);
        var scene = ScadaScene
            .CreateEmpty("win-test", "Test", new(1280, 873))
            .WithLegacyTextOverride("784", "###.0")
            .WithCommittedElementPlusConversion(group);

        Assert.IsTrue(scene.GetConvertedLegacySourceElementIds().Contains("784"));
        Assert.IsFalse(scene.TextOverrides.Any(overrideItem => overrideItem.SourceElementId == "784"));
    }

    private static ScadaElement CreateShape(string id, double x, double y)
    {
        return new ScadaElement(
            id,
            id,
            ScadaElementKind.Shape,
            new SceneBounds(x, y, 20, 20),
            null,
            new ScadaElementLayout(ElementPositionMode.Relative, "group-001"),
            ScadaElementStyle.DefaultText,
            null);
    }

    private static ScadaElement CreateGroup(string id, double x, double y, IReadOnlyList<ScadaElement> children)
    {
        return new ScadaElement(
            id,
            id,
            ScadaElementKind.Group,
            new SceneBounds(x, y, 80, 80),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            null,
            children);
    }
}
