using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.RuntimeContracts;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.Tests.RuntimeContracts;

internal sealed record ScadaV2RuntimeConformanceProject(
    ScadaProject Project,
    IReadOnlyList<Ft100ProjectPageExportInput> Pages);

internal static class ScadaV2RuntimeConformanceProjectFactory
{
    public const string ProjectName = "SCADA V2 Runtime Conformance";
    public const string MainPageId = "conformance-main";
    public const string HeaderPageId = "conformance-header";
    public const string FooterPageId = "conformance-footer";
    public const string FragmentPageId = "conformance-fragment";

    public static ScadaV2RuntimeConformanceProject Create()
    {
        var main = BuildMainScene();
        var header = ScadaScene.CreateEmpty(HeaderPageId, "Conformance Header", new CanvasSize(1280, 64)) with
        {
            PageType = ScadaPageType.Header,
            Elements = [ScadaElement.CreateText("header-text", "Header", 8, 8)]
        };
        var footer = ScadaScene.CreateEmpty(FooterPageId, "Conformance Footer", new CanvasSize(1280, 48)) with
        {
            PageType = ScadaPageType.Footer,
            Elements = [ScadaElement.CreateText("footer-text", "Footer", 8, 8)]
        };
        var fragment = ScadaScene.CreateEmpty(FragmentPageId, "Conformance Fragment", new CanvasSize(320, 240)) with
        {
            PageType = ScadaPageType.Fragment,
            Elements = [ScadaElement.CreateText("fragment-text", "Fragment", 8, 8)]
        };

        var scenes = new[] { main, header, footer, fragment };
        var project = ScadaProject.CreateDefault(ProjectName) with
        {
            HomePageId = MainPageId,
            Scenes = scenes.Select(scene => new ScadaSceneReference(
                scene.Id,
                scene.Title,
                $"scenes/{scene.Id}.scene.json",
                scene.PageType,
                scene.CanvasSize,
                scene.EffectiveBackground,
                scene.IncludeInBuild,
                scene.HeaderPageId,
                scene.FooterPageId)).ToArray(),
            TagCatalog = new ScadaTagCatalog(
                "scada-v2-conformance-tags-v1",
                [
                    new ScadaTagDefinition("conformance.tag.bool", "Boolean", Datatype: "Boolean", Writeable: true),
                    new ScadaTagDefinition("conformance.tag.number", "Number", Datatype: "Float", Writeable: false),
                    new ScadaTagDefinition("conformance.tag.write", "Write", Datatype: "Float", Writeable: true)
                ],
                "generated-conformance-tags.json")
        };

        return new ScadaV2RuntimeConformanceProject(
            project,
            scenes.Select(scene => new Ft100ProjectPageExportInput(scene, null)).ToArray());
    }

