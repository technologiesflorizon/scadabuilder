namespace ScadaBuilderV2.Infrastructure.ReferenceProjects;

public interface IReferenceScadaProjectReader
{
    ValueTask<ReferenceScadaProjectManifest> LoadAsync(
        string projectJsonPath,
        CancellationToken cancellationToken = default);

    ValueTask<ReferenceScadaProjectManifest> LoadAmrReferenceAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<ReferenceScadaPage>> ListPagesAsync(
        string projectJsonPath,
        CancellationToken cancellationToken = default);
}
