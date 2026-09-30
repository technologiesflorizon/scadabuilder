using System.Collections.Specialized;
using System.ComponentModel;
using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// `Banc d'essai` sheet selection: <see cref="QuickWindowTestBenchViewModel.LoadPage"/> must be idempotent
/// and a programmatic resync must never request a preview — only a genuine operator pick may
/// (controller fix round 2, DEC-0052).
/// </summary>
/// <remarks>
/// Regression: <c>OnPageCommandCompleted</c> now calls the refresh that reaches <c>LoadPage</c> on every
/// page-property command (fix round 1), and the `SheetSelectorComboBox`'s `SelectedValue` binding (also
/// fix round 1) turns any resulting <c>Sheets</c>/`SelectedSheetKey` churn into a WPF `SelectionChanged`
/// that the control used to forward unconditionally, re-running the full lowering and preview reload on
/// every unrelated page command. <see cref="QuickWindowTestBenchViewModel.SheetSelected"/> is the
/// WPF-free, directly testable signal this is fixed against.
///
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class QuickWindowTestBenchSheetSelectionTests
{
    [TestMethod]
    public void LoadPageCalledTwiceWithTheSamePageAndSheetsChangesNothing()
    {
        var bench = new QuickWindowTestBenchViewModel();
        var (pageA, sheetX, sheetY) = Fixture();
        bench.LoadPage(pageA, [sheetX, sheetY]);
        bench.SelectSheet(sheetX.SheetKey);

        var collectionChanges = 0;
        var propertyChanges = 0;
        bench.Sheets.CollectionChanged += (_, _) => collectionChanges++;
        bench.PropertyChanged += (_, _) => propertyChanges++;

        bench.LoadPage(pageA, [sheetX, sheetY]);

        Assert.AreEqual(0, collectionChanges, "an unchanged (page, sheets) reload must not touch the Sheets collection");
        Assert.AreEqual(0, propertyChanges, "an unchanged (page, sheets) reload must raise no PropertyChanged at all");
        Assert.AreEqual(sheetX.SheetKey, bench.SelectedSheetKey, "the existing selection must survive untouched");
    }

    [TestMethod]
    public void AProgrammaticResyncAcrossARealPageChangeNeverRaisesThePreviewRequest()
    {
        var bench = new QuickWindowTestBenchViewModel();
        var (pageA, sheetX, sheetY) = Fixture();
        var pageB = Page("popup2", "popup2", Guid.Parse("7b000000-0000-4000-8000-0000000000b2"));

        var raised = new List<Guid>();
        bench.SheetSelected += (_, sheetKey) => raised.Add(sheetKey);

        bench.LoadPage(pageA, [sheetX, sheetY]);
        bench.SelectSheet(sheetX.SheetKey);
        Assert.AreEqual(1, raised.Count, "the genuine pick that sets up the scenario must itself raise once");

        // A real page switch (different PageKey) that still offers the same sheets: the previously valid
        // selection is carried over by LoadPage itself, never through SelectSheet, so it must not preview.
        bench.LoadPage(pageB, [sheetX, sheetY]);

        Assert.AreEqual(1, raised.Count, "restoring a still-valid selection across a page reload must never raise the preview request");
        Assert.AreEqual(sheetX.SheetKey, bench.SelectedSheetKey);
    }

    [TestMethod]
    public void AGenuineSelectionOfADifferentSheetRaisesThePreviewRequestExactlyOnce()
    {
        var bench = new QuickWindowTestBenchViewModel();
        var (pageA, sheetX, sheetY) = Fixture();
        bench.LoadPage(pageA, [sheetX, sheetY]);

        var raised = new List<Guid>();
        bench.SheetSelected += (_, sheetKey) => raised.Add(sheetKey);

        bench.SelectSheet(sheetX.SheetKey);
        bench.SelectSheet(sheetY.SheetKey);

        CollectionAssert.AreEqual(new[] { sheetX.SheetKey, sheetY.SheetKey }, raised, "each genuinely different pick raises exactly once, in order");
    }

    [TestMethod]
    public void ReselectingTheSameSheetNeverRaisesASecondPreviewRequest()
    {
        var bench = new QuickWindowTestBenchViewModel();
        var (pageA, sheetX, sheetY) = Fixture();
        bench.LoadPage(pageA, [sheetX, sheetY]);

        var raised = 0;
        bench.SheetSelected += (_, _) => raised++;

        bench.SelectSheet(sheetX.SheetKey);
        bench.SelectSheet(sheetX.SheetKey);

        Assert.AreEqual(1, raised, "the WPF control forwards SelectionChanged unconditionally; the no-op guard lives in SelectSheet itself");
    }

    private static (ScadaScene PageA, ParameterSheet SheetX, ParameterSheet SheetY) Fixture()
    {
        var pageA = Page("popup", "popup", Guid.Parse("7b000000-0000-4000-8000-0000000000a1"));
        var sheetX = new ParameterSheet(Guid.Parse("7b000000-0000-4000-8000-0000000000f1"), "fan_03", "Ventilateur 3", "#1 := \"Ventilateur 3\"");
        var sheetY = new ParameterSheet(Guid.Parse("7b000000-0000-4000-8000-0000000000f2"), "fan_05", "Ventilateur 5", "#1 := \"Ventilateur 5\"");
        return (pageA, sheetX, sheetY);
    }

    private static ScadaScene Page(string code, string title, Guid pageKey) =>
        ScadaScene.CreateEmpty(code, title, new CanvasSize(200, 120)) with
        {
            PageKey = pageKey,
            PageCode = code,
            PageType = ScadaPageType.QuickWindow
        };
}
