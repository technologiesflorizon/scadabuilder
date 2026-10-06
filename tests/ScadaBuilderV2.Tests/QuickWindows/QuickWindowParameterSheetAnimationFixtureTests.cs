using System.Security.Cryptography;
using System.Text;
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
/// Task 13: freezes a real, strict-2.3 <c>.sb2</c> archive of a parameter-sheet quick window whose
/// definition carries a live <c>#1</c>-bound <see cref="ScadaReadVariableRule"/> (an independent,
/// continuous tag display) and a live <c>#3</c>-driven <see cref="ScadaStateRule"/> effect that swaps
/// visible text ("ARRET"/"MARCHE"). Both are substituted by the shared runtime at
/// <c>mountInstance</c> (DEC-0052, contract §11b.1) and both are then driven live by the ordinary
/// page runtime (<c>StateEngine.evaluate</c>/<c>onTagValuesChanged</c>) exactly as a real TF100Web
/// page would — unlike <see cref="ParameterSheetEndToEndTests"/>, which proves substitution occurs
/// but never pushes a live tag value afterward, and unlike
/// <see cref="QuickWindowRuntimeHandshakeFixtureTests"/>, whose package carries no <c>#N</c> member and
/// therefore no substitution at all.
/// </summary>
/// <remarks>
/// This is the Builder half of the TF100Web animation probe
/// (<c>frontend/tests_runtime_js/quick-window-parameter-sheet.test.mjs</c> on TF100Web's
/// <c>main</c>, merged from <c>codex/quick-window-v1</c>): that JS test loads THIS exact package's bundled runtime and
/// manifest, opens the invocation through the real, unmodified TF100Web host
/// (<c>static/asset/js/quick-window-host.js</c>), feeds a new value to the tag bound to <c>#1</c>
/// through the tag bridge, and asserts the mounted element's visible text changes — closing the "no
/// element is animated by a substituted binding" half of
/// <c>docs/08_implementation_status/KNOWN_GAPS_V2.md</c> entry 36.
///
/// Regenerate with <c>SCADA_UPDATE_QUICK_WINDOW_PARAMETER_SHEET=1</c> after a deliberate review of the
/// manifest, compiler, runtime or exporter change that caused the drift, then re-vendor the identical
/// bytes into TF100Web's <c>frontend/test_fixtures/quick-window-parameter-sheet.sb2</c> (+ <c>.sha256</c>).
///
/// Decisions: DEC-0050, DEC-0052.
/// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §11b.1, §12.
/// Tests reused for arrangement style: ParameterSheetEndToEndTests, QuickWindowRuntimeHandshakeFixtureTests.
/// </remarks>
[TestClass]
public sealed class QuickWindowParameterSheetAnimationFixtureTests
{
    private const string FixtureName = "quick-window-parameter-sheet.sb2";
    private const string HashName = "quick-window-parameter-sheet.sha256";

    private static readonly Guid QuickWindowPageKey = Guid.Parse("7e130000-0000-4000-8000-0000000000a1");
    private static readonly Guid CallerPageKey = Guid.Parse("7e130000-0000-4000-8000-0000000000c1");
    private static readonly Guid SheetFan03Key = Guid.Parse("7e130000-0000-4000-8000-0000000000f3");

    private const string QuickWindowPageCode = "popup_fan_task13";
    private const string CallerPageCode = "synoptique_task13";
    private const string SheetFan03Code = "fan_03_task13";
    private const string SheetFan03Name = "Ventilateur 3";

    private const string SheetFan03Text =
        "#1 := {FAN_03_RUN}\n" +
        "#3 := {FAN_03_SPEED} * 0.1\n" +
        "#4 := \"Ventilateur 3\"\n";

