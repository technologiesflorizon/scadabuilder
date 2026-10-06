using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.QuickWindows;

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
    /// Applies <paramref name="migratedCommand"/> onto the caller command and retires
    /// <paramref name="retiredInvocationKey"/> from the project, as one atomic transition: the operator
    /// migrated an `OpenQuickWindow` command from a legacy `QuickWindowInvocationKey` to a
    /// `ParameterSheetKey`/`QuickWindowPageKey` pair, and the old invocation must not survive as an orphan
    /// (controller fix round 1 on Task 10 - see <see cref="QuickWindowInvocationMigration"/>).
    /// </summary>
    public QuickWindowWorkspaceMutation RetireMigratedInvocation(
        PageWorkspaceSnapshot snapshot,
        Guid ownerPageKey,
        string ownerElementId,
        string ownerCommandId,
        ScadaCommandBinding migratedCommand,
        Guid retiredInvocationKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(migratedCommand);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerElementId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerCommandId);

        if (!snapshot.Scenes.TryGetValue(ownerPageKey, out var scene))
            return Block(snapshot, "retire migrated quick-window invocation", "quick-window.owner-page-missing", "The caller page is not loaded.", ownerPageKey);
        var element = scene.FindElementRecursive(ownerElementId);
        if (element is null)
            return Block(snapshot, "retire migrated quick-window invocation", "quick-window.owner-element-missing", "The caller element does not exist.", ownerPageKey);
        var command = element.EffectiveCommandConfig.Commands.FirstOrDefault(item => string.Equals(item.Id, ownerCommandId, StringComparison.Ordinal));
        if (command is null)
            return Block(snapshot, "retire migrated quick-window invocation", "quick-window.owner-command-invalid", "The caller command does not exist.", ownerPageKey);

        var updatedCommands = element.EffectiveCommandConfig.Commands
            .Select(item => string.Equals(item.Id, ownerCommandId, StringComparison.Ordinal) ? migratedCommand : item)
            .ToArray();
        var updatedScene = scene.WithReplacedElementRecursive(element with
        {
            CommandConfig = new ScadaElementCommandConfig(updatedCommands)
        });
        var scenes = new Dictionary<Guid, ScadaScene>(snapshot.Scenes) { [ownerPageKey] = updatedScene };
        var after = snapshot with
        {
            Version = snapshot.Version + 1,
            Project = QuickWindowInvocationMigration.WithoutInvocation(snapshot.Project, retiredInvocationKey),
            Scenes = scenes
        };
        return new QuickWindowWorkspaceMutation(
            snapshot,
            after,
            CommandResult.Success("Quick-window command migrated; the legacy invocation was retired.", [ownerPageKey], ownerPageKey, workspaceDirty: true),
            "retire migrated quick-window invocation",
            AffectedInvocationKey: retiredInvocationKey);
    }

    /// <summary>Returns stable authoring diagnostics after an invocation mutation.</summary>
    public QuickWindowDependencyAnalysis Analyze(PageWorkspaceSnapshot snapshot) => dependencyAnalyzer.Analyze(snapshot);

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
}
