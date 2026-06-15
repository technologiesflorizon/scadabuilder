namespace ScadaBuilderV2.Application.ElementStudio;

public interface IElementStudioComponentPackageWriter
{
    Task<string> WriteToLibraryAsync(
        ElementStudioComponentPackage package,
        string libraryRoot,
        CancellationToken cancellationToken = default);

    Task<string> WriteToPathAsync(
        ElementStudioComponentPackage package,
        string packagePath,
        CancellationToken cancellationToken = default);
}
