using ScadaBuilderV2.Domain.Elements;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ElementGroupTests
{
    [TestMethod]
    public void GroupElementsCreatesParentAndRelativeChildren()
    {
        var shapeA = CreateShape("shape-a", 100, 50, 20, 10);
        var shapeB = CreateShape("shape-b", 140, 80, 30, 15);

        var group = ElementGroupOperations.Group("group-001", "Groupe pompe", [shapeA, shapeB]);

        Assert.AreEqual(100, group.Bounds.X);
        Assert.AreEqual(50, group.Bounds.Y);
        Assert.AreEqual(70, group.Bounds.Width);
        Assert.AreEqual(45, group.Bounds.Height);
        Assert.AreEqual("group-001", shapeA.ParentId);
        Assert.AreEqual(ElementPositionMode.Relative, shapeA.Layout.PositionMode);
        Assert.AreEqual(0, shapeA.Bounds.X);
        Assert.AreEqual(0, shapeA.Bounds.Y);
        Assert.AreEqual(40, shapeB.Bounds.X);
        Assert.AreEqual(30, shapeB.Bounds.Y);
        Assert.AreEqual(2, group.Children.Count);
    }

    [TestMethod]
    public void MovingParentKeepsChildRelativeCoordinates()
    {
        var shapeA = CreateShape("shape-a", 100, 50, 20, 10);
        var shapeB = CreateShape("shape-b", 140, 80, 30, 15);
        var group = ElementGroupOperations.Group("group-001", "Groupe pompe", [shapeA, shapeB]);

        ElementGroupOperations.MoveGroupBy(group, 25, 10);

        Assert.AreEqual(125, group.Bounds.X);
        Assert.AreEqual(60, group.Bounds.Y);
        Assert.AreEqual(0, shapeA.Bounds.X);
        Assert.AreEqual(0, shapeA.Bounds.Y);
        Assert.AreEqual(40, shapeB.Bounds.X);
        Assert.AreEqual(30, shapeB.Bounds.Y);
        var absoluteB = ElementGroupOperations.GetAbsoluteBounds(shapeB, group.Bounds);
        Assert.AreEqual(165, absoluteB.X);
        Assert.AreEqual(90, absoluteB.Y);
    }

    [TestMethod]
    public void MovingChildChangesOnlyRelativeChildCoordinates()
    {
        var shapeA = CreateShape("shape-a", 100, 50, 20, 10);
        var shapeB = CreateShape("shape-b", 140, 80, 30, 15);
        var group = ElementGroupOperations.Group("group-001", "Groupe pompe", [shapeA, shapeB]);

        ElementGroupOperations.MoveChildRelative(group, shapeB, 55, 35);

        Assert.AreEqual(100, group.Bounds.X);
        Assert.AreEqual(50, group.Bounds.Y);
        Assert.AreEqual(55, shapeB.Bounds.X);
        Assert.AreEqual(35, shapeB.Bounds.Y);
        var absoluteB = ElementGroupOperations.GetAbsoluteBounds(shapeB, group.Bounds);
        Assert.AreEqual(155, absoluteB.X);
        Assert.AreEqual(85, absoluteB.Y);
    }

    [TestMethod]
    public void UngroupPreservesVisualAbsolutePosition()
    {
        var shapeA = CreateShape("shape-a", 100, 50, 20, 10);
        var shapeB = CreateShape("shape-b", 140, 80, 30, 15);
        var group = ElementGroupOperations.Group("group-001", "Groupe pompe", [shapeA, shapeB]);
        ElementGroupOperations.MoveGroupBy(group, 25, 10);

        var ungrouped = ElementGroupOperations.Ungroup(group);

        Assert.AreEqual(2, ungrouped.Count);
        Assert.AreEqual(0, group.Children.Count);
        Assert.IsNull(shapeA.ParentId);
        Assert.AreEqual(ElementPositionMode.Absolute, shapeA.Layout.PositionMode);
        Assert.AreEqual(125, shapeA.Bounds.X);
        Assert.AreEqual(60, shapeA.Bounds.Y);
        Assert.AreEqual(165, shapeB.Bounds.X);
        Assert.AreEqual(90, shapeB.Bounds.Y);
    }

    [TestMethod]
    public void GroupsCanContainMixedElementsAndNestedGroups()
    {
        var childA = CreateShape("shape-a", 10, 10, 20, 20);
        var childB = CreateShape("shape-b", 50, 10, 20, 20);
        var innerGroup = ElementGroupOperations.Group("group-inner", "Inner", [childA, childB]);
        var numeric = new NumericInput(
            "numeric-001",
            "Temperature",
            new SceneBounds(120, 100, 80, 24),
            ScadaElementStyle.DefaultInput,
            isReadOnly: true,
            displayFormat: "###.0");

        var outerGroup = ElementGroupOperations.Group("group-outer", "Outer", [innerGroup, numeric]);

        Assert.AreEqual(2, outerGroup.Children.Count);
        Assert.AreEqual("group-outer", innerGroup.ParentId);
        Assert.AreEqual("group-outer", numeric.ParentId);
        Assert.AreEqual("group-inner", childA.ParentId);
        Assert.AreEqual("group-inner", childB.ParentId);
        Assert.IsTrue(outerGroup.Children.OfType<ElementGroup>().Single().ContainsDescendant("shape-a"));
        Assert.AreEqual(ScadaElementKind.Group, outerGroup.ToScadaElement().Kind);
        Assert.AreEqual(2, outerGroup.ToScadaElement().Children?.Count);
    }

    [TestMethod]
    public void GroupInputRejectsSelfDuplicateAndDescendantSelections()
    {
        var childA = CreateShape("shape-a", 10, 10, 20, 20);
        var childB = CreateShape("shape-b", 50, 10, 20, 20);
        var innerGroup = ElementGroupOperations.Group("group-inner", "Inner", [childA, childB]);

        Assert.ThrowsException<InvalidOperationException>(() =>
            ElementGroupOperations.Group("shape-a", "Self", [childA]));
        Assert.ThrowsException<InvalidOperationException>(() =>
            ElementGroupOperations.Group("group-dup", "Duplicate", [childA, childA]));
        Assert.ThrowsException<InvalidOperationException>(() =>
            ElementGroupOperations.Group("group-parent", "Parent", [innerGroup, childA]));
    }

    private static ShapeElement CreateShape(string id, double x, double y, double width, double height)
    {
        return new ShapeElement(
            id,
            id,
            new SceneBounds(x, y, width, height),
            ScadaElementStyle.DefaultText);
    }
}
