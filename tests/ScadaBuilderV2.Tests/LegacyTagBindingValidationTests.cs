using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

/// <summary>
/// Locks the build-validation warnings raised for every element that still carries a legacy
/// <c>Data.TagBinding</c>: inert on a normal page, legacy but still read in quick-window content.
/// </summary>
/// <remarks>
/// Decisions: DEC-0053.
/// Contracts: docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md §2 règle 5.
/// </remarks>
[TestClass]
public sealed class LegacyTagBindingValidationTests
{
    private static readonly Guid PageKey = Guid.Parse("c8000000-0000-4000-8000-0000000000a1");
    private static readonly Guid QuickWindowPageKey = Guid.Parse("c8000000-0000-4000-8000-0000000000b1");
    private static readonly Guid SheetKey = Guid.Parse("c8000000-0000-4000-8000-0000000000f1");

    [TestMethod]
    public void ATagBindingOnANormalPageIsAnInertWarningNotAnError()
    {
        var (reference, scene) = Page(PageKey, ScadaPageType.Default, "synoptique", TagBindingText("level", "FAN_03_RUN"));
        var project = Project([reference]);

        var issues = ScadaProjectBuildValidator.Validate(project, [scene]);

        var issue = issues.Single(item => item.ElementId == "level");
        Assert.AreEqual(ScadaBuildValidationSeverity.Warning, issue.Severity);
        Assert.AreEqual(ScadaProjectBuildValidator.LegacyTagBindingInertCode, issue.Code);
        Assert.AreEqual("tag-binding.legacy-inert", issue.Code);
        StringAssert.Contains(issue.Message, "inerte sur cette page");
        StringAssert.Contains(issue.Message, "FAN_03_RUN");
        Assert.AreEqual(PageKey, issue.PageKey);
        Assert.AreEqual("Elements[level].Data.TagBinding", issue.PropertyPath);
    }

    [TestMethod]
    public void ATagBindingInLoweredQuickWindowContentIsALegacyWarningWithItsOwnCode()
    {
        var (quickWindowReference, quickWindowScene) = Page(QuickWindowPageKey, ScadaPageType.QuickWindow, "popup", TagBindingText("level", "#5"));
        var opener = ScadaElement.CreateButton("open", "Ouvrir", 20, 20) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding("open-cmd", "Ouvrir", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
                    ParameterSheetKey: SheetKey, QuickWindowPageKey: QuickWindowPageKey)
            ])
        };
        var (callerReference, callerScene) = Page(PageKey, ScadaPageType.Default, "synoptique", opener);
        var project = Project([callerReference, quickWindowReference]) with
        {
            ParameterSheets = [new ParameterSheet(SheetKey, "sheet", "Feuille", "#5 := {FAN_03_RUN}\n")]
        };

        var lowered = ParameterSheetLowering.Lower(project, new Dictionary<Guid, ScadaScene>
        {
            [PageKey] = callerScene,
            [QuickWindowPageKey] = quickWindowScene
        });
        var issues = ScadaProjectBuildValidator.Validate(lowered.Project, lowered.Scenes.Values.ToArray());

        var issue = issues.Single(item => item.ElementId == "level");
        Assert.AreEqual(ScadaBuildValidationSeverity.Warning, issue.Severity);
        Assert.AreEqual("tag-binding.legacy-quick-window", issue.Code);
        Assert.AreNotEqual(ScadaProjectBuildValidator.LegacyTagBindingInertCode, issue.Code);
        StringAssert.Contains(issue.Message, "Lecture de variable");
        Assert.AreEqual(QuickWindowPageKey, issue.PageKey, "the definition key is the quick-window page key (DEC-0052 R4)");
    }

    [TestMethod]
    public void AnUnloweredQuickWindowPageSceneIsAlsoReportedAsLegacyNotInert()
    {
        var (reference, scene) = Page(QuickWindowPageKey, ScadaPageType.QuickWindow, "popup", TagBindingText("level", "#5"));
        var issues = new List<ScadaBuildValidationIssue>();

        ScadaProjectBuildValidator.AuditLegacyTagBindings(issues, scene, isQuickWindowPage: true);

        Assert.AreEqual("tag-binding.legacy-quick-window", issues.Single().Code);
        Assert.AreEqual(ScadaPageType.QuickWindow, reference.Type);
    }

    [TestMethod]
    public void AResidualTagBindingIsStillReportedAndSaysTheReadTagIdWins()
    {
        var element = TagBindingText("level", "FAN_03_RUN");
        element = element with { Data = element.Data! with { ReadTagId = "tf100.mapping.301" } };
        var (reference, scene) = Page(PageKey, ScadaPageType.Default, "synoptique", element);

        var issue = ScadaProjectBuildValidator.Validate(Project([reference]), [scene]).Single(item => item.ElementId == "level");

        Assert.AreEqual("tag-binding.legacy-inert", issue.Code);
        StringAssert.Contains(issue.Message, "résiduel");
    }

    [TestMethod]
    public void ACustomElementLibraryReferenceAndAnElementWithoutTagBindingAreNeverReported()
    {
        var custom = new ScadaElement(
            "custom",
            "custom",
            ScadaElementKind.Custom,
            new SceneBounds(0, 0, 40, 40),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData("<svg></svg>", null, null, null, null, null, null, null, "pompe.sep", false));
        var (reference, scene) = Page(PageKey, ScadaPageType.Default, "synoptique", custom, ScadaElement.CreateText("plain", "plain", 0, 0));

        var issues = ScadaProjectBuildValidator.Validate(Project([reference]), [scene]);

        Assert.IsFalse(issues.Any(item => item.Code.StartsWith("tag-binding.", StringComparison.Ordinal)),
            string.Join(" | ", issues.Select(item => $"{item.Code}: {item.Message}")));
    }

    private static ScadaProject Project(IReadOnlyList<ScadaSceneReference> pages) =>
        ScadaProject.CreateDefault("LegacyTagBindingValidation") with
        {
            ManifestVersion = "2.3",
            HomePageId = "synoptique",
            Scenes = pages,
            TagCatalog = new ScadaTagCatalog("test", [new ScadaTagDefinition("tf100.mapping.301", "FAN_03_RUN", Datatype: "Boolean")])
        };

    private static (ScadaSceneReference Reference, ScadaScene Scene) Page(
        Guid key, ScadaPageType type, string code, params ScadaElement[] elements)
    {
        var reference = new ScadaSceneReference(code, code, $"scenes/{code}.scene.json", type, PageKey: key, PageCode: code);
        var scene = ScadaScene.CreateEmpty(code, code, new CanvasSize(430, 215)) with
        {
            PageKey = key,
            PageCode = code,
            PageType = type,
            Elements = elements
        };
        return (reference, scene);
    }

    private static ScadaElement TagBindingText(string id, string tagBinding)
    {
        var element = ScadaElement.CreateText(id, id, 0, 0);
        return element with { Data = element.Data! with { TagBinding = tagBinding } };
    }
}
