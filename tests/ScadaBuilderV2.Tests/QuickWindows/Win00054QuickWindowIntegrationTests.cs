using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.RuntimeContracts;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.RuntimeContracts;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ModernProjects;
using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Rendering;
using ScadaBuilderV2.Rendering.QuickWindows;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// The win00054 vertical: one motor definition, two independent invocations, end to end.
/// </summary>
/// <remarks>
/// The shape comes from the operator inventory of `win00054` recorded in
/// `tests/conformance/industrial/win00054-quick-window-mapping-audit.json`: a motor window with a name
/// field and four exclusive modes — AUTO, ARRET, MAN, DEPART — plus a close button.
///
/// The mappings do not. That audit is `BLOCKED`: the reference project holds eleven motor mappings and
/// every one is a read-only contactor state, so no motor write command exists to bind a mode button to.
/// Rather than invent entries in the authoritative catalog, this vertical runs on a **synthetic catalog
/// of its own**, declared here and nowhere else. Its ids carry the `synthetic.motor.` prefix precisely so
/// that they can never be mistaken for a `tf100.mapping.*` id and can never be deployed by accident, and
/// `AMR_REF_SCADA_V2` is left untouched.
///
/// What this proves is the chain — authoring, persistence, validation, capability analysis, preview and
/// export — against a window that looks like the real one. What it does not prove is any binding to the
/// real refrigeration PLC, which the audit says is not available at all.
///
/// Decisions: DEC-0047, DEC-0050. Plan: Task 7.1.
/// </remarks>
[TestClass]
public sealed class Win00054QuickWindowIntegrationTests
{
    private const string TagSchema = "scada-v2-win00054-synthetic-tags-v1";
    private const string CallerPageId = "win00054";

    private static readonly Guid DefinitionKey = Guid.Parse("7a054000-0000-4000-8000-000000000001");
    private static readonly Guid CallerPageKey = Guid.Parse("7a054000-0000-4000-8000-0000000000ca");
    private static readonly Guid M101 = Guid.Parse("7a054101-0000-4000-8000-000000000101");
    private static readonly Guid M102 = Guid.Parse("7a054102-0000-4000-8000-000000000102");

    private static readonly Guid Running = Guid.Parse("7a054000-0000-4000-8000-000000000011");
    private static readonly Guid ActiveMode = Guid.Parse("7a054000-0000-4000-8000-000000000012");
    private static readonly Guid CommandAuto = Guid.Parse("7a054000-0000-4000-8000-000000000021");
    private static readonly Guid CommandStop = Guid.Parse("7a054000-0000-4000-8000-000000000022");
    private static readonly Guid CommandManual = Guid.Parse("7a054000-0000-4000-8000-000000000023");
    private static readonly Guid CommandStart = Guid.Parse("7a054000-0000-4000-8000-000000000024");
    private static readonly Guid MotorName = Guid.Parse("7a054000-0000-4000-8000-000000000031");
    private static readonly Guid Precision = Guid.Parse("7a054000-0000-4000-8000-000000000032");

    [TestMethod]
    public void TheDefinitionRedrawsTheOperatorInventoryOfWin00054()
    {
        var definition = MotorDefinition();
        var elements = definition.EffectiveContent.EffectiveElements;

        // Four exclusive modes, the inventory's own labels, and nothing invented beside them.
        CollectionAssert.AreEqual(
            new[] { "AUTO", "ARRET", "MAN", "DEPART" },
            elements.Where(element => element.Id.StartsWith("mode-", StringComparison.Ordinal))
                .Select(element => element.DisplayName).ToArray());

        Assert.IsTrue(elements.Any(element => element.Id == "motor-name"), "the NOM DU MOTEUR field is present");
        Assert.IsTrue(elements.Any(element => element.Id == "close"), "the close button is present");

        // Redrawn on an empty canvas: no geometry, id or override was carried over from the legacy scene.
        Assert.IsFalse(
            elements.Any(element => element.Id.StartsWith("legacy_", StringComparison.Ordinal)),
            "no legacy element id may survive into the definition");
        Assert.AreEqual(ScadaElementKind.Button, elements.Single(element => element.Id == "close").Kind);
    }

