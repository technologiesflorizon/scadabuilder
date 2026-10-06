using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Locks the quick-window authoring shell: distinct project group, bounded editor context,
/// page-only command policy and the wiring-only rule for MainWindow. The DEC-0050 `QuickWindow` history
/// scope and its content action were decommissioned with their tests on 2026-10-06 (no producer outside tests).
/// Decisions: DEC-0050 (décommissionné), FR-033, FR-035, FR-UI-12, FR-UI-14, FR-UI-25, FR-UI-26. Plan: Task 3.1.
/// </summary>
[TestClass]
public sealed class QuickWindowShellContractTests
{
    private static readonly Guid DefinitionKey = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [TestMethod]
    public void QuickWindowContextHidesPageOnlyCommandsAndKeepsAuthoringCommands()
    {
        var quickWindow = QuickWindowEditorContext.ForQuickWindow(DefinitionKey, "motor", "Motor");

        foreach (var pageOnly in new[]
                 {
                     "page.new", "page.rename", "page.duplicate", "page.delete", "page.open",
                     "page.set-home", "page.set-build-inclusion", "page.set-type", "page.set-composition",
                     "page.properties", "page.validate", "import.legacy", "import.tags"
                 })
        {
            Assert.IsFalse(quickWindow.IsCommandVisible(pageOnly), $"'{pageOnly}' is page-only and must be hidden.");
        }

        foreach (var authoring in new[]
                 {
                     "edit.undo", "edit.redo", "edit.copy", "edit.paste",
                     "object.group", "object.ungroup", "object.lock",
                     "layer.forward", "layer.backward", "tool.select", "tool.text"
                 })
        {
            Assert.IsTrue(quickWindow.IsCommandVisible(authoring), $"'{authoring}' must stay available on a quick window.");
        }
    }

    [TestMethod]
    public void PageContextKeepsEveryPageCommandAndIsLabelledDistinctly()
    {
        var pageKey = Guid.NewGuid();
        var page = QuickWindowEditorContext.ForPage(pageKey, "win00003");
        var quickWindow = QuickWindowEditorContext.ForQuickWindow(DefinitionKey, "motor", "Motor");

        Assert.IsTrue(page.IsPage);
        Assert.IsFalse(page.IsQuickWindow);
        Assert.IsTrue(page.IsCommandVisible("page.set-home"));
        Assert.AreEqual(pageKey, page.PageKey);

        Assert.IsTrue(quickWindow.IsQuickWindow);
        Assert.IsFalse(quickWindow.IsPage);
        Assert.AreEqual(DefinitionKey, quickWindow.QuickWindowDefinitionKey);
        Assert.AreEqual("Fenêtre rapide", quickWindow.ContextBadge);
        StringAssert.Contains(quickWindow.ContextTitle, "Motor");
        Assert.AreNotEqual(page.ContextBadge, quickWindow.ContextBadge, "the active context must be unambiguous");
    }

    [TestMethod]
    public void MainWindowCodeBehindKeepsOnlyQuickWindowWiring()
    {
        var source = ReadAppFile("MainWindow.xaml.cs");

        foreach (var businessType in new[]
                 {
                     "QuickWindowDefinitionService", "QuickWindowInvocationService", "QuickWindowDependencyAnalyzer",
                     "QuickWindowInterfaceCompatibility", "QuickWindowWorkspaceMutation", "QuickWindowBindingValidator"
                 })
        {
            Assert.IsFalse(
                source.Contains(businessType, StringComparison.Ordinal),
                $"'{businessType}' is quick-window business logic and must live outside MainWindow.xaml.cs.");
        }

        var quickWindowLines = source.Split('\n').Count(line => line.Contains("QuickWindow", StringComparison.Ordinal));
        Assert.IsTrue(
            quickWindowLines <= 12,
            $"MainWindow.xaml.cs carries {quickWindowLines} quick-window lines; only controller wiring belongs here.");
    }

