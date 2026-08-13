using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.QuickWindows;

/// <summary>Identifies where a durable quick-window invocation is owned.</summary>
public enum QuickWindowUsageKind
{
    PageCommand,
    DefinitionCommand,
    UnattachedInvocation,
    InvocationBinding
}

/// <summary>One navigable use of a quick-window definition, invocation or interface member.</summary>
/// <remarks>
/// Decisions: DEC-0050, FR-012, FR-014, FR-018.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §§8.4, 11.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowApplicationTests.cs.
/// </remarks>
public sealed record QuickWindowUsage(
    QuickWindowUsageKind Kind,
    Guid DefinitionKey,
    Guid InvocationKey,
    Guid? OwnerPageKey,
    Guid? OwnerDefinitionKey,
    string? ElementId,
    string? CommandId,
    string PropertyPath,
    Guid? MemberKey = null,
    bool IsResolved = true);

/// <summary>Complete dependency graph and authoring diagnostics for one coherent workspace snapshot.</summary>
public sealed record QuickWindowDependencyAnalysis(
    IReadOnlyList<QuickWindowUsage> Usages,
    IReadOnlyList<ScadaBuildValidationIssue> Diagnostics)
{
    /// <summary>Returns every invocation or command that currently targets a definition.</summary>
    public IReadOnlyList<QuickWindowUsage> GetInbound(Guid definitionKey) =>
        Usages.Where(usage => usage.DefinitionKey == definitionKey && usage.MemberKey is null).ToArray();

    /// <summary>Returns every invocation binding that currently references an interface member.</summary>
    public IReadOnlyList<QuickWindowUsage> GetMemberUsages(Guid definitionKey, Guid memberKey) =>
        Usages.Where(usage => usage.DefinitionKey == definitionKey && usage.MemberKey == memberKey).ToArray();
}

/// <summary>Builds the page/definition/invocation graph without WPF, persistence or runtime dependencies.</summary>
/// <remarks>
/// Decisions: DEC-0050, FR-012, FR-014, FR-018.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §§8.4, 11, 14.1.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowApplicationTests.cs.
/// </remarks>
public sealed class QuickWindowDependencyAnalyzer
{
    /// <summary>Analyzes all definitions, invocations and commands from one coherent workspace snapshot.</summary>
    public QuickWindowDependencyAnalysis Analyze(PageWorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var definitions = snapshot.Project.EffectiveQuickWindows;
        var invocations = snapshot.Project.EffectiveQuickWindowInvocations;
        var definitionsByKey = definitions
            .GroupBy(definition => definition.DefinitionKey)
            .ToDictionary(group => group.Key, group => group.First());
        var invocationsByKey = invocations
            .GroupBy(invocation => invocation.InvocationKey)
            .ToDictionary(group => group.Key, group => group.First());
        var usages = new List<QuickWindowUsage>();
        var diagnostics = new List<ScadaBuildValidationIssue>();
        var referencedInvocations = new HashSet<Guid>();
        var definitionEdges = new List<DefinitionEdge>();

        foreach (var duplicate in definitions.GroupBy(definition => definition.DefinitionKey).Where(group => group.Count() > 1))
        {
            AddDiagnostic("quick-window.duplicate-definition", $"DefinitionKey '{duplicate.Key}' is duplicated.",
                "Project.QuickWindows", duplicate.Key);
        }

        foreach (var duplicate in invocations.GroupBy(invocation => invocation.InvocationKey).Where(group => group.Count() > 1))
        {
            AddDiagnostic("quick-window.duplicate-invocation", $"InvocationKey '{duplicate.Key}' is duplicated.",
                "Project.QuickWindowInvocations", duplicate.Key);
        }

        foreach (var invocation in invocations)
        {
            if (!definitionsByKey.TryGetValue(invocation.DefinitionKey, out var definition))
            {
                AddDiagnostic(
                    "quick-window.definition-missing",
                    $"Invocation '{invocation.InvocationKey}' references missing definition '{invocation.DefinitionKey}'.",
                    $"Project.QuickWindowInvocations[{invocation.InvocationKey}].DefinitionKey",
                    invocation.DefinitionKey);
                continue;
            }

            foreach (var binding in invocation.Bindings ?? Array.Empty<QuickWindowBinding>())
            {
                usages.Add(new QuickWindowUsage(
                    QuickWindowUsageKind.InvocationBinding,
                    definition.DefinitionKey,
                    invocation.InvocationKey,
                    invocation.OwnerPageKey,
                    null,
                    invocation.OwnerElementId,
                    invocation.OwnerCommandId,
                    $"Project.QuickWindowInvocations[{invocation.InvocationKey}].Bindings[{binding.MemberKey}]",
                    binding.MemberKey,
                    definition.EffectiveInterfaceMembers.Any(member => member.MemberKey == binding.MemberKey)));
            }

            foreach (var result in QuickWindowBindingValidator.ValidateInvocation(invocation, definition, snapshot.Project.TagCatalog)
                         .Where(result => !result.IsValid))
            {
                var code = result.ErrorCode switch
                {
                    "binding.unknown-member" => "quick-window.port-removed",
                    "invocation.interface-version-mismatch" => "quick-window.interface-version-incompatible",
                    _ => $"quick-window.{result.ErrorCode ?? "binding-invalid"}"
                };
                diagnostics.Add(new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Warning,
                    code,
                    result.Message ?? "Quick-window invocation is invalid.",
                    PageKey: invocation.OwnerPageKey,
                    ElementId: invocation.OwnerElementId,
                    CommandId: invocation.OwnerCommandId,
                    PropertyPath: $"Project.QuickWindowInvocations[{invocation.InvocationKey}]",
                    TargetKey: invocation.InvocationKey,
                    SuggestedFix: "Review the definition interface version and every explicit binding."));
            }
        }

