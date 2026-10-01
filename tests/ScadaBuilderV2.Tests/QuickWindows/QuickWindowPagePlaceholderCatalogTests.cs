using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Locks <see cref="QuickWindowPagePlaceholderCatalog.Build"/>: the `#N` tag catalog offered to the
/// state/command selectors of a QuickWindow page comes from the parameter sheets linked to it, numerically
/// ordered, instead of the former static `#1`..`#999` list sorted as text (DEC-0052, user-reported bug on
/// `Popup_Fan_Condenseur`).
/// </summary>
/// <remarks>
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5, §6.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class QuickWindowPagePlaceholderCatalogTests
{
    [TestMethod]
    public void LinkedSheetYieldsExactlyItsThreeEntriesInNumericOrder()
    {
        var pageKey = Guid.NewGuid();
        var sheet = Sheet("#1 := {FAN_03_RUN}\n#2 := \"Ventilateur 3\"\n#10 := {FAN_03_SPEED} * 0.1");
        var project = Project([sheet], CommandCatalog());
        var callerPage = Page("win00001", OpenCommand(sheet.SheetKey, pageKey));
        var scenes = Scenes(callerPage);

        var catalog = QuickWindowPagePlaceholderCatalog.Build(project, scenes, pageKey);

        CollectionAssert.AreEqual(new[] { "#1", "#2", "#10" }, catalog.Tags.Select(tag => tag.Id).ToArray());
    }

    [TestMethod]
    public void NoLinkingCommandFallsBackToTheUnionOfEverySheet()
    {
        var pageKey = Guid.NewGuid();
        var sheetA = Sheet("#1 := {FAN_03_RUN}");
        var sheetB = Sheet("#2 := \"Ventilateur 3\"");
        var project = Project([sheetA, sheetB], CommandCatalog());
        var scenes = Scenes(Page("win00001"));

        var catalog = QuickWindowPagePlaceholderCatalog.Build(project, scenes, pageKey);

        CollectionAssert.AreEqual(new[] { "#1", "#2" }, catalog.Tags.Select(tag => tag.Id).ToArray());
    }

    [TestMethod]
    public void TwoLinkedSheetsWithDifferentNumbersYieldTheirUnion()
    {
        var pageKey = Guid.NewGuid();
        var sheetA = Sheet("#1 := {FAN_03_RUN}");
        var sheetB = Sheet("#5 := {FAN_03_SPEED} * 0.1");
        var project = Project([sheetA, sheetB], CommandCatalog());
        var callerPage = Page(
            "win00001",
            OpenCommand(sheetA.SheetKey, pageKey, elementId: "open-a", commandId: "cmd-a"),
            OpenCommand(sheetB.SheetKey, pageKey, elementId: "open-b", commandId: "cmd-b"));
        var scenes = Scenes(callerPage);

        var catalog = QuickWindowPagePlaceholderCatalog.Build(project, scenes, pageKey);

        CollectionAssert.AreEqual(new[] { "#1", "#5" }, catalog.Tags.Select(tag => tag.Id).ToArray());
    }

    [TestMethod]
    public void APageReferencedNumberAbsentFromRetainedSheetsIsKeptAndMarkedUndefined()
    {
        var pageKey = Guid.NewGuid();
        var sheet = Sheet("#1 := {FAN_03_RUN}");
        var project = Project([sheet], CommandCatalog());
        var callerPage = Page("win00001", OpenCommand(sheet.SheetKey, pageKey));
        var quickWindowPage = PageWithTextUsage(pageKey, "#7");
        var scenes = Scenes(callerPage, quickWindowPage);

        var catalog = QuickWindowPagePlaceholderCatalog.Build(project, scenes, pageKey);

        CollectionAssert.AreEqual(new[] { "#1", "#7" }, catalog.Tags.Select(tag => tag.Id).ToArray());
        var undefined = catalog.Tags.Single(tag => tag.Id == "#7");
        StringAssert.Contains(undefined.DisplayName, "non défini");
        Assert.IsFalse(undefined.Writeable);
    }

    [TestMethod]
    public void AnUnlinkedSheetContributesNothingOnceAtLeastOneSheetIsLinked()
    {
        var pageKey = Guid.NewGuid();
        var linked = Sheet("#1 := {FAN_03_RUN}");
        var unlinked = Sheet("#99 := \"jamais liee\"");
        var project = Project([linked, unlinked], CommandCatalog());
        var callerPage = Page("win00001", OpenCommand(linked.SheetKey, pageKey));
        var scenes = Scenes(callerPage);

        var catalog = QuickWindowPagePlaceholderCatalog.Build(project, scenes, pageKey);

        CollectionAssert.AreEqual(new[] { "#1" }, catalog.Tags.Select(tag => tag.Id).ToArray());
    }

    [TestMethod]
    public void TheDisplayedLabelCarriesTheSheetSource()
    {
        var pageKey = Guid.NewGuid();
        var sheet = Sheet("#1 := {FAN_03_RUN}");
        var project = Project([sheet], CommandCatalog());
        var callerPage = Page("win00001", OpenCommand(sheet.SheetKey, pageKey));
        var scenes = Scenes(callerPage);

        var catalog = QuickWindowPagePlaceholderCatalog.Build(project, scenes, pageKey);

        var tag = catalog.Tags.Single(t => t.Id == "#1");
        StringAssert.Contains(tag.DisplayName, "FAN_03_RUN");
    }

    [TestMethod]
    public void TheOneHundredEntryDoesNotSortBeforeTheTenthOrTheFirstAsItDidWithTheStaticCatalog()
    {
        var pageKey = Guid.NewGuid();
        var sheet = Sheet("#1 := 1\n#10 := 1\n#100 := 1");
        var project = Project([sheet], CommandCatalog());
        var callerPage = Page("win00001", OpenCommand(sheet.SheetKey, pageKey));
        var scenes = Scenes(callerPage);

        var catalog = QuickWindowPagePlaceholderCatalog.Build(project, scenes, pageKey);

        CollectionAssert.AreEqual(new[] { "#1", "#10", "#100" }, catalog.Tags.Select(tag => tag.Id).ToArray());
    }

    [TestMethod]
    public void WriteableAndDatatypeAreCopiedFromTheResolvedProjectTagWhenTheEntryIsASingleTagBinding()
    {
        var pageKey = Guid.NewGuid();
        var sheet = Sheet("#1 := {FAN_03_START}");
        var project = Project([sheet], CommandCatalog());
        var callerPage = Page("win00001", OpenCommand(sheet.SheetKey, pageKey));
        var scenes = Scenes(callerPage);

        var catalog = QuickWindowPagePlaceholderCatalog.Build(project, scenes, pageKey);

        var tag = catalog.Tags.Single(t => t.Id == "#1");
        Assert.IsTrue(tag.Writeable);
        Assert.AreEqual("Boolean", tag.Datatype);
    }

    [TestMethod]
    public void ALiteralEntryIsNeverWriteable()
    {
        var pageKey = Guid.NewGuid();
        var sheet = Sheet("#1 := \"Ventilateur 3\"");
        var project = Project([sheet], CommandCatalog());
        var callerPage = Page("win00001", OpenCommand(sheet.SheetKey, pageKey));
        var scenes = Scenes(callerPage);

        var catalog = QuickWindowPagePlaceholderCatalog.Build(project, scenes, pageKey);

        Assert.IsFalse(catalog.Tags.Single(t => t.Id == "#1").Writeable);
    }

    // ---- Fixtures --------------------------------------------------------

    private static ParameterSheet Sheet(string text) =>
        new(Guid.NewGuid(), $"sheet-{Guid.NewGuid():N}", "Feuille", text);

    private static ScadaTagCatalog CommandCatalog() => new(
        "quick-window-page-placeholder-catalog-tests",
        [
            new ScadaTagDefinition("tf100.mapping.101", "FAN_03_RUN", Datatype: "Boolean"),
            new ScadaTagDefinition("tf100.mapping.102", "FAN_03_START", Datatype: "Boolean", Writeable: true),
            new ScadaTagDefinition("tf100.mapping.103", "FAN_03_SPEED", Datatype: "Decimal"),
        ]);

    private static ScadaProject Project(IReadOnlyList<ParameterSheet> sheets, ScadaTagCatalog tagCatalog) =>
        ScadaProject.CreateDefault("QuickWindowPagePlaceholderCatalogTests") with
        {
            ManifestVersion = "2.3",
            ParameterSheets = sheets,
            TagCatalog = tagCatalog
        };

    private static ScadaElement OpenCommand(Guid sheetKey, Guid pageKey, string elementId = "open", string commandId = "open-cmd") =>
        ScadaElement.CreateButton(elementId, "Ouvrir", 20, 20) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding(commandId, "Ouvrir", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
                    ParameterSheetKey: sheetKey, QuickWindowPageKey: pageKey)
            ])
        };

    private static ScadaScene Page(string code, params ScadaElement[] elements)
    {
        var page = ScadaScene.CreateEmpty(code, code, CanvasSize.DefaultDesktop) with
        {
            PageKey = Guid.NewGuid(),
            PageCode = code
        };
        return elements.Aggregate(page, (current, element) => current.WithElement(element));
    }

    /// <summary>One QuickWindow-type page whose own content displays <paramref name="token"/> as text.</summary>
    private static ScadaScene PageWithTextUsage(Guid pageKey, string token)
    {
        var textElement = ScadaElement.CreateText("text", token, 0, 0);
        textElement = textElement with { Data = textElement.Data! with { Text = token } };
        var page = ScadaScene.CreateEmpty("win00002", "win00002", CanvasSize.DefaultDesktop) with
        {
            PageKey = pageKey,
            PageCode = "win00002",
            PageType = ScadaPageType.QuickWindow
        };
        return page.WithElement(textElement);
    }

    private static Dictionary<Guid, ScadaScene> Scenes(params ScadaScene[] pages) =>
        pages.ToDictionary(page => page.PageKey);
}
