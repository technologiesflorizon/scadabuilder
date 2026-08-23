using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Locks the quick-window authoring shell: distinct project group, bounded editor context,
/// page-only command policy, single workspace history and the wiring-only rule for MainWindow.
/// Decisions: DEC-0050, FR-033, FR-035, FR-UI-12, FR-UI-14, FR-UI-25, FR-UI-26. Plan: Task 3.1.
/// </summary>
[TestClass]
public sealed class QuickWindowShellContractTests
{
    private static readonly Guid DefinitionKey = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherDefinitionKey = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [TestMethod]
    public void QuickWindowHistoryTargetIsDistinctFromSceneAndProject()
    {
        var target = EditorHistoryTarget.ForQuickWindow(DefinitionKey);

        Assert.AreEqual(EditorHistoryScope.QuickWindow, target.Scope);
        Assert.AreEqual(DefinitionKey, target.QuickWindowDefinitionKey);
        Assert.IsTrue(target.RefersToSameTarget(EditorHistoryTarget.ForQuickWindow(DefinitionKey)));
        Assert.IsFalse(target.RefersToSameTarget(EditorHistoryTarget.ForQuickWindow(OtherDefinitionKey)));
        Assert.IsFalse(target.RefersToSameTarget(EditorHistoryTarget.Project));
        Assert.IsFalse(target.RefersToSameTarget(EditorHistoryTarget.ForScene("win00003")));
    }

    [TestMethod]
    public async Task AlternatingPageAndQuickWindowMutationsShareOneUndoStack()
    {
        var definition = Definition();
        var history = new EditorHistoryService();
        var context = CreateContext(definition, out var state);

        history.Push(new QuickWindowContentChangedAction(
            DefinitionKey,
            definition.EffectiveContent,
            definition.EffectiveContent with { CanvasSize = new CanvasSize(640, 480) },
            "resize quick window canvas"));
        history.Push(new DelegateEditorHistoryAction("win00003", "move element", _ => Task.CompletedTask, _ => Task.CompletedTask));
        history.Push(new QuickWindowContentChangedAction(
            DefinitionKey,
            definition.EffectiveContent with { CanvasSize = new CanvasSize(640, 480) },
            definition.EffectiveContent with { CanvasSize = new CanvasSize(800, 600) },
            "resize quick window canvas"));

        Assert.AreEqual(3, history.UndoCount, "quick windows must not create a second history stack");

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.AreEqual(new CanvasSize(640, 480), state.Definition.EffectiveContent.CanvasSize);
        Assert.IsTrue(await history.UndoAsync(context), "a page action stays undoable between two quick-window actions");
        Assert.IsTrue(await history.UndoAsync(context));
        Assert.AreEqual(new CanvasSize(480, 320), state.Definition.EffectiveContent.CanvasSize);
        Assert.AreEqual(0, history.UndoCount);
        Assert.AreEqual(3, history.RedoCount, "switching context must never clear the redo stack");
    }

    [TestMethod]
    public async Task UndoingAQuickWindowMutationRefreshesItsOwnContext()
    {
        var definition = Definition();
        var context = CreateContext(definition, out var state);
        var history = new EditorHistoryService();
        history.Push(new QuickWindowContentChangedAction(
            DefinitionKey,
            definition.EffectiveContent,
            definition.EffectiveContent with { CanvasSize = new CanvasSize(640, 480) },
            "resize quick window canvas"));

        await history.UndoAsync(context);

        Assert.AreEqual(1, state.RefreshedTargets.Count);
        Assert.AreEqual(EditorHistoryScope.QuickWindow, state.RefreshedTargets[0].Scope);
        Assert.AreEqual(DefinitionKey, state.RefreshedTargets[0].QuickWindowDefinitionKey);
    }

    [TestMethod]
    public async Task AQuickWindowActionIsNotAppliedByAContextThatCannotResolveIt()
    {
        var history = new EditorHistoryService();
        var definition = Definition();
        history.Push(new QuickWindowContentChangedAction(
            DefinitionKey,
            definition.EffectiveContent,
            definition.EffectiveContent with { CanvasSize = new CanvasSize(640, 480) },
            "resize quick window canvas"));
        var pageOnlyContext = new EditorHistoryContext
        {
            ActiveSceneId = "win00003",
            GetActiveScene = () => null,
            ReplaceActiveScene = _ => { },
            MarkDirty = () => { },
            RefreshPreviewAsync = () => Task.CompletedTask,
            SetStatus = _ => { }
        };

        Assert.IsFalse(await history.UndoAsync(pageOnlyContext));
        Assert.AreEqual(1, history.UndoCount, "an unresolvable action stays on the stack instead of being lost");
    }

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
    public void MainWindowXamlExposesTheQuickWindowGroupAndItsCommands()
    {
        var xaml = ReadAppFile("MainWindow.xaml");

        StringAssert.Contains(xaml, "x:Name=\"QuickWindowsAnchorable\"");
        StringAssert.Contains(xaml, "Title=\"Fenêtres rapides\"");
        StringAssert.Contains(xaml, "x:Name=\"QuickWindowsListBox\"");
        StringAssert.Contains(xaml, "SelectionChanged=\"OnQuickWindowSelectionChanged\"");
        StringAssert.Contains(xaml, "MouseDoubleClick=\"OnQuickWindowsListMouseDoubleClick\"");

        foreach (var command in new[]
                 {
                     "quick-window.new", "quick-window.open", "quick-window.rename",
                     "quick-window.duplicate", "quick-window.delete"
                 })
        {
            StringAssert.Contains(xaml, $"Tag=\"{command}\"");
        }

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

    private sealed class HistoryState
    {
        public QuickWindowDefinition Definition { get; set; } = null!;
        public List<EditorHistoryTarget> RefreshedTargets { get; } = [];
    }

    private static EditorHistoryContext CreateContext(QuickWindowDefinition definition, out HistoryState state)
    {
        var captured = new HistoryState { Definition = definition };
        state = captured;
        return new EditorHistoryContext
        {
            ActiveSceneId = "win00003",
            GetActiveScene = () => null,
            ReplaceActiveScene = _ => { },
            GetQuickWindowDefinition = key => key == captured.Definition.DefinitionKey ? captured.Definition : null,
            ReplaceQuickWindowDefinition = updated => captured.Definition = updated,
            MarkDirty = () => { },
            RefreshPreviewAsync = () => Task.CompletedTask,
            RefreshTargetAsync = target =>
            {
                captured.RefreshedTargets.Add(target);
                return Task.CompletedTask;
            },
            SetStatus = _ => { }
        };
    }

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