        foreach (var page in snapshot.Project.Scenes)
        {
            if (!snapshot.Scenes.TryGetValue(page.PageKey, out var scene))
            {
                AddDiagnostic(
                    "quick-window.scene-snapshot-missing",
                    $"Page '{page.EffectivePageCode}' has no scene in the coherent workspace snapshot.",
                    "Workspace.Scenes",
                    page.PageKey,
                    page.PageKey,
                    page.EffectivePageCode);
                continue;
            }

            AnalyzeCommands(
                scene.Elements,
                QuickWindowUsageKind.PageCommand,
                page.PageKey,
                null,
                $"Scenes[{page.PageKey}]",
                page.EffectivePageCode);
        }

        foreach (var definition in definitions)
        {
            AnalyzeCommands(
                definition.EffectiveContent.EffectiveElements,
                QuickWindowUsageKind.DefinitionCommand,
                null,
                definition.DefinitionKey,
                $"Project.QuickWindows[{definition.DefinitionKey}].Content",
                null);
        }

        foreach (var invocation in invocations.Where(invocation => !referencedInvocations.Contains(invocation.InvocationKey)))
        {
            usages.Add(new QuickWindowUsage(
                QuickWindowUsageKind.UnattachedInvocation,
                invocation.DefinitionKey,
                invocation.InvocationKey,
                invocation.OwnerPageKey,
                null,
                invocation.OwnerElementId,
                invocation.OwnerCommandId,
                $"Project.QuickWindowInvocations[{invocation.InvocationKey}]",
                IsResolved: definitionsByKey.ContainsKey(invocation.DefinitionKey)));
        }

        ValidateCyclesAndDepth(definitionsByKey.Keys, definitionEdges, diagnostics);

