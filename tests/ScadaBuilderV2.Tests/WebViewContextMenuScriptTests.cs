using System.Text.RegularExpressions;
using ScadaBuilderV2.Application.Commands;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class WebViewContextMenuScriptTests
{
    [TestMethod]
    public void LegacyContextMenuIsNotOpenedFromRightPointerDown()
    {
        var source = ReadMainWindowSource();
        var normalized = NormalizeNewLines(source);

        StringAssert.Contains(normalized, "if (event.button === 2) {\n      return;\n    }");
        Assert.IsFalse(
            normalized.Contains("if (event.button === 2) {\n      openContextMenu(event);\n      return;\n    }", StringComparison.Ordinal),
            "Right pointerdown must not open the context menu because the native contextmenu event handles it.");
    }

    [TestMethod]
    public void LegacyContextMenuHasSingleDocumentContextMenuHandler()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "event.__scadaContextMenuHandled");
        StringAssert.Contains(source, "document.addEventListener('contextmenu', openContextMenu, true);");
        Assert.IsFalse(
            source.Contains("window.addEventListener('contextmenu', openContextMenu, true);", StringComparison.Ordinal),
            "Registering both document and window contextmenu handlers can emit duplicate requests and clear selection.");
    }

    [TestMethod]
    public void ConversionElementPlusOffersExplicitTargets()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "\"source.convert-to-element-plus\"");
        StringAssert.Contains(source, "source.convert-to-element-plus.text");
        StringAssert.Contains(source, "source.convert-to-element-plus.numeric-readonly");
        StringAssert.Contains(source, "source.convert-to-element-plus.input-text");
        StringAssert.Contains(source, "source.convert-to-element-plus.numeric-editable");
        StringAssert.Contains(source, "source.convert-to-element-plus.button");
        StringAssert.Contains(source, "\"Conversion Element+\"");
        StringAssert.Contains(source, "\"Affichage numerique\"");
        StringAssert.Contains(source, "\"Bouton\"");
        StringAssert.Contains(source, "Children:");
    }

    [TestMethod]
    public void ContextMenuShowsDisabledPropertiesForLegacySources()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "new EditorCommandDescriptor(");
        StringAssert.Contains(source, "\"source.properties\"");
        StringAssert.Contains(source, "\"object.properties\"");
        StringAssert.Contains(source, "Convertir l'element en Element+ avant d'ouvrir ses proprietes.");
        StringAssert.Contains(source, "button.disabled = true;");
        StringAssert.Contains(source, "button.setAttribute('aria-disabled', 'true');");
        StringAssert.Contains(source, "button.title = reason;");
        StringAssert.Contains(source, ".filter(command => command && command.Id)");
        Assert.IsFalse(
            source.Contains(".filter(command => command && command.Id && command.IsEnabled !== false)", StringComparison.Ordinal),
            "Disabled context-menu commands must remain visible so the hover warning can be shown.");
    }

    [TestMethod]
    public void LegacyButtonTextEditingUsesVisibleButtonText()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        StringAssert.Contains(source, "if ((el.tagName || '').toLowerCase() === 'button') return el.textContent || el.value || '';");
        StringAssert.Contains(source, "if ((el.tagName || '').toLowerCase() === 'button') {\n      el.textContent = text;");
    }

    [TestMethod]
    public void ModernButtonRendersTextAndUsesPropertyText()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "element.Kind === 'Button'");
        StringAssert.Contains(source, "button.textContent = data.Text || data.Placeholder || element.DisplayName || 'Bouton';");
        StringAssert.Contains(source, "const buttonKind = String(element.ButtonKind || element.buttonKind || 'Command');");
        StringAssert.Contains(source, "wrapper.dataset.scadaButtonKind = buttonKind;");
        StringAssert.Contains(source, "wrapper.dataset.scadaButtonBehavior = JSON.stringify(buttonBehavior || {});");
        StringAssert.Contains(source, "wrapper.dataset.scadaToggleState = 'off';");
        StringAssert.Contains(source, "wrapper.dataset.scadaDisabled = 'true';");
        StringAssert.Contains(source, "wrapper.setAttribute('aria-disabled', 'true');");
        StringAssert.Contains(source, "button.dataset.scadaButtonKind = buttonKind;");
        StringAssert.Contains(source, "current.Kind is ScadaElementKind.InputText or ScadaElementKind.Text or ScadaElementKind.Button");
    }

    [TestMethod]
    public void ElementPropertiesExposeAdvancedButtonPressedFields()
    {
        var mainXaml = ReadMainWindowFile("MainWindow.xaml");
        var dialogXaml = ReadMainWindowFile("ElementPropertiesDialog.xaml");
        var source = ReadMainWindowSource();
        var dialogCode = ReadMainWindowFile("ElementPropertiesDialog.xaml.cs");

        StringAssert.Contains(mainXaml, "x:Name=\"ElementBackgroundColorPicker\"");
        StringAssert.Contains(mainXaml, "x:Name=\"ElementBorderColorPicker\"");
        StringAssert.Contains(dialogXaml, "x:Name=\"BackgroundColorPicker\"");
        StringAssert.Contains(dialogXaml, "x:Name=\"BorderColorPicker\"");
        StringAssert.Contains(source, "BorderColor = GetColorPickerValue(ElementBorderColorPicker, style.BorderColor)");
        StringAssert.Contains(dialogCode, "BorderColor: GetColorPickerValue(BorderColorPicker, \"#8AA0A6\")");
        StringAssert.Contains(mainXaml, "x:Name=\"ButtonPressedEnabledCheckBox\"");
        StringAssert.Contains(mainXaml, "x:Name=\"ButtonPressedBackgroundColorPicker\"");
        StringAssert.Contains(mainXaml, "x:Name=\"ButtonPressedForegroundColorPicker\"");
        StringAssert.Contains(mainXaml, "x:Name=\"ButtonPressedBorderColorPicker\"");
        StringAssert.Contains(mainXaml, "local:ColorPickerField");
        StringAssert.Contains(dialogXaml, "x:Name=\"ButtonPressedEnabledCheckBox\"");
        StringAssert.Contains(dialogXaml, "x:Name=\"ButtonPressedBackgroundColorPicker\"");
        StringAssert.Contains(dialogXaml, "local:ColorPickerField");
        StringAssert.Contains(dialogCode, "var pressedStyle = buttonBehavior.EffectivePressed;");
        StringAssert.Contains(source, "new ScadaButtonPressedStyle(");
        StringAssert.Contains(source, "ButtonPressedBackgroundColorPicker");
        StringAssert.Contains(source, "ButtonPressedBorderColorPicker");
    }

    [TestMethod]
    public void ColorPickerDialogMatchesCssBackgroundPickerControls()
    {
        var dialogXaml = ReadMainWindowFile("ColorPickerDialog.xaml");
        var dialogCode = ReadMainWindowFile("ColorPickerDialog.xaml.cs");

        StringAssert.Contains(dialogXaml, "x:Name=\"SaturationValuePicker\"");
        StringAssert.Contains(dialogXaml, "x:Name=\"HueColorLayer\"");
        StringAssert.Contains(dialogXaml, "x:Name=\"SaturationValueSelectorTransform\"");
        StringAssert.Contains(dialogXaml, "x:Name=\"HueSlider\"");
        StringAssert.Contains(dialogXaml, "x:Name=\"ColorTextBox\"");
        StringAssert.Contains(dialogXaml, "x:Name=\"RedSlider\"");
        StringAssert.Contains(dialogXaml, "x:Name=\"GreenSlider\"");
        StringAssert.Contains(dialogXaml, "x:Name=\"BlueSlider\"");
        StringAssert.Contains(dialogXaml, "Content=\"Annuler\"");
        StringAssert.Contains(dialogXaml, "Content=\"Enregistrer\"");
        StringAssert.Contains(dialogCode, "UpdateSaturationValueFromPoint");
        StringAssert.Contains(dialogCode, "FromHsv(_hue, _saturation, _value)");
    }

    [TestMethod]
    public void ModernDoubleClickOpensWpfPropertiesDialog()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "window.chrome?.webview?.postMessage({ type: 'openSceneObjectProperties', id: element.Id });");
        StringAssert.Contains(source, "ShowModernElementProperties(message.Id);");
        StringAssert.Contains(source, "var dialog = new ElementPropertiesDialog(current, FormatElementEventsSummary(current))");
        Assert.IsFalse(
            source.Contains("openModernEditor(", StringComparison.Ordinal),
            "Double-click properties must use the WPF ElementPropertiesDialog, not the old WebView floating editor.");
        StringAssert.Contains(source, "button.dataset.scadaButtonBehavior = JSON.stringify(buttonBehavior || {});");
        Assert.IsFalse(
            source.Contains("wrapper.style.background = cssText(buttonHover.Background", StringComparison.Ordinal),
            "Button hover metadata must not be applied inside SCADA Builder V2 preview.");
        Assert.IsFalse(
            source.Contains("button.disabled = buttonBehavior.IsDisabled === true;", StringComparison.Ordinal),
            "Button disabled state is FT100Web metadata in this slice, not an editor-preview runtime behavior.");
    }

    [TestMethod]
    public void ElementPropertiesDialogUsesEventDialogVisualTokens()
    {
        var xaml = ReadMainWindowFile("ElementPropertiesDialog.xaml");

        StringAssert.Contains(xaml, "Title=\"Proprietes Element+\"");
        StringAssert.Contains(xaml, "Width=\"620\"");
        StringAssert.Contains(xaml, "Height=\"680\"");
        StringAssert.Contains(xaml, "<SolidColorBrush x:Key=\"PanelBrush\" Color=\"#F7FBF5\"/>");
        StringAssert.Contains(xaml, "<SolidColorBrush x:Key=\"BorderBrushSoft\" Color=\"#DCE8DD\"/>");
        StringAssert.Contains(xaml, "<Setter Property=\"Background\" Value=\"#2090A0\"/>");
        StringAssert.Contains(xaml, "<Setter Property=\"BorderBrush\" Value=\"#0F7280\"/>");
    }

    [TestMethod]
    public void ElementPropertiesDialogKeepsEventEntryPoint()
    {
        var xaml = ReadMainWindowFile("ElementPropertiesDialog.xaml");
        var dialogCode = ReadMainWindowFile("ElementPropertiesDialog.xaml.cs");
        var source = ReadMainWindowSource();

        StringAssert.Contains(xaml, "<TabItem Header=\"Evenement\">");
        StringAssert.Contains(xaml, "x:Name=\"EventSummaryText\"");
        StringAssert.Contains(xaml, "x:Name=\"OpenEventsButton\"");
        StringAssert.Contains(xaml, "Click=\"OnOpenEventsClick\"");
        StringAssert.Contains(dialogCode, "public Action? OpenEvents { get; set; }");
        StringAssert.Contains(dialogCode, "OpenEvents?.Invoke();");
        StringAssert.Contains(source, "dialog.OpenEvents = () =>");
        StringAssert.Contains(source, "OpenElementEventDialog(current.Id, dialog);");
        StringAssert.Contains(source, "dialog.SetEventSummary(FormatElementEventsSummary(latestWithEvents));");
    }

    [TestMethod]
    public void ElementPropertiesExposeAdvancedShapeStyleFields()
    {
        var mainXaml = ReadMainWindowFile("MainWindow.xaml");
        var dialogXaml = ReadMainWindowFile("ElementPropertiesDialog.xaml");
        var source = ReadMainWindowSource();
        var dialogCode = ReadMainWindowFile("ElementPropertiesDialog.xaml.cs");

        StringAssert.Contains(mainXaml, "x:Name=\"ElementOpacityTextBox\"");
        StringAssert.Contains(mainXaml, "x:Name=\"ElementRotationTextBox\"");
        StringAssert.Contains(dialogXaml, "x:Name=\"OpacityTextBox\"");
        StringAssert.Contains(dialogXaml, "x:Name=\"RotationTextBox\"");
        StringAssert.Contains(source, "Opacity = Math.Clamp(ParseDoubleOrDefault(ElementOpacityTextBox.Text, style.Opacity), 0, 1)");
        StringAssert.Contains(source, "Rotation = ParseDoubleOrDefault(ElementRotationTextBox.Text, style.Rotation)");
        StringAssert.Contains(source, "wrapper.style.opacity = `${Math.max(0, Math.min(1, Number(style.Opacity ?? 1)))}`;");
        StringAssert.Contains(source, "wrapper.style.transform = `rotate(${Number(style.Rotation ?? 0)}deg)`;");
        StringAssert.Contains(dialogCode, "Opacity: Math.Clamp(opacity, 0, 1)");
        StringAssert.Contains(dialogCode, "Rotation: rotation");
    }

    [TestMethod]
    public void WebViewKeyboardShortcutsDoNotDeleteOnBackspaceOrInsideEditors()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        StringAssert.Contains(source, "function isEditableKeyboardTarget(target)");
        StringAssert.Contains(source, "if (activeTextEditor?.editor === target) return true;");
        StringAssert.Contains(source, "if (isEditableKeyboardTarget(event.target)) {\n      return;\n    }");
        StringAssert.Contains(source, "if (event.key === 'Backspace') {\n      event.preventDefault();\n      event.stopPropagation();\n      return;\n    }\n\n    if (event.key === 'Delete') {");
        Assert.IsFalse(
            source.Contains("event.key === 'Delete' || event.key === 'Backspace'", StringComparison.Ordinal),
            "Backspace must not be treated as an Element+ delete shortcut.");
    }

    [TestMethod]
    public void Ft100ExportPrefersReferenceHtmlSourceBeforeRawFallback()
    {
        var source = ReadMainWindowSource();
        var referenceIndex = source.IndexOf("var referenceSource = new LegacyViewerSource", StringComparison.Ordinal);
        var rawIndex = source.IndexOf("FindRawLegacyHtml(_repositoryRoot, page.Id)", StringComparison.Ordinal);

        Assert.IsTrue(referenceIndex >= 0, "Reference page source must be resolved explicitly.");
        Assert.IsTrue(rawIndex >= 0, "Raw legacy fallback must remain available.");
        Assert.IsTrue(
            referenceIndex < rawIndex,
            "FT100 export must prefer the reference page HTML source before falling back to raw 03_web_legacy HTML.");
        StringAssert.Contains(source, "\"reference-html\"");
        StringAssert.Contains(source, "\"reference-html-missing\"");
    }

    [TestMethod]
    public void LegacyContextMenuRendersNestedSubmenus()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "const createCommandNode = command =>");
        StringAssert.Contains(source, "command.Children || command.children || []");
        StringAssert.Contains(source, "wrapper.className = 'submenu'");
        StringAssert.Contains(source, "panel.className = 'submenu-panel'");
    }

    [TestMethod]
    public void LegacyContextSubmenuHasPointerBridge()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "#scada-extract-menu .submenu::after");
        StringAssert.Contains(source, "width: 14px;");
        StringAssert.Contains(source, "height: calc(100% + 16px);");
        StringAssert.Contains(source, "left: calc(100% - 1px);");
        StringAssert.Contains(source, "#scada-extract-menu .submenu[data-submenu-x=\"left\"]::after");
        StringAssert.Contains(source, "right: calc(100% - 1px);");
        Assert.IsFalse(
            source.Contains("left: calc(100% + 5px);", StringComparison.Ordinal),
            "A positive gap between the parent item and submenu makes the submenu collapse while the pointer crosses to it.");
    }

    [TestMethod]
    public void ContextMenuUsesMeasuredSmartPlacementWithinBounds()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "function getContextMenuBounds()");
        StringAssert.Contains(source, "function clampContextCoordinate(value, size, min, max)");
        StringAssert.Contains(source, "menu.style.visibility = 'hidden';");
        StringAssert.Contains(source, "const menuRect = menu.getBoundingClientRect();");
        StringAssert.Contains(source, "const left = clampContextCoordinate(x, menuWidth, bounds.left, bounds.right);");
        StringAssert.Contains(source, "const top = clampContextCoordinate(y, menuHeight, bounds.top, bounds.bottom);");
        StringAssert.Contains(source, "window.innerWidth - margin");
        StringAssert.Contains(source, "window.innerHeight - margin");
        Assert.IsFalse(
            source.Contains("const surface = getPageSurface();\r\n    if (!surface || surface === document.body)", StringComparison.Ordinal),
            "Context menu bounds must use the WebView viewport, not the resized scene surface.");
        Assert.IsFalse(
            source.Contains("const menuHeight = Math.max(42, menu.offsetHeight || commands.length * 34);", StringComparison.Ordinal),
            "The context menu must measure the rendered DOM instead of estimating height while hidden.");
    }

    [TestMethod]
    public void ContextSubmenusCanFlipAndClampInsideBounds()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "wrapper.addEventListener('mouseenter', () => positionSubmenuPanel(wrapper));");
        StringAssert.Contains(source, "wrapper.addEventListener('focusin', () => positionSubmenuPanel(wrapper));");
        StringAssert.Contains(source, "function positionSubmenuPanel(wrapper)");
        StringAssert.Contains(source, "const opensRight = wrapperRect.right + panelRect.width <= bounds.right;");
        StringAssert.Contains(source, "const opensLeft = wrapperRect.left - panelRect.width >= bounds.left;");
        StringAssert.Contains(source, "wrapper.dataset.submenuX = 'left';");
        StringAssert.Contains(source, "const adjustedViewportTop = clampContextCoordinate(viewportTop, panelHeight, bounds.top, bounds.bottom);");
        StringAssert.Contains(source, "const maxPanelHeight = Math.max(42, window.innerHeight - 16);");
    }

    [TestMethod]
    public void ContextMenuPointerDownDoesNotClearSelectionBeforeCommandClick()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        StringAssert.Contains(source, "if (event.target && menu.contains(event.target))");
        StringAssert.Contains(source, "event.stopPropagation();\n      return;\n    }\n    hideMenu();");
        Assert.IsTrue(
            source.IndexOf("if (event.target && menu.contains(event.target))", StringComparison.Ordinal) <
            source.IndexOf("hideMenu();\n\n    if (placementKind)", StringComparison.Ordinal),
            "The global pointerdown handler must ignore context-menu clicks before it starts drag selection or clears selection.");
    }

    [TestMethod]
    public void ContextMenuCommandClickSendsCurrentSelectionSnapshot()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "event.preventDefault();");
        StringAssert.Contains(source, "event.stopPropagation();");
        StringAssert.Contains(source, "items: getSelectedMessages()");
        StringAssert.Contains(source, "if (message.Items is { Count: > 0 })");
    }

    [TestMethod]
    public void LegacyContextMenuDeletesSelectionInsteadOfClearingIt()
    {
        var source = ReadMainWindowSource();
        var deleteMethod = ExtractMethod(source, "private async Task DeleteSelectedSceneObjectsAsync(string? fallbackElementId = null)");

        StringAssert.Contains(source, "new EditorCommandDescriptor(\"selection.delete\", \"Supprimer la selection\", \"selection\")");
        StringAssert.Contains(source, "case \"selection.delete\":");
        StringAssert.Contains(source, "await DeleteSelectedSceneObjectsAsync();");
        StringAssert.Contains(deleteMethod, "new SceneObjectsDeletedAction(");
        StringAssert.Contains(deleteMethod, ".WithoutSceneObjects(deletedElements.Select(element => element.Id))");
        StringAssert.Contains(deleteMethod, ".WithRemovedSourceElementIds(sourceIds)");
        StringAssert.Contains(deleteMethod, "await RemoveLegacyElementsInViewerAsync(sourceIds);");
        StringAssert.Contains(deleteMethod, "MarkActiveSceneDirty();");
        StringAssert.Contains(deleteMethod, "Sauvegarde requise");
        Assert.IsFalse(
            deleteMethod.Contains("await HideLegacyElementsInViewerAsync(sourceIds);", StringComparison.Ordinal),
            "Durable source deletion must remove the source DOM node as scene-state feedback, not persist deletion as a WebView hide.");
        Assert.IsFalse(
            source.Contains("new EditorCommandDescriptor(\"selection.clear\", \"Effacer la selection\", \"selection\")", StringComparison.Ordinal),
            "The right-click menu must delete the selected objects, not only clear the current selection.");
    }

    [TestMethod]
    public void LegacyInventoryDoesNotAutoHidePresentSourceNodes()
    {
        var source = ReadMainWindowSource();
        var inventoryMethod = ExtractMethod(source, "private void ApplyLegacyInventory(IReadOnlyList<LegacyViewerElementMessage> items)");

        StringAssert.Contains(inventoryMethod, "MaterializeLegacyElementsFromInventory(items);");
        StringAssert.Contains(inventoryMethod, "foreach (var legacyElement in _activeScene.GetLegacyStaticElements())");
        Assert.IsFalse(
            inventoryMethod.Contains("removedLegacyIds", StringComparison.Ordinal),
            "Inventory deltas must not decide that present source DOM nodes should disappear.");
        Assert.IsFalse(
            inventoryMethod.Contains("HideLegacyElementsInViewerAsync(removedLegacyIds", StringComparison.Ordinal),
            "ApplyLegacyInventory must not auto-hide source nodes that are absent from the materialized inventory.");
    }

    [TestMethod]
    public void PersistentSourceDeletionUsesNodeRemovalInsteadOfDisplayMasking()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        StringAssert.Contains(source, "const removedNodes = new Map();");
        StringAssert.Contains(source, "function removeSourceElement(el)");
        StringAssert.Contains(source, "rememberRemovedSourceElement(el, id);");
        StringAssert.Contains(source, "el.remove();");
        StringAssert.Contains(source, "function removeLegacyElements(ids)");
        StringAssert.Contains(source, "removeLegacyElements,");
        StringAssert.Contains(source, "window.scadaSceneEditor && window.scadaSceneEditor.removeLegacyElements");
        Assert.IsFalse(
            source.Contains("el.style.display = 'none';", StringComparison.Ordinal),
            "Source delete/suppress operations must not store persistent deletion as a display mask.");
        Assert.IsFalse(
            source.Contains("el.setAttribute('data-scada-deleted', 'true');", StringComparison.Ordinal),
            "Deleted source state belongs to the scene model, not to a persistent WebView DOM marker.");
    }

    [TestMethod]
    public void LegacyInventoryMaterializesStaticSceneElementsWithoutModernOverlayRendering()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "MaterializeLegacyElementsFromInventory(items);");
        StringAssert.Contains(source, "ScadaElement.CreateLegacyStatic");
        StringAssert.Contains(source, "_activeScene.WithLegacyElementsMaterialized()");
        StringAssert.Contains(source, ".Where(element => !element.IsLegacyStatic)");
    }

    [TestMethod]
    public void ModernContextMenuDeletesTheCurrentSelection()
    {
        var source = ReadMainWindowSource();
        var deleteMethod = ExtractMethod(source, "private async Task DeleteSelectedSceneObjectsAsync(string? fallbackElementId = null)");

        StringAssert.Contains(source, "new(\"object.delete\", \"Supprimer la selection\", \"object\")");
        StringAssert.Contains(source, "await DeleteSelectedSceneObjectsAsync(message.Id);");
        StringAssert.Contains(source, "private async Task DeleteSelectedModernElements(string? fallbackId = null)");
        StringAssert.Contains(source, "_selectedSceneObjectIds.Clear();");
        StringAssert.Contains(deleteMethod, "new DeletedSceneObjectSnapshot(");
        StringAssert.Contains(deleteMethod, "_activeScene.FindParentOf(element.Id)?.Id");
        StringAssert.Contains(deleteMethod, "new SceneObjectsDeletedAction(");
        StringAssert.Contains(deleteMethod, "_activeSceneTab?.History.Push");
    }

    [TestMethod]
    public void ElementGroupUiRendersGroupFrameAndSelectionContext()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "const renderElement = (element, parentWrapper = null) =>");
        StringAssert.Contains(source, "element.Kind === 'Group'");
        StringAssert.Contains(source, "wrapper.dataset.groupContext = element.IsGroupContextSelected ? 'true' : 'false';");
        StringAssert.Contains(source, "wrapper.dataset.parentGroupId = parentWrapper.dataset.id;");
        StringAssert.Contains(source, "wrapper.style.zIndex = `${Number(element.RenderIndex ?? element.renderIndex ?? 0)}`;");
        StringAssert.Contains(source, "border: 1px dashed transparent !important;");
        StringAssert.Contains(source, ".scada-modern-group[data-selected=\"true\"]");
        StringAssert.Contains(source, ".scada-modern-group[data-group-context=\"true\"]");
    }

    [TestMethod]
    public void ElementGroupSelectionShowsOnlyMutualizedGroupAnchors()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, ".scada-modern-element[data-selected=\"true\"] > .scada-modern-handle");
        StringAssert.Contains(source, ".scada-modern-element[data-selected=\"true\"] > .scada-modern-badge");
        Assert.IsFalse(
            source.Contains(".scada-modern-element[data-selected=\"true\"] .scada-modern-handle", StringComparison.Ordinal),
            "Selection handles must not cascade into grouped children.");
        Assert.IsFalse(
            source.Contains(".scada-modern-element[data-selected=\"true\"] .scada-modern-badge", StringComparison.Ordinal),
            "Selection badges must not cascade into grouped children.");
    }

    [TestMethod]
    public void ElementGroupDragPromotesChildWrappersToGroupWrapper()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        StringAssert.Contains(source, "function getSceneMoveWrapper(wrapper)");
        StringAssert.Contains(source, "return wrapper.parentElement?.closest?.('.scada-modern-group') || wrapper;");
        StringAssert.Contains(source, "const sceneMoveWrapper = getSceneMoveWrapper(wrapper);");
        StringAssert.Contains(source, "const sceneMoveId = sceneMoveWrapper?.dataset?.id || element.Id;");
        StringAssert.Contains(source, "toggleModernElementInSelection(sceneMoveId);");
        StringAssert.Contains(source, "selectModernElementInDom(sceneMoveId);");
        StringAssert.Contains(source, "id: sceneMoveId,");
        StringAssert.Contains(source, "wrapper: sceneMoveWrapper,");
    }

    [TestMethod]
    public void ElementGroupContextMenuPromotesChildWrappersToGroupWrapper()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        StringAssert.Contains(source, "const sceneContextWrapper = getSceneMoveWrapper(wrapper);");
        StringAssert.Contains(source, "const sceneContextId = sceneContextWrapper?.dataset?.id || element.Id;");
        StringAssert.Contains(source, "selectedModernIds.has(sceneContextId)");
        StringAssert.Contains(source, "selectModernElementInDom(sceneContextId);");
        StringAssert.Contains(source, "toggleModernElementInSelection(sceneContextId);");
        StringAssert.Contains(source, "id: sceneContextId,");
    }

    [TestMethod]
    public void LegacyGroupingRequiresElementPlusConversion()
    {
        var source = ReadMainWindowSource();
        var groupMethod = ExtractMethod(source, "private Task GroupSelectedLegacyElementsAsync()");

        StringAssert.Contains(groupMethod, "WarnLegacyGroupingRequiresConversion();");
        Assert.IsFalse(groupMethod.Contains("CreateGroupFrameFromLegacySelection", StringComparison.Ordinal));
        Assert.IsFalse(groupMethod.Contains("_activeScene.WithElement(group)", StringComparison.Ordinal));
        StringAssert.Contains(source, "source.group-requires-conversion");
        StringAssert.Contains(source, "Convertir les elements legacy en Element+ avant de les grouper.");
    }

    [TestMethod]
    public void ElementGroupUiExposesGroupAndUngroupCommands()
    {
        var source = ReadMainWindowSource();
        var groupMethod = ExtractMethod(source, "private async Task GroupSelectedModernElementsAsync()");
        var ungroupMethod = ExtractMethod(source, "private void UngroupSelectedModernElement()");

        StringAssert.Contains(source, "object.group");
        StringAssert.Contains(source, "object.ungroup");
        StringAssert.Contains(source, "GroupSelectedModernElementsAsync");
        StringAssert.Contains(source, "UngroupSelectedModernElement");
        StringAssert.Contains(source, "targetKind: 'object'");
        StringAssert.Contains(groupMethod, "_activeScene.WithGroupedElements(groupId, groupName, selectedModernIds)");
        StringAssert.Contains(groupMethod, "new SceneSnapshotChangedAction(");
        StringAssert.Contains(ungroupMethod, "new SceneSnapshotChangedAction(");
    }

    [TestMethod]
    public void TopRibbonDispatchesElementGroupCommands()
    {
        var source = ReadMainWindowSource();
        var dispatchMethod = ExtractMethod(source, "private async void ExecuteRibbonCommand(string commandId)");

        StringAssert.Contains(dispatchMethod, "case \"object.group\":");
        StringAssert.Contains(dispatchMethod, "await GroupSelectedModernElementsAsync();");
        StringAssert.Contains(dispatchMethod, "case \"object.ungroup\":");
        StringAssert.Contains(dispatchMethod, "UngroupSelectedModernElement();");
    }

    [TestMethod]
    public void LegacyContextMenuExposesElementStudioCommand()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "source.open-in-element-studio");
        StringAssert.Contains(source, "Ouvrir dans Studio Element+");
        StringAssert.Contains(source, "OpenSelectedLegacyInElementStudioAsync");
        StringAssert.Contains(source, "CaptureSelectedLegacyElementsForStudioAsync");
        StringAssert.Contains(source, "CreateElementStudioImportPackage(selectedLegacy)");
        StringAssert.Contains(source, "WriteToProjectAsync(package, projectsRoot)");
        StringAssert.Contains(source, "TryLaunchElementStudioAsync(packagePath)");
        StringAssert.Contains(source, "AppendElementStudioLaunchLog(packagePath, launch)");
        StringAssert.Contains(source, "startInfo.ArgumentList.Add(packagePath);");
        StringAssert.Contains(source, "LaunchElementStudioExecutableAsync");
        StringAssert.Contains(source, "WaitForStudioWindowAsync");
        StringAssert.Contains(source, "FindVisibleStudioProcess");
        StringAssert.Contains(source, "BringProcessWindowToFront");
        StringAssert.Contains(source, "BuildElementStudioProjectAsync(studioProjectPath)");
        StringAssert.Contains(source, "dotnetStartInfo.ArgumentList.Add(\"--no-build\");");
        StringAssert.Contains(source, "Studio Element+ via dotnet run reste actif, mais aucune fenetre WPF visible n'a ete detectee.");
        StringAssert.Contains(source, "Studio Element+ a quitte immediatement");
        StringAssert.Contains(source, "ResolveElementStudioProjectPath");
        StringAssert.Contains(source, "dotnetStartInfo.ArgumentList.Add(\"run\");");
    }

    [TestMethod]
    public void ElementStudioPackageCapturesLegacyMarkupForSourceRendering()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "toElementMessage(el, options = {})");
        StringAssert.Contains(source, "includeLegacyMarkup");
        StringAssert.Contains(source, "getSelectedMessagesForStudio");
        StringAssert.Contains(source, "computedStyleText = Array.from(computed)");
        StringAssert.Contains(source, "outline-offset");
        StringAssert.Contains(source, "clone.removeAttribute('class')");
        StringAssert.Contains(source, "clone.setAttribute('style'");
        StringAssert.Contains(source, "legacyMarkup = clone.outerHTML || ''");
        StringAssert.Contains(source, "rawMetadataJson: includeLegacyMarkup ? JSON.stringify(rawMetadata) : ''");
        StringAssert.Contains(source, "function getRenderOrder(el)");
        StringAssert.Contains(source, "renderOrder: getRenderOrder(el)");
        StringAssert.Contains(source, "element.LegacyMarkup");
        StringAssert.Contains(source, "element.RawMetadataJson");
        StringAssert.Contains(source, "element.ZIndex");
        StringAssert.Contains(source, "new ElementStudioLegacyItem(");
    }

    [TestMethod]
    public void LegacyInventoryDoesNotCaptureHeavyMarkupUntilStudioExport()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "postInventory()");
        StringAssert.Contains(source, "getSelectedMessagesForStudio()");
        StringAssert.Contains(source, "CaptureSelectedLegacyElementsForStudioAsync");
        StringAssert.Contains(source, "window.scadaSceneEditor.getSelectedMessagesForStudio");
        Assert.IsFalse(
            source.Contains(".map(el => toElementMessage(el, { includeLegacyMarkup: true }));\r\n    window.chrome?.webview?.postMessage({ type: 'inventory'", StringComparison.Ordinal),
            "Inventory refresh must stay lightweight; full markup capture is reserved for Studio Element+ export.");
    }

    [TestMethod]
    public void ModernElementSelectionSupportsMultiSelect()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "const selectedModernIds = new Set();");
        StringAssert.Contains(source, "toggleModernElementInSelection(sceneMoveId);");
        StringAssert.Contains(source, "toggleModernElementInSelection(sceneContextId);");
        StringAssert.Contains(source, "additive: event.ctrlKey || event.shiftKey");
        StringAssert.Contains(source, "toggle: event.ctrlKey || event.shiftKey");
        StringAssert.Contains(source, "command?.Ids || command?.ids || []");
    }

    [TestMethod]
    public void LegacyContextMenuPreservesExistingMultiSelectionWhenTargetIsAmbiguous()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "} else if (!target && selected.size === 0) {");
        StringAssert.Contains(source, "const hasLegacySelection = target || selected.size > 0;");
        StringAssert.Contains(source, "targetKind: hasLegacySelection ? 'source' : 'background'");
        StringAssert.Contains(source, "items: hasLegacySelection ? getSelectedMessages() : []");
    }

    [TestMethod]
    public void ConversionUndoRestoresLegacyElementsThroughViewerApi()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "restoreLegacyElements");
        StringAssert.Contains(source, "hideLegacyElements");
        StringAssert.Contains(source, "UndoLegacyConversionAsync");
        StringAssert.Contains(source, "RedoLegacyConversionAsync");
        StringAssert.Contains(source, "History.Push(new DelegateEditorHistoryAction");
        Assert.IsFalse(source.Contains("_conversionUndoStack", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("_legacyDeletionUndoStack", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MainEditorUsesSceneScopedCommonHistory()
    {
        var xaml = ReadMainWindowFile("MainWindow.xaml");
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "public EditorHistoryService History { get; } = new();");
        StringAssert.Contains(source, "_activeSceneTab.History.UndoAsync");
        StringAssert.Contains(source, "_activeSceneTab.History.RedoAsync");
        StringAssert.Contains(source, "SceneBackgroundChangedAction");
        StringAssert.Contains(xaml, "UndoSceneCommand");
        StringAssert.Contains(xaml, "RedoSceneCommand");
        StringAssert.Contains(source, "case \"edit.undo\":");
        StringAssert.Contains(source, "case \"edit.redo\":");
        var ribbonCommandIds = RibbonCommandCatalog
            .EnumerateCommands(RibbonCommandCatalog.CreateDefault())
            .Select(command => command.Id)
            .ToHashSet(StringComparer.Ordinal);
        Assert.IsTrue(ribbonCommandIds.Contains("edit.undo"));
        Assert.IsTrue(ribbonCommandIds.Contains("edit.redo"));
        Assert.IsFalse(source.Contains("Stack<ConversionUndoSnapshot>", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Stack<LegacyDeletionUndoSnapshot>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ModernElementGeometryUsesCommonHistoryWithBeforeAfterBounds()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "new ModernElementBoundsChangedAction(");
        StringAssert.Contains(source, "message.BeforeX");
        StringAssert.Contains(source, "message.BeforeY");
        StringAssert.Contains(source, "message.BeforeWidth");
        StringAssert.Contains(source, "message.BeforeHeight");
        StringAssert.Contains(source, "_activeSceneTab?.History.Push(new ModernElementBoundsChangedAction");
    }

    [TestMethod]
    public void ModernElementPropertiesUseCommonHistory()
    {
        var source = ReadMainWindowSource();
        var webViewMethod = ExtractMethod(source, "private void UpdateModernElementProperties(LegacyViewerMessage message)");
        var panelMethod = ExtractMethod(source, "private void OnElementPropertyChanged(object sender, RoutedEventArgs e)");
        var commitMethod = ExtractMethod(source, "private void CommitModernElementProperties(ScadaElement current, ScadaElement updated)");

        StringAssert.Contains(webViewMethod, "CommitModernElementProperties(current, updated);");
        StringAssert.Contains(panelMethod, "CommitModernElementProperties(current, updated);");
        StringAssert.Contains(commitMethod, "new ModernElementChangedAction(");
        StringAssert.Contains(webViewMethod, "Equals(current, updated)");
        StringAssert.Contains(panelMethod, "Equals(current, updated)");
    }

    [TestMethod]
    public void RemainingSceneMutationsUseCommonHistorySnapshots()
    {
        var source = ReadMainWindowSource();
        var insertMethod = ExtractMethod(source, "private void PlaceModernElement(string? kind, string? shapeKindText, double x, double y)");
        var insertCommitMethod = ExtractMethod(source, "private void AddModernElementToScene(ScadaElement element, string historyLabel)");
        var libraryMethod = ExtractMethod(source, "private async Task CreateElementPlusLibraryInstanceAsync(");
        var legacyTextMethod = ExtractMethod(source, "private void EditLegacyText(string? id, string? text)");

        StringAssert.Contains(insertCommitMethod, "new SceneSnapshotChangedAction(");
        StringAssert.Contains(libraryMethod, "new SceneSnapshotChangedAction(");
        StringAssert.Contains(legacyTextMethod, "new SceneSnapshotChangedAction(");
        StringAssert.Contains(insertMethod, "AddModernElementToScene(element, \"insertion Element+\");");
        StringAssert.Contains(insertCommitMethod, "historyLabel");
        StringAssert.Contains(libraryMethod, "\"instanciation librairie Element+\"");
        StringAssert.Contains(legacyTextMethod, "\"edition texte legacy\"");
    }

    [TestMethod]
    public void ModernDragPostsOneCommittedGeometrySnapshotOnPointerUp()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        StringAssert.Contains(source, "function postModernGeometry(id, before, after)");
        StringAssert.Contains(source, "beforeX: Math.max(0, Math.round(before.x))");
        StringAssert.Contains(source, "beforeWidth: Math.max(8, Math.round(before.width))");
        StringAssert.Contains(source, "x: modernDrag.startX");
        StringAssert.Contains(source, "postModernGeometry(\n          modernDrag.id,");
        StringAssert.Contains(source, "const before = { ...geometry };");
        StringAssert.Contains(source, "postModernGeometry(selectedModernId, before, geometry);");
    }

    [TestMethod]
    public void ModernSelectionMoveNormalizesGroupChildrenBeforeMoving()
    {
        var source = ReadMainWindowSource();
        var moveMethod = ExtractMethod(source, "private async Task MoveSceneObjectSelectionByAsync(LegacyViewerMessage message)");

        StringAssert.Contains(moveMethod, "NormalizeSceneObjectIdsForMove(ResolveSceneObjectSelectionIds(message))");
        StringAssert.Contains(source, "private IReadOnlyList<string> NormalizeSceneObjectIdsForMove(IEnumerable<string> ids)");
        StringAssert.Contains(source, "private string NormalizeSceneObjectIdForMove(string id)");
        StringAssert.Contains(source, "return parent?.Kind == ScadaElementKind.Group ? parent.Id : id;");
    }

    [TestMethod]
    public void SourceElementDragPostsNeutralSelectionMoveAndReappliesBounds()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        StringAssert.Contains(source, "let sourceDrag = null;");
        StringAssert.Contains(source, "function applySourceElementBounds(bounds)");
        StringAssert.Contains(source, "function getSelectableElementById(id)");
        StringAssert.Contains(source, "setSvgSourceElementGeometry(el, geometry);");
        StringAssert.Contains(source, "el.setAttribute('x', `${x}`);");
        StringAssert.Contains(source, "applySourceElementBounds,\n    renderModernElements");
        StringAssert.Contains(source, "type: 'moveSelectionBy'");
        StringAssert.Contains(source, "targetKind,");
        StringAssert.Contains(source, "postSelectionMove(\n          'source',");
        StringAssert.Contains(source, "case \"moveSelectionBy\":");
        StringAssert.Contains(source, "await ApplySourceElementBoundsInViewerAsync(updatedElements);");
        StringAssert.Contains(source, "new SceneSelectionMovedAction(\n            updatedScene.Id,\n            movedBounds,\n            \"deplacement selection\")");
    }

    [TestMethod]
    public void SceneBackgroundIsPreparedBeforeWebViewNavigation()
    {
        var source = ReadMainWindowSource();
        var normalized = NormalizeNewLines(source);
        var loadMethod = ExtractMethod(source, "private async Task LoadActiveTabPreviewAsync()");
        var normalizedLoadMethod = NormalizeNewLines(loadMethod);

        StringAssert.Contains(loadMethod, "var backgroundColor = _activeScene?.BackgroundColor ?? \"#000000\";");
        StringAssert.Contains(loadMethod, "await PrepareInitialSceneBackgroundScriptAsync(backgroundColor);");
        Assert.IsTrue(
            loadMethod.IndexOf("PrepareInitialSceneBackgroundScriptAsync", StringComparison.Ordinal) <
            loadMethod.IndexOf("PreviewWebView.Source = sourceUri", StringComparison.Ordinal),
            "The scene background script must be registered before WebView navigation to avoid the fallback paint.");
        Assert.IsTrue(
            loadMethod.IndexOf("UpdatePreviewSurfaceBackground(backgroundColor)", StringComparison.Ordinal) <
            loadMethod.IndexOf("PreviewWebView.Source = sourceUri", StringComparison.Ordinal),
            "The WPF preview surface must be painted with the scene background before WebView navigation starts.");
        StringAssert.Contains(normalizedLoadMethod, "PreviewWebView.Visibility = Visibility.Collapsed;\n            await RefreshActiveSceneInViewerAsync();\n            PreviewWebView.Visibility = Visibility.Visible;");
        StringAssert.Contains(normalizedLoadMethod, "PreviewWebView.Visibility = Visibility.Collapsed;\n            PreviewWebView.Source = sourceUri;");
        StringAssert.Contains(source, "AddScriptToExecuteOnDocumentCreatedAsync(script)");
        StringAssert.Contains(source, "const styleId = 'scada-initial-scene-background';");
        StringAssert.Contains(source, "document.addEventListener('readystatechange', apply);");
        StringAssert.Contains(normalized, "PreviewWebView.Visibility = Visibility.Visible;\n            SetStatus(\"Legacy Extraction actif");
        StringAssert.Contains(source, "PreviewSurfaceBorder.Background = new SolidColorBrush(color);");
        StringAssert.Contains(source, "PreviewWebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);");
        StringAssert.Contains(source, "UpdatePreviewNativeBackground(color);");
    }

    [TestMethod]
    public void SceneCanvasSizeIsReappliedDuringPreviewRefresh()
    {
        var source = ReadMainWindowSource();
        var refreshMethod = ExtractMethod(source, "private async Task RefreshActiveSceneInViewerAsync()");

        StringAssert.Contains(refreshMethod, "await ApplySceneBackgroundColorAsync(_activeScene.BackgroundColor, updateStatus: false);");
        StringAssert.Contains(refreshMethod, "await ApplySceneCanvasSizeAsync(_activeScene.CanvasSize);");
        Assert.IsTrue(
            refreshMethod.IndexOf("ApplySceneBackgroundColorAsync", StringComparison.Ordinal) <
            refreshMethod.IndexOf("ApplySceneCanvasSizeAsync", StringComparison.Ordinal),
            "Preview refresh must restore scene color before applying scene dimensions.");
        Assert.IsTrue(
            refreshMethod.IndexOf("ApplySceneCanvasSizeAsync", StringComparison.Ordinal) <
            refreshMethod.IndexOf("RenderModernSceneAsync", StringComparison.Ordinal),
            "The scene surface must be sized before modern overlay elements are rendered.");
    }

    [TestMethod]
    public void SceneCanvasSizeScriptUpdatesLegacyVariablesAndModernLayer()
    {
        var source = ReadMainWindowSource();
        var canvasSizeMethod = ExtractMethod(source, "private async Task ApplySceneCanvasSizeAsync(CanvasSize canvasSize)");

        StringAssert.Contains(canvasSizeMethod, "window.scadaSceneEditor.setCanvasSize(size);");
        StringAssert.Contains(canvasSizeMethod, "document.documentElement.style.setProperty('--page-w', size.width + 'px');");
        StringAssert.Contains(canvasSizeMethod, "document.documentElement.style.setProperty('--page-h', size.height + 'px');");
        StringAssert.Contains(canvasSizeMethod, "surface.style.setProperty('--page-w', size.width + 'px');");
        StringAssert.Contains(canvasSizeMethod, "surface.style.setProperty('--page-h', size.height + 'px');");
        StringAssert.Contains(canvasSizeMethod, "surface.style.overflow = 'hidden';");
        StringAssert.Contains(canvasSizeMethod, "const modernLayer = document.getElementById('scada-modern-layer');");
        StringAssert.Contains(canvasSizeMethod, "modernLayer.style.width = size.width + 'px';");
        StringAssert.Contains(canvasSizeMethod, "modernLayer.style.height = size.height + 'px';");
    }

    [TestMethod]
    public void SceneCanvasResizeHandleCommitsCanvasSizeThroughWebView()
    {
        var source = ReadMainWindowSource();
        var messageHandler = ExtractMethod(source, "private void OnLegacyViewerMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)");
        var resizeMethod = ExtractMethod(source, "private async Task ResizeActiveSceneCanvasFromPreviewAsync(LegacyViewerMessage message)");
        var applyCanvasMethod = ExtractMethod(source, "private async Task ApplyActiveSceneCanvasSizeAsync(");

        StringAssert.Contains(source, "#scada-scene-resize-handle");
        StringAssert.Contains(source, "function ensureSceneResizeHandle()");
        StringAssert.Contains(source, "function setSceneSurfaceSize(width, height)");
        StringAssert.Contains(source, "event.target?.closest?.('#scada-scene-resize-handle')");
        StringAssert.Contains(source, "type: 'previewSceneCanvasResize'");
        StringAssert.Contains(source, "type: 'resizeSceneCanvas'");
        StringAssert.Contains(source, "beforeWidth: sceneCanvasResize.startWidth");
        StringAssert.Contains(source, "width: finalSize.width");
        StringAssert.Contains(messageHandler, "case \"previewSceneCanvasResize\":");
        StringAssert.Contains(messageHandler, "case \"resizeSceneCanvas\":");
        StringAssert.Contains(messageHandler, "PreviewActiveSceneCanvasResize(message);");
        StringAssert.Contains(messageHandler, "_ = ResizeActiveSceneCanvasFromPreviewAsync(message);");
        StringAssert.Contains(resizeMethod, "await ApplyActiveSceneCanvasSizeAsync(");
        StringAssert.Contains(applyCanvasMethod, ".WithCanvasSize(new CanvasSize(width, height))");
        StringAssert.Contains(applyCanvasMethod, "new SceneSnapshotChangedAction(");
        StringAssert.Contains(applyCanvasMethod, "MarkActiveSceneDirty();");
        StringAssert.Contains(applyCanvasMethod, "await ApplySceneCanvasSizeAsync(_activeScene.CanvasSize);");
    }

    [TestMethod]
    public void PageDimensionFieldsApplyCanvasSizeOnChange()
    {
        var xaml = ReadMainWindowFile("MainWindow.xaml");
        var source = ReadMainWindowSource();

        StringAssert.Contains(xaml, "VerticalScrollBarVisibility=\"Auto\"");
        StringAssert.Contains(xaml, "x:Name=\"PageWidthTextBox\"");
        StringAssert.Contains(xaml, "TextChanged=\"OnPageDimensionTextChanged\"");
        StringAssert.Contains(xaml, "x:Name=\"PageHeightTextBox\"");
        StringAssert.Contains(source, "private readonly DispatcherTimer _pageDimensionApplyTimer");
        StringAssert.Contains(source, "_pageDimensionApplyTimer.Tick += OnPageDimensionApplyTimerTick;");
        StringAssert.Contains(source, "private void OnPageDimensionTextChanged(object sender, TextChangedEventArgs e)");
        StringAssert.Contains(source, "private async void OnPageDimensionApplyTimerTick(object? sender, EventArgs e)");
        StringAssert.Contains(source, "await ApplyActiveSceneCanvasSizeAsync(width, height, \"dimensions page\"");
        StringAssert.Contains(source, "SetPageDimensionFields(width, height);");
    }

    [TestMethod]
    public void PageTypeSelectionAppliesSceneTypeAndMarksDirty()
    {
        var xaml = ReadMainWindowFile("MainWindow.xaml");
        var source = ReadMainWindowSource();
        var handler = ExtractMethod(source, "private void OnPageTypeSelectionChanged(object sender, SelectionChangedEventArgs e)");
        var applyMethod = ExtractMethod(source, "private void ApplyActiveScenePageType(ScadaPageType pageType)");

        StringAssert.Contains(xaml, "x:Name=\"PageTypeComboBox\"");
        StringAssert.Contains(xaml, "SelectionChanged=\"OnPageTypeSelectionChanged\"");
        StringAssert.Contains(handler, "ApplyActiveScenePageType(GetSelectedPageType());");
        StringAssert.Contains(applyMethod, "_activeScene.WithPageType(pageType)");
        StringAssert.Contains(applyMethod, "new SceneSnapshotChangedAction(");
        StringAssert.Contains(applyMethod, "MarkActiveSceneDirty();");
        StringAssert.Contains(applyMethod, "LoadPageProperties(_activeScene);");
    }

    [TestMethod]
    public void ReadOnlyNumericElementsRenderDisplayFormatWhenValueIsMissing()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "element.Kind === 'InputNumeric' && data.IsReadOnly === true");
        StringAssert.Contains(source, "data.Value ?? data.DisplayFormat ?? data.Placeholder ?? ''");
        Assert.IsFalse(
            source.Contains("input.type = element.Kind === 'InputNumeric' ? 'number' : 'text';\r\n        input.readOnly = data.IsReadOnly === true;\r\n        input.placeholder = data.Placeholder || '';\r\n        input.value = element.Kind === 'InputNumeric'\r\n          ? (data.Value ?? '')", StringComparison.Ordinal),
            "Read-only numeric display must not render as an empty number input when legacy text is a numeric placeholder such as ####.");
    }

    [TestMethod]
    public void ElementDataTabDeprecatesLegacyTagDecimalsAndUnitFields()
    {
        var mainXaml = ReadMainWindowFile("MainWindow.xaml");
        var dialogXaml = ReadMainWindowFile("ElementPropertiesDialog.xaml");
        var source = ReadMainWindowSource();
        var dialogCode = ReadMainWindowFile("ElementPropertiesDialog.xaml.cs");

        StringAssert.Contains(mainXaml, "Text=\"Contraintes de saisie\"");
        StringAssert.Contains(mainXaml, "Text=\"Format affichage\"");
        StringAssert.Contains(mainXaml, "<StackPanel Margin=\"0,8,4,0\" Visibility=\"Collapsed\">");
        StringAssert.Contains(mainXaml, "x:Name=\"ElementDecimalsTextBox\" TextChanged=\"OnElementPropertyChanged\"");
        StringAssert.Contains(mainXaml, "x:Name=\"ElementUnitTextBox\" TextChanged=\"OnElementPropertyChanged\"");
        StringAssert.Contains(mainXaml, "x:Name=\"ElementTagBindingTextBox\"");
        StringAssert.Contains(mainXaml, "Visibility=\"Collapsed\"");
        StringAssert.Contains(dialogXaml, "Text=\"Contraintes de saisie\"");
        StringAssert.Contains(dialogXaml, "x:Name=\"TagBindingTextBox\" Visibility=\"Collapsed\"");
        StringAssert.Contains(source, "canEditNumericInputConstraints");
        StringAssert.Contains(source, "element.Data?.IsReadOnly != true");
        StringAssert.Contains(dialogCode, "UpdateDataConstraintState();");
        StringAssert.Contains(dialogCode, "ReadOnlyCheckBox.IsChecked != true");
    }

    [TestMethod]
    public void ConversionRemovesLegacyFromRuntimeInventoryImmediately()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "RemoveLegacyElementsFromInventory(convertedLegacyIds);");
        StringAssert.Contains(source, "await RemoveLegacyElementsInViewerAsync(convertedLegacyIds);");
        StringAssert.Contains(source, "_hiddenSourceObjectIds.Contains(id)");
        StringAssert.Contains(source, "UndoLegacyConversionAsync");
        StringAssert.Contains(source, "RestoreLegacyElementInInventory(source);");
    }

    [TestMethod]
    public void ModernPropertiesDialogCommitsThroughSceneHistory()
    {
        var source = ReadMainWindowSource();
        var dialogCode = ReadMainWindowFile("ElementPropertiesDialog.xaml.cs");

        StringAssert.Contains(source, "BuildUpdatedElementFromDialog(latest, dialog.Result)");
        StringAssert.Contains(source, "CommitModernElementProperties(latest, updated);");
        StringAssert.Contains(source, "new ModernElementChangedAction(");
        StringAssert.Contains(dialogCode, "public ElementPropertiesDialogResult? Result { get; private set; }");
        StringAssert.Contains(dialogCode, "DialogResult = true;");
    }

    [TestMethod]
    public void ElementPropertiesDialogReadsElementDataBeforeRuntimeFlags()
    {
        var source = NormalizeNewLines(ReadMainWindowFile("ElementPropertiesDialog.xaml.cs"));

        var dataIndex = source.IndexOf("var data = current.Data ?? new ScadaElementData", StringComparison.Ordinal);
        var buttonIndex = source.IndexOf("var buttonBehavior = current.EffectiveButtonBehavior;", StringComparison.Ordinal);

        Assert.IsTrue(dataIndex >= 0, "ElementPropertiesDialog data initialization was not found.");
        Assert.IsTrue(buttonIndex >= 0, "ElementPropertiesDialog button behavior initialization was not found.");
        Assert.IsTrue(
            dataIndex < buttonIndex,
            "The properties dialog must initialize Element+ data before applying runtime-specific fields.");
    }

    [TestMethod]
    public void ManualInsertionsUseUniqueElementIds()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "var id = CreateUniqueElementId($\"text_{sequence:000}\");");
        StringAssert.Contains(source, "var id = CreateUniqueElementId($\"input_numeric_{sequence:000}\");");
        StringAssert.Contains(source, "var inputTextId = CreateUniqueElementId($\"input_text_{textSequence:000}\");");
        StringAssert.Contains(source, "var id = CreateUniqueElementId($\"shape_{sequence:000}\");");
        StringAssert.Contains(source, "var id = CreateUniqueElementId($\"button_{sequence:000}\");");
        Assert.IsFalse(
            source.Contains("return ScadaElement.CreateText($\"text_{sequence:000}\"", StringComparison.Ordinal),
            "Manual text insertion must not reuse an existing id when sequences are non-contiguous.");
    }

    [TestMethod]
    public void InsertRibbonExposesStandardShapesAndButtons()
    {
        var source = ReadMainWindowSource();
        var commandIds = RibbonCommandCatalog
            .EnumerateCommands(RibbonCommandCatalog.CreateDefault())
            .Select(command => command.Id)
            .ToArray();

        CollectionAssert.Contains(commandIds, "insert.shape.rectangle");
        CollectionAssert.Contains(commandIds, "insert.shape.ellipse");
        CollectionAssert.Contains(commandIds, "insert.shape.circle");
        CollectionAssert.Contains(commandIds, "insert.shape.triangle");
        CollectionAssert.Contains(commandIds, "insert.shape.star");
        CollectionAssert.Contains(commandIds, "insert.shape.line");
        CollectionAssert.Contains(commandIds, "insert.shape.arrow");
        CollectionAssert.Contains(commandIds, "insert.hmi.indicator-lamp");
        CollectionAssert.Contains(commandIds, "insert.hmi.bar-horizontal");
        CollectionAssert.Contains(commandIds, "insert.hmi.bar-vertical");
        CollectionAssert.Contains(commandIds, "insert.hmi.tank");
        CollectionAssert.Contains(commandIds, "insert.hmi.pipe-horizontal");
        CollectionAssert.Contains(commandIds, "insert.hmi.pipe-vertical");
        CollectionAssert.Contains(commandIds, "insert.hmi.valve");
        CollectionAssert.Contains(commandIds, "insert.hmi.pump");
        CollectionAssert.Contains(commandIds, "insert.hmi.motor");
        CollectionAssert.Contains(commandIds, "insert.hmi.fan");
        CollectionAssert.Contains(commandIds, "insert.hmi.conveyor");
        CollectionAssert.Contains(commandIds, "insert.hmi.gauge");
        CollectionAssert.Contains(commandIds, "insert.hmi.switch");
        CollectionAssert.Contains(commandIds, "insert.hmi.breaker");
        CollectionAssert.Contains(commandIds, "insert.hmi.transformer");
        CollectionAssert.Contains(commandIds, "insert.hmi.alarm-beacon");
        CollectionAssert.Contains(commandIds, "insert.button.command");
        CollectionAssert.Contains(commandIds, "insert.button.toggle");
        CollectionAssert.Contains(commandIds, "insert.button.navigation");
        CollectionAssert.Contains(commandIds, "insert.button.alarm-ack");
        CollectionAssert.Contains(commandIds, "insert.button.emergency-stop");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Rectangle);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Ellipse);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Circle, commandId);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Triangle, commandId);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Star, commandId);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Line);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Arrow);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.IndicatorLamp);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.HorizontalBar);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.VerticalBar);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Tank);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.PipeHorizontal);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.PipeVertical);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Valve);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Pump);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Motor);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Fan);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Conveyor);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Gauge);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Switch);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Breaker);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.Transformer);");
        StringAssert.Contains(source, "BeginShapePlacement(ScadaShapeKind.AlarmBeacon);");
        StringAssert.Contains(source, "BeginButtonPlacement(ScadaButtonKind.Command);");
        StringAssert.Contains(source, "BeginButtonPlacement(ScadaButtonKind.Toggle);");
        StringAssert.Contains(source, "BeginButtonPlacement(ScadaButtonKind.Navigation);");
        StringAssert.Contains(source, "BeginButtonPlacement(ScadaButtonKind.AlarmAcknowledge);");
        StringAssert.Contains(source, "BeginButtonPlacement(ScadaButtonKind.EmergencyStop);");
    }

    [TestMethod]
    public void ModernShapePreviewUsesSvgShapeKind()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "function renderShapeElement(element, style)");
        StringAssert.Contains(source, "String(element.ShapeKind || element.shapeKind || 'Rectangle').toLowerCase()");
        StringAssert.Contains(source, "document.createElementNS('http://www.w3.org/2000/svg', 'svg')");
        StringAssert.Contains(source, "shapeKind === 'circle'");
        StringAssert.Contains(source, "shapeKind === 'ellipse'");
        StringAssert.Contains(source, "shapeKind === 'triangle'");
        StringAssert.Contains(source, "shapeKind === 'star'");
        StringAssert.Contains(source, "shapeKind === 'line' || shapeKind === 'arrow'");
        StringAssert.Contains(source, "data.ShapeStartX ?? data.shapeStartX");
        StringAssert.Contains(source, "data.ShapeEndX ?? data.shapeEndX");
        StringAssert.Contains(source, "shapeKind === 'indicatorlamp'");
        StringAssert.Contains(source, "shapeKind === 'horizontalbar' || shapeKind === 'verticalbar'");
        StringAssert.Contains(source, "shapeKind === 'tank'");
        StringAssert.Contains(source, "shapeKind === 'pipehorizontal' || shapeKind === 'pipevertical'");
        StringAssert.Contains(source, "shapeKind === 'valve'");
        StringAssert.Contains(source, "shapeKind === 'pump'");
        StringAssert.Contains(source, "shapeKind === 'motor'");
        StringAssert.Contains(source, "shapeKind === 'fan'");
        StringAssert.Contains(source, "shapeKind === 'conveyor'");
        StringAssert.Contains(source, "shapeKind === 'gauge'");
        StringAssert.Contains(source, "shapeKind === 'switch'");
        StringAssert.Contains(source, "shapeKind === 'breaker'");
        StringAssert.Contains(source, "shapeKind === 'transformer'");
        StringAssert.Contains(source, "shapeKind === 'alarmbeacon'");
        StringAssert.Contains(source, "const clampPercent = value =>");
        StringAssert.Contains(source, "wrapper.appendChild(renderShapeElement(element, style));");
        StringAssert.Contains(source, "ShapeKind = element.Kind == ScadaElementKind.Shape ? element.EffectiveShapeKind.ToString() : null");
        StringAssert.Contains(source, "public string? ShapeKind { get; set; }");
    }

    [TestMethod]
    public void ModernPreviewPayloadSerializesVisualDiscriminatorsAsText()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "ShapeKind = element.Kind == ScadaElementKind.Shape ? element.EffectiveShapeKind.ToString() : null");
        StringAssert.Contains(source, "ButtonKind = element.Kind == ScadaElementKind.Button ? element.EffectiveButtonKind.ToString() : null");
        StringAssert.Contains(source, "public string? ShapeKind { get; set; }");
        StringAssert.Contains(source, "public string? ButtonKind { get; set; }");
        StringAssert.DoesNotMatch(source, new Regex(@"public\s+ScadaShapeKind\?\s+ShapeKind\s+\{\s+get;\s+set;\s+\}"));
        StringAssert.DoesNotMatch(source, new Regex(@"public\s+ScadaButtonKind\?\s+ButtonKind\s+\{\s+get;\s+set;\s+\}"));
    }

    [TestMethod]
    public void BeginPlacementClearsModernSelectionState()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        StringAssert.Contains(source, "if (action === 'beginPlacement') {\n        clearModernSelection(false);");
        Assert.IsTrue(
            source.IndexOf("clearModernSelection(false);", StringComparison.Ordinal) <
            source.IndexOf("placementKind = command?.Kind || null;", StringComparison.Ordinal),
            "Starting placement must clear modern selection before the next canvas click creates the element.");
    }

    [TestMethod]
    public void LineAndArrowPlacementUseTwoPointMode()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        StringAssert.Contains(source, "private static bool IsTwoPointShape(ScadaShapeKind shapeKind)");
        StringAssert.Contains(source, "shapeKind is ScadaShapeKind.Line or ScadaShapeKind.Arrow");
        StringAssert.Contains(source, "ShapeKind: kind == ScadaElementKind.Shape ? shapeKind.ToString() : null");
        StringAssert.Contains(source, "IsTwoPoint: isTwoPointShape");
        StringAssert.Contains(source, "let placementIsTwoPoint = false;");
        StringAssert.Contains(source, "type: 'placeTwoPointElement'");
        StringAssert.Contains(source, "ShapeStartX = startX");
        StringAssert.Contains(source, "ShapeEndY = endY");
        StringAssert.Contains(source, "clearPlacementState(true);");
    }

    [TestMethod]
    public void SinglePointShapePlacementPostsAndConsumesShapeKind()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        StringAssert.Contains(source, "PlaceModernElement(message.Kind, message.ShapeKind, message.X, message.Y);");
        StringAssert.Contains(source, "private void PlaceModernElement(string? kind, string? shapeKindText, double x, double y)");
        StringAssert.Contains(source, "ParseShapeKind(shapeKindText) ?? _pendingInsertShapeKind");
        StringAssert.Contains(source, "var element = CreateModernElement(elementKind.Value, x, y, shapeKind);");
        StringAssert.Contains(source, "const shapeKind = placementShapeKind;\n        window.chrome?.webview?.postMessage({ type: 'placeElement', kind, shapeKind, x: point.x, y: point.y });");
    }

    [TestMethod]
    public void PlacementClickTakesPriorityOverExistingModernElementSelection()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        var placementIndex = source.IndexOf("if (placementKind) {\n      const surface = getPageSurface();", StringComparison.Ordinal);
        var modernElementSkipIndex = source.IndexOf("if (event.target?.closest?.('.scada-modern-element')) {\n      return;\n    }\n\n    const target = findSelectable(event.target);", StringComparison.Ordinal);

        Assert.IsTrue(placementIndex >= 0, "The global pointerdown placement branch was not found.");
        Assert.IsTrue(modernElementSkipIndex >= 0, "The modern-element selection skip branch was not found after placement.");
        Assert.IsTrue(
            placementIndex < modernElementSkipIndex,
            "Placement mode must handle the click before existing modern elements can consume it.");
    }

    [TestMethod]
    public void PlacementModeDisablesModernElementPointerInteractions()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        StringAssert.Contains(source, "body.scada-placement-active .scada-modern-element {\n      pointer-events: none;\n    }");
        Assert.IsTrue(
            CountOccurrences(source, "if (placementKind) {\n          return;\n        }\n        if (event.target?.closest?.('.scada-modern-element') !== wrapper)") >= 3,
            "Modern element pointer, double-click, and context-menu handlers must ignore events while placement is active.");
    }

    [TestMethod]
    public void LegacyInventoryTargetsAllSourceDataIdsIncludingSvgSourceShapes()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        StringAssert.Contains(source, "const selectableSelector = '[data-id]:not(.scada-modern-element)';");
        StringAssert.Contains(source, "const inventorySelector = '.layer[data-id]:not(.scada-modern-element), .shape-layer [data-id]';");
        StringAssert.Contains(source, "return `[data-id=\"${escaped}\"]:not(.scada-modern-element)`;");
        StringAssert.Contains(source, "function getInventoryElements()");
        StringAssert.Contains(source, "const items = getInventoryElements().map(toElementMessage);");
        StringAssert.Contains(source, "const el = getSelectableElementById(id);");
        StringAssert.Contains(source, "const target = getSelectableElementById(overrideItem.Id);");
    }

    [TestMethod]
    public void StatusDiagnosticButtonOpensDiagnosticDetails()
    {
        var xaml = ReadMainWindowFile("MainWindow.xaml");
        var source = ReadMainWindowSource();

        StringAssert.Contains(xaml, "Click=\"OnStatusDiagnosticsClick\"");
        StringAssert.Contains(xaml, "ToolTip=\"Afficher les diagnostics\"");
        StringAssert.Contains(source, "private void OnStatusDiagnosticsClick");
        StringAssert.Contains(source, "ResolveElementStudioLaunchLogPath");
        StringAssert.Contains(source, "element-studio-launch.log");
        StringAssert.Contains(source, "MessageBox.Show");
    }

    private static string ReadMainWindowSource()
    {
        return ReadMainWindowFile("MainWindow.xaml.cs");
    }

    private static string ReadMainWindowFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "ScadaBuilderV2.App",
                fileName);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate {fileName} from test output directory.");
        return "";
    }

    private static string NormalizeNewLines(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string ExtractMethod(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"Method signature not found: {signature}");
        var bodyStart = source.IndexOf('{', start);
        Assert.IsTrue(bodyStart >= 0, $"Method body not found: {signature}");

        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[start..(index + 1)];
                }
            }
        }

        Assert.Fail($"Method end not found: {signature}");
        return "";
    }
}
