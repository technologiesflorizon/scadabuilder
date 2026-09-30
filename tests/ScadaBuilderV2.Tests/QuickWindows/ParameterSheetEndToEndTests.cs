using System.Text;
using System.Text.Json;
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
/// Task 12: proves a parameter-sheet quick window end to end inside the Builder — a caller page with two
/// buttons opening the same <c>QuickWindow</c> page through two different parameter sheets, lowered,
/// validated (0 error) and exported as a real package — then freezes the exported manifest and the
/// definition's HTML fragment into a committed fixture under <c>tests/conformance/</c> so the shared
/// JavaScript runtime test (<c>tests/runtime-js/quick-window-parameter-sheet-e2e.test.mjs</c>) can mount the
/// REAL exported fragment (not hand-written HTML) and prove each opened instance shows only its own sheet's
/// values.
/// </summary>
/// <remarks>
/// The fixture is regenerated only by <c>SCADA_UPDATE_PARAMETER_SHEET_E2E=1</c>, following the same
/// protected-harness convention as <c>QuickWindowRuntimeHandshakeFixtureTests</c>. A plain directory of
/// text files (manifest + one HTML fragment) was preferred over a zipped <c>.sb2</c> here because the JS
/// side has no zip-reading dependency and none may be added (constraints.md); reading two committed text
/// files needs none.
///
/// Every element of the <c>QuickWindow</c> page's content is authored as <see cref="ScadaElementKind.Text"/>
/// on purpose: a <see cref="ScadaElementKind.Shape"/> (as used by other fixtures in this suite) emits
/// self-closing SVG child markup (`&lt;rect .../&gt;`) that the JS harness's minimal HTML fragment parser
/// (`tests/runtime-js/harness.mjs`, `parseFragment`) cannot walk; a state condition's
/// `data-scada-state-config` attribute is emitted on the element's outer `&lt;div&gt;` regardless of its
/// content kind, so `Text` carries the same proof with no self-closing markup anywhere in the fragment.
///
/// Decisions: DEC-0050, DEC-0052.
/// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §12.4.
/// Tests reused for arrangement style: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetExportTests.cs,
/// tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowRuntimeHandshakeFixtureTests.cs.
/// </remarks>
[TestClass]
public sealed class ParameterSheetEndToEndTests
{
    private const string FixtureDirectoryName = "quick-window-parameter-sheet-e2e";
    private const string ManifestFileName = "manifest.json";

    private static readonly Guid QuickWindowPageKey = Guid.Parse("7e000000-0000-4000-8000-0000000000a1");
    private static readonly Guid CallerPageKey = Guid.Parse("7e000000-0000-4000-8000-0000000000c1");
    private static readonly Guid SheetFan03Key = Guid.Parse("7e000000-0000-4000-8000-0000000000f3");
    private static readonly Guid SheetFan05Key = Guid.Parse("7e000000-0000-4000-8000-0000000000f5");

    private const string QuickWindowPageCode = "popup_fan_e2e";
    private const string CallerPageCode = "synoptique_e2e";
    private const string SheetFan03Code = "fan_03";
    private const string SheetFan05Code = "fan_05";
    private const string SheetFan03Name = "Ventilateur 3";
    private const string SheetFan05Name = "Ventilateur 5";

    private const string SheetFan03Text =
        "#1 := {FAN_03_RUN}\n" +
        "#3 := {FAN_03_SPEED} * 0.1\n" +
        "#4 := \"Ventilateur 3\"\n";

    private const string SheetFan05Text =
        "#1 := {FAN_05_RUN}\n" +
        "#3 := {FAN_05_SPEED} * 0.1\n" +
        "#4 := \"Ventilateur 5\"\n";

