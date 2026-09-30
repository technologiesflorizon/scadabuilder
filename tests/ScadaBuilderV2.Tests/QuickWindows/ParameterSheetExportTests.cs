using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Exports the lowered projection of quick-window pages, parameter sheets and open commands, and checks
/// that the manifest's <c>QuickWindows[]</c>/<c>QuickWindowInvocations[]</c> registries carry the same
/// expression-AST shape the exporter already writes for state conditions.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, DEC-0052.
/// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §12.4.
/// Tests reused for arrangement style: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetLoweringTests.cs,
/// tests/ScadaBuilderV2.Tests/QuickWindows/Win00054QuickWindowIntegrationTests.cs.
/// </remarks>
[TestClass]
public sealed class ParameterSheetExportTests
{
    private static readonly Guid QuickWindowPageKey = Guid.Parse("6b000000-0000-4000-8000-0000000000a1");
    private static readonly Guid CallerPageKey = Guid.Parse("6b000000-0000-4000-8000-0000000000c1");
    private static readonly Guid SheetKey = Guid.Parse("6b000000-0000-4000-8000-0000000000f3");

    private static readonly Guid SecondQuickWindowPageKey = Guid.Parse("6c000000-0000-4000-8000-0000000000a2");
    private static readonly Guid SecondSheetKey = Guid.Parse("6c000000-0000-4000-8000-0000000000f4");

    private const string QuickWindowPageCode = "popup_fan";
    private const string CallerPageCode = "synoptique";
    private const string SheetCode = "fan_03";
    private const string SheetName = "Ventilateur 3";
    private const string OpenElementId = "open-fan";
    private const string OpenCommandId = "open";
    private const string StateExpressionSource = "{FAN_03_SPEED} * 0.1";

    private const string FullSheet =
        "#1 := {FAN_03_RUN}\n" +
        "#3 := {FAN_03_SPEED} * 0.1\n" +
        "#4 := \"Ventilateur 3\"\n";

    [TestMethod]
    public async Task LoweredQuickWindowsExportTheirExpressionAstAlongsideBindingsAndKeepTheContentUntouched()
    {
        var (project, scenes) = Arrange(FullSheet);
        var lowered = ParameterSheetLowering.Lower(project, scenes);
        Assert.AreEqual(0, lowered.Diagnostics.Count, Describe(lowered));

        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var exportDirectory = Path.Combine(root, "export");
            var pages = lowered.Scenes.Values
                .Select(scene => new Ft100ProjectPageExportInput(scene, null))
                .ToArray();
            var result = await new Ft100SceneExporter().ExportProjectAsync(lowered.Project, pages, exportDirectory);

            var manifestJson = await File.ReadAllTextAsync(result.ManifestPath);
            using var manifest = JsonDocument.Parse(manifestJson);

            var quickWindows = manifest.RootElement.GetProperty("QuickWindows");
            Assert.AreEqual(1, quickWindows.GetArrayLength());
            var definition = quickWindows[0];
            Assert.AreEqual(QuickWindowPageKey.ToString("D"), definition.GetProperty("DefinitionKey").GetString());

            var pagesElement = manifest.RootElement.GetProperty("Pages");
            Assert.IsFalse(
                pagesElement.EnumerateArray().Any(page => page.GetProperty("Id").GetString() == QuickWindowPageCode),
                "no exported page may carry the QuickWindow page's code");

            var invocations = manifest.RootElement.GetProperty("QuickWindowInvocations");
            Assert.AreEqual(1, invocations.GetArrayLength());
            var bindings = invocations[0].GetProperty("Bindings");

            var member3 = QuickWindowKeys.Member(QuickWindowPageKey, 3).ToString("D");
            var member4 = QuickWindowKeys.Member(QuickWindowPageKey, 4).ToString("D");
            var binding3 = bindings.EnumerateArray().Single(b => b.GetProperty("MemberKey").GetString() == member3);
            var binding4 = bindings.EnumerateArray().Single(b => b.GetProperty("MemberKey").GetString() == member4);

            Assert.AreEqual("Expression", binding3.GetProperty("SourceKind").GetString());
            var ast3 = binding3.GetProperty("ExpressionAst");
            Assert.AreEqual("binary", ast3.GetProperty("type").GetString());
            var op3 = ast3.GetProperty("op").GetString();
            Assert.AreEqual("multiply", op3);

            Assert.AreEqual("Literal", binding4.GetProperty("SourceKind").GetString());
            Assert.AreEqual("Ventilateur 3", binding4.GetProperty("LiteralValue").GetString());
            Assert.IsFalse(binding4.TryGetProperty("ExpressionAst", out _), "a literal binding carries no ExpressionAst");

            // The definition's HTML fragment keeps every #N untouched.
            var relativeHtmlPath = definition.GetProperty("RelativePath").GetString()!;
            var definitionHtml = await File.ReadAllTextAsync(
                Path.Combine(result.ExportDirectory, relativeHtmlPath.Replace('/', Path.DirectorySeparatorChar)));
            StringAssert.Contains(definitionHtml, "#1");
            StringAssert.Contains(definitionHtml, "#3");
            StringAssert.Contains(definitionHtml, "#4");

            // The same "op" the state condition on the caller page exports through data-scada-state-config.
            var callerHtmlPath = Path.Combine(result.ExportDirectory, CallerPageCode, $"{CallerPageCode}.html");
            var callerHtml = await File.ReadAllTextAsync(callerHtmlPath);
            var stateOp = ExtractStateConditionOp(callerHtml);
            Assert.AreEqual(stateOp, op3, "ruling R3: the same AST shape as a state condition's expression");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ADanglingHeaderPageReferenceIsANamedDiagnostic()
    {
        var (project, scenes) = Arrange(FullSheet);
        project = project with
        {
            Scenes = project.Scenes
                .Select(page => page.EffectivePageCode == CallerPageCode
                    ? page with { HeaderPageKey = QuickWindowPageKey }
                    : page)
                .ToArray()
        };

        var lowered = ParameterSheetLowering.Lower(project, scenes);

        var issue = lowered.Diagnostics.Single(d => d.Code == "parameter-sheet.page-referenced-as-page");
        Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity);
        StringAssert.Contains(issue.Message, $"'{CallerPageCode}'");
        StringAssert.Contains(issue.Message, $"'{QuickWindowPageCode}'");
        Assert.AreEqual(QuickWindowPageKey, issue.TargetKey);
        Assert.AreEqual($"Scenes[{CallerPageCode}].HeaderPageKey", issue.PropertyPath);
    }

