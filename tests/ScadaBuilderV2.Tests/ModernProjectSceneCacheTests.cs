using System.Text.Json;
using System.Text.Json.Serialization;
using ScadaBuilderV2.Application.Formats;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ModernProjects;
using ScadaBuilderV2.Infrastructure.ModernProjects.Converters;

namespace ScadaBuilderV2.Tests;

/// <summary>
/// Locks the scene file cache of the workspace snapshot read path: an unchanged scene file is neither
/// re-read nor re-deserialized, a cached snapshot is deep-equal to an uncached one, and a modified, rewritten
/// or deleted scene file is never served from cache. Undo/redo restores whole snapshots, so a stale cached
/// scene would restore wrong content.
/// </summary>
/// <remarks>
/// Decisions: DEC-0049.
/// Contracts: docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class ModernProjectSceneCacheTests
{
    private static readonly JsonSerializerOptions CompareOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    [TestMethod]
    public async Task SecondCaptureOfAnUnchangedProjectReadsNoSceneFile()
    {
        using var fixture = await SyntheticProject.CreateAsync();
        var store = new ModernProjectStore();

        var first = await fixture.ReadAsync(store);
        var readsAfterFirst = store.SceneCache.FileReadCount;
        var deserializationsAfterFirst = store.SceneCache.DeserializationCount;
        var second = await fixture.ReadAsync(store);

        Assert.AreEqual(3, readsAfterFirst, "the first capture reads every scene file once");
        Assert.AreEqual(0, store.SceneCache.FileReadCount - readsAfterFirst, "an unchanged project must read zero scene files");
        Assert.AreEqual(0, store.SceneCache.DeserializationCount - deserializationsAfterFirst);
        Assert.AreEqual(Serialize(first), Serialize(second));
    }

    [TestMethod]
    public async Task CachedSnapshotOfTheReferenceProjectIsDeepEqualToAnUncachedOne()
    {
        using var fixture = ReferenceProjectCopy.Create();
        var cachedStore = new ModernProjectStore();
        var project = await cachedStore.LoadProjectFromRootAsync(fixture.Root);
        var context = new PageWorkspaceReadContext(new Dictionary<Guid, ScadaScene>(), [], project, []);

        _ = await cachedStore.ReadWorkspaceSnapshotFromProjectRootAsync(fixture.Root, context);
        var readsAfterWarmUp = cachedStore.SceneCache.FileReadCount;
        var cached = await cachedStore.ReadWorkspaceSnapshotFromProjectRootAsync(fixture.Root, context);
        var uncached = await new ModernProjectStore().ReadWorkspaceSnapshotFromProjectRootAsync(fixture.Root, context);

        Assert.AreEqual(project!.Scenes.Count, readsAfterWarmUp, "the warm-up reads every scene file once");
        Assert.AreEqual(readsAfterWarmUp, cachedStore.SceneCache.FileReadCount, "the cached capture must not touch any scene file");
        Assert.AreEqual(Serialize(uncached), Serialize(cached), "a cached snapshot must be deep-equal to an uncached one");
    }

    [TestMethod]
    public async Task SingleSceneLoadFromCacheIsDeepEqualToAnUncachedLoad()
    {
        using var fixture = await SyntheticProject.CreateAsync();
        var store = new ModernProjectStore();
        var page = fixture.Project.Scenes[1];

        _ = await store.LoadOrCreateSceneFromProjectRootAsync(fixture.Root, page);
        var reads = store.SceneCache.FileReadCount;
        var cached = await store.LoadOrCreateSceneFromProjectRootAsync(fixture.Root, page);
        var uncached = await new ModernProjectStore().LoadOrCreateSceneFromProjectRootAsync(fixture.Root, page);

        Assert.AreEqual(reads, store.SceneCache.FileReadCount);
        Assert.AreEqual(JsonSerializer.Serialize(uncached, CompareOptions), JsonSerializer.Serialize(cached, CompareOptions));
    }

    [TestMethod]
    public async Task SceneFileModifiedOnDiskIsReRead()
    {
        using var fixture = await SyntheticProject.CreateAsync();
        var store = new ModernProjectStore();
        var page = fixture.Project.Scenes[0];
        _ = await fixture.ReadAsync(store);

        var path = fixture.ScenePath(page);
        var json = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path, json.Replace("Texte p1", "Texte modifie p1", StringComparison.Ordinal));
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(5));
        var reads = store.SceneCache.FileReadCount;
        var after = await fixture.ReadAsync(store);

        Assert.AreEqual(1, store.SceneCache.FileReadCount - reads, "only the modified scene file is read again");
        Assert.AreEqual("Texte modifie p1", after.Scenes[page.PageKey].Elements.Single().DisplayName);
        Assert.AreEqual(
            Serialize(await fixture.ReadAsync(new ModernProjectStore())),
            Serialize(after));
    }

    [TestMethod]
    public async Task SceneFileRewrittenBySaveWithinTheSameTimestampIsNotServedStale()
    {
        using var fixture = await SyntheticProject.CreateAsync();
        var store = new ModernProjectStore();
        var page = fixture.Project.Scenes[0];
        var before = await fixture.ReadAsync(store);
        var path = fixture.ScenePath(page);
        var originalStamp = File.GetLastWriteTimeUtc(path);
        var originalLength = new FileInfo(path).Length;

        // Same length, different content, and the original timestamp forced back: only the explicit
        // invalidation performed by the save can tell the cache that this file changed.
        var edited = before.Scenes[page.PageKey] with
        {
            Elements = [before.Scenes[page.PageKey].Elements.Single() with { DisplayName = "Texte P1" }]
        };
        var scenes = before.Scenes.ToDictionary(item => item.Key, item => item.Value);
        scenes[page.PageKey] = edited;
        await store.SaveWorkspaceSnapshotToProjectRootAsync(fixture.Root, before with { Scenes = scenes });
        Assert.AreEqual(originalLength, new FileInfo(path).Length, "the arrangement needs a same-length rewrite");
        File.SetLastWriteTimeUtc(path, originalStamp);

        var after = await fixture.ReadAsync(store);

        Assert.AreEqual("Texte P1", after.Scenes[page.PageKey].Elements.Single().DisplayName);
        Assert.AreEqual(Serialize(await fixture.ReadAsync(new ModernProjectStore())), Serialize(after));
    }

    [TestMethod]
    public async Task UnchangedSceneFilesRewrittenBySaveAreReHashedButNotDeserialized()
    {
        using var fixture = await SyntheticProject.CreateAsync();
        var store = new ModernProjectStore();
        var snapshot = await fixture.ReadAsync(store);
        await store.SaveWorkspaceSnapshotToProjectRootAsync(fixture.Root, snapshot);
        var reads = store.SceneCache.FileReadCount;
        var deserializations = store.SceneCache.DeserializationCount;

        var after = await fixture.ReadAsync(store);

        Assert.AreEqual(3, store.SceneCache.FileReadCount - reads, "every rewritten file is re-read and re-hashed");
        Assert.AreEqual(0, store.SceneCache.DeserializationCount - deserializations, "byte-identical content keeps the cached scene");
        Assert.AreEqual(Serialize(await fixture.ReadAsync(new ModernProjectStore())), Serialize(after));
    }

    [TestMethod]
    public async Task DeletedSceneFileIsNotServedFromCache()
    {
        using var fixture = await SyntheticProject.CreateAsync();
        var store = new ModernProjectStore();
        var page = fixture.Project.Scenes[2];
        var before = await fixture.ReadAsync(store);
        Assert.AreEqual(1, before.Scenes[page.PageKey].Elements.Count);

        File.Delete(fixture.ScenePath(page));
        var after = await fixture.ReadAsync(store);
        var single = await store.LoadOrCreateSceneFromProjectRootAsync(fixture.Root, page);

        Assert.AreEqual(0, after.Scenes[page.PageKey].Elements.Count, "a deleted scene file falls back to an empty scene, never to the cached one");
        Assert.AreEqual(0, single.Elements.Count);
        Assert.AreEqual(2, store.SceneCache.Count, "the deleted file's entry is forgotten");
        Assert.AreEqual(Serialize(await fixture.ReadAsync(new ModernProjectStore())), Serialize(after));
    }

    [TestMethod]
    public async Task InvalidationRacingAReadPreventsTheStaleEntryFromBeingStored()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "race.scene.json");
            await File.WriteAllTextAsync(path, "AAAA");
            var originalStamp = File.GetLastWriteTimeUtc(path);
            var cache = new SceneFileCache();

            // Deterministic interleaving: while the reader holds the old bytes, a save replaces the file with
            // same-length content, the file system keeps the same timestamp (FAT/exFAT/network share), and the
            // writer invalidates the entry - all before the reader stores its result.
            var first = await cache.GetOrReadAsync(path, async (bytes, token) =>
            {
                await File.WriteAllTextAsync(path, "BBBB", token);
                File.SetLastWriteTimeUtc(path, originalStamp);
                cache.Invalidate(path);
                return TitledScene(bytes);
            });
            var second = await cache.GetOrReadAsync(path, (bytes, _) => Task.FromResult<ScadaScene?>(TitledScene(bytes)));

            Assert.AreEqual("AAAA", first!.Title, "the racing reader still returns what it read");
            Assert.AreEqual("BBBB", second!.Title, "the entry read before the invalidation must not have been stored");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ScopeChangeRacingAReadPreventsALateEntryForThePreviousProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var projectA = Path.Combine(root, "A");
        Directory.CreateDirectory(projectA);
        try
        {
            var path = Path.Combine(projectA, "late.scene.json");
            await File.WriteAllTextAsync(path, "AAAA");
            var cache = new SceneFileCache();

            _ = await cache.GetOrReadAsync(path, (bytes, _) =>
            {
                cache.RetainOnly(Path.Combine(root, "B"));
                return Task.FromResult<ScadaScene?>(TitledScene(bytes));
            });

            Assert.AreEqual(0, cache.Count, "a read of project A finishing after the switch to project B must not be cached");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task FailedProjectOpenDoesNotKeepTheFailedProjectScenesCached()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var projectRoot = Path.Combine(root, "Failing");
            var key = Guid.NewGuid();
            var page = new ScadaSceneReference(
                "imported",
                "Importee",
                $"scenes/{key:N}.scene.json",
                PageKey: key,
                PageCode: "imported",
                Origin: PageOrigin.Imported,
                ImportProvenance: new ImportProvenance("Wonderware"));
            var scene = (ScadaScene.CreateEmpty("imported", "Importee", new(1280, 873)) with { PageKey = key, PageCode = "imported" })
                .WithElement(ScadaElement.CreateText("text-1", "Texte", 10, 20));
            var project = ScadaProject.CreateDefault("FailingOpen") with
            {
                Scenes = [page],
                FormatVersion = ScadaFormatGeneration.Project
            };
            await new ModernProjectStore().SaveWorkspaceSnapshotToProjectRootAsync(
                projectRoot,
                new PageWorkspaceSnapshot(1, project, new Dictionary<Guid, ScadaScene> { [key] = scene }, []));
            var store = new ModernProjectStore();
            var registry = new ArtifactConverterRegistry();
            registry.Register(new ProjectGeneration1Converter());
            var repository = new ProjectWorkspaceRepository(
                store,
                new ReferenceProjectCompatibilityLocator(),
                registry,
                new ConversionCoordinator(registry, new RefusingConsent()));

            var result = await repository.OpenAsync(Path.Combine(projectRoot, "project.json"));

            Assert.IsFalse(result.IsSuccess, "the arrangement needs an open refused after its snapshot read (imported page without source)");
            Assert.AreEqual(0, store.SceneCache.Count, "a refused open must not leave the refused project's scenes cached");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static ScadaScene TitledScene(byte[] bytes) =>
        ScadaScene.CreateEmpty("race", System.Text.Encoding.UTF8.GetString(bytes), new(1280, 873));

    [TestMethod]
    public async Task OpenTabOverridesTakePrecedenceOverCachedScenes()
    {
        using var fixture = await SyntheticProject.CreateAsync();
        var store = new ModernProjectStore();
        var page = fixture.Project.Scenes[0];
        var warm = await fixture.ReadAsync(store);
        var open = warm.Scenes[page.PageKey] with
        {
            Elements = [warm.Scenes[page.PageKey].Elements.Single() with { DisplayName = "Edition en cours" }]
        };

        var snapshot = await store.ReadWorkspaceSnapshotFromProjectRootAsync(
            fixture.Root,
            new PageWorkspaceReadContext(new Dictionary<Guid, ScadaScene> { [page.PageKey] = open }, [page.PageKey], fixture.Project, []));

        Assert.AreEqual("Edition en cours", snapshot.Scenes[page.PageKey].Elements.Single().DisplayName);
    }

    [TestMethod]
    public async Task ScopingTheCacheToAnotherProjectForgetsThePreviousProjectScenes()
    {
        using var fixture = await SyntheticProject.CreateAsync();
        var store = new ModernProjectStore();
        _ = await fixture.ReadAsync(store);
        Assert.AreEqual(3, store.SceneCache.Count);

        store.ScopeSceneCacheTo(fixture.Root);
        Assert.AreEqual(3, store.SceneCache.Count, "scoping to the same project keeps its scenes");
        store.ScopeSceneCacheTo(Path.Combine(Path.GetTempPath(), "another-project"));
        Assert.AreEqual(0, store.SceneCache.Count);
        _ = await fixture.ReadAsync(store);
        store.ScopeSceneCacheTo(null);
        Assert.AreEqual(0, store.SceneCache.Count, "closing the project forgets every cached scene");
    }

    private static string Serialize(PageWorkspaceSnapshot snapshot) => JsonSerializer.Serialize(
        new
        {
            snapshot.Version,
            snapshot.Project,
            Scenes = snapshot.Scenes.OrderBy(item => item.Key).Select(item => new { item.Key, item.Value }).ToArray(),
            snapshot.PendingDeletions
        },
        CompareOptions);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ScadaBuilderV2.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        Assert.Fail("Unable to locate the repository root.");
        return string.Empty;
    }

    /// <summary>Never consents: these projects are already at the current generation, so nothing asks.</summary>
    private sealed class RefusingConsent : IConversionConsent
    {
        public Task<ConversionDecision> RequestAsync(ConversionPlan plan, CancellationToken cancellationToken) => Task.FromResult(ConversionDecision.Cancel);
    }

    /// <summary>A three-page project written through the store's own atomic save.</summary>
    private sealed class SyntheticProject : IDisposable
    {
        private SyntheticProject(string root, ScadaProject project)
        {
            Root = root;
            Project = project;
        }

        public string Root { get; }

        public ScadaProject Project { get; }

        public static async Task<SyntheticProject> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
            var references = new List<ScadaSceneReference>();
            var scenes = new Dictionary<Guid, ScadaScene>();
            for (var index = 1; index <= 3; index++)
            {
                var code = $"p{index}";
                var key = Guid.NewGuid();
                references.Add(new ScadaSceneReference(code, $"Page {index}", $"scenes/{key:N}.scene.json", PageKey: key, PageCode: code, Origin: PageOrigin.Native));
                scenes[key] = (ScadaScene.CreateEmpty(code, $"Page {index}", new(1280, 873)) with { PageKey = key, PageCode = code })
                    .WithElement(ScadaElement.CreateText($"text-{index}", $"Texte p{index}", 10 * index, 20));
            }

            var project = ScadaProject.CreateDefault("SceneCacheTests") with { Scenes = references };
            await new ModernProjectStore().SaveWorkspaceSnapshotToProjectRootAsync(root, new PageWorkspaceSnapshot(1, project, scenes, []));
            var saved = await new ModernProjectStore().LoadProjectFromRootAsync(root);
            return new SyntheticProject(root, saved!);
        }

        public string ScenePath(ScadaSceneReference page) =>
            Path.Combine(Root, page.RelativePath.Replace('/', Path.DirectorySeparatorChar));

        public Task<PageWorkspaceSnapshot> ReadAsync(ModernProjectStore store) =>
            store.ReadWorkspaceSnapshotFromProjectRootAsync(
                Root,
                new PageWorkspaceReadContext(new Dictionary<Guid, ScadaScene>(), [], Project, []));

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    /// <summary>A disposable copy of the tracked reference project's manifest and scenes (56 scenes, ~60 MB).</summary>
    private sealed class ReferenceProjectCopy : IDisposable
    {
        private ReferenceProjectCopy(string root) => Root = root;

        public string Root { get; }

        public static ReferenceProjectCopy Create()
        {
            var source = Path.Combine(FindRepositoryRoot(), "projects", "AMR_REF_SCADA_V2");
            var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "scenes"));
            File.Copy(Path.Combine(source, "project.json"), Path.Combine(root, "project.json"));
            foreach (var file in Directory.GetFiles(Path.Combine(source, "scenes")))
            {
                File.Copy(file, Path.Combine(root, "scenes", Path.GetFileName(file)));
            }

            return new ReferenceProjectCopy(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
