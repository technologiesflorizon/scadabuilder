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
    public void EverySheetContributesWhetherLinkedToThisPageToAnotherPageOrToNoPageAtAll()
    {
        // "Toutes les feuilles comptent": no filtering by `Ouvrir une fenêtre rapide` command.
        var pageKey = Guid.NewGuid();
        var unlinked = NamedSheet("A_libre", "#1 := {FAN_03_RUN}");
        var linkedHere = NamedSheet("B_ici", "#2 := {FAN_03_START}");
        var linkedElsewhere = NamedSheet("C_ailleurs", "#3 := \"autre fenetre\"");
        var elsewherePage = Page(
            "win00001",
            OpenCommand(linkedHere.SheetKey, pageKey, elementId: "open-a", commandId: "cmd-a"),
            OpenCommand(linkedElsewhere.SheetKey, Guid.NewGuid(), elementId: "open-b", commandId: "cmd-b"));

        var catalog = BuildCatalog(Project([unlinked, linkedHere, linkedElsewhere], CommandCatalog()), null, pageKey, elsewherePage);

        CollectionAssert.AreEqual(new[] { "#1", "#2", "#3" }, catalog.Tags.Select(tag => tag.Id).ToArray());
    }

    [TestMethod]
    public void ASheetLinkedOnlyToAnotherPageStillContributes()
    {
        var pageKey = Guid.NewGuid();
        var theirs = NamedSheet("Evaporateur_1", "#4 := {FAN_03_RUN}");
        var callerPage = Page("win00001", OpenCommand(theirs.SheetKey, Guid.NewGuid()));

        var catalog = BuildCatalog(Project([theirs], CommandCatalog()), null, pageKey, callerPage);

        Assert.AreEqual("Paramètre — ex. : {FAN_03_RUN}", catalog.Tags.Single(t => t.Id == "#4").Device);
    }

    [TestMethod]
    public void APageReferencedNumberAbsentFromEverySheetIsKeptAndMarkedUndefined()
    {
        var pageKey = Guid.NewGuid();
        var sheet = Sheet("#1 := {FAN_03_RUN}");
        var quickWindowPage = PageWithTextUsage(pageKey, "#7");

        var catalog = BuildCatalog(Project([sheet], CommandCatalog()), quickWindowPage, pageKey);

        CollectionAssert.AreEqual(new[] { "#1", "#7" }, catalog.Tags.Select(tag => tag.Id).ToArray());
        var undefined = catalog.Tags.Single(tag => tag.Id == "#7");
        // DisplayName MUST equal Id exactly - the descriptive text lives in Device instead.
        Assert.AreEqual("#7", undefined.DisplayName);
        Assert.AreEqual("Paramètre — (non défini dans les feuilles)", undefined.Device);
        // An undefined #N must stay offered to the write-tag selector until the sheet is fixed.
        Assert.IsTrue(undefined.Writeable);
    }

    [TestMethod]
    public void TheCaptionListsOneTwoOrThreeDistinctSourcesWithoutSheetNames()
    {
        var pageKey = Guid.NewGuid();
        var sheets = Enumerable.Range(1, 3).Select(i => NamedSheet($"Condenseur_{i}", $"#1 := {{T{i}}}")).ToArray();

        Assert.AreEqual("Paramètre — ex. : {T1}", DeviceOf(sheets.Take(1), pageKey));
        Assert.AreEqual("Paramètre — ex. : {T1}, {T2}", DeviceOf(sheets.Take(2), pageKey));
        Assert.AreEqual("Paramètre — ex. : {T1}, {T2}, {T3}", DeviceOf(sheets.Take(3), pageKey));
    }

    [TestMethod]
    public void TheCaptionOfFiveDistinctSourcesShowsThreeAndCountsTheTwoOthers()
    {
        var pageKey = Guid.NewGuid();
        var sheets = Enumerable.Range(1, 5).Select(i => NamedSheet($"Condenseur_{i}", $"#1 := {{T{i}}}")).ToArray();

        Assert.AreEqual("Paramètre — ex. : {T1}, {T2}, {T3}, … (+2)", DeviceOf(sheets, pageKey));
    }

    [TestMethod]
    public void TheCaptionOrderIsDeterministicWhateverTheSheetOrder()
    {
        var pageKey = Guid.NewGuid();
        var sheet1 = NamedSheet("Condenseur_1", "#1 := {FAN_03_RUN}");
        var sheet2 = NamedSheet("condenseur_2", "#1 := {FAN_03_START}");

        Assert.AreEqual("Paramètre — ex. : {FAN_03_RUN}, {FAN_03_START}", DeviceOf([sheet1, sheet2], pageKey));
        Assert.AreEqual("Paramètre — ex. : {FAN_03_RUN}, {FAN_03_START}", DeviceOf([sheet2, sheet1], pageKey));
    }

    [TestMethod]
    public void ADuplicatedSourceAcrossMergedSheetsIsListedOnce()
    {
        var pageKey = Guid.NewGuid();
        var sheets = new[]
        {
            NamedSheet("A", "#1 := {FAN_03_RUN}"),
            NamedSheet("B", "#1 := {FAN_03_RUN}"),
            NamedSheet("C", "#1 := {FAN_03_START}"),
            NamedSheet("D", "#1 := {FAN_03_RUN}")
        };

        Assert.AreEqual("Paramètre — ex. : {FAN_03_RUN}, {FAN_03_START}", DeviceOf(sheets, pageKey));
    }

    [TestMethod]
    public void LiteralAndExpressionSourcesAreRenderedAsTheyAlwaysWere()
    {
        var pageKey = Guid.NewGuid();
        var sheet = NamedSheet("Condenseur_1", "#2 := \"Ventilateur 3\"\n#3 := {FAN_03_SPEED} * 0.1");

        var catalog = BuildCatalog(Project([sheet], CommandCatalog()), null, pageKey);

        Assert.AreEqual("Paramètre — ex. : \"Ventilateur 3\"", catalog.Tags.Single(t => t.Id == "#2").Device);
        Assert.AreEqual("Paramètre — ex. : {FAN_03_SPEED} * 0.1", catalog.Tags.Single(t => t.Id == "#3").Device);
    }

    [TestMethod]
    public void WholeValuesAreDroppedAndCountedWhenTheCaptionWouldExceedEightyCharacters()
    {
        var pageKey = Guid.NewGuid();
        var a = "{" + new string('A', 20) + "}";
        var b = "{" + new string('B', 20) + "}";
        var c = "{" + new string('C', 20) + "}";
        var threeShort = new[] { NamedSheet("1", $"#1 := {a}"), NamedSheet("2", $"#1 := {b}"), NamedSheet("3", $"#1 := {c}") };

        var twoShown = DeviceOf(threeShort, pageKey);

        Assert.AreEqual($"Paramètre — ex. : {a}, {b}, … (+1)", twoShown);
        Assert.IsTrue(twoShown.Length <= 80, twoShown);

        var x = "{" + new string('X', 28) + "}";
        var y = "{" + new string('Y', 28) + "}";
        var z = "{" + new string('Z', 28) + "}";
        var threeLong = new[] { NamedSheet("1", $"#1 := {x}"), NamedSheet("2", $"#1 := {y}"), NamedSheet("3", $"#1 := {z}") };

        var oneShown = DeviceOf(threeLong, pageKey);

        Assert.AreEqual($"Paramètre — ex. : {x}, … (+2)", oneShown);
        Assert.IsTrue(oneShown.Length <= 80, oneShown);
    }

    [TestMethod]
    public void AFirstValueTooLongForTheLimitIsTruncatedWithAnEllipsisKeepingTheCountOfTheOthers()
    {
        var pageKey = Guid.NewGuid();
        var huge = "{" + new string('Z', 100) + "}";

        var alone = DeviceOf([NamedSheet("1", $"#1 := {huge}")], pageKey);

        Assert.AreEqual(80, alone.Length, alone);
        Assert.AreEqual("Paramètre — ex. : " + huge[..61] + "…", alone);

        var withOther = DeviceOf([NamedSheet("1", $"#1 := {huge}"), NamedSheet("2", "#1 := {OTHER}")], pageKey);

        Assert.AreEqual(80, withOther.Length, withOther);
        Assert.AreEqual("Paramètre — ex. : " + huge[..53] + "…, … (+1)", withOther);
    }

    [TestMethod]
    public void EveryDisplayNameEqualsItsIdForDefinedMultiSourceLiteralExpressionAndUndefinedEntries()
    {
        // DisplayName is an identifier-bearing field throughout the app (ElementStateRuleDialog,
        // ExpressionCreationDialog, ScadaExpressionValidator.TryResolveTagReference), never a display-only one.
        var pageKey = Guid.NewGuid();
        var sheetA = Sheet("#1 := {FAN_03_RUN}\n#2 := \"Ventilateur 3\"\n#3 := {FAN_03_SPEED} * 0.1\n#5 := {FAN_03_SPEED}");
        var sheetB = Sheet("#5 := {FAN_03_START}");
        var project = Project([sheetA, sheetB], CommandCatalog());

        var catalog = BuildCatalog(project, PageWithTextUsage(pageKey, "#7"), pageKey);

        CollectionAssert.AreEqual(new[] { "#1", "#2", "#3", "#5", "#7" }, catalog.Tags.Select(t => t.Id).ToArray());
        foreach (var tag in catalog.Tags)
        {
            Assert.AreEqual(tag.Id, tag.DisplayName, $"DisplayName must equal Id for {tag.Id}");
        }
    }

    [TestMethod]
    public void TheOneHundredEntryDoesNotSortBeforeTheTenthOrTheFirstAsItDidWithTheStaticCatalog()
    {
        var pageKey = Guid.NewGuid();
        var sheet = Sheet("#1 := 1\n#10 := 1\n#100 := 1");
        var project = Project([sheet], CommandCatalog());
        var catalog = BuildCatalog(project, null, pageKey);

        CollectionAssert.AreEqual(new[] { "#1", "#10", "#100" }, catalog.Tags.Select(tag => tag.Id).ToArray());
    }

    [TestMethod]
    public void WriteableAndDatatypeAreCopiedFromTheResolvedProjectTagWhenTheEntryIsASingleTagBinding()
    {
        var pageKey = Guid.NewGuid();
        var sheet = Sheet("#1 := {FAN_03_START}");
        var project = Project([sheet], CommandCatalog());
        var catalog = BuildCatalog(project, null, pageKey);

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
        var catalog = BuildCatalog(project, null, pageKey);

        Assert.IsFalse(catalog.Tags.Single(t => t.Id == "#1").Writeable);
    }

    [TestMethod]
    public void WriteableIsTrueWhenEveryTagContributionAcrossDifferentSheetsResolvesToAWritableTag()
    {
        // Ruling (fix round 1): two sheets binding the same #1 to two different writable project tags
        // (FAN_03_SP / FAN_04_SP) must still report Writeable: true.
        var pageKey = Guid.NewGuid();
        var sheetA = Sheet("#1 := {FAN_03_SP}");
        var sheetB = Sheet("#1 := {FAN_04_SP}");
        var project = Project([sheetA, sheetB], CommandCatalog());
        var catalog = BuildCatalog(project, null, pageKey);

        var tag = catalog.Tags.Single(t => t.Id == "#1");
        Assert.IsTrue(tag.Writeable);
        Assert.AreEqual("Decimal", tag.Datatype, "both contributions agree on Decimal");
    }

    [TestMethod]
    public void WriteableIsFalseWhenOneTagContributionResolvesToANonWritableTag()
    {
        var pageKey = Guid.NewGuid();
        var sheetA = Sheet("#1 := {FAN_03_SP}"); // writable
        var sheetB = Sheet("#1 := {FAN_05_SP}"); // not writable
        var project = Project([sheetA, sheetB], CommandCatalog());
        var catalog = BuildCatalog(project, null, pageKey);

        Assert.IsFalse(catalog.Tags.Single(t => t.Id == "#1").Writeable);
    }

    [TestMethod]
    public void IsPlaceholderCatalogIsTrueOnlyWhenEveryTagIsShapedLikeAHashNumber()
    {
        var placeholderTags = new[]
        {
            new ScadaTagDefinition("#1", "#1 — {A}"),
            new ScadaTagDefinition("#10", "#10 — {B}"),
            new ScadaTagDefinition("#100", "#100 — {C}")
        };
        Assert.IsTrue(QuickWindowPagePlaceholderCatalog.IsPlaceholderCatalog(placeholderTags));

        var normalTags = new[]
        {
            new ScadaTagDefinition("tf100.mapping.1", "FAN_03_RUN"),
            new ScadaTagDefinition("tf100.mapping.2", "FAN_03_FAULT")
        };
        Assert.IsFalse(QuickWindowPagePlaceholderCatalog.IsPlaceholderCatalog(normalTags));

        var mixedTags = new[] { placeholderTags[0], normalTags[0] };
        Assert.IsFalse(QuickWindowPagePlaceholderCatalog.IsPlaceholderCatalog(mixedTags));

        Assert.IsFalse(QuickWindowPagePlaceholderCatalog.IsPlaceholderCatalog(Array.Empty<ScadaTagDefinition>()));
    }

    [TestMethod]
    public void OrderForSelectorKeepsNumericOrderOnAPlaceholderCatalogButSortsANormalCatalogByDisplayName()
    {
        var placeholderTags = new[]
        {
            new ScadaTagDefinition("#1", "#1 — {A}"),
            new ScadaTagDefinition("#10", "#10 — {B}"),
            new ScadaTagDefinition("#2", "#2 — {C}")
        };
        var orderedPlaceholders = QuickWindowPagePlaceholderCatalog.OrderForSelector(placeholderTags);
        // Passed through unchanged: re-sorting by DisplayName would scramble #1, #10, #2 lexically.
        CollectionAssert.AreEqual(placeholderTags, orderedPlaceholders.ToArray());

        var normalTags = new[]
        {
            new ScadaTagDefinition("tf100.mapping.1", "Ventilateur 3"),
            new ScadaTagDefinition("tf100.mapping.2", "Compresseur 1")
        };
        var orderedNormal = QuickWindowPagePlaceholderCatalog.OrderForSelector(normalTags, StringComparer.Ordinal);
        CollectionAssert.AreEqual(
            new[] { "Compresseur 1", "Ventilateur 3" },
            orderedNormal.Select(tag => tag.DisplayName).ToArray());
    }

    // ---- Fixtures --------------------------------------------------------

    private static ParameterSheet Sheet(string text) =>
        new(Guid.NewGuid(), $"sheet-{Guid.NewGuid():N}", "Feuille", text);

    private static ParameterSheet NamedSheet(string displayName, string text) =>
        new(Guid.NewGuid(), $"sheet-{Guid.NewGuid():N}", displayName, text);

    private static ScadaTagCatalog CommandCatalog() => new(
        "quick-window-page-placeholder-catalog-tests",
        [
            new ScadaTagDefinition("tf100.mapping.101", "FAN_03_RUN", Datatype: "Boolean"),
            new ScadaTagDefinition("tf100.mapping.102", "FAN_03_START", Datatype: "Boolean", Writeable: true),
            new ScadaTagDefinition("tf100.mapping.103", "FAN_03_SPEED", Datatype: "Decimal"),
            new ScadaTagDefinition("tf100.mapping.104", "FAN_03_SP", Datatype: "Decimal", Writeable: true),
            new ScadaTagDefinition("tf100.mapping.105", "FAN_04_SP", Datatype: "Decimal", Writeable: true),
            new ScadaTagDefinition("tf100.mapping.106", "FAN_05_SP", Datatype: "Decimal", Writeable: false),
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

    /// <summary>
    /// Builds the catalog of <paramref name="pageKey"/>. <paramref name="otherPages"/> (typically pages carrying
    /// `Ouvrir une fenêtre rapide` commands) are deliberately irrelevant to the catalog: it only needs the project
    /// and the active page scene.
    /// </summary>
    private static ScadaTagCatalog BuildCatalog(
        ScadaProject project,
        ScadaScene? pageScene,
        Guid pageKey,
        params ScadaScene[] otherPages) =>
        QuickWindowPagePlaceholderCatalog.Build(project, pageScene);

    private static string DeviceOf(IEnumerable<ParameterSheet> sheets, Guid pageKey) =>
        BuildCatalog(Project(sheets.ToArray(), CommandCatalog()), null, pageKey).Tags.Single(t => t.Id == "#1").Device!;
}
