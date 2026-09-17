namespace ScadaBuilderV2.Application.Projects;

using ScadaBuilderV2.Domain.Projects;

/// <summary>Creates and validates editable V2 project workspaces without UI dependencies.</summary>
/// <remarks>
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-07-29-project-lifecycle-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ProjectLifecycleInfrastructureTests.cs,
/// tests/ScadaBuilderV2.Tests/ProjectLifecycleCoordinatorTests.cs.
/// </remarks>
public interface IProjectWorkspaceRepository
{
    /// <summary>Validates a creation request without touching the file system.</summary>
    /// <remarks>
    /// D2 makes this validation shared between the creation dialog and Application. The coordinator runs it
    /// before the unsaved-changes gate so a request that cannot succeed never costs the operator a decision
    /// about the project they already have open.
    /// </remarks>
    IReadOnlyList<ScadaBuildValidationIssue> ValidateCreation(CreateProjectRequest request);

    /// <summary>Creates a complete project atomically and returns its prepared snapshot.</summary>
    Task<ProjectRepositoryResult> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Validates and prepares an existing project without persisting model migration.</summary>
    Task<ProjectRepositoryResult> OpenAsync(
        string projectFilePath,
        CancellationToken cancellationToken = default);
}
