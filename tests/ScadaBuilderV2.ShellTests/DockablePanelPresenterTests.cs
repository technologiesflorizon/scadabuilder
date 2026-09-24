using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;
using ScadaBuilderV2.App.Shell;

namespace ScadaBuilderV2.ShellTests;

/// <summary>
/// Runtime coverage for <see cref="DockablePanelPresenter"/> against the real AvalonDock 4.74.1 assembly.
/// </summary>
/// <remarks>
/// This is the suite that <c>tests/ScadaBuilderV2.Tests/DockablePanelReattachmentTests.cs</c> could not be: that
/// project targets plain <c>net8.0</c> and cannot reference WPF or AvalonDock, so its predecessor pinned the
/// *shape* of the shipped helper's source (it mentioned <c>AddToLayout</c>, it branched on <c>Parent</c>) and
/// passed while the helper did not work. Every test here builds a real <see cref="DockingManager"/>, exercises
/// <see cref="DockablePanelPresenter"/> against it, and asserts on <c>manager.Layout.Descendents()</c> - the
/// live tree - never on <see cref="LayoutAnchorable.Parent"/>, which stays non-null on a detached anchorable and
/// so cannot distinguish "attached and visible" from "orphaned in a dead tree" (see the probe referenced in
/// <see cref="DockablePanelPresenter"/>'s remarks).
///
/// AvalonDock requires an STA thread; each test builds and drives its scene on a dedicated
/// <see cref="Thread"/> with <see cref="ApartmentState.STA"/> and joins it, then asserts on the thread's
/// result back on the test-runner thread, per the brief for this suite.
///
/// Tests: this file. Surface: <c>src/ScadaBuilderV2.App/Shell/DockablePanelPresenter.cs</c>.
/// </remarks>
[TestClass]
public sealed class DockablePanelPresenterTests
{
    private const string KeptId = "Kept";
    private const string DroppedId = "Dropped";

    [TestMethod]
    public void ShowMakesAnAnchorableOmittedByTheRestoredLayoutAppearInTheLiveTreeWithItsContentIntact()
    {
        var result = RunOnSta(() =>
        {
            var (manager, kept, dropped) = BuildScene();
            RestoreLayoutOmitting(manager, DroppedId);

            // Before the fix under test: `dropped.Parent` is non-null here (it is parented in the dead tree
            // that Deserialize replaced), which is exactly why a `Parent is null` guard never re-attaches it.
            var shown = DockablePanelPresenter.Show(manager, dropped);

            return new SceneOutcome(
                ShownIsNotNull: shown is not null,
                ShownInLiveTree: shown is not null && IsInLiveTree(manager, shown),
                ShownContentText: (shown?.Content as TextBlock)?.Text,
                LiveCountWithDroppedId: CountLiveWithContentId(manager, DroppedId));
        });

        Assert.IsTrue(result.ShownIsNotNull, "Show returned null for an anchorable omitted by the restored layout.");
        Assert.IsTrue(result.ShownInLiveTree, "the anchorable opened by Show() is not in the live AvalonDock tree (manager.Layout.Descendents()).");
        Assert.AreEqual("dropped", result.ShownContentText, "the reopened anchorable lost its Content.");
        Assert.AreEqual(1, result.LiveCountWithDroppedId, "the live tree has an unexpected number of anchorables for the reopened panel.");
    }

    [TestMethod]
    public void ShowActsOnTheLiveAnchorableWhenTheRestoredLayoutAlreadyContainsIt()
    {
        var result = RunOnSta(() =>
        {
            var (manager, kept, dropped) = BuildScene();
            RestoreLayoutOmitting(manager, DroppedId); // Kept survives the restore; Dropped does not.

            var liveBeforeShow = manager.Layout!.Descendents().OfType<LayoutAnchorable>()
                .First(a => a.ContentId == KeptId);

            var shown = DockablePanelPresenter.Show(manager, kept);

            return new SceneOutcome(
                ShownIsNotNull: shown is not null,
                ShownInLiveTree: shown is not null && IsInLiveTree(manager, shown),
                ShownContentText: (shown?.Content as TextBlock)?.Text,
                LiveCountWithDroppedId: CountLiveWithContentId(manager, KeptId),
                ShownIsTheLiveObject: ReferenceEquals(shown, liveBeforeShow),
                ShownIsTheDeclaredObject: ReferenceEquals(shown, kept));
        });

        Assert.IsTrue(result.ShownIsNotNull);
        Assert.IsTrue(result.ShownInLiveTree, "Show() must act on the anchorable actually present in the live tree.");
        Assert.IsTrue(result.ShownIsTheLiveObject, "Show() must return/act on the live object resolved by ContentId, not the stale x:Name-declared one.");
        Assert.IsFalse(result.ShownIsTheDeclaredObject, "the declared (dead-tree) object must not be the one shown when a live one already exists.");
        Assert.AreEqual("kept", result.ShownContentText, "the live anchorable's Content changed unexpectedly.");
        Assert.AreEqual(1, result.LiveCountWithDroppedId, "the live tree has an unexpected number of anchorables for 'Kept'.");
    }

