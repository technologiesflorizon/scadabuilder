using System.Text.Json;
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
/// Locks the quick-window compilation and the structural export gate: deterministic registries and content
/// paths, namespaced ids and CSS, no editor-only data, and a public export that leaves zero artifact while a
/// quick-window capability is `Blocked`.
/// Decisions: DEC-0047, DEC-0050, FR-019, FR-020. Plan: Task 4.2.
/// </summary>
[TestClass]
public sealed class QuickWindowExporterTests
{
    private static readonly Guid DefinitionKey = Guid.Parse("a1b2c3d4-1111-4222-8333-aaaaaaaaaaaa");
    private static readonly Guid OtherDefinitionKey = Guid.Parse("e5f6a7b8-2222-4333-8444-bbbbbbbbbbbb");
    private static readonly Guid RunningKey = Guid.Parse("11112222-3333-4444-5555-666677778888");
    private static readonly Guid SpeedKey = Guid.Parse("99998888-7777-6666-5555-444433332222");
    private static readonly Guid InvocationKey = Guid.Parse("cccccccc-dddd-eeee-ffff-000011112222");

    [TestMethod]
    public void TheCompilerEmitsTheContractualPathsAndNamespacedContent()
    {
        var compilation = QuickWindowCompiler.Compile(Project());

        var definition = compilation.Definitions.Single(entry => entry.Code == "moteur");
        Assert.AreEqual("qw-a1b2c3d4", definition.Namespace);
        Assert.AreEqual("qw-a1b2c3d4/qw-a1b2c3d4.html", definition.RelativePath);
        Assert.AreEqual("qw-a1b2c3d4/css/qw-a1b2c3d4.css", definition.CssRelativePath);
        Assert.AreEqual(480, definition.Width);
        Assert.AreEqual(320, definition.Height);

        var file = compilation.Files.Single(item => item.RelativeHtmlPath == definition.RelativePath);
        StringAssert.Contains(file.Html, "id=\"ft100-qw-a1b2c3d4\"");
        StringAssert.Contains(file.Html, "id=\"ft100-qw-a1b2c3d4__sensor\"");
        StringAssert.Contains(file.Html, "href=\"css/qw-a1b2c3d4.css\"");
        StringAssert.Contains(file.Css, "#ft100-qw-a1b2c3d4");
    }

    [TestMethod]
    public void TwoDefinitionsSharingAuthorIdsNeverShareADomIdOrACssRule()
    {
        var compilation = QuickWindowCompiler.Compile(Project());

        var first = compilation.Files.Single(file => file.RelativeHtmlPath.StartsWith("qw-a1b2c3d4", StringComparison.Ordinal));
        var second = compilation.Files.Single(file => file.RelativeHtmlPath.StartsWith("qw-e5f6a7b8", StringComparison.Ordinal));

        Assert.IsFalse(second.Html.Contains("ft100-qw-a1b2c3d4", StringComparison.Ordinal));
        Assert.IsFalse(second.Css.Contains("qw-a1b2c3d4", StringComparison.Ordinal));
        StringAssert.Contains(first.Html, "id=\"ft100-qw-a1b2c3d4__sensor\"");
        StringAssert.Contains(second.Html, "id=\"ft100-qw-e5f6a7b8__sensor\"");
    }

    [TestMethod]
    public void EveryRegistryIsOrderedByItsDurableKey()
    {
        var compilation = QuickWindowCompiler.Compile(Project());

        CollectionAssert.AreEqual(
            compilation.Definitions.Select(entry => entry.DefinitionKey).OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            compilation.Definitions.Select(entry => entry.DefinitionKey).ToArray(),
            "definitions are ordered by DefinitionKey");
        CollectionAssert.AreEqual(
            compilation.Invocations.Select(entry => entry.InvocationKey).OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            compilation.Invocations.Select(entry => entry.InvocationKey).ToArray(),
            "invocations are ordered by InvocationKey");

        var members = compilation.Definitions.Single(entry => entry.Code == "moteur").InterfaceMembers;
        CollectionAssert.AreEqual(
            members.Select(member => member.MemberKey).OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            members.Select(member => member.MemberKey).ToArray(),
            "members are ordered by MemberKey");

        var bindings = compilation.Invocations.Single().Bindings;
        CollectionAssert.AreEqual(
            bindings.Select(binding => binding.MemberKey).OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            bindings.Select(binding => binding.MemberKey).ToArray(),
            "bindings are ordered by MemberKey");
    }

    [TestMethod]
    public void TheCompilationIsByteDeterministic()
    {
        var first = JsonSerializer.Serialize(QuickWindowCompiler.Compile(Project()));
        var second = JsonSerializer.Serialize(QuickWindowCompiler.Compile(Project()));

        Assert.AreEqual(first, second, "two compilations of the same project must be identical");
    }