        var stableDiagnostics = diagnostics
            .GroupBy(issue => (issue.Code, issue.PropertyPath, issue.TargetKey, issue.Message))
            .Select(group => group.First())
            .OrderBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.PropertyPath, StringComparer.Ordinal)
            .ThenBy(issue => issue.TargetKey)
            .ToArray();
        var stableUsages = usages
            .OrderBy(usage => usage.DefinitionKey)
            .ThenBy(usage => usage.InvocationKey)
            .ThenBy(usage => usage.PropertyPath, StringComparer.Ordinal)
            .ThenBy(usage => usage.MemberKey)
            .ToArray();
        return new QuickWindowDependencyAnalysis(stableUsages, stableDiagnostics);

        void AnalyzeCommands(
            IEnumerable<ScadaElement> elements,
            QuickWindowUsageKind usageKind,
            Guid? ownerPageKey,
            Guid? ownerDefinitionKey,
            string ownerPath,
            string? pageCode)
        {
            foreach (var element in Flatten(elements))
            {
                foreach (var command in element.EffectiveCommandConfig.Commands.Where(command => command.Kind == ScadaCommandKind.OpenQuickWindow))
                {
                    var path = $"{ownerPath}.Elements[{element.Id}].CommandConfig.Commands[{command.Id}].QuickWindowInvocationKey";
                    if (command.QuickWindowInvocationKey is not { } invocationKey || invocationKey == Guid.Empty)
                    {
                        AddDiagnostic(
                            "quick-window.invocation-missing",
                            $"OpenQuickWindow command '{command.Id}' requires an InvocationKey.",
                            path,
                            null,
                            ownerPageKey,
                            pageCode,
                            element.Id,
                            command.Id);
                        continue;
                    }

                    referencedInvocations.Add(invocationKey);
                    if (!invocationsByKey.TryGetValue(invocationKey, out var invocation))
                    {
                        AddDiagnostic(
                            "quick-window.invocation-missing",
                            $"OpenQuickWindow command '{command.Id}' references missing invocation '{invocationKey}'.",
                            path,
                            invocationKey,
                            ownerPageKey,
                            pageCode,
                            element.Id,
                            command.Id);
                        continue;
                    }

                    var resolved = definitionsByKey.ContainsKey(invocation.DefinitionKey);
                    usages.Add(new QuickWindowUsage(
                        usageKind,
                        invocation.DefinitionKey,
                        invocation.InvocationKey,
                        ownerPageKey,
                        ownerDefinitionKey,
                        element.Id,
                        command.Id,
                        path,
                        IsResolved: resolved));

                    if (ownerDefinitionKey is { } sourceDefinitionKey)
                    {
                        definitionEdges.Add(new DefinitionEdge(sourceDefinitionKey, invocation.DefinitionKey, path));
                    }

                    if (!resolved)
                    {
                        AddDiagnostic(
                            "quick-window.definition-missing",
                            $"Invocation '{invocation.InvocationKey}' references missing definition '{invocation.DefinitionKey}'.",
                            path,
                            invocation.DefinitionKey,
                            ownerPageKey,
                            pageCode,
                            element.Id,
                            command.Id);
                    }
                    else if (ownerPageKey is { } pageKey &&
                             ((invocation.OwnerPageKey is { } declaredPage && declaredPage != pageKey) ||
                              (!string.IsNullOrWhiteSpace(invocation.OwnerElementId) && !string.Equals(invocation.OwnerElementId, element.Id, StringComparison.Ordinal)) ||
                              (!string.IsNullOrWhiteSpace(invocation.OwnerCommandId) && !string.Equals(invocation.OwnerCommandId, command.Id, StringComparison.Ordinal))))
                    {
                        AddDiagnostic(
                            "quick-window.invocation-owner-mismatch",
                            $"Invocation '{invocation.InvocationKey}' owner metadata does not match its caller.",
                            path,
                            invocation.InvocationKey,
                            pageKey,
                            pageCode,
                            element.Id,
                            command.Id,
                            ScadaBuildValidationSeverity.Warning);
                    }
                }
            }
        }

        void AddDiagnostic(
            string code,
            string message,
            string propertyPath,
            Guid? targetKey = null,
            Guid? pageKey = null,
            string? pageCode = null,
            string? elementId = null,
            string? commandId = null,
            ScadaBuildValidationSeverity severity = ScadaBuildValidationSeverity.Error)
        {
            diagnostics.Add(new ScadaBuildValidationIssue(
                severity,
                code,
                message,
                pageCode,
                pageKey,
                elementId,
                commandId,
                propertyPath,
                targetKey,
                "Repair or remove the stale quick-window reference."));
        }
    }

    private static void ValidateCyclesAndDepth(
        IEnumerable<Guid> definitionKeys,
        IReadOnlyList<DefinitionEdge> edges,
        List<ScadaBuildValidationIssue> diagnostics)
    {
        var edgesBySource = edges
            .GroupBy(edge => edge.SourceDefinitionKey)
            .ToDictionary(group => group.Key, group => group.OrderBy(edge => edge.PropertyPath, StringComparer.Ordinal).ToArray());
        var seenCycles = new HashSet<string>(StringComparer.Ordinal);
        var seenDepthEdges = new HashSet<string>(StringComparer.Ordinal);

        foreach (var root in definitionKeys.OrderBy(key => key))
        {
            Visit(root, 1, new List<Guid> { root });
        }

        void Visit(Guid current, int depth, List<Guid> path)
        {
            if (!edgesBySource.TryGetValue(current, out var outgoing))
                return;

            foreach (var edge in outgoing)
            {
                var cycleIndex = path.IndexOf(edge.TargetDefinitionKey);
                if (cycleIndex >= 0)
                {
                    var cycle = path.Skip(cycleIndex).Append(edge.TargetDefinitionKey).ToArray();
                    var canonical = CanonicalCycle(cycle);
                    if (seenCycles.Add(canonical))
                    {
                        diagnostics.Add(new ScadaBuildValidationIssue(
                            ScadaBuildValidationSeverity.Error,
                            "cycle/depth-exceeded",
                            $"Quick-window dependency cycle detected: {string.Join(" -> ", cycle)}.",
                            PropertyPath: edge.PropertyPath,
                            TargetKey: edge.TargetDefinitionKey,
                            SuggestedFix: "Remove the recursive OpenQuickWindow command."));
                    }
                    continue;
                }

                var targetDepth = depth + 1;
                if (targetDepth > 2)
                {
                    if (seenDepthEdges.Add(edge.PropertyPath))
                    {
                        diagnostics.Add(new ScadaBuildValidationIssue(
                            ScadaBuildValidationSeverity.Error,
                            "cycle/depth-exceeded",
                            $"Quick-window nesting depth exceeds two definitions at '{edge.TargetDefinitionKey}'.",
                            PropertyPath: edge.PropertyPath,
                            TargetKey: edge.TargetDefinitionKey,
                            SuggestedFix: "Keep Page -> A -> B as the deepest supported chain."));
                    }
                    continue;
                }

                path.Add(edge.TargetDefinitionKey);
                Visit(edge.TargetDefinitionKey, targetDepth, path);
                path.RemoveAt(path.Count - 1);
            }
        }
    }

    private static string CanonicalCycle(IReadOnlyList<Guid> cycle)
    {
        var nodes = cycle.Take(cycle.Count - 1).Select(key => key.ToString("N")).ToArray();
        if (nodes.Length == 0)
            return string.Empty;
        return Enumerable.Range(0, nodes.Length)
            .Select(offset => string.Join(">", Enumerable.Range(0, nodes.Length).Select(index => nodes[(index + offset) % nodes.Length])))
            .OrderBy(value => value, StringComparer.Ordinal)
            .First();
    }

    private static IEnumerable<ScadaElement> Flatten(IEnumerable<ScadaElement> elements)
    {
        foreach (var element in elements)
        {
            yield return element;
            foreach (var child in Flatten(element.ChildElements))
                yield return child;
        }
    }

    private sealed record DefinitionEdge(Guid SourceDefinitionKey, Guid TargetDefinitionKey, string PropertyPath);
}
