namespace ScadaBuilderV2.Application.Projects;

using ScadaBuilderV2.Domain.Projects;

/// <summary>Visual/session boundary required by the project lifecycle coordinator.</summary>
public interface IProjectLifecycleHost
{
    bool HasActiveProject { get; }
    bool HasUnsavedChanges { get; }
    Task<ProjectCloseDecision> RequestCloseDecisionAsync(CancellationToken cancellationToken);
    Task SaveActiveProjectAsync(CancellationToken cancellationToken);
    Task ActivateProjectAsync(ProjectLoadCandidate candidate, CancellationToken cancellationToken);
    Task CloseActiveProjectAsync(CancellationToken cancellationToken);
}

/// <summary>Coordinates non-reentrant create, open, save and close transitions for one application window.</summary>
/// <remarks>
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-07-29-project-lifecycle-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ProjectLifecycleCoordinatorTests.cs.
/// </remarks>
public sealed class ProjectLifecycleCoordinator(
    IProjectWorkspaceRepository repository,
    IRecentProjectStore recentProjects,
    IProjectLifecycleHost host)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<ProjectRepositoryResult> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            return Busy();
        }

        try
        {
            if (!await CanReplaceActiveAsync(cancellationToken))
            {
                return Cancelled();
            }

            var result = await repository.CreateAsync(request, cancellationToken);
            if (!result.IsSuccess)
            {
                return result;
            }

            await host.ActivateProjectAsync(result.Candidate!, cancellationToken);
            await recentProjects.RecordAsync(result.Candidate!.Location, result.Candidate.Snapshot.Project.Name, cancellationToken);
            await recentProjects.WriteCreationParentAsync(request.ParentDirectory, cancellationToken);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ProjectRepositoryResult> OpenAsync(
        string projectFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            return Busy();
        }

        try
        {
            var result = await repository.OpenAsync(projectFilePath, cancellationToken);
            if (!result.IsSuccess)
            {
                return result;
            }

            if (!await CanReplaceActiveAsync(cancellationToken))
            {
                return Cancelled();
            }

            await host.ActivateProjectAsync(result.Candidate!, cancellationToken);
            await recentProjects.RecordAsync(result.Candidate!.Location, result.Candidate.Snapshot.Project.Name, cancellationToken);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!host.HasActiveProject || !await gate.WaitAsync(0, cancellationToken))
        {
            return false;
        }

        try
        {
            await host.SaveActiveProjectAsync(cancellationToken);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> CloseAsync(CancellationToken cancellationToken = default)
    {
        if (!host.HasActiveProject)
        {
            return true;
        }
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            return false;
        }

        try
        {
            if (!await CanReplaceActiveAsync(cancellationToken))
            {
                return false;
            }
            await host.CloseActiveProjectAsync(cancellationToken);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<bool> CanReplaceActiveAsync(CancellationToken cancellationToken)
    {
        if (!host.HasActiveProject || !host.HasUnsavedChanges)
        {
            return true;
        }

        var decision = await host.RequestCloseDecisionAsync(cancellationToken);
        if (decision == ProjectCloseDecision.Cancel)
        {
            return false;
        }
        if (decision == ProjectCloseDecision.Save)
        {
            await host.SaveActiveProjectAsync(cancellationToken);
        }
        return true;
    }

    private static ProjectRepositoryResult Busy() =>
        new(null, [new(
            ScadaBuildValidationSeverity.Error,
            "project.transition-busy",
            "Une autre transition de projet est déjà en cours.")]);

    private static ProjectRepositoryResult Cancelled() =>
        new(null, [new(
            ScadaBuildValidationSeverity.Warning,
            "project.transition-cancelled",
            "La transition de projet a été annulée.")]);
}
