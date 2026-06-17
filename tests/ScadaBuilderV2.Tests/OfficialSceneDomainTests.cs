using ScadaBuilderV2.Domain.Legacy;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class OfficialSceneDomainTests
{
    [TestMethod]
    public void ScadaSceneStartsAsModernDomainWithoutLegacyDependency()
    {
        var scene = ScadaScene.CreateEmpty("scene-01", "Main overview", new CanvasSize(1280, 720));

        Assert.AreEqual("scene-01", scene.Id);
        Assert.AreEqual("Main overview", scene.Title);
        Assert.AreEqual(1280, scene.CanvasSize.Width);
        Assert.AreEqual(0, scene.Elements.Count);
    }

    [TestMethod]
    public void ImportedElementUsesDisplayNameInsteadOfLegacyIdForUserLabel()
    {
        var trace = new LegacySourceTrace(
            "Wonderware.ArchestrA",
            "win00008",
            "legacy:795",
            "Text29",
            "pages/win00008.json");
        var element = new ScadaElement(
            "element-001",
            "Pump label",
            ScadaElementKind.Text,
            new SceneBounds(12, 24, 160, 32),
            trace);

        Assert.IsTrue(element.IsImportedFromLegacy);
        Assert.AreEqual("Pump label", element.UserLabel);
        Assert.AreEqual("legacy:795", element.LegacySource?.SourceElementId);
    }

    [TestMethod]
    public void LegacyExtractionCandidateIsAReviewItemNotTheFinalElement()
    {
        var document = new LegacySourceDocument(
            "legacy-win00008",
            "win00008 legacy HTML",
            "Wonderware.ArchestrA",
            "references/legacy/win00008.html");
        var candidate = new LegacyExtractionCandidate(
            "candidate-001",
            document,
            "legacy:795",
            "Text29",
            ScadaElementKind.Text,
            new SceneBounds(10, 20, 100, 24),
            LegacyExtractionState.Candidate);

        Assert.AreEqual(LegacyExtractionState.Candidate, candidate.State);
        Assert.AreEqual("Text29", candidate.SuggestedDisplayName);
        Assert.IsTrue(candidate.SourceBounds.HasPositiveSize);
    }

    [TestMethod]
    public void InputTextElementHasEditableStyleAndDataDefaults()
    {
        var element = ScadaElement.CreateInputText("input_text_001", "InputText001", 120, 240);

        Assert.AreEqual(ScadaElementKind.InputText, element.Kind);
        Assert.AreEqual(120, element.Bounds.X);
        Assert.AreEqual(240, element.Bounds.Y);
        Assert.AreEqual(180, element.Bounds.Width);
        Assert.AreEqual(32, element.Bounds.Height);
        Assert.AreEqual(ElementPositionMode.Absolute, element.Layout?.PositionMode);
        Assert.AreEqual("Segoe UI", element.Style?.FontFamily);
        Assert.AreEqual("#FFFFFF", element.Style?.Background);
        Assert.AreEqual("Texte", element.Data?.Placeholder);
        Assert.IsFalse(element.Data?.IsReadOnly ?? true);
    }

    [TestMethod]
    public void TextElementIsDistinctFromTextInput()
    {
        var element = ScadaElement.CreateText("text_001", "Text001", 12, 18);

        Assert.AreEqual(ScadaElementKind.Text, element.Kind);
        Assert.AreEqual("Texte", element.Data?.Text);
        Assert.IsNull(element.Data?.Placeholder);
        Assert.AreEqual("Transparent", element.Style?.Background);
        Assert.AreEqual(0, element.Style?.BorderWidth);
    }

    [TestMethod]
    public void InputNumericElementCarriesNumericProperties()
    {
        var element = ScadaElement.CreateInputNumeric("input_numeric_001", "InputNumeric001", 20, 30);

        Assert.AreEqual(ScadaElementKind.InputNumeric, element.Kind);
        Assert.AreEqual(0, element.Data?.Value);
        Assert.AreEqual(0, element.Data?.Decimals);
        Assert.AreEqual("0", element.Data?.DisplayFormat);
    }

    [TestMethod]
    public void NumericDisplayAndNumericInputShareKindAndDifferByReadOnly()
    {
        var display = ScadaElement.CreateInputNumeric("numeric_display_001", "AffichageNumerique001", 20, 30, isReadOnly: true);
        var input = ScadaElement.CreateInputNumeric("input_numeric_001", "InputNumeric001", 20, 30);

        Assert.AreEqual(input.Kind, display.Kind);
        Assert.AreEqual(ScadaElementKind.InputNumeric, display.Kind);
        Assert.IsTrue(display.Data?.IsReadOnly ?? false);
        Assert.IsFalse(input.Data?.IsReadOnly ?? true);
    }

    [TestMethod]
    public void SceneCanRemoveModernElement()
    {
        var scene = ScadaScene
            .CreateEmpty("scene-01", "Main overview", new CanvasSize(1280, 720))
            .WithElement(ScadaElement.CreateInputText("input_text_001", "InputText001", 10, 20))
            .WithElement(ScadaElement.CreateInputNumeric("input_numeric_001", "InputNumeric001", 30, 40));

        var updated = scene.WithoutElement("input_text_001");

        Assert.AreEqual(1, updated.Elements.Count);
        Assert.AreEqual("input_numeric_001", updated.Elements[0].Id);
    }

    [TestMethod]
    public void LegacyStaticElementIsARealSceneElementWithLegacyIdentity()
    {
        var element = CreateLegacyStatic("784", "Text22");
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(element)
            .WithLegacyElementsMaterialized();

        Assert.AreEqual(ScadaElementKind.LegacyStatic, scene.Elements.Single().Kind);
        Assert.AreEqual("784", scene.FindLegacyStaticBySourceElementId("784")?.LegacySource?.SourceElementId);
        Assert.AreEqual("Text", scene.GetLegacyStaticElements().Single().LegacyPayload?.LegacyType);
        Assert.IsFalse(scene.GetConvertedLegacySourceElementIds().Contains("784"));

        var deleted = scene.WithoutElementRecursive(element.Id);

        Assert.IsNull(deleted.FindLegacyStaticBySourceElementId("784"));
        Assert.AreEqual(0, deleted.Elements.Count);
    }

    [TestMethod]
    public void SceneTracksRemovedSourceIdsAsActiveSceneState()
    {
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithRemovedSourceElementIds(["3", "4"]);

        Assert.IsTrue(scene.RemovedSourceIds.Contains("3"));
        Assert.IsTrue(scene.RemovedSourceIds.Contains("4"));
        Assert.IsTrue(scene.GetSuppressedSourceElementIds().Contains("3"));

        var restored = scene.WithoutRemovedSourceElementIds(["3"]);

        Assert.IsFalse(restored.RemovedSourceIds.Contains("3"));
        Assert.IsTrue(restored.RemovedSourceIds.Contains("4"));
    }

    [TestMethod]
    public void EventRegistryDefinesFrenchTriggerLabelsAndChangePageContract()
    {
        var click = ScadaEventRegistry.FindTrigger(ScadaEventRegistry.ClickKey);
        var release = ScadaEventRegistry.FindTrigger(ScadaEventRegistry.ReleaseKey);
        var hover = ScadaEventRegistry.FindTrigger(ScadaEventRegistry.HoverKey);
        var changePage = ScadaEventRegistry.FindAction(ScadaEventRegistry.ChangePageFunction);

        Assert.IsNotNull(click);
        Assert.AreEqual("Clic", click.FrenchLabel);
        Assert.AreEqual("click", click.RuntimeTrigger);
        Assert.IsTrue(click.AllowsMultiple);
        Assert.IsTrue(click.SupportsConditions);
        Assert.AreEqual("Relachement", release?.FrenchLabel);
        Assert.AreEqual("Survol", hover?.FrenchLabel);
        Assert.AreEqual(ScadaActionKind.Navigate, changePage?.Kind);
        Assert.IsTrue(changePage?.Implemented ?? false);
        CollectionAssert.Contains(changePage?.RequiredArguments.ToArray(), "TargetPageId");
    }

    [TestMethod]
    public void SceneCanAttachMultipleClickChangePageEventsToOneElement()
    {
        var button = ScadaElement.CreateText("btn_next", "Bouton navigation", 10, 20);
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(button)
            .WithChangePageEvent("btn_next", ScadaEventRegistry.ClickKey, "win00009")
            .WithChangePageEvent("btn_next", ScadaEventRegistry.ClickKey, "win00010");

        var updated = scene.FindElementRecursive("btn_next");

        Assert.IsNotNull(updated);
        Assert.AreEqual(2, updated.EventBindings.Count);
        Assert.IsTrue(updated.EventBindings.All(binding => binding.Trigger == ScadaEventRegistry.ClickRuntimeTrigger));
        Assert.AreEqual(2, scene.ActionDefinitions.Count);
        CollectionAssert.AreEquivalent(
            new[] { "win00009", "win00010" },
            scene.ActionDefinitions.Select(action => action.TargetPageId).ToArray());
    }

    [TestMethod]
    public void SceneCanRemoveOneElementEventAndPruneOrphanAction()
    {
        var button = ScadaElement.CreateText("btn_next", "Bouton navigation", 10, 20);
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(button)
            .WithChangePageEvent("btn_next", ScadaEventRegistry.ClickKey, "win00009")
            .WithChangePageEvent("btn_next", ScadaEventRegistry.ClickKey, "win00010");

        var updated = scene.WithoutObjectEventAt("btn_next", 0);
        var element = updated.FindElementRecursive("btn_next");

        Assert.IsNotNull(element);
        Assert.AreEqual(1, element.EventBindings.Count);
        Assert.AreEqual(1, updated.ActionDefinitions.Count);
        Assert.AreEqual("win00010", updated.ActionDefinitions.Single().TargetPageId);
        Assert.AreEqual(updated.ActionDefinitions.Single().Id, element.EventBindings.Single().ActionId);
    }

    [TestMethod]
    public void ButtonElementHasDefaultHoverUnlessExplicitlyDisabled()
    {
        var button = new ScadaElement(
            "btn_ack",
            "Acquitter",
            ScadaElementKind.Button,
            new SceneBounds(10, 20, 120, 32),
            null);

        Assert.IsFalse(button.EffectiveButtonBehavior.IsDisabled);
        Assert.IsTrue(button.EffectiveButtonBehavior.EffectiveHover.Enabled);
        Assert.AreEqual("#EAF5F7", button.EffectiveButtonBehavior.EffectiveHover.Background);

        var disabled = button with
        {
            ButtonBehavior = new ScadaButtonBehavior(true, ScadaButtonHoverStyle.Default with { Enabled = false })
        };

        Assert.IsTrue(disabled.EffectiveButtonBehavior.IsDisabled);
        Assert.IsFalse(disabled.EffectiveButtonBehavior.EffectiveHover.Enabled);
    }

    private static ScadaElement CreateLegacyStatic(string sourceId, string name)
    {
        return ScadaElement.CreateLegacyStatic(
            $"legacy_{sourceId}",
            name,
            new SceneBounds(80, 57, 45, 24),
            new LegacySourceTrace("Wonderware/ArchestrA", "win00008", sourceId, name, "win00008.html"),
            new LegacyElementPayload("Text", "###.0", true, "Arial", 16, "#FFFFFF", "Transparent", "<text>###.0</text>", "{}"));
    }
}
