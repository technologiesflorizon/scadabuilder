using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ApplicationCommandTests
{
    [TestMethod]
    public async Task RegistryTogglesPersistentElementLockAsynchronously()
    {
        var registry = new CommandRegistry();
        registry.Register(new ToggleElementLockCommand());
        var element = ScadaElement.CreateText("element-1", "Element 1", 10, 20);
        var scene = ScadaScene.CreateEmpty("scene", "Scene", new(1280, 873)).WithElement(element);
        var context = new ApplicationContext { ActiveSceneSnapshot = scene };
        context.ApplyActiveSceneMutation = updated => context.ActiveSceneSnapshot = updated;
        context.Selection.SetSelection(["element-1"], "element-1");

        var result = await registry.ExecuteAsync("object.lock", context);

        Assert.AreEqual(CommandResultStatus.Succeeded, result.Status);
        Assert.IsTrue(result.Changed);
        Assert.IsTrue(context.ActiveSceneSnapshot.FindElementRecursive("element-1")!.IsLocked);
        Assert.IsTrue(result.WorkspaceDirty);
        Assert.IsFalse(context.IsBusy);
    }

    [TestMethod]
    public async Task RegistryReturnsCancelledInsteadOfFailedAndReleasesGate()
    {
        var registry = new CommandRegistry();
        registry.Register(new DelegateCommand(
            "cancel",
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return CommandResult.NoChange("unreachable");
            }));
        var context = new ApplicationContext();
        using var cancellation = new CancellationTokenSource();

        var execution = registry.ExecuteAsync("cancel", context, cancellation.Token);
        cancellation.Cancel();
        var result = await execution;

        Assert.AreEqual(CommandResultStatus.Cancelled, result.Status);
        Assert.IsFalse(result.Changed);
        Assert.IsFalse(context.IsBusy);
    }

    [TestMethod]
    public async Task RegistryBlocksOverlappingExecutionOnTheSameWorkspace()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = new CommandRegistry();
        registry.Register(new DelegateCommand(
            "long-running",
            async (_, cancellationToken) =>
            {
                started.SetResult();
                await release.Task.WaitAsync(cancellationToken);
                return CommandResult.Success("done");
            }));
        var context = new ApplicationContext();

        var firstExecution = registry.ExecuteAsync("long-running", context);
        await started.Task;
        Assert.IsTrue(context.IsBusy);

        var secondResult = await registry.ExecuteAsync("long-running", context);
        Assert.AreEqual(CommandResultStatus.Blocked, secondResult.Status);
        StringAssert.Contains(secondResult.Message, "already running");

        release.SetResult();
        var firstResult = await firstExecution;
        Assert.AreEqual(CommandResultStatus.Succeeded, firstResult.Status);
        Assert.IsFalse(context.IsBusy);
    }

    [TestMethod]
    public async Task RegistryAppliesAuthorizationBeforeExecution()
    {
        var executed = false;
        var registry = new CommandRegistry();
        registry.Register(new DelegateCommand(
            "restricted",
            (_, _) =>
            {
                executed = true;
                return Task.FromResult(CommandResult.Success("unexpected"));
            }));
        var context = new ApplicationContext(new DenyAllAuthorizationPolicy());

        var result = await registry.ExecuteAsync("restricted", context);

        Assert.AreEqual(CommandResultStatus.Blocked, result.Status);
        Assert.IsFalse(executed);
    }

    [TestMethod]
    public async Task RegistryConvertsUnhandledExceptionsToFailedResult()
    {
        var expected = new InvalidOperationException("boom");
        var registry = new CommandRegistry();
        registry.Register(new DelegateCommand(
            "failure",
            (_, _) => Task.FromException<CommandResult>(expected)));

        var result = await registry.ExecuteAsync("failure", new ApplicationContext());

        Assert.AreEqual(CommandResultStatus.Failed, result.Status);
        Assert.AreSame(expected, result.Exception);
        StringAssert.Contains(result.Message, "boom");
    }

    [TestMethod]
    public void ResultCarriesInternalRoutingDirtyStateAndDiagnostics()
    {
        var pageKey = Guid.NewGuid();
        var diagnostic = new ScadaBuildValidationIssue(
            ScadaBuildValidationSeverity.Error,
            "page.invalid",
            "Invalid page",
            "page_one");

        var result = CommandResult.Success(
            "updated",
            [pageKey, Guid.Empty, pageKey],
            pageToSelectKey: pageKey,
            pageToOpenKey: pageKey,
            workspaceDirty: true,
            diagnostics: [diagnostic]);

        CollectionAssert.AreEqual(new[] { pageKey }, result.AffectedPageKeys.ToArray());
        Assert.AreEqual(pageKey, result.PageToSelectKey);
        Assert.AreEqual(pageKey, result.PageToOpenKey);
        Assert.IsTrue(result.WorkspaceDirty);
        Assert.AreSame(diagnostic, result.Diagnostics.Single());
    }

    private sealed class DelegateCommand(
        string id,
        Func<ApplicationContext, CancellationToken, Task<CommandResult>> executeAsync) : IApplicationCommand
    {
        public string Id => id;

        public string DisplayName => id;

        public bool CanExecute(ApplicationContext context) => true;

        public Task<CommandResult> ExecuteAsync(
            ApplicationContext context,
            CancellationToken cancellationToken = default) => executeAsync(context, cancellationToken);
    }

    private sealed class DenyAllAuthorizationPolicy : ICommandAuthorizationPolicy
    {
        public bool IsAuthorized(IApplicationCommand command, ApplicationContext context) => false;
    }
}
