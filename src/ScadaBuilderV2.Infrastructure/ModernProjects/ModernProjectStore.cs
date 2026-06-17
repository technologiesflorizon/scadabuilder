using System.Text.Json;
using System.Text.Json.Serialization;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Domain.Versioning;

namespace ScadaBuilderV2.Infrastructure.ModernProjects;

public sealed class ModernProjectStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<ScadaProject> EnsureReferenceModernProjectAsync(string repositoryRoot, IReadOnlyList<ScadaSceneReference> scenes)
    {
        var projectRoot = GetReferenceModernProjectRoot(repositoryRoot);
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(Path.Combine(projectRoot, "scenes"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "assets"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "library", "elements"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "libraries"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "imports", "legacy"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "imports", "tags"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "exports"));

        var projectPath = Path.Combine(projectRoot, "project.json");
        var project = new ScadaProject(
            "AMR_REF_SCADA_V2",
            ScadaVersion.Initial,
            CanvasSize.DefaultDesktop,
            ResponsiveMode.Fixed,
            AuthoringMode.DesktopFirst,
            DefaultDevicePresets.All,
            scenes);

        if (File.Exists(projectPath))
        {
            var existing = await LoadProjectFileAsync(projectPath);
            if (existing is not null)
            {
                project = existing with
                {
                    ManifestVersion = string.IsNullOrWhiteSpace(existing.ManifestVersion) ? "2.0" : existing.ManifestVersion,
                    Scenes = MergeSceneReferences(existing.Scenes, scenes)
                };
            }
        }

        if (!File.Exists(projectPath) || !ScenesEquivalent(project.Scenes, scenes))
        {
            await SaveJsonAsync(projectPath, project);
        }

        return project;
    }

    public async Task<ScadaScene> LoadOrCreateSceneAsync(string repositoryRoot, string sceneId, string title, CanvasSize canvasSize)
    {
        var path = GetScenePath(repositoryRoot, sceneId);
        if (File.Exists(path))
        {
            await using var read = File.OpenRead(path);
            var scene = await JsonSerializer.DeserializeAsync<ScadaScene>(read, JsonOptions);
            if (scene is not null)
            {
                return scene.WithoutConvertedLegacyTextOverrides();
            }
        }

        return ScadaScene.CreateEmpty(sceneId, title, canvasSize);
    }

    public async Task SaveSceneAsync(string repositoryRoot, ScadaScene scene)
    {
        var path = GetScenePath(repositoryRoot, scene.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await SaveJsonAsync(path, scene);
        await UpsertSceneReferenceAsync(repositoryRoot, scene);
    }

    public async Task<ScadaProject?> LoadProjectAsync(string repositoryRoot)
    {
        var projectPath = Path.Combine(GetReferenceModernProjectRoot(repositoryRoot), "project.json");
        return File.Exists(projectPath)
            ? await LoadProjectFileAsync(projectPath)
            : null;
    }

    public async Task SaveProjectAsync(string repositoryRoot, ScadaProject project)
    {
        var projectPath = Path.Combine(GetReferenceModernProjectRoot(repositoryRoot), "project.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        await SaveJsonAsync(projectPath, project);
    }

    public static string GetReferenceModernProjectRoot(string repositoryRoot)
    {
        return Path.Combine(repositoryRoot, "SCADA_BUILDER_V2", "projects", "AMR_REF_SCADA_V2");
    }

    /// <summary>
    /// Gets the project-local directory where imported tag export snapshots are stored.
    /// </summary>
    public static string GetTagImportDirectory(string repositoryRoot)
    {
        return Path.Combine(GetReferenceModernProjectRoot(repositoryRoot), "imports", "tags");
    }

    private static string GetScenePath(string repositoryRoot, string sceneId)
    {
        return Path.Combine(GetReferenceModernProjectRoot(repositoryRoot), "scenes", $"{sceneId}.scene.json");
    }

    private static async Task SaveJsonAsync<T>(string path, T value)
    {
        await using var write = File.Create(path);
        await JsonSerializer.SerializeAsync(write, value, JsonOptions);
    }

    private static async Task<ScadaProject?> LoadProjectFileAsync(string projectPath)
    {
        await using var read = File.OpenRead(projectPath);
        return await JsonSerializer.DeserializeAsync<ScadaProject>(read, JsonOptions);
    }

    private static async Task UpsertSceneReferenceAsync(string repositoryRoot, ScadaScene scene)
    {
        var projectRoot = GetReferenceModernProjectRoot(repositoryRoot);
        var projectPath = Path.Combine(projectRoot, "project.json");
        if (!File.Exists(projectPath))
        {
            return;
        }

        var project = await LoadProjectFileAsync(projectPath);
        if (project is null)
        {
            return;
        }

        var reference = new ScadaSceneReference(
            scene.Id,
            scene.Title,
            $"scenes/{scene.Id}.scene.json",
            scene.PageType,
            scene.CanvasSize,
            scene.EffectiveBackground,
            scene.IncludeInBuild,
            scene.HeaderPageId,
            scene.FooterPageId);

        var scenes = project.Scenes
            .Where(existing => !string.Equals(existing.Id, scene.Id, StringComparison.Ordinal))
            .Append(reference)
            .OrderBy(existing => existing.Id, StringComparer.Ordinal)
            .ToArray();

        var homePageId = project.HomePageId;
        if (!string.IsNullOrWhiteSpace(homePageId) &&
            string.Equals(homePageId, scene.Id, StringComparison.Ordinal) &&
            (scene.PageType != ScadaPageType.Default || !scene.IncludeInBuild))
        {
            homePageId = null;
        }

        await SaveJsonAsync(projectPath, project with { Scenes = scenes, ManifestVersion = "2.0", HomePageId = homePageId });
    }

    private static IReadOnlyList<ScadaSceneReference> MergeSceneReferences(
        IReadOnlyList<ScadaSceneReference> existing,
        IReadOnlyList<ScadaSceneReference> incoming)
    {
        var incomingById = incoming.ToDictionary(scene => scene.Id, StringComparer.Ordinal);
        var merged = existing
            .Select(scene => incomingById.TryGetValue(scene.Id, out var incomingScene)
                ? incomingScene with
                {
                    Type = scene.Type,
                    CanvasSize = scene.CanvasSize,
                    Background = scene.Background,
                    IncludeInBuild = scene.IncludeInBuild,
                    HeaderPageId = scene.HeaderPageId,
                    FooterPageId = scene.FooterPageId
                }
                : scene)
            .ToList();

        var existingIds = existing.Select(scene => scene.Id).ToHashSet(StringComparer.Ordinal);
        merged.AddRange(incoming.Where(scene => !existingIds.Contains(scene.Id)));
        return merged;
    }

    private static bool ScenesEquivalent(IReadOnlyList<ScadaSceneReference> current, IReadOnlyList<ScadaSceneReference> incoming)
    {
        var currentIds = current.Select(scene => scene.Id).ToHashSet(StringComparer.Ordinal);
        var incomingIds = incoming.Select(scene => scene.Id).ToHashSet(StringComparer.Ordinal);
        return currentIds.SetEquals(incomingIds);
    }
}
