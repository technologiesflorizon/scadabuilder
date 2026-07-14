using System.Windows;
using ScadaBuilderV2.App.Diagnostics;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App.Pages;

/// <summary>Connects WPF page surfaces to the shared asynchronous page command registry.</summary>
public sealed class PageCommandController(
    Window owner,
    CommandRegistry registry,
    PageWorkspaceController workspace,
    Action<ScadaProject, CommandResult> completed,
    Action showDiagnostics)
{
    public async Task<CommandResult> ExecuteAsync(string commandId, PageCommandRequest request, Guid? selectedPageKey)
    {
        var snapshot = await workspace.CaptureSnapshotAsync();
        var ui = workspace.CaptureUiSnapshot(selectedPageKey);
        PageWorkspaceMutation? prepared = null;
        var context = new ApplicationContext
        {
            CurrentProject = snapshot.Project,
            PageWorkspace = snapshot,
            PageWorkspaceUi = ui,
            PageCommandRequest = request,
            SelectedPageKey = selectedPageKey,
            ActiveEditorPageKey = ui.ActivePageKey,
            HomePageKey = snapshot.Project.EffectiveHomePageKey,
            WorkspaceHistory = workspace.History,
            IsPageWorkspaceDirty = workspace.IsProjectDirty,
            ApplyPageWorkspaceMutation = mutation => prepared = mutation
        };
        var result = await registry.ExecuteAsync(commandId, context);
        if (prepared is not null) await workspace.ApplyMutationAsync(prepared);
        completed(context.CurrentProject ?? snapshot.Project, result);
        return result;
    }

    public async Task<CommandResult> ExecuteInteractiveAsync(string commandId, ScadaSceneReference? page)
    {
        if (commandId == "page.new")
        {
            var dialog = PageEditorDialog.ForNew(PageCodePolicy.SuggestDuplicateCode("page", workspace.Project?.Scenes.Select(item => item.EffectivePageCode)));
            dialog.Owner = owner;
            return dialog.ShowDialog() == true
                ? await ExecuteAsync(commandId, new NewPageRequest(dialog.PageCode, dialog.PageTitle), page?.PageKey)
                : CommandResult.Cancelled();
        }
        if (page is null) return CommandResult.Blocked("Sélectionnez d'abord une page.");
        if (commandId == "page.delete" && MessageBox.Show(
                owner,
                $"Supprimer la page '{page.EffectivePageCode}'? Les dépendances seront vérifiées avant toute modification.",
                "Supprimer la page",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return CommandResult.Cancelled();
        }
        if (commandId is "page.rename" or "page.duplicate")
        {
            var duplicate = commandId == "page.duplicate";
            var suggested = duplicate
                ? PageCodePolicy.SuggestDuplicateCode(page.EffectivePageCode, workspace.Project?.Scenes.Select(item => item.EffectivePageCode))
                : page.EffectivePageCode;
            var dialog = new PageEditorDialog(duplicate ? PageEditorDialogMode.Duplicate : PageEditorDialogMode.Rename, suggested, duplicate ? $"{page.Title} - copie" : page.Title) { Owner = owner };
            if (dialog.ShowDialog() != true) return CommandResult.Cancelled();
            return duplicate
                ? await ExecuteAsync(commandId, new DuplicatePageRequest(page.PageKey, dialog.PageCode, dialog.PageTitle), page.PageKey)
                : await ExecuteAsync(commandId, new RenamePageRequest(page.PageKey, dialog.PageTitle), page.PageKey);
        }
        return commandId switch
        {
            "page.delete" => await ExecuteAsync(commandId, new DeletePageRequest(page.PageKey), page.PageKey),
            "page.open" => await ExecuteAsync(commandId, new OpenPageRequest(page.PageKey), page.PageKey),
            "page.properties" => await ExecuteAsync(commandId, new ShowPagePropertiesRequest(page.PageKey), page.PageKey),
            "page.validate" => await ExecuteAsync(commandId, new ValidatePagesRequest(), page.PageKey),
            _ => CommandResult.Blocked($"Commande de page inconnue: {commandId}")
        };
    }

    /// <summary>Shows a structured command failure and optionally opens detailed diagnostics.</summary>
    public void PresentFailure(CommandResult result)
    {
        if (result.Status == CommandResultStatus.Cancelled) return;
        var dialog = new CommandErrorDialog(result) { Owner = owner };
        dialog.ShowDialog();
        if (dialog.ShowDiagnosticsRequested) showDiagnostics();
    }

    /// <summary>Shows a structured validation failure and optionally opens detailed diagnostics.</summary>
    public void PresentFailure(string summary, IReadOnlyList<ScadaBuildValidationIssue> issues)
    {
        var dialog = new CommandErrorDialog(summary, issues) { Owner = owner };
        dialog.ShowDialog();
        if (dialog.ShowDiagnosticsRequested) showDiagnostics();
    }
}
