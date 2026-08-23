using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Rendering;
using ScadaBuilderV2.Rendering.QuickWindows;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Locks the editor-only quick-window instance preview: namespaced DOM/CSS, minimal chrome with backdrop,
/// `X` and `Escape`, the shared host manager adapted from the frozen prototype, monotonic generations and
/// the guarantee that test-bench data never leaves the preview.
/// Decisions: DEC-0050, FR-019, FR-020, FR-021, FR-022, FR-UI-07, FR-UI-08, FR-UI-21. Plan: Task 3.4.
/// </summary>
[TestClass]
public sealed class QuickWindowPreviewTests
{
    private static readonly Guid DefinitionKey = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherDefinitionKey = Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa");
    private static readonly Guid RunningKey = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");

    [TestMethod]
    public void ThePreviewRendersOneNamespacedInstanceWithItsMinimalChrome()
    {
        var document = QuickWindowPreviewDocumentFactory.Create(new QuickWindowPreviewInput(Definition(), Guid.NewGuid()));

        Assert.AreEqual("qw-11111111", document.Namespace);
        Assert.AreEqual("qw-motor", document.InstanceCode);
        StringAssert.Contains(document.Html, "<template id=\"qw-11111111__template\">");
        StringAssert.Contains(document.Html, "class=\"qw-frame\" role=\"dialog\" aria-modal=\"true\"");
        StringAssert.Contains(document.Html, "data-qw-close=\"self\"");
        StringAssert.Contains(document.Html, "aria-label=\"Fermer\"");
        StringAssert.Contains(document.Css, ".qw-backdrop");
        StringAssert.Contains(document.Css, "[data-qw-def=\"qw-11111111\"] .qw-frame");
        StringAssert.Contains(document.Html, "id=\"ft100-qw-motor\"", "the instance content reuses the exported Element+ geometry");
        Assert.AreEqual(0, document.Warnings.Count);
    }

    [TestMethod]
    public void TwoDefinitionsSharingAuthorIdsStayIsolatedByTheirNamespace()
    {
        var first = QuickWindowPreviewDocumentFactory.Create(new QuickWindowPreviewInput(Definition(), Guid.NewGuid()));
        var second = QuickWindowPreviewDocumentFactory.Create(new QuickWindowPreviewInput(
            Definition() with { DefinitionKey = OtherDefinitionKey, Code = "pump", DisplayName = "Pump" },
            Guid.NewGuid()));

        Assert.AreNotEqual(first.Namespace, second.Namespace);
        Assert.AreNotEqual(first.InstanceCode, second.InstanceCode);
        StringAssert.Contains(first.Html, "id=\"ft100-qw-motor__sensor\"");
        StringAssert.Contains(second.Html, "id=\"ft100-qw-pump__sensor\"");
        Assert.IsFalse(second.Html.Contains("ft100-qw-motor", StringComparison.Ordinal), "no id may leak between two definitions");
        Assert.IsFalse(second.Css.Contains("qw-11111111", StringComparison.Ordinal), "no CSS rule may leak between two definitions");
    }

    [TestMethod]
    public void TestBenchValuesStayInsideThePreviewAndNeverReachTheModelOrTheExport()
    {
        var definition = Definition();
        var bench = new QuickWindowTestBenchValues(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["motor.run"] = "true" },
            [QuickWindowBinding.FromLiteral(RunningKey, "42")],
            "Moteur d'essai");

        var document = QuickWindowPreviewDocumentFactory.Create(new QuickWindowPreviewInput(definition, Guid.NewGuid(), bench));

        StringAssert.Contains(document.Html, QuickWindowPreviewDocumentFactory.TestValuesAttribute);
        StringAssert.Contains(document.Html, "motor.run");
        StringAssert.Contains(document.Html, "Moteur d&#x27;essai");
        Assert.AreEqual(0, definition.EffectiveInterfaceMembers.Count(member => member.DefaultValue == "42"), "the bench never mutates the definition");

