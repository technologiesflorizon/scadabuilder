using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class EditorHistoryServiceTests
{
    [TestMethod]
    public async Task BackgroundActionUndoRedoRestoresSceneProperty()
    {
        var history = new EditorHistoryService();
        var scene = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithBackgroundColor("#000000");
        var dirtyCount = 0;
        var refreshCount = 0;
        var status = "";
        var context = new EditorHistoryContext
        {
            ActiveSceneId = scene.Id,
            GetActiveScene = () => scene,
            ReplaceActiveScene = updated => scene = updated,
            MarkDirty = () => dirtyCount++,
            RefreshPreviewAsync = () =>
            {
                refreshCount++;
                return Task.CompletedTask;
            },
            SetStatus = value => status = value
        };

        history.Push(new SceneBackgroundChangedAction(scene.Id, "#000000", "#2090A0"));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.AreEqual("#000000", scene.BackgroundColor);
        Assert.AreEqual(1, dirtyCount);
        Assert.AreEqual(1, refreshCount);
        StringAssert.Contains(status, "Undo fond de scene");

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.AreEqual("#2090A0", scene.BackgroundColor);
        Assert.AreEqual(2, dirtyCount);
        Assert.AreEqual(2, refreshCount);
        StringAssert.Contains(status, "Redo fond de scene");
    }

    [TestMethod]
    public async Task ModernBoundsActionUndoRedoRestoresElementBounds()
    {
        var element = ScadaElement.CreateInputText("input-001", "Input001", 10, 20);
        var scene = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithElement(element);
        var history = new EditorHistoryService();
        var before = element.Bounds;
        var after = new SceneBounds(40, 50, 220, 44);
        scene = scene.WithReplacedElementRecursive(element with { Bounds = after });
        var context = CreateContext(scene, updated => scene = updated);

        history.Push(new ModernElementBoundsChangedAction(scene.Id, element.Id, before, after));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.AreEqual(before.X, scene.FindElementRecursive(element.Id)?.Bounds.X);
        Assert.AreEqual(before.Y, scene.FindElementRecursive(element.Id)?.Bounds.Y);

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.AreEqual(after.X, scene.FindElementRecursive(element.Id)?.Bounds.X);
        Assert.AreEqual(after.Width, scene.FindElementRecursive(element.Id)?.Bounds.Width);
    }

    [TestMethod]
    public async Task ModernElementChangeActionUndoRedoRestoresElementSnapshot()
    {
        var element = ScadaElement.CreateInputText("input-001", "Input001", 10, 20);
        var updated = element with { DisplayName = "Input renamed" };
        var scene = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithElement(updated);
        var history = new EditorHistoryService();
        var context = CreateContext(scene, replacement => scene = replacement);

        history.Push(new ModernElementChangedAction(scene.Id, element, updated, "proprietes Element+"));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.AreEqual("Input001", scene.FindElementRecursive(element.Id)?.DisplayName);

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.AreEqual("Input renamed", scene.FindElementRecursive(element.Id)?.DisplayName);
    }

    [TestMethod]
    public async Task ModernElementChangeActionUndoRedoRestoresFlipHorizontally()
    {
        var element = ScadaElement.CreateInputText("input-001", "Input001", 10, 20);
        var updated = element with { Style = element.Style with { FlipHorizontally = true } };
        var scene = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithElement(updated);
        var history = new EditorHistoryService();
        var context = CreateContext(scene, replacement => scene = replacement);

        history.Push(new ModernElementChangedAction(scene.Id, element, updated, "proprietes Element+"));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.IsFalse(scene.FindElementRecursive(element.Id)?.Style.FlipHorizontally);

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.IsTrue(scene.FindElementRecursive(element.Id)?.Style.FlipHorizontally);
    }

    [TestMethod]
    public async Task ConsecutiveModernElementChangesMergeForSingleUndoStep()
    {
        var initial = ScadaElement.CreateText("text-001", "Text001", 10, 20);
        var first = initial with { DisplayName = "A" };
        var second = first with { DisplayName = "AB" };
        var scene = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithElement(second);
        var history = new EditorHistoryService();
        var context = CreateContext(scene, replacement => scene = replacement);

        history.Push(new ModernElementChangedAction(scene.Id, initial, first, "proprietes Element+"));
        history.Push(new ModernElementChangedAction(scene.Id, first, second, "proprietes Element+"));

        Assert.AreEqual(1, history.UndoCount);
        Assert.IsTrue(await history.UndoAsync(context));
        Assert.AreEqual("Text001", scene.FindElementRecursive(initial.Id)?.DisplayName);
    }

    [TestMethod]
    public async Task SceneObjectDeleteActionUndoRedoRestoresTopLevelElement()
    {
        var element = ScadaElement.CreateInputText("input-001", "Input001", 10, 20);
        var scene = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithElement(element)
            .WithoutElementRecursive(element.Id);
        var history = new EditorHistoryService();
        var context = CreateContext(scene, replacement => scene = replacement);

        history.Push(new SceneObjectsDeletedAction(
            scene.Id,
            [new DeletedSceneObjectSnapshot(element, null, 0)]));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.IsNotNull(scene.FindElementRecursive(element.Id));

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.IsNull(scene.FindElementRecursive(element.Id));
    }

    [TestMethod]
    public async Task SceneObjectAddedActionUndoRedoRestoresTopLevelElement()
    {
        var element = ScadaElement.CreateInputText("input-002", "Input002", 30, 40);
        var scene = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithElement(element);
        var history = new EditorHistoryService();
        var context = CreateContext(scene, replacement => scene = replacement);

        history.Push(new SceneObjectsAddedAction(
            scene.Id,
            [new DeletedSceneObjectSnapshot(element, null, 0)]));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.IsNull(scene.FindElementRecursive(element.Id));

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.IsNotNull(scene.FindElementRecursive(element.Id));
    }

    [TestMethod]
    public async Task SceneObjectAddedActionRestoresChildToOriginalParentOnRedo()
    {
        var child = CreateShape("shape-002", 7, 8);
        var group = CreateGroup("group-002", 50, 60, []);
        var scene = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithElement(group);
        var history = new EditorHistoryService();
        var context = CreateContext(scene, replacement => scene = replacement);

        history.Push(new SceneObjectsAddedAction(
            scene.Id,
            [new DeletedSceneObjectSnapshot(child, group.Id, 0)]));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.IsNull(scene.FindElementRecursive(child.Id));

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.AreEqual(group.Id, scene.FindParentOf(child.Id)?.Id);
    }

    [TestMethod]
    public async Task SceneObjectDeleteActionRestoresChildToOriginalParent()
    {
        var child = CreateShape("shape-001", 5, 6);
        var group = CreateGroup("group-001", 100, 200, [child]);
        var scene = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithElement(group)
            .WithoutElementRecursive(child.Id);
        var history = new EditorHistoryService();
        var context = CreateContext(scene, replacement => scene = replacement);

        history.Push(new SceneObjectsDeletedAction(
            scene.Id,
            [new DeletedSceneObjectSnapshot(child, group.Id, 0)]));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.AreEqual(group.Id, scene.FindParentOf(child.Id)?.Id);

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.IsNull(scene.FindElementRecursive(child.Id));
        Assert.IsNotNull(scene.FindElementRecursive(group.Id));
    }

    [TestMethod]
    public async Task SceneObjectDeleteActionUndoRedoRestoresRemovedSourceIds()
    {
        var element = ScadaElement.CreateLegacyStatic(
            "legacy_784",
            "Text22",
            new SceneBounds(80, 57, 45, 24),
            new LegacySourceTrace("Wonderware/ArchestrA", "win00008", "784", "Text22", "win00008.html"),
            new LegacyElementPayload("Text", "###.0", true, "Arial", 16, "#FFFFFF", "Transparent", "<text>###.0</text>", "{}"));
        var scene = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithElement(element)
            .WithLegacyElementsMaterialized()
            .WithoutSceneObjects([element.Id])
            .WithRemovedSourceElementIds(["784"]);
        var history = new EditorHistoryService();
        var context = CreateContext(scene, replacement => scene = replacement);

        history.Push(new SceneObjectsDeletedAction(
            scene.Id,
            [new DeletedSceneObjectSnapshot(element, null, 0)]));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.IsNotNull(scene.FindElementRecursive(element.Id));
        Assert.IsFalse(scene.RemovedSourceIds.Contains("784"));

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.IsNull(scene.FindElementRecursive(element.Id));
        Assert.IsTrue(scene.RemovedSourceIds.Contains("784"));
    }

    [TestMethod]
    public async Task SceneObjectDeleteActionUndoRedoRestoresSourceOnlyRemovedIds()
    {
        var scene = ScadaScene.CreateEmpty("win00002", "win00002", new(1280, 120))
            .WithLegacyElementsMaterialized()
            .WithRemovedSourceElementIds(["3", "4", "13"]);
        var history = new EditorHistoryService();
        var context = CreateContext(scene, replacement => scene = replacement);

        history.Push(new SceneObjectsDeletedAction(
            scene.Id,
            Array.Empty<DeletedSceneObjectSnapshot>(),
            ["3", "4", "13"]));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.IsFalse(scene.RemovedSourceIds.Contains("3"));
        Assert.IsFalse(scene.RemovedSourceIds.Contains("4"));
        Assert.IsFalse(scene.RemovedSourceIds.Contains("13"));

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.IsTrue(scene.RemovedSourceIds.Contains("3"));
        Assert.IsTrue(scene.RemovedSourceIds.Contains("4"));
        Assert.IsTrue(scene.RemovedSourceIds.Contains("13"));
    }

    [TestMethod]
    public async Task SceneObjectDeleteActionUndoRedoRestoresMixedObjectsAndSourceOnlyIds()
    {
        var sourceElement = ScadaElement.CreateLegacyStatic(
            "legacy_784",
            "Text22",
            new SceneBounds(80, 57, 45, 24),
            new LegacySourceTrace("Wonderware/ArchestrA", "win00008", "784", "Text22", "win00008.html"),
            new LegacyElementPayload("Text", "###.0", true, "Arial", 16, "#FFFFFF", "Transparent", "<text>###.0</text>", "{}"));
        var modernElement = ScadaElement.CreateText("text_001", "Element+", 20, 30);
        var scene = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithElement(sourceElement)
            .WithElement(modernElement)
            .WithLegacyElementsMaterialized()
            .WithoutSceneObjects([sourceElement.Id, modernElement.Id])
            .WithRemovedSourceElementIds(["784", "source-only-13"]);
        var history = new EditorHistoryService();
        var context = CreateContext(scene, replacement => scene = replacement);

        history.Push(new SceneObjectsDeletedAction(
            scene.Id,
            [
                new DeletedSceneObjectSnapshot(sourceElement, null, 0),
                new DeletedSceneObjectSnapshot(modernElement, null, 1)
            ],
            ["784", "source-only-13"]));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.IsNotNull(scene.FindElementRecursive(sourceElement.Id));
        Assert.IsNotNull(scene.FindElementRecursive(modernElement.Id));
        Assert.IsFalse(scene.RemovedSourceIds.Contains("784"));
        Assert.IsFalse(scene.RemovedSourceIds.Contains("source-only-13"));

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.IsNull(scene.FindElementRecursive(sourceElement.Id));
        Assert.IsNull(scene.FindElementRecursive(modernElement.Id));
        Assert.IsTrue(scene.RemovedSourceIds.Contains("784"));
        Assert.IsTrue(scene.RemovedSourceIds.Contains("source-only-13"));
    }

    [TestMethod]
    public async Task SceneSnapshotActionUndoRedoReplacesWholeScene()
    {
        var element = ScadaElement.CreateText("text-001", "Text001", 10, 20);
        var before = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873));
        var after = before.WithElement(element);
        var scene = after;
        var history = new EditorHistoryService();
        var context = CreateContext(scene, replacement => scene = replacement);

        history.Push(new SceneSnapshotChangedAction(scene.Id, before, after, "insertion Element+"));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.IsNull(scene.FindElementRecursive(element.Id));

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.IsNotNull(scene.FindElementRecursive(element.Id));
    }

    [TestMethod]
    public async Task SceneSelectionMovedActionUndoRedoRestoresMovedBounds()
    {
        var sourceElement = ScadaElement.CreateLegacyStatic(
            "source_button_8",
            "Button8",
            new SceneBounds(1134, 4, 138, 72),
            new LegacySourceTrace("Wonderware/ArchestrA", "win00003", "8", "Button8", "win00003.html"),
            new LegacyElementPayload("Button", "", true, "Segoe UI", 10, "#fff", "#ccc", null, null));
        var sceneObject = ScadaElement.CreateText("text-001", "Text001", 20, 30);
        var scene = ScadaScene.CreateEmpty("win00003", "win00003", new(1280, 120))
            .WithElement(sourceElement)
            .WithElement(sceneObject);
        scene = scene
            .WithReplacedElementRecursive(sourceElement with { Bounds = new SceneBounds(1144, 14, 138, 72) })
            .WithReplacedElementRecursive(sceneObject with { Bounds = new SceneBounds(30, 40, 180, 28) });
        var history = new EditorHistoryService();
        var context = CreateContext(scene, updated => scene = updated);

        history.Push(new SceneSelectionMovedAction(
            scene.Id,
            [
                new MovedSceneElementBounds(sourceElement.Id, sourceElement.Bounds, new SceneBounds(1144, 14, 138, 72)),
                new MovedSceneElementBounds(sceneObject.Id, sceneObject.Bounds, new SceneBounds(30, 40, 180, 28))
            ]));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.AreEqual(1134, scene.FindElementRecursive(sourceElement.Id)?.Bounds.X);
        Assert.AreEqual(4, scene.FindElementRecursive(sourceElement.Id)?.Bounds.Y);
        Assert.AreEqual(20, scene.FindElementRecursive(sceneObject.Id)?.Bounds.X);
        Assert.AreEqual(30, scene.FindElementRecursive(sceneObject.Id)?.Bounds.Y);

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.AreEqual(1144, scene.FindElementRecursive(sourceElement.Id)?.Bounds.X);
        Assert.AreEqual(14, scene.FindElementRecursive(sourceElement.Id)?.Bounds.Y);
        Assert.AreEqual(30, scene.FindElementRecursive(sceneObject.Id)?.Bounds.X);
        Assert.AreEqual(40, scene.FindElementRecursive(sceneObject.Id)?.Bounds.Y);
    }

    [TestMethod]
    public async Task SceneSelectionMovedActionUndoRedoRestoresGroupAndChildBoundsTogether()
    {
        var child = CreateShape("shape-001", 5, 6);
        var group = CreateGroup("group-001", 100, 200, [child]);
        var scene = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithElement(group);
        var history = new EditorHistoryService();
        var context = CreateContext(scene, updated => scene = updated);

        var beforeGroupBounds = group.Bounds;
        var afterGroupBounds = new SceneBounds(100, 200, 160, 160);
        var beforeChildBounds = child.Bounds;
        var afterChildBounds = new SceneBounds(10, 12, 40, 40);

        scene = scene
            .WithReplacedElementRecursive(group with { Bounds = afterGroupBounds })
            .WithReplacedElementRecursive(child with { Bounds = afterChildBounds });

        history.Push(new SceneSelectionMovedAction(
            scene.Id,
            [
                new MovedSceneElementBounds(group.Id, beforeGroupBounds, afterGroupBounds),
                new MovedSceneElementBounds(child.Id, beforeChildBounds, afterChildBounds)
            ],
            "resize de groupe"));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.AreEqual(beforeGroupBounds.Width, scene.FindElementRecursive(group.Id)?.Bounds.Width);
        Assert.AreEqual(beforeChildBounds.Width, scene.FindElementRecursive(child.Id)?.Bounds.Width);

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.AreEqual(afterGroupBounds.Width, scene.FindElementRecursive(group.Id)?.Bounds.Width);
        Assert.AreEqual(afterChildBounds.Width, scene.FindElementRecursive(child.Id)?.Bounds.Width);
    }

    [TestMethod]
    public async Task HistoriesAreIndependentPerSceneInstance()
    {
        var sceneA = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873));
        var sceneB = ScadaScene.CreateEmpty("win00034", "win00034", new(1280, 873));
        var historyA = new EditorHistoryService();
        var historyB = new EditorHistoryService();

        historyA.Push(new SceneBackgroundChangedAction(sceneA.Id, "#000000", "#111111"));
        historyB.Push(new SceneBackgroundChangedAction(sceneB.Id, "#000000", "#222222"));

        var contextA = CreateContext(sceneA, updated => sceneA = updated);
        var contextB = CreateContext(sceneB, updated => sceneB = updated);

        Assert.IsTrue(await historyB.UndoAsync(contextB));
        Assert.AreEqual("#000000", sceneB.BackgroundColor);
        Assert.AreEqual(1, historyA.UndoCount);
        Assert.AreEqual(0, historyB.UndoCount);

        Assert.IsTrue(await historyA.UndoAsync(contextA));
        Assert.AreEqual("#000000", sceneA.BackgroundColor);
    }

    [TestMethod]
    public async Task NewActionClearsRedoStack()
    {
        var scene = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873));
        var history = new EditorHistoryService();
        var context = CreateContext(scene, updated => scene = updated);

        history.Push(new SceneBackgroundChangedAction(scene.Id, "#000000", "#111111"));
        Assert.IsTrue(await history.UndoAsync(context));
        Assert.IsTrue(history.CanRedo);

        history.Push(new SceneBackgroundChangedAction(scene.Id, "#000000", "#222222"));

        Assert.IsFalse(history.CanRedo);
        Assert.AreEqual(1, history.UndoCount);
    }

    [TestMethod]
    public void ConsecutiveCommittedBackgroundChangesRemainSeparate()
    {
        var history = new EditorHistoryService();

        history.Push(new SceneBackgroundChangedAction("win00008", "#000000", "#111111"));
        history.Push(new SceneBackgroundChangedAction("win00008", "#111111", "#222222"));

        Assert.AreEqual(2, history.UndoCount);
    }

    private static EditorHistoryContext CreateContext(
        ScadaScene scene,
        Action<ScadaScene> replaceScene)
    {
        var currentScene = scene;
        return new EditorHistoryContext
        {
            ActiveSceneId = currentScene.Id,
            GetActiveScene = () => currentScene,
            ReplaceActiveScene = updated =>
            {
                currentScene = updated;
                replaceScene(updated);
            },
            MarkDirty = () => { },
            RefreshPreviewAsync = () => Task.CompletedTask,
            SetStatus = _ => { }
        };
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