    [TestMethod]
    public void EachModeButtonWritesItsOwnPortAndTheStateIsReadOnly()
    {
        var definition = MotorDefinition();
        var members = definition.EffectiveInterfaceMembers.ToDictionary(member => member.MemberKey);

        foreach (var key in new[] { CommandAuto, CommandStop, CommandManual, CommandStart })
        {
            Assert.AreEqual(QuickWindowInterfaceFamily.WriteCommand, members[key].Family);
            Assert.AreEqual(QuickWindowMemberAccess.Write, members[key].Access);
        }

        Assert.AreEqual(QuickWindowInterfaceFamily.ReadState, members[Running].Family);
        Assert.IsTrue(members[Running].Required, "a motor window without its run feedback is not usable");
        Assert.AreEqual(QuickWindowMemberAccess.Read, members[ActiveMode].Access);

        // The two parameters are what make one definition serve two motors.
        Assert.AreEqual(QuickWindowInterfaceFamily.PublicParameter, members[MotorName].Family);
        Assert.AreEqual(QuickWindowDataType.String, members[MotorName].DataType);
        Assert.AreEqual(QuickWindowDataType.Integer, members[Precision].DataType);
    }

    [TestMethod]
    public void TheTwoInvocationsNeverShareATagInEitherDirection()
    {
        var project = Project();
        var first = project.EffectiveQuickWindowInvocations.Single(invocation => invocation.InvocationKey == M101);
        var second = project.EffectiveQuickWindowInvocations.Single(invocation => invocation.InvocationKey == M102);

        var firstTags = first.Bindings!.Where(binding => binding.TagId is not null).Select(binding => binding.TagId!).ToHashSet(StringComparer.Ordinal);
        var secondTags = second.Bindings!.Where(binding => binding.TagId is not null).Select(binding => binding.TagId!).ToHashSet(StringComparer.Ordinal);

        Assert.IsTrue(firstTags.Count > 0 && secondTags.Count > 0);
        Assert.AreEqual(0, firstTags.Intersect(secondTags, StringComparer.Ordinal).Count(),
            "reading or writing one motor must never touch the other");
        Assert.AreEqual(first.DefinitionKey, second.DefinitionKey, "both invocations serve the same definition");

        // The parameters differ, which is the whole point of two invocations over one definition.
        Assert.AreEqual("M101", Literal(first, MotorName));
        Assert.AreEqual("M102", Literal(second, MotorName));
    }

    [TestMethod]
    public void AMissingRequiredPortIsAnErrorAndAMissingOptionalOneIsNot()
    {
        var project = Project();
        var invocation = project.EffectiveQuickWindowInvocations.First();

        var withoutOptional = Rebind(project, invocation.InvocationKey,
            invocation.Bindings!.Where(binding => binding.MemberKey != ActiveMode).ToArray());
        Assert.IsFalse(
            ScadaProjectBuildValidator.Validate(withoutOptional).Any(issue => issue.Code == "quick-window.required-missing"),
            "an unbound optional port is an authoring choice, not a build error");

        var withoutRequired = Rebind(project, invocation.InvocationKey,
            invocation.Bindings!.Where(binding => binding.MemberKey != Running).ToArray());
        Assert.IsTrue(
            ScadaProjectBuildValidator.Validate(withoutRequired).Any(issue => issue.Code == "quick-window.required-missing"),
            "an unbound required port must fail the build");
    }