    [TestMethod]
    public void MainWindowXamlExposesTheParameterSheetGroupAndItsCommands()
    {
        // DEC-0052 superseded the front door: the "Fenêtres rapides" panel now lists parameter sheets,
        // not quick-window definitions. Definitions are still produced by the compiler, but this panel no
        // longer offers "quick-window.new"/"quick-window.open"/etc. as a manual entry point.
        var xaml = ReadAppFile("MainWindow.xaml");

        StringAssert.Contains(xaml, "x:Name=\"QuickWindowsAnchorable\"");
        StringAssert.Contains(xaml, "Title=\"Fenêtres rapides\"");
        StringAssert.Contains(xaml, "x:Name=\"ParameterSheetsListBox\"");
        StringAssert.Contains(xaml, "ItemsSource=\"{Binding ParameterSheetsPanel.Items}\"");
        StringAssert.Contains(xaml, "SelectionChanged=\"OnParameterSheetSelectionChanged\"");
        StringAssert.Contains(xaml, "MouseDoubleClick=\"OnParameterSheetsListMouseDoubleClick\"");

        foreach (var command in new[]
                 {
                     "parameter-sheet.new", "parameter-sheet.open", "parameter-sheet.rename",
                     "parameter-sheet.duplicate", "parameter-sheet.delete"
                 })
        {
            StringAssert.Contains(xaml, $"Tag=\"{command}\"");
        }

        Assert.IsFalse(
            xaml.Contains("x:Name=\"QuickWindowsListBox\"", StringComparison.Ordinal),
            "the panel no longer lists quick-window definitions directly");
        Assert.IsFalse(
            xaml.Contains("x:Name=\"PagesListBox\" ItemsSource=\"{Binding QuickWindows", StringComparison.Ordinal),
            "quick windows must not be mixed into the pages list");
    }

    [TestMethod]
    public void QuickWindowCanvasProjectionIsEditorOnlyAndNeverExportable()
    {
        var definition = Definition() with
        {
            Content = new VisualContent(new CanvasSize(640, 480), Elements: []),
        };

        var projection = QuickWindowPreviewProjection.Create(definition);

        Assert.IsFalse(projection.Reference.IncludeInBuild, "a projected quick window must never be exportable");
        Assert.AreEqual(PageOrigin.Native, projection.Reference.EffectiveOrigin);
        Assert.AreEqual(definition.DefinitionKey, projection.Reference.PageKey, "the definition key stays the routing key");
        StringAssert.StartsWith(projection.ProjectedCode, QuickWindowPreviewProjection.ProjectedCodePrefix);
        Assert.AreEqual(new CanvasSize(640, 480), projection.Scene.CanvasSize);
        Assert.AreEqual(definition.EffectiveTitle, projection.Scene.Title);
        Assert.AreEqual(string.Empty, projection.Reference.RelativePath, "no durable page file backs the projection");
    }

    [TestMethod]
    public void QuickWindowProjectionCodeCannotCollideWithAPageCode()
    {
        var projection = QuickWindowPreviewProjection.Create(Definition());

        Assert.AreNotEqual("motor", projection.ProjectedCode);
        Assert.AreEqual("qw-motor", projection.ProjectedCode);
    }

    [TestMethod]
    public void HostedQuickWindowSurfaceIgnoresEveryCanvasMessage()
    {
        var source = ReadAppFile("MainWindow.xaml.cs");
        var handler = source.IndexOf("private void OnLegacyViewerMessageReceived", StringComparison.Ordinal);
        Assert.AreNotEqual(-1, handler, "the canvas message handler must exist");
        var guard = source.IndexOf("if (IsQuickWindowSurfaceHosted) return;", handler, StringComparison.Ordinal);
        var firstDispatch = source.IndexOf("ForwardTableWebViewMessage", handler, StringComparison.Ordinal);

        Assert.AreNotEqual(-1, guard, "a hosted quick window must gate the canvas message handler.");
        Assert.IsTrue(
            guard < firstDispatch,
            "the read-only gate must run before any canvas message is dispatched to the active page.");
    }
    private static QuickWindowDefinition Definition() =>
        new(
            DefinitionKey,
            "motor",
            "Motor",
            1,
            new VisualContent(new CanvasSize(480, 320), Elements: []),
            [],
            new QuickWindowPresentationDefaults());

    private static string ReadAppFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "ScadaBuilderV2.App", relativePath);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate src/ScadaBuilderV2.App/{relativePath}.");
        return string.Empty;
    }
}
