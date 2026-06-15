using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScadaBuilderV2.Infrastructure.ReferenceProjects;

public sealed class ReferenceScadaProjectReader : IReferenceScadaProjectReader
{
    private const string AmrReferenceProjectRelativePath = "SCADA_BUILDER/AMR_SCADA/AMR_REF_SCADA/project.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async ValueTask<ReferenceScadaProjectManifest> LoadAsync(
        string projectJsonPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectJsonPath))
        {
            throw new ArgumentException("The project.json path is required.", nameof(projectJsonPath));
        }

        var fullProjectJsonPath = Path.GetFullPath(projectJsonPath);
        if (!File.Exists(fullProjectJsonPath))
        {
            throw new FileNotFoundException("The reference SCADA project.json file was not found.", fullProjectJsonPath);
        }

        await using var stream = File.OpenRead(fullProjectJsonPath);
        var projectJson = await JsonSerializer.DeserializeAsync<ProjectJsonManifest>(
            stream,
            SerializerOptions,
            cancellationToken);

        if (projectJson is null)
        {
            throw new InvalidDataException("The reference SCADA project.json file is empty or invalid.");
        }

        if (string.IsNullOrWhiteSpace(projectJson.Name))
        {
            throw new InvalidDataException("The reference SCADA project.json file must define a project name.");
        }

        if (projectJson.Pages is null)
        {
            throw new InvalidDataException("The reference SCADA project.json file must define a pages array.");
        }

        var projectDirectory = Path.GetDirectoryName(fullProjectJsonPath)
            ?? throw new InvalidDataException("The reference SCADA project directory could not be resolved.");

        var pages = projectJson.Pages
            .Select(pagePath => CreatePage(projectDirectory, pagePath))
            .ToArray();

        return new ReferenceScadaProjectManifest(
            projectJson.Name,
            projectJson.Version,
            fullProjectJsonPath,
            projectDirectory,
            pages);
    }

    public ValueTask<ReferenceScadaProjectManifest> LoadAmrReferenceAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new ArgumentException("The repository root path is required.", nameof(repositoryRoot));
        }

        var projectJsonPath = Path.Combine(
            Path.GetFullPath(repositoryRoot),
            AmrReferenceProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));

        return LoadAsync(projectJsonPath, cancellationToken);
    }

    public async ValueTask<IReadOnlyList<ReferenceScadaPage>> ListPagesAsync(
        string projectJsonPath,
        CancellationToken cancellationToken = default)
    {
        var project = await LoadAsync(projectJsonPath, cancellationToken);
        return project.Pages;
    }

    private static ReferenceScadaPage CreatePage(string projectDirectory, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidDataException("The reference SCADA project contains an empty page path.");
        }

        var normalizedRelativePath = relativePath.Replace('\\', '/');
        var pageFileName = normalizedRelativePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(pageFileName))
        {
            throw new InvalidDataException($"The reference SCADA page path '{relativePath}' is invalid.");
        }

        var id = Path.GetFileNameWithoutExtension(pageFileName);
        var absolutePath = Path.GetFullPath(Path.Combine(
            projectDirectory,
            normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        return new ReferenceScadaPage(id, id, normalizedRelativePath, absolutePath);
    }

    private sealed class ProjectJsonManifest
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("version")]
        public string? Version { get; init; }

        [JsonPropertyName("pages")]
        public string[]? Pages { get; init; }
    }
}
