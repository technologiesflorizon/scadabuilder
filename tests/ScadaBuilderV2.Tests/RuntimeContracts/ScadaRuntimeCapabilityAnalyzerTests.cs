using ScadaBuilderV2.Application.RuntimeContracts;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.RuntimeContracts;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.RuntimeContracts;

[TestClass]
public sealed class ScadaRuntimeCapabilityAnalyzerTests
{
    [TestMethod]
    public void AnalyzeDerivesPageCompositionBindingsStateCommandsAndActions()
    {
        var state = new ScadaElementStateConfig(
            new ScadaEffectBlock(Opacity: 0.4),
            ScadaEffectBlock.Empty,
            new[]
            {
                new ScadaStateRule(
                    "state-1",
                    "High",
                    true,
                    ScadaExpression.FromAst(
                        "{temperature} > 10",
                        new ScadaExprBinary(
                            ScadaExprBinaryOp.GreaterThan,
                            new ScadaExprTagRef("temperature", "tf100.mapping.1"),
                            new ScadaExprLiteralNumber(10))),
                    new ScadaEffectBlock(ColorFilterColor: "#00FF00", TextContent: "RUN"))
            },
            new ScadaReadVariableRule("tf100.mapping.1", "{valeur}"));
        var command = new ScadaCommandBinding(
            "command-1",
            "Toggle",
            true,
            ScadaCommandTrigger.OnClick,
            ScadaCommandKind.WriteTag,
            new ScadaConfirmation("Confirm"),
            "tf100.mapping.3",
            "tf100.mapping.3",
            ScadaWriteMode.Toggle);
        var input = ScadaElement.CreateInputNumeric("input-1", "Input", 10, 10) with
        {
            Data = ScadaElement.CreateInputNumeric("source", "Source", 0, 0).Data! with
            {
                ReadTagId = "tf100.mapping.1",
                WriteTagId = "tf100.mapping.2"
            },
            StateConfig = state,
            CommandConfig = new ScadaElementCommandConfig(new[] { command })
        };
        var table = ScadaElement.CreateTable("table-1", "Table", 10, 50, 1, 1, false);
        var tableCell = table.Table!.EffectiveCells.Single() with
        {
            Content = new ScadaTableCellContent(ScadaTableCellContentKind.InputNumeric),
            ValueBindings = new ScadaTableCellValueBindings("tf100.mapping.4", "tf100.mapping.5")
        };
        table = table with { Table = table.Table with { Cells = new[] { tableCell } } };
        var scene = new ScadaScene(
            "page",
            "Page",
            CanvasSize.DefaultDesktop,
            new[] { input, table },
            PageType: ScadaPageType.Default,
            Actions: new[]
            {
                new ScadaActionDefinition(
                    "show-1",
                    ScadaActionKind.Show,
                    TargetElementId: "input-1",
                    Condition: new ScadaActionCondition("tf100.mapping.1", ScadaConditionOperator.GreaterThan, "10")),
                new ScadaActionDefinition(
                    "popup-1",
                    ScadaActionKind.MountFragment,
                    TargetPageId: "popup",
                    PopupOptions: new ScadaPopupOptions(ScadaPopupPosition.HostRegion, ScadaPopupSizePreset.Medium, HostRegionId: "input-1"),
                    ConditionGroup: new ScadaActionConditionGroup(
                        new[] { new ScadaActionCondition("tf100.mapping.1", ScadaConditionOperator.True) },
                        ScadaConditionGroupMode.Any,
                        ScadaMissingConditionPolicy.AllowAction))
            },
            HeaderPageId: "header",
            FooterPageId: "footer");
        var project = ScadaProject.CreateDefault("contract") with
        {
            Scenes = new[]
            {
                new ScadaSceneReference("page", "Page", "scenes/page.scene.json", HeaderPageId: "header", FooterPageId: "footer")
            }
        };

        var analysis = ScadaRuntimeCapabilityAnalyzer.Analyze(project, new[] { scene });
        var ids = analysis.RequiredCapabilities.Select(capability => capability.Id).ToHashSet(StringComparer.Ordinal);

        CollectionAssert.IsSubsetOf(new[]
        {
            "page.default", "page.compose.header", "page.compose.footer",
            "element.input-numeric", "element.table", "table.cell.input-numeric",
            "binding.element.read", "binding.element.write", "binding.element.distinct-write",
            "binding.table.read", "binding.table.write", "binding.table.distinct-write",
            "state.rules", "state.read-variable", "state.quality-fallback", "state.default-effect",
            "expression.binary", "expression.binary.greater-than", "expression.tag-ref", "expression.literal-number",
            "effect.opacity", "effect.color-filter", "effect.text-content",
            "command.trigger.on-click", "command.write-tag", "command.write.toggle", "command.confirmation",
            "action.show", "action.mount-fragment", "action.condition", "action.condition.greater-than",
            "action.condition-group", "action.condition-group.any", "action.missing-policy.allow-action",
            "popup.options", "popup.position.host-region", "popup.size.medium"
        }, ids.ToArray());
        Assert.IsTrue(analysis.BlockedCapabilities.Any(capability => capability.Id == "action.show"));
    }

