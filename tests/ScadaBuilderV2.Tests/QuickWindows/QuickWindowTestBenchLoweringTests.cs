using ScadaBuilderV2.App.QuickWindows;
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