    [TestMethod]
    public async Task TheVerticalSurvivesSaveAndReopen()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var store = new ModernProjectStore();
        try
        {
            var project = Project();
            var snapshot = new PageWorkspaceSnapshot(
                1,
                project,
                new Dictionary<Guid, ScadaScene> { [CallerPageKey] = CallerScene() },
                Array.Empty<PendingPageDeletion>());
            await store.SaveWorkspaceSnapshotToProjectRootAsync(Path.Combine(root, "proj"), snapshot);

            var reopened = await store.LoadProjectFromRootAsync(Path.Combine(root, "proj"));
            Assert.IsNotNull(reopened);

            var definition = reopened!.EffectiveQuickWindows.Single();
            Assert.AreEqual(DefinitionKey, definition.DefinitionKey);
            Assert.AreEqual(8, definition.EffectiveInterfaceMembers.Count);
            Assert.AreEqual(2, reopened.EffectiveQuickWindowInvocations.Count);

            var first = reopened.EffectiveQuickWindowInvocations.Single(invocation => invocation.InvocationKey == M101);
            Assert.AreEqual("M101", Literal(first, MotorName));
            Assert.AreEqual(
                "synthetic.motor.m101.running",
                first.Bindings!.Single(binding => binding.MemberKey == Running).TagId);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void TheVerticalRequiresOnlyCapabilitiesPromotedInPhase6()
    {
        var analysis = ScadaRuntimeCapabilityAnalyzer.Analyze(Project(), [CallerScene()]);

        Assert.AreEqual(0, analysis.BlockedCapabilities.Count,
            "the vertical must not depend on a capability that is still closed");

        var required = analysis.RequiredCapabilities.Select(capability => capability.Id).ToArray();
        foreach (var expected in new[]
                 {
                     "command.open-quick-window",
                     "quick-window.definition",
                     "quick-window.local-interface.typed",
                     "quick-window.port-binding",
                     "quick-window.port.required",
                     "quick-window.presentation.backdrop",
                 })
        {
            CollectionAssert.Contains(required, expected);
        }

        CollectionAssert.DoesNotContain(required, "quick-window.binding.parent-port");
        CollectionAssert.DoesNotContain(required, "quick-window.nesting.depth-2");
    }

    [TestMethod]
    public async Task TheVerticalExportsAndCarriesBothInvocationsIntoThePackage()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var archivePath = Path.Combine(root, "export", "win00054.sb2");
            var result = await new Ft100SceneExporter().ExportProjectArchiveAsync(
                Project(), [new Ft100ProjectPageExportInput(CallerScene(), null)], archivePath);

            Assert.IsTrue(result.Validation.IsValid,
                string.Join("; ", result.Validation.Errors.Select(error => error.Message)));
            Assert.IsTrue(File.Exists(archivePath));

            var compilation = Rendering.QuickWindows.QuickWindowCompiler.Compile(Project());
            Assert.AreEqual(1, compilation.Definitions.Count);
            Assert.AreEqual(2, compilation.Invocations.Count);
            var namespaceName = compilation.Definitions[0].Namespace;
            Assert.IsTrue(compilation.Files.Any(file => file.RelativeHtmlPath == $"{namespaceName}/{namespaceName}.html"));

            // Both invocations reach the package, each with its own motor name.
            var names = compilation.Invocations
                .Select(invocation => invocation.Bindings.Single(binding => binding.MemberKey == MotorName.ToString("D")).LiteralValue)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "M101", "M102" }, names);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void TheCallerPageOpensBothInvocationsAndTargetsNoPage()
    {
        var scene = CallerScene();
        var commands = scene.Elements
            .SelectMany(element => element.EffectiveCommandConfig.Commands)
            .Where(command => command.Kind == ScadaCommandKind.OpenQuickWindow)
            .ToArray();

        Assert.AreEqual(2, commands.Length);
        CollectionAssert.AreEqual(
            new[] { M101, M102 },
            commands.Select(command => command.QuickWindowInvocationKey!.Value).OrderBy(key => key).ToArray());
        Assert.IsTrue(commands.All(command => command.TargetPageId is null && command.TargetPageKey is null),
            "a quick-window command addresses an invocation, never a page");
    }

    [TestMethod]
    public async Task ReopeningTheSameMotorRecreatesTheInstanceAndTheLastRequestIsTheCurrentOne()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var adapter = new BuilderQuickWindowHostAdapter(root);
            var definition = MotorDefinition();

            var first = await adapter.OpenAsync(definition, M101);
            var again = await adapter.OpenAsync(definition, M101);
            var other = await adapter.OpenAsync(definition, M102);

            // Reopening the same motor does not stack a second window: it recreates, and the newest
            // request is the one the host considers current.
            Assert.AreNotEqual(first.RuntimeInstanceId, again.RuntimeInstanceId);
            Assert.IsTrue(again.Generation > first.Generation);
            Assert.AreEqual(M102, adapter.Current?.InvocationKey, "the last motor opened is the current one");
            Assert.AreEqual(other.Generation, adapter.Generation);

            adapter.Close();
            Assert.IsNull(adapter.Current);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ThePreviewShowsTheMotorNameAndKeepsTestValuesOutOfTheModel()
    {
        var definition = MotorDefinition();
        var document = QuickWindowPreviewDocumentFactory.Create(
            new QuickWindowPreviewInput(definition, M101, TagCatalog: SyntheticCatalog()));

        Assert.AreEqual("qw-7a054000", document.Namespace);
        StringAssert.Contains(document.Html, "class=\"qw-frame\" role=\"dialog\" aria-modal=\"true\"");
        // FR-UI-03: the preview falls back to DisplayName, which the deployed runtime does not yet do.
        StringAssert.Contains(document.Html, "Moteur");
        StringAssert.Contains(document.Html, "AUTO");
        StringAssert.Contains(document.Html, "DEPART");
        Assert.AreEqual(0, document.Warnings.Count);

        // The preview is editor-only: nothing it renders may be found in what the package carries.
        var compiled = Rendering.QuickWindows.QuickWindowCompiler.Compile(Project())
            .Files.Single(file => file.RelativeHtmlPath.StartsWith("qw-7a054000", StringComparison.Ordinal));
        foreach (var editorOnly in new[] { "qw-frame", "qw-backdrop", "data-qw-close", "aria-modal" })
        {
            Assert.IsFalse(compiled.Html.Contains(editorOnly, StringComparison.Ordinal),
                $"'{editorOnly}' is preview chrome and must never reach the package");
        }
    }

    [TestMethod]
    public void RebindingOneMotorAndUndoingRestoresItsExactBindingsAndLeavesTheOtherAlone()
    {
        var project = Project();
        var before = project.EffectiveQuickWindowInvocations.Single(invocation => invocation.InvocationKey == M101);
        var otherBefore = project.EffectiveQuickWindowInvocations.Single(invocation => invocation.InvocationKey == M102);

        // Rebind M101 onto the other motor's start command, which is exactly the mistake undo exists for.
        var mistaken = Rebind(project, M101,
            before.Bindings!.Select(binding => binding.MemberKey == CommandStart
                ? QuickWindowBinding.FromTag(CommandStart, "synthetic.motor.m102.start")
                : binding).ToArray());
        var after = mistaken.EffectiveQuickWindowInvocations.Single(invocation => invocation.InvocationKey == M101);
        Assert.AreEqual("synthetic.motor.m102.start", after.Bindings!.Single(binding => binding.MemberKey == CommandStart).TagId);

        var undone = Rebind(mistaken, M101, before.Bindings!);
        var restored = undone.EffectiveQuickWindowInvocations.Single(invocation => invocation.InvocationKey == M101);

        CollectionAssert.AreEqual(
            before.Bindings!.Select(binding => $"{binding.MemberKey}:{binding.SourceKind}:{binding.TagId}:{binding.LiteralValue}").ToArray(),
            restored.Bindings!.Select(binding => $"{binding.MemberKey}:{binding.SourceKind}:{binding.TagId}:{binding.LiteralValue}").ToArray(),
            "undo restores the exact bindings, not an equivalent set");
        CollectionAssert.AreEqual(
            otherBefore.Bindings!.Select(binding => binding.TagId).ToArray(),
            undone.EffectiveQuickWindowInvocations.Single(invocation => invocation.InvocationKey == M102).Bindings!.Select(binding => binding.TagId).ToArray(),
            "the other motor was never touched");
    }

    private static string? Literal(QuickWindowInvocation invocation, Guid memberKey) =>
        invocation.Bindings!.Single(binding => binding.MemberKey == memberKey).LiteralValue;

    private static ScadaProject Rebind(ScadaProject project, Guid invocationKey, IReadOnlyList<QuickWindowBinding> bindings) =>
        project with
        {
            QuickWindowInvocations = project.EffectiveQuickWindowInvocations
                .Select(invocation => invocation.InvocationKey == invocationKey ? invocation with { Bindings = bindings } : invocation)
                .ToArray()
        };

    /// <summary>
    /// The synthetic catalog. Every id carries the `synthetic.motor.` prefix so it cannot be confused with,
    /// or deployed as, a `tf100.mapping.*` id from a real project.
    /// </summary>
    private static ScadaTagCatalog SyntheticCatalog() => new(
        TagSchema,
        [
            new ScadaTagDefinition("synthetic.motor.m101.running", "M101 run feedback", Datatype: "Boolean", Writeable: false),
            new ScadaTagDefinition("synthetic.motor.m101.mode", "M101 active mode", Datatype: "Integer", Writeable: false),
            new ScadaTagDefinition("synthetic.motor.m101.auto", "M101 auto", Datatype: "Boolean", Writeable: true),
            new ScadaTagDefinition("synthetic.motor.m101.stop", "M101 stop", Datatype: "Boolean", Writeable: true),
            new ScadaTagDefinition("synthetic.motor.m101.manual", "M101 manual", Datatype: "Boolean", Writeable: true),
            new ScadaTagDefinition("synthetic.motor.m101.start", "M101 start", Datatype: "Boolean", Writeable: true),
            new ScadaTagDefinition("synthetic.motor.m102.running", "M102 run feedback", Datatype: "Boolean", Writeable: false),
            new ScadaTagDefinition("synthetic.motor.m102.mode", "M102 active mode", Datatype: "Integer", Writeable: false),
            new ScadaTagDefinition("synthetic.motor.m102.auto", "M102 auto", Datatype: "Boolean", Writeable: true),
            new ScadaTagDefinition("synthetic.motor.m102.stop", "M102 stop", Datatype: "Boolean", Writeable: true),
            new ScadaTagDefinition("synthetic.motor.m102.manual", "M102 manual", Datatype: "Boolean", Writeable: true),
            new ScadaTagDefinition("synthetic.motor.m102.start", "M102 start", Datatype: "Boolean", Writeable: true),
        ],
        "generated-win00054-synthetic-tags.json");

    private static QuickWindowDefinition MotorDefinition() => new(
        DefinitionKey,
        "moteur",
        "Moteur",
        InterfaceVersion: 1,
        new VisualContent(
            new CanvasSize(260, 190),
            Elements:
            [
                ScadaElement.CreateText("motor-name", "NOM DU MOTEUR", 65, 2),
                ScadaElement.CreateShape("run-indicator", "Etat", ScadaShapeKind.Rectangle, 5, 5),
                ScadaElement.CreateButton("mode-auto", "AUTO", 15, 38),
                ScadaElement.CreateButton("mode-stop", "ARRET", 134, 38),
                ScadaElement.CreateButton("mode-manual", "MAN", 15, 85),
                ScadaElement.CreateButton("mode-start", "DEPART", 132, 84),
                ScadaElement.CreateButton("close", "Fermer", 82, 141) with
                {
                    CommandConfig = new ScadaElementCommandConfig(
                    [
                        new ScadaCommandBinding("close-command", "Fermer", true, ScadaCommandTrigger.OnClick,
                            ScadaCommandKind.CloseQuickWindow)
                    ])
                }
            ]),
        [
            new QuickWindowInterfaceMember(Running, "Running", QuickWindowInterfaceFamily.ReadState,
                QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read, Required: true),
            new QuickWindowInterfaceMember(ActiveMode, "ActiveMode", QuickWindowInterfaceFamily.ReadState,
                QuickWindowDataType.Integer, QuickWindowMemberAccess.Read),
            new QuickWindowInterfaceMember(CommandAuto, "CommandAuto", QuickWindowInterfaceFamily.WriteCommand,
                QuickWindowDataType.Boolean, QuickWindowMemberAccess.Write),
            new QuickWindowInterfaceMember(CommandStop, "CommandStop", QuickWindowInterfaceFamily.WriteCommand,
                QuickWindowDataType.Boolean, QuickWindowMemberAccess.Write),
            new QuickWindowInterfaceMember(CommandManual, "CommandManual", QuickWindowInterfaceFamily.WriteCommand,
                QuickWindowDataType.Boolean, QuickWindowMemberAccess.Write),
            new QuickWindowInterfaceMember(CommandStart, "CommandStart", QuickWindowInterfaceFamily.WriteCommand,
                QuickWindowDataType.Boolean, QuickWindowMemberAccess.Write),
            new QuickWindowInterfaceMember(MotorName, "MotorName", QuickWindowInterfaceFamily.PublicParameter,
                QuickWindowDataType.String, QuickWindowMemberAccess.Read),
            new QuickWindowInterfaceMember(Precision, "Precision", QuickWindowInterfaceFamily.PublicParameter,
                QuickWindowDataType.Integer, QuickWindowMemberAccess.Read),
        ],
        new QuickWindowPresentationDefaults(Title: "Moteur"));

    /// <summary>One invocation per motor: same definition, disjoint tags, its own name and precision.</summary>
    private static QuickWindowInvocation Invocation(Guid invocationKey, string motor, string callerElementId) => new(
        invocationKey,
        DefinitionKey,
        [
            QuickWindowBinding.FromTag(Running, $"synthetic.motor.{motor.ToLowerInvariant()}.running"),
            QuickWindowBinding.FromTag(ActiveMode, $"synthetic.motor.{motor.ToLowerInvariant()}.mode"),
            QuickWindowBinding.FromTag(CommandAuto, $"synthetic.motor.{motor.ToLowerInvariant()}.auto"),
            QuickWindowBinding.FromTag(CommandStop, $"synthetic.motor.{motor.ToLowerInvariant()}.stop"),
            QuickWindowBinding.FromTag(CommandManual, $"synthetic.motor.{motor.ToLowerInvariant()}.manual"),
            QuickWindowBinding.FromTag(CommandStart, $"synthetic.motor.{motor.ToLowerInvariant()}.start"),
            QuickWindowBinding.FromLiteral(MotorName, motor),
            QuickWindowBinding.FromLiteral(Precision, "1"),
        ],
        InterfaceVersion: 1,
        OwnerPageKey: CallerPageKey,
        OwnerElementId: callerElementId,
        OwnerCommandId: $"open-{motor.ToLowerInvariant()}");

    private static ScadaScene CallerScene() =>
        ScadaScene.CreateEmpty(CallerPageId, "Moteurs", new CanvasSize(1280, 873)) with
        {
            PageKey = CallerPageKey,
            PageCode = CallerPageId,
            Elements =
            [
                Caller("caller-m101", "M101", M101),
                Caller("caller-m102", "M102", M102),
            ]
        };

    private static ScadaElement Caller(string id, string motor, Guid invocationKey) =>
        ScadaElement.CreateButton(id, motor, 20, 20) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding($"open-{motor.ToLowerInvariant()}", $"Ouvrir {motor}", true,
                    ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
                    QuickWindowInvocationKey: invocationKey)
            ])
        };

    private static ScadaProject Project()
    {
        var scene = CallerScene();
        return ScadaProject.CreateDefault("Win00054MotorVertical") with
        {
            ManifestVersion = "2.3",
            HomePageId = CallerPageId,
            Scenes =
            [
                // The reference carries the persistence path; the exporter derives the package path itself.
                new ScadaSceneReference(scene.Id, scene.Title, $"scenes/{scene.Id}.scene.json",
                    PageKey: scene.PageKey, PageCode: scene.EffectivePageCode)
            ],
            TagCatalog = SyntheticCatalog(),
            QuickWindows = [MotorDefinition()],
            QuickWindowInvocations = [Invocation(M101, "M101", "caller-m101"), Invocation(M102, "M102", "caller-m102")]
        };
    }
}
