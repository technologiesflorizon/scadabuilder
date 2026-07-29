namespace ScadaBuilderV2.Application.Projects;

/// <summary>Creates and validates editable V2 project workspaces without UI dependencies.</summary>
/// <remarks>
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-07-29-project-lifecycle-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ProjectCreationIntegrationTests.cs, tests/ScadaBuilderV2.Tests/ProjectOpenIntegrationTests.cs.
/// </remarks>
public interface IProjectWorkspaceRepository
{
    /// <summary>Creates a complete project atomically and returns its prepared snapshot.</summary>
    Task<ProjectRepositoryResult> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Validates and prepares an existing project without persisting model migration.</summary>
    Task<ProjectRepositoryResult> OpenAsync(
        string projectFilePath,
        CancellationToken cancellationToken = default);
}