    [TestMethod]
    public void AnAnchorableClosedByTheUserComesBackWithItsContentWhenReopenedThroughShow()
    {
        var result = RunOnSta(() =>
        {
            var (manager, kept, dropped) = BuildScene();
            // Restore first, as a real session always does at startup (dock-layout.xml is read and
            // deserialized even when it omits nothing): this is what makes the live 'Kept' a different
            // object from the x:Name-declared 'kept' field, exactly the condition the probe validated for
            // this scenario. "None" is not a real ContentId, so nothing is actually stripped.
            RestoreLayoutOmitting(manager, "None");
            var live = manager.Layout!.Descendents().OfType<LayoutAnchorable>().First(a => a.ContentId == KeptId);
            live.Close();

            var stillInLiveTreeAfterClose = IsInLiveTree(manager, live);

            var shown = DockablePanelPresenter.Show(manager, kept);

            return new SceneOutcome(
                ShownIsNotNull: shown is not null,
                ShownInLiveTree: shown is not null && IsInLiveTree(manager, shown),
                ShownContentText: (shown?.Content as TextBlock)?.Text,
                LiveCountWithDroppedId: CountLiveWithContentId(manager, KeptId),
                ClosedStillInLiveTree: stillInLiveTreeAfterClose);
        });

        Assert.IsFalse(result.ClosedStillInLiveTree, "sanity check: Close() should have removed the anchorable from the live tree (this is the bug's second manifestation, not the fix).");
        Assert.IsTrue(result.ShownIsNotNull);
        Assert.IsTrue(result.ShownInLiveTree, "a closed-then-reopened anchorable must be back in the live AvalonDock tree.");
        Assert.AreEqual("kept", result.ShownContentText, "the reopened anchorable lost its Content after Close()+Show().");
        Assert.AreEqual(1, result.LiveCountWithDroppedId, "Close()+Show() must not leave duplicate or orphaned anchorables for the same ContentId.");
    }

    [TestMethod]
    public void CallingShowTwiceInARowDoesNotThrowAndDoesNotDuplicateTheAnchorable()
    {
        var result = RunOnSta(() =>
        {
            var (manager, kept, dropped) = BuildScene();
            RestoreLayoutOmitting(manager, DroppedId);

            DockablePanelPresenter.Show(manager, dropped);
            var secondShown = DockablePanelPresenter.Show(manager, dropped); // must not throw

            return new SceneOutcome(
                ShownIsNotNull: secondShown is not null,
                ShownInLiveTree: secondShown is not null && IsInLiveTree(manager, secondShown),
                ShownContentText: (secondShown?.Content as TextBlock)?.Text,
                LiveCountWithDroppedId: CountLiveWithContentId(manager, DroppedId));
        });

        Assert.IsTrue(result.ShownIsNotNull, "the second Show() call threw or returned null.");
        Assert.IsTrue(result.ShownInLiveTree);
        Assert.AreEqual("dropped", result.ShownContentText);
        Assert.AreEqual(1, result.LiveCountWithDroppedId, "calling Show() twice must not duplicate the anchorable in the live tree.");
    }

    [TestMethod]
    public void HideRemovesTheAnchorableFromViewAndIsANoOpWhenItIsAbsentFromTheLiveTree()
    {
        var result = RunOnSta(() =>
        {
            var (manager, kept, dropped) = BuildScene();

            DockablePanelPresenter.Hide(manager, kept);
            var hiddenIsVisible = manager.Layout!.Descendents().OfType<LayoutAnchorable>()
                .FirstOrDefault(a => a.ContentId == KeptId)?.IsVisible;

            // 'dropped' was never restored and never shown, so it has no live counterpart: Hide must not throw.
            RestoreLayoutOmitting(manager, DroppedId);
            DockablePanelPresenter.Hide(manager, dropped);

            return new SceneOutcome(
                ShownIsNotNull: true, // not exercised by this scenario; keep the throw-detection above meaningful
                ShownInLiveTree: false,
                ShownContentText: null,
                LiveCountWithDroppedId: 0,
                HiddenIsVisible: hiddenIsVisible);
        });

        Assert.IsFalse(result.HiddenIsVisible ?? true, "Hide() must take the panel out of view (IsVisible must become false).");
    }

