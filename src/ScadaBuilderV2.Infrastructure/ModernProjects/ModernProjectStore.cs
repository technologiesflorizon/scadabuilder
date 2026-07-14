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
        var project = ModernProjectMigration.MigrateProject(new ScadaProject(
            "AMR_REF_SCADA_V2",
            ScadaVersion.Initial,
            CanvasSize.DefaultDesktop,
            ResponsiveMode.Fixed,
            AuthoringMode.DesktopFirst,
            DefaultDevicePresets.All,
            scenes), scenes);
        var originalJson = string.Empty;

        if (File.Exists(projectPath))
        {
            var existing = await LoadProjectFileAsync(projectPath);
            if (existing is not null)
            {
                originalJson = JsonSerializer.Serialize(existing, JsonOptions);
                var migratedExisting = ModernProjectMigration.MigrateProject(existing, scenes);
                project = ModernProjectMigration.MigrateProject(migratedExisting with
                {
                    ManifestVersion = string.IsNullOrWhiteSpace(existing.ManifestVersion) ? "2.0" : existing.ManifestVersion,
                    Scenes = MergeSceneReferences(migratedExisting.Scenes, project.Scenes)
                }, scenes);
            }
        }

        var migratedJson = JsonSerializer.Serialize(project, JsonOptions);
        if (!File.Exists(projectPath) || !string.Equals(originalJson, migratedJson, StringComparison.Ordinal))
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
                var normalized = scene.WithoutConvertedLegacyTextOverrides();
                var project = await LoadProjectAsync(repositoryRoot);
                return project is null ? normalized : ModernProjectMigration.MigrateScene(normalized, project);
            }
        }

        return ScadaScene.CreateEmpty(sceneId, title, canvasSize);
    }

    public async Task SaveSceneAsync(string repositoryRoot, ScadaScene scene)
    {
        var project = await LoadProjectAsync(repositoryRoot);
        var normalized = project is null ? scene : ModernProjectMigration.MigrateScene(scene, project);
        var path = GetScenePath(repositoryRoot, normalized.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await SaveJsonAsync(path, normalized);
        await UpsertSceneReferenceAsync(repositoryRoot, normalized);
    }

    public async Task<ScadaProject?> LoadProjectAsync(string repositoryRoot)
    {
        var projectPath = Path.Combine(GetReferenceModernProjectRoot(repositoryRoot), "project.json");
        var project = File.Exists(projectPath)
            ? await LoadProjectFileAsync(projectPath)
            : null;
        return project is null ? null : ModernProjectMigration.MigrateProject(project);
    }

    public async Task SaveProjectAsync(string repositoryRoot, ScadaProject project)
    {
        var projectPath = Path.Combine(GetReferenceModernProjectRoot(repositoryRoot), "project.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        await SaveJsonAsync(projectPath, ModernProjectMigration.MigrateProject(project));
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

        var existingReference = project.Scenes.FirstOrDefault(existing =>
            (scene.PageKey != Guid.Empty && existing.PageKey == scene.PageKey) ||
            string.Equals(existing.EffectivePageCode, scene.EffectivePageCode, StringComparison.OrdinalIgnoreCase));
        var reference = new ScadaSceneReference(
            scene.EffectivePageCode,
            scene.Title,
            existingReference?.RelativePath ?? $"scenes/{scene.Id}.scene.json",
            scene.PageType,
            scene.CanvasSize,
            scene.EffectiveBackground,
            scene.IncludeInBuild,
            scene.HeaderPageId,
            scene.FooterPageId,
            scene.PageKey,
            scene.EffectivePageCode,
            scene.EffectiveOrigin,
            scene.ImportProvenance,
            scene.HeaderPageKey,
            scene.FooterPageKey);

        var replaced = false;
        var scenes = project.Scenes.Select(existing =>
        {
            if ((scene.PageKey != Guid.Empty && existing.PageKey == scene.PageKey) ||
                string.Equals(existing.EffectivePageCode, scene.EffectivePageCode, StringComparison.OrdinalIgnoreCase))
            {
                replaced = true;
                return reference;
            }

            return existing;
        }).ToList();
        if (!replaced)
        {
            scenes.Add(reference);
        }

        var homePageId = project.HomePageId;
        if (!string.IsNullOrWhiteSpace(homePageId) &&
            string.Equals(homePageId, scene.Id, StringComparison.Ordinal) &&
            (scene.PageType != ScadaPageType.Default || !scene.IncludeInBuild))
        {
            homePageId = null;
        }

        await SaveJsonAsync(projectPath, ModernProjectMigration.MigrateProject(project with
        {
            Scenes = scenes,
            ManifestVersion = "2.0",
            HomePageId = homePageId
        }));
    }

    private static IReadOnlyList<ScadaSceneReference> MergeSceneReferences(
        IReadOnlyList<ScadaSceneReference> existing,
        IReadOnlyList<ScadaSceneReference> incoming)
    {
        var merged = existing.ToList();
        var existingCodes = existing.Select(scene => scene.EffectivePageCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        merged.AddRange(incoming.Where(scene => !existingCodes.Contains(scene.EffectivePageCode)));
        return merged;
    }
}
