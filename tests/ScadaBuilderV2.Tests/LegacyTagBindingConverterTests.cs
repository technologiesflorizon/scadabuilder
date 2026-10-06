using ScadaBuilderV2.Application.Conversion;
using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

/// <summary>
/// Locks the assisted conversion of the legacy <c>Data.TagBinding</c> field to <c>Data.ReadTagId</c>
/// (« Lecture de variable »): resolution, refusal, one undoable transition, residual detection.
/// </summary>
/// <remarks>
/// Decisions: DEC-0053.
/// Contracts: docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md §2 règle 5.
/// </remarks>
[TestClass]
public sealed class LegacyTagBindingConverterTests
{
    [TestMethod]
    public async Task AResolvedTagBindingBecomesTheCanonicalReadTagInOneUndoableTransition()
    {
        var original = Scene(TagBindingText("level", "FAN_03_RUN"));
        var scene = original;
        var history = new EditorHistoryService();

        var result = LegacyTagBindingConverter.ConvertElement(scene, "level", Catalog(), isQuickWindowPage: false);

        Assert.IsTrue(result.Converted, result.Message);
        Assert.AreEqual("tf100.mapping.301", result.ReadTagId);
        var converted = result.Scene.FindElementRecursive("level")!;
        Assert.AreEqual("tf100.mapping.301", converted.Data!.ReadTagId, "the canonical catalog id, not the display name");
        Assert.IsNull(converted.Data.TagBinding, "the legacy binding is cleared by the conversion");
        Assert.IsNotNull(result.HistoryAction);

        scene = result.Scene;
        history.Push(result.HistoryAction!);
        Assert.AreEqual(1, history.UndoCount, "a single history entry");

        Assert.IsTrue(await history.UndoAsync(Context(() => scene, replacement => scene = replacement)));
        Assert.AreSame(original, scene, "undo restores exactly the scene before the conversion");

        Assert.IsTrue(await history.RedoAsync(Context(() => scene, replacement => scene = replacement)));
        Assert.AreEqual("tf100.mapping.301", scene.FindElementRecursive("level")!.Data!.ReadTagId);
    }

    [TestMethod]
    public void TheConversionNeverMergesWithAPrecedingPropertyEditOfTheSameElement()
    {
        var element = TagBindingText("level", "FAN_03_RUN");
        var renamed = element with { DisplayName = "renamed" };
        var scene = Scene(renamed);
        var history = new EditorHistoryService();
        history.Push(new ModernElementChangedAction(scene.Id, element, renamed, "proprietes Element+"));

        var result = LegacyTagBindingConverter.ConvertElement(scene, "level", Catalog(), isQuickWindowPage: false);
        history.Push(result.HistoryAction!);

        Assert.AreEqual(2, history.UndoCount, "the conversion stays its own transition");
    }

    [TestMethod]
    public void AnUnresolvedValueIsRefusedWithAMessageAndNothingChanges()
    {
        var scene = Scene(TagBindingText("level", "UNKNOWN_TAG"));

        var result = LegacyTagBindingConverter.ConvertElement(scene, "level", Catalog(), isQuickWindowPage: false);

        Assert.IsFalse(result.Converted);
        Assert.AreSame(scene, result.Scene);
        Assert.IsNull(result.HistoryAction);
        StringAssert.Contains(result.Message, "Conversion impossible");
        StringAssert.Contains(result.Message, "UNKNOWN_TAG");
        Assert.AreEqual("UNKNOWN_TAG", scene.FindElementRecursive("level")!.Data!.TagBinding);
    }

    [TestMethod]
    public void AnAmbiguousOrDisabledTagIsRefused()
    {
        var catalog = new ScadaTagCatalog("test", [
            new ScadaTagDefinition("tf100.mapping.1", "DUP"),
            new ScadaTagDefinition("tf100.mapping.2", "DUP"),
            new ScadaTagDefinition("tf100.mapping.3", "OFF", Enabled: false)
        ]);

        var ambiguous = LegacyTagBindingConverter.ConvertElement(Scene(TagBindingText("a", "DUP")), "a", catalog, false);
        var disabled = LegacyTagBindingConverter.ConvertElement(Scene(TagBindingText("b", "OFF")), "b", catalog, false);

        Assert.IsFalse(ambiguous.Converted);
        StringAssert.Contains(ambiguous.Message, "plusieurs tags");
        Assert.IsFalse(disabled.Converted);
        StringAssert.Contains(disabled.Message, "désactivé");
    }

    [TestMethod]
    public void APlaceholderOnAQuickWindowPageStaysAsIsAndIsRefusedOnANormalPage()
    {
        var scene = Scene(TagBindingText("level", "#5"));

        var onQuickWindow = LegacyTagBindingConverter.ConvertElement(scene, "level", Catalog(), isQuickWindowPage: true);
        var onPage = LegacyTagBindingConverter.ConvertElement(scene, "level", Catalog(), isQuickWindowPage: false);

        Assert.IsTrue(onQuickWindow.Converted, onQuickWindow.Message);
        Assert.AreEqual("#5", onQuickWindow.Scene.FindElementRecursive("level")!.Data!.ReadTagId);
        Assert.IsNull(onQuickWindow.Scene.FindElementRecursive("level")!.Data!.TagBinding);
        Assert.IsFalse(onPage.Converted);
        StringAssert.Contains(onPage.Message, "Fenêtre rapide");
    }

