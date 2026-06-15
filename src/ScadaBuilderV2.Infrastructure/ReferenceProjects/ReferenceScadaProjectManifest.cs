namespace ScadaBuilderV2.Infrastructure.ReferenceProjects;

public sealed record ReferenceScadaProjectManifest(
    string Name,
    string? Version,
    string ProjectJsonPath,
    string ProjectDirectory,
    IReadOnlyList<ReferenceScadaPage> Pages);

public sealed record ReferenceScadaPage(
    string Id,
    string Title,
    string RelativePath,
    string AbsolutePath);
