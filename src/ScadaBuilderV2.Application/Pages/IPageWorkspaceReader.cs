namespace ScadaBuilderV2.Application.Pages;

/// <summary>Loads one coherent project snapshot while giving open or dirty editor scenes precedence over disk.</summary>
public interface IPageWorkspaceReader
{
    /// <summary>Reads every project page, using supplied open/dirty scenes before loading closed scenes from storage.</summary>
    Task<PageWorkspaceSnapshot> ReadWorkspaceSnapshotAsync(
        string repositoryRoot,
        PageWorkspaceReadContext? context = null,
        CancellationToken cancellationToken = default);
}
