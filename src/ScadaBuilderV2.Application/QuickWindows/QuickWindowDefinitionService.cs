using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.Application.QuickWindows;

/// <summary>One prepared, immutable transition of the project quick-window workspace.</summary>
public sealed record QuickWindowWorkspaceMutation(
    PageWorkspaceSnapshot Before,
    PageWorkspaceSnapshot After,
    CommandResult Result,
    string HistoryLabel,
    Guid? AffectedDefinitionKey = null,
    Guid? AffectedInvocationKey = null,
    QuickWindowUsage? UsageToNavigate = null);

/// <summary>Creates, updates, routes to and deletes quick-window definitions without WPF or file I/O.</summary>
/// <remarks>
/// Decisions: DEC-0050, FR-001, FR-014, FR-018, FR-019.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §§8.2, 9.1, 11.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowApplicationTests.cs.
/// </remarks>
public sealed class QuickWindowDefinitionService(QuickWindowDependencyAnalyzer? dependencyAnalyzer = null)
{
    private readonly QuickWindowDependencyAnalyzer dependencyAnalyzer = dependencyAnalyzer ?? new QuickWindowDependencyAnalyzer();

    /// <summary>Creates a new empty definition after validating its portable project-local code.</summary>
    public QuickWindowWorkspaceMutation CreateEmpty(PageWorkspaceSnapshot snapshot, string code, string displayName)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        code = code?.Trim() ?? string.Empty;
        displayName = displayName?.Trim() ?? string.Empty;
        var definition = QuickWindowDefinition.CreateEmpty(code, displayName);
        var issues = ValidateCandidate(snapshot, definition, current: null);
        if (issues.Count > 0)
            return Blocked(snapshot, "create quick window", issues);

