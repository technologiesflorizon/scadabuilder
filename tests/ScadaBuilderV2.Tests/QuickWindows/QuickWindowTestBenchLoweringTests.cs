using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// `Banc d'essai` lowering: a synthetic "Ouvrir une fenêtre rapide" command pairing one sheet with the
/// active quick-window page, lowered on an in-memory snapshot the caller never writes back (DEC-0052).
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5, §6, §7.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class QuickWindowTestBenchLoweringTests
{
    private static readonly Guid PageKey = Guid.Parse("7a000000-0000-4000-8000-0000000000a1");
    private static readonly Guid SheetKey = Guid.Parse("7a000000-0000-4000-8000-0000000000f1");

    [TestMethod]
    public void LoweringASheetAgainstTheActivePageProducesADefinitionAndAMatchingInvocation()
    {
        var (project, page) = BuildProject("#4 := \"Ventilateur 3\"");

        var result = QuickWindowTestBenchLowering.Lower(project, page, PageKey, project.EffectiveParameterSheets[0]);

        Assert.IsNotNull(result.Definition);
        Assert.IsNotNull(result.Invocation);
        Assert.AreEqual(PageKey, result.Definition!.DefinitionKey);
        Assert.AreEqual(PageKey, result.Invocation!.DefinitionKey);
        var binding = result.Invocation.Bindings.Single();
        Assert.AreEqual(QuickWindowBindingSourceKind.Literal, binding.SourceKind);
        Assert.AreEqual("Ventilateur 3", binding.LiteralValue);
        Assert.IsFalse(result.Diagnostics.Any(issue => issue.Severity == ScadaBuildValidationSeverity.Error));
    }

    [TestMethod]
    public void AnUndefinedPlaceholderRefusesThePreviewAndNamesTheError()
    {
        var (project, page) = BuildProject(sheetText: "// vide\n");

        var result = QuickWindowTestBenchLowering.Lower(project, page, PageKey, project.EffectiveParameterSheets[0]);

        Assert.IsNull(result.Definition);
        Assert.IsNull(result.Invocation);
        Assert.IsTrue(result.Diagnostics.Any(issue =>
            issue.Severity == ScadaBuildValidationSeverity.Error &&
            issue.Code == "parameter-sheet.placeholder-undefined"));
    }

    [TestMethod]
    public void LoweringNeverMutatesTheProjectOrTheActivePageItReads()
    {
        var (project, page) = BuildProject("#4 := \"Ventilateur 3\"");
        var beforeSheets = project.ParameterSheets;
        var beforeScenes = project.Scenes;
        var beforeElements = page.Elements;

        QuickWindowTestBenchLowering.Lower(project, page, PageKey, project.EffectiveParameterSheets[0]);

        Assert.AreSame(beforeSheets, project.ParameterSheets, "the caller's project record is never replaced");
        Assert.AreSame(beforeScenes, project.Scenes, "the caller's page inventory is never replaced");
        Assert.AreSame(beforeElements, page.Elements, "the caller's page content is never replaced");
        Assert.AreEqual(0, project.EffectiveQuickWindows.Count, "no definition is ever written into the real project");
        Assert.AreEqual(0, project.EffectiveQuickWindowInvocations.Count, "no invocation is ever written into the real project");
    }

    [TestMethod]
    public void ANestedOpenQuickWindowCommandUsingAnotherSheetAndAnotherPageStillLetsThePreviewSucceed()
    {
        // Controller fix round 1: the active page carries a second, unrelated "Ouvrir une fenêtre rapide"
        // button pairing a DIFFERENT sheet with a DIFFERENT QuickWindow page (whose scene is not loaded,
        // exactly like a page the operator has not opened in a tab). Before the fix, the snapshot only
        // carried the chosen sheet and the active page, so this nested, otherwise well-formed command
        // raised a false parameter-sheet.sheet-missing / parameter-sheet.page-not-quick-window and blocked
        // the preview of the pairing actually under test.
        var otherPageKey = Guid.Parse("7a000000-0000-4000-8000-0000000000a2");
        var otherSheetKey = Guid.Parse("7a000000-0000-4000-8000-0000000000f2");

        var activePageRef = new ScadaSceneReference(
            "popup", "Popup ventilateur", "scenes/popup.scene.json", ScadaPageType.QuickWindow,
            PageKey: PageKey, PageCode: "popup");
        var otherPageRef = new ScadaSceneReference(
            "popup2", "Popup autre", "scenes/popup2.scene.json", ScadaPageType.QuickWindow,
            PageKey: otherPageKey, PageCode: "popup2");

        var titleElement = ScadaElement.CreateText("title", "title", 0, 0);
        titleElement = titleElement with { Data = titleElement.Data! with { Text = "#4" } };
        var nestedButton = ScadaElement.CreateButton("nested-open", "Autre fenêtre", 0, 0) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding("nested-open-cmd", "Ouvrir", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
                    ParameterSheetKey: otherSheetKey, QuickWindowPageKey: otherPageKey)
            ])
        };
        var page = ScadaScene.CreateEmpty("popup", "Popup ventilateur", new CanvasSize(200, 120)) with
        {
            PageKey = PageKey,
            PageCode = "popup",
            PageType = ScadaPageType.QuickWindow,
            Elements = [titleElement, nestedButton]
        };

        var project = ScadaProject.CreateDefault("QuickWindowTestBenchLoweringNested") with
        {
            ManifestVersion = "2.3",
            Scenes = [activePageRef, otherPageRef],
            ParameterSheets =
            [
                new ParameterSheet(SheetKey, "fan_03", "Ventilateur 3", "#4 := \"Ventilateur 3\""),
                new ParameterSheet(otherSheetKey, "other", "Autre feuille", "#1 := \"Peu importe\"")
            ]
        };

        var result = QuickWindowTestBenchLowering.Lower(project, page, PageKey, project.ParameterSheets[0]);

        Assert.IsNotNull(result.Definition, "the pairing under test must still preview despite the unrelated nested command");
        Assert.IsNotNull(result.Invocation);
        Assert.AreEqual("Ventilateur 3", result.Invocation!.Bindings.Single().LiteralValue);
        Assert.IsFalse(
            result.Diagnostics.Any(issue => issue.Code is "parameter-sheet.sheet-missing" or "parameter-sheet.page-not-quick-window"),
            "the other sheet and the other QuickWindow page reference must both be visible to lowering, so no false diagnostic is raised for the nested command");
    }

    private static (ScadaProject Project, ScadaScene Page) BuildProject(string sheetText)
    {
        var pageReference = new ScadaSceneReference(
            "popup", "Popup ventilateur", "scenes/popup.scene.json", ScadaPageType.QuickWindow,
            PageKey: PageKey, PageCode: "popup");
        var titleElement = ScadaElement.CreateText("title", "title", 0, 0);
        titleElement = titleElement with { Data = titleElement.Data! with { Text = "#4" } };
        var page = ScadaScene.CreateEmpty("popup", "Popup ventilateur", new CanvasSize(200, 120)) with
        {
            PageKey = PageKey,
            PageCode = "popup",
            PageType = ScadaPageType.QuickWindow,
            Elements = [titleElement]
        };

        var project = ScadaProject.CreateDefault("QuickWindowTestBenchLowering") with
        {
            ManifestVersion = "2.3",
            Scenes = [pageReference],
            ParameterSheets = [new ParameterSheet(SheetKey, "fan_03", "Ventilateur 3", sheetText)]
        };

        return (project, page);
    }
}
