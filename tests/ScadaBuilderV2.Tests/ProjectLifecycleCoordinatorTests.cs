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

        Assert.IsFalse(closed.HasBlockingError);
        Assert.AreEqual(0, closed.Diagnostics.Count);
        Assert.AreEqual(0, host.SaveCount);
        Assert.AreEqual(1, host.CloseCount);
    }

    /// <summary>A refused save aborts the transition instead of destroying the work it was meant to keep.</summary>
    /// <remarks>
    /// The operator answered `Enregistrer` precisely to keep the changes. Before this gate the coordinator
    /// ignored the outcome and replaced the session anyway, so a full disk or a revoked permission silently
    /// discarded exactly what the answer was protecting.
    /// </remarks>
    [TestMethod]
    public async Task OpenAsync_SaveRefused_KeepsCurrentProjectAndReportsAnError()
    {
        var host = new StubHost
        {
            HasActiveProject = true,
            HasUnsavedChanges = true,
            CloseDecision = ProjectCloseDecision.Save,
            SaveSucceeds = false
        };
        var repository = new StubRepository { OpenResult = Success(CreateCandidate()) };
        var coordinator = new ProjectLifecycleCoordinator(repository, new StubRecentStore(), host);

        var result = await coordinator.OpenAsync(@"C:\Projects\Test\project.json");

        Assert.IsTrue(result.HasBlockingError);
        Assert.AreEqual("project.save-refused", result.Diagnostics.Single().Code);
        Assert.AreEqual(1, host.SaveCount);
        Assert.AreEqual(0, host.ActivationCount, "the session must not be replaced after a refused save");
        Assert.AreEqual(0, host.CloseCount);
    }

    [TestMethod]
    public async Task CloseAsync_SaveRefused_KeepsProjectOpenAndSaysWhy()
    {
        var host = new StubHost
        {
            HasActiveProject = true,
            HasUnsavedChanges = true,
            CloseDecision = ProjectCloseDecision.Save,
            SaveSucceeds = false
        };
        var coordinator = new ProjectLifecycleCoordinator(new StubRepository(), new StubRecentStore(), host);

        var result = await coordinator.CloseAsync();

        Assert.IsTrue(result.HasBlockingError);
        Assert.AreEqual("project.save-refused", result.Diagnostics.Single().Code);
        Assert.AreEqual(0, host.CloseCount);
    }

    /// <summary>A cancellation is not an error: the operator already knows what they chose.</summary>
    [TestMethod]
    public async Task CloseAsync_Cancelled_ReportsAWarningRatherThanAnError()
    {
        var host = new StubHost
        {
            HasActiveProject = true,
            HasUnsavedChanges = true,
            CloseDecision = ProjectCloseDecision.Cancel
        };
        var coordinator = new ProjectLifecycleCoordinator(new StubRepository(), new StubRecentStore(), host);

        var result = await coordinator.CloseAsync();

        Assert.IsFalse(result.HasBlockingError);
        Assert.AreEqual("project.transition-cancelled", result.Diagnostics.Single().Code);
        Assert.AreEqual(0, host.CloseCount);
    }

    /// <summary>An impossible creation request never costs the operator a decision about the open project.</summary>
    [TestMethod]
    public async Task CreateAsync_InvalidRequest_NeverAsksAboutUnsavedChanges()
    {
        var repository = new StubRepository
        {
            CreationValidation =
            [
                new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Error,
                    "project.create-target-exists",
                    "Le dossier cible existe déjà.")
            ]
        };
        var host = new StubHost { HasActiveProject = true, HasUnsavedChanges = true };
        var coordinator = new ProjectLifecycleCoordinator(repository, new StubRecentStore(), host);

        var result = await coordinator.CreateAsync(CreateRequest());

        Assert.IsTrue(result.HasBlockingError);
        Assert.AreEqual(0, host.DecisionRequestCount, "the request was already known to be impossible");
        Assert.AreEqual(0, repository.CreateCallCount);
        Assert.AreEqual(0, host.ActivationCount);
    }

    [TestMethod]
    public async Task CreateAsync_DirtySessionCancelled_CreatesNothing()
    {
        var repository = new StubRepository { CreateResult = Success(CreateCandidate()) };
        var host = new StubHost
        {
            HasActiveProject = true,
            HasUnsavedChanges = true,
            CloseDecision = ProjectCloseDecision.Cancel
        };
        var coordinator = new ProjectLifecycleCoordinator(repository, new StubRecentStore(), host);

        var result = await coordinator.CreateAsync(CreateRequest());

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("project.transition-cancelled", result.Diagnostics.Single().Code);
        Assert.AreEqual(0, repository.CreateCallCount, "D4 leaves neither a partial project nor a recent entry");
        Assert.AreEqual(0, host.ActivationCount);
    }

    [TestMethod]
    public async Task CreateAsync_CleanSession_ActivatesAndRecordsTheRecentEntry()
    {
        var repository = new StubRepository { CreateResult = Success(CreateCandidate()) };
        var recents = new StubRecentStore();
        var host = new StubHost();
        var coordinator = new ProjectLifecycleCoordinator(repository, recents, host);

        var result = await coordinator.CreateAsync(CreateRequest());

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, host.ActivationCount);
        Assert.AreEqual(1, recents.RecordCount);
        Assert.AreEqual(0, host.DecisionRequestCount);
    }

    /// <summary>A second transition started while one is running is refused, not queued.</summary>
    [TestMethod]
    public async Task ATransitionStartedWhileAnotherRunsIsRefusedAsBusy()
    {
        var gate = new TaskCompletionSource();
        var host = new StubHost { ActivationGate = gate.Task };
        var repository = new StubRepository { OpenResult = Success(CreateCandidate()) };
        var coordinator = new ProjectLifecycleCoordinator(repository, new StubRecentStore(), host);

        var first = coordinator.OpenAsync(@"C:\Projects\Test\project.json");
        var second = await coordinator.OpenAsync(@"C:\Projects\Other\project.json");

        Assert.IsTrue(second.HasBlockingError);
        Assert.AreEqual("project.transition-busy", second.Diagnostics.Single().Code);

        gate.SetResult();
        Assert.IsTrue((await first).IsSuccess);
        Assert.AreEqual(1, host.ActivationCount);
    }

    [TestMethod]
    public async Task CloseAsync_WithoutActiveProject_IsASuccessfulNoOp()
    {
        var host = new StubHost { HasActiveProject = false };
        var coordinator = new ProjectLifecycleCoordinator(new StubRepository(), new StubRecentStore(), host);

        var result = await coordinator.CloseAsync();

        Assert.IsFalse(result.HasBlockingError);
        Assert.AreEqual(0, host.CloseCount);
        Assert.AreEqual(0, host.DecisionRequestCount);
    }

    private static CreateProjectRequest CreateRequest() =>
        new(
            "Projet test",
            @"C:\Projects",
            "projet-test",
            "win00001",
            "Page principale",
            CanvasSize.DefaultDesktop,
            ResponsiveMode.Fixed,
            AuthoringMode.DesktopFirst);

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
        public IReadOnlyList<ScadaBuildValidationIssue> CreationValidation { get; init; } = [];
        public int CreateCallCount { get; private set; }

        public IReadOnlyList<ScadaBuildValidationIssue> ValidateCreation(CreateProjectRequest request) =>
            CreationValidation;

        public Task<ProjectRepositoryResult> CreateAsync(
            CreateProjectRequest request,
            CancellationToken cancellationToken = default)
        {
            CreateCallCount++;
            return Task.FromResult(CreateResult);
        }

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

        public bool SaveSucceeds { get; init; } = true;

        public Task<bool> SaveActiveProjectAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(SaveSucceeds);
        }

        /// <summary>Held open by the non-reentrancy test so a second transition overlaps the first.</summary>
        public Task? ActivationGate { get; init; }

        public async Task ActivateProjectAsync(ProjectLoadCandidate candidate, CancellationToken cancellationToken)
        {
            if (ActivationGate is not null)
            {
                await ActivationGate;
            }
            ActivationCount++;
            ActivatedCandidate = candidate;
        }

        public Task CloseActiveProjectAsync(CancellationToken cancellationToken)
        {
            CloseCount++;
            return Task.CompletedTask;
        }
    }
}
