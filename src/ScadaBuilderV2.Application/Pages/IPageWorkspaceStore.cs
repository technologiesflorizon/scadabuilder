namespace ScadaBuilderV2.Application.Pages;

/// <summary>Persistence boundary for one coherent page workspace snapshot.</summary>
public interface IPageWorkspaceStore
{
    /// <summary>Atomically validates and saves the project, scenes and pending deletions.</summary>
    Task SaveWorkspaceSnapshotAsync(
        string repositoryRoot,
        PageWorkspaceSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
