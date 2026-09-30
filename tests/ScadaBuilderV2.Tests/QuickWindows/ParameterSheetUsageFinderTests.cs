using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Locks the parameter-sheet usage finder: every `Ouvrir une fenêtre rapide` command whose
/// `ParameterSheetKey` matches is returned, including one nested inside a child element, in durable page
/// order, and an unrelated sheet key returns nothing (fix round 1, DEC-0052).
/// </summary>
/// <remarks>
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class ParameterSheetUsageFinderTests
{
    [TestMethod]
    public void FindReturnsEveryUsageIncludingOneNestedInAChildElementInPageOrder()
    {
        var sheetKey = Guid.NewGuid();
        var otherSheetKey = Guid.NewGuid();

        var child = ScadaElement.CreateButton("child", "Enfant", 0, 0, ScadaButtonKind.Command) with
        {
            CommandConfig = new ScadaElementCommandConfig([OpenQuickWindowCommand("open-child", "Ouvrir enfant", sheetKey)])
        };
        var container = ScadaElement.CreateButton("container", "Conteneur", 0, 0, ScadaButtonKind.Command) with
        {
            CommandConfig = new ScadaElementCommandConfig([OpenQuickWindowCommand("open-other", "Autre", otherSheetKey)]),
            Children = [child]
        };
        var top = ScadaElement.CreateButton("top", "Haut", 0, 0, ScadaButtonKind.Command) with
        {
            CommandConfig = new ScadaElementCommandConfig([OpenQuickWindowCommand("open-top", "Ouvrir", sheetKey)])
        };
        var page = Page("win00001", top, container);
        var snapshot = Snapshot(page);

        var usages = ParameterSheetUsageFinder.Find(snapshot, sheetKey);

        Assert.AreEqual(2, usages.Count);
        Assert.AreEqual("win00001", usages[0].PageCode);
        Assert.AreEqual("top", usages[0].ElementId);
        Assert.AreEqual("open-top", usages[0].CommandId);
        Assert.AreEqual("Ouvrir", usages[0].CommandName);
        Assert.AreEqual("child", usages[1].ElementId);
        Assert.AreEqual("open-child", usages[1].CommandId);
    }

    [TestMethod]
    public void FindReturnsNothingForAnUnrelatedSheetKey()
    {
        var sheetKey = Guid.NewGuid();
        var element = ScadaElement.CreateButton("e1", "E1", 0, 0, ScadaButtonKind.Command) with
        {
            CommandConfig = new ScadaElementCommandConfig([OpenQuickWindowCommand("open", "Ouvrir", Guid.NewGuid())])
        };
        var page = Page("win00002", element);
        var snapshot = Snapshot(page);

        var usages = ParameterSheetUsageFinder.Find(snapshot, sheetKey);

        Assert.AreEqual(0, usages.Count);
    }

    private static ScadaCommandBinding OpenQuickWindowCommand(string id, string name, Guid sheetKey) => new(
        id, name, true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow, ParameterSheetKey: sheetKey);

    private static ScadaScene Page(string code, params ScadaElement[] elements)
    {
        var page = ScadaScene.CreateEmpty(code, code, CanvasSize.DefaultDesktop) with
        {
            PageKey = Guid.NewGuid(),
            PageCode = code
        };
        return elements.Aggregate(page, (current, element) => current.WithElement(element));
    }

    private static PageWorkspaceSnapshot Snapshot(params ScadaScene[] pages)
    {
        var references = pages.Select(page => new ScadaSceneReference(
            page.Id,
            page.Title,
            $"scenes/{page.PageKey:N}.scene.json",
            PageKey: page.PageKey,
            PageCode: page.EffectivePageCode)).ToArray();
        var project = ScadaProject.CreateDefault("ParameterSheetUsageFinderTests") with
        {
            ManifestVersion = "2.3",
            Scenes = references
        };
        return new PageWorkspaceSnapshot(1, project, pages.ToDictionary(page => page.PageKey), []);
    }
}
