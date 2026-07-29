using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.Projects;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ProjectLifecycleCoordinatorTests
{
    [TestMethod]
    public async Task OpenAsync_InvalidCandidate_DoesNotReplaceActiveSession()
    {
        var repository = new StubRepository { OpenResult = Failure("project.invalid") };
        var host = new StubHost { HasActiveProject = true, HasUnsavedChanges = true };
        var coordinator = new ProjectLifecycleCoordinator(repository, new StubRecentStore(), host);

        var result = await coordinator.OpenAsync(@"C:\broken\project.json");

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(0, host.DecisionRequestCount);
        Assert.AreEqual(0, host.ActivationCount);
        Assert.AreEqual(0, host.CloseCount);
    }

    [TestMethod]
    public async Task OpenAsync_DirtySessionCancelled_KeepsCurrentProject()
    {
        var candidate = CreateCandidate();
        var host = new StubHost
        {
            HasActiveProject = true,
            HasUnsavedChanges = true,
            CloseDecision = ProjectCloseDecision.Cancel
        };
        var recent = new StubRecentStore();
        var coordinator = new ProjectLifecycleCoordinator(
            new StubRepository { OpenResult = Success(candidate) },
            recent,
            host);

        var result = await coordinator.OpenAsync(candidate.Location.ProjectFilePath);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(1, host.DecisionRequestCount);
        Assert.AreEqual(0, host.ActivationCount);
        Assert.AreEqual(0, recent.RecordCount);
    }

    [TestMethod]
    public async Task OpenAsync_DirtySessionSaved_ActivatesCandidateAndRecordsRecent()
    {
        var candidate = CreateCandidate();
        var host = new StubHost
        {
            HasActiveProject = true,
            HasUnsavedChanges = true,
            CloseDecision = ProjectCloseDecision.Save
        };
        var recent = new StubRecentStore();
        var coordinator = new ProjectLifecycleCoordinator(
            new StubRepository { OpenResult = Success(candidate) },
            recent,
            host);

        var result = await coordinator.OpenAsync(candidate.Location.ProjectFilePath);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, host.SaveCount);
        Assert.AreEqual(1, host.ActivationCount);
        Assert.AreSame(candidate, host.ActivatedCandidate);
        Assert.AreEqual(1, recent.RecordCount);
    }

    [TestMethod]
    public async Task CloseAsync_DiscardClosesWithoutSaving()
    {
        var host = new StubHost
        {
            HasActiveProject = true,
            HasUnsavedChanges = true,
            CloseDecision = ProjectCloseDecision.Discard
        };
        var coordinator = new ProjectLifecycleCoordinator(new StubRepository(), new StubRecentStore(), host);

        var closed = await coordinator.CloseAsync();

        Assert.IsTrue(closed);
        Assert.AreEqual(0, host.SaveCount);
        Assert.AreEqual(1, host.CloseCount);
    }

    private static ProjectLoadCandidate CreateCandidate()
    {
        var project = ScadaProject.CreateDefault("Projet test");
        var location = new ProjectWorkspaceLocation(@"C:\Projects\Test", @"C:\Projects\Test\project.json");
        var snapshot = new PageWorkspaceSnapshot(1, project, new Dictionary<Guid, ScadaScene>(), []);
        return new ProjectLoadCandidate(location, snapshot, false, []);
    }

    private static ProjectRepositoryResult Success(ProjectLoadCandidate candidate) => new(candidate, []);

    private static ProjectRepositoryResult Failure(string code) =>
        new(null, [new(ScadaBuildValidationSeverity.Error, code, "Échec attendu.")]);

    private sealed class StubRepository : IProjectWorkspaceRepository
    {
        public ProjectRepositoryResult OpenResult { get; init; } = Failure("not-configured");
        public ProjectRepositoryResult CreateResult { get; init; } = Failure("not-configured");

        public Task<ProjectRepositoryResult> CreateAsync(
            CreateProjectRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResult);

        public Task<ProjectRepositoryResult> OpenAsync(
            string projectFilePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OpenResult);
    }

    private sealed class StubRecentStore : IRecentProjectStore
    {
        public int RecordCount { get; private set; }
        public bool IsInitialized => false;

        public Task<IReadOnlyList<RecentProjectEntry>> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RecentProjectEntry>>([]);

        public Task RecordAsync(
            ProjectWorkspaceLocation location,
            string displayName,
            CancellationToken cancellationToken = default)
        {
            RecordCount++;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string projectFilePath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public string GetDefaultCreationParent() => @"C:\Projects";

        public Task<string> ReadCreationParentAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(GetDefaultCreationParent());

        public Task WriteCreationParentAsync(string path, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubHost : IProjectLifecycleHost
    {
        public bool HasActiveProject { get; init; }
        public bool HasUnsavedChanges { get; init; }
        public ProjectCloseDecision CloseDecision { get; init; } = ProjectCloseDecision.Cancel;
        public int DecisionRequestCount { get; private set; }
        public int SaveCount { get; private set; }
        public int ActivationCount { get; private set; }
        public int CloseCount { get; private set; }
        public ProjectLoadCandidate? ActivatedCandidate { get; private set; }

        public Task<ProjectCloseDecision> RequestCloseDecisionAsync(CancellationToken cancellationToken)
        {
            DecisionRequestCount++;
            return Task.FromResult(CloseDecision);
        }

        public Task SaveActiveProjectAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task ActivateProjectAsync(ProjectLoadCandidate candidate, CancellationToken cancellationToken)
        {
            ActivationCount++;
            ActivatedCandidate = candidate;
            return Task.CompletedTask;
        }

        public Task CloseActiveProjectAsync(CancellationToken cancellationToken)
        {
            CloseCount++;
            return Task.CompletedTask;
        }
    }
}
