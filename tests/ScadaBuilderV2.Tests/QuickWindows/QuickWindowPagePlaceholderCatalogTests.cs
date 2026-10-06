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
        // DisplayName MUST equal Id exactly (fix round 2) - the descriptive text lives in Device instead.
        Assert.AreEqual("#7", undefined.DisplayName);
        StringAssert.Contains(undefined.Device, "non défini");
        // Writeable ruling (fix round 1): an undefined #N must stay offered to the write-tag selector until
        // the sheet is fixed, or an already-authored write command regresses (table write-tag combo filters
        // by Writeable: TableCellNumericInputInspector.cs:39, TableEditCoordinator.cs:187).
        Assert.IsTrue(undefined.Writeable);
    }

    [TestMethod]
    public void LinkingCommandsOnTwoDifferentPagesFromTwoDifferentScenesSourcesBothContributeToTheUnion()
    {
        // Fix round 1 (project-wide "feuilles liées" search): MainWindow.BuildActiveQuickWindowPagePlaceholderCatalog
        // merges a project-wide snapshot's scenes with an in-memory overlay (open tabs, the active scene)
        // before calling Build. Reproduce that merge of two distinct sources here (no WPF host in this test
        // project) and assert the union still carries both sheets' #N entries, not just the overlay's.
        var pageKey = Guid.NewGuid();
        var sheetA = Sheet("#1 := {FAN_03_RUN}");
        var sheetB = Sheet("#5 := {FAN_03_SPEED} * 0.1");
        var project = Project([sheetA, sheetB], CommandCatalog());

        var callerPageFromSnapshot = Page("win00001", OpenCommand(sheetA.SheetKey, pageKey, elementId: "open-a", commandId: "cmd-a"));
        var callerPageFromOverlay = Page("win00003", OpenCommand(sheetB.SheetKey, pageKey, elementId: "open-b", commandId: "cmd-b"));

        var scenes = new Dictionary<Guid, ScadaScene>();
        foreach (var (key, scene) in Scenes(callerPageFromSnapshot)) scenes[key] = scene;
        foreach (var (key, scene) in Scenes(callerPageFromOverlay)) scenes[key] = scene;

        var catalog = QuickWindowPagePlaceholderCatalog.Build(project, scenes, pageKey);

        CollectionAssert.AreEqual(new[] { "#1", "#5" }, catalog.Tags.Select(tag => tag.Id).ToArray());
    }

    [TestMethod]
    public void ALinkedSheetAndAnUnlinkedSheetBothContributeAndTheCaptionNamesBothInOrder()
    {
        // Sheet-named captions (user change request): a sheet created before its command must show up, and
        // the caption must say which sheet each source comes from, in a deterministic order (sheet
        // DisplayName ordinal-ignore-case, then SheetKey) whatever the project's sheet order is.
        var pageKey = Guid.NewGuid();
        var sheet1 = NamedSheet("Condenseur_1", "#1 := {FAN_03_RUN}");
        var sheet2 = NamedSheet("Condenseur_2", "#1 := {FAN_03_START}");
        var callerPage = Page("win00001", OpenCommand(sheet1.SheetKey, pageKey));
        var scenes = Scenes(callerPage);

        foreach (var sheets in new[] { new[] { sheet1, sheet2 }, new[] { sheet2, sheet1 } })
        {
            var catalog = QuickWindowPagePlaceholderCatalog.Build(Project(sheets, CommandCatalog()), scenes, pageKey);

            var tag = catalog.Tags.Single();
            Assert.AreEqual("#1", tag.DisplayName);
            Assert.AreEqual("Paramètre — Condenseur_1 : {FAN_03_RUN} · Condenseur_2 : {FAN_03_START}", tag.Device);
        }
    }

    [TestMethod]
    public void ASheetLinkedOnlyToAnotherPageIsExcluded()
    {
        var pageKey = Guid.NewGuid();
        var otherPageKey = Guid.NewGuid();
        var mine = NamedSheet("Condenseur_1", "#1 := {FAN_03_RUN}");
        var theirs = NamedSheet("Evaporateur_1", "#1 := {FAN_03_START}\n#9 := \"autre fenetre\"");
        var callerPage = Page(
            "win00001",
            OpenCommand(mine.SheetKey, pageKey, elementId: "open-a", commandId: "cmd-a"),
            OpenCommand(theirs.SheetKey, otherPageKey, elementId: "open-b", commandId: "cmd-b"));
        var scenes = Scenes(callerPage);

        var catalog = QuickWindowPagePlaceholderCatalog.Build(Project([mine, theirs], CommandCatalog()), scenes, pageKey);

        var tag = catalog.Tags.Single();
        Assert.AreEqual("#1", tag.Id);
        Assert.AreEqual("Paramètre — Condenseur_1 : {FAN_03_RUN}", tag.Device);
    }

    [TestMethod]
    public void WhenEverySheetIsLinkedOnlyToOtherPagesTheCatalogFallsBackToEverySheet()
    {
        var pageKey = Guid.NewGuid();
        var theirs = NamedSheet("Evaporateur_1", "#4 := {FAN_03_RUN}");
        var callerPage = Page("win00001", OpenCommand(theirs.SheetKey, Guid.NewGuid()));
        var scenes = Scenes(callerPage);

        var catalog = QuickWindowPagePlaceholderCatalog.Build(Project([theirs], CommandCatalog()), scenes, pageKey);

        CollectionAssert.AreEqual(new[] { "#4" }, catalog.Tags.Select(tag => tag.Id).ToArray());
    }

    [TestMethod]
    public void TheCaptionOfASingleSheetNamesIt()
    {
        var pageKey = Guid.NewGuid();
        var sheet = NamedSheet("Condenseur_1", "#1 := {FAN_03_RUN}");
        var scenes = Scenes(Page("win00001", OpenCommand(sheet.SheetKey, pageKey)));

        var catalog = QuickWindowPagePlaceholderCatalog.Build(Project([sheet], CommandCatalog()), scenes, pageKey);

        Assert.AreEqual("Paramètre — Condenseur_1 : {FAN_03_RUN}", catalog.Tags.Single().Device);
    }

    [TestMethod]
    public void LiteralAndExpressionSourcesAreRenderedWithTheirSheetName()
    {
        var pageKey = Guid.NewGuid();
        var sheet = NamedSheet("Condenseur_1", "#2 := \"Ventilateur 3\"\n#3 := {FAN_03_SPEED} * 0.1");
        var scenes = Scenes(Page("win00001", OpenCommand(sheet.SheetKey, pageKey)));

        var catalog = QuickWindowPagePlaceholderCatalog.Build(Project([sheet], CommandCatalog()), scenes, pageKey);

        Assert.AreEqual("Paramètre — Condenseur_1 : \"Ventilateur 3\"", catalog.Tags.Single(t => t.Id == "#2").Device);
        Assert.AreEqual("Paramètre — Condenseur_1 : {FAN_03_SPEED} * 0.1", catalog.Tags.Single(t => t.Id == "#3").Device);
    }

    [TestMethod]
    public void AnUndefinedNumberKeepsItsUnchangedCaptionAndEveryDisplayNameStillEqualsItsId()
    {
        var pageKey = Guid.NewGuid();
        var sheet = NamedSheet("Condenseur_1", "#1 := {FAN_03_RUN}");
        var scenes = Scenes(Page("win00001", OpenCommand(sheet.SheetKey, pageKey)), PageWithTextUsage(pageKey, "#7"));

        var catalog = QuickWindowPagePlaceholderCatalog.Build(Project([sheet], CommandCatalog()), scenes, pageKey);

        Assert.AreEqual("Paramètre — (non défini dans les feuilles)", catalog.Tags.Single(t => t.Id == "#7").Device);
        foreach (var tag in catalog.Tags) Assert.AreEqual(tag.Id, tag.DisplayName);
    }

    [TestMethod]
    public void ALongCaptionIsTruncatedBetweenSegmentsNeverInsideAToken()
    {
        var pageKey = Guid.NewGuid();
        var sheets = Enumerable.Range(1, 8)
            .Select(i => NamedSheet($"Condenseur_numero_{i:00}", $"#1 := {{FAN_03_RUN}} + {i}"))
            .ToArray();
        var scenes = Scenes(Page("win00001", OpenCommand(sheets[0].SheetKey, pageKey)));

        var catalog = QuickWindowPagePlaceholderCatalog.Build(Project(sheets, CommandCatalog()), scenes, pageKey);

        var device = catalog.Tags.Single().Device!;
        Assert.IsTrue(device.Length <= 160, device);
        Assert.IsTrue(device.EndsWith(" · …", StringComparison.Ordinal), device);
        Assert.IsTrue(device.StartsWith("Paramètre — Condenseur_numero_01 : {FAN_03_RUN} + 1 · Condenseur_numero_02", StringComparison.Ordinal), device);
        var withoutSuffix = device[..^" · …".Length];
        foreach (var segment in withoutSuffix["Paramètre — ".Length..].Split(" · "))
        {
            StringAssert.Matches(segment, new System.Text.RegularExpressions.Regex(@"^Condenseur_numero_\d\d : \{FAN_03_RUN\} \+ \d$"));
        }
    }

    [TestMethod]
    public void TheDisplayedSourceLivesInDeviceNeverInDisplayName()
    {
        // Fix round 2 (bug report on Popup_Fan_Condenseur): ElementStateRuleDialog.BuildExpressionFromVariable
        // and ExpressionCreationDialog compose `{tag.DisplayName}` (now `{tag.Id}` via ScadaTagExpressionComposer,
        // but DisplayName is still matched elsewhere) directly into expression source text, and
        // ScadaExpressionValidator.TryResolveTagReference matches a parsed token against DisplayName as its
        // second resolution step. Putting the sheet's source description there produced
        // `{#1 — {BP_1A_AUTO}}`, unparsable. The source now lives in Device; DisplayName is always the bare Id.
        var pageKey = Guid.NewGuid();
        var sheet = Sheet("#1 := {FAN_03_RUN}");
        var project = Project([sheet], CommandCatalog());
        var callerPage = Page("win00001", OpenCommand(sheet.SheetKey, pageKey));
        var scenes = Scenes(callerPage);

        var catalog = QuickWindowPagePlaceholderCatalog.Build(project, scenes, pageKey);

        var tag = catalog.Tags.Single(t => t.Id == "#1");
        Assert.AreEqual("#1", tag.DisplayName);
        StringAssert.Contains(tag.Device, "FAN_03_RUN");
    }

    [TestMethod]
    public void ARegressionTest_EveryPlaceholderEntryHasDisplayNameEqualToId()
    {
        // Required regression test (a): DisplayName == Id == "#N" for defined, multi-source, literal,
        // expression and undefined entries - DisplayName is an identifier-bearing field throughout the app
        // (ElementStateRuleDialog, ExpressionCreationDialog, ScadaExpressionValidator.TryResolveTagReference),
        // never a display-only one. A violation here is exactly the bug reported on Popup_Fan_Condenseur.
        var pageKey = Guid.NewGuid();
        var sheetA = Sheet("#1 := {FAN_03_RUN}\n#2 := \"Ventilateur 3\"\n#3 := {FAN_03_SPEED} * 0.1\n#5 := {FAN_03_SPEED}");
        var sheetB = Sheet("#5 := {FAN_03_START}"); // second source for the same number -> multi-source
        var project = Project([sheetA, sheetB], CommandCatalog());
        var callerPage = Page(
            "win00001",
            OpenCommand(sheetA.SheetKey, pageKey, elementId: "open-a", commandId: "cmd-a"),
            OpenCommand(sheetB.SheetKey, pageKey, elementId: "open-b", commandId: "cmd-b"));
        var quickWindowPage = PageWithTextUsage(pageKey, "#7"); // undefined
        var scenes = Scenes(callerPage, quickWindowPage);

        var catalog = QuickWindowPagePlaceholderCatalog.Build(project, scenes, pageKey);

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

    [TestMethod]
    public void WriteableIsTrueWhenEveryTagContributionAcrossDifferentSheetsResolvesToAWritableTag()
    {
        // Ruling (fix round 1): two sheets binding the same #1 to two different writable project tags
        // (FAN_03_SP / FAN_04_SP) must still report Writeable: true.
        var pageKey = Guid.NewGuid();
        var sheetA = Sheet("#1 := {FAN_03_SP}");
        var sheetB = Sheet("#1 := {FAN_04_SP}");
        var project = Project([sheetA, sheetB], CommandCatalog());
        var callerPage = Page(
            "win00001",
            OpenCommand(sheetA.SheetKey, pageKey, elementId: "open-a", commandId: "cmd-a"),
            OpenCommand(sheetB.SheetKey, pageKey, elementId: "open-b", commandId: "cmd-b"));
        var scenes = Scenes(callerPage);

        var catalog = QuickWindowPagePlaceholderCatalog.Build(project, scenes, pageKey);

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
        var callerPage = Page(
            "win00001",
            OpenCommand(sheetA.SheetKey, pageKey, elementId: "open-a", commandId: "cmd-a"),
            OpenCommand(sheetB.SheetKey, pageKey, elementId: "open-b", commandId: "cmd-b"));
        var scenes = Scenes(callerPage);

        var catalog = QuickWindowPagePlaceholderCatalog.Build(project, scenes, pageKey);

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

    private static Dictionary<Guid, ScadaScene> Scenes(params ScadaScene[] pages) =>
        pages.ToDictionary(page => page.PageKey);
}
