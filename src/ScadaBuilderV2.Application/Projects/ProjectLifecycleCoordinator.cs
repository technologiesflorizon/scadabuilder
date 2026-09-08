namespace ScadaBuilderV2.Application.Projects;

using ScadaBuilderV2.Domain.Projects;

/// <summary>Visual/session boundary required by the project lifecycle coordinator.</summary>
public interface IProjectLifecycleHost
{
    bool HasActiveProject { get; }
    bool HasUnsavedChanges { get; }
    Task<ProjectCloseDecision> RequestCloseDecisionAsync(CancellationToken cancellationToken);

    /// <summary>Persists the active project, returning false when the snapshot was not written.</summary>
    /// <remarks>
    /// The result is load-bearing. When the operator answers `Enregistrer`, D9 says the snapshot is
    /// persisted and D8 says any error keeps the previous session; a save that reports failure must
    /// therefore abort the transition rather than let it destroy the work it was asked to keep.
    /// </remarks>
    Task<bool> SaveActiveProjectAsync(CancellationToken cancellationToken);

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
            // A request that cannot succeed must not cost the operator a decision about the project they
            // already have open. The same validation backs the creation dialog, so this only fires when the
            // target became unavailable between the dialog closing and the transition starting.
            var invalid = repository.ValidateCreation(request);
            if (invalid.Any(issue => issue.Severity == ScadaBuildValidationSeverity.Error))
            {
                return new ProjectRepositoryResult(null, invalid);
            }

            var blocked = await BlockingReplacementOutcomeAsync(cancellationToken);
            if (blocked is not null)
            {
                return blocked;
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

            var blocked = await BlockingReplacementOutcomeAsync(cancellationToken);
            if (blocked is not null)
            {
                return blocked;
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
            return await host.SaveActiveProjectAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Closes the active project and returns to the welcome state without closing the window.</summary>
    /// <remarks>
    /// The diagnostics distinguish the three ways a close can stop: another transition is running, the
    /// operator cancelled, or the save they asked for failed. A caller that only sees a boolean cannot tell
    /// the last one from the second, and the last one is the only one worth interrupting them for.
    /// </remarks>
    public async Task<ProjectRepositoryResult> CloseAsync(CancellationToken cancellationToken = default)
    {
        if (!host.HasActiveProject)
        {
            return Completed();
        }
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            return Busy();
        }

        try
        {
            var blocked = await BlockingReplacementOutcomeAsync(cancellationToken);
            if (blocked is not null)
            {
                return blocked;
            }
            await host.CloseActiveProjectAsync(cancellationToken);
            return Completed();
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Resolves the unsaved-changes gate; a non-null result means the transition must stop.</summary>
    /// <remarks>
    /// A refused save is as blocking as `Annuler`, but it is not the same event and must not be reported as
    /// one: the operator answered `Enregistrer` precisely to keep the work, so a failed write is an error
    /// they have to see, while a cancellation is a choice they already know they made.
    /// </remarks>
    private async Task<ProjectRepositoryResult?> BlockingReplacementOutcomeAsync(CancellationToken cancellationToken)
    {
        if (!host.HasActiveProject || !host.HasUnsavedChanges)
        {
            return null;
        }

        var decision = await host.RequestCloseDecisionAsync(cancellationToken);
        if (decision == ProjectCloseDecision.Cancel)
        {
            return Cancelled();
        }
        if (decision == ProjectCloseDecision.Save && !await host.SaveActiveProjectAsync(cancellationToken))
        {
            return SaveRefused();
        }
        return null;
    }

    private static ProjectRepositoryResult Completed() => new(null, []);

    private static ProjectRepositoryResult Busy() =>
        new(null, [new(
            ScadaBuildValidationSeverity.Error,
            "project.transition-busy",
            "Une autre transition de projet est déjà en cours.")]);

    private static ProjectRepositoryResult SaveRefused() =>
        new(null, [new(
            ScadaBuildValidationSeverity.Error,
            "project.save-refused",
            "Le projet n'a pas pu être enregistré; la transition est abandonnée et la session reste ouverte.")]);

    private static ProjectRepositoryResult Cancelled() =>
        new(null, [new(
            ScadaBuildValidationSeverity.Warning,
            "project.transition-cancelled",
            "La transition de projet a été annulée.")]);
}
