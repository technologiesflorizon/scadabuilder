using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.RuntimeContracts;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.RuntimeContracts;

/// <summary>Result of deriving the runtime capabilities required by one export model.</summary>
public sealed record ScadaRuntimeCapabilityAnalysis(
    IReadOnlyList<ScadaRuntimeCapability> RequiredCapabilities)
{
    /// <summary>Capabilities that are declared but cannot yet be used by strict manifest export.</summary>
    public IReadOnlyList<ScadaRuntimeCapability> BlockedCapabilities => RequiredCapabilities
        .Where(capability => capability.Status == ScadaRuntimeCapabilityStatus.Blocked)
        .ToArray();
}

/// <summary>
/// Derives runtime requirements from project/page metadata and the canonical scene model.
/// The analyzer is pure and does not inspect WPF, files, generated HTML, or TF100Web state.
/// </summary>
/// <remarks>
/// Decisions: DEC-0047.
/// Contracts: docs/superpowers/specs/2026-07-16-scada-v2-tf100web-runtime-conformance-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/RuntimeContracts/ScadaRuntimeCapabilityAnalyzerTests.cs.
/// </remarks>
public static class ScadaRuntimeCapabilityAnalyzer
{
    /// <summary>Analyzes all compiled project references and their available scene models.</summary>
    public static ScadaRuntimeCapabilityAnalysis Analyze(
        ScadaProject project,
        IReadOnlyList<ScadaScene> scenes)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(scenes);

        var capabilities = new Dictionary<string, ScadaRuntimeCapability>(StringComparer.Ordinal);

