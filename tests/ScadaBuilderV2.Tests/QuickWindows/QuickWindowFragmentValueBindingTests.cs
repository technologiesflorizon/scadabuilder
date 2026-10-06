using System.Text.RegularExpressions;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Application.RuntimeContracts;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.RuntimeContracts;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Rendering;
using ScadaBuilderV2.Rendering.QuickWindows;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Locks how a quick-window definition fragment carries the two value-binding sites a page only transports
/// through its manifest objects: <c>Data.TagBinding</c> and a numeric table cell's <c>ValueBindings</c>.
/// </summary>
/// <remarks>
/// On a page, TF100Web derives <c>data-scada-mapping-id</c> from <c>Pages[].Objects[]</c>
/// (<c>_binding_config_from_manifest_object</c>, <c>iter_table_cell_binding_targets</c>) and injects it into the
/// composed fragment server side. A quick-window fragment is served by <c>scada_quick_window_fragment</c>
/// without any injection and its definition has no manifest objects, so those two sites must travel inside the
/// fragment itself as <c>data-scada-read-tag</c>/<c>data-scada-write-tag</c>, the attributes the shared runtime
/// already substitutes (<c>#N</c>) and the host already reads (<c>ScadaTagCache.applyReadBindings</c>).
///
/// Decisions: DEC-0050, DEC-0052.
/// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §11b.2.
/// </remarks>
[TestClass]
public sealed class QuickWindowFragmentValueBindingTests
{
    private static readonly Guid DefinitionKey = Guid.Parse("b7000000-0000-4000-8000-0000000000d1");
    private static readonly Guid QuickWindowPageKey = Guid.Parse("b7000000-0000-4000-8000-0000000000a1");
    private static readonly Guid CallerPageKey = Guid.Parse("b7000000-0000-4000-8000-0000000000c1");
    private static readonly Guid SheetKey = Guid.Parse("b7000000-0000-4000-8000-0000000000f1");
    private const string Namespace = "qw-b7000000";

    [TestMethod]
    public void ADirectTagBindingToARealTagIsEmittedAsItsCanonicalReadTag()
    {
        var html = CompileDefinition(TagBindingText("level", "FAN_03_RUN"));

        Assert.AreEqual("tf100.mapping.301", Attribute(OpeningTag(html, $"ft100-{Namespace}__level"), "data-scada-read-tag"),
            "a TagBinding naming a catalog tag must reach the fragment as the canonical mapping id the host reads");
    }

    [TestMethod]
    public void ReadTagIdWinsOverTagBindingAndACustomElementNeverEmitsItsLibraryFileName()
    {
        var both = TagBindingText("both", "FAN_03_RUN") is var element
            ? element with { Data = element.Data! with { ReadTagId = "tf100.mapping.302" } }
            : null!;
        var custom = new ScadaElement(
            "custom",
            "custom",
            ScadaElementKind.Custom,
            new SceneBounds(0, 0, 40, 40),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData("<svg></svg>", null, null, null, null, null, null, null, "pompe.sep", false));

        var html = CompileDefinition(both, custom);

        Assert.AreEqual("tf100.mapping.302", Attribute(OpeningTag(html, $"ft100-{Namespace}__both"), "data-scada-read-tag"));
        Assert.IsNull(Attribute(OpeningTag(html, $"ft100-{Namespace}__custom"), "data-scada-read-tag"),
            "a Custom element's TagBinding records its source .sep, never a tag");
    }

    [TestMethod]
    public void ABoundNumericTableCellCarriesItsReadAndWriteTagsOnItsOwnCell()
    {
        var html = CompileDefinition(NumericTable("table", "tf100.mapping.302", "tf100.mapping.303"));

        var cell = OpeningTag(html, $"ft100-{Namespace}__table__cell-0-0");
        Assert.AreEqual("tf100.mapping.302", Attribute(cell, "data-scada-read-tag"));
        Assert.AreEqual("tf100.mapping.303", Attribute(cell, "data-scada-write-tag"));
        Assert.IsNull(Attribute(OpeningTag(html, $"ft100-{Namespace}__table"), "data-scada-read-tag"),
            "no binding attribute is ever emitted on the table wrapper (package contract §11)");
    }

