using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
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

    // Fixed keys, because the conformance package must export byte-identically on every machine and a
    // generated Guid would move the archive hash on each run.
    private static readonly Guid MainPageKey = Guid.Parse("c0170000-0000-4000-8000-000000000000");
    private static readonly Guid OuterDefinitionKey = Guid.Parse("c0170001-0000-4000-8000-000000000001");
    private static readonly Guid InnerDefinitionKey = Guid.Parse("c0170002-0000-4000-8000-000000000002");
    private static readonly Guid OuterState = Guid.Parse("c0170011-0000-4000-8000-000000000011");
    private static readonly Guid OuterSetpoint = Guid.Parse("c0170012-0000-4000-8000-000000000012");
    private static readonly Guid OuterLabel = Guid.Parse("c0170013-0000-4000-8000-000000000013");
    private static readonly Guid OuterCount = Guid.Parse("c0170014-0000-4000-8000-000000000014");
    private static readonly Guid InnerState = Guid.Parse("c0170021-0000-4000-8000-000000000021");
    private static readonly Guid InnerDetail = Guid.Parse("c0170022-0000-4000-8000-000000000022");
    private static readonly Guid OuterInvocationKey = Guid.Parse("c0170031-0000-4000-8000-000000000031");
    private static readonly Guid InnerInvocationKey = Guid.Parse("c0170032-0000-4000-8000-000000000032");

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
            // Quick windows only travel in manifest 2.3; profiles 2.1 and 2.2 fail closed and the default
            // 2.0 refuses them outright. The conformance project must therefore declare 2.3 to carry them.
            ManifestVersion = "2.3",
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
                "generated-conformance-tags.json"),
            QuickWindows = [BuildOuterQuickWindow(), BuildInnerQuickWindow()],
            QuickWindowInvocations = BuildQuickWindowInvocations()
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
            PageKey = MainPageKey,
            HeaderPageId = HeaderPageId,
            FooterPageId = FooterPageId,
            Elements = elements,
            Actions = [new ScadaActionDefinition("action-navigate", ScadaActionKind.Navigate, TargetPageId: MainPageId)]
        };
    }

    /// <summary>
    /// Builds the outer quick window, which carries every promoted definition-side capability at once.
    /// </summary>
    /// <remarks>
    /// Four things are deliberate. Its interface holds a required public member, which is the only trigger
    /// for `quick-window.port.required`. Its presentation keeps the backdrop, the only trigger for
    /// `quick-window.presentation.backdrop`. Its own content carries an `OpenQuickWindow` command, which is
    /// what makes the chain two levels deep and the only trigger for `quick-window.nesting.depth-2`. And no
    /// binding anywhere in this project is `ParentPort`, so `quick-window.binding.parent-port` stays out of
    /// the analysis and keeps its Blocked status honest.
    /// </remarks>
    private static QuickWindowDefinition BuildOuterQuickWindow() =>
        new(
            OuterDefinitionKey,
            "conformance-outer",
            "Conformance Outer",
            InterfaceVersion: 1,
            new VisualContent(
                new CanvasSize(520, 360),
                Elements:
                [
                    ScadaElement.CreateText("outer-title", "Outer", 12, 12),
                    ScadaElement.CreateInputNumeric("outer-value", "Value", 12, 60, isReadOnly: true),
                    ScadaElement.CreateButton("outer-open-inner", "Open inner", 12, 280) with
                    {
                        CommandConfig = new ScadaElementCommandConfig(
                        [
                            new ScadaCommandBinding(
                                "outer-open-inner-command",
                                "Open inner",
                                true,
                                ScadaCommandTrigger.OnClick,
                                ScadaCommandKind.OpenQuickWindow,
                                QuickWindowInvocationKey: InnerInvocationKey)
                        ])
                    }
                ]),
            [
                new QuickWindowInterfaceMember(OuterState, "State", QuickWindowInterfaceFamily.ReadState,
                    QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read, Required: true),
                new QuickWindowInterfaceMember(OuterSetpoint, "Setpoint", QuickWindowInterfaceFamily.WriteCommand,
                    QuickWindowDataType.Decimal, QuickWindowMemberAccess.Write),
                new QuickWindowInterfaceMember(OuterLabel, "Label", QuickWindowInterfaceFamily.PublicParameter,
                    QuickWindowDataType.String, QuickWindowMemberAccess.Read),
                new QuickWindowInterfaceMember(OuterCount, "Count", QuickWindowInterfaceFamily.ReadState,
                    QuickWindowDataType.Integer, QuickWindowMemberAccess.Read)
            ],
            new QuickWindowPresentationDefaults(Title: "Conformance Outer"));

    /// <summary>Builds the inner quick window, the second level of the nesting chain.</summary>
    private static QuickWindowDefinition BuildInnerQuickWindow() =>
        new(
            InnerDefinitionKey,
            "conformance-inner",
            "Conformance Inner",
            InterfaceVersion: 1,
            new VisualContent(
                new CanvasSize(360, 240),
                Elements:
                [
                    ScadaElement.CreateText("inner-title", "Inner", 8, 8),
                    ScadaElement.CreateInputNumeric("inner-value", "Value", 8, 48, isReadOnly: true),
                    ScadaElement.CreateButton("inner-close", "Close", 8, 180) with
                    {
                        CommandConfig = new ScadaElementCommandConfig(
                        [
                            new ScadaCommandBinding(
                                "inner-close-command",
                                "Close",
                                true,
                                ScadaCommandTrigger.OnClick,
                                ScadaCommandKind.CloseQuickWindow)
                        ])
                    }
                ]),
            [
                new QuickWindowInterfaceMember(InnerState, "State", QuickWindowInterfaceFamily.ReadState,
                    QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read),
                new QuickWindowInterfaceMember(InnerDetail, "Detail", QuickWindowInterfaceFamily.ReadState,
                    QuickWindowDataType.Decimal, QuickWindowMemberAccess.Read)
            ],
            new QuickWindowPresentationDefaults(Title: "Conformance Inner"));

    /// <summary>Builds both invocations, every binding tag-sourced so no parent-port capability is required.</summary>
    private static IReadOnlyList<QuickWindowInvocation> BuildQuickWindowInvocations() =>
    [
        new QuickWindowInvocation(
            OuterInvocationKey,
            OuterDefinitionKey,
            [
                QuickWindowBinding.FromTag(OuterState, "conformance.tag.bool"),
                QuickWindowBinding.FromTag(OuterSetpoint, "conformance.tag.write"),
                // Literal rather than tag: the conformance tag catalog carries no integer tag, and a second
                // binding source kind is worth more here than a third tag-sourced one.
                QuickWindowBinding.FromLiteral(OuterCount, "3")
            ],
            InterfaceVersion: 1,
            OwnerPageKey: null,
            OwnerElementId: "commands-all",
            OwnerCommandId: "kind-open-quick-window"),
        new QuickWindowInvocation(
            InnerInvocationKey,
            InnerDefinitionKey,
            [
                QuickWindowBinding.FromTag(InnerState, "conformance.tag.bool"),
                QuickWindowBinding.FromTag(InnerDetail, "conformance.tag.number")
            ],
            InterfaceVersion: 1,
            OwnerPageKey: null,
            OwnerElementId: "outer-open-inner",
            OwnerCommandId: "outer-open-inner-command")
    ];

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
            new ScadaCommandBinding("kind-open-url", "Open URL", true, ScadaCommandTrigger.OnClick,
                ScadaCommandKind.OpenUrl, Url: "https://example.invalid/conformance"),
            new ScadaCommandBinding("write-toggle", "Toggle", true, ScadaCommandTrigger.OnClick,
                ScadaCommandKind.WriteTag, new ScadaConfirmation("Confirm conformance write"),
                "conformance.tag.write", "conformance.tag.bool", ScadaWriteMode.Toggle, "1", "0"),
            new ScadaCommandBinding("write-fixed", "Fixed", true, ScadaCommandTrigger.OnClick,
                ScadaCommandKind.WriteTag, WriteTagId: "conformance.tag.write", WriteMode: ScadaWriteMode.SetFixed, FixedValue: "12.5"),
            new ScadaCommandBinding("write-input", "Input", true, ScadaCommandTrigger.OnClick,
                ScadaCommandKind.WriteTag, WriteTagId: "conformance.tag.write", WriteMode: ScadaWriteMode.SetFromInput),
            // The caller half of the quick-window vertical. Only the open command belongs on a page:
            // CloseQuickWindow is valid solely inside a definition's own content, so it lives in the inner
            // window below, which is also where a real close button lives.
            new ScadaCommandBinding("kind-open-quick-window", "Open quick window", true, ScadaCommandTrigger.OnClick,
                ScadaCommandKind.OpenQuickWindow, QuickWindowInvocationKey: OuterInvocationKey)
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