    /// <summary>Builds a scene shaped like <c>MainWindow.xaml</c>: anchorables created up front, held by field, in a pane inside a DockingManager shown in a window.</summary>
    private static (DockingManager Manager, LayoutAnchorable Kept, LayoutAnchorable Dropped) BuildScene()
    {
        var kept = new LayoutAnchorable { Title = "Kept", ContentId = KeptId, CanClose = true, Content = new TextBlock { Text = "kept" } };
        var dropped = new LayoutAnchorable { Title = "Dropped", ContentId = DroppedId, CanClose = true, Content = new TextBlock { Text = "dropped" } };

        var pane = new LayoutAnchorablePane();
        pane.Children.Add(kept);
        pane.Children.Add(dropped);

        var documentPane = new LayoutDocumentPane();
        var rootPanel = new LayoutPanel();
        rootPanel.Children.Add(pane);
        rootPanel.Children.Add(documentPane);
        var root = new LayoutRoot { RootPanel = rootPanel };

        var manager = new DockingManager { Layout = root };
        var window = new Window { Content = manager, Width = 900, Height = 600, ShowInTaskbar = false };
        window.Show();

        return (manager, kept, dropped);
    }

    /// <summary>
    /// Serializes the manager's current layout, removes the anchorable with <paramref name="omittedContentId"/>
    /// from the XML, then deserializes it back with no <see cref="LayoutSerializationCallback"/> - exactly what
    /// <c>MainWindow.DeserializeLayout</c> does. This replaces <c>manager.Layout</c> wholesale, which is what
    /// orphans the <c>x:Name</c>-declared anchorable objects into a dead tree even when their <c>ContentId</c>
    /// survives the restore.
    /// </summary>
    private static void RestoreLayoutOmitting(DockingManager manager, string omittedContentId)
    {
        string xml;
        using (var writer = new System.IO.StringWriter())
        {
            new XmlLayoutSerializer(manager).Serialize(writer);
            xml = writer.ToString();
        }

        var doc = System.Xml.Linq.XDocument.Parse(xml);
        foreach (var element in doc.Descendants("LayoutAnchorable")
                     .Where(e => (string?)e.Attribute("ContentId") == omittedContentId)
                     .ToArray())
        {
            element.Remove();
        }

        using var reader = new System.IO.StringReader(doc.ToString());
        new XmlLayoutSerializer(manager).Deserialize(reader);
    }

    private static bool IsInLiveTree(DockingManager manager, LayoutAnchorable anchorable) =>
        manager.Layout!.Descendents().OfType<LayoutAnchorable>().Any(a => ReferenceEquals(a, anchorable));

    private static int CountLiveWithContentId(DockingManager manager, string contentId) =>
        manager.Layout!.Descendents().OfType<LayoutAnchorable>().Count(a => a.ContentId == contentId);

    /// <summary>Runs <paramref name="scenario"/> to completion on a dedicated STA thread, as AvalonDock requires, and returns its result.</summary>
    private static SceneOutcome RunOnSta(Func<SceneOutcome> scenario)
    {
        SceneOutcome? result = null;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                // Application is a process-wide singleton (Application.Current); MSTest runs every test in
                // this file in the same process, so only the first test's STA thread may construct it.
                // Fully qualified: this project also references ScadaBuilderV2.App, which pulls in the
                // sibling namespace ScadaBuilderV2.Application - an unqualified "Application" here binds to
                // that namespace instead of System.Windows.Application.
                if (System.Windows.Application.Current is null)
                {
                    _ = new System.Windows.Application();
                }

                result = scenario();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new AssertFailedException($"Scenario threw on the STA thread: {failure}", failure);
        }

        Assert.IsNotNull(result, "scenario did not produce a result.");
        return result!.Value;
    }

    private readonly record struct SceneOutcome(
        bool ShownIsNotNull,
        bool ShownInLiveTree,
        string? ShownContentText,
        int LiveCountWithDroppedId,
        bool ShownIsTheLiveObject = false,
        bool ShownIsTheDeclaredObject = false,
        bool ClosedStillInLiveTree = false,
        bool? HiddenIsVisible = null);
}