    [TestMethod]
    public void ACommandReferencesItsInvocationKeyAndNeverAPageTarget()
    {
        var compilation = QuickWindowCompiler.Compile(Project());
        var invocation = compilation.Invocations.Single();

        Assert.AreEqual(InvocationKey.ToString("D"), invocation.InvocationKey);
        Assert.AreEqual(DefinitionKey.ToString("D"), invocation.DefinitionKey);
        Assert.AreEqual("caller", invocation.OwnerElementId);
        Assert.AreEqual("open", invocation.OwnerCommandId);
        Assert.AreEqual(2, invocation.Bindings.Count);
        Assert.AreEqual("Tag", invocation.Bindings.Single(binding => binding.MemberKey == RunningKey.ToString("D")).SourceKind);
        Assert.AreEqual("Literal", invocation.Bindings.Single(binding => binding.MemberKey == SpeedKey.ToString("D")).SourceKind);
    }

    [TestMethod]
    public void CompiledContentCarriesNoEditorOnlyArtifact()
    {
        var compilation = QuickWindowCompiler.Compile(Project());

        foreach (var file in compilation.Files)
        {
            foreach (var marker in new[] { "data-qw-test-values", "qw-frame", "qw-backdrop", "qw-preview", "data-qw-inst" })
            {
                Assert.IsFalse(
                    file.Html.Contains(marker, StringComparison.Ordinal),
                    $"'{marker}' is editor-only and must never be compiled into a package.");
            }
        }
    }