    [TestMethod]
    public void AnElementThatAlreadyHasAReadTagIdOffersNoConversionAndIsReportedResidual()
    {
        var element = TagBindingText("level", "FAN_03_RUN");
        element = element with { Data = element.Data! with { ReadTagId = "tf100.mapping.302" } };
        var scene = Scene(element);

        Assert.AreEqual(LegacyTagBindingState.Residual, LegacyTagBindingConverter.Inspect(element));
        var result = LegacyTagBindingConverter.ConvertElement(scene, "level", Catalog(), false);

        Assert.IsFalse(result.Converted);
        Assert.AreSame(scene, result.Scene);
        StringAssert.Contains(result.Message, "résiduel");
        Assert.AreEqual("tf100.mapping.302", scene.FindElementRecursive("level")!.Data!.ReadTagId);
    }

    [TestMethod]
    public void InspectOffersTheConversionOnlyForARealLegacyBinding()
    {
        var custom = new ScadaElement(
            "custom",
            "custom",
            ScadaElementKind.Custom,
            new SceneBounds(0, 0, 40, 40),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData("<svg></svg>", null, null, null, null, null, null, null, "pompe.sep", false));

        Assert.AreEqual(LegacyTagBindingState.Convertible, LegacyTagBindingConverter.Inspect(TagBindingText("a", "FAN_03_RUN")));
        Assert.AreEqual(LegacyTagBindingState.None, LegacyTagBindingConverter.Inspect(ScadaElement.CreateText("b", "b", 0, 0)));
        Assert.AreEqual(LegacyTagBindingState.None, LegacyTagBindingConverter.Inspect(custom),
            "a Custom element's TagBinding is its library .sep reference, never a tag");
    }

    [TestMethod]
    public async Task ConvertingAPageConvertsEveryConvertibleBindingAsOneTransitionAndExplainsTheRest()
    {
        var residual = TagBindingText("residual", "FAN_03_SPEED");
        residual = residual with { Data = residual.Data! with { ReadTagId = "tf100.mapping.302" } };
        var group = ScadaElement.CreateText("group", "group", 0, 0) with
        {
            Kind = ScadaElementKind.Group,
            Children = [TagBindingText("nested", "FAN_03_SPEED")]
        };
        var original = Scene(TagBindingText("level", "FAN_03_RUN"), TagBindingText("unknown", "NOPE"), residual, group);
        var scene = original;

        var result = LegacyTagBindingConverter.ConvertScene(scene, Catalog(), isQuickWindowPage: false);

        CollectionAssert.AreEquivalent(new[] { "level", "nested" }, result.Converted.Select(item => item.ElementId).ToArray());
        CollectionAssert.AreEquivalent(new[] { "unknown", "residual" }, result.Skipped.Select(item => item.ElementId).ToArray());
        Assert.AreEqual("tf100.mapping.302", result.Scene.FindElementRecursive("nested")!.Data!.ReadTagId);
        Assert.AreEqual("NOPE", result.Scene.FindElementRecursive("unknown")!.Data!.TagBinding);
        StringAssert.Contains(result.Summary, "2 TagBinding converti(s) en Lecture de variable, 2 ignoré(s).");
        StringAssert.Contains(result.Summary, "NOPE");

        var history = new EditorHistoryService();
        history.Push(result.HistoryAction!);
        scene = result.Scene;
        Assert.IsTrue(await history.UndoAsync(Context(() => scene, replacement => scene = replacement)));
        Assert.AreSame(original, scene, "one undo reverts the whole page conversion");
    }

    [TestMethod]
    public void ConvertingAPageWithNothingConvertibleProducesNoTransition()
    {
        var scene = Scene(TagBindingText("unknown", "NOPE"));

        var result = LegacyTagBindingConverter.ConvertScene(scene, Catalog(), false);

        Assert.AreSame(scene, result.Scene);
        Assert.IsNull(result.HistoryAction);
        Assert.AreEqual(1, result.Skipped.Count);
    }

    private static ScadaScene Scene(params ScadaElement[] elements) =>
        ScadaScene.CreateEmpty("win_tb", "win_tb", new CanvasSize(400, 200)) with { Elements = elements };

    private static ScadaElement TagBindingText(string id, string tagBinding)
    {
        var element = ScadaElement.CreateText(id, id, 0, 0);
        return element with { Data = element.Data! with { TagBinding = tagBinding } };
    }

    private static ScadaTagCatalog Catalog() => new(
        "test",
        [
            new ScadaTagDefinition("tf100.mapping.301", "FAN_03_RUN", Datatype: "Boolean"),
            new ScadaTagDefinition("tf100.mapping.302", "FAN_03_SPEED", Datatype: "Decimal")
        ]);

    private static EditorHistoryContext Context(Func<ScadaScene> getScene, Action<ScadaScene> replace) => new()
    {
        ActiveSceneId = "win_tb",
        GetActiveScene = getScene,
        ReplaceActiveScene = replace,
        MarkDirty = () => { },
        RefreshPreviewAsync = () => Task.CompletedTask,
        SetStatus = _ => { }
    };
}
