namespace ScadaBuilderV2.Application.Projects;

/// <summary>Persists the per-user list of recently opened project manifests.</summary>
public interface IRecentProjectStore
{
    bool IsInitialized { get; }
    Task<IReadOnlyList<RecentProjectEntry>> ReadAsync(CancellationToken cancellationToken = default);
    Task RecordAsync(ProjectWorkspaceLocation location, string displayName, CancellationToken cancellationToken = default);
    Task RemoveAsync(string projectFilePath, CancellationToken cancellationToken = default);
    string GetDefaultCreationParent();
    Task<string> ReadCreationParentAsync(CancellationToken cancellationToken = default);
    Task WriteCreationParentAsync(string path, CancellationToken cancellationToken = default);
}