    [TestMethod]
    public void TagBindingAndTableCellPlaceholdersLowerAsReadSitesAndReachTheFragmentForMountTimeSubstitution()
    {
        var (project, scenes) = ParameterSheetProject(
            "#5 := {FAN_03_RUN}\n#6 := {FAN_03_SPEED}\n",
            TagBindingText("level", "#5"),
            NumericTable("table", "#6", null));

        var lowered = ParameterSheetLowering.Lower(project, scenes);
        Assert.AreEqual(0, lowered.Diagnostics.Count(d => d.Severity == ScadaBuildValidationSeverity.Error),
            string.Join(" | ", lowered.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));

        var definition = lowered.Project.EffectiveQuickWindows.Single();
        CollectionAssert.AreEquivalent(new[] { "#5", "#6" }, definition.EffectiveInterfaceMembers.Select(m => m.Name).ToArray());
        Assert.IsTrue(definition.EffectiveInterfaceMembers.All(m => m.Access == QuickWindowMemberAccess.Read));

        var compilation = QuickWindowCompiler.Compile(lowered.Project);
        var ns = QuickWindowNamespace.ForDefinition(definition.DefinitionKey);
        var html = compilation.Files.Single().Html;
        Assert.AreEqual("#5", Attribute(OpeningTag(html, $"ft100-{ns}__level"), "data-scada-read-tag"));
        Assert.AreEqual("#6", Attribute(OpeningTag(html, $"ft100-{ns}__table__cell-0-0"), "data-scada-read-tag"));

        var invocation = lowered.Project.EffectiveQuickWindowInvocations.Single();
        Assert.IsTrue(invocation.Bindings!.All(b => b.SourceKind == QuickWindowBindingSourceKind.Tag));
    }

