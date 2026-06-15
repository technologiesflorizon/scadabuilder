namespace ScadaBuilderV2.Application.ElementStudio;

public interface IElementStudioImportPackageWriter
{
    Task<string> WriteToProjectAsync(
        ElementStudioImportPackage package,
        string projectsRoot,
        CancellationToken cancellationToken = default);

    Task<string> WriteToPathAsync(
        ElementStudioImportPackage package,
        string packagePath,
        CancellationToken cancellationToken = default);
}