    [TestMethod]
    public async Task AParameterSheetQuickWindowExportsAsAStrict23ArchiveWithLiveReadVariableAndStateEffectPlaceholders()
    {
        var (project, scenes) = Arrange();

        var lowered = ParameterSheetLowering.Lower(project, scenes);
        Assert.AreEqual(0, lowered.Diagnostics.Count, Describe(lowered));

        var validationIssues = ScadaProjectBuildValidator.Validate(lowered.Project, lowered.Scenes.Values.ToArray());
        Assert.IsFalse(
            validationIssues.Any(issue => issue.Severity == ScadaBuildValidationSeverity.Error),
            string.Join(" | ", validationIssues.Select(issue => $"{issue.Severity} {issue.Code}: {issue.Message}")));

        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", "qw-param-sheet-anim", Guid.NewGuid().ToString("N"));
        try
        {
            var pages = lowered.Scenes.Values.Select(scene => new Ft100ProjectPageExportInput(scene, null)).ToArray();

            var firstBytes = await BuildArchiveAsync(lowered.Project, pages, Path.Combine(root, "first"));
            var secondBytes = await BuildArchiveAsync(lowered.Project, pages, Path.Combine(root, "second"));
            CollectionAssert.AreEqual(firstBytes, secondBytes, "the exporter must produce byte-identical archives for identical inputs");

            var sha256 = Convert.ToHexString(SHA256.HashData(firstBytes)).ToLowerInvariant();

            var conformanceDirectory = Tf100WebFixtureTestSupport.FindConformanceDirectory();
            var fixturePath = Path.Combine(conformanceDirectory, FixtureName);
            var hashPath = Path.Combine(conformanceDirectory, HashName);

            if (string.Equals(Environment.GetEnvironmentVariable("SCADA_UPDATE_QUICK_WINDOW_PARAMETER_SHEET"), "1", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(conformanceDirectory);
                File.WriteAllBytes(fixturePath, firstBytes);
                File.WriteAllText(hashPath, $"{sha256}  {FixtureName}\n", new UTF8Encoding(false));
            }

            Assert.IsTrue(File.Exists(fixturePath), "Run with SCADA_UPDATE_QUICK_WINDOW_PARAMETER_SHEET=1 to create the fixture.");
            CollectionAssert.AreEqual(firstBytes, File.ReadAllBytes(fixturePath), "the committed parameter-sheet animation package drifted");
            Assert.AreEqual(
                $"{sha256}  {FixtureName}\n",
                File.ReadAllText(hashPath).Replace("\r\n", "\n", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void TheVendoredTf100WebCopyIsTheSameArtifact()
    {
        var conformanceDirectory = Tf100WebFixtureTestSupport.FindConformanceDirectory();
        var builderFixture = Path.Combine(conformanceDirectory, FixtureName);
        Assert.IsTrue(File.Exists(builderFixture), "the Builder fixture must exist; run with SCADA_UPDATE_QUICK_WINDOW_PARAMETER_SHEET=1");

        var vendored = Tf100WebFixtureTestSupport.ReadVendoredFixture(FixtureName, out var source);
        if (vendored is null)
        {
            Assert.Inconclusive("TF100Web is not available on this machine; the vendored copy cannot be compared.");
            return;
        }

        CollectionAssert.AreEqual(
            File.ReadAllBytes(builderFixture),
            vendored,
            $"both repositories must execute the identical parameter-sheet animation artifact ({source})");
    }

    private static async Task<byte[]> BuildArchiveAsync(
        ScadaProject project, Ft100ProjectPageExportInput[] pages, string stagingRoot)
    {
        Directory.CreateDirectory(stagingRoot);
        var archivePath = Path.Combine(stagingRoot, FixtureName);
        var result = await new Ft100SceneExporter().ExportProjectArchiveAsync(project, pages, archivePath);
        Assert.IsTrue(result.Validation.IsValid, string.Join(" | ", result.Validation.Errors.Select(e => e.Message)));
        return await File.ReadAllBytesAsync(result.ArchivePath);
    }

    // ---- Arrangement ------------------------------------------------------------------------------------

    private static (ScadaProject Project, IReadOnlyDictionary<Guid, ScadaScene> Scenes) Arrange()
    {
        var quickWindow = Page(QuickWindowPageKey, ScadaPageType.QuickWindow, QuickWindowPageCode, "Popup ventilateur", FanPageElements());
        var caller = Page(CallerPageKey, ScadaPageType.Default, CallerPageCode, "Synoptique",
            OpenCommandElement("open-fan3", "open-3", SheetFan03Key, QuickWindowPageKey, "Ventilateur 3"));

        var project = ScadaProject.CreateDefault("QuickWindowParameterSheetAnimation") with
        {
            ManifestVersion = "2.3",
            HomePageId = CallerPageCode,
            Scenes = [caller.Reference, quickWindow.Reference],
            TagCatalog = Catalog(),
            ParameterSheets = [new ParameterSheet(SheetFan03Key, SheetFan03Code, SheetFan03Name, SheetFan03Text)]
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
        ReadVariableElement("read1", "#1"),
        StateTextElement("state3", "{#3} == 1"),
        TextElement("title4", "#4"),
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

    /// <summary>
    /// A live, continuous tag display (<see cref="ScadaReadVariableRule"/>) with an EMPTY state list:
    /// the state engine's "no rule matched" default path applies <c>DefaultEffect</c> (also empty, no
    /// <c>TextContent</c>), which falls through to <c>_applyReadVariable</c> and writes the tag's own
    /// live value onto <c>[data-scada-text]</c> every evaluation — the one mechanism in the shared
    /// runtime that continuously mirrors a tag's value into visible text, independent of any state
    /// rule. This is what the TF100Web probe pushes a new value through and reads back.
    /// </summary>
    private static ScadaElement ReadVariableElement(string id, string readVariableTagId)
    {
        var element = ScadaElement.CreateText(id, id, 0, 0);
        return element with
        {
            Data = element.Data! with { Text = string.Empty },
            StateConfig = ScadaElementStateConfig.Default with
            {
                ReadVariable = new ScadaReadVariableRule(readVariableTagId)
            }
        };
    }

    /// <summary>
    /// A first-match-wins state list driven by a <c>#3</c> expression: "Actif" sets visible text to
    /// "MARCHE" when true, and the (also visible) default effect sets "ARRET" otherwise. Proves the
    /// alternate animation surface named by the brief (a state class/style effect), independent of the
    /// read-variable element above.
    /// </summary>
    private static ScadaElement StateTextElement(string id, string expressionSource)
    {
        var element = ScadaElement.CreateText(id, id, 0, 0);
        return element with
        {
            Data = element.Data! with { Text = "ARRET" },
            StateConfig = ScadaElementStateConfig.Default with
            {
                States = [new ScadaStateRule($"{id}-state", "Actif", true, ScadaExpression.FromSource(expressionSource),
                    ScadaEffectBlock.Empty with { TextContent = "MARCHE" })],
                DefaultEffect = ScadaEffectBlock.Empty with { TextContent = "ARRET" }
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
        "scada-v2-parameter-sheet-anim-tests",
        [
            new ScadaTagDefinition("tf100.mapping.301", "FAN_03_RUN", Datatype: "Boolean"),
            new ScadaTagDefinition("tf100.mapping.302", "FAN_03_SPEED", Datatype: "Decimal"),
        ],
        "generated-parameter-sheet-anim-tags.json");

    private static string Describe(ParameterSheetLoweringResult result) =>
        string.Join(" | ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}"));
}
