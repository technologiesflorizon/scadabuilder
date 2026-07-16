using ScadaBuilderV2.Application.RuntimeContracts;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;
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
}
