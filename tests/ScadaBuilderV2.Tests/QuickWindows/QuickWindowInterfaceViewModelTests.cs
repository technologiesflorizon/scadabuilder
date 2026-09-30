using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// `LoadPlaceholders`: the read-only `Interface locale` rows built directly from a quick-window page's
/// `#N` usages, with no definition and no manual member editing (DEC-0052).
/// </summary>
/// <remarks>
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class QuickWindowInterfaceViewModelTests
{
    [TestMethod]
    public void LoadPlaceholdersProducesOneRowPerNumberSortedByNumberWithItsUsageCount()
    {
        var page = Page(
            TextElement("title", "#3"),
            TextElement("title2", "#3"),
            TextElement("name", "#1"));

        var model = new QuickWindowInterfacePanelViewModel();
        model.LoadPlaceholders(page);

        Assert.AreEqual(2, model.PlaceholderRows.Count);
        Assert.AreEqual(1, model.PlaceholderRows[0].Number);
        Assert.AreEqual(1, model.PlaceholderRows[0].UsageCount);
        Assert.AreEqual(3, model.PlaceholderRows[1].Number);
        Assert.AreEqual(2, model.PlaceholderRows[1].UsageCount);
    }

    [TestMethod]
    public void LoadPlaceholdersReportsAnInvalidTokenWithoutTurningItIntoARow()
    {
        var page = Page(TextElement("legacy", "Pompe #0"));

        var model = new QuickWindowInterfacePanelViewModel();
        model.LoadPlaceholders(page);

        Assert.AreEqual(0, model.PlaceholderRows.Count);
        Assert.IsTrue(model.HasInvalidPlaceholderTokens);
        Assert.AreEqual(1, model.InvalidPlaceholderTokens.Count);
        Assert.AreEqual("#0", model.InvalidPlaceholderTokens[0].Raw);
        Assert.AreEqual("legacy", model.InvalidPlaceholderTokens[0].ElementId);
    }

    [TestMethod]
    public void LoadPlaceholdersReplacesThePreviousRowsOnReload()
    {
        var model = new QuickWindowInterfacePanelViewModel();
        model.LoadPlaceholders(Page(TextElement("a", "#5")));
        Assert.AreEqual(1, model.PlaceholderRows.Count);

        model.LoadPlaceholders(Page(TextElement("b", "#2")));

        Assert.AreEqual(1, model.PlaceholderRows.Count);
        Assert.AreEqual(2, model.PlaceholderRows[0].Number);
    }

    private static ScadaScene Page(params ScadaElement[] elements) =>
        ScadaScene.CreateEmpty("popup", "Popup", new CanvasSize(200, 120)) with
        {
            PageType = ScadaPageType.QuickWindow,
            Elements = elements
        };

    private static ScadaElement TextElement(string id, string text)
    {
        var element = ScadaElement.CreateText(id, id, 0, 0);
        return element with { Data = element.Data! with { Text = text } };
    }
}
