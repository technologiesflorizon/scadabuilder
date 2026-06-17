using System.Text.Json;
using ScadaBuilderV2.Application.ElementStudio;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ElementStudio;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class StudioElementPlusContractTests
{
    [TestMethod]
    public async Task Ft1TransferPackageRemainsBuilderToStudioImportOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var package = CreatePackage();
        var writer = new ElementStudioImportPackageWriter();

        try
        {
            var path = await writer.WriteToProjectAsync(package, root);

            Assert.AreEqual(
                Path.Combine(root, "AMR_REF_SCADA_V2", ".studio", "imports", "pipe_source_001.ft1"),
                path);
            Assert.AreEqual(".ft1", Path.GetExtension(path));
            Assert.IsFalse(path.EndsWith(".sep", StringComparison.OrdinalIgnoreCase));

            var json = await File.ReadAllTextAsync(path);
            StringAssert.Contains(json, "\"Schema\": \"scada-builder-v2.element-studio.import\"");
            StringAssert.Contains(json, "\"Format\": \"json.ft1\"");
            StringAssert.Contains(json, "\"SourceSceneId\": \"win00008\"");
            var loaded = JsonSerializer.Deserialize<ElementStudioImportPackage>(
                json,
                ElementStudioImportPackageWriter.CreateJsonSerializerOptions());
            Assert.IsNotNull(loaded);
            Assert.AreEqual("<polygon points=\"75,179 116,179 116,203\" />", loaded.Items.Single().LegacyMarkup);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void SepSharedLibraryContractIsOneComponentPerFile()
    {
        var sepContract = ReadProjectFile("docs", "05_studio_element_plus", "STUDIO_ELEMENT_PLUS_SEP_CONTRACT_V2.md");

        StringAssert.Contains(sepContract, "*.sep");
        StringAssert.Contains(sepContract, "projects/<project-id>/library/elements/");
        StringAssert.Contains(sepContract, "`.sep` is the editable Studio Element+ component source format.");
        StringAssert.Contains(sepContract, "One `.sep` file contains exactly one Element+ component.");
        StringAssert.Contains(sepContract, "The `.sep` output is the editable Element+ component source file.");
        StringAssert.Contains(sepContract, "`.sep` is the shared library format consumed by Studio Element+ and SCADA Builder V2.");
    }

    [TestMethod]
    public async Task ScadaBuilderLibraryReaderLoadsSepComponentsFromProjectLibrary()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var libraryRoot = Path.Combine(root, "library", "elements");
        var store = new ElementStudioComponentPackageStore();
        var reader = new ElementPlusLibraryReader();

        try
        {
            var package = ElementStudioComponentPackageFactory.CreateSvg(
                "pipe-test",
                "Pipe Test",
                new SceneBounds(0, 0, 120, 32),
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 120 32\"><path d=\"M0 16 H120\" /></svg>",
                ElementStudioComponentMetadata.Current("V2.0.3.0019"),
                category: "Piping",
                tags: ["legacy", "pipe"]);

            await store.WriteToLibraryAsync(package, libraryRoot);

            var snapshot = await reader.ReadAsync(libraryRoot);

            Assert.AreEqual(1, snapshot.Items.Count);
            Assert.AreEqual(0, snapshot.Diagnostics.Count);
            Assert.AreEqual("pipe-test", snapshot.Items[0].ComponentId);
            Assert.AreEqual("Pipe Test", snapshot.Items[0].Name);
            Assert.AreEqual("Piping", snapshot.Items[0].Category);
            Assert.AreEqual(ElementStudioComponentVisualKind.Svg, snapshot.Items[0].VisualKind);
            StringAssert.Contains(snapshot.Items[0].PreviewMarkup, "<svg");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void SvgNormalizerRestoresVisibleViewBoxForLegacyAbsoluteGeometry()
    {
        const string brokenSvg = """
<svg xmlns="http://www.w3.org/2000/svg" width="12" height="333" viewBox="0 0 12 333">
  <g id="Element001">
    <polygon points="209.0,385.0 209.0,718.0 198.0,718.0 198.0,385.0 209.0,385.0" fill="rgba(127,127,127,1.000)" />
  </g>
</svg>
""";

        var normalized = ElementStudioSvgMarkupNormalizer.NormalizeSvgMarkup(brokenSvg);

        StringAssert.Contains(normalized, "viewBox=\"198 385 11 333\"");
        StringAssert.Contains(normalized, "width=\"11\"");
        StringAssert.Contains(normalized, "height=\"333\"");
        StringAssert.Contains(normalized, "points=\"209.0,385.0 209.0,718.0 198.0,718.0 198.0,385.0 209.0,385.0\"");
    }

    [TestMethod]
    public void ScadaBuilderLibraryPanelWatchesProjectSepDirectory()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml");
        var code = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");
        var modernProjectStore = ReadProjectFile("src", "ScadaBuilderV2.Infrastructure", "ModernProjects", "ModernProjectStore.cs");
        var studioCode = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml.cs");

        StringAssert.Contains(xaml, "<TabItem Header=\"Librairie\">");
        StringAssert.Contains(xaml, "x:Name=\"ElementLibraryListBox\"");
        StringAssert.Contains(xaml, "x:Name=\"ElementLibraryStatusText\"");
        StringAssert.Contains(xaml, "<WrapPanel/>");
        StringAssert.Contains(xaml, "PreviewMouseMove=\"OnElementLibraryPreviewMouseMove\"");
        StringAssert.Contains(xaml, "MouseDoubleClick=\"OnElementLibraryMouseDoubleClick\"");
        StringAssert.Contains(xaml, "local:HtmlPreviewControl");
        StringAssert.Contains(xaml, "Markup=\"{Binding PreviewMarkup}\"");
        StringAssert.Contains(xaml, "AllowDrop=\"True\"");
        StringAssert.Contains(xaml, "Drop=\"OnPreviewWebViewDrop\"");
        StringAssert.Contains(code, "ElementPlusLibraryReader");
        StringAssert.Contains(code, "FileSystemWatcher(libraryRoot, \"*.sep\")");
        StringAssert.Contains(code, "StartElementLibraryWatcher();");
        StringAssert.Contains(code, "RefreshElementLibraryAsync();");
        StringAssert.Contains(code, "ElementPlusLibraryDragFormat");
        StringAssert.Contains(code, "DragDrop.DoDragDrop");
        StringAssert.Contains(code, "OnElementLibraryMouseDoubleClick");
        StringAssert.Contains(code, "ResolveVisibleSceneCenterAsync");
        StringAssert.Contains(code, "centerOnPoint: true");
        StringAssert.Contains(code, "CreateElementPlusLibraryInstanceAsync");
        StringAssert.Contains(code, "ScadaElementKind.Custom");
        StringAssert.Contains(code, "scada-modern-custom-content");
        StringAssert.Contains(code, "ElementStudioSvgMarkupNormalizer.NormalizeSvgMarkup");
        StringAssert.Contains(code, "wrapper.style.padding = '0'");
        StringAssert.Contains(code, "\"library\"");
        StringAssert.Contains(code, "\"elements\"");
        StringAssert.Contains(
            ReadProjectFile("src", "ScadaBuilderV2.App", "HtmlPreviewControl.cs"),
            "NavigateToString");
        StringAssert.Contains(modernProjectStore, "Path.Combine(projectRoot, \"library\", \"elements\")");
        StringAssert.Contains(studioCode, "workspace.Package.TargetLibraryPath");
        StringAssert.Contains(studioCode, "Directory.CreateDirectory(explicitLibrary)");
        StringAssert.Contains(studioCode, "ResolveProjectLibraryFromDirectory(importPackageDirectory)");
        StringAssert.Contains(studioCode, "ResolveProjectLibraryFromRepository(workspace.Package.SourceProjectId)");
        Assert.IsFalse(
            studioCode.Contains("Path.GetDirectoryName(sourcePage)", StringComparison.Ordinal),
            "Studio Element+ Save As .sep must not infer the target library from the legacy source page path.");
    }

    [TestMethod]
    public void ScadaBuilderLaunchesStudioFromProjectInDevelopmentToAvoidStaleBinaries()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");

        StringAssert.Contains(code, "LaunchElementStudioProjectAsync(studioProjectPath, packagePath)");
        StringAssert.Contains(code, "Path.Combine(AppContext.BaseDirectory, \"ScadaBuilderV2.ElementStudio.App.exe\")");
        Assert.IsFalse(
            code.Contains("Path.Combine(sourceRoot, \"bin\", \"Debug\"", StringComparison.Ordinal),
            "SCADA Builder must not launch source bin\\Debug Studio Element+ executables because they can be stale.");
        Assert.IsFalse(
            code.Contains("Path.Combine(sourceRoot, \"bin\", \"Release\"", StringComparison.Ordinal),
            "SCADA Builder must not launch source bin\\Release Studio Element+ executables because they can be stale.");
    }

    [TestMethod]
    public void TagCatalogPanelExposesSearchFiltersAndFilteredSummary()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml");
        var code = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");

        StringAssert.Contains(xaml, "<TabItem Header=\"Catalogue Tags\">");
        StringAssert.Contains(xaml, "x:Name=\"TagCatalogFilteredSummaryText\"");
        StringAssert.Contains(xaml, "x:Name=\"TagCatalogSearchTextBox\"");
        StringAssert.Contains(xaml, "ToolTip=\"Recherche tag\"");
        StringAssert.Contains(xaml, "x:Name=\"TagCatalogDeviceFilterComboBox\"");
        StringAssert.Contains(xaml, "x:Name=\"TagCatalogDatatypeFilterComboBox\"");
        StringAssert.Contains(xaml, "x:Name=\"TagCatalogAccessFilterComboBox\"");
        StringAssert.Contains(xaml, "x:Name=\"TagCatalogStateFilterComboBox\"");
        StringAssert.Contains(xaml, "TextChanged=\"OnTagCatalogFilterChanged\"");
        StringAssert.Contains(xaml, "SelectionChanged=\"OnTagCatalogFilterChanged\"");
        StringAssert.Contains(xaml, "Header=\"Id\"");
        StringAssert.Contains(xaml, "Binding=\"{Binding Id}\"");
        StringAssert.Contains(code, "CollectionViewSource.GetDefaultView(_tagCatalogItems)");
        StringAssert.Contains(code, "_tagCatalogView.Filter = FilterTagCatalogItem");
        StringAssert.Contains(code, "RefreshTagCatalogFilterOptions();");
        StringAssert.Contains(code, "TagCatalogFilterMatches(");
        StringAssert.Contains(code, "MatchesTextSearch(");
        StringAssert.Contains(code, "MatchesExactFilter(");
        StringAssert.Contains(code, "UpdateTagCatalogFilteredSummary();");
        StringAssert.Contains(code, "public string SearchText => string.Join");
        StringAssert.Contains(code, "Tous les appareils");
        StringAssert.Contains(code, "Tous les types");
        StringAssert.Contains(code, "Tous les acces");
        StringAssert.Contains(code, "Tous les etats");
    }

    [TestMethod]
    public void ElementTabSelectionContractKeepsListAndWorkzoneInSync()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml");
        var code = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml.cs");
        var viewModel = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "ElementStudioViewModels.cs");

        StringAssert.Contains(xaml, "<TabItem Header=\"Element\">");
        StringAssert.Contains(xaml, "ItemsSource=\"{Binding ImportedItems}\"");
        StringAssert.Contains(xaml, "SelectionMode=\"Extended\"");
        StringAssert.Contains(xaml, "SelectionChanged=\"OnElementSelectionChanged\"");
        StringAssert.Contains(code, "workspace.SetSelectedItems(selectedItems);");
        StringAssert.Contains(code, "HighlightSelectedSourcesInWebViewAsync(selectedItems.Select(item => item.SourceElementId))");
        StringAssert.Contains(code, "workspace.SetSelectedItemIds(sourceElementIds);");
        StringAssert.Contains(code, "SynchronizeElementListSelection(workspace.SelectedItems);");
        StringAssert.Contains(code, "workspace.MoveSelectedItemsBy(message.DeltaX, message.DeltaY);");
        StringAssert.Contains(code, "workspace.ResizeSelectedItemsBy(message.DeltaWidth, message.DeltaHeight);");
        StringAssert.Contains(code, "workspace.DeleteSelectedItems();");
        StringAssert.Contains(code, "workspace.DuplicateSelectedItems();");
        StringAssert.Contains(code, "workspace.SetSelectedBounds(");
        StringAssert.Contains(code, "window.elementStudio = { selectSources };");
        StringAssert.Contains(code, "item.dataset.selected = selectedIds.has(item.dataset.sourceId) ? 'true' : 'false';");
        Assert.IsFalse(
            code.Contains("labelSourceId", StringComparison.Ordinal),
            "Studio Element+ selection must stay bound to actual imported elements, not visual Element### label overlays.");
        StringAssert.Contains(viewModel, "public ObservableCollection<ElementStudioItemViewModel> ImportedItems");
        StringAssert.Contains(viewModel, "public ObservableCollection<ElementStudioItemViewModel> SelectedItems");
        StringAssert.Contains(viewModel, "public void ResizeSelectedItemsBy");
        StringAssert.Contains(viewModel, "public void SetSelectedBounds");
        StringAssert.Contains(viewModel, "states.Add(\"Hidden\");");
        StringAssert.Contains(viewModel, "states.Add(\"Lock\");");
        StringAssert.Contains(viewModel, "\\\"groupId\\\"");
        StringAssert.Contains(viewModel, "\\\"isLocked\\\":true");
    }

    [TestMethod]
    public void StudioSelectionDecisionBaseIsDocumentedAndReferenced()
    {
        var architecture = ReadProjectFile("docs", "05_studio_element_plus", "STUDIO_ELEMENT_PLUS_ARCHITECTURE_V2.md");
        var decisions = ReadProjectFile("docs", "05_studio_element_plus", "STUDIO_ELEMENT_PLUS_SELECTION_CONTRACT_V2.md");

        StringAssert.Contains(architecture, "docs/05_studio_element_plus/STUDIO_ELEMENT_PLUS_SELECTION_CONTRACT_V2.md");
        StringAssert.Contains(architecture, "`Shift + clic` adds to the current selection");
        StringAssert.Contains(architecture, "`Alt + clic` removes from the current selection");
        StringAssert.Contains(decisions, "Status: Approved decision base");
        StringAssert.Contains(decisions, "`Shift + clic` adds the clicked element to the current selection.");
        StringAssert.Contains(decisions, "`Alt + clic` removes the clicked element from the current selection.");
        StringAssert.Contains(decisions, "`Shift + drag` adds intersecting elements to the current selection.");
        StringAssert.Contains(decisions, "`Alt + drag` removes intersecting elements from the current selection.");
        StringAssert.Contains(decisions, "Tests must be added or updated when selection behavior changes.");
    }

    [TestMethod]
    public void DrawingToolContractRequiresRealComponentPrimitives()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml");
        var sepContract = ReadProjectFile("docs", "05_studio_element_plus", "STUDIO_ELEMENT_PLUS_SEP_CONTRACT_V2.md");

        StringAssert.Contains(xaml, "Text=\"Outils de dessin\"");
        StringAssert.Contains(xaml, "Content=\"Ligne\"");
        StringAssert.Contains(xaml, "Content=\"Polyline\"");
        StringAssert.Contains(xaml, "Content=\"Rectangle\"");
        StringAssert.Contains(xaml, "Content=\"Polygone\"");
        StringAssert.Contains(xaml, "Content=\"Image\"");

        StringAssert.Contains(sepContract, "Make drawing tools functional:");
        StringAssert.Contains(sepContract, "`Ligne`: create line primitives.");
        StringAssert.Contains(sepContract, "`Polyline`: create editable polyline primitives.");
        StringAssert.Contains(sepContract, "`Rectangle`: create editable rectangle primitives.");
        StringAssert.Contains(sepContract, "`Polygone`: create editable polygon primitives.");
        StringAssert.Contains(sepContract, "`Image`: import and embed raster images into the `.sep`.");
        StringAssert.Contains(sepContract, "Ensure created primitives become part of the Element+ component model, not editor-only overlays.");
        StringAssert.Contains(sepContract, "Drawing tools create real component primitives, not temporary editor overlays.");
    }

    [TestMethod]
    public void SepExportContractExcludesWorkzoneAndEditorState()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml");
        var sepContract = ReadProjectFile("docs", "05_studio_element_plus", "STUDIO_ELEMENT_PLUS_SEP_CONTRACT_V2.md");

        StringAssert.Contains(xaml, "x:Name=\"WorkzoneSurface\"");
        StringAssert.Contains(xaml, "Width=\"{Binding WorkzoneScaledWidth}\"");
        StringAssert.Contains(xaml, "Height=\"{Binding WorkzoneScaledHeight}\"");
        StringAssert.Contains(xaml, "Text=\"{Binding WorkzoneSizeText}\"");
        StringAssert.Contains(sepContract, "The Studio workzone/canvas/viewport is an editor surface and must not be exported as part of the Element+ payload.");
        StringAssert.Contains(sepContract, "Ensure the workzone, zoom level, pan position, selection rectangle, and editor UI overlays are not exported as component geometry.");
        StringAssert.Contains(sepContract, "The Studio workzone is editor state and must not become exported Element+ geometry.");
        StringAssert.Contains(sepContract, "Legacy selection UI artifacts must not become part of the component payload.");
    }

    [TestMethod]
    public void StudioFinalPolishContractKeepsSelectionStructureAndDecisionTrace()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml");
        var code = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml.cs");
        var viewModel = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "ElementStudioViewModels.cs");
        var decisions = ReadProjectFile("docs", "05_studio_element_plus", "STUDIO_ELEMENT_PLUS_SELECTION_CONTRACT_V2.md");
        var version = ReadProjectFile("VERSION");

        StringAssert.Contains(version, "V2.1.2.0014");
        StringAssert.Contains(code, "case \"clearSelection\":");
        StringAssert.Contains(code, "UpdateSelectionGeometryFields();");
        StringAssert.Contains(xaml, "Text=\"{Binding StructureSummary}\"");
        StringAssert.Contains(xaml, "Text=\"{Binding SelectionStateSummary}\"");
        StringAssert.Contains(xaml, "Text=\"{Binding VisibleItemCount}\"");
        StringAssert.Contains(xaml, "Text=\"{Binding HiddenItemCount}\"");
        StringAssert.Contains(xaml, "Text=\"{Binding LockedItemCount}\"");
        StringAssert.Contains(xaml, "Text=\"{Binding GroupCount}\"");
        Assert.IsFalse(
            xaml.Contains("Groupes et calques a venir", StringComparison.Ordinal),
            "The Structure tab must expose live editor state, not placeholder text.");
        StringAssert.Contains(viewModel, "public string StructureSummary");
        StringAssert.Contains(viewModel, "public string SelectionStateSummary");
        StringAssert.Contains(viewModel, "private void NotifyEditorStateChanged()");
        StringAssert.Contains(decisions, "## 10. Final Test And Polish Slice V2.1.0.0004");
        StringAssert.Contains(decisions, "`Escape` clears the active workzone selection");
    }

    private static ElementStudioImportPackage CreatePackage()
    {
        return ElementStudioImportPackageFactory.Create(
            "pipe_source_001",
            "AMR_REF_SCADA_V2",
            "win00008",
            "dist/pages/win00008.html",
            new[]
            {
                new ElementStudioLegacyItem(
                    "793",
                    "Polygon9",
                    "polygon",
                    new SceneBounds(75, 179, 41, 24),
                    new SceneBounds(0, 0, 0, 0),
                    "M 75 179 L 116 179 L 116 203 Z",
                    "<polygon points=\"75,179 116,179 116,203\" />",
                    null,
                    ElementStudioStyleSnapshot.Default,
                    4,
                    "{\"source\":\"legacy-svg\"}")
            },
            ElementStudioPackageMetadata.Current("V2.0.3.0016"));
    }

    private static string ReadProjectFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate project file: {Path.Combine(segments)}");
        return "";
    }
}
