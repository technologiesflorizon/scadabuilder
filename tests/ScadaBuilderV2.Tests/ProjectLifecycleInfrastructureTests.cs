using System.Security.Cryptography;
using ScadaBuilderV2.Application.Projects;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Infrastructure.ModernProjects;
using ScadaBuilderV2.Infrastructure.Shell;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ProjectLifecycleInfrastructureTests
{
    [TestMethod]
    public async Task CreateAndOpenProject_UsesSelectedRootAndPreservesManifestOnOpen()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var repository = CreateRepository();
            var request = new CreateProjectRequest(
                "Projet test",
                root,
                "ProjetTest",
                "win00001",
                "Page principale",
                new CanvasSize(1280, 873),
                ResponsiveMode.Fixed,
                AuthoringMode.DesktopFirst);

            var created = await repository.CreateAsync(request);

            Assert.IsTrue(created.IsSuccess, string.Join(Environment.NewLine, created.Diagnostics.Select(issue => issue.Message)));
            var candidate = created.Candidate!;
            Assert.AreEqual(Path.Combine(root, "ProjetTest"), candidate.Location.ProjectRoot);
            Assert.IsTrue(File.Exists(candidate.Location.ProjectFilePath));
            Assert.IsTrue(Directory.Exists(Path.Combine(candidate.Location.ProjectRoot, "library", "elements")));
            Assert.IsTrue(Directory.Exists(Path.Combine(candidate.Location.ProjectRoot, "imports", "tags")));
            Assert.AreEqual("win00001", candidate.Snapshot.Project.EffectiveHomePageId);
            Assert.IsTrue(candidate.Snapshot.Project.Scenes.Single().IncludeInBuild);

            var before = SHA256.HashData(await File.ReadAllBytesAsync(candidate.Location.ProjectFilePath));
            var opened = await repository.OpenAsync(candidate.Location.ProjectFilePath);
            var after = SHA256.HashData(await File.ReadAllBytesAsync(candidate.Location.ProjectFilePath));

            Assert.IsTrue(opened.IsSuccess, string.Join(Environment.NewLine, opened.Diagnostics.Select(issue => issue.Message)));
            CollectionAssert.AreEqual(before, after, "Opening a project must not persist migration or rewrite project.json.");
            Assert.AreEqual(candidate.Location.ProjectRoot, opened.Candidate!.Location.ProjectRoot);
            Assert.AreEqual(1, opened.Candidate.Snapshot.Scenes.Count);
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    [TestMethod]
    public async Task CreateProject_WhenTargetExists_LeavesExistingDirectoryUntouched()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var target = Path.Combine(root, "Existing");
            Directory.CreateDirectory(target);
            var marker = Path.Combine(target, "keep.txt");
            await File.WriteAllTextAsync(marker, "keep");

            var result = await CreateRepository().CreateAsync(new CreateProjectRequest(
                "Existing",
                root,
                "Existing",
                "win00001",
                "Page principale",
                CanvasSize.DefaultDesktop,
                ResponsiveMode.Fixed,
                AuthoringMode.DesktopFirst));

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Diagnostics.Any(issue => issue.Code == "project.target-exists"));
            Assert.AreEqual("keep", await File.ReadAllTextAsync(marker));
            Assert.AreEqual(0, Directory.EnumerateDirectories(root, ".*.create-*").Count());
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    [TestMethod]
    public async Task OpenProject_InvalidManifestFailsClosed()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var projectRoot = Path.Combine(root, "Broken");
            Directory.CreateDirectory(projectRoot);
            var manifest = Path.Combine(projectRoot, "project.json");
            await File.WriteAllTextAsync(manifest, "{ broken");

            var result = await CreateRepository().OpenAsync(manifest);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Candidate);
            Assert.IsTrue(result.Diagnostics.Any(issue => issue.Code == "project.open-failed"));
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    [TestMethod]
    public async Task RecentProjectStore_DeduplicatesAndRemovalDoesNotDeleteProject()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var settingsRoot = Path.Combine(root, "settings");
            var projectRoot = Path.Combine(root, "Project");
            Directory.CreateDirectory(projectRoot);
            var manifest = Path.Combine(projectRoot, "project.json");
            await File.WriteAllTextAsync(manifest, "{}");
            var store = new RecentProjectStore(settingsRoot);
            var location = new ProjectWorkspaceLocation(projectRoot, manifest);

            Assert.IsFalse(store.IsInitialized);
            await store.RecordAsync(location, "Premier nom");
            Assert.IsTrue(store.IsInitialized);
            await store.RecordAsync(location, "Nom courant");
            var entries = await store.ReadAsync();

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("Nom courant", entries[0].DisplayName);
            Assert.IsTrue(entries[0].IsAvailable);

            await store.RemoveAsync(manifest);

            Assert.AreEqual(0, (await store.ReadAsync()).Count);
            Assert.IsTrue(File.Exists(manifest), "Removing a recent entry must never delete project data.");
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    [TestMethod]
    public void ExistingProjectDiscovery_FindsProjectManifestsUnderNearestProjectsRoot()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var appDirectory = Path.Combine(root, "src", "App", "bin");
            var firstProject = Path.Combine(root, "projects", "First");
            var secondProject = Path.Combine(root, "projects", "Second");
            Directory.CreateDirectory(appDirectory);
            Directory.CreateDirectory(firstProject);
            Directory.CreateDirectory(secondProject);
            File.WriteAllText(Path.Combine(firstProject, "project.json"), "{}");
            File.WriteAllText(Path.Combine(secondProject, "project.json"), "{}");
            Directory.CreateDirectory(Path.Combine(root, "projects", "Incomplete"));

            var manifests = new ExistingProjectDiscovery().Discover(appDirectory);

            CollectionAssert.AreEqual(
                new[]
                {
                    Path.Combine(firstProject, "project.json"),
                    Path.Combine(secondProject, "project.json")
                },
                manifests.ToArray());
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    private static ProjectWorkspaceRepository CreateRepository() =>
        new(new ModernProjectStore(), new ReferenceProjectCompatibilityLocator());

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scada-project-lifecycle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        Assert.IsTrue(fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase));
        Directory.Delete(fullPath, recursive: true);
    }
}