    [TestMethod]
    public async Task AProjectUsingOnlyPromotedQuickWindowCapabilitiesExports()
    {
        var root = TemporaryRoot();
        try
        {
            var archivePath = Path.Combine(root, "export", "package.sb2");
            var exporter = new Ft100SceneExporter();

            var result = await exporter.ExportProjectArchiveAsync(
                Project(), [new Ft100ProjectPageExportInput(CallerScene(), null)], archivePath);

            Assert.IsTrue(result.Validation.IsValid,
                string.Join("; ", result.Validation.Errors.Select(error => error.Message)));
            Assert.IsTrue(File.Exists(archivePath), "a project on promoted capabilities produces its archive");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task AProjectTouchingAStillBlockedQuickWindowCapabilityLeavesZeroArtifact()
    {
        // The promotion was partial, and this is the half that stayed shut. Parent-port forwarding has no
        // host evidence, so a project that forwards a parent port must still be refused before anything is
        // written -- not merely fail validation after producing a package.
        var root = TemporaryRoot();
        try
        {
            var archivePath = Path.Combine(root, "export", "package.sb2");
            var exporter = new Ft100SceneExporter();

            var failure = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                exporter.ExportProjectArchiveAsync(
                    ParentPortProject(), [new Ft100ProjectPageExportInput(CallerScene(), null)], archivePath));

            StringAssert.Contains(failure.Message, "quick-window.binding.parent-port");
            Assert.IsFalse(File.Exists(archivePath), "a blocked export writes no archive");
            Assert.IsFalse(
                Directory.Exists(Path.Combine(root, "export")),
                "a blocked export creates no directory at all, not even an empty one");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task TheDirectoryExportIsBlockedBeforeAnyStagingDirectoryExists()
    {
        var root = TemporaryRoot();
        try
        {
            var exportDirectory = Path.Combine(root, "package");
            var exporter = new Ft100SceneExporter();

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                exporter.ExportProjectAsync(ParentPortProject(), [new Ft100ProjectPageExportInput(CallerScene(), null)], exportDirectory));

            Assert.IsFalse(Directory.Exists(exportDirectory), "the staging directory is created after the gate, never before");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void EveryPromotedQuickWindowCapabilityIsRequiredAndOpensStrictExport()
    {
        string[] promoted =
        [
            "quick-window.definition",
            "quick-window.local-interface.typed",
            "quick-window.port.required",
            "quick-window.port-binding",
            "quick-window.presentation.backdrop",
            "quick-window.instance.single-per-definition",
            "quick-window.lifecycle.host-owned",
            "quick-window.dom.scoped-root"
        ];

        var analysis = ScadaRuntimeCapabilityAnalyzer.Analyze(Project(), [CallerScene()]);
        var required = analysis.RequiredCapabilities.ToDictionary(item => item.Id, StringComparer.Ordinal);

        foreach (var capability in promoted)
        {
            Assert.IsTrue(required.TryGetValue(capability, out var entry),
                $"'{capability}' must still be derived from this project.");
            Assert.AreEqual(ScadaRuntimeCapabilityStatus.Supported, entry!.Status, capability);
        }

        Assert.AreEqual(0, analysis.BlockedCapabilities.Count,
            "a project on promoted capabilities alone leaves nothing closing strict export");
    }

    [TestMethod]
    public void NoProductPathExposesABypassOfTheStructuralGate()
    {
        // Only executable lines are scanned: prose may name a forbidden bypass to forbid it.
        var exporter = ExecutableLines(ReadRenderingFile("Ft100SceneExporter.cs"));
        var compiler = ExecutableLines(ReadRenderingFile(Path.Combine("QuickWindows", "QuickWindowCompiler.cs")));

        foreach (var bypass in new[] { "allowBlocked", "AllowBlocked", "ignoreCapabilities", "SCADA_ALLOW_BLOCKED", "skipCapabilityGate" })
        {
            Assert.IsFalse(exporter.Contains(bypass, StringComparison.Ordinal), $"'{bypass}' would bypass the capability gate.");
            Assert.IsFalse(compiler.Contains(bypass, StringComparison.Ordinal), $"'{bypass}' would bypass the capability gate.");
        }

        StringAssert.Contains(compiler, "internal static class QuickWindowCompiler",
            "the compiler stays internal so no product caller can reach it.");

        var archiveIndex = exporter.IndexOf("public async Task<Ft100ProjectArchiveExportResult> ExportProjectArchiveAsync", StringComparison.Ordinal);
        Assert.AreNotEqual(-1, archiveIndex);
        var gateIndex = exporter.IndexOf("EnsureProjectExportable(project, pages, manifestProfile);", archiveIndex, StringComparison.Ordinal);
        var firstDirectoryIndex = exporter.IndexOf("Directory.CreateDirectory(archiveDirectory);", archiveIndex, StringComparison.Ordinal);
        Assert.AreNotEqual(-1, gateIndex, "the archive entry point must run the gate itself");
        Assert.IsTrue(gateIndex < firstDirectoryIndex, "the gate must run before any directory is created");
    }

    private static string ExecutableLines(string source) => string.Join(
        Environment.NewLine,
        source
            .ReplaceLineEndings("\n")
            .Split('\n')
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    private static string TemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "scada-quick-window-export", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static ScadaScene CallerScene()
    {
        var command = new ScadaCommandBinding(
            "open",
            "open",
            true,
            ScadaCommandTrigger.OnClick,
            ScadaCommandKind.OpenQuickWindow,
            QuickWindowInvocationKey: InvocationKey);
        var caller = ScadaElement.CreateButton("caller", "caller", 10, 20, ScadaButtonKind.Command) with
        {
            CommandConfig = new ScadaElementCommandConfig([command])
        };
        return ScadaScene.CreateEmpty("win00003", "win00003", CanvasSize.DefaultDesktop) with
        {
            PageKey = Guid.Parse("dddddddd-eeee-ffff-0000-111122223333"),
            PageCode = "win00003",
            Elements = [caller],
            IncludeInBuild = true
        };
    }

    private static QuickWindowDefinition Definition(Guid key, string code) =>
        new(
            key,
            code,
            code,
            1,
            new VisualContent(
                new CanvasSize(480, 320),
                Elements: [ScadaElement.CreateText("sensor", "Sensor", 10, 20)]),
            [
                new QuickWindowInterfaceMember(RunningKey, "Running", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read, Required: true),
                new QuickWindowInterfaceMember(SpeedKey, "Speed", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.Decimal, QuickWindowMemberAccess.Read)
            ],
            new QuickWindowPresentationDefaults());

    private static ScadaProject Project()
    {
        var scene = CallerScene();
        return ScadaProject.CreateDefault("QuickWindowExport") with
        {
            ManifestVersion = "2.3",
            Scenes =
            [
                new ScadaSceneReference(scene.Id, scene.Title, $"{scene.Id}/{scene.Id}.html", PageKey: scene.PageKey, PageCode: scene.EffectivePageCode)
            ],
            // A tag binding is only validatable against a catalog. Before Phase 6 the capability gate
            // refused this project long before the binding was reached, so the omission never showed.
            TagCatalog = new ScadaTagCatalog(
                "quick-window-export-tags-v1",
                [new ScadaTagDefinition("motor.run", "Motor running", Datatype: "Boolean", Writeable: false)],
                "generated-quick-window-export-tags.json"),
            QuickWindows = [Definition(OtherDefinitionKey, "pompe"), Definition(DefinitionKey, "moteur")],
            QuickWindowInvocations =
            [
                new QuickWindowInvocation(
                    InvocationKey,
                    DefinitionKey,
                    [
                        QuickWindowBinding.FromLiteral(SpeedKey, "12"),
                        QuickWindowBinding.FromTag(RunningKey, "motor.run")
                    ],
                    InterfaceVersion: 1,
                    OwnerPageKey: scene.PageKey,
                    OwnerElementId: "caller",
                    OwnerCommandId: "open")
            ]
        };
    }


    /// <summary>Builds the same project with one parent-port binding, the capability Phase 6 left Blocked.</summary>
    private static ScadaProject ParentPortProject()
    {
        var project = Project();
        var invocation = project.QuickWindowInvocations![0];
        return project with
        {
            QuickWindowInvocations =
            [
                invocation with
                {
                    Bindings = [.. invocation.Bindings!, QuickWindowBinding.FromParentPort(RunningKey, SpeedKey)]
                }
            ]
        };
    }
    private static string ReadRenderingFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "ScadaBuilderV2.Rendering", relativePath);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate src/ScadaBuilderV2.Rendering/{relativePath}.");
        return string.Empty;
    }
}
