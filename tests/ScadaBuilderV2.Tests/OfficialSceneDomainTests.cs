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
        Assert.AreEqual(1, element.Style?.Opacity);
        Assert.AreEqual(0, element.Style?.Rotation);
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
        var openPopup = ScadaEventRegistry.FindAction(ScadaEventRegistry.OpenPopupFunction);
        var closePopup = ScadaEventRegistry.FindAction(ScadaEventRegistry.ClosePopupFunction);
        var togglePopup = ScadaEventRegistry.FindAction(ScadaEventRegistry.TogglePopupFunction);
        var show = ScadaEventRegistry.FindAction(ScadaEventRegistry.ShowFunction);
        var hide = ScadaEventRegistry.FindAction(ScadaEventRegistry.HideFunction);
        var toggleVisibility = ScadaEventRegistry.FindAction(ScadaEventRegistry.ToggleVisibilityFunction);
        var showBorder = ScadaEventRegistry.FindAction(ScadaEventRegistry.ShowBorderFunction);
        var hideBorder = ScadaEventRegistry.FindAction(ScadaEventRegistry.HideBorderFunction);
        var toggleBorder = ScadaEventRegistry.FindAction(ScadaEventRegistry.ToggleBorderFunction);
        var startBlink = ScadaEventRegistry.FindAction(ScadaEventRegistry.StartBlinkEffectFunction);
        var stopGlow = ScadaEventRegistry.FindAction(ScadaEventRegistry.StopGlowEffectFunction);
        var toggleAlarm = ScadaEventRegistry.FindAction(ScadaEventRegistry.ToggleAlarmEffectFunction);
        var readValue = ScadaEventRegistry.FindAction(ScadaEventRegistry.ReadValueFunction);
        var writeValue = ScadaEventRegistry.FindAction(ScadaEventRegistry.WriteValueFunction);
        var writeTag = ScadaEventRegistry.FindAction(ScadaEventRegistry.WriteTagFunction);

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
        Assert.AreEqual(ScadaActionKind.MountFragment, openPopup?.Kind);
        Assert.IsTrue(openPopup?.Implemented ?? false);
        CollectionAssert.Contains(openPopup?.RequiredArguments.ToArray(), "TargetPageId");
        Assert.AreEqual(ScadaActionKind.ClosePopup, closePopup?.Kind);
        Assert.IsTrue(closePopup?.Implemented ?? false);
        CollectionAssert.Contains(closePopup?.RequiredArguments.ToArray(), "TargetPageId");
        Assert.AreEqual(ScadaActionKind.TogglePopup, togglePopup?.Kind);
        Assert.IsTrue(togglePopup?.Implemented ?? false);
        CollectionAssert.Contains(togglePopup?.RequiredArguments.ToArray(), "TargetPageId");
        Assert.AreEqual(ScadaActionKind.Show, show?.Kind);
        Assert.IsTrue(show?.Implemented ?? false);
        CollectionAssert.Contains(show?.RequiredArguments.ToArray(), "TargetElementId");
        Assert.AreEqual(ScadaActionKind.Hide, hide?.Kind);
        Assert.IsTrue(hide?.Implemented ?? false);
        Assert.AreEqual(ScadaActionKind.ToggleVisibility, toggleVisibility?.Kind);
        Assert.IsTrue(toggleVisibility?.Implemented ?? false);
        Assert.AreEqual(ScadaActionKind.SetClass, showBorder?.Kind);
        Assert.IsTrue(showBorder?.Implemented ?? false);
        CollectionAssert.Contains(showBorder?.RequiredArguments.ToArray(), "TargetElementId");
        Assert.AreEqual(ScadaActionKind.RemoveClass, hideBorder?.Kind);
        Assert.IsTrue(hideBorder?.Implemented ?? false);
        CollectionAssert.Contains(hideBorder?.RequiredArguments.ToArray(), "TargetElementId");
        Assert.AreEqual(ScadaActionKind.ToggleClass, toggleBorder?.Kind);
        Assert.IsTrue(toggleBorder?.Implemented ?? false);
        CollectionAssert.Contains(toggleBorder?.RequiredArguments.ToArray(), "TargetElementId");
        Assert.AreEqual(ScadaActionKind.SetClass, startBlink?.Kind);
        Assert.IsTrue(startBlink?.Implemented ?? false);
        Assert.AreEqual(ScadaActionKind.RemoveClass, stopGlow?.Kind);
        Assert.IsTrue(stopGlow?.Implemented ?? false);
        Assert.AreEqual(ScadaActionKind.ToggleClass, toggleAlarm?.Kind);
        Assert.IsTrue(toggleAlarm?.Implemented ?? false);
        Assert.AreEqual(ScadaActionKind.ReadValue, readValue?.Kind);
        Assert.IsTrue(readValue?.Implemented ?? false);
        CollectionAssert.Contains(readValue?.RequiredArguments.ToArray(), "TagId");
        Assert.AreEqual(ScadaActionKind.WriteValue, writeValue?.Kind);
        Assert.IsTrue(writeValue?.Implemented ?? false);
        CollectionAssert.Contains(writeValue?.RequiredArguments.ToArray(), "TagId");
        Assert.AreEqual(ScadaActionKind.WriteTag, writeTag?.Kind);
        Assert.IsFalse(writeTag?.Implemented ?? true);
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
    public void SceneCanAttachConditionalObjectVisibilityEvent()
    {
        var button = ScadaElement.CreateText("btn_show", "Afficher", 10, 20);
        var target = ScadaElement.CreateText("pump_status", "Pompe", 100, 20);
        var condition = new ScadaActionCondition("tf100.mapping.running", ScadaConditionOperator.True);
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(button)
            .WithElement(target)
            .WithObjectVisibilityEvent("btn_show", ScadaEventRegistry.ClickKey, ScadaActionKind.Show, "pump_status", condition);

        var updated = scene.FindElementRecursive("btn_show");
        var action = scene.ActionDefinitions.Single();

        Assert.IsNotNull(updated);
        Assert.AreEqual(ScadaActionKind.Show, action.Kind);
        Assert.AreEqual("pump_status", action.TargetElementId);
        Assert.AreEqual("tf100.mapping.running", action.Condition?.TagId);
        Assert.AreEqual(ScadaConditionOperator.True, action.Condition?.Operator);
        Assert.AreEqual(action.Id, updated.EventBindings.Single().ActionId);
        Assert.AreEqual(ScadaEventRegistry.ClickRuntimeTrigger, updated.EventBindings.Single().Trigger);
    }

    [TestMethod]
    public void SceneCanAttachCompoundObjectVisibilityConditionGroup()
    {
        var button = ScadaElement.CreateText("btn_show", "Afficher", 10, 20);
        var target = ScadaElement.CreateText("pump_status", "Pompe", 100, 20);
        var group = new ScadaActionConditionGroup(
            [
                new ScadaActionCondition("tf100.mapping.running", ScadaConditionOperator.True),
                new ScadaActionCondition("tf100.mapping.pressure", ScadaConditionOperator.GreaterThan, "12.5")
            ],
            ScadaConditionGroupMode.All,
            ScadaMissingConditionPolicy.AllowAction);
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(button)
            .WithElement(target)
            .WithObjectVisibilityEvent("btn_show", ScadaEventRegistry.ClickKey, ScadaActionKind.Show, "pump_status", conditionGroup: group);

        var action = scene.ActionDefinitions.Single();

        Assert.AreEqual(ScadaConditionGroupMode.All, action.ConditionGroup?.Mode);
        Assert.AreEqual(ScadaMissingConditionPolicy.AllowAction, action.ConditionGroup?.MissingTagPolicy);
        Assert.AreEqual(2, action.ConditionGroup?.Conditions.Count);
        Assert.AreEqual("tf100.mapping.pressure", action.ConditionGroup?.Conditions[1].TagId);
    }

    [TestMethod]
    public void SceneCanAttachObjectBorderEvents()
    {
        var source = ScadaElement.CreateText("btn_hover", "Survol", 10, 20);
        var target = ScadaElement.CreateText("pump_group", "Pompe", 100, 20);
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(source)
            .WithElement(target)
            .WithObjectBorderEvent("btn_hover", ScadaEventRegistry.HoverEnterKey, ScadaActionKind.SetClass, "pump_group")
            .WithObjectBorderEvent("btn_hover", ScadaEventRegistry.HoverExitKey, ScadaActionKind.RemoveClass, "pump_group")
            .WithObjectBorderEvent("btn_hover", ScadaEventRegistry.ClickKey, ScadaActionKind.ToggleClass, "pump_group");

        var updated = scene.FindElementRecursive("btn_hover");

        Assert.IsNotNull(updated);
        Assert.AreEqual(3, updated.EventBindings.Count);
        CollectionAssert.AreEquivalent(
            new[] { ScadaActionKind.SetClass, ScadaActionKind.RemoveClass, ScadaActionKind.ToggleClass },
            scene.ActionDefinitions.Select(action => action.Kind).ToArray());
        Assert.IsTrue(scene.ActionDefinitions.All(action => action.TargetElementId == "pump_group"));
        Assert.IsTrue(scene.ActionDefinitions.All(action => action.ClassName == ScadaEventRegistry.RuntimeBorderHighlightClass));
    }

    [TestMethod]
    public void SceneCanAttachStandardVisualEffectEvents()
    {
        var source = ScadaElement.CreateText("btn_alarm", "Alarme", 10, 20);
        var target = ScadaElement.CreateText("pump_group", "Pompe", 100, 20);
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(source)
            .WithElement(target)
            .WithVisualEffectEvent("btn_alarm", ScadaEventRegistry.ClickKey, ScadaEventRegistry.StartBlinkEffectFunction, "pump_group")
            .WithVisualEffectEvent("btn_alarm", ScadaEventRegistry.ReleaseKey, ScadaEventRegistry.StopBlinkEffectFunction, "pump_group")
            .WithVisualEffectEvent("btn_alarm", ScadaEventRegistry.ClickKey, ScadaEventRegistry.ToggleAlarmEffectFunction, "pump_group");

        CollectionAssert.AreEquivalent(
            new[] { ScadaActionKind.SetClass, ScadaActionKind.RemoveClass, ScadaActionKind.ToggleClass },
            scene.ActionDefinitions.Select(action => action.Kind).ToArray());
        Assert.IsTrue(scene.ActionDefinitions.Any(action => action.ClassName == ScadaEventRegistry.RuntimeBlinkEffectClass));
        Assert.IsTrue(scene.ActionDefinitions.Any(action => action.ClassName == ScadaEventRegistry.RuntimeAlarmEffectClass));
        Assert.IsTrue(scene.ActionDefinitions.All(action => action.TargetElementId == "pump_group"));
    }

    [TestMethod]
    public void SceneCanAttachOpenPopupEvent()
    {
        var button = ScadaElement.CreateText("btn_popup", "Details", 10, 20);
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(button)
            .WithOpenPopupEvent("btn_popup", ScadaEventRegistry.ClickKey, "popup_pump");

        var updated = scene.FindElementRecursive("btn_popup");
        var action = scene.ActionDefinitions.Single();

        Assert.IsNotNull(updated);
        Assert.AreEqual(ScadaActionKind.MountFragment, action.Kind);
        Assert.AreEqual("popup_pump", action.TargetPageId);
        Assert.AreEqual(action.Id, updated.EventBindings.Single().ActionId);
        Assert.AreEqual(ScadaEventRegistry.ClickRuntimeTrigger, updated.EventBindings.Single().Trigger);
    }

    [TestMethod]
    public void SceneCanAttachCloseAndTogglePopupEvents()
    {
        var closeButton = ScadaElement.CreateText("btn_close", "Fermer details", 10, 20);
        var toggleButton = ScadaElement.CreateText("btn_toggle", "Basculer details", 10, 60);
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(closeButton)
            .WithElement(toggleButton)
            .WithClosePopupEvent("btn_close", ScadaEventRegistry.ClickKey, "popup_pump")
            .WithTogglePopupEvent("btn_toggle", ScadaEventRegistry.ClickKey, "popup_pump");

        var closeAction = scene.ActionDefinitions.Single(action => action.Kind == ScadaActionKind.ClosePopup);
        var toggleAction = scene.ActionDefinitions.Single(action => action.Kind == ScadaActionKind.TogglePopup);

        Assert.AreEqual("popup_pump", closeAction.TargetPageId);
        Assert.AreEqual("popup_pump", toggleAction.TargetPageId);
        Assert.AreEqual(closeAction.Id, scene.FindElementRecursive("btn_close")?.EventBindings.Single().ActionId);
        Assert.AreEqual(toggleAction.Id, scene.FindElementRecursive("btn_toggle")?.EventBindings.Single().ActionId);
    }

    [TestMethod]
    public void SceneCanAttachAdvancedPopupOptions()
    {
        var button = ScadaElement.CreateText("btn_popup", "Details", 10, 20);
        var host = ScadaElement.CreateText("host_faceplate", "Host", 100, 20);
        var options = new ScadaPopupOptions(
            ScadaPopupPosition.HostRegion,
            ScadaPopupSizePreset.Medium,
            AllowMultiple: true,
            ResetOnOpen: false,
            HostRegionId: "host_faceplate");
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(button)
            .WithElement(host)
            .WithOpenPopupEvent("btn_popup", ScadaEventRegistry.ClickKey, "popup_pump", options);

        var action = scene.ActionDefinitions.Single();

        Assert.AreEqual(ScadaActionKind.MountFragment, action.Kind);
        Assert.AreEqual("popup_pump", action.TargetPageId);
        Assert.AreEqual(ScadaPopupPosition.HostRegion, action.PopupOptions?.Position);
        Assert.AreEqual(ScadaPopupSizePreset.Medium, action.PopupOptions?.SizePreset);
        Assert.IsTrue(action.PopupOptions?.AllowMultiple ?? false);
        Assert.IsFalse(action.PopupOptions?.ResetOnOpen ?? true);
        Assert.AreEqual("host_faceplate", action.PopupOptions?.HostRegionId);
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
    public void SceneCanAttachReadAndWriteValueBindingsToElement()
    {
        var input = ScadaElement.CreateInputNumeric("input_sp", "Consigne", 10, 20);
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(input)
            .WithValueBinding("input_sp", readTagId: "tf100.mapping.41")
            .WithValueBinding("input_sp", writeTagId: "tf100.mapping.42");

        var updated = scene.FindElementRecursive("input_sp");

        Assert.IsNotNull(updated);
        Assert.AreEqual("tf100.mapping.41", updated.Data?.ReadTagId);
        Assert.AreEqual("tf100.mapping.42", updated.Data?.WriteTagId);
        Assert.AreEqual(0, updated.EventBindings.Count);
        Assert.AreEqual(0, scene.ActionDefinitions.Count);
    }

    [TestMethod]
    public void BuildValidationRejectsInvalidWriteValueBindings()
    {
        var text = ScadaElement.CreateText("txt_value", "Valeur", 10, 20)
            with
            {
                Data = new ScadaElementData(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    false,
                    WriteTagId: "tf100.mapping.41")
            };
        var input = ScadaElement.CreateInputNumeric("input_sp", "Consigne", 10, 60);
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(text)
            .WithElement(input)
            .WithValueBinding("input_sp", writeTagId: "tf100.mapping.41")
            .WithValueBinding("input_sp", readTagId: "tf100.mapping.99");
        var project = ScadaProject.CreateDefault("Validation") with
        {
            Scenes =
            [
                new ScadaSceneReference("win00008", "win00008", "scenes/win00008.scene.json"),
                new ScadaSceneReference("default_target", "Default target", "scenes/default_target.scene.json")
            ],
            TagCatalog = new ScadaTagCatalog(
                "tf100web-scada-tags-v1",
                [
                    new ScadaTagDefinition(
                        "tf100.mapping.41",
                        "Pression",
                        Datatype: "Float",
                        Device: "PLC-1",
                        Writeable: false)
                ])
        };

        var issues = ScadaProjectBuildValidator.Validate(project, [scene]);

        Assert.IsTrue(issues.Any(issue => issue.Code == "tag.write-readonly-element"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "tag.write-readonly-tag"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "tag.read-missing"));
    }

    [TestMethod]
    public void BuildValidationRejectsInvalidActionConditions()
    {
        var button = ScadaElement.CreateText("btn_show", "Afficher", 10, 20);
        var target = ScadaElement.CreateText("pump_status", "Pompe", 100, 20);
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(button)
            .WithElement(target)
            .WithObjectVisibilityEvent(
                "btn_show",
                ScadaEventRegistry.ClickKey,
                ScadaActionKind.Show,
                "missing_target",
                new ScadaActionCondition("tf100.mapping.pressure", ScadaConditionOperator.True));
        var project = ScadaProject.CreateDefault("Validation") with
        {
            Scenes = [new ScadaSceneReference("win00008", "win00008", "scenes/win00008.scene.json")],
            TagCatalog = new ScadaTagCatalog(
                "tf100web-scada-tags-v1",
                [
                    new ScadaTagDefinition(
                        "tf100.mapping.pressure",
                        "Pression",
                        Datatype: "Float",
                        Device: "PLC-1")
                ])
        };

        var issues = ScadaProjectBuildValidator.Validate(project, [scene]);

        Assert.IsTrue(issues.Any(issue => issue.Code == "action.target-missing"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "condition.boolean-operator-nonboolean-tag"));
    }

    [TestMethod]
    public void BuildValidationRejectsInvalidCompoundActionConditions()
    {
        var button = ScadaElement.CreateText("btn_show", "Afficher", 10, 20);
        var target = ScadaElement.CreateText("pump_status", "Pompe", 100, 20);
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(button)
            .WithElement(target)
            .WithObjectVisibilityEvent(
                "btn_show",
                ScadaEventRegistry.ClickKey,
                ScadaActionKind.Show,
                "pump_status",
                conditionGroup: new ScadaActionConditionGroup(
                    [
                        new ScadaActionCondition("tf100.mapping.pressure", ScadaConditionOperator.False),
                        new ScadaActionCondition("tf100.mapping.missing", ScadaConditionOperator.Equals, "1")
                    ],
                    ScadaConditionGroupMode.Any));
        var project = ScadaProject.CreateDefault("Validation") with
        {
            Scenes = [new ScadaSceneReference("win00008", "win00008", "scenes/win00008.scene.json")],
            TagCatalog = new ScadaTagCatalog(
                "tf100web-scada-tags-v1",
                [
                    new ScadaTagDefinition(
                        "tf100.mapping.pressure",
                        "Pression",
                        Datatype: "Float",
                        Device: "PLC-1")
                ])
        };

        var issues = ScadaProjectBuildValidator.Validate(project, [scene]);

        Assert.IsTrue(issues.Any(issue => issue.Code == "condition.boolean-operator-nonboolean-tag"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "condition.tag-missing"));
    }

    [TestMethod]
    public void BuildValidationRejectsInvalidClassActionTarget()
    {
        var button = ScadaElement.CreateText("btn_hover", "Survol", 10, 20);
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(button)
            .WithObjectBorderEvent("btn_hover", ScadaEventRegistry.HoverEnterKey, ScadaActionKind.SetClass, "missing_target");
        var project = ScadaProject.CreateDefault("Validation") with
        {
            Scenes = [new ScadaSceneReference("win00008", "win00008", "scenes/win00008.scene.json")]
        };

        var issues = ScadaProjectBuildValidator.Validate(project, [scene]);

        Assert.IsTrue(issues.Any(issue => issue.Code == "action.target-missing"));
    }

    [TestMethod]
    public void BuildValidationRejectsInvalidPopupFragmentTargets()
    {
        var button = ScadaElement.CreateText("btn_popup", "Details", 10, 20);
        var missingScene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(button)
            .WithOpenPopupEvent("btn_popup", ScadaEventRegistry.ClickKey, "missing_popup");
        var wrongTypeScene = ScadaScene
            .CreateEmpty("win00009", "win00009", new CanvasSize(1280, 873))
            .WithElement(button)
            .WithOpenPopupEvent("btn_popup", ScadaEventRegistry.ClickKey, "default_target");
        var excludedScene = ScadaScene
            .CreateEmpty("win00010", "win00010", new CanvasSize(1280, 873))
            .WithElement(button)
            .WithOpenPopupEvent("btn_popup", ScadaEventRegistry.ClickKey, "excluded_popup");
        var project = ScadaProject.CreateDefault("Validation") with
        {
            Scenes =
            [
                new ScadaSceneReference("win00008", "win00008", "scenes/win00008.scene.json"),
                new ScadaSceneReference("win00009", "win00009", "scenes/win00009.scene.json"),
                new ScadaSceneReference("win00010", "win00010", "scenes/win00010.scene.json"),
                new ScadaSceneReference("default_target", "Default target", "scenes/default_target.scene.json"),
                new ScadaSceneReference("excluded_popup", "Excluded popup", "scenes/excluded_popup.scene.json", ScadaPageType.Fragment, IncludeInBuild: false)
            ]
        };

        var issues = ScadaProjectBuildValidator.Validate(project, [missingScene, wrongTypeScene, excludedScene]);

        Assert.IsTrue(issues.Any(issue => issue.Code == "popup.fragment-missing"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "popup.target-not-fragment"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "popup.fragment-not-compiled"));
    }

    [TestMethod]
    public void BuildValidationRejectsInvalidPopupHostRegion()
    {
        var button = ScadaElement.CreateText("btn_popup", "Details", 10, 20);
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new CanvasSize(1280, 873))
            .WithElement(button)
            .WithOpenPopupEvent(
                "btn_popup",
                ScadaEventRegistry.ClickKey,
                "popup_pump",
                new ScadaPopupOptions(ScadaPopupPosition.HostRegion, HostRegionId: "missing_host"));
        var project = ScadaProject.CreateDefault("Validation") with
        {
            Scenes =
            [
                new ScadaSceneReference("win00008", "win00008", "scenes/win00008.scene.json"),
                new ScadaSceneReference("popup_pump", "Popup", "scenes/popup_pump.scene.json", ScadaPageType.Fragment)
            ]
        };

        var issues = ScadaProjectBuildValidator.Validate(project, [scene]);

        Assert.IsTrue(issues.Any(issue => issue.Code == "popup.host-region-missing"));
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
        Assert.IsTrue(button.EffectiveButtonBehavior.EffectivePressed.Enabled);
        Assert.AreEqual("#0F7280", button.EffectiveButtonBehavior.EffectivePressed.Background);
        Assert.AreEqual(ScadaButtonKind.Command, button.EffectiveButtonKind);

        var disabled = button with
        {
            ButtonBehavior = new ScadaButtonBehavior(true, ScadaButtonHoverStyle.Default with { Enabled = false })
        };

        Assert.IsTrue(disabled.EffectiveButtonBehavior.IsDisabled);
        Assert.IsFalse(disabled.EffectiveButtonBehavior.EffectiveHover.Enabled);
        Assert.AreEqual("#0F7280", disabled.EffectiveButtonBehavior.EffectivePressed.Background);

        var alarmAck = ScadaElement.CreateButton("btn_ack", "Acquitter001", 30, 40, ScadaButtonKind.AlarmAcknowledge);
        Assert.AreEqual(ScadaButtonKind.AlarmAcknowledge, alarmAck.EffectiveButtonKind);
        Assert.AreEqual("Acquitter", alarmAck.Data?.Text);
        Assert.AreEqual("AlarmAcknowledge", alarmAck.Data?.DisplayFormat);
        Assert.AreEqual(new SceneBounds(30, 40, 132, 40), alarmAck.Bounds);

        var emergencyStop = ScadaElement.CreateButton("btn_stop", "ArretUrgence001", 30, 40, ScadaButtonKind.EmergencyStop);
        Assert.AreEqual(ScadaButtonKind.EmergencyStop, emergencyStop.EffectiveButtonKind);
        Assert.AreEqual("STOP", emergencyStop.Data?.Text);
        Assert.AreEqual("#C62828", emergencyStop.Style?.Background);
        Assert.AreEqual(new SceneBounds(30, 40, 96, 96), emergencyStop.Bounds);
    }

    [TestMethod]
    public void ShapeElementDefaultsAndFactoriesPreserveShapeKind()
    {
        var legacyShape = new ScadaElement(
            "shape_legacy",
            "Legacy rectangle",
            ScadaElementKind.Shape,
            new SceneBounds(10, 20, 120, 72),
            null);

        Assert.AreEqual(ScadaShapeKind.Rectangle, legacyShape.EffectiveShapeKind);

        var arrow = ScadaElement.CreateShape("shape_arrow", "Fleche001", ScadaShapeKind.Arrow, 30, 40);

        Assert.AreEqual(ScadaElementKind.Shape, arrow.Kind);
        Assert.AreEqual(ScadaShapeKind.Arrow, arrow.EffectiveShapeKind);
        Assert.AreEqual("Transparent", arrow.Style?.Background);
        Assert.AreEqual(140, arrow.Bounds.Width);

        var lamp = ScadaElement.CreateShape("shape_lamp", "Voyant001", ScadaShapeKind.IndicatorLamp, 10, 20);
        Assert.AreEqual(new SceneBounds(10, 20, 64, 64), lamp.Bounds);

        var bar = ScadaElement.CreateShape("shape_bar", "BarreHorizontale001", ScadaShapeKind.HorizontalBar, 10, 20);
        Assert.AreEqual(65, bar.Data?.Value);
        Assert.AreEqual(0, bar.Data?.Minimum);
        Assert.AreEqual(100, bar.Data?.Maximum);

        var tank = ScadaElement.CreateShape("shape_tank", "Reservoir001", ScadaShapeKind.Tank, 10, 20);
        Assert.AreEqual(new SceneBounds(10, 20, 96, 140), tank.Bounds);
        Assert.AreEqual(65, tank.Data?.Value);
        Assert.AreEqual(0, tank.Data?.Minimum);
        Assert.AreEqual(100, tank.Data?.Maximum);

        var pipeHorizontal = ScadaElement.CreateShape("shape_pipe_h", "TuyauHorizontal001", ScadaShapeKind.PipeHorizontal, 10, 20);
        Assert.AreEqual(new SceneBounds(10, 20, 160, 32), pipeHorizontal.Bounds);

        var pipeVertical = ScadaElement.CreateShape("shape_pipe_v", "TuyauVertical001", ScadaShapeKind.PipeVertical, 10, 20);
        Assert.AreEqual(new SceneBounds(10, 20, 32, 160), pipeVertical.Bounds);

        var valve = ScadaElement.CreateShape("shape_valve", "Vanne001", ScadaShapeKind.Valve, 10, 20);
        Assert.AreEqual(new SceneBounds(10, 20, 96, 64), valve.Bounds);

        var pump = ScadaElement.CreateShape("shape_pump", "Pompe001", ScadaShapeKind.Pump, 10, 20);
        Assert.AreEqual(new SceneBounds(10, 20, 96, 72), pump.Bounds);

        var motor = ScadaElement.CreateShape("shape_motor", "Moteur001", ScadaShapeKind.Motor, 10, 20);
        Assert.AreEqual(new SceneBounds(10, 20, 96, 72), motor.Bounds);

        var fan = ScadaElement.CreateShape("shape_fan", "Ventilateur001", ScadaShapeKind.Fan, 10, 20);
        Assert.AreEqual(new SceneBounds(10, 20, 88, 88), fan.Bounds);

        var conveyor = ScadaElement.CreateShape("shape_conveyor", "Convoyeur001", ScadaShapeKind.Conveyor, 10, 20);
        Assert.AreEqual(new SceneBounds(10, 20, 180, 56), conveyor.Bounds);

        var gauge = ScadaElement.CreateShape("shape_gauge", "Jauge001", ScadaShapeKind.Gauge, 10, 20);
        Assert.AreEqual(new SceneBounds(10, 20, 88, 88), gauge.Bounds);
        Assert.AreEqual(65, gauge.Data?.Value);
        Assert.AreEqual(0, gauge.Data?.Minimum);
        Assert.AreEqual(100, gauge.Data?.Maximum);

        var switchShape = ScadaElement.CreateShape("shape_switch", "Interrupteur001", ScadaShapeKind.Switch, 10, 20);
        Assert.AreEqual(new SceneBounds(10, 20, 96, 56), switchShape.Bounds);

        var breaker = ScadaElement.CreateShape("shape_breaker", "Disjoncteur001", ScadaShapeKind.Breaker, 10, 20);
        Assert.AreEqual(new SceneBounds(10, 20, 96, 72), breaker.Bounds);

        var transformer = ScadaElement.CreateShape("shape_transformer", "Transformateur001", ScadaShapeKind.Transformer, 10, 20);
        Assert.AreEqual(new SceneBounds(10, 20, 112, 80), transformer.Bounds);

        var alarmBeacon = ScadaElement.CreateShape("shape_alarm", "BaliseAlarme001", ScadaShapeKind.AlarmBeacon, 10, 20);
        Assert.AreEqual(new SceneBounds(10, 20, 72, 88), alarmBeacon.Bounds);
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
