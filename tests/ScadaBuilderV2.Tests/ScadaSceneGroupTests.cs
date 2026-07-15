using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ScadaSceneGroupTests
{
    [TestMethod]
    public void SceneGroupSelectedModernElementsCreatesParentAndRelativeChildren()
    {
        var shapeA = CreateShape("shape-001", 100, 200);
        var shapeB = CreateShape("shape-002", 150, 240);
        var scene = ScadaScene
            .CreateEmpty("win-test", "Test", new(1280, 873))
            .WithElement(shapeA)
            .WithElement(shapeB);

        scene = scene.WithGroupedElements("group-001", "Groupe pompe", ["shape-001", "shape-002"]);

        var group = scene.FindElementRecursive("group-001");
        Assert.IsNotNull(group);
        Assert.AreEqual(ScadaElementKind.Group, group.Kind);
        Assert.AreEqual(100, group.Bounds.X);
        Assert.AreEqual(200, group.Bounds.Y);
        Assert.AreEqual(70, group.Bounds.Width);
        Assert.AreEqual(60, group.Bounds.Height);
        Assert.AreEqual(2, group.ChildElements.Count);

        var childA = group.ChildElements.Single(child => child.Id == "shape-001");
        var childB = group.ChildElements.Single(child => child.Id == "shape-002");
        Assert.AreEqual(0, childA.Bounds.X);
        Assert.AreEqual(0, childA.Bounds.Y);
        Assert.AreEqual(50, childB.Bounds.X);
        Assert.AreEqual(40, childB.Bounds.Y);
        Assert.AreEqual(ElementPositionMode.Relative, childA.Layout?.PositionMode);
        Assert.AreEqual("group-001", childA.Layout?.RelativeToElementId);
        Assert.AreEqual("group-001", childB.Layout?.RelativeToElementId);
        Assert.AreEqual(1, scene.Elements.Count);
    }

    [TestMethod]
    public void SceneGroupSelectedModernElementsPreservesSiblingRenderOrder()
    {
        var lower = CreateShape("shape-lower", 100, 200);
        var upper = CreateShape("shape-upper", 150, 240);
        var scene = ScadaScene
            .CreateEmpty("win-test", "Test", new(1280, 873))
            .WithElement(lower)
            .WithElement(upper);

        scene = scene.WithGroupedElements("group-001", "Groupe pompe", ["shape-upper", "shape-lower"]);

        var group = scene.FindElementRecursive("group-001");
        Assert.IsNotNull(group);
        CollectionAssert.AreEqual(
            new[] { "shape-lower", "shape-upper" },
            group.ChildElements.Select(child => child.Id).ToArray());
    }

    [TestMethod]
    public void SceneGroupSelectedModernElementsUsesTopmostSelectedSiblingInsertion()
    {
        var lower = CreateShape("shape-lower", 100, 200);
        var middle = CreateShape("shape-middle", 120, 220);
        var upper = CreateShape("shape-upper", 150, 240);
        var scene = ScadaScene
            .CreateEmpty("win-test", "Test", new(1280, 873))
            .WithElement(lower)
            .WithElement(middle)
            .WithElement(upper);

        scene = scene.WithGroupedElements("group-001", "Groupe pompe", ["shape-lower", "shape-upper"]);

        CollectionAssert.AreEqual(
            new[] { "shape-middle", "group-001" },
            scene.Elements.Select(element => element.Id).ToArray());
    }

    [TestMethod]
    public void SceneGroupSelectedModernElementsRejectsLegacyStaticSource()
    {
        var modern = CreateShape("shape-001", 100, 200);
        var legacy = ScadaElement.CreateLegacyStatic(
            "legacy-001",
            "Legacy001",
            new SceneBounds(150, 240, 20, 20),
            new LegacySourceTrace("Wonderware/ArchestrA", "win-test", "784", "Text22", null),
            new LegacyElementPayload("text", "Text", true, "Segoe UI", 12, "#000000", "Transparent", null, null));
        var scene = ScadaScene
            .CreateEmpty("win-test", "Test", new(1280, 873))
            .WithElement(modern)
            .WithElement(legacy);

        Assert.ThrowsException<InvalidOperationException>(() =>
            scene.WithGroupedElements("group-001", "Groupe pompe", ["shape-001", "legacy-001"]));
    }

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
    public void SceneLockOperationsPreserveRecursiveLockThroughGroupAndUngroup()
    {
        var scene = ScadaScene.CreateEmpty("win-test", "Test", new(1280, 873))
            .WithElement(CreateShape("shape-001", 10, 20))
            .WithElement(CreateShape("shape-002", 40, 20));
        scene = scene.WithGroupedElements("group-001", "Group", ["shape-001", "shape-002"]);
        scene = ScadaSceneElementLockOperations.ApplyRecursive(scene, ["group-001"], true);

        Assert.IsTrue(scene.FindElementRecursive("group-001")!.IsLocked);
        Assert.IsTrue(scene.FindElementRecursive("shape-001")!.IsLocked);
        scene = scene.WithUngroupedElement("group-001");
        Assert.IsTrue(scene.FindElementRecursive("shape-001")!.IsLocked);
        Assert.IsTrue(scene.FindElementRecursive("shape-002")!.IsLocked);
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
