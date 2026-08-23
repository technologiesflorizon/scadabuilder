using System.IO;
using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.App;

/// <summary>
/// Quick-window authoring surface of the shell: project group, selection, creation, duplication,
/// rename, deletion and the bounded editor context that hides page-only commands.
/// </summary>
/// <remarks>
/// Every decision lives in <see cref="QuickWindowWorkspaceController"/> and the Application services it
/// delegates to. This file owns only the WPF wiring: event handlers, dialogs and status projection.
///
/// Decisions: DEC-0050, FR-001, FR-033, FR-035, FR-UI-12, FR-UI-13, FR-UI-14, FR-UI-22, FR-UI-25, FR-UI-26.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §9.1.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowShellContractTests.cs.
/// </remarks>
public partial class MainWindow : IQuickWindowWorkspaceHost
{
    private QuickWindowWorkspaceController? _quickWindowWorkspaceController;
    private QuickWindowEditorContext? _activeEditorContext;
    private bool _isUpdatingQuickWindowSelection;
    private Guid? _hostedQuickWindowKey;
    private QuickWindowDefinition? _hostedQuickWindowDefinition;
    private bool _isQuickWindowInterfacePanelBound;

    /// <summary>
    /// Gets whether the canvas currently hosts a quick-window projection instead of a page.
    /// The projection is read-only in this slice: canvas messages are ignored so no interaction can
    /// mutate the last active page while its content is not the one displayed.
    /// </summary>
    private bool IsQuickWindowSurfaceHosted => _hostedQuickWindowKey is not null;

    /// <summary>Gets the quick-window workspace controller, created on first use.</summary>
    private QuickWindowWorkspaceController QuickWindowWorkspace =>
        _quickWindowWorkspaceController ??= new QuickWindowWorkspaceController(this);

    /// <summary>Gets the quick-window inventory bound by the project group.</summary>
    public QuickWindowsPanelViewModel QuickWindowsPanel => QuickWindowWorkspace.Panel;

    /// <summary>
    /// Gets the catalogue offered to the state, command, binding and expression selectors of the active
    /// surface: the project tags on a page, the local interface members on a quick window (FR-031).
    /// </summary>
    private ScadaTagCatalog? ActiveSelectorTagCatalog => _hostedQuickWindowDefinition is { } definition
        ? QuickWindowInterfaceCatalogProjection.Create(definition)
        : _modernProject?.TagCatalog;

    private void OnShowQuickWindowInterfaceAnchorableClick(object sender, RoutedEventArgs e) =>
        QuickWindowInterfaceAnchorable.Show();

    /// <summary>
    /// Returns whether one command applies to the active authoring surface.
    /// A page-only command is hidden on a quick window rather than left active and ambiguous.
    /// </summary>
    private bool IsCommandVisibleInActiveContext(RibbonCommandDefinition definition) =>
        _activeEditorContext?.IsCommandVisible(definition.Id) ?? true;

    /// <summary>Reloads the quick-window group from the current workspace snapshot.</summary>
    private async Task RefreshQuickWindowGroupAsync()
    {
        if (_modernProject is null) return;
        try
        {
            var snapshot = await _pageWorkspaceController.CaptureSnapshotAsync();
            QuickWindowWorkspace.Load(snapshot);
        }
        catch (Exception ex)
        {
            SetStatus($"Chargement des fenetres rapides impossible: {ex.Message}");
        }
    }