        foreach (var page in project.Scenes.Where(page => page.IncludeInBuild))
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.PageTypes[page.Type]);
            AddComposition(capabilities, page.HeaderPageId, page.HeaderPageKey, page.FooterPageId, page.FooterPageKey);
        }

        foreach (var scene in scenes.Where(scene => scene.IncludeInBuild))
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.PageTypes[scene.PageType]);
            AddComposition(capabilities, scene.HeaderPageId, scene.HeaderPageKey, scene.FooterPageId, scene.FooterPageKey);

            foreach (var element in Flatten(scene.Elements))
            {
                AnalyzeElement(capabilities, element);
            }

            foreach (var action in scene.ActionDefinitions)
            {
                AnalyzeAction(capabilities, action);
            }
        }

        AnalyzeQuickWindows(capabilities, project);

        return new ScadaRuntimeCapabilityAnalysis(capabilities.Values
            .OrderBy(capability => capability.Id, StringComparer.Ordinal)
            .ToArray());
    }

    /// <summary>
    /// Derives the granular quick-window capabilities, each from its own trigger. Nothing is inferred from
    /// another capability and no umbrella id exists, so a capability may only be promoted with evidence that
    /// carries exactly its own id.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0047, DEC-0050.
    /// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §10.2.
    /// Tests: tests/ScadaBuilderV2.Tests/RuntimeContracts/ScadaRuntimeCapabilityAnalyzerTests.cs.
    /// </remarks>
    private static void AnalyzeQuickWindows(
        IDictionary<string, ScadaRuntimeCapability> capabilities,
        ScadaProject project)
    {
        var definitions = project.EffectiveQuickWindows;
        if (definitions.Count == 0)
        {
            return;
        }

        // One exported definition is enough for transport, host lifecycle and scoped DOM root.
        Add(capabilities, ScadaRuntimeCapabilityCatalog.QuickWindowDefinition);
        Add(capabilities, ScadaRuntimeCapabilityCatalog.QuickWindowSinglePerDefinition);
        Add(capabilities, ScadaRuntimeCapabilityCatalog.QuickWindowHostOwnedLifecycle);
        Add(capabilities, ScadaRuntimeCapabilityCatalog.QuickWindowScopedDomRoot);

        foreach (var definition in definitions)
        {
            var members = definition.EffectiveInterfaceMembers;
            if (members.Count > 0)
            {
                Add(capabilities, ScadaRuntimeCapabilityCatalog.QuickWindowLocalInterfaceTyped);
            }

            if (members.Any(member => member.IsPublic && member.Required))
            {
                Add(capabilities, ScadaRuntimeCapabilityCatalog.QuickWindowPortRequired);
            }

            if (definition.EffectivePresentation.Backdrop)
            {
                Add(capabilities, ScadaRuntimeCapabilityCatalog.QuickWindowBackdrop);
            }

            // A definition whose own content opens another definition is depth-2 nesting.
            if (Flatten(definition.EffectiveContent.EffectiveElements)
                .SelectMany(element => element.EffectiveCommandConfig.Commands)
                .Any(command => command.Kind == ScadaCommandKind.OpenQuickWindow))
            {
                Add(capabilities, ScadaRuntimeCapabilityCatalog.QuickWindowNestingDepth2);
            }
        }

        foreach (var invocation in project.EffectiveQuickWindowInvocations)
        {
            var bindings = invocation.Bindings ?? [];
            if (bindings.Count > 0)
            {
                // An explicit absence is still a persisted binding decision.
                Add(capabilities, ScadaRuntimeCapabilityCatalog.QuickWindowPortBinding);
            }

            if (bindings.Any(binding => binding.SourceKind == QuickWindowBindingSourceKind.ParentPort))
            {
                Add(capabilities, ScadaRuntimeCapabilityCatalog.QuickWindowParentPortBinding);
            }
        }
    }

    private static void AddComposition(
        IDictionary<string, ScadaRuntimeCapability> capabilities,
        string? headerPageId,
        Guid? headerPageKey,
        string? footerPageId,
        Guid? footerPageKey)
    {
        if (!string.IsNullOrWhiteSpace(headerPageId) || headerPageKey is { } headerKey && headerKey != Guid.Empty)
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.PageHeaderComposition);
        }
        if (!string.IsNullOrWhiteSpace(footerPageId) || footerPageKey is { } footerKey && footerKey != Guid.Empty)
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.PageFooterComposition);
        }
    }

    private static void AnalyzeElement(
        IDictionary<string, ScadaRuntimeCapability> capabilities,
        ScadaElement element)
    {
        Add(capabilities, ScadaRuntimeCapabilityCatalog.ElementKinds[element.Kind]);

        if (element.Kind == ScadaElementKind.Shape)
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.ShapeKinds[element.EffectiveShapeKind]);
        }

        if (element.Kind == ScadaElementKind.Button)
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.ButtonKinds[element.EffectiveButtonKind]);
            var behavior = element.EffectiveButtonBehavior;
            if (behavior.IsDisabled)
            {
                Add(capabilities, ScadaRuntimeCapabilityCatalog.ButtonDisabled);
            }
            if (behavior.EffectiveHover.Enabled)
            {
                Add(capabilities, ScadaRuntimeCapabilityCatalog.ButtonHover);
            }
            if (behavior.EffectivePressed.Enabled)
            {
                Add(capabilities, ScadaRuntimeCapabilityCatalog.ButtonPressed);
            }
        }

        AnalyzeElementBindings(capabilities, element.Data);
        AnalyzeTable(capabilities, element.Table);
        AnalyzeState(capabilities, element.StateConfig);
        AnalyzeCommands(capabilities, element.CommandConfig);
    }

    private static void AnalyzeElementBindings(
        IDictionary<string, ScadaRuntimeCapability> capabilities,
        ScadaElementData? data)
    {
        if (data is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(data.ReadTagId))
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.ElementReadBinding);
        }
        if (!string.IsNullOrWhiteSpace(data.WriteTagId))
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.ElementWriteBinding);
            if (!string.IsNullOrWhiteSpace(data.ReadTagId) &&
                !string.Equals(data.ReadTagId, data.WriteTagId, StringComparison.Ordinal))
            {
                Add(capabilities, ScadaRuntimeCapabilityCatalog.DistinctElementWriteBinding);
            }
        }
    }

    private static void AnalyzeTable(
        IDictionary<string, ScadaRuntimeCapability> capabilities,
        ScadaTableDefinition? table)
    {
        if (table is null)
        {
            return;
        }

        foreach (var cell in table.EffectiveCells)
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.TableCellKinds[cell.EffectiveContent.Kind]);
            var binding = cell.ValueBindings;
            if (binding is null)
            {
                continue;
            }
            if (!string.IsNullOrWhiteSpace(binding.ReadTagId))
            {
                Add(capabilities, ScadaRuntimeCapabilityCatalog.TableReadBinding);
            }
            if (!string.IsNullOrWhiteSpace(binding.WriteTagId))
            {
                Add(capabilities, ScadaRuntimeCapabilityCatalog.TableWriteBinding);
                if (!string.IsNullOrWhiteSpace(binding.ReadTagId) &&
                    !string.Equals(binding.ReadTagId, binding.WriteTagId, StringComparison.Ordinal))
                {
                    Add(capabilities, ScadaRuntimeCapabilityCatalog.DistinctTableWriteBinding);
                }
            }
        }
    }

    private static void AnalyzeState(
        IDictionary<string, ScadaRuntimeCapability> capabilities,
        ScadaElementStateConfig? config)
    {
        if (config is null)
        {
            return;
        }

        Add(capabilities, ScadaRuntimeCapabilityCatalog.StateQualityFallback);
        Add(capabilities, ScadaRuntimeCapabilityCatalog.StateDefaultEffect);
        AnalyzeEffect(capabilities, config.QualityFallback);
        AnalyzeEffect(capabilities, config.DefaultEffect);

        if (config.ReadVariable is not null)
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.StateReadVariable);
        }

        if (config.States.Count > 0)
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.StateRules);
        }

        foreach (var state in config.States)
        {
            if (state.Expression.Ast is not null)
            {
                AnalyzeExpression(capabilities, state.Expression.Ast);
            }
            AnalyzeEffect(capabilities, state.Effect);
        }
    }

    private static void AnalyzeExpression(
        IDictionary<string, ScadaRuntimeCapability> capabilities,
        ScadaExprNode node)
    {
        if (ScadaRuntimeCapabilityCatalog.ExpressionNodeTypes.TryGetValue(node.GetType(), out var nodeCapability))
        {
            Add(capabilities, nodeCapability);
        }

        switch (node)
        {
            case ScadaExprUnary unary:
                Add(capabilities, ScadaRuntimeCapabilityCatalog.UnaryOperators[unary.Op]);
                AnalyzeExpression(capabilities, unary.Operand);
                break;
            case ScadaExprBinary binary:
                Add(capabilities, ScadaRuntimeCapabilityCatalog.BinaryOperators[binary.Op]);
                AnalyzeExpression(capabilities, binary.Left);
                AnalyzeExpression(capabilities, binary.Right);
                break;
            case ScadaExprFunc function:
                if (ScadaRuntimeCapabilityCatalog.ExpressionFunctions.TryGetValue(function.Name, out var functionCapability))
                {
                    Add(capabilities, functionCapability);
                }
                foreach (var argument in function.Args)
                {
                    AnalyzeExpression(capabilities, argument);
                }
                break;
        }
    }

    private static void AnalyzeEffect(
        IDictionary<string, ScadaRuntimeCapability> capabilities,
        ScadaEffectBlock effect)
    {
        AddEffectProperty(capabilities, nameof(effect.BackgroundColor), effect.BackgroundColor);
        AddEffectProperty(capabilities, nameof(effect.BorderColor), effect.BorderColor);
        AddEffectProperty(capabilities, nameof(effect.BorderWidth), effect.BorderWidth);
        AddEffectProperty(capabilities, nameof(effect.TextColor), effect.TextColor);
        AddEffectProperty(capabilities, nameof(effect.TextContent), effect.TextContent);
        AddEffectProperty(capabilities, nameof(effect.TextVisible), effect.TextVisible);
        AddEffectProperty(capabilities, nameof(effect.ElementVisible), effect.ElementVisible);
        AddEffectProperty(capabilities, nameof(effect.Opacity), effect.Opacity);
        AddEffectProperty(capabilities, nameof(effect.Rotation), effect.Rotation);
        AddEffectProperty(capabilities, nameof(effect.ColorFilterColor), effect.ColorFilterColor);
        AddEffectProperty(capabilities, nameof(effect.ColorFilterOpacity), effect.ColorFilterOpacity);
        AddEffectProperty(capabilities, nameof(effect.ColorFilterHalo), effect.ColorFilterHalo);
        AddEffectProperty(capabilities, nameof(effect.ColorFilterHaloColor), effect.ColorFilterHaloColor);
        if (effect.Animation is { } animation)
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.EffectProperties[nameof(effect.Animation)]);
            Add(capabilities, ScadaRuntimeCapabilityCatalog.Animations[animation]);
        }
    }

    private static void AddEffectProperty(
        IDictionary<string, ScadaRuntimeCapability> capabilities,
        string propertyName,
        object? value)
    {
        if (value is not null)
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.EffectProperties[propertyName]);
        }
    }

    private static void AnalyzeCommands(
        IDictionary<string, ScadaRuntimeCapability> capabilities,
        ScadaElementCommandConfig? config)
    {
        if (config is null)
        {
            return;
        }

        foreach (var command in config.Commands)
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.CommandTriggers[command.Trigger]);
            Add(capabilities, ScadaRuntimeCapabilityCatalog.CommandKinds[command.Kind]);
            if (command.WriteMode is { } writeMode)
            {
                Add(capabilities, ScadaRuntimeCapabilityCatalog.WriteModes[writeMode]);
            }
            if (command.Confirmation is not null)
            {
                Add(capabilities, ScadaRuntimeCapabilityCatalog.CommandConfirmation);
            }
        }
    }

    private static void AnalyzeAction(
        IDictionary<string, ScadaRuntimeCapability> capabilities,
        ScadaActionDefinition action)
    {
        Add(capabilities, ScadaRuntimeCapabilityCatalog.ActionKinds[action.Kind]);
        if (action.Condition is not null)
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.ActionCondition);
            Add(capabilities, ScadaRuntimeCapabilityCatalog.ConditionOperators[action.Condition.Operator]);
        }
        if (action.ConditionGroup is { } group)
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.ActionConditionGroup);
            Add(capabilities, ScadaRuntimeCapabilityCatalog.ConditionGroupModes[group.Mode]);
            Add(capabilities, ScadaRuntimeCapabilityCatalog.MissingConditionPolicies[group.MissingTagPolicy]);
            foreach (var condition in group.Conditions)
            {
                Add(capabilities, ScadaRuntimeCapabilityCatalog.ConditionOperators[condition.Operator]);
            }
        }
        if (action.PopupOptions is { } popup)
        {
            Add(capabilities, ScadaRuntimeCapabilityCatalog.PopupOptions);
            Add(capabilities, ScadaRuntimeCapabilityCatalog.PopupPositions[popup.Position]);
            Add(capabilities, ScadaRuntimeCapabilityCatalog.PopupSizes[popup.SizePreset]);
        }
    }

    private static IEnumerable<ScadaElement> Flatten(IEnumerable<ScadaElement> elements)
    {
        foreach (var element in elements)
        {
            yield return element;
            foreach (var child in Flatten(element.ChildElements))
            {
                yield return child;
            }
        }
    }

    private static void Add(
        IDictionary<string, ScadaRuntimeCapability> capabilities,
        ScadaRuntimeCapability capability) => capabilities[capability.Id] = capability;
}