    [TestMethod]
    public void AnalyzeIgnoresExcludedPagesAndDeduplicatesStableIds()
    {
        var project = ScadaProject.CreateDefault("contract") with
        {
            Scenes = new[]
            {
                new ScadaSceneReference("active", "Active", "active.scene.json"),
                new ScadaSceneReference("draft", "Draft", "draft.scene.json", IncludeInBuild: false)
            }
        };
        var scenes = new[]
        {
            ScadaScene.CreateEmpty("active", "Active", CanvasSize.DefaultDesktop),
            ScadaScene.CreateEmpty("draft", "Draft", CanvasSize.DefaultDesktop) with
            {
                IncludeInBuild = false,
                PageType = ScadaPageType.Fragment
            }
        };

        var analysis = ScadaRuntimeCapabilityAnalyzer.Analyze(project, scenes);

        CollectionAssert.AreEqual(
            new[] { "page.default" },
            analysis.RequiredCapabilities.Select(capability => capability.Id).ToArray());
    }

    [TestMethod]
    public void AProjectWithoutQuickWindowsRequiresNoQuickWindowCapability()
    {
        var analysis = ScadaRuntimeCapabilityAnalyzer.Analyze(QuickWindowProject(), []);

        Assert.AreEqual(0, QuickWindowIds(analysis).Length);
    }

    [TestMethod]
    public void OneExportedDefinitionRequiresTransportLifecycleAndScopedRootOnly()
    {
        var project = QuickWindowProject(QuickWindowDefinitionFixture());

        var required = QuickWindowIds(ScadaRuntimeCapabilityAnalyzer.Analyze(project, []));

        CollectionAssert.AreEqual(
            new[]
            {
                "quick-window.definition",
                "quick-window.dom.scoped-root",
                "quick-window.instance.single-per-definition",
                "quick-window.lifecycle.host-owned",
                "quick-window.presentation.backdrop"
            },
            required,
            "A bare definition triggers transport, host policy, lifecycle, scoped root and its backdrop default.");
    }

    [TestMethod]
    public void ATypedLocalInterfaceAndARequiredPortAreTwoIndependentTriggers()
    {
        var optionalOnly = QuickWindowProject(QuickWindowDefinitionFixture(members:
        [
            QuickWindowMemberFixture("Speed", required: false)
        ]));
        var withRequired = QuickWindowProject(QuickWindowDefinitionFixture(members:
        [
            QuickWindowMemberFixture("Speed", required: true)
        ]));

        var optionalIds = QuickWindowIds(ScadaRuntimeCapabilityAnalyzer.Analyze(optionalOnly, []));
        var requiredIds = QuickWindowIds(ScadaRuntimeCapabilityAnalyzer.Analyze(withRequired, []));

        CollectionAssert.Contains(optionalIds, "quick-window.local-interface.typed");
        CollectionAssert.DoesNotContain(optionalIds, "quick-window.port.required");
        CollectionAssert.Contains(requiredIds, "quick-window.port.required");
    }

    [TestMethod]
    public void ABindingTriggersPortBindingAndOnlyAParentPortTriggersItsOwnCapability()
    {
        var member = QuickWindowMemberFixture("Speed", required: false);
        var definition = QuickWindowDefinitionFixture(members: [member]);
        var literal = QuickWindowProject(definition, QuickWindowInvocationFixture(definition, QuickWindowBinding.FromLiteral(member.MemberKey, "12")));
        var parent = QuickWindowProject(definition, QuickWindowInvocationFixture(definition, QuickWindowBinding.FromParentPort(member.MemberKey, Guid.NewGuid())));
        var absent = QuickWindowProject(definition, QuickWindowInvocationFixture(definition, QuickWindowBinding.Absent(member.MemberKey)));

        var literalIds = QuickWindowIds(ScadaRuntimeCapabilityAnalyzer.Analyze(literal, []));
        var parentIds = QuickWindowIds(ScadaRuntimeCapabilityAnalyzer.Analyze(parent, []));
        var absentIds = QuickWindowIds(ScadaRuntimeCapabilityAnalyzer.Analyze(absent, []));

        CollectionAssert.Contains(literalIds, "quick-window.port-binding");
        CollectionAssert.DoesNotContain(literalIds, "quick-window.binding.parent-port");
        CollectionAssert.Contains(parentIds, "quick-window.binding.parent-port");
        CollectionAssert.Contains(
            absentIds,
            "quick-window.port-binding",
            "an explicit absence is still a persisted binding decision");
    }

    [TestMethod]
    public void OnlyADefinitionOpeningAnotherDefinitionTriggersDepthTwoNesting()
    {
        var flat = QuickWindowProject(QuickWindowDefinitionFixture());
        var nesting = QuickWindowProject(QuickWindowDefinitionFixture(elements:
        [
            ScadaElement.CreateButton("child-caller", "child-caller", 0, 0, ScadaButtonKind.Command) with
            {
                CommandConfig = new ScadaElementCommandConfig(
                [
                    new ScadaCommandBinding(
                        "open",
                        "open",
                        true,
                        ScadaCommandTrigger.OnClick,
                        ScadaCommandKind.OpenQuickWindow,
                        QuickWindowInvocationKey: Guid.NewGuid())
                ])
            }
        ]));

        CollectionAssert.DoesNotContain(QuickWindowIds(ScadaRuntimeCapabilityAnalyzer.Analyze(flat, [])), "quick-window.nesting.depth-2");
        CollectionAssert.Contains(QuickWindowIds(ScadaRuntimeCapabilityAnalyzer.Analyze(nesting, [])), "quick-window.nesting.depth-2");
    }

