using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Domain.RuntimeContracts;

/// <summary>
/// Canonical mapping from every persistent runtime variant to a stable capability id.
/// Explicit dictionaries are intentional: completeness tests must fail when a new enum
/// value or effect field is added without a contract decision.
/// </summary>
/// <remarks>
/// Decisions: DEC-0047.
/// Contracts: docs/superpowers/specs/2026-07-16-scada-v2-tf100web-runtime-conformance-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/RuntimeContracts/ScadaRuntimeCapabilityCatalogTests.cs.
/// </remarks>
public static class ScadaRuntimeCapabilityCatalog
{
    private static readonly ScadaRuntimeCapabilityEvidence BaselineEvidence = new(
        ["tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs"],
        ["tests/ScadaBuilderV2.Tests/Runtime/RuntimeJsModulesTests.cs"],
        ["F:/Projet/Git/TF100Web/frontend/tests_scada_package.py"]);

    /// <summary>Gets the capability catalog contract version.</summary>
    public const string ContractVersion = "1.0";

    /// <summary>Gets capabilities keyed by persistent page type.</summary>
    public static IReadOnlyDictionary<ScadaPageType, ScadaRuntimeCapability> PageTypes { get; } = Map(
        (ScadaPageType.Default, Capability("page.default", ScadaRuntimeCapabilityOwner.Tf100WebHost)),
        (ScadaPageType.Fragment, Capability("page.fragment", ScadaRuntimeCapabilityOwner.Tf100WebHost)),
        (ScadaPageType.Header, Capability("page.header", ScadaRuntimeCapabilityOwner.Tf100WebHost)),
        (ScadaPageType.Footer, Capability("page.footer", ScadaRuntimeCapabilityOwner.Tf100WebHost)));

    /// <summary>Gets capabilities keyed by persistent element kind.</summary>
    public static IReadOnlyDictionary<ScadaElementKind, ScadaRuntimeCapability> ElementKinds { get; } = Map(
        (ScadaElementKind.Text, Capability("element.text", ScadaRuntimeCapabilityOwner.PackageTransport)),
        (ScadaElementKind.InputText, Capability("element.input-text", ScadaRuntimeCapabilityOwner.PackageTransport)),
        (ScadaElementKind.InputNumeric, Capability("element.input-numeric", ScadaRuntimeCapabilityOwner.PackageTransport)),
        (ScadaElementKind.Image, Capability("element.image", ScadaRuntimeCapabilityOwner.PackageTransport)),
        (ScadaElementKind.Shape, Capability("element.shape", ScadaRuntimeCapabilityOwner.PackageTransport)),
        (ScadaElementKind.Group, Capability("element.group", ScadaRuntimeCapabilityOwner.PackageTransport)),
        (ScadaElementKind.Button, Capability("element.button", ScadaRuntimeCapabilityOwner.PackageTransport)),
        (ScadaElementKind.Table, Capability("element.table", ScadaRuntimeCapabilityOwner.PackageTransport)),
        (ScadaElementKind.Container, Capability("element.container", ScadaRuntimeCapabilityOwner.PackageTransport)),
        (ScadaElementKind.LegacyStatic, Capability("element.legacy-static", ScadaRuntimeCapabilityOwner.PackageTransport)),
        (ScadaElementKind.Custom, Capability("element.custom", ScadaRuntimeCapabilityOwner.PackageTransport)));

    /// <summary>Gets capabilities keyed by persistent shape kind.</summary>
    public static IReadOnlyDictionary<ScadaShapeKind, ScadaRuntimeCapability> ShapeKinds { get; } = EnumCapabilities<ScadaShapeKind>(
        value => $"shape.{ToKebabCase(value.ToString())}", ScadaRuntimeCapabilityOwner.PackageTransport);

    /// <summary>Gets capabilities keyed by persistent button kind.</summary>
    public static IReadOnlyDictionary<ScadaButtonKind, ScadaRuntimeCapability> ButtonKinds { get; } = Map(
        (ScadaButtonKind.Command, Capability("button.command", ScadaRuntimeCapabilityOwner.PackageTransport)),
        (ScadaButtonKind.Toggle, Capability("button.toggle", ScadaRuntimeCapabilityOwner.PackageTransport)),
        (ScadaButtonKind.Navigation, Capability("button.navigation", ScadaRuntimeCapabilityOwner.PackageTransport)),
        (ScadaButtonKind.AlarmAcknowledge, Capability("button.alarm-acknowledge", ScadaRuntimeCapabilityOwner.PackageTransport)),
        (ScadaButtonKind.EmergencyStop, Capability("button.emergency-stop", ScadaRuntimeCapabilityOwner.PackageTransport)));