        var definitions = snapshot.Project.EffectiveQuickWindows.Append(definition).ToArray();
        var after = Next(snapshot, snapshot.Project with { QuickWindows = definitions });
        return Changed(snapshot, after, "Quick window created.", "create quick window", definition.DefinitionKey);
    }

    /// <summary>
    /// Updates metadata, content or the typed interface. Breaking member removal requires an explicit caller decision,
    /// while interface contract changes require a higher InterfaceVersion.
    /// </summary>
    public QuickWindowWorkspaceMutation Update(
        PageWorkspaceSnapshot snapshot,
        QuickWindowDefinition candidate,
        bool confirmReferencedMemberRemoval = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(candidate);
        var current = snapshot.Project.EffectiveQuickWindows.FirstOrDefault(definition => definition.DefinitionKey == candidate.DefinitionKey);
        if (current is null)
        {
            return Blocked(snapshot, "update quick window", [Issue(
                "quick-window.definition-missing",
                $"Definition '{candidate.DefinitionKey}' does not exist.",
                "Project.QuickWindows",
                candidate.DefinitionKey)]);
        }

        var issues = ValidateCandidate(snapshot, candidate, current);
        if (InterfaceContractChanged(current, candidate) && candidate.InterfaceVersion <= current.InterfaceVersion)
        {
            issues.Add(Issue(
                "quick-window.interface-version-not-incremented",
                "Changing the local interface contract requires a higher InterfaceVersion.",
                $"Project.QuickWindows[{candidate.DefinitionKey}].InterfaceVersion",
                candidate.DefinitionKey));
        }

        var removedMemberKeys = current.EffectiveInterfaceMembers.Select(member => member.MemberKey)
            .Except(candidate.EffectiveInterfaceMembers.Select(member => member.MemberKey))
            .ToArray();
        var analysis = dependencyAnalyzer.Analyze(snapshot);
        var removedMemberUsages = removedMemberKeys
            .SelectMany(memberKey => analysis.GetMemberUsages(current.DefinitionKey, memberKey))
            .ToArray();
        if (removedMemberUsages.Length > 0 && !confirmReferencedMemberRemoval)
        {
            issues.AddRange(removedMemberUsages.Select(usage => Issue(
                "quick-window.interface-member-in-use",
                $"Interface member '{usage.MemberKey}' is still referenced by invocation '{usage.InvocationKey}'.",
                usage.PropertyPath,
                usage.MemberKey)));
        }

        if (issues.Count > 0)
            return Blocked(snapshot, "update quick window", issues);

        var definitions = snapshot.Project.EffectiveQuickWindows
            .Select(definition => definition.DefinitionKey == candidate.DefinitionKey ? candidate : definition)
            .ToArray();
        var after = Next(snapshot, snapshot.Project with { QuickWindows = definitions });
        var diagnostics = confirmReferencedMemberRemoval
            ? dependencyAnalyzer.Analyze(after).Diagnostics
            : Array.Empty<ScadaBuildValidationIssue>();
        return Changed(snapshot, after, "Quick window updated.", "update quick window", candidate.DefinitionKey, diagnostics);
    }

    /// <summary>Lists every caller and unattached invocation that currently prevents definition deletion.</summary>
    public IReadOnlyList<QuickWindowUsage> ListUsages(PageWorkspaceSnapshot snapshot, Guid definitionKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return dependencyAnalyzer.Analyze(snapshot).GetInbound(definitionKey);
    }

    /// <summary>Produces a non-mutating navigation result for a stable usage entry.</summary>
    public QuickWindowWorkspaceMutation NavigateToUsage(PageWorkspaceSnapshot snapshot, Guid definitionKey, int usageIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var usages = ListUsages(snapshot, definitionKey);
        if (usageIndex < 0 || usageIndex >= usages.Count)
        {
            return Blocked(snapshot, "navigate to quick-window usage", [Issue(
                "quick-window.usage-missing",
                "The requested quick-window usage does not exist.",
                "Project.QuickWindows",
                definitionKey)]);
        }

        var usage = usages[usageIndex];
        var result = CommandResult.NoChange(
            "Quick-window usage selected.",
            pageToSelectKey: usage.OwnerPageKey,
            pageToOpenKey: usage.OwnerPageKey);
        return new QuickWindowWorkspaceMutation(
            snapshot,
            snapshot,
            result,
            "navigate to quick-window usage",
            definitionKey,
            usage.InvocationKey,
            usage);
    }

    /// <summary>Deletes an unreferenced definition; any caller or orphan invocation blocks the operation.</summary>
    public QuickWindowWorkspaceMutation Delete(PageWorkspaceSnapshot snapshot, Guid definitionKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var definition = snapshot.Project.EffectiveQuickWindows.FirstOrDefault(item => item.DefinitionKey == definitionKey);
        if (definition is null)
        {
            return Blocked(snapshot, "delete quick window", [Issue(
                "quick-window.definition-missing",
                $"Definition '{definitionKey}' does not exist.",
                "Project.QuickWindows",
                definitionKey)]);
        }

        var usages = ListUsages(snapshot, definitionKey);
        if (usages.Count > 0)
        {
            var diagnostics = usages.Select(usage => Issue(
                "quick-window.delete-dependency",
                $"Definition '{definition.EffectiveCode}' is still referenced by invocation '{usage.InvocationKey}'.",
                usage.PropertyPath,
                definitionKey)).ToArray();
            return Blocked(snapshot, "delete quick window", diagnostics);
        }

        var definitions = snapshot.Project.EffectiveQuickWindows
            .Where(item => item.DefinitionKey != definitionKey)
            .ToArray();
        var after = Next(snapshot, snapshot.Project with { QuickWindows = definitions });
        return Changed(snapshot, after, "Quick window deleted.", "delete quick window", definitionKey);
    }

    private static List<ScadaBuildValidationIssue> ValidateCandidate(
        PageWorkspaceSnapshot snapshot,
        QuickWindowDefinition candidate,
        QuickWindowDefinition? current)
    {
        var codes = snapshot.Project.EffectiveQuickWindows.Select(definition => definition.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return QuickWindowValidation.ValidateDefinition(candidate, codes, current?.Code)
            .Select(message => Issue(
                "quick-window.definition-invalid",
                message,
                $"Project.QuickWindows[{candidate.DefinitionKey}]",
                candidate.DefinitionKey))
            .ToList();
    }

    private static bool InterfaceContractChanged(QuickWindowDefinition before, QuickWindowDefinition after)
    {
        static string Signature(QuickWindowInterfaceMember member) => string.Join("|",
            member.MemberKey,
            member.Name,
            member.Family,
            member.DataType,
            member.Access,
            member.Required,
            member.DefaultValue);
        return !before.EffectiveInterfaceMembers.Select(Signature).OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(after.EffectiveInterfaceMembers.Select(Signature).OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal);
    }

    private static PageWorkspaceSnapshot Next(PageWorkspaceSnapshot snapshot, ScadaProject project) =>
        snapshot with { Version = snapshot.Version + 1, Project = project };

    private static QuickWindowWorkspaceMutation Changed(
        PageWorkspaceSnapshot before,
        PageWorkspaceSnapshot after,
        string message,
        string label,
        Guid definitionKey,
        IReadOnlyList<ScadaBuildValidationIssue>? diagnostics = null) =>
        new(before, after, CommandResult.Success(message, workspaceDirty: true, diagnostics: diagnostics), label, definitionKey);

    private static QuickWindowWorkspaceMutation Blocked(
        PageWorkspaceSnapshot snapshot,
        string label,
        IReadOnlyList<ScadaBuildValidationIssue> issues) =>
        new(snapshot, snapshot, CommandResult.Blocked("Quick-window mutation was blocked.", issues), label);

    private static ScadaBuildValidationIssue Issue(string code, string message, string propertyPath, Guid? targetKey) =>
        new(
            ScadaBuildValidationSeverity.Error,
            code,
            message,
            PropertyPath: propertyPath,
            TargetKey: targetKey,
            SuggestedFix: "Repair the reference or make an explicit compatible interface change.");
}