    [TestMethod]
    public void ABackdropIsOptedOutIndependentlyOfEveryOtherCapability()
    {
        var withoutBackdrop = QuickWindowProject(QuickWindowDefinitionFixture() with
        {
            PresentationDefaults = new QuickWindowPresentationDefaults(Backdrop: false)
        });

        var ids = QuickWindowIds(ScadaRuntimeCapabilityAnalyzer.Analyze(withoutBackdrop, []));

        CollectionAssert.DoesNotContain(ids, "quick-window.presentation.backdrop");
        CollectionAssert.Contains(ids, "quick-window.definition");
    }

    [TestMethod]
    public void APromotedQuickWindowProjectNoLongerBlocksStrictExport()
    {
        var member = QuickWindowMemberFixture("Speed", required: true);
        var definition = QuickWindowDefinitionFixture(members: [member]);
        var project = QuickWindowProject(definition, QuickWindowInvocationFixture(definition, QuickWindowBinding.FromLiteral(member.MemberKey, "12")));

        var analysis = ScadaRuntimeCapabilityAnalyzer.Analyze(project, []);
        var quickWindow = analysis.RequiredCapabilities
            .Where(capability => capability.Id.StartsWith("quick-window.", StringComparison.Ordinal))
            .ToArray();

        Assert.IsTrue(quickWindow.Length > 0);
        Assert.IsTrue(
            quickWindow.All(capability => capability.Status == ScadaRuntimeCapabilityStatus.Supported),
            "this project only uses capabilities promoted in Phase 6");
        Assert.AreEqual(0, analysis.BlockedCapabilities.Count,
            "nothing here should still be closing strict export");
    }

    [TestMethod]
    public void AParentPortBindingStillBlocksStrictExportAfterThePhase6Promotion()
    {
        // The promotion was deliberately partial. This is the half that stayed shut, and the test that
        // says so: parent-port forwarding is implemented in the Builder and the runtime, but no deployed
        // package has ever exercised it, so it has no host evidence and must keep the gate closed.
        var parent = QuickWindowMemberFixture("Speed", required: true);
        var definition = QuickWindowDefinitionFixture(members: [parent]);
        var project = QuickWindowProject(
            definition,
            QuickWindowInvocationFixture(definition, QuickWindowBinding.FromParentPort(parent.MemberKey, parent.MemberKey)));

        var analysis = ScadaRuntimeCapabilityAnalyzer.Analyze(project, []);

        CollectionAssert.Contains(
            analysis.BlockedCapabilities.Select(capability => capability.Id).ToArray(),
            "quick-window.binding.parent-port");
    }

    private static string[] QuickWindowIds(ScadaRuntimeCapabilityAnalysis analysis) => analysis.RequiredCapabilities
        .Where(capability =>
            capability.Id.StartsWith("quick-window.", StringComparison.Ordinal) ||
            capability.Id.EndsWith("-quick-window", StringComparison.Ordinal))
        .Select(capability => capability.Id)
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToArray();

    private static ScadaProject QuickWindowProject(
        QuickWindowDefinition? definition = null,
        QuickWindowInvocation? invocation = null) =>
        ScadaProject.CreateDefault("QuickWindowCapabilities") with
        {
            ManifestVersion = "2.3",
            QuickWindows = definition is null ? [] : [definition],
            QuickWindowInvocations = invocation is null ? [] : [invocation]
        };

    private static QuickWindowDefinition QuickWindowDefinitionFixture(
        IReadOnlyList<QuickWindowInterfaceMember>? members = null,
        IReadOnlyList<ScadaElement>? elements = null) =>
        new(
            Guid.Parse("aaaabbbb-cccc-dddd-eeee-ffff00001111"),
            "moteur",
            "Moteur",
            1,
            new VisualContent(new CanvasSize(480, 320), Elements: elements ?? []),
            members ?? [],
            new QuickWindowPresentationDefaults());

    private static QuickWindowInterfaceMember QuickWindowMemberFixture(string name, bool required) =>
        new(
            Guid.Parse("11112222-3333-4444-5555-666677778888"),
            name,
            QuickWindowInterfaceFamily.PublicParameter,
            QuickWindowDataType.Decimal,
            QuickWindowMemberAccess.Read,
            required);

    private static QuickWindowInvocation QuickWindowInvocationFixture(
        QuickWindowDefinition definition,
        params QuickWindowBinding[] bindings) =>
        new(
            Guid.Parse("99998888-7777-6666-5555-444433332222"),
            definition.DefinitionKey,
            bindings,
            InterfaceVersion: definition.InterfaceVersion);
}
