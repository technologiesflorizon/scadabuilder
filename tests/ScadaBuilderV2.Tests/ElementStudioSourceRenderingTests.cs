namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ElementStudioSourceRenderingTests
{
    [TestMethod]
    public void StudioUsesWebView2ForLegacySourceLayer()
    {
        var xaml = ReadStudioFile("MainWindow.xaml");
        var code = ReadStudioFile("MainWindow.xaml.cs");

        StringAssert.Contains(xaml, "Microsoft.Web.WebView2.Wpf");
        StringAssert.Contains(xaml, "LegacySourceWebView");
        StringAssert.Contains(code, "LegacySourceWebView.NavigateToString(BuildLegacySourceDocument(workspace));");
        StringAssert.Contains(code, "item.LegacyMarkup");
        StringAssert.Contains(code, "legacy-source-layer");
    }

    [TestMethod]
    public void StudioRendersLegacyMarkupInSingleOrderedStack()
    {
        var code = ReadStudioFile("MainWindow.xaml.cs");

        StringAssert.Contains(code, ".OrderBy(item => item.ZIndex)");
        StringAssert.Contains(code, "BuildLegacyItemHtml");
        StringAssert.Contains(code, "BuildLegacyItemContent");
        StringAssert.Contains(code, "IsSvgLegacyMarkup");
        StringAssert.Contains(code, "trimmed.StartsWith(\"<polygon\"");
        StringAssert.Contains(code, "class=\"legacy-source-item\"");
        StringAssert.Contains(code, "z-index: {{zIndex}}");
        Assert.IsFalse(
            code.Contains("string.Concat(package.Items.Select(BuildLegacyItemHtml))", StringComparison.Ordinal),
            "SVG legacy elements such as polygon cannot be injected directly into HTML div wrappers.");
        Assert.IsFalse(
            code.Contains("id=\"legacy-svg-layer\"", StringComparison.Ordinal),
            "Separate SVG and HTML layers break global legacy z-order.");
        Assert.IsFalse(
            code.Contains("id=\"legacy-html-layer\"", StringComparison.Ordinal),
            "Separate SVG and HTML layers break global legacy z-order.");
    }

    [TestMethod]
    public void ReadOnlyPropertyTextBoxesUseOneWayBindings()
    {
        var xaml = ReadStudioFile("MainWindow.xaml");

        StringAssert.Contains(xaml, "SelectedItem.SourceElementId, Mode=OneWay");
        StringAssert.Contains(xaml, "SelectedItem.ElementName, Mode=OneWay");
        StringAssert.Contains(xaml, "SelectedItem.SourceName, Mode=OneWay");
        StringAssert.Contains(xaml, "SelectedItem.LegacyType, Mode=OneWay");
        StringAssert.Contains(xaml, "SelectedItem.Bounds.X, Mode=OneWay");
        StringAssert.Contains(xaml, "SelectedItem.Bounds.Y, Mode=OneWay");
        StringAssert.Contains(xaml, "SelectedItem.Bounds.Width, Mode=OneWay");
        StringAssert.Contains(xaml, "SelectedItem.Bounds.Height, Mode=OneWay");
        StringAssert.Contains(xaml, "SelectedItem.GeometrySummary, Mode=OneWay");
        Assert.IsFalse(
            xaml.Contains("Text=\"{Binding SelectedItem.SourceElementId}\"", StringComparison.Ordinal),
            "TextBox.Text defaults to TwoWay and crashes when bound to a read-only view-model property.");
    }

    [TestMethod]
    public void StudioLayoutPlacesDrawingToolsLeftAndContextTabsRight()
    {
        var xaml = ReadStudioFile("MainWindow.xaml");

        StringAssert.Contains(xaml, "Text=\"Outils de dessin\"");
        StringAssert.Contains(xaml, "Content=\"Polyline\"");
        StringAssert.Contains(xaml, "Content=\"Polygone\"");
        StringAssert.Contains(xaml, "Content=\"Image\"");
        Assert.IsTrue(
            xaml.IndexOf("Text=\"Outils de dessin\"", StringComparison.Ordinal) <
            xaml.IndexOf("<wv2:WebView2 x:Name=\"LegacySourceWebView\"", StringComparison.Ordinal),
            "Drawing tools should stay in the left panel before the central workzone.");
        Assert.IsTrue(
            xaml.IndexOf("<GridSplitter Grid.Row=\"0\" Grid.Column=\"3\"", StringComparison.Ordinal) <
            xaml.IndexOf("<TabItem Header=\"Element\">", StringComparison.Ordinal),
            "Element and Structure tabs should live in the right context panel with Properties and Component.");
    }

    [TestMethod]
    public void StudioWorkzoneUsesPackageBoundsAndNoPermanentRectangleOverlay()
    {
        var xaml = ReadStudioFile("MainWindow.xaml");
        var viewModel = ReadStudioFile("ElementStudioViewModels.cs");

        StringAssert.Contains(xaml, "Width=\"{Binding WorkzoneScaledWidth}\"");
        StringAssert.Contains(xaml, "Height=\"{Binding WorkzoneScaledHeight}\"");
        StringAssert.Contains(viewModel, "public double WorkzoneWidth");
        StringAssert.Contains(viewModel, "public double WorkzoneHeight");
        StringAssert.Contains(viewModel, "public double WorkzoneScaledWidth => WorkzoneWidth * WorkzoneZoom;");
        StringAssert.Contains(viewModel, "public double WorkzoneScaledHeight => WorkzoneHeight * WorkzoneZoom;");
        Assert.IsFalse(
            xaml.Contains("Opacity=\"0.35\"", StringComparison.Ordinal),
            "Imported source rectangles must not render as permanent graphic content in Studio.");
    }

    [TestMethod]
    public void StudioWorkzoneDoesNotRenderElementNameLabelsAsVisualContent()
    {
        var code = ReadStudioFile("MainWindow.xaml.cs");

        Assert.IsFalse(
            code.Contains("element-name-label", StringComparison.Ordinal),
            "Element001/Element002 labels are structure metadata and must not render as workzone content.");
        Assert.IsFalse(
            code.Contains("BuildElementLabelHtml", StringComparison.Ordinal),
            "Studio Element+ must not create permanent Element### label boxes in the workzone.");
        Assert.IsFalse(
            code.Contains("data-label-source-id", StringComparison.Ordinal),
            "Selection must target actual imported items, not separate label boxes.");
    }

    [TestMethod]
    public void StudioSupportsSourceSelectionAndSvgComponentDraft()
    {
        var xaml = ReadStudioFile("MainWindow.xaml");
        var code = ReadStudioFile("MainWindow.xaml.cs");
        var viewModel = ReadStudioFile("ElementStudioViewModels.cs");

        StringAssert.Contains(xaml, "Content=\"Creer SVG\"");
        StringAssert.Contains(xaml, "Click=\"OnCreateSvgComponentClick\"");
        StringAssert.Contains(code, "WebMessageReceived += OnLegacySourceWebMessageReceived");
        StringAssert.Contains(code, "window.chrome?.webview?.postMessage({ type: 'selectSources', sourceElementIds: Array.from(selectedIds) })");
        StringAssert.Contains(code, "workspace.SetSelectedItemIds(sourceElementIds);");
        StringAssert.Contains(code, "SynchronizeElementListSelection(workspace.SelectedItems);");
        StringAssert.Contains(code, "Composant SVG Element+ cree en memoire");
        StringAssert.Contains(viewModel, "public string ComponentVisualKind");
        StringAssert.Contains(viewModel, "public string ComponentSummary");
    }

    [TestMethod]
    public void StudioElementTabUsesElementPlusNamesForImportedItems()
    {
        var xaml = ReadStudioFile("MainWindow.xaml");
        var viewModel = ReadStudioFile("ElementStudioViewModels.cs");

        StringAssert.Contains(xaml, "<TabItem Header=\"Element\">");
        StringAssert.Contains(xaml, "Text=\"Elements importes\"");
        StringAssert.Contains(xaml, "SelectedItem.ElementName, Mode=OneWay");
        StringAssert.Contains(viewModel, "public string ElementName");
        StringAssert.Contains(viewModel, "CreateElementPlusName(index + 1)");
        StringAssert.Contains(viewModel, "return $\"Element{index:000}\";");
        StringAssert.Contains(viewModel, "public string DisplayName");
        StringAssert.Contains(viewModel, "return $\"{ElementName}  [{LegacyType}]{suffix}\";");
    }

    [TestMethod]
    public void StudioElementListSelectionHighlightsMultipleWorkzoneItems()
    {
        var xaml = ReadStudioFile("MainWindow.xaml");
        var code = ReadStudioFile("MainWindow.xaml.cs");
        var viewModel = ReadStudioFile("ElementStudioViewModels.cs");

        StringAssert.Contains(xaml, "SelectionMode=\"Extended\"");
        StringAssert.Contains(xaml, "SelectionChanged=\"OnElementSelectionChanged\"");
        StringAssert.Contains(code, "private async void OnElementSelectionChanged");
        StringAssert.Contains(code, "workspace.SetSelectedItems(selectedItems);");
        StringAssert.Contains(code, "SynchronizeElementListSelection(workspace.SelectedItems);");
        StringAssert.Contains(code, "HighlightSelectedSourcesInWebViewAsync");
        StringAssert.Contains(code, "window.elementStudio && window.elementStudio.selectSources");
        StringAssert.Contains(code, "function selectSources(sourceElementIds)");
        StringAssert.Contains(code, "selectedIds = new Set(sourceElementIds || []);");
        StringAssert.Contains(code, "SourceElementIds");
        StringAssert.Contains(viewModel, "public ObservableCollection<ElementStudioItemViewModel> SelectedItems");
        StringAssert.Contains(viewModel, "public void SetSelectedItems");
    }

    [TestMethod]
    public void StudioSelectionToolSupportsDragMarqueeAndSvgClickHitTesting()
    {
        var code = ReadStudioFile("MainWindow.xaml.cs");

        StringAssert.Contains(code, "id=\"selection-marquee\"");
        StringAssert.Contains(code, "border: 1px dashed rgba(0, 120, 212, .95);");
        StringAssert.Contains(code, "background: rgba(0, 120, 212, .07);");
        StringAssert.Contains(code, "box-shadow: inset 0 0 0 1px rgba(255,255,255,.65);");
        StringAssert.Contains(code, "sourceLayer.addEventListener('pointerdown'");
        StringAssert.Contains(code, "sourceLayer.addEventListener('pointermove'");
        StringAssert.Contains(code, "sourceLayer.addEventListener('pointerup'");
        StringAssert.Contains(code, "dragMode = 'move';");
        StringAssert.Contains(code, "dragMode = 'resize';");
        StringAssert.Contains(code, "applyMovePreview(current.x - dragStart.x, current.y - dragStart.y);");
        StringAssert.Contains(code, "applyResizePreview(current.x - dragStart.x, current.y - dragStart.y);");
        StringAssert.Contains(code, "postCommand('moveSources', { deltaX, deltaY });");
        StringAssert.Contains(code, "postCommand('resizeSources', { deltaWidth: deltaX, deltaHeight: deltaY });");
        StringAssert.Contains(code, "postCommand('deleteSelection');");
        StringAssert.Contains(code, "postCommand('duplicateSelection');");
        StringAssert.Contains(code, "class=\"resize-handle\"");
        StringAssert.Contains(code, "class=\"group-frame\"");
        StringAssert.Contains(code, "BuildGroupFramesHtml");
        StringAssert.Contains(code, "data-group-id");
        StringAssert.Contains(code, "function intersects(a, b)");
        StringAssert.Contains(code, ".filter(item => item.dataset.locked !== 'true' && intersects(itemRect(item), rect))");
        StringAssert.Contains(code, "item.dataset.locked === 'true'");
        Assert.IsTrue(
            code.IndexOf("if (event.altKey)", StringComparison.Ordinal) <
            code.IndexOf("} else if (event.shiftKey)", StringComparison.Ordinal),
            "Alt+click must remove from selection before Shift/Ctrl add/toggle handling.");
        StringAssert.Contains(code, "selectedIds.delete(sourceElementId);");
        StringAssert.Contains(code, "} else if (event.shiftKey) {");
        StringAssert.Contains(code, "selectedIds.add(sourceElementId);");
        StringAssert.Contains(code, "idsInRect.forEach(id => selectedIds.add(id));");
        StringAssert.Contains(code, "idsInRect.forEach(id => selectedIds.delete(id));");
        Assert.IsTrue(
            code.IndexOf("if (event.altKey)", code.IndexOf("if (wasDrag)", StringComparison.Ordinal), StringComparison.Ordinal) <
            code.IndexOf("} else if (event.shiftKey || event.ctrlKey || event.metaKey)", StringComparison.Ordinal),
            "Alt+drag must subtract from selection before additive drag handling.");
        StringAssert.Contains(code, "#selection-marquee-layer");
        StringAssert.Contains(code, "pointer-events: none;");
        StringAssert.Contains(code, "pointer-events: auto;");
    }

    [TestMethod]
    public void StudioSelectionToolSupportsEscapeClearAndGeometryFieldSync()
    {
        var code = ReadStudioFile("MainWindow.xaml.cs");
        var xaml = ReadStudioFile("MainWindow.xaml");

        StringAssert.Contains(code, "event.key === 'Escape'");
        StringAssert.Contains(code, "postCommand('clearSelection');");
        StringAssert.Contains(code, "case \"clearSelection\":");
        StringAssert.Contains(code, "workspace.ClearSelection();");
        StringAssert.Contains(code, "UpdateSelectionGeometryFields();");
        StringAssert.Contains(code, "FormatCommonSelectionValue");
        StringAssert.Contains(code, "SelectionXTextBox.Text = FormatCommonSelectionValue(selected, item => item.Bounds.X);");
        StringAssert.Contains(code, "SelectionYTextBox.Text = FormatCommonSelectionValue(selected, item => item.Bounds.Y);");
        StringAssert.Contains(code, "SelectionWidthTextBox.Text = FormatCommonSelectionValue(selected, item => item.Bounds.Width);");
        StringAssert.Contains(code, "SelectionHeightTextBox.Text = FormatCommonSelectionValue(selected, item => item.Bounds.Height);");
        StringAssert.Contains(xaml, "x:Name=\"SelectionXTextBox\"");
        StringAssert.Contains(xaml, "x:Name=\"SelectionYTextBox\"");
        StringAssert.Contains(xaml, "x:Name=\"SelectionWidthTextBox\"");
        StringAssert.Contains(xaml, "x:Name=\"SelectionHeightTextBox\"");
    }

    [TestMethod]
    public void StudioWorkzoneCanBeResizedFromViewRibbon()
    {
        var xaml = ReadStudioFile("MainWindow.xaml");
        var code = ReadStudioFile("MainWindow.xaml.cs");
        var viewModel = ReadStudioFile("ElementStudioViewModels.cs");

        StringAssert.Contains(xaml, "Content=\"Zone -\"");
        StringAssert.Contains(xaml, "Content=\"Zone +\"");
        StringAssert.Contains(xaml, "Content=\"Fit zone\"");
        StringAssert.Contains(xaml, "Text=\"{Binding WorkzoneSizeText}\"");
        StringAssert.Contains(code, "OnShrinkWorkzoneClick");
        StringAssert.Contains(code, "OnGrowWorkzoneClick");
        StringAssert.Contains(code, "OnFitWorkzoneClick");
        StringAssert.Contains(viewModel, "public void ResizeWorkzone(double deltaWidth, double deltaHeight)");
        StringAssert.Contains(viewModel, "public void FitWorkzoneToImportedBounds()");
        StringAssert.Contains(viewModel, "Math.Clamp(value, 160, 20000)");
        StringAssert.Contains(viewModel, "Math.Clamp(value, 120, 20000)");
    }

    [TestMethod]
    public void StudioFileRibbonSavesSepComponent()
    {
        var project = ReadStudioFile("ScadaBuilderV2.ElementStudio.App.csproj");
        var xaml = ReadStudioFile("MainWindow.xaml");
        var code = ReadStudioFile("MainWindow.xaml.cs");
        var viewModel = ReadStudioFile("ElementStudioViewModels.cs");

        StringAssert.Contains(project, "ScadaBuilderV2.Infrastructure");
        StringAssert.Contains(xaml, "Content=\"Enregistrer\" Click=\"OnSaveComponentClick\"");
        StringAssert.Contains(xaml, "Content=\"Ajouter a la librairie\" Click=\"OnSaveComponentAsClick\"");
        StringAssert.Contains(xaml, "SavedComponentPath, Mode=OneWay");
        StringAssert.Contains(code, "ElementStudioComponentPackageStore");
        StringAssert.Contains(code, "private async void OnSaveComponentClick");
        StringAssert.Contains(code, "private async void OnSaveComponentAsClick");
        StringAssert.Contains(code, "SaveFileDialog");
        StringAssert.Contains(code, "CreateCurrentComponentPackage");
        StringAssert.Contains(code, "ElementStudioComponentPackageFactory.Create");
        StringAssert.Contains(code, "BuildComponentSvgMarkup");
        StringAssert.Contains(code, "CreateEmbeddedAssetMap");
        StringAssert.Contains(code, "ElementStudioComponentPartKind.Image");
        StringAssert.Contains(code, "componentPackageStore.WriteToPathAsync");
        StringAssert.Contains(code, "viewBox=\"{viewBoxX} {viewBoxY} {width} {height}\"");
        StringAssert.Contains(viewModel, "public string SavedComponentPath");
        StringAssert.Contains(viewModel, "public IReadOnlyList<string> ComponentTagList");
    }

    [TestMethod]
    public void StudioRebasesHtmlLegacyMarkupAndMapsLocalAssets()
    {
        var code = ReadStudioFile("MainWindow.xaml.cs");

        StringAssert.Contains(code, "SetVirtualHostNameToFolderMapping");
        StringAssert.Contains(code, "https://studio-import.local/{value}");
        StringAssert.Contains(code, "NormalizeHtmlLegacyMarkupForStudio");
        StringAssert.Contains(code, "NormalizeHtmlStyleForStudio");
        StringAssert.Contains(code, "\"left\",");
        StringAssert.Contains(code, "\"top\",");
        StringAssert.Contains(code, "declarations.Add(\"position: static\")");
        StringAssert.Contains(code, "declarations.Add(\"width: 100%\")");
        StringAssert.Contains(code, "declarations.Add(\"height: 100%\")");
    }

    [TestMethod]
    public void StudioSanitizesLegacySelectionArtifacts()
    {
        var code = ReadStudioFile("MainWindow.xaml.cs");

        StringAssert.Contains(code, "SanitizeLegacyMarkup");
        StringAssert.Contains(code, "data-scada-selected");
        StringAssert.Contains(code, "outline");
        StringAssert.Contains(code, "box-shadow");
        StringAssert.Contains(code, "SanitizeLegacyMarkup(item.LegacyMarkup)");
    }

    private static string ReadStudioFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "ScadaBuilderV2.ElementStudio.App",
                fileName);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate Studio Element+ file: {fileName}");
        return "";
    }
}