        var scene = definition.EffectiveContent.ToScene("win00003", "win00003", Guid.NewGuid(), "win00003");
        var reference = new ScadaSceneReference("win00003", "win00003", "scenes/win00003.scene.json", PageKey: Guid.NewGuid(), PageCode: "win00003");
        var exported = NativePageDocumentFactory.Create(new PageDocumentInput(reference, scene));
        Assert.IsFalse(
            exported.Html.Contains(QuickWindowPreviewDocumentFactory.TestValuesAttribute, StringComparison.Ordinal),
            "editor-only test data must never appear in an exportable document");
        Assert.IsFalse(exported.Html.Contains("qw-frame", StringComparison.Ordinal), "preview chrome must never be exported");
    }

    [TestMethod]
    public void ThePreviewScriptCarriesTheSharedRuntimeAndTheHostManagerButTheExportBundleDoesNot()
    {
        var script = QuickWindowPreviewDocumentFactory.BuildScript();

        StringAssert.Contains(script, "ScadaRuntime.QuickWindowHost");
        StringAssert.Contains(script, "ScadaRuntime.TagBridge");
        StringAssert.Contains(script, "ScadaRuntime.StateEngine");
        Assert.IsFalse(
            Ft100SceneExporter.GetRuntimeScript().Contains("QuickWindowHost", StringComparison.Ordinal),
            "quick-window runtime capabilities stay Blocked: the export bundle must not carry the host manager");
    }

    [TestMethod]
    public void TheHostManagerOwnsTheInstanceSemanticsAndTheAdapterNeverDuplicatesThem()
    {
        var host = ReadRuntimeModule("quick-window-host.js");

        foreach (var contract in new[]
                 {
                     "SinglePerDefinition", "stale-hydration-rejected", "depth-exceeded", "cycle",
                     "data-qw-generation", "closeSelf", "Escape"
                 })
        {
            StringAssert.Contains(host, contract, $"the host manager must implement '{contract}'.");
        }

        StringAssert.Contains(host, "ScadaRuntime.initPage", "mounting delegates to the shared runtime");
        StringAssert.Contains(host, "ScadaRuntime.disposePage", "disposal delegates to the shared runtime");

        var adapter = ReadAppFile(Path.Combine("QuickWindows", "BuilderQuickWindowHostAdapter.cs"));
        foreach (var forbidden in new[] { "SinglePerDefinition policy", "bringToFront", "disposeInstance" })
        {
            Assert.IsFalse(
                adapter.Contains(forbidden, StringComparison.Ordinal),
                $"'{forbidden}' belongs to the shared host manager, never to the Builder adapter.");
        }
    }

    [TestMethod]
    public async Task EveryPreviewRequestReceivesAStrictlyMonotonicGenerationAndItsOwnInstanceId()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var adapter = new BuilderQuickWindowHostAdapter(root);
            var definition = Definition();
            var invocation = Guid.NewGuid();

            var first = await adapter.OpenAsync(definition, invocation);
            var second = await adapter.OpenAsync(definition, invocation);
            var third = await adapter.OpenAsync(definition, Guid.NewGuid());

            Assert.AreEqual(1, first.Generation);
            Assert.AreEqual(2, second.Generation);
            Assert.AreEqual(3, third.Generation);
            Assert.AreNotEqual(first.RuntimeInstanceId, second.RuntimeInstanceId, "each request carries its own temporary instance id");
            Assert.AreEqual(third.Generation, adapter.Generation);
            Assert.AreEqual(third.InvocationKey, adapter.Current?.InvocationKey);

            adapter.Close();
            Assert.IsNull(adapter.Current);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ThePreviewIsMaterializedInsideTheEditorPreviewRootOnly()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var definition = Definition();
            var document = await PreviewDocument.MaterializeQuickWindowAsync(
                new QuickWindowPreviewInput(definition, Guid.NewGuid()),
                root);

            var htmlPath = document.GetSourcePath(root);
            StringAssert.StartsWith(htmlPath, Path.GetFullPath(root), "the preview never escapes its root");
            Assert.IsTrue(File.Exists(htmlPath));
            Assert.IsTrue(File.Exists(Path.Combine(Path.GetDirectoryName(htmlPath)!, "css", "qw-motor.css")));
            Assert.IsTrue(File.Exists(Path.Combine(Path.GetDirectoryName(htmlPath)!, QuickWindowPreviewDocumentFactory.ScriptFileName)));
            StringAssert.Contains(await File.ReadAllTextAsync(htmlPath), "QuickWindowHost.open");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void TheTestBenchProducesTransientValuesAndNeverTouchesTheDefinition()
    {
        var definition = Definition();
        var bench = new QuickWindowTestBenchViewModel();

        bench.Load(definition);

        Assert.AreEqual(1, bench.Rows.Count, "only public ports are exercised by the bench");
        var row = bench.Rows[0];
        row.TemporaryTagId = "motor.run";
        row.TemporaryValue = "true";
        bench.TitleOverride = "Moteur d'essai";

        var values = bench.ToTestBenchValues();

        Assert.AreEqual("true", values.EffectiveTagValues["motor.run"]);
        Assert.AreEqual(QuickWindowBindingSourceKind.Tag, values.EffectiveBindings.Single().SourceKind);
        Assert.AreEqual("Moteur d'essai", values.TitleOverride);
        Assert.AreEqual(1, definition.EffectiveInterfaceMembers.Count);
        Assert.IsNull(definition.EffectiveInterfaceMembers[0].DefaultValue, "the bench never writes into the durable interface");

        bench.Clear();
        Assert.AreEqual(0, bench.Rows.Count);
        Assert.IsFalse(bench.HasDefinition);
        Assert.IsTrue(bench.ToTestBenchValues().IsEmpty);
    }

    private static QuickWindowDefinition Definition() =>
        new(
            DefinitionKey,
            "motor",
            "Motor",
            1,
            new VisualContent(
                new CanvasSize(480, 320),
                Elements: [ScadaElement.CreateText("sensor", "Sensor", 10, 20)]),
            [
                new QuickWindowInterfaceMember(RunningKey, "Running", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read, Required: true)
            ],
            new QuickWindowPresentationDefaults());

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "scada-quick-window-preview", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string ReadRuntimeModule(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "ScadaBuilderV2.Rendering", "Runtime", fileName);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate Runtime/{fileName}.");
        return string.Empty;
    }

    private static string ReadAppFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "ScadaBuilderV2.App", relativePath);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate src/ScadaBuilderV2.App/{relativePath}.");
        return string.Empty;
    }
}
