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
        StringAssert.Contains(source, "\"Conversion Element+\"");
        StringAssert.Contains(source, "\"Affichage numerique\"");
        StringAssert.Contains(source, "Children:");
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
        StringAssert.Contains(deleteMethod, "MarkActiveSceneDirty();");
        StringAssert.Contains(deleteMethod, "Sauvegarde requise");
        Assert.IsFalse(
            source.Contains("new EditorCommandDescriptor(\"selection.clear\", \"Effacer la selection\", \"selection\")", StringComparison.Ordinal),
            "The right-click menu must delete the selected objects, not only clear the current selection.");
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
        StringAssert.Contains(source, ".scada-modern-group[data-group-context=\"true\"]");
    }

    [TestMethod]
    public void LegacyGroupingDoesNotHideOrRepaintLegacyElements()
    {
        var source = ReadMainWindowSource();
        var groupMethod = ExtractMethod(source, "private async Task GroupSelectedLegacyElementsAsync()");

        StringAssert.Contains(groupMethod, "CreateGroupFrameFromLegacySelection(selectedLegacy)");
        StringAssert.Contains(groupMethod, "_activeScene.WithElement(group)");
        Assert.IsFalse(groupMethod.Contains("WithCommittedElementPlusConversion", StringComparison.Ordinal));
        Assert.IsFalse(groupMethod.Contains("_hiddenLegacyElementIds.Add", StringComparison.Ordinal));
        Assert.IsFalse(groupMethod.Contains("HideLegacyElementsInViewerAsync", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("ResolveVisibleLegacyShapeBackground", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("visibleShapeFill", StringComparison.Ordinal));
        StringAssert.Contains(source, "shape.style.background = cssText(style.Background, 'transparent');");
        StringAssert.Contains(source, ".scada-modern-group {");
        StringAssert.Contains(source, "background: transparent !important;");
    }

    [TestMethod]
    public void ElementGroupUiExposesGroupAndUngroupCommands()
    {
        var source = ReadMainWindowSource();
        var groupMethod = ExtractMethod(source, "private async Task GroupSelectedLegacyElementsAsync()");
        var ungroupMethod = ExtractMethod(source, "private void UngroupSelectedModernElement()");

        StringAssert.Contains(source, "source.group-to-element-plus");
        StringAssert.Contains(source, "object.ungroup");
        StringAssert.Contains(source, "GroupSelectedLegacyElementsAsync");
        StringAssert.Contains(source, "UngroupSelectedModernElement");
        StringAssert.Contains(source, "targetKind: 'object'");
        StringAssert.Contains(groupMethod, "new SceneSnapshotChangedAction(");
        StringAssert.Contains(ungroupMethod, "new SceneSnapshotChangedAction(");
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
        StringAssert.Contains(source, "toggleModernElementInSelection(element.Id);");
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
        StringAssert.Contains(xaml, "Click=\"OnRedoClick\"");
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

        StringAssert.Contains(webViewMethod, "new ModernElementChangedAction(");
        StringAssert.Contains(panelMethod, "new ModernElementChangedAction(");
        StringAssert.Contains(webViewMethod, "Equals(current, updated)");
        StringAssert.Contains(panelMethod, "Equals(current, updated)");
    }

    [TestMethod]
    public void RemainingSceneMutationsUseCommonHistorySnapshots()
    {
        var source = ReadMainWindowSource();
        var insertMethod = ExtractMethod(source, "private void PlaceModernElement(string? kind, double x, double y)");
        var libraryMethod = ExtractMethod(source, "private async Task CreateElementPlusLibraryInstanceAsync(");
        var legacyTextMethod = ExtractMethod(source, "private void EditLegacyText(string? id, string? text)");

        StringAssert.Contains(insertMethod, "new SceneSnapshotChangedAction(");
        StringAssert.Contains(libraryMethod, "new SceneSnapshotChangedAction(");
        StringAssert.Contains(legacyTextMethod, "new SceneSnapshotChangedAction(");
        StringAssert.Contains(insertMethod, "\"insertion Element+\"");
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
    public void SourceElementDragPostsNeutralSelectionMoveAndReappliesBounds()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        StringAssert.Contains(source, "let sourceDrag = null;");
        StringAssert.Contains(source, "function applySourceElementBounds(bounds)");
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
    public void ConversionRemovesLegacyFromRuntimeInventoryImmediately()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "RemoveLegacyElementsFromInventory(convertedLegacyIds);");
        StringAssert.Contains(source, "_hiddenSourceObjectIds.Contains(id)");
        StringAssert.Contains(source, "UndoLegacyConversionAsync");
        StringAssert.Contains(source, "RestoreLegacyElementInInventory(source);");
    }

    [TestMethod]
    public void ModernEditorTabsDoNotTriggerCanvasSelectionClear()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        StringAssert.Contains(source, "if (activeModernEditor && activeModernEditor.contains(event.target))");
        StringAssert.Contains(source, "event.stopPropagation();\n      return;\n    }\n    if (event.target && menu.contains(event.target))");
        Assert.IsTrue(
            source.IndexOf("if (activeModernEditor && activeModernEditor.contains(event.target))", StringComparison.Ordinal) <
            source.IndexOf("hideMenu();\n\n    if (placementKind)", StringComparison.Ordinal),
            "The global pointerdown handler must ignore property editor clicks before it starts drag selection or clears modern selection.");
    }

    [TestMethod]
    public void ModernEditorReadsElementDataBeforeReadOnlyNumericTitle()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        var dataIndex = source.IndexOf("const data = element.Data || {};", StringComparison.Ordinal);
        var readOnlyIndex = source.IndexOf("const isReadOnlyNumeric = isNumeric && data.IsReadOnly === true;", StringComparison.Ordinal);

        Assert.IsTrue(dataIndex >= 0, "Modern editor data initialization was not found.");
        Assert.IsTrue(readOnlyIndex >= 0, "Modern editor read-only numeric flag was not found.");
        Assert.IsTrue(
            dataIndex < readOnlyIndex,
            "The modern editor must initialize data before reading data.IsReadOnly.");
    }

    [TestMethod]
    public void ManualInsertionsUseUniqueElementIds()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "var id = CreateUniqueElementId($\"text_{sequence:000}\");");
        StringAssert.Contains(source, "var id = CreateUniqueElementId($\"input_numeric_{sequence:000}\");");
        StringAssert.Contains(source, "var inputTextId = CreateUniqueElementId($\"input_text_{textSequence:000}\");");
        Assert.IsFalse(
            source.Contains("return ScadaElement.CreateText($\"text_{sequence:000}\"", StringComparison.Ordinal),
            "Manual text insertion must not reuse an existing id when sequences are non-contiguous.");
    }

    [TestMethod]
    public void BeginPlacementClosesModernEditorState()
    {
        var source = NormalizeNewLines(ReadMainWindowSource());

        StringAssert.Contains(source, "if (action === 'beginPlacement') {\n        closeModernEditor();\n        clearModernSelection(false);");
        Assert.IsTrue(
            source.IndexOf("closeModernEditor();\n        clearModernSelection(false);", StringComparison.Ordinal) <
            source.IndexOf("placementKind = command?.Kind || null;", StringComparison.Ordinal),
            "Starting placement must clear the floating box/editor before the next canvas click creates the element.");
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
