using System.Text.Json;
using System.Text.Json.Serialization;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ModernProjects;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ModernProjectAtomicSnapshotTests
{
    [TestMethod]
    public async Task SaveWorkspaceSnapshotPersistsCoherentProjectAndScenes()
    {
        var root = CreateRoot();
        var pageKey = Guid.NewGuid();
        var snapshot = CreateSnapshot(1, CreateScene(pageKey, "page_one", "Initial"));

        try
        {
            var store = new ModernProjectStore();
            await store.SaveWorkspaceSnapshotAsync(root, snapshot);

            var projectRoot = ModernProjectStore.GetReferenceModernProjectRoot(root);
            var project = await ReadJsonAsync<ScadaProject>(Path.Combine(projectRoot, "project.json"));
            var scene = await ReadJsonAsync<ScadaScene>(Path.Combine(projectRoot, snapshot.Project.Scenes[0].RelativePath));
            Assert.AreEqual(pageKey, project.Scenes.Single(page => page.EffectivePageCode == "page_one").PageKey);
            Assert.AreEqual(pageKey, scene.PageKey);
            Assert.AreEqual("Initial", scene.Title);
            AssertNoPendingTransactions(projectRoot);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task SaveWorkspaceSnapshotDeletesRemovedSceneOnlyAfterProjectCommit()
    {
        var root = CreateRoot();
        var firstKey = Guid.NewGuid();
        var secondKey = Guid.NewGuid();
        var first = CreateScene(firstKey, "page_one", "One");
        var second = CreateScene(secondKey, "page_two", "Two");
        var initial = CreateSnapshot(1, first, second);

        try
        {
            var store = new ModernProjectStore();
            await store.SaveWorkspaceSnapshotAsync(root, initial);
            var removedReference = initial.Project.Scenes.Single(page => page.PageKey == secondKey);
            var updated = CreateSnapshot(2, first) with
            {
                PendingDeletions = [new PendingPageDeletion(secondKey, removedReference.RelativePath)]
            };

            await store.SaveWorkspaceSnapshotAsync(root, updated);

            var projectRoot = ModernProjectStore.GetReferenceModernProjectRoot(root);
            var project = await ReadJsonAsync<ScadaProject>(Path.Combine(projectRoot, "project.json"));
            Assert.AreEqual(1, project.Scenes.Count);
            Assert.IsFalse(File.Exists(Path.Combine(projectRoot, removedReference.RelativePath)));
            AssertNoPendingTransactions(projectRoot);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task SaveWorkspaceSnapshotRollsBackScenesWhenProjectCommitFails()
    {
        var root = CreateRoot();
        var pageKey = Guid.NewGuid();
        var initial = CreateSnapshot(1, CreateScene(pageKey, "page_one", "Before"));

        try
        {
            var store = new ModernProjectStore();
            await store.SaveWorkspaceSnapshotAsync(root, initial);
            var projectRoot = ModernProjectStore.GetReferenceModernProjectRoot(root);
            var projectPath = Path.Combine(projectRoot, "project.json");
            var scenePath = Path.Combine(projectRoot, initial.Project.Scenes[0].RelativePath);
            var updated = CreateSnapshot(2, CreateScene(pageKey, "page_one", "After"));

            await using (File.Open(projectPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                await Assert.ThrowsExceptionAsync<IOException>(() => store.SaveWorkspaceSnapshotAsync(root, updated));
            }

            var scene = await ReadJsonAsync<ScadaScene>(scenePath);
            Assert.AreEqual("Before", scene.Title);
            AssertNoPendingTransactions(projectRoot);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task LoadProjectRollsBackInterruptedPreCommitTransaction()
    {
        var root = CreateRoot();
        var pageKey = Guid.NewGuid();
        var initial = CreateSnapshot(1, CreateScene(pageKey, "page_one", "Before"));

        try
        {
            var store = new ModernProjectStore();
            await store.SaveWorkspaceSnapshotAsync(root, initial);
            var projectRoot = ModernProjectStore.GetReferenceModernProjectRoot(root);
            var relativePath = initial.Project.Scenes[0].RelativePath.Replace('\\', '/');
            var scenePath = Path.Combine(projectRoot, relativePath);
            var transactionRoot = Path.Combine(projectRoot, ".studio", "transactions", "interrupted");
            var backupPath = Path.Combine(transactionRoot, "backup", relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
            File.Copy(scenePath, backupPath);
            await WriteJsonAsync(scenePath, CreateScene(pageKey, "page_one", "Interrupted"));
            await WriteJournalAsync(
                transactionRoot,
                "WritingScenes",
                [new JournalWrite(relativePath, $"new/{relativePath}", $"backup/{relativePath}", true)],
                []);

            _ = await store.LoadProjectAsync(root);

            var recovered = await ReadJsonAsync<ScadaScene>(scenePath);
            Assert.AreEqual("Before", recovered.Title);
            Assert.IsFalse(Directory.Exists(transactionRoot));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task LoadProjectFinalizesCommittedPendingDeletion()
    {
        var root = CreateRoot();
        var firstKey = Guid.NewGuid();
        var secondKey = Guid.NewGuid();
        var initial = CreateSnapshot(
            1,
            CreateScene(firstKey, "page_one", "One"),
            CreateScene(secondKey, "page_two", "Two"));

        try
        {
            var store = new ModernProjectStore();
            await store.SaveWorkspaceSnapshotAsync(root, initial);
            var projectRoot = ModernProjectStore.GetReferenceModernProjectRoot(root);
            var deletedRelativePath = initial.Project.Scenes.Single(page => page.PageKey == secondKey).RelativePath.Replace('\\', '/');
            var transactionRoot = Path.Combine(projectRoot, ".studio", "transactions", "committed");
            await WriteJournalAsync(transactionRoot, "ProjectCommitted", [], [deletedRelativePath]);

            _ = await store.LoadProjectAsync(root);

            Assert.IsFalse(File.Exists(Path.Combine(projectRoot, deletedRelativePath)));
            Assert.IsFalse(Directory.Exists(transactionRoot));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task SnapshotValidationNeverDeletesOutsideProjectScenes()
    {
        var root = CreateRoot();
        var outsidePath = Path.Combine(root, "wonderware-source.html");
        await File.WriteAllTextAsync(outsidePath, "source");
        var pageKey = Guid.NewGuid();
        var snapshot = CreateSnapshot(1, CreateScene(pageKey, "page_one", "One")) with
        {
            PendingDeletions = [new PendingPageDeletion(Guid.NewGuid(), "../../wonderware-source.html")]
        };

        try
        {
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                new ModernProjectStore().SaveWorkspaceSnapshotAsync(root, snapshot));
            Assert.IsTrue(File.Exists(outsidePath));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static PageWorkspaceSnapshot CreateSnapshot(long version, params ScadaScene[] scenes)
    {
        var references = scenes.Select(scene => new ScadaSceneReference(
            scene.EffectivePageCode,
            scene.Title,
            $"scenes/{scene.PageKey:N}.scene.json",
            PageKey: scene.PageKey,
            PageCode: scene.EffectivePageCode)).ToArray();
        var project = ScadaProject.CreateDefault("Atomic") with { Scenes = references };
        return new PageWorkspaceSnapshot(
            version,
            project,
            scenes.ToDictionary(scene => scene.PageKey),
            []);
    }

    private static ScadaScene CreateScene(Guid pageKey, string pageCode, string title)
    {
        return ScadaScene.CreateEmpty(pageCode, title, CanvasSize.DefaultDesktop) with
        {
            PageKey = pageKey,
            PageCode = pageCode,
            Origin = PageOrigin.Native
        };
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<T> ReadJsonAsync<T>(string path)
    {
        await using var stream = File.OpenRead(path);
        return (await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions))!;
    }

    private static async Task WriteJsonAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions);
    }

    private static async Task WriteJournalAsync(
        string transactionRoot,
        string phase,
        IReadOnlyList<JournalWrite> writes,
        IReadOnlyList<string> deletions)
    {
        Directory.CreateDirectory(transactionRoot);
        await WriteJsonAsync(
            Path.Combine(transactionRoot, "journal.json"),
            new
            {
                TransactionId = Path.GetFileName(transactionRoot),
                SnapshotVersion = 1,
                Phase = phase,
                Writes = writes,
                Deletions = deletions
            });
    }

    private static void AssertNoPendingTransactions(string projectRoot)
    {
        var transactionsRoot = Path.Combine(projectRoot, ".studio", "transactions");
        Assert.IsFalse(Directory.Exists(transactionsRoot) && Directory.EnumerateDirectories(transactionsRoot).Any());
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed record JournalWrite(
        string TargetRelativePath,
        string StagedRelativePath,
        string BackupRelativePath,
        bool TargetExisted);
}