    [TestMethod]
    public void ATagBindingOrTableCellPlaceholderBoundToALiteralIsRefusedAsAReadSite()
    {
        var (project, scenes) = ParameterSheetProject(
            "#5 := \"x\"\n#6 := \"y\"\n",
            TagBindingText("level", "#5"),
            NumericTable("table", "#6", null));

        var lowered = ParameterSheetLowering.Lower(project, scenes);

        var refused = lowered.Diagnostics.Where(d => d.Code == "parameter-sheet.read-not-tag").Select(d => d.Message).ToArray();
        Assert.AreEqual(2, refused.Length, string.Join(" | ", lowered.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));
        Assert.IsTrue(refused.Any(m => m.Contains("#5", StringComparison.Ordinal)));
        Assert.IsTrue(refused.Any(m => m.Contains("#6", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void APageKeepsItsManifestOnlyTransportForBothSites()
    {
        var scene = ScadaScene.CreateEmpty("win_tb", "win_tb", new CanvasSize(400, 200)) with
        {
            Elements = [TagBindingText("level", "FAN_03_RUN"), NumericTable("table", "tf100.mapping.302", null)]
        };

        var html = Ft100SceneExporter.BuildDocumentHtml(scene, "css/win_tb.css", string.Empty, null, Catalog(), new List<string>());

        Assert.IsFalse(html.Contains("data-scada-read-tag", StringComparison.Ordinal),
            "page bytes are unchanged: TF100Web injects the mapping of both sites from the page's manifest objects");
    }

    [TestMethod]
    public void AQuickWindowTagBindingDeclaresTheElementReadBindingCapability()
    {
        var project = ProjectWithDefinition(TagBindingText("level", "FAN_03_RUN"));

        var analysis = ScadaRuntimeCapabilityAnalyzer.Analyze(project, []);

        Assert.IsTrue(analysis.RequiredCapabilities.Any(c => c.Id == ScadaRuntimeCapabilityCatalog.ElementReadBinding.Id),
            "the fragment now carries a host-read data-scada-read-tag, so the negotiated contract must say so");
    }

    // ---- Arrangement ------------------------------------------------------------------------------------

    private static string CompileDefinition(params ScadaElement[] elements) =>
        QuickWindowCompiler.Compile(ProjectWithDefinition(elements)).Files.Single().Html;

    private static ScadaProject ProjectWithDefinition(params ScadaElement[] elements) =>
        ScadaProject.CreateDefault("QuickWindowFragmentBindings") with
        {
            ManifestVersion = "2.3",
            TagCatalog = Catalog(),
            QuickWindows =
            [
                new QuickWindowDefinition(
                    DefinitionKey,
                    "fragment",
                    "fragment",
                    1,
                    new VisualContent(new CanvasSize(400, 200), Elements: elements),
                    [],
                    new QuickWindowPresentationDefaults())
            ]
        };

    private static (ScadaProject Project, IReadOnlyDictionary<Guid, ScadaScene> Scenes) ParameterSheetProject(
        string sheetText, params ScadaElement[] quickWindowElements)
    {
        var quickWindow = Page(QuickWindowPageKey, ScadaPageType.QuickWindow, "popup_tb", quickWindowElements);
        var caller = Page(CallerPageKey, ScadaPageType.Default, "synoptique_tb",
            ScadaElement.CreateButton("open", "Ouvrir", 20, 20) with
            {
                CommandConfig = new ScadaElementCommandConfig(
                [
                    new ScadaCommandBinding("open-cmd", "Ouvrir", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
                        ParameterSheetKey: SheetKey, QuickWindowPageKey: QuickWindowPageKey)
                ])
            });
        var project = ScadaProject.CreateDefault("QuickWindowFragmentBindingsSheets") with
        {
            ManifestVersion = "2.3",
            HomePageId = "synoptique_tb",
            Scenes = [caller.Reference, quickWindow.Reference],
            TagCatalog = Catalog(),
            ParameterSheets = [new ParameterSheet(SheetKey, "sheet_tb", "Feuille", sheetText)]
        };
        return (project, new Dictionary<Guid, ScadaScene>
        {
            [CallerPageKey] = caller.Scene,
            [QuickWindowPageKey] = quickWindow.Scene
        });
    }

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
        return element with { Data = element.Data! with { Text = string.Empty, TagBinding = tagBinding } };
    }

    private static ScadaElement NumericTable(string id, string? readTagId, string? writeTagId)
    {
        var table = ScadaElement.CreateTable(id, id, 0, 40, rows: 1, columns: 1, firstRowIsHeader: false);
        return table with
        {
            Table = table.Table! with
            {
                Cells =
                [
                    new ScadaTableCell(0, 0,
                        Content: new ScadaTableCellContent(ScadaTableCellContentKind.InputNumeric),
                        ValueBindings: new ScadaTableCellValueBindings(readTagId, writeTagId))
                ]
            }
        };
    }

    private static ScadaTagCatalog Catalog() => new(
        "quick-window-fragment-binding-tests",
        [
            new ScadaTagDefinition("tf100.mapping.301", "FAN_03_RUN", Datatype: "Boolean"),
            new ScadaTagDefinition("tf100.mapping.302", "FAN_03_SPEED", Datatype: "Decimal"),
            new ScadaTagDefinition("tf100.mapping.303", "FAN_03_SETPOINT", Datatype: "Decimal", Writeable: true),
        ],
        "generated-quick-window-fragment-binding-tags.json");

    private static string OpeningTag(string html, string domId)
    {
        var match = Regex.Match(html, $"<[a-z]+ id=\"{Regex.Escape(domId)}\"[^>]*>");
        Assert.IsTrue(match.Success, $"no element with id '{domId}' in:\n{html}");
        return match.Value;
    }

    private static string? Attribute(string openingTag, string name)
    {
        var match = Regex.Match(openingTag, $"\\s{Regex.Escape(name)}=\"([^\"]*)\"");
        return match.Success ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }
}