    private static ScadaScene BuildMainScene()
    {
        var elements = new List<ScadaElement>();
        var nextX = 8d;
        var nextY = 8d;

        void Add(ScadaElement element)
        {
            elements.Add(element with { Bounds = element.Bounds with { X = nextX, Y = nextY } });
            nextX += 118;
            if (nextX > 1120)
            {
                nextX = 8;
                nextY += 74;
            }
        }

        Add(ScadaElement.CreateText("element-text", "Text", 0, 0));
        Add(SimpleElement("element-input-text", ScadaElementKind.InputText, new ScadaElementData(
            "Input", "Text", null, null, null, null, null, null, null, false,
            "conformance.tag.write", "conformance.tag.write")));
        Add(SimpleElement("element-input-numeric", ScadaElementKind.InputNumeric, new ScadaElementData(
            "12.5", "0", 12.5, 0, 100, 1, "u", "fixed:1", null, false,
            "conformance.tag.number", "conformance.tag.write")));
        Add(SimpleElement("element-image", ScadaElementKind.Image));
        Add(SimpleElement("element-container", ScadaElementKind.Container));
        Add(SimpleElement("element-legacy-static", ScadaElementKind.LegacyStatic));
        Add(SimpleElement("element-custom", ScadaElementKind.Custom));

        var groupChild = ScadaElement.CreateText("group-child", "Group child", 4, 4);
        Add(new ScadaElement(
            "element-group", "Group", ScadaElementKind.Group, new SceneBounds(0, 0, 110, 58), null,
            Children: [groupChild]));

        foreach (var shapeKind in Enum.GetValues<ScadaShapeKind>())
        {
            Add(ScadaElement.CreateShape($"shape-{Kebab(shapeKind)}", shapeKind.ToString(), shapeKind, 0, 0));
        }

        foreach (var buttonKind in Enum.GetValues<ScadaButtonKind>())
        {
            Add(ScadaElement.CreateButton($"button-{Kebab(buttonKind)}", buttonKind.ToString(), 0, 0, buttonKind));
        }
        Add(ScadaElement.CreateButton("button-disabled", "Disabled", 0, 0) with
        {
            ButtonBehavior = new ScadaButtonBehavior(true)
        });

        var table = ScadaElement.CreateTable("table-all-cells", "Table", 0, 0, rows: 1, columns: 3, firstRowIsHeader: false);
        var tableDefinition = table.Table! with
        {
            Cells =
            [
                new ScadaTableCell(0, 0, Content: new ScadaTableCellContent(ScadaTableCellContentKind.Text, "Text")),
                new ScadaTableCell(0, 1, Content: new ScadaTableCellContent(ScadaTableCellContentKind.InputText, Placeholder: "Text")),
                new ScadaTableCell(
                    0,
                    2,
                    Content: new ScadaTableCellContent(
                        ScadaTableCellContentKind.InputNumeric,
                        Placeholder: "0.0",
                        NumericValue: 12.5,
                        Minimum: 0,
                        Maximum: 100,
                        Step: 0.1,
                        DisplayFormat: "fixed:1"),
                    ValueBindings: new ScadaTableCellValueBindings("conformance.tag.number", "conformance.tag.write"))
            ]
        };
        Add(table with { Table = tableDefinition, Bounds = new SceneBounds(0, 0, tableDefinition.Width, tableDefinition.Height) });

        Add(BuildStateElement());
        Add(BuildCommandElement());

        return ScadaScene.CreateEmpty(MainPageId, "Runtime Conformance", new CanvasSize(1280, 900)) with
        {
            HeaderPageId = HeaderPageId,
            FooterPageId = FooterPageId,
            Elements = elements,
            Actions = [new ScadaActionDefinition("action-navigate", ScadaActionKind.Navigate, TargetPageId: MainPageId)]
        };
    }

    private static ScadaElement BuildStateElement()
    {
        var fullEffect = new ScadaEffectBlock(
            BackgroundColor: "#102030",
            BorderColor: "#405060",
            BorderWidth: 2,
            TextColor: "#FFFFFF",
            TextContent: "ACTIVE {valeur}",
            TextVisible: true,
            ElementVisible: true,
            Opacity: 0.8,
            Rotation: 5,
            ColorFilterColor: "#00AA55",
            ColorFilterOpacity: 0.7,
            ColorFilterHalo: true,
            ColorFilterHaloColor: "#00FF88");

        var expressions = new List<ScadaExprNode>
        {
            new ScadaExprLiteralNumber(1),
            new ScadaExprLiteralBool(true),
            new ScadaExprLiteralString("ready"),
            new ScadaExprTagRef("Boolean", "conformance.tag.bool"),
            new ScadaExprUnary(ScadaExprUnaryOp.Not, new ScadaExprLiteralBool(false)),
            new ScadaExprUnary(ScadaExprUnaryOp.Negate, new ScadaExprLiteralNumber(1))
        };
        expressions.AddRange(Enum.GetValues<ScadaExprBinaryOp>().Select(op => new ScadaExprBinary(
            op,
            op is ScadaExprBinaryOp.And or ScadaExprBinaryOp.Or ? new ScadaExprLiteralBool(true) : new ScadaExprLiteralNumber(8),
            op is ScadaExprBinaryOp.And or ScadaExprBinaryOp.Or ? new ScadaExprLiteralBool(false) : new ScadaExprLiteralNumber(2))));
        expressions.AddRange(
        [
            new ScadaExprFunc("ABS", [new ScadaExprLiteralNumber(-2)]),
            new ScadaExprFunc("MIN", [new ScadaExprLiteralNumber(1), new ScadaExprLiteralNumber(2)]),
            new ScadaExprFunc("MAX", [new ScadaExprLiteralNumber(1), new ScadaExprLiteralNumber(2)]),
            new ScadaExprFunc("BIT", [new ScadaExprLiteralNumber(4), new ScadaExprLiteralNumber(2)])
        ]);

        var states = expressions.Select((expression, index) => new ScadaStateRule(
            $"expression-{index:D2}",
            $"Expression {index:D2}",
            true,
            ScadaExpression.FromAst($"fixture-{index:D2}", expression),
            index == 0 ? fullEffect : ScadaEffectBlock.Empty)).ToArray();

        return SimpleElement("state-all", ScadaElementKind.Text) with
        {
            StateConfig = new ScadaElementStateConfig(
                fullEffect with { TextContent = "NO DATA" },
                fullEffect with { TextContent = "REST" },
                states,
                new ScadaReadVariableRule("conformance.tag.number", "Value: {valeur}"))
        };
    }

