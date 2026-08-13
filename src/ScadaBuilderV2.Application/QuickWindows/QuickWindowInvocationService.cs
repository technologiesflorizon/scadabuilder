using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.QuickWindows;

/// <summary>Typed request that atomically associates one caller command with one invocation descriptor.</summary>
public sealed record UpsertQuickWindowInvocationRequest(
    Guid OwnerPageKey,
    string OwnerElementId,
    string OwnerCommandId,
    Guid DefinitionKey,
    IReadOnlyList<QuickWindowBinding> Bindings,
    Guid? InvocationKey = null,
    string? TitleOverride = null);

/// <summary>Coordinates caller commands and invocation descriptors as one immutable workspace mutation.</summary>
/// <remarks>
/// Decisions: DEC-0050, FR-008, FR-014, FR-022.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §§8.4, 8.5, 14.1.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowApplicationTests.cs.
/// </remarks>
public sealed class QuickWindowInvocationService(QuickWindowDependencyAnalyzer? dependencyAnalyzer = null)
{
    private readonly QuickWindowDependencyAnalyzer dependencyAnalyzer = dependencyAnalyzer ?? new QuickWindowDependencyAnalyzer();

    /// <summary>
    /// Creates or replaces one invocation and updates its OpenQuickWindow command in the same prepared snapshot.
    /// Invalid authoring bindings remain saveable and are returned as warnings; broken owner/definition references block.
    /// </summary>
    public QuickWindowWorkspaceMutation Upsert(PageWorkspaceSnapshot snapshot, UpsertQuickWindowInvocationRequest request)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);
        var definition = snapshot.Project.EffectiveQuickWindows.FirstOrDefault(item => item.DefinitionKey == request.DefinitionKey);
        if (definition is null)
            return Block(snapshot, "save quick-window invocation", "quick-window.definition-missing", "The target quick-window definition does not exist.", request.DefinitionKey);
        if (!snapshot.Scenes.TryGetValue(request.OwnerPageKey, out var scene))
            return Block(snapshot, "save quick-window invocation", "quick-window.owner-page-missing", "The caller page is not loaded in the workspace snapshot.", request.OwnerPageKey);

        var element = scene.FindElementRecursive(request.OwnerElementId);
        if (element is null)
            return Block(snapshot, "save quick-window invocation", "quick-window.owner-element-missing", "The caller element does not exist.", request.OwnerPageKey);
        var command = element.EffectiveCommandConfig.Commands.FirstOrDefault(item => string.Equals(item.Id, request.OwnerCommandId, StringComparison.Ordinal));
        if (command is null || command.Kind != ScadaCommandKind.OpenQuickWindow)
            return Block(snapshot, "save quick-window invocation", "quick-window.owner-command-invalid", "The caller command must be an existing OpenQuickWindow command.", request.OwnerPageKey);

        var invocationKey = request.InvocationKey is { } supplied && supplied != Guid.Empty
            ? supplied
            : command.QuickWindowInvocationKey is { } existingKey && existingKey != Guid.Empty
                ? existingKey
                : Guid.NewGuid();
        var existing = snapshot.Project.EffectiveQuickWindowInvocations.FirstOrDefault(item => item.InvocationKey == invocationKey);
        if (existing is not null && !IsSameOwner(existing, request))
            return Block(snapshot, "save quick-window invocation", "quick-window.invocation-owner-conflict", "InvocationKey is already owned by another caller.", invocationKey);

        var bindings = (request.Bindings ?? Array.Empty<QuickWindowBinding>())
            .Select(binding => binding with { })
            .ToArray();
        var invocation = new QuickWindowInvocation(
            invocationKey,
            definition.DefinitionKey,
            bindings,
            request.TitleOverride,
            definition.InterfaceVersion,
            request.OwnerPageKey,
            request.OwnerElementId,
            request.OwnerCommandId);
        var invocations = snapshot.Project.EffectiveQuickWindowInvocations
            .Where(item => item.InvocationKey != invocationKey)
            .Append(invocation)
            .ToArray();
        var updatedCommands = element.EffectiveCommandConfig.Commands
            .Select(item => string.Equals(item.Id, command.Id, StringComparison.Ordinal)
                ? item with { QuickWindowInvocationKey = invocationKey }
                : item)
            .ToArray();
        var updatedScene = scene.WithReplacedElementRecursive(element with
        {
            CommandConfig = new ScadaElementCommandConfig(updatedCommands)
        });
        var scenes = new Dictionary<Guid, ScadaScene>(snapshot.Scenes)
        {
            [request.OwnerPageKey] = updatedScene
        };
        var after = snapshot with
        {
            Version = snapshot.Version + 1,
            Project = snapshot.Project with { QuickWindowInvocations = invocations },
            Scenes = scenes
        };
        var diagnostics = QuickWindowBindingValidator.ValidateInvocation(invocation, definition, snapshot.Project.TagCatalog)
            .Where(result => !result.IsValid)
            .Select(result => new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Warning,
                result.ErrorCode == "binding.required-missing" ? "quick-window.required-missing" : $"quick-window.{result.ErrorCode ?? "binding-invalid"}",
                result.Message ?? "Quick-window binding is invalid.",
                PageKey: request.OwnerPageKey,
                ElementId: request.OwnerElementId,
                CommandId: request.OwnerCommandId,
                PropertyPath: $"Project.QuickWindowInvocations[{invocationKey}]",
                TargetKey: invocationKey,
                SuggestedFix: "Complete or repair the typed binding before build/export."))
            .ToArray();
        return new QuickWindowWorkspaceMutation(
            snapshot,
            after,
            CommandResult.Success("Quick-window invocation saved.", [request.OwnerPageKey], request.OwnerPageKey, workspaceDirty: true, diagnostics: diagnostics),
            "save quick-window invocation",
            definition.DefinitionKey,
            invocationKey);
    }

    /// <summary>Removes an invocation and clears exactly the command that owned it.</summary>
    public QuickWindowWorkspaceMutation RemoveInvocation(PageWorkspaceSnapshot snapshot, Guid invocationKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var invocation = snapshot.Project.EffectiveQuickWindowInvocations.FirstOrDefault(item => item.InvocationKey == invocationKey);
        if (invocation is null)
            return Block(snapshot, "remove quick-window invocation", "quick-window.invocation-missing", "The invocation does not exist.", invocationKey);
        if (invocation.OwnerPageKey is not { } pageKey ||
            string.IsNullOrWhiteSpace(invocation.OwnerElementId) ||
            string.IsNullOrWhiteSpace(invocation.OwnerCommandId) ||
            !snapshot.Scenes.TryGetValue(pageKey, out var scene))
            return Block(snapshot, "remove quick-window invocation", "quick-window.invocation-owner-missing", "The invocation owner cannot be resolved.", invocationKey);

        var element = scene.FindElementRecursive(invocation.OwnerElementId);
        if (element is null)
            return Block(snapshot, "remove quick-window invocation", "quick-window.owner-element-missing", "The invocation owner element does not exist.", invocationKey);
        var commands = element.EffectiveCommandConfig.Commands.Select(command =>
            string.Equals(command.Id, invocation.OwnerCommandId, StringComparison.Ordinal) && command.QuickWindowInvocationKey == invocationKey
                ? command with { QuickWindowInvocationKey = null }
                : command).ToArray();
        var updatedScene = scene.WithReplacedElementRecursive(element with { CommandConfig = new ScadaElementCommandConfig(commands) });
        var scenes = new Dictionary<Guid, ScadaScene>(snapshot.Scenes) { [pageKey] = updatedScene };
        var after = snapshot with
        {
            Version = snapshot.Version + 1,
            Project = snapshot.Project with
            {
                QuickWindowInvocations = snapshot.Project.EffectiveQuickWindowInvocations.Where(item => item.InvocationKey != invocationKey).ToArray()
            },
            Scenes = scenes
        };
        return new QuickWindowWorkspaceMutation(
            snapshot,
            after,
            CommandResult.Success("Quick-window invocation removed.", [pageKey], pageKey, workspaceDirty: true),
            "remove quick-window invocation",
            invocation.DefinitionKey,
            invocationKey);
    }

    /// <summary>Deletes a caller element and all invocation descriptors owned by its command subtree atomically.</summary>
    public QuickWindowWorkspaceMutation DeleteCaller(PageWorkspaceSnapshot snapshot, Guid pageKey, string elementId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        if (!snapshot.Scenes.TryGetValue(pageKey, out var scene))
            return Block(snapshot, "delete quick-window caller", "quick-window.owner-page-missing", "The caller page is not loaded.", pageKey);
        var element = scene.FindElementRecursive(elementId);
        if (element is null)
            return Block(snapshot, "delete quick-window caller", "quick-window.owner-element-missing", "The caller element does not exist.", pageKey);

        var invocationKeys = Flatten([element])
            .SelectMany(item => item.EffectiveCommandConfig.Commands)
            .Where(command => command.Kind == ScadaCommandKind.OpenQuickWindow && command.QuickWindowInvocationKey is not null)
            .Select(command => command.QuickWindowInvocationKey!.Value)
            .Concat(snapshot.Project.EffectiveQuickWindowInvocations
                .Where(invocation => invocation.OwnerPageKey == pageKey &&
                                     (string.Equals(invocation.OwnerElementId, elementId, StringComparison.Ordinal) ||
                                      element.ChildElements.Any(child => string.Equals(child.Id, invocation.OwnerElementId, StringComparison.Ordinal))))
                .Select(invocation => invocation.InvocationKey))
            .ToHashSet();
        var affectedDefinition = snapshot.Project.EffectiveQuickWindowInvocations
            .FirstOrDefault(invocation => invocationKeys.Contains(invocation.InvocationKey))?.DefinitionKey;
        var updatedScene = scene.WithoutSceneObjects([elementId]);
        var scenes = new Dictionary<Guid, ScadaScene>(snapshot.Scenes) { [pageKey] = updatedScene };
        var after = snapshot with
        {
            Version = snapshot.Version + 1,
            Project = snapshot.Project with
            {
                QuickWindowInvocations = snapshot.Project.EffectiveQuickWindowInvocations
                    .Where(invocation => !invocationKeys.Contains(invocation.InvocationKey))
                    .ToArray()
            },
            Scenes = scenes
        };
        return new QuickWindowWorkspaceMutation(
            snapshot,
            after,
            CommandResult.Success("Caller and quick-window invocation deleted.", [pageKey], pageKey, workspaceDirty: true),
            "delete quick-window caller",
            affectedDefinition,
            invocationKeys.Count == 1 ? invocationKeys.Single() : null);
    }

    /// <summary>Returns stable authoring diagnostics after an invocation mutation.</summary>
    public QuickWindowDependencyAnalysis Analyze(PageWorkspaceSnapshot snapshot) => dependencyAnalyzer.Analyze(snapshot);

    private static bool IsSameOwner(QuickWindowInvocation invocation, UpsertQuickWindowInvocationRequest request) =>
        invocation.OwnerPageKey == request.OwnerPageKey &&
        string.Equals(invocation.OwnerElementId, request.OwnerElementId, StringComparison.Ordinal) &&
        string.Equals(invocation.OwnerCommandId, request.OwnerCommandId, StringComparison.Ordinal);

    private static QuickWindowWorkspaceMutation Block(
        PageWorkspaceSnapshot snapshot,
        string label,
        string code,
        string message,
        Guid? targetKey)
    {
        var issue = new ScadaBuildValidationIssue(
            ScadaBuildValidationSeverity.Error,
            code,
            message,
            PropertyPath: "Project.QuickWindowInvocations",
            TargetKey: targetKey,
            SuggestedFix: "Reload the workspace and select a valid definition, element and command.");
        return new QuickWindowWorkspaceMutation(snapshot, snapshot, CommandResult.Blocked(message, [issue]), label);
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
}