    /// <summary>Gets capabilities keyed by persistent table-cell content kind.</summary>
    public static IReadOnlyDictionary<ScadaTableCellContentKind, ScadaRuntimeCapability> TableCellKinds { get; } = Map(
        (ScadaTableCellContentKind.Text, Capability("table.cell.text", ScadaRuntimeCapabilityOwner.PackageTransport)),
        (ScadaTableCellContentKind.InputText, Capability("table.cell.input-text", ScadaRuntimeCapabilityOwner.PackageTransport)),
        (ScadaTableCellContentKind.InputNumeric, Capability("table.cell.input-numeric", ScadaRuntimeCapabilityOwner.PackageTransport)));

    /// <summary>Gets capabilities keyed by legacy action kind.</summary>
    public static IReadOnlyDictionary<ScadaActionKind, ScadaRuntimeCapability> ActionKinds { get; } = Map(
        (ScadaActionKind.Navigate, Capability("action.navigate", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaActionKind.Show, Blocked("action.show", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaActionKind.Hide, Blocked("action.hide", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaActionKind.ToggleVisibility, Blocked("action.toggle-visibility", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaActionKind.MountFragment, Blocked("action.mount-fragment", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaActionKind.ClosePopup, Blocked("action.close-popup", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaActionKind.TogglePopup, Blocked("action.toggle-popup", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaActionKind.ReadValue, Blocked("action.read-value", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaActionKind.WriteValue, Blocked("action.write-value", ScadaRuntimeCapabilityOwner.SharedRuntime)));

    /// <summary>Gets capabilities keyed by action condition operator.</summary>
    public static IReadOnlyDictionary<ScadaConditionOperator, ScadaRuntimeCapability> ConditionOperators { get; } =
        EnumCapabilities<ScadaConditionOperator>(value => $"action.condition.{ToKebabCase(value.ToString())}", ScadaRuntimeCapabilityOwner.SharedRuntime, ScadaRuntimeCapabilityStatus.Blocked);

    /// <summary>Gets capabilities keyed by condition-group mode.</summary>
    public static IReadOnlyDictionary<ScadaConditionGroupMode, ScadaRuntimeCapability> ConditionGroupModes { get; } =
        EnumCapabilities<ScadaConditionGroupMode>(value => $"action.condition-group.{ToKebabCase(value.ToString())}", ScadaRuntimeCapabilityOwner.SharedRuntime, ScadaRuntimeCapabilityStatus.Blocked);

    /// <summary>Gets capabilities keyed by missing-condition policy.</summary>
    public static IReadOnlyDictionary<ScadaMissingConditionPolicy, ScadaRuntimeCapability> MissingConditionPolicies { get; } =
        EnumCapabilities<ScadaMissingConditionPolicy>(value => $"action.missing-policy.{ToKebabCase(value.ToString())}", ScadaRuntimeCapabilityOwner.SharedRuntime, ScadaRuntimeCapabilityStatus.Blocked);

    /// <summary>Gets capabilities keyed by popup position.</summary>
    public static IReadOnlyDictionary<ScadaPopupPosition, ScadaRuntimeCapability> PopupPositions { get; } =
        EnumCapabilities<ScadaPopupPosition>(value => $"popup.position.{ToKebabCase(value.ToString())}", ScadaRuntimeCapabilityOwner.Tf100WebHost, ScadaRuntimeCapabilityStatus.Blocked);

    /// <summary>Gets capabilities keyed by popup size preset.</summary>
    public static IReadOnlyDictionary<ScadaPopupSizePreset, ScadaRuntimeCapability> PopupSizes { get; } =
        EnumCapabilities<ScadaPopupSizePreset>(value => $"popup.size.{ToKebabCase(value.ToString())}", ScadaRuntimeCapabilityOwner.Tf100WebHost, ScadaRuntimeCapabilityStatus.Blocked);

    /// <summary>Gets capabilities keyed by command trigger.</summary>
    public static IReadOnlyDictionary<ScadaCommandTrigger, ScadaRuntimeCapability> CommandTriggers { get; } =
        EnumCapabilities<ScadaCommandTrigger>(value => $"command.trigger.{ToKebabCase(value.ToString())}", ScadaRuntimeCapabilityOwner.SharedRuntime);

    /// <summary>Gets capabilities keyed by command kind.</summary>
    public static IReadOnlyDictionary<ScadaCommandKind, ScadaRuntimeCapability> CommandKinds { get; } = Map(
        (ScadaCommandKind.WriteTag, Capability("command.write-tag", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaCommandKind.Navigate, Capability("command.navigate", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaCommandKind.OpenPopup, Capability("command.open-popup", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaCommandKind.TogglePopup, Capability("command.toggle-popup", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaCommandKind.ClosePopup, Capability("command.close-popup", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaCommandKind.OpenUrl, Capability("command.open-url", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaCommandKind.Back, Capability("command.back", ScadaRuntimeCapabilityOwner.SharedRuntime)));

    /// <summary>Gets capabilities keyed by write mode.</summary>
    public static IReadOnlyDictionary<ScadaWriteMode, ScadaRuntimeCapability> WriteModes { get; } = Map(
        (ScadaWriteMode.Momentary, Blocked("command.write.momentary", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaWriteMode.Toggle, Capability("command.write.toggle", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaWriteMode.SetFixed, Capability("command.write.set-fixed", ScadaRuntimeCapabilityOwner.SharedRuntime)),
        (ScadaWriteMode.SetFromInput, Capability("command.write.set-from-input", ScadaRuntimeCapabilityOwner.SharedRuntime)));

    /// <summary>Gets capabilities keyed by unary expression operator.</summary>
    public static IReadOnlyDictionary<ScadaExprUnaryOp, ScadaRuntimeCapability> UnaryOperators { get; } =
        EnumCapabilities<ScadaExprUnaryOp>(value => $"expression.unary.{ToKebabCase(value.ToString())}", ScadaRuntimeCapabilityOwner.SharedRuntime);

    /// <summary>Gets capabilities keyed by binary expression operator.</summary>
    public static IReadOnlyDictionary<ScadaExprBinaryOp, ScadaRuntimeCapability> BinaryOperators { get; } =
        EnumCapabilities<ScadaExprBinaryOp>(value => $"expression.binary.{ToKebabCase(value.ToString())}", ScadaRuntimeCapabilityOwner.SharedRuntime);

    /// <summary>Gets capabilities keyed by concrete expression AST node type.</summary>
    public static IReadOnlyDictionary<Type, ScadaRuntimeCapability> ExpressionNodeTypes { get; } = new Dictionary<Type, ScadaRuntimeCapability>
    {
        [typeof(ScadaExprLiteralNumber)] = Capability("expression.literal-number", ScadaRuntimeCapabilityOwner.SharedRuntime),
        [typeof(ScadaExprLiteralBool)] = Capability("expression.literal-bool", ScadaRuntimeCapabilityOwner.SharedRuntime),
        [typeof(ScadaExprLiteralString)] = Capability("expression.literal-string", ScadaRuntimeCapabilityOwner.SharedRuntime),
        [typeof(ScadaExprTagRef)] = Capability("expression.tag-ref", ScadaRuntimeCapabilityOwner.SharedRuntime),
        [typeof(ScadaExprUnary)] = Capability("expression.unary", ScadaRuntimeCapabilityOwner.SharedRuntime),
        [typeof(ScadaExprBinary)] = Capability("expression.binary", ScadaRuntimeCapabilityOwner.SharedRuntime),
        [typeof(ScadaExprFunc)] = Capability("expression.function", ScadaRuntimeCapabilityOwner.SharedRuntime)
    };

    /// <summary>Gets capabilities keyed by canonical expression function name.</summary>
    public static IReadOnlyDictionary<string, ScadaRuntimeCapability> ExpressionFunctions { get; } =
        new Dictionary<string, ScadaRuntimeCapability>(StringComparer.OrdinalIgnoreCase)
        {
            ["ABS"] = Capability("expression.function.abs", ScadaRuntimeCapabilityOwner.SharedRuntime),
            ["MIN"] = Capability("expression.function.min", ScadaRuntimeCapabilityOwner.SharedRuntime),
            ["MAX"] = Capability("expression.function.max", ScadaRuntimeCapabilityOwner.SharedRuntime),
            ["BIT"] = Capability("expression.function.bit", ScadaRuntimeCapabilityOwner.SharedRuntime)
        };

    /// <summary>Gets capabilities keyed by state-effect animation.</summary>
    public static IReadOnlyDictionary<ScadaAnimation, ScadaRuntimeCapability> Animations { get; } =
        EnumCapabilities<ScadaAnimation>(value => $"effect.animation.{ToKebabCase(value.ToString())}", ScadaRuntimeCapabilityOwner.SharedRuntime,
            ScadaRuntimeCapabilityStatus.Blocked);

    /// <summary>Gets capabilities keyed by persistent state-effect property name.</summary>
    public static IReadOnlyDictionary<string, ScadaRuntimeCapability> EffectProperties { get; } =
        new Dictionary<string, ScadaRuntimeCapability>(StringComparer.Ordinal)
        {
            [nameof(ScadaEffectBlock.BackgroundColor)] = Capability("effect.background-color", ScadaRuntimeCapabilityOwner.SharedRuntime),
            [nameof(ScadaEffectBlock.BorderColor)] = Capability("effect.border-color", ScadaRuntimeCapabilityOwner.SharedRuntime),
            [nameof(ScadaEffectBlock.BorderWidth)] = Capability("effect.border-width", ScadaRuntimeCapabilityOwner.SharedRuntime),
            [nameof(ScadaEffectBlock.TextColor)] = Capability("effect.text-color", ScadaRuntimeCapabilityOwner.SharedRuntime),
            [nameof(ScadaEffectBlock.TextContent)] = Capability("effect.text-content", ScadaRuntimeCapabilityOwner.SharedRuntime),
            [nameof(ScadaEffectBlock.TextVisible)] = Capability("effect.text-visible", ScadaRuntimeCapabilityOwner.SharedRuntime),
            [nameof(ScadaEffectBlock.ElementVisible)] = Capability("effect.element-visible", ScadaRuntimeCapabilityOwner.SharedRuntime),
            [nameof(ScadaEffectBlock.Opacity)] = Capability("effect.opacity", ScadaRuntimeCapabilityOwner.SharedRuntime),
            [nameof(ScadaEffectBlock.Rotation)] = Capability("effect.rotation", ScadaRuntimeCapabilityOwner.SharedRuntime),
            [nameof(ScadaEffectBlock.Animation)] = Blocked("effect.animation", ScadaRuntimeCapabilityOwner.SharedRuntime),
            [nameof(ScadaEffectBlock.ColorFilterColor)] = Capability("effect.color-filter", ScadaRuntimeCapabilityOwner.SharedRuntime),
            [nameof(ScadaEffectBlock.ColorFilterOpacity)] = Capability("effect.color-filter-opacity", ScadaRuntimeCapabilityOwner.SharedRuntime),
            [nameof(ScadaEffectBlock.ColorFilterHalo)] = Capability("effect.color-filter-halo", ScadaRuntimeCapabilityOwner.SharedRuntime),
            [nameof(ScadaEffectBlock.ColorFilterHaloColor)] = Capability("effect.color-filter-halo-color", ScadaRuntimeCapabilityOwner.SharedRuntime)
        };

    /// <summary>Gets the header composition capability.</summary>
    public static ScadaRuntimeCapability PageHeaderComposition { get; } = Capability("page.compose.header", ScadaRuntimeCapabilityOwner.Tf100WebHost);
    /// <summary>Gets the footer composition capability.</summary>
    public static ScadaRuntimeCapability PageFooterComposition { get; } = Capability("page.compose.footer", ScadaRuntimeCapabilityOwner.Tf100WebHost);
    /// <summary>Gets the Element+ read-binding capability.</summary>
    public static ScadaRuntimeCapability ElementReadBinding { get; } = Capability("binding.element.read", ScadaRuntimeCapabilityOwner.Tf100WebHost);
    /// <summary>Gets the Element+ write-binding capability.</summary>
    public static ScadaRuntimeCapability ElementWriteBinding { get; } = Capability("binding.element.write", ScadaRuntimeCapabilityOwner.Tf100WebHost);
    /// <summary>Gets the distinct Element+ read/write binding capability.</summary>
    public static ScadaRuntimeCapability DistinctElementWriteBinding { get; } = Capability("binding.element.distinct-write", ScadaRuntimeCapabilityOwner.Tf100WebHost);
    /// <summary>Gets the table-cell read-binding capability.</summary>
    public static ScadaRuntimeCapability TableReadBinding { get; } = Capability("binding.table.read", ScadaRuntimeCapabilityOwner.Tf100WebHost);
    /// <summary>Gets the table-cell write-binding capability.</summary>
    public static ScadaRuntimeCapability TableWriteBinding { get; } = Capability("binding.table.write", ScadaRuntimeCapabilityOwner.Tf100WebHost);
    /// <summary>Gets the distinct table-cell read/write binding capability.</summary>
    public static ScadaRuntimeCapability DistinctTableWriteBinding { get; } = Capability("binding.table.distinct-write", ScadaRuntimeCapabilityOwner.Tf100WebHost);
    /// <summary>Gets the ordered state-rules capability.</summary>
    public static ScadaRuntimeCapability StateRules { get; } = Capability("state.rules", ScadaRuntimeCapabilityOwner.SharedRuntime);
    /// <summary>Gets the state read-variable capability.</summary>
    public static ScadaRuntimeCapability StateReadVariable { get; } = Capability("state.read-variable", ScadaRuntimeCapabilityOwner.SharedRuntime);
    /// <summary>Gets the state quality-fallback capability.</summary>
    public static ScadaRuntimeCapability StateQualityFallback { get; } = Capability("state.quality-fallback", ScadaRuntimeCapabilityOwner.SharedRuntime);
    /// <summary>Gets the state default-effect capability.</summary>
    public static ScadaRuntimeCapability StateDefaultEffect { get; } = Capability("state.default-effect", ScadaRuntimeCapabilityOwner.SharedRuntime);
    /// <summary>Gets the command confirmation capability.</summary>
    public static ScadaRuntimeCapability CommandConfirmation { get; } = Capability("command.confirmation", ScadaRuntimeCapabilityOwner.SharedRuntime);
    /// <summary>Gets the legacy action condition capability.</summary>
    public static ScadaRuntimeCapability ActionCondition { get; } = Blocked("action.condition", ScadaRuntimeCapabilityOwner.SharedRuntime);
    /// <summary>Gets the legacy action condition-group capability.</summary>
    public static ScadaRuntimeCapability ActionConditionGroup { get; } = Blocked("action.condition-group", ScadaRuntimeCapabilityOwner.SharedRuntime);
    /// <summary>Gets the popup options capability.</summary>
    public static ScadaRuntimeCapability PopupOptions { get; } = Blocked("popup.options", ScadaRuntimeCapabilityOwner.Tf100WebHost);
    /// <summary>Gets the disabled button behavior capability.</summary>
    public static ScadaRuntimeCapability ButtonDisabled { get; } = Capability("button.disabled", ScadaRuntimeCapabilityOwner.PackageTransport);
    /// <summary>Gets the hover button behavior capability.</summary>
    public static ScadaRuntimeCapability ButtonHover { get; } = Capability("button.hover", ScadaRuntimeCapabilityOwner.PackageTransport);
    /// <summary>Gets the pressed button behavior capability.</summary>
    public static ScadaRuntimeCapability ButtonPressed { get; } = Capability("button.pressed", ScadaRuntimeCapabilityOwner.PackageTransport);

    /// <summary>Gets every unique capability in stable ordinal order.</summary>
    public static IReadOnlyList<ScadaRuntimeCapability> All { get; } = CollectAll();

    private static IReadOnlyList<ScadaRuntimeCapability> CollectAll()
    {
        var values = new List<ScadaRuntimeCapability>();
        values.AddRange(PageTypes.Values);
        values.AddRange(ElementKinds.Values);
        values.AddRange(ShapeKinds.Values);
        values.AddRange(ButtonKinds.Values);
        values.AddRange(TableCellKinds.Values);
        values.AddRange(ActionKinds.Values);
        values.AddRange(ConditionOperators.Values);
        values.AddRange(ConditionGroupModes.Values);
        values.AddRange(MissingConditionPolicies.Values);
        values.AddRange(PopupPositions.Values);
        values.AddRange(PopupSizes.Values);
        values.AddRange(CommandTriggers.Values);
        values.AddRange(CommandKinds.Values);
        values.AddRange(WriteModes.Values);
        values.AddRange(UnaryOperators.Values);
        values.AddRange(BinaryOperators.Values);
        values.AddRange(ExpressionNodeTypes.Values);
        values.AddRange(ExpressionFunctions.Values);
        values.AddRange(Animations.Values);
        values.AddRange(EffectProperties.Values);
        values.AddRange(new[]
        {
            PageHeaderComposition, PageFooterComposition,
            ElementReadBinding, ElementWriteBinding, DistinctElementWriteBinding,
            TableReadBinding, TableWriteBinding, DistinctTableWriteBinding,
            StateRules, StateReadVariable, StateQualityFallback, StateDefaultEffect,
            CommandConfirmation, ActionCondition, ActionConditionGroup, PopupOptions,
            ButtonDisabled, ButtonHover, ButtonPressed
        });

        return values
            .GroupBy(value => value.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(value => value.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static ScadaRuntimeCapability Capability(
        string id,
        ScadaRuntimeCapabilityOwner owner,
        ScadaRuntimeCapabilityStatus status = ScadaRuntimeCapabilityStatus.Supported,
        ScadaRuntimeCapabilityEvidence? evidence = null) =>
        new(
            id,
            ContractVersion,
            owner,
            status,
            ArtifactsFor(id),
            $"planned:{id}",
            evidence ?? (status == ScadaRuntimeCapabilityStatus.Supported
                ? BaselineEvidence
                : ScadaRuntimeCapabilityEvidence.Pending));

    private static ScadaRuntimeCapability Blocked(string id, ScadaRuntimeCapabilityOwner owner) =>
        Capability(id, owner, ScadaRuntimeCapabilityStatus.Blocked);

    private static IReadOnlyDictionary<T, ScadaRuntimeCapability> Map<T>(
        params (T Key, ScadaRuntimeCapability Capability)[] entries) where T : notnull =>
        entries.ToDictionary(entry => entry.Key, entry => entry.Capability);

    private static IReadOnlyDictionary<T, ScadaRuntimeCapability> EnumCapabilities<T>(
        Func<T, string> idFactory,
        ScadaRuntimeCapabilityOwner owner,
        ScadaRuntimeCapabilityStatus status = ScadaRuntimeCapabilityStatus.Supported) where T : struct, Enum =>
        Enum.GetValues<T>().ToDictionary(value => value, value => Capability(idFactory(value), owner, status));

    private static IReadOnlyList<string> ArtifactsFor(string id)
    {
        if (id.StartsWith("page.", StringComparison.Ordinal))
        {
            return ["manifest.json:Pages", "<page-id>/<page-id>.html"];
        }
        if (id.StartsWith("element.", StringComparison.Ordinal) ||
            id.StartsWith("shape.", StringComparison.Ordinal) ||
            id.StartsWith("button.", StringComparison.Ordinal) ||
            id.StartsWith("table.", StringComparison.Ordinal))
        {
            return ["<page-id>/<page-id>.html:DOM/CSS"];
        }
        if (id.StartsWith("binding.", StringComparison.Ordinal))
        {
            return ["<page-id>/<page-id>.html:data-scada-*", "scada-runtime.<hash>.js"];
        }
        if (id.StartsWith("state.", StringComparison.Ordinal) ||
            id.StartsWith("expression.", StringComparison.Ordinal) ||
            id.StartsWith("effect.", StringComparison.Ordinal) ||
            id.StartsWith("command.", StringComparison.Ordinal))
        {
            return ["<page-id>/<page-id>.html:data-scada-*", "scada-runtime.<hash>.js"];
        }
        if (id.StartsWith("action.", StringComparison.Ordinal) ||
            id.StartsWith("popup.", StringComparison.Ordinal))
        {
            return ["<page-id>/<page-id>.html:action-registry", "scada-runtime.<hash>.js"];
        }
        return ["manifest.json"];
    }

    private static string ToKebabCase(string value)
    {
        var output = new List<char>(value.Length + 4);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (index > 0 && char.IsUpper(character) && !char.IsUpper(value[index - 1]))
            {
                output.Add('-');
            }
            output.Add(char.ToLowerInvariant(character));
        }
        return new string(output.ToArray());
    }
}
