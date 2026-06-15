using ScadaBuilderV2.Application.Selection;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class LegacyElementSelectionModelTests
{
    [TestMethod]
    public void SnapshotKeepsOnlySelectableElementsAsSelected()
    {
        var elements = new[]
        {
            new LegacyElementListItem("legacy:002", "Text02", "text"),
            new LegacyElementListItem("legacy:001", "Text01", "text")
        };

        var snapshot = LegacyElementSelectionSnapshot.FromInventory(
            elements,
            ["legacy:001", "missing"]);

        Assert.AreEqual(2, snapshot.Elements.Count);
        Assert.AreEqual("Text01", snapshot.Elements[0].DisplayName);
        Assert.AreEqual(1, snapshot.SelectedElementIds.Count);
        Assert.IsTrue(snapshot.SelectedElementIds.Contains("legacy:001"));
        Assert.AreEqual("Text01 (text)", snapshot.PropertySummary);
    }

    [TestMethod]
    public void SnapshotPropertySummaryReflectsMultiSelection()
    {
        var elements = new[]
        {
            new LegacyElementListItem("legacy:001", "Text01", "text"),
            new LegacyElementListItem("legacy:002", "Pump", "shape")
        };

        var snapshot = LegacyElementSelectionSnapshot.FromInventory(
            elements,
            ["legacy:001", "legacy:002"]);

        Assert.AreEqual("2 elements source selectionnes", snapshot.PropertySummary);
        Assert.AreEqual(2, snapshot.SelectedElements.Count);
    }

    [TestMethod]
    public void SceneInventoryIncludesSceneObjectsBeforeSourceObjects()
    {
        var legacy = new[]
        {
            new LegacyElementListItem("source-001", "SourceText", "text")
        };
        var modern = new[]
        {
            new SceneElementListItem("object:input_text_001", "input_text_001", "InputText001 [InputText]", "InputText", "Object")
        };

        var snapshot = SceneElementInventorySnapshot.FromElements(
            legacy,
            modern,
            [],
            "input_text_001");

        Assert.AreEqual(2, snapshot.Elements.Count);
        Assert.AreEqual("Object", snapshot.Elements[0].Source);
        Assert.AreEqual("object:input_text_001", snapshot.Elements[0].Key);
        Assert.IsTrue(snapshot.SelectedKeys.Contains("object:input_text_001"));
    }

    [TestMethod]
    public void ConvertedLegacyText22IsRemovedFromElementInventoryWhenHidden()
    {
        var legacy = new[]
        {
            new LegacyElementListItem("784", "Text22", "Text", 80, 57, 45, 24, "###.0", true),
            new LegacyElementListItem("785", "Text23", "Text", 126, 57, 45, 24, "###.0", true)
        };
        var modern = new[]
        {
            new SceneElementListItem(
                "object:elementplus_numeric_display_784",
                "elementplus_numeric_display_784",
                "Element+ Text22 [InputNumeric]",
                "InputNumeric",
                "Object")
        };

        var snapshot = SceneElementInventorySnapshot.FromElements(
            legacy,
            modern,
            ["784"],
            "elementplus_numeric_display_784",
            ["784"]);

        Assert.AreEqual(2, snapshot.Elements.Count);
        Assert.IsFalse(snapshot.Elements.Any(element => element.Key == "source:784"));
        Assert.IsTrue(snapshot.Elements.Any(element => element.Key == "object:elementplus_numeric_display_784"));
        Assert.IsTrue(snapshot.Elements.Any(element => element.Key == "source:785"));
        Assert.IsFalse(snapshot.SelectedKeys.Contains("source:784"));
        Assert.IsTrue(snapshot.SelectedKeys.Contains("object:elementplus_numeric_display_784"));
    }

    [TestMethod]
    public void SceneElementInventorySupportsMultipleSelectedSceneObjects()
    {
        var snapshot = SceneElementInventorySnapshot.FromElements(
            Array.Empty<LegacyElementListItem>(),
            new[]
            {
                new SceneElementListItem("object:group-001", "group-001", "Group001 [Group]", "Group", "Object"),
                new SceneElementListItem("object:shape-001", "shape-001", "Shape001 [Shape]", "Shape", "Object")
            },
            Array.Empty<string>(),
            "group-001",
            selectedModernElementIds: new[] { "group-001", "shape-001" });

        Assert.AreEqual(2, snapshot.SelectedKeys.Count);
        Assert.IsTrue(snapshot.SelectedKeys.Contains("object:group-001"));
        Assert.IsTrue(snapshot.SelectedKeys.Contains("object:shape-001"));
    }
}