    [TestMethod]
    public void ADanglingNavigateCommandTargetIsANamedDiagnostic()
    {
        var (project, scenes) = Arrange(FullSheet);
        var callerScene = scenes[CallerPageKey];
        var navigate = ScadaElement.CreateButton("nav", "Aller", 0, 0) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding("nav-cmd", "Aller", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.Navigate,
                    TargetPageKey: QuickWindowPageKey)
            ])
        };
        scenes = new Dictionary<Guid, ScadaScene>(scenes)
        {
            [CallerPageKey] = callerScene with { Elements = [.. callerScene.Elements, navigate] }
        };

        var lowered = ParameterSheetLowering.Lower(project, scenes);

        var issue = lowered.Diagnostics.Single(d => d.Code == "parameter-sheet.page-referenced-as-page");
        Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity);
        StringAssert.Contains(issue.Message, "'nav-cmd'");
        StringAssert.Contains(issue.Message, "'nav'");
        StringAssert.Contains(issue.Message, $"'{QuickWindowPageCode}'");
        Assert.AreEqual("nav", issue.ElementId);
        Assert.AreEqual("nav-cmd", issue.CommandId);
        Assert.AreEqual(QuickWindowPageKey, issue.TargetKey);
    }

    [TestMethod]
    public void ADanglingLegacyActionTargetIsANamedDiagnostic()
    {
        var (project, scenes) = Arrange(FullSheet);
        var callerScene = scenes[CallerPageKey];
        var action = new ScadaActionDefinition("legacy-nav", ScadaActionKind.Navigate, TargetPageKey: QuickWindowPageKey);
        scenes = new Dictionary<Guid, ScadaScene>(scenes)
        {
            [CallerPageKey] = callerScene with { Actions = [action] }
        };

        var lowered = ParameterSheetLowering.Lower(project, scenes);

        var issue = lowered.Diagnostics.Single(d => d.Code == "parameter-sheet.page-referenced-as-page");
        Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity);
        StringAssert.Contains(issue.Message, "'legacy-nav'");
        StringAssert.Contains(issue.Message, $"'{QuickWindowPageCode}'");
        Assert.AreEqual(QuickWindowPageKey, issue.TargetKey);
        Assert.AreEqual($"Scenes[{CallerPageCode}].Actions[legacy-nav].TargetPageKey", issue.PropertyPath);
    }

    [TestMethod]
    public void ADanglingPageReferenceIsADiagnosticThroughTheAppValidationPathRatherThanAnException()
    {
        var (project, scenes) = Arrange(FullSheet);
        project = project with
        {
            Scenes = project.Scenes
                .Select(page => page.EffectivePageCode == CallerPageCode
                    ? page with { FooterPageKey = QuickWindowPageKey }
                    : page)
                .ToArray()
        };

        // The app's validation path: lower first, then validate the lowered projection. No exception.
        var lowered = ParameterSheetLowering.Lower(project, scenes);
        var validationIssues = lowered.Diagnostics
            .Concat(ScadaProjectBuildValidator.Validate(lowered.Project, lowered.Scenes.Values.ToArray()))
            .ToArray();

        Assert.IsTrue(
            validationIssues.Any(issue => issue.Code == "parameter-sheet.page-referenced-as-page"
                && issue.Severity == ScadaBuildValidationSeverity.Error),
            string.Join(" | ", validationIssues.Select(issue => $"{issue.Severity} {issue.Code}: {issue.Message}")));

        // What the gate exists to avoid: exporting the lowered project's dangling reference directly still
        // throws the raw exception the controller ruling calls out (PageRuntimeIdentityResolver, unguarded).
        Assert.ThrowsException<InvalidOperationException>(
            () => PageRuntimeIdentityResolver.Project(lowered.Project, lowered.Scenes.Values.ToArray()));
    }

    [TestMethod]
    public async Task AQuickWindowPageThatOpensAnotherQuickWindowLowersValidatesAndExportsCarryingTheInvocationKey()
    {
        var innerSheet = "#1 := {FAN_03_RUN}\n";
        var innerElements = new[] { ReadElement("inner-read", "#1") };
        var innerPageRef = Page(SecondQuickWindowPageKey, ScadaPageType.QuickWindow, "popup_inner", "Popup interne", innerElements);
        var innerScene = innerPageRef.Scene;

        var openInner = ScadaElement.CreateButton("open-inner", "Ouvrir interne", 0, 0) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding("open-inner-cmd", "Ouvrir", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
                    ParameterSheetKey: SecondSheetKey, QuickWindowPageKey: SecondQuickWindowPageKey)
            ])
        };
        var outerElements = new[] { ReadElement("outer-read", "#1"), TextElement("outer-title", "Ventilateur #4"), openInner };
        var outerPageRef = Page(QuickWindowPageKey, ScadaPageType.QuickWindow, QuickWindowPageCode, "Popup ventilateur", outerElements);

        var callerRef = Page(CallerPageKey, ScadaPageType.Default, CallerPageCode, "Synoptique", OpenCommandElement(SheetKey, QuickWindowPageKey));

        var project = ScadaProject.CreateDefault("ParameterSheetExportNesting") with
        {
            ManifestVersion = "2.3",
            HomePageId = CallerPageCode,
            Scenes = [callerRef.Reference, outerPageRef.Reference, innerPageRef.Reference],
            TagCatalog = Catalog(),
            ParameterSheets =
            [
                new ParameterSheet(SheetKey, SheetCode, SheetName, "#1 := {FAN_03_RUN}\n#4 := \"Ventilateur 3\"\n"),
                new ParameterSheet(SecondSheetKey, "inner", "Popup interne", innerSheet)
            ]
        };
        var scenes = new Dictionary<Guid, ScadaScene>
        {
            [CallerPageKey] = callerRef.Scene,
            [QuickWindowPageKey] = outerPageRef.Scene,
            [SecondQuickWindowPageKey] = innerScene
        };

        var lowered = ParameterSheetLowering.Lower(project, scenes);
        Assert.AreEqual(0, lowered.Diagnostics.Count, Describe(lowered));
        Assert.AreEqual(2, lowered.Project.EffectiveQuickWindows.Count);
        Assert.AreEqual(2, lowered.Project.EffectiveQuickWindowInvocations.Count);

        var validationIssues = ScadaProjectBuildValidator.Validate(lowered.Project, lowered.Scenes.Values.ToArray());
        Assert.IsFalse(
            validationIssues.Any(issue => issue.Severity == ScadaBuildValidationSeverity.Error),
            string.Join(" | ", validationIssues.Select(issue => $"{issue.Severity} {issue.Code}: {issue.Message}")));

        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var exportDirectory = Path.Combine(root, "export");
            var pages = lowered.Scenes.Values.Select(scene => new Ft100ProjectPageExportInput(scene, null)).ToArray();
            var result = await new Ft100SceneExporter().ExportProjectAsync(lowered.Project, pages, exportDirectory);

            var manifestJson = await File.ReadAllTextAsync(result.ManifestPath);
            using var manifest = JsonDocument.Parse(manifestJson);
            var definitions = manifest.RootElement.GetProperty("QuickWindows");
            Assert.AreEqual(2, definitions.GetArrayLength());

            var outerDefinition = definitions.EnumerateArray()
                .Single(d => d.GetProperty("DefinitionKey").GetString() == QuickWindowPageKey.ToString("D"));
            var outerHtmlPath = Path.Combine(
                result.ExportDirectory,
                outerDefinition.GetProperty("RelativePath").GetString()!.Replace('/', Path.DirectorySeparatorChar));
            var outerHtml = await File.ReadAllTextAsync(outerHtmlPath);

            var innerInvocationKey = lowered.Project.EffectiveQuickWindowInvocations
                .Single(invocation => invocation.OwnerElementId == "open-inner")
                .InvocationKey;
            StringAssert.Contains(outerHtml, innerInvocationKey.ToString("D"),
                "the outer definition's exported command carries the inner invocation key");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    // ---- Arrangement ------------------------------------------------------------------------------------

    private static (ScadaProject Project, IReadOnlyDictionary<Guid, ScadaScene> Scenes) Arrange(string sheetText)
    {
        var quickWindow = Page(QuickWindowPageKey, ScadaPageType.QuickWindow, QuickWindowPageCode, "Popup ventilateur", FanPageElements());
        var caller = Page(CallerPageKey, ScadaPageType.Default, CallerPageCode, "Synoptique",
            OpenCommandElement(SheetKey, QuickWindowPageKey),
            StateElement("caller-state", StateExpressionSource));

        var project = ScadaProject.CreateDefault("ParameterSheetExport") with
        {
            ManifestVersion = "2.3",
            HomePageId = CallerPageCode,
            Scenes = [caller.Reference, quickWindow.Reference],
            TagCatalog = Catalog(),
            ParameterSheets = [new ParameterSheet(SheetKey, SheetCode, SheetName, sheetText)]
        };
        var scenes = new Dictionary<Guid, ScadaScene>
        {
            [CallerPageKey] = caller.Scene,
            [QuickWindowPageKey] = quickWindow.Scene
        };
        return (project, scenes);
    }

    private static ScadaElement[] FanPageElements() =>
    [
        ReadElement("read1", "#1"),
        StateElement("state3", "{#3} == 1"),
        TextElement("title4", "Ventilateur #4"),
    ];

    private static (ScadaSceneReference Reference, ScadaScene Scene) Page(
        Guid key, ScadaPageType type, string code, string title, params ScadaElement[] elements)
    {
        var reference = new ScadaSceneReference(code, title, $"scenes/{code}.scene.json", type, PageKey: key, PageCode: code);
        var scene = ScadaScene.CreateEmpty(code, title, new CanvasSize(430, 215)) with
        {
            PageKey = key,
            PageCode = code,
            PageType = type,
            Elements = elements
        };
        return (reference, scene);
    }

    private static ScadaElement TextElement(string id, string text)
    {
        var element = ScadaElement.CreateText(id, id, 0, 0);
        return element with { Data = element.Data! with { Text = text } };
    }

    private static ScadaElement ReadElement(string id, string tagId)
    {
        var element = ScadaElement.CreateText(id, id, 0, 0);
        return element with { Data = element.Data! with { Text = string.Empty, ReadTagId = tagId } };
    }

    private static ScadaElement StateElement(string id, string expressionSource) =>
        ScadaElement.CreateShape(id, id, ScadaShapeKind.Rectangle, 0, 0) with
        {
            StateConfig = ScadaElementStateConfig.Default with
            {
                States = [new ScadaStateRule($"{id}-state", "Actif", true, ScadaExpression.FromSource(expressionSource), ScadaEffectBlock.Empty)]
            }
        };

    private static ScadaElement OpenCommandElement(Guid sheetKey, Guid pageKey) =>
        ScadaElement.CreateButton(OpenElementId, "Ventilateur 3", 20, 20) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding(OpenCommandId, "Ouvrir", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
                    ParameterSheetKey: sheetKey, QuickWindowPageKey: pageKey)
            ])
        };

    private static ScadaTagCatalog Catalog() => new(
        "scada-v2-parameter-sheet-export-tests",
        [
            // Realistic datatypes (Task 6 fix round 1): #N members now infer their DataType from every
            // invocation's binding (spec §6), so a Boolean tag on a Tag-kind #N no longer fails
            // QuickWindowBindingValidator's real type check the way it would against an always-String member.
            new ScadaTagDefinition("tf100.mapping.101", "FAN_03_RUN", Datatype: "Boolean"),
            new ScadaTagDefinition("tf100.mapping.103", "FAN_03_SPEED", Datatype: "Decimal"),
        ],
        "generated-parameter-sheet-export-tags.json");

    private static string ExtractStateConditionOp(string html)
    {
        var match = Regex.Match(html, "data-scada-state-config=\"([^\"]*)\"");
        Assert.IsTrue(match.Success, "the caller page must export a data-scada-state-config attribute");
        var json = WebUtility.HtmlDecode(match.Groups[1].Value);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("states")[0].GetProperty("expression").GetProperty("ast").GetProperty("op").GetString()!;
    }

    private static string Describe(ParameterSheetLoweringResult result) =>
        string.Join(" | ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}"));
}
