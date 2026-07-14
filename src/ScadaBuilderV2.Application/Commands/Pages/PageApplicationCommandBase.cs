using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Application.Pages;

namespace ScadaBuilderV2.Application.Commands.Pages;

/// <summary>Shared adapter from typed page requests to the page command coordinator.</summary>
public abstract class PageApplicationCommandBase<TRequest>(PageCommandCoordinator coordinator) : IApplicationCommand
    where TRequest : PageCommandRequest
{
    private readonly PageCommandCoordinator coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

    public abstract string Id { get; }

    public abstract string DisplayName { get; }

    public virtual bool CanExecute(ApplicationContext context) =>
        context.PageWorkspace is not null &&
        context.PageWorkspaceUi is not null &&
        context.PageCommandRequest is TRequest;

    public Task<CommandResult> ExecuteAsync(ApplicationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (context.PageWorkspace is not { } workspace ||
            context.PageWorkspaceUi is not { } ui ||
            context.PageCommandRequest is not TRequest request)
        {
            return Task.FromResult(CommandResult.Blocked($"La commande '{Id}' ne possède pas de contexte de page complet."));
        }

        var beforeWasDirty = context.IsPageWorkspaceDirty;
        var mutation = coordinator.Execute(workspace, ui, request);
        if (mutation.Result.Status != CommandResultStatus.Succeeded)
        {
            return Task.FromResult(mutation.Result);
        }

        context.ApplyPageWorkspaceMutation?.Invoke(mutation);
        context.PageWorkspace = mutation.After;
        context.CurrentProject = mutation.After.Project;
        context.PageWorkspaceUi = mutation.AfterUi;
        context.SelectedPageKey = mutation.AfterUi.SelectedPageKey;
        context.ActiveEditorPageKey = mutation.AfterUi.ActivePageKey;
        context.HomePageKey = mutation.After.Project.EffectiveHomePageKey;

        if (mutation.Result.Changed)
        {
            context.WorkspaceHistory?.Push(mutation.ToHistoryAction(beforeWasDirty));
            context.IsPageWorkspaceDirty = true;
        }

        return Task.FromResult(mutation.Result);
    }
}