    [TestMethod]
    public async Task AQuickWindowOpenedThroughTwoParameterSheetsLowersValidatesAndExportsTwoIndependentInvocations()
    {
        var (project, scenes) = Arrange();

        var lowered = ParameterSheetLowering.Lower(project, scenes);
        Assert.AreEqual(0, lowered.Diagnostics.Count, Describe(lowered));

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

            var quickWindows = manifest.RootElement.GetProperty("QuickWindows");
            Assert.AreEqual(1, quickWindows.GetArrayLength(), "the two sheets open the SAME QuickWindow definition");
            var definition = quickWindows[0];
            Assert.AreEqual(QuickWindowPageKey.ToString("D"), definition.GetProperty("DefinitionKey").GetString());
            var relativeHtmlPath = definition.GetProperty("RelativePath").GetString()!;

            var invocations = manifest.RootElement.GetProperty("QuickWindowInvocations");
            Assert.AreEqual(2, invocations.GetArrayLength(), "one invocation per open command / sheet");

            var member1 = QuickWindowKeys.Member(QuickWindowPageKey, 1).ToString("D");
            var member3 = QuickWindowKeys.Member(QuickWindowPageKey, 3).ToString("D");
            var member4 = QuickWindowKeys.Member(QuickWindowPageKey, 4).ToString("D");

            var invocationFan03 = invocations.EnumerateArray()
                .Single(invocation => Binding(invocation, member4).GetProperty("LiteralValue").GetString() == SheetFan03Name);
            var invocationFan05 = invocations.EnumerateArray()
                .Single(invocation => Binding(invocation, member4).GetProperty("LiteralValue").GetString() == SheetFan05Name);

            Assert.AreNotEqual(
                invocationFan03.GetProperty("InvocationKey").GetString(),
                invocationFan05.GetProperty("InvocationKey").GetString(),
                "two open commands over two distinct sheets must produce two distinct invocations");
            Assert.AreEqual(definition.GetProperty("DefinitionKey").GetString(), invocationFan03.GetProperty("DefinitionKey").GetString());
            Assert.AreEqual(definition.GetProperty("DefinitionKey").GetString(), invocationFan05.GetProperty("DefinitionKey").GetString());

            // #1 (read): each invocation binds a different tag.
            Assert.AreEqual("Tag", Binding(invocationFan03, member1).GetProperty("SourceKind").GetString());
            Assert.AreEqual("tf100.mapping.301", Binding(invocationFan03, member1).GetProperty("TagId").GetString());
            Assert.AreEqual("Tag", Binding(invocationFan05, member1).GetProperty("SourceKind").GetString());
            Assert.AreEqual("tf100.mapping.501", Binding(invocationFan05, member1).GetProperty("TagId").GetString());

            // #3 (état): each invocation binds a different expression over a different numeric tag.
            Assert.AreEqual("Expression", Binding(invocationFan03, member3).GetProperty("SourceKind").GetString());
            Assert.AreEqual("Expression", Binding(invocationFan05, member3).GetProperty("SourceKind").GetString());
            var ast03 = Binding(invocationFan03, member3).GetProperty("ExpressionAst");
            var ast05 = Binding(invocationFan05, member3).GetProperty("ExpressionAst");
            Assert.AreEqual("binary", ast03.GetProperty("type").GetString());
            Assert.AreEqual("binary", ast05.GetProperty("type").GetString());
            Assert.AreNotEqual(ast03.ToString(), ast05.ToString(), "the two sheets' #3 expressions must not be identical");

            // #4 (texte): literal strings already used above to tell the invocations apart.
            Assert.AreEqual("Literal", Binding(invocationFan03, member4).GetProperty("SourceKind").GetString());
            Assert.AreEqual("Literal", Binding(invocationFan05, member4).GetProperty("SourceKind").GetString());

            var definitionHtml = await File.ReadAllTextAsync(
                Path.Combine(result.ExportDirectory, relativeHtmlPath.Replace('/', Path.DirectorySeparatorChar)));
            StringAssert.Contains(definitionHtml, "#1");
            StringAssert.Contains(definitionHtml, "#3");
            StringAssert.Contains(definitionHtml, "#4");
            StringAssert.DoesNotMatch(definitionHtml, new System.Text.RegularExpressions.Regex("<[a-zA-Z][^>]*/>"),
                "the fragment must carry no self-closing tag: the JS harness's minimal fragment parser cannot walk one");

            WriteOrCompareFixture(manifestJson, relativeHtmlPath, definitionHtml);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static JsonElement Binding(JsonElement invocation, string memberKey) =>
        invocation.GetProperty("Bindings").EnumerateArray().Single(b => b.GetProperty("MemberKey").GetString() == memberKey);

    /// <summary>
    /// Regenerates the committed fixture (`SCADA_UPDATE_PARAMETER_SHEET_E2E=1`) or asserts the freshly
    /// exported manifest/fragment still match it byte for byte, exactly the way
    /// <see cref="QuickWindowRuntimeHandshakeFixtureTests"/> locks its own package. The fixture is a plain
    /// directory (no zip): `tests/runtime-js/quick-window-parameter-sheet-e2e.test.mjs` reads it
    /// unconditionally, with no environment variable and no skip, so the JS-side proof always runs in CI.
    /// </summary>
    private static void WriteOrCompareFixture(string manifestJson, string relativeHtmlPath, string definitionHtml)
    {
        var repositoryRoot = FindRepositoryRoot();
        var fixtureDirectory = Path.Combine(repositoryRoot, "tests", "conformance", FixtureDirectoryName);
        var manifestPath = Path.Combine(fixtureDirectory, ManifestFileName);
        var htmlFixturePath = Path.Combine(fixtureDirectory, relativeHtmlPath.Replace('/', Path.DirectorySeparatorChar));
        var utf8NoBom = new UTF8Encoding(false);

        if (string.Equals(Environment.GetEnvironmentVariable("SCADA_UPDATE_PARAMETER_SHEET_E2E"), "1", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(fixtureDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(htmlFixturePath)!);
            File.WriteAllText(manifestPath, manifestJson.ReplaceLineEndings("\n"), utf8NoBom);
            File.WriteAllText(htmlFixturePath, definitionHtml.ReplaceLineEndings("\n"), utf8NoBom);
        }

        Assert.IsTrue(File.Exists(manifestPath), "Run with SCADA_UPDATE_PARAMETER_SHEET_E2E=1 to create the fixture.");
        Assert.IsTrue(File.Exists(htmlFixturePath), "Run with SCADA_UPDATE_PARAMETER_SHEET_E2E=1 to create the fixture.");
        Assert.AreEqual(
            manifestJson.ReplaceLineEndings("\n"),
            File.ReadAllText(manifestPath).Replace("\r\n", "\n", StringComparison.Ordinal),
            "the committed end-to-end manifest fixture drifted");
        Assert.AreEqual(
            definitionHtml.ReplaceLineEndings("\n"),
            File.ReadAllText(htmlFixturePath).Replace("\r\n", "\n", StringComparison.Ordinal),
            "the committed end-to-end fragment fixture drifted");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ScadaBuilderV2.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        Assert.Fail("Unable to locate the repository root.");
        return string.Empty;
    }

    // ---- Arrangement ------------------------------------------------------------------------------------

    private static (ScadaProject Project, IReadOnlyDictionary<Guid, ScadaScene> Scenes) Arrange()
    {
        var quickWindow = Page(QuickWindowPageKey, ScadaPageType.QuickWindow, QuickWindowPageCode, "Popup ventilateur", FanPageElements());
        var caller = Page(CallerPageKey, ScadaPageType.Default, CallerPageCode, "Synoptique",
            OpenCommandElement("open-fan3", "open-3", SheetFan03Key, QuickWindowPageKey, "Ventilateur 3"),
            OpenCommandElement("open-fan5", "open-5", SheetFan05Key, QuickWindowPageKey, "Ventilateur 5"));

        var project = ScadaProject.CreateDefault("ParameterSheetEndToEnd") with
        {
            ManifestVersion = "2.3",
            HomePageId = CallerPageCode,
            Scenes = [caller.Reference, quickWindow.Reference],
            TagCatalog = Catalog(),
            ParameterSheets =
            [
                new ParameterSheet(SheetFan03Key, SheetFan03Code, SheetFan03Name, SheetFan03Text),
                new ParameterSheet(SheetFan05Key, SheetFan05Code, SheetFan05Name, SheetFan05Text)
            ]
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
        StateTextElement("state3", "{#3} == 1"),
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

    /// <summary>
    /// A state condition carried by a <see cref="ScadaElementKind.Text"/> element rather than a
    /// <see cref="ScadaElementKind.Shape"/>: see the class remarks for why (self-closing SVG content would
    /// defeat the JS harness's minimal fragment parser). `data-scada-state-config` is emitted on the outer
    /// `&lt;div&gt;` for every element kind, so the substitution proof is identical either way.
    /// </summary>
    private static ScadaElement StateTextElement(string id, string expressionSource)
    {
        var element = ScadaElement.CreateText(id, id, 0, 0);
        return element with
        {
            StateConfig = ScadaElementStateConfig.Default with
            {
                States = [new ScadaStateRule($"{id}-state", "Actif", true, ScadaExpression.FromSource(expressionSource), ScadaEffectBlock.Empty)]
            }
        };
    }

    private static ScadaElement OpenCommandElement(
        string elementId, string commandId, Guid sheetKey, Guid pageKey, string buttonLabel) =>
        ScadaElement.CreateButton(elementId, buttonLabel, 20, 20) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding(commandId, "Ouvrir", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
                    ParameterSheetKey: sheetKey, QuickWindowPageKey: pageKey)
            ])
        };

    private static ScadaTagCatalog Catalog() => new(
        "scada-v2-parameter-sheet-e2e-tests",
        [
            new ScadaTagDefinition("tf100.mapping.301", "FAN_03_RUN", Datatype: "Boolean"),
            new ScadaTagDefinition("tf100.mapping.302", "FAN_03_SPEED", Datatype: "Decimal"),
            new ScadaTagDefinition("tf100.mapping.501", "FAN_05_RUN", Datatype: "Boolean"),
            new ScadaTagDefinition("tf100.mapping.502", "FAN_05_SPEED", Datatype: "Decimal"),
        ],
        "generated-parameter-sheet-e2e-tags.json");

    private static string Describe(ParameterSheetLoweringResult result) =>
        string.Join(" | ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}"));
}