    private async void OnQuickWindowCommandClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string commandId } source) return;
        if (source.DataContext is QuickWindowListItemViewModel item)
        {
            _isUpdatingQuickWindowSelection = true;
            try
            {
                QuickWindowsPanel.Selected = item;
                QuickWindowsListBox.SelectedItem = item;
            }
            finally
            {
                _isUpdatingQuickWindowSelection = false;
            }
        }

        await ExecuteQuickWindowCommandAsync(commandId);
        e.Handled = true;
    }

    private void OnQuickWindowSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingQuickWindowSelection) return;
        QuickWindowsPanel.Selected = QuickWindowsListBox.SelectedItem as QuickWindowListItemViewModel;
    }

    private async void OnQuickWindowsListMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (QuickWindowsPanel.Selected is null) return;
        await ExecuteQuickWindowCommandAsync("quick-window.open");
        e.Handled = true;
    }

    private async Task ExecuteQuickWindowCommandAsync(string commandId)
    {
        if (_modernProject is null)
        {
            SetStatus("Aucun projet actif.");
            return;
        }

        var selectedKey = QuickWindowsPanel.Selected?.DefinitionKey;
        var snapshot = await _pageWorkspaceController.CaptureSnapshotAsync();
        var mutation = commandId switch
        {
            "quick-window.new" => await QuickWindowWorkspace.CreateAsync(snapshot),
            "quick-window.duplicate" when selectedKey is { } key => await QuickWindowWorkspace.DuplicateAsync(snapshot, key),
            "quick-window.rename" when selectedKey is { } key => await QuickWindowWorkspace.RenameAsync(snapshot, key),
            "quick-window.delete" when selectedKey is { } key => await QuickWindowWorkspace.DeleteAsync(snapshot, key),
            "quick-window.open" when selectedKey is { } key => await OpenQuickWindowAsync(snapshot, key),
            _ => null
        };

        if (mutation is null || mutation.Result.Status != CommandResultStatus.Succeeded) return;
        await ApplyQuickWindowMutationAsync(mutation);
    }

    private async Task<QuickWindowWorkspaceMutation?> OpenQuickWindowAsync(PageWorkspaceSnapshot snapshot, Guid definitionKey)
    {
        await QuickWindowWorkspace.OpenAsync(snapshot, definitionKey);
        return null;
    }

    /// <summary>
    /// Applies one prepared quick-window mutation as a single project-scoped, undoable transition and
    /// refreshes the group. The workspace keeps one history stack shared with pages.
    /// </summary>
    private async Task ApplyQuickWindowMutationAsync(QuickWindowWorkspaceMutation mutation)
    {
        var beforeUi = _pageWorkspaceController.CaptureUiSnapshot(_pagesPanel.SelectedPage?.PageKey);
        var beforeWasDirty = _pageWorkspaceController.IsProjectDirty;
        _modernProject = mutation.After.Project;
        _pageWorkspaceController.ReplaceProject(mutation.After.Project, markDirty: true);
        var afterUi = _pageWorkspaceController.CaptureUiSnapshot(_pagesPanel.SelectedPage?.PageKey);
        _pageWorkspaceController.History.Push(mutation.ToHistoryAction(beforeUi, afterUi, beforeWasDirty));
        await RefreshQuickWindowGroupAsync();
        if (mutation.AffectedDefinitionKey is { } definitionKey)
        {
            QuickWindowsPanel.Selected = QuickWindowsPanel.Items.FirstOrDefault(item => item.DefinitionKey == definitionKey);
        }

        RefreshActiveRibbonCommandStates();
        if (!string.IsNullOrWhiteSpace(mutation.Result.Message)) SetStatus(mutation.Result.Message);
    }

    /// <inheritdoc />
    public async Task ActivateQuickWindowAsync(QuickWindowEditorContext context, QuickWindowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(definition);
        _activeEditorContext = context;
        _hostedQuickWindowKey = definition.DefinitionKey;
        _hostedQuickWindowDefinition = definition;
        EditorContextBadgeText.Text = context.ContextBadge;
        EditorContextTitleText.Text = context.ContextTitle;
        EditorContextPanel.Visibility = Visibility.Visible;
        ShowLocalInterfacePanel();
        RefreshActiveRibbonCommandStates();
        await HostQuickWindowProjectionAsync(definition);
    }

    /// <summary>
    /// Materializes the definition visual content as an editor-only native preview and shows it on the
    /// shared canvas. The projection never enters the project, is excluded from build and carries a
    /// collision-free code, so no editor artifact can reach an export.
    /// </summary>
    private async Task HostQuickWindowProjectionAsync(QuickWindowDefinition definition)
    {
        if (_repositoryRoot is null)
        {
            SetPreviewPlaceholder("Aucun projet actif.");
            return;
        }

        try
        {
            var projection = QuickWindowPreviewProjection.Create(definition);
            var previewRoot = Path.Combine(
                _repositoryRoot,
                ".studio",
                "preview",
                QuickWindowPreviewProjection.PreviewDirectoryName);
            var preview = await PreviewDocument.MaterializeNativeAsync(
                new PageDocumentInput(projection.Reference, projection.Scene),
                previewRoot);
            var sourceUri = preview.GetSourceUri(previewRoot);

            UpdatePreviewSurfaceBackground(projection.Scene.BackgroundColor);
            ActivePageText.Text = projection.ProjectedCode;
            PreviewSourceText.Text = sourceUri.LocalPath;
            PreviewPlaceholder.Visibility = Visibility.Collapsed;
            PreviewWebView.Visibility = Visibility.Collapsed;
            PreviewWebView.Source = sourceUri;
            PreviewWebView.Visibility = Visibility.Visible;
            SetStatus($"Fenetre rapide affichee en lecture seule: {definition.DisplayName}");
        }
        catch (Exception ex)
        {
            SetPreviewPlaceholder($"Affichage de la fenetre rapide impossible: {ex.Message}");
        }
    }

    /// <summary>Restores the page context after a page becomes the active surface again.</summary>
    private void ActivatePageEditorContext(Guid pageKey, string code, string? title)
    {
        _activeEditorContext = QuickWindowEditorContext.ForPage(pageKey, code, title);
        _hostedQuickWindowKey = null;
        _hostedQuickWindowDefinition = null;
        QuickWindowWorkspace.ClearActiveContext();
        EditorContextBadgeText.Text = _activeEditorContext.ContextBadge;
        EditorContextTitleText.Text = _activeEditorContext.ContextTitle;
        EditorContextPanel.Visibility = Visibility.Visible;
        HideLocalInterfacePanel();
    }

    /// <summary>
    /// Shows `Interface locale` in place of the project `Catalogue Tags` while a quick window is authored.
    /// The tag catalogue is hidden rather than left visible and ambiguous: quick-window content never
    /// references a physical project tag.
    /// </summary>
    private void ShowLocalInterfacePanel()
    {
        if (!_isQuickWindowInterfacePanelBound)
        {
            QuickWindowInterfacePanelControl.Bind(QuickWindowWorkspace.InterfacePanel);
            QuickWindowInterfacePanelControl.AddMemberRequested += OnQuickWindowAddMemberRequested;
            QuickWindowInterfacePanelControl.EditMemberRequested += OnQuickWindowEditMemberRequested;
            QuickWindowInterfacePanelControl.DeleteMemberRequested += OnQuickWindowDeleteMemberRequested;
            QuickWindowInterfacePanelControl.NavigateToUsageRequested += OnQuickWindowNavigateToMemberUsageRequested;
            QuickWindowInterfacePanelControl.MemberInlineEdited += OnQuickWindowMemberInlineEdited;
            _isQuickWindowInterfacePanelBound = true;
        }

        TagCatalogAnchorable.Hide();
        QuickWindowInterfaceAnchorable.Show();
        QuickWindowInterfaceAnchorable.IsActive = true;
    }

    /// <summary>Restores the project `Catalogue Tags` when a page becomes the active surface again.</summary>
    private void HideLocalInterfacePanel()
    {
        QuickWindowInterfaceAnchorable.Hide();
        TagCatalogAnchorable.Show();
    }

    private async void OnQuickWindowAddMemberRequested(object? sender, EventArgs e) =>
        await ExecuteQuickWindowInterfaceCommandAsync((snapshot, definitionKey) =>
            QuickWindowWorkspace.AddInterfaceMemberAsync(snapshot, definitionKey));

    private async void OnQuickWindowEditMemberRequested(object? sender, QuickWindowInterfaceMemberViewModel member) =>
        await ExecuteQuickWindowInterfaceCommandAsync((snapshot, definitionKey) =>
            QuickWindowWorkspace.EditInterfaceMemberAsync(snapshot, definitionKey, member.MemberKey));

    private async void OnQuickWindowDeleteMemberRequested(object? sender, QuickWindowInterfaceMemberViewModel member) =>
        await ExecuteQuickWindowInterfaceCommandAsync((snapshot, definitionKey) =>
            QuickWindowWorkspace.DeleteInterfaceMemberAsync(snapshot, definitionKey, member.MemberKey));

    private async void OnQuickWindowMemberInlineEdited(object? sender, QuickWindowInterfaceMemberViewModel member) =>
        await ExecuteQuickWindowInterfaceCommandAsync((snapshot, definitionKey) =>
            Task.FromResult(QuickWindowWorkspace.ApplyInlineInterfaceEdit(snapshot, definitionKey, member.Member)));

    private async void OnQuickWindowNavigateToMemberUsageRequested(object? sender, QuickWindowInterfaceMemberViewModel member)
    {
        if (_modernProject is null || _hostedQuickWindowKey is not { } definitionKey) return;
        var snapshot = await _pageWorkspaceController.CaptureSnapshotAsync();
        var mutation = QuickWindowWorkspace.NavigateToMemberUsage(snapshot, definitionKey, member.MemberKey);
        if (mutation is null) return;
        NavigateToQuickWindowUsage(mutation);
    }

    /// <summary>
    /// Applies one local-interface command as a single undoable transition, then refreshes the hosted
    /// projection so the canvas keeps showing the definition that was just edited.
    /// </summary>
    private async Task ExecuteQuickWindowInterfaceCommandAsync(
        Func<PageWorkspaceSnapshot, Guid, Task<QuickWindowWorkspaceMutation?>> command)
    {
        if (_modernProject is null || _hostedQuickWindowKey is not { } definitionKey)
        {
            SetStatus("Aucune fenetre rapide active.");
            return;
        }

        var snapshot = await _pageWorkspaceController.CaptureSnapshotAsync();
        var mutation = await command(snapshot, definitionKey);
        if (mutation is null || mutation.Result.Status != CommandResultStatus.Succeeded || !mutation.Result.Changed) return;

        await ApplyQuickWindowMutationAsync(mutation);
        if (mutation.After.Project.EffectiveQuickWindows.FirstOrDefault(item => item.DefinitionKey == definitionKey) is { } updated)
        {
            _hostedQuickWindowDefinition = updated;
        }
    }

    /// <summary>
    /// Builds the bounded quick-window authoring context of the active surface: the definitions that may be
    /// opened, the parent ports a binding may forward and whether `CloseQuickWindow(Self)` is offered.
    /// </summary>
    private QuickWindowCommandAuthoringContext BuildQuickWindowAuthoringContext() => new(
        _modernProject?.EffectiveQuickWindows ?? [],
        _hostedQuickWindowDefinition?.EffectiveInterfaceMembers ?? [],
        IsQuickWindowSurfaceHosted,
        invocationKey => _modernProject?.EffectiveQuickWindowInvocations
            .FirstOrDefault(invocation => invocation.InvocationKey == invocationKey));

    /// <summary>
    /// Applies one invocation authored by the `Liaisons` tab as a single undoable transition covering the
    /// project, the caller scene and the caller command.
    /// </summary>
    private async Task<QuickWindowInvocationAuthoringOutcome> SaveQuickWindowInvocationFromDialogAsync(
        string elementId,
        QuickWindowInvocationAuthoringRequest request)
    {
        if (_modernProject is null || _activeSceneTab?.PageKey is not { } pageKey)
        {
            return QuickWindowInvocationAuthoringOutcome.Blocked("Aucune page active pour enregistrer les liaisons.");
        }

        try
        {
            var snapshot = await _pageWorkspaceController.CaptureSnapshotAsync();
            var mutation = QuickWindowWorkspace.SaveInvocation(snapshot, pageKey, elementId, request);
            if (mutation.Result.Status != CommandResultStatus.Succeeded)
            {
                var reason = mutation.Result.Diagnostics.FirstOrDefault()?.Message ?? mutation.Result.Message;
                return new QuickWindowInvocationAuthoringOutcome(false, null, reason, mutation.Result.Diagnostics);
            }

            await ApplyQuickWindowMutationAsync(mutation);
            if (mutation.After.Scenes.TryGetValue(pageKey, out var updatedScene))
            {
                _activeScene = updatedScene;
                MarkActiveSceneDirty();
                RefreshModernSceneUi();
            }

            return new QuickWindowInvocationAuthoringOutcome(
                true,
                mutation.AffectedInvocationKey,
                mutation.Result.Message,
                mutation.Result.Diagnostics);
        }
        catch (Exception ex)
        {
            return QuickWindowInvocationAuthoringOutcome.Blocked($"Enregistrement des liaisons impossible: {ex.Message}");
        }
    }

    /// <summary>Selects and reveals the page that owns one quick-window usage, without mutating anything.</summary>
    private void NavigateToQuickWindowUsage(QuickWindowWorkspaceMutation mutation)
    {
        if (mutation.Result.PageToSelectKey is { } pageKey)
        {
            _pagesPanel.SelectedPage = _pagesPanel.Items.FirstOrDefault(item => item.PageKey == pageKey);
            _isUpdatingPageSelection = true;
            try
            {
                PagesListBox.SelectedItem = _pagesPanel.SelectedPage;
            }
            finally
            {
                _isUpdatingPageSelection = false;
            }

            PageAnchorable.IsActive = true;
        }

        var usage = mutation.UsageToNavigate;
        SetStatus(usage is null
            ? mutation.Result.Message
            : $"Utilisation: {usage.PropertyPath}");
    }

    /// <inheritdoc />
    public async Task<string?> RequestQuickWindowNameAsync(string title, string proposedName)
    {
        var dialog = new LibraryNameDialog(title, proposedName) { Owner = this };
        return dialog.ShowDialog() == true ? dialog.EnteredName : null;
    }

    /// <inheritdoc />
    public Task<bool> ConfirmQuickWindowDeletionAsync(QuickWindowDefinition definition, IReadOnlyList<QuickWindowUsage> usages)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(usages);
        if (usages.Count > 0)
        {
            var callers = string.Join(Environment.NewLine, usages.Take(10).Select(usage => $"· {usage.PropertyPath}"));
            MessageBox.Show(
                this,
                $"'{definition.DisplayName}' est encore referencee par {usages.Count} appelant(s):{Environment.NewLine}{callers}{Environment.NewLine}{Environment.NewLine}Retirez ces appels avant de supprimer la definition.",
                "Suppression bloquee",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return Task.FromResult(false);
        }

        var confirmed = MessageBox.Show(
            this,
            $"Supprimer definitivement la fenetre rapide '{definition.DisplayName}' ?",
            "Supprimer la fenetre rapide",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
        return Task.FromResult(confirmed);
    }

    /// <inheritdoc />
    public Task<QuickWindowInterfaceMember?> RequestInterfaceMemberAsync(
        string title,
        QuickWindowInterfaceMemberDraft draft,
        IReadOnlyList<QuickWindowInterfaceMember> siblings)
    {
        var dialog = new QuickWindowInterfaceMemberDialog(title, draft, siblings) { Owner = this };
        return Task.FromResult(dialog.ShowDialog() == true ? dialog.AuthoredMember : null);
    }

    /// <inheritdoc />
    public Task<bool> ConfirmInterfaceMemberDeletionAsync(
        QuickWindowInterfaceMember member,
        IReadOnlyList<QuickWindowUsage> usages)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(usages);
        var callers = string.Join(Environment.NewLine, usages.Take(10).Select(usage => $"· {usage.PropertyPath}"));
        var confirmed = MessageBox.Show(
            this,
            $"Le membre '{member.Name}' est utilise par {usages.Count} liaison(s):{Environment.NewLine}{callers}{Environment.NewLine}{Environment.NewLine}Supprimer ce membre rendra ces invocations a reparer. Continuer ?",
            "Supprimer un membre reference",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
        return Task.FromResult(confirmed);
    }

    /// <inheritdoc />
    public void ReportQuickWindowStatus(string message) => SetStatus(message);
}
