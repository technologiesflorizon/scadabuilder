namespace ScadaBuilderV2.Application.ElementStudio;

public interface IElementStudioComponentPackageReader
{
    Task<ElementStudioComponentPackage> ReadFromPathAsync(
        string packagePath,
        CancellationToken cancellationToken = default);
}