    private static ScadaElement BuildCommandElement()
    {
        var commands = new List<ScadaCommandBinding>();
        foreach (var trigger in Enum.GetValues<ScadaCommandTrigger>())
        {
            commands.Add(new ScadaCommandBinding(
                $"trigger-{Kebab(trigger)}", trigger.ToString(), true, trigger, ScadaCommandKind.Back));
        }

        commands.AddRange(
        [
            new ScadaCommandBinding("kind-navigate", "Navigate", true, ScadaCommandTrigger.OnClick,
                ScadaCommandKind.Navigate, TargetPageId: MainPageId),
            new ScadaCommandBinding("kind-open-popup", "Open popup", true, ScadaCommandTrigger.OnClick,
                ScadaCommandKind.OpenPopup, TargetPageId: FragmentPageId),
            new ScadaCommandBinding("kind-toggle-popup", "Toggle popup", true, ScadaCommandTrigger.OnClick,
                ScadaCommandKind.TogglePopup, TargetPageId: FragmentPageId),
            new ScadaCommandBinding("kind-close-popup", "Close popup", true, ScadaCommandTrigger.OnClick,
                ScadaCommandKind.ClosePopup, TargetPageId: FragmentPageId),
            new ScadaCommandBinding("kind-open-url", "Open URL", true, ScadaCommandTrigger.OnClick,
                ScadaCommandKind.OpenUrl, Url: "https://example.invalid/conformance"),
            new ScadaCommandBinding("write-toggle", "Toggle", true, ScadaCommandTrigger.OnClick,
                ScadaCommandKind.WriteTag, new ScadaConfirmation("Confirm conformance write"),
                "conformance.tag.write", "conformance.tag.bool", ScadaWriteMode.Toggle, "1", "0"),
            new ScadaCommandBinding("write-fixed", "Fixed", true, ScadaCommandTrigger.OnClick,
                ScadaCommandKind.WriteTag, WriteTagId: "conformance.tag.write", WriteMode: ScadaWriteMode.SetFixed, FixedValue: "12.5"),
            new ScadaCommandBinding("write-input", "Input", true, ScadaCommandTrigger.OnClick,
                ScadaCommandKind.WriteTag, WriteTagId: "conformance.tag.write", WriteMode: ScadaWriteMode.SetFromInput)
        ]);

        return ScadaElement.CreateButton("commands-all", "Commands", 0, 0) with
        {
            CommandConfig = new ScadaElementCommandConfig(commands)
        };
    }

    private static ScadaElement SimpleElement(string id, ScadaElementKind kind, ScadaElementData? data = null) =>
        new(
            id,
            id,
            kind,
            new SceneBounds(0, 0, 110, 58),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            data ?? new ScadaElementData(id, null, null, null, null, null, null, null, null, false));

    private static string Kebab<T>(T value) where T : Enum =>
        string.Concat(value.ToString().Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));
}
