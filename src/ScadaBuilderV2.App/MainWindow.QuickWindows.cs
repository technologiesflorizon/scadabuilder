using System.IO;
using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Application.Clipboard;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;
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
    private bool _isUpdatingParameterSheetSelection;
    private Guid? _hostedQuickWindowKey;
    private QuickWindowDefinition? _hostedQuickWindowDefinition;
    private bool _isQuickWindowInterfacePanelBound;
    private bool _isQuickWindowTestBenchBound;
    private BuilderQuickWindowHostAdapter? _quickWindowPreviewHost;

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

    /// <summary>Gets the parameter-sheet inventory listed by the "Fenêtres rapides" panel (DEC-0052).</summary>
    public ParameterSheetsPanelViewModel ParameterSheetsPanel => QuickWindowWorkspace.SheetsPanel;

    /// <summary>
    /// Gets the catalogue offered to the state, command, binding and expression selectors of the active
    /// surface: the project tags on a page, the local interface members on a quick window (FR-031).
    /// </summary>
    private ScadaTagCatalog? ActiveSelectorTagCatalog => _hostedQuickWindowDefinition is { } definition
        ? QuickWindowInterfaceCatalogProjection.Create(definition)
        : IsActivePageOfQuickWindowType
            ? BuildActiveQuickWindowPagePlaceholderCatalog()
            : _modernProject?.TagCatalog;

    /// <summary>
    /// Gets whether the active page (not a hosted quick-window definition) carries
    /// <see cref="ScadaPageType.QuickWindow"/>: its selectors offer the `#N` parameters defined by the
    /// parameter sheets linked to it instead of project tags (DEC-0052).
    /// </summary>
    private bool IsActivePageOfQuickWindowType =>
        _activeSceneTab?.PageKey is { } pageKey &&
        _modernProject?.Scenes.FirstOrDefault(page => page.PageKey == pageKey)?.Type == ScadaPageType.QuickWindow;

    /// <summary>
    /// Builds the `#N` catalog of the active quick-window page from the project's parameter sheets
    /// (<see cref="QuickWindowPagePlaceholderCatalog"/>), replacing the former static `#1`..`#999` list.
    /// </summary>
    /// <remarks>
    /// Recomputed on every access rather than cached: the walk below only touches objects already held by
    /// <see cref="_pageWorkspaceController"/> and <see cref="_activeScene"/> (no new
    /// <see cref="PageWorkspaceController.CaptureSnapshotAsync"/>, no disk read), so a sheet edited or a
    /// linking command added since the last dialog open is always reflected without any invalidation to get
    /// wrong. Only pages currently open (or visited by project history) contribute their commands to the
    /// "feuilles liées" search; a linking command on a page never opened this session is not found, which
    /// degrades to the safe "no sheet linked -> keep every sheet" rule rather than hiding a value.
    ///
    /// Decisions: DEC-0052.
    /// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPagePlaceholderCatalogTests.cs.
    /// </remarks>
    private ScadaTagCatalog BuildActiveQuickWindowPagePlaceholderCatalog()
    {
        if (_modernProject is null || _activeSceneTab?.PageKey is not { } pageKey)
        {
            return new ScadaTagCatalog("quick-window-page-placeholder-parameters/2.0", Array.Empty<ScadaTagDefinition>());
        }

        var scenes = new Dictionary<Guid, ScadaScene>();
        foreach (var key in _pageWorkspaceController.GetWorkspaceSceneKeys())
        {
            if (_pageWorkspaceController.GetScene(key) is { } scene) scenes[key] = scene;
        }

        if (_activeScene is not null) scenes[pageKey] = _activeScene;

        return QuickWindowPagePlaceholderCatalog.Build(_modernProject, scenes, pageKey);
    }

    private void OnShowQuickWindowsAnchorableClick(object sender, RoutedEventArgs e) =>
        ShowAnchorable(QuickWindowsAnchorable);

    private void OnShowQuickWindowInterfaceAnchorableClick(object sender, RoutedEventArgs e) =>
        ShowAnchorable(QuickWindowInterfaceAnchorable);

    private void OnShowQuickWindowTestBenchAnchorableClick(object sender, RoutedEventArgs e) =>
        ShowAnchorable(QuickWindowTestBenchAnchorable);

    /// <summary>Gets the editor-only preview host adapter, created once the project root is known.</summary>
    private BuilderQuickWindowHostAdapter? QuickWindowPreviewHost =>
        _quickWindowPreviewHost ??= _repositoryRoot is null
            ? null
            : new BuilderQuickWindowHostAdapter(Path.Combine(_repositoryRoot, ".studio", "preview"));

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

    private async void OnParameterSheetCommandClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string commandId } source) return;
        if (source.DataContext is ParameterSheetListItemViewModel item)
        {
            _isUpdatingParameterSheetSelection = true;
            try
            {
                ParameterSheetsPanel.Selected = item;
                ParameterSheetsListBox.SelectedItem = item;
            }
            finally
            {
                _isUpdatingParameterSheetSelection = false;
            }
        }

        await ExecuteParameterSheetCommandAsync(commandId);
        e.Handled = true;
    }

    private void OnParameterSheetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingParameterSheetSelection) return;
        ParameterSheetsPanel.Selected = ParameterSheetsListBox.SelectedItem as ParameterSheetListItemViewModel;
    }

    private async void OnParameterSheetsListMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ParameterSheetsPanel.Selected is null) return;
        await ExecuteParameterSheetCommandAsync("parameter-sheet.open");
        e.Handled = true;
    }

    /// <summary>Dispatches one parameter-sheet panel command: create, rename, duplicate, delete or open (DEC-0052).</summary>
    /// <remarks>
    /// A workspace snapshot reads every non-open scene of the project, so it is never captured before a name
    /// dialog: new, rename and duplicate ask first and capture only once the operator confirmed (a cancelled
    /// dialog captures nothing), and opening a sheet only needs the in-memory project. Only delete, which must
    /// scan every page for callers before its confirmation, captures up front.
    /// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetAskBeforeCaptureTests.cs.
    /// </remarks>
    private async Task ExecuteParameterSheetCommandAsync(string commandId)
    {
        if (_modernProject is null)
        {
            SetStatus("Aucun projet actif.");
            return;
        }

        var selectedKey = ParameterSheetsPanel.Selected?.SheetKey;
        var currentProject = _pageWorkspaceController.Project ?? _modernProject;
        Func<Task<PageWorkspaceSnapshot>> captureSnapshot = () => _pageWorkspaceController.CaptureSnapshotAsync();

        if (commandId == "parameter-sheet.open")
        {
            if (selectedKey is not { } openKey) return;
            await OpenParameterSheetEditorAsync(currentProject, openKey);
            return;
        }

        var mutation = commandId switch
        {
            "parameter-sheet.new" => await QuickWindowWorkspace.CreateSheetAsync(currentProject, captureSnapshot),
            "parameter-sheet.duplicate" when selectedKey is { } key => await QuickWindowWorkspace.DuplicateSheetAsync(currentProject, key, captureSnapshot),
            "parameter-sheet.rename" when selectedKey is { } key => await QuickWindowWorkspace.RenameSheetAsync(currentProject, key, captureSnapshot),
            "parameter-sheet.delete" when selectedKey is { } key => await QuickWindowWorkspace.DeleteSheetAsync(await captureSnapshot(), key),
            _ => null
        };

        if (mutation is null) return;
        if (mutation.Result.Status != CommandResultStatus.Succeeded)
        {
            var reason = mutation.Result.Diagnostics.FirstOrDefault()?.Message;
            if (!string.IsNullOrWhiteSpace(reason)) SetStatus(reason);
            return;
        }

        await ApplyParameterSheetMutationAsync(mutation);
        if (commandId == "parameter-sheet.new" && mutation.AffectedDefinitionKey is { } createdKey)
        {
            // The mutation's After snapshot already carries the new sheet: no second capture to open the editor.
            await OpenParameterSheetEditorAsync(mutation.After.Project, createdKey);
        }
    }

    /// <summary>Opens the sheet editor dialog on one sheet; "Enregistrer" applies an undoable text update.</summary>
    private async Task OpenParameterSheetEditorAsync(ScadaProject project, Guid sheetKey)
    {
        var sheet = project.EffectiveParameterSheets.FirstOrDefault(item => item.SheetKey == sheetKey);
        if (sheet is null) return;

        var viewModel = new ParameterSheetEditorViewModel(sheet, _modernProject?.TagCatalog);
        var dialog = new ParameterSheetEditorDialog(viewModel) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SavedSheet is not { } saved) return;

        var current = await _pageWorkspaceController.CaptureSnapshotAsync();
        var mutation = await QuickWindowWorkspace.UpdateSheetTextAsync(current, sheetKey, saved.Text);
        if (mutation is null) return;
        await ApplyParameterSheetMutationAsync(mutation);
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

    /// <summary>Applies one prepared parameter-sheet mutation, then keeps the sheet panel selection in sync.</summary>
    private async Task ApplyParameterSheetMutationAsync(QuickWindowWorkspaceMutation mutation)
    {
        await ApplyQuickWindowMutationAsync(mutation);
        if (mutation.AffectedDefinitionKey is { } sheetKey)
        {
            ParameterSheetsPanel.Selected = ParameterSheetsPanel.Items.FirstOrDefault(item => item.SheetKey == sheetKey);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Retained only to satisfy <see cref="IQuickWindowWorkspaceHost"/>: <see cref="QuickWindowWorkspaceController.OpenAsync"/>
    /// is its only caller and has had none of its own since Task 9 replaced the "+" empty-definition
    /// workflow with authoring quick windows as pages (spec §9) — no panel or menu reaches this method any
    /// more. It still hosts the definition's visual content as a read-only canvas projection (defence in
    /// depth) but no longer drives `Interface locale`/`Banc d'essai`: those now read the ACTIVE PAGE
    /// (<see cref="RefreshQuickWindowPageSurfaces"/>), which this hosted-definition surface is not.
    /// </remarks>
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
        HideLocalInterfacePanel();
        HideQuickWindowTestBench();
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

    /// <summary>
    /// Restores the page context after a page becomes the active surface again, and shows `Interface
    /// locale`/`Banc d'essai` for the active page's own `#N` placeholders when it is authored as
    /// <see cref="ScadaPageType.QuickWindow"/> (DEC-0052) instead of the retired hosted-definition path.
    /// </summary>
    private void ActivatePageEditorContext(Guid pageKey, string code, string? title)
    {
        _activeEditorContext = QuickWindowEditorContext.ForPage(pageKey, code, title);
        _hostedQuickWindowKey = null;
        _hostedQuickWindowDefinition = null;
        QuickWindowWorkspace.ClearActiveContext();
        EditorContextBadgeText.Text = _activeEditorContext.ContextBadge;
        EditorContextTitleText.Text = _activeEditorContext.ContextTitle;
        EditorContextPanel.Visibility = Visibility.Visible;
        RefreshQuickWindowPageSurfaces();
    }

    /// <summary>
    /// Shows `Interface locale` and the `Banc d'essai` for the active page when it is authored as
    /// <see cref="ScadaPageType.QuickWindow"/>, reading its `#N` placeholders and letting the operator
    /// preview it with a parameter sheet (DEC-0052); hides both and restores the project `Catalogue Tags`
    /// otherwise.
    /// </summary>
    /// <remarks>
    /// Controller fix round 1: reads the type from <see cref="_activeSceneTab"/>'s own
    /// <see cref="SceneWorkspaceTab.Page"/> — the same source <see cref="BuildQuickWindowAuthoringContext"/>
    /// uses — never from <c>_modernProject.Scenes</c>. A `page.set-type` result assigns
    /// <c>_modernProject</c> in <see cref="OnPageCommandCompleted"/> only after
    /// <see cref="PageWorkspaceController.ReplaceProject"/> calls <c>tab.UpdatePage</c> and re-activates the
    /// tab; reading <c>_modernProject.Scenes</c> here observed the pre-mutation project and never reflected
    /// a type change into or out of <see cref="ScadaPageType.QuickWindow"/> on the currently active tab.
    /// </remarks>
    private void RefreshQuickWindowPageSurfaces()
    {
        if (_activeSceneTab?.Page.Type == ScadaPageType.QuickWindow && _activeScene is not null)
        {
            ShowLocalInterfacePanelForPage(_activeScene);
            ShowQuickWindowTestBenchForPage(_activeScene);
        }
        else
        {
            HideLocalInterfacePanel();
            HideQuickWindowTestBench();
        }
    }

    /// <summary>
    /// Shows `Interface locale` in place of the project `Catalogue Tags` while a quick-window page is the
    /// active surface. The tag catalogue is hidden rather than left visible and ambiguous: quick-window
    /// content never references a physical project tag.
    /// </summary>
    private void ShowLocalInterfacePanelForPage(ScadaScene page)
    {
        if (!_isQuickWindowInterfacePanelBound)
        {
            QuickWindowInterfacePanelControl.Bind(QuickWindowWorkspace.InterfacePanel);
            _isQuickWindowInterfacePanelBound = true;
        }

        QuickWindowWorkspace.InterfacePanel.LoadPlaceholders(page);
        HideAnchorable(TagCatalogAnchorable);
        ShowAnchorable(QuickWindowInterfaceAnchorable);
    }

    /// <summary>Restores the project `Catalogue Tags` when a page becomes the active surface again.</summary>
    private void HideLocalInterfacePanel()
    {
        HideAnchorable(QuickWindowInterfaceAnchorable);
        ShowAnchorable(TagCatalogAnchorable);
    }

    /// <summary>Binds and shows the test bench of the active quick-window page (DEC-0052).</summary>
    private void ShowQuickWindowTestBenchForPage(ScadaScene page)
    {
        if (!_isQuickWindowTestBenchBound)
        {
            QuickWindowTestBenchControl.SheetSelected += OnQuickWindowTestBenchSheetSelected;
            QuickWindowTestBenchControl.CloseRequested += OnQuickWindowPreviewCloseRequested;
            _isQuickWindowTestBenchBound = true;
        }

        QuickWindowTestBenchControl.Model.LoadPage(page, _modernProject?.EffectiveParameterSheets ?? []);
        ShowAnchorable(QuickWindowTestBenchAnchorable);
    }

    /// <summary>Clears the bench and its preview when a page becomes the active surface again.</summary>
    private void HideQuickWindowTestBench()
    {
        QuickWindowTestBenchControl.HidePreview();
        QuickWindowTestBenchControl.Model.Clear();
        QuickWindowPreviewHost?.Close();
        HideAnchorable(QuickWindowTestBenchAnchorable);
    }

    /// <summary>
    /// Previews the active quick-window page with one parameter sheet's values (DEC-0052): builds a
    /// synthetic "Ouvrir une fenêtre rapide" command pairing the chosen sheet with the active page on an
    /// in-memory snapshot — never written into the project — lowers that snapshot and, when lowering
    /// carries no error, feeds the resulting definition and invocation to the existing preview so it
    /// mounts through <c>mountInstance</c> and the `#N` substitution of Task 7. Lowering diagnostics
    /// (errors and warnings) are always shown in the bench, whether or not the preview opens.
    /// </summary>
    /// <remarks>
    /// Controller fix round 2: only fires for a genuine operator pick — the control's `SheetSelected`
    /// event now relays <see cref="QuickWindowTestBenchViewModel.SheetSelected"/>, which a programmatic
    /// resync (<see cref="QuickWindowTestBenchViewModel.LoadPage"/>, called on every page-property
    /// command since fix round 1) never raises. <see cref="QuickWindowTestBenchViewModel.SelectedSheetKey"/>
    /// is already set by the time this runs, so no redundant <c>SelectSheet</c> call is needed here.
    /// </remarks>
    private async void OnQuickWindowTestBenchSheetSelected(object? sender, Guid sheetKey)
    {
        if (_modernProject is null || _activeSceneTab?.PageKey is not { } pageKey || _activeScene is null)
        {
            QuickWindowTestBenchControl.Model.Status = "Aucune fenetre rapide active.";
            return;
        }

        var sheet = _modernProject.EffectiveParameterSheets.FirstOrDefault(item => item.SheetKey == sheetKey);
        if (sheet is null)
        {
            QuickWindowTestBenchControl.Model.Status = "Feuille de paramètres introuvable.";
            return;
        }

        var lowering = QuickWindowTestBenchLowering.Lower(_modernProject, _activeScene, pageKey, sheet);
        QuickWindowTestBenchControl.Model.SetLoweringDiagnostics(lowering.Diagnostics);
        if (lowering.Definition is null || lowering.Invocation is null)
        {
            QuickWindowTestBenchControl.HidePreview("Aperçu impossible: la feuille ne peut pas être appliquée à cette page (voir diagnostics).");
            return;
        }

        if (QuickWindowPreviewHost is not { } adapter)
        {
            QuickWindowTestBenchControl.Model.Status = "Aucun projet actif.";
            return;
        }

        try
        {
            var session = await adapter.OpenAsync(
                lowering.Definition,
                lowering.Invocation.InvocationKey,
                invocation: lowering.Invocation,
                tagCatalog: _modernProject.TagCatalog);
            QuickWindowTestBenchControl.ShowPreview(session.Source);
            QuickWindowTestBenchControl.Model.Status =
                $"Instance de prévisualisation ouverte avec la feuille '{sheet.DisplayName}' (génération {session.Generation}).";
        }
        catch (Exception ex)
        {
            QuickWindowTestBenchControl.HidePreview($"Aperçu impossible: {ex.Message}");
        }
    }

    private void OnQuickWindowPreviewCloseRequested(object? sender, EventArgs e)
    {
        QuickWindowPreviewHost?.Close();
        QuickWindowTestBenchControl.HidePreview("Instance de prévisualisation fermée.");
    }

    /// <summary>
    /// Builds the bounded quick-window authoring context of the active surface: the definitions that may be
    /// opened, the parent ports a binding may forward and whether `CloseQuickWindow(Self)` is offered.
    /// </summary>
    /// <remarks>
    /// `CloseQuickWindow` is now offered exactly when the active page is authored as type
    /// <see cref="ScadaPageType.QuickWindow"/> - a quick window is a page, not the separate "hosted
    /// definition editing" surface `IsQuickWindowSurfaceHosted` used to gate this on (DEC-0052).
    /// </remarks>
    private QuickWindowCommandAuthoringContext BuildQuickWindowAuthoringContext() => new(
        _modernProject?.EffectiveQuickWindows ?? [],
        _hostedQuickWindowDefinition?.EffectiveInterfaceMembers ?? [],
        _activeSceneTab?.Page.Type == ScadaPageType.QuickWindow,
        invocationKey => _modernProject?.EffectiveQuickWindowInvocations
            .FirstOrDefault(invocation => invocation.InvocationKey == invocationKey));

    /// <summary>
    /// Applies a command edit that migrates an `OpenQuickWindow` command from a legacy
    /// `QuickWindowInvocationKey` to a `ParameterSheetKey`/`QuickWindowPageKey` pair, retiring the
    /// now-orphaned invocation from the project as a single undoable transition (controller fix round 1,
    /// Task 10): <see cref="ApplyQuickWindowMutationAsync"/> pushes one project-scoped history entry whose
    /// undo restores both the caller command and the retired invocation together.
    /// </summary>
    private async Task<ScadaElement> RetireMigratedQuickWindowInvocationAsync(
        string elementId,
        ScadaCommandBinding migratedCommand,
        Guid retiredInvocationKey)
    {
        var fallback = () => _activeScene?.FindElementRecursive(elementId) ?? ScadaElement.CreateInputText(elementId, elementId, 0, 0);
        if (_modernProject is null || _activeSceneTab?.PageKey is not { } pageKey || _activeScene is null)
        {
            return fallback();
        }

        var snapshot = await _pageWorkspaceController.CaptureSnapshotAsync();
        var mutation = QuickWindowWorkspace.RetireMigratedInvocation(snapshot, pageKey, elementId, migratedCommand.Id, migratedCommand, retiredInvocationKey);
        if (mutation.Result.Status != CommandResultStatus.Succeeded)
        {
            SetStatus(mutation.Result.Diagnostics.FirstOrDefault()?.Message ?? mutation.Result.Message);
            return fallback();
        }

        await ApplyQuickWindowMutationAsync(mutation);
        if (mutation.After.Scenes.TryGetValue(pageKey, out var updatedScene))
        {
            _activeScene = updatedScene;
            MarkActiveSceneDirty();
            RefreshModernSceneUi();
        }

        return fallback();
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

            ActivateAnchorable(PageAnchorable);
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
    public Task<bool> ConfirmParameterSheetDeletionAsync(ParameterSheet sheet, IReadOnlyList<ParameterSheetUsage> usages)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(usages);
        var callers = string.Join(
            Environment.NewLine,
            usages.Take(10).Select(usage => $"· {usage.PageCode} / {usage.ElementId} / {usage.CommandId}"));
        var confirmed = MessageBox.Show(
            this,
            $"La feuille de paramètres '{sheet.DisplayName}' est encore référencée par {usages.Count} commande(s) « Ouvrir une fenêtre rapide » :{Environment.NewLine}{callers}{Environment.NewLine}{Environment.NewLine}Supprimer quand même ?",
            "Supprimer une feuille de paramètres référencée",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
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
    public Task<QuickWindowPasteDecision> ResolveQuickWindowPasteAsync(QuickWindowClipboardAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var dialog = new QuickWindowPasteDiagnosticsDialog(analysis) { Owner = this };
        dialog.ShowDialog();
        return Task.FromResult(dialog.Decision);
    }

    /// <summary>
    /// Validates one clipboard payload against the active surface before it is inserted, and returns the
    /// objects the caller may actually paste. A boundary-crossing payload is refused by default.
    /// </summary>
    private async Task<IReadOnlyList<ScadaElement>?> PrepareQuickWindowPasteAsync(IReadOnlyList<ScadaElement> elements)
    {
        var target = _hostedQuickWindowDefinition is { } definition
            ? QuickWindowClipboardTarget.ForQuickWindow(definition)
            : QuickWindowClipboardTarget.ForPage();
        var existingIds = _activeScene is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : _activeScene.Elements.Select(element => element.Id).ToHashSet(StringComparer.Ordinal);
        var plan = await QuickWindowWorkspace.PreparePasteAsync(elements, target, _modernProject, existingIds);
        return plan.IsAllowed ? plan.Elements : null;
    }

    /// <summary>Gets the clipboard origin of the active authoring surface.</summary>
    private SceneClipboardOrigin CurrentClipboardOrigin => _hostedQuickWindowKey is { } definitionKey
        ? SceneClipboardOrigin.ForQuickWindow(definitionKey)
        : SceneClipboardOrigin.ForPage(_activeSceneTab?.PageKey);

    /// <inheritdoc />
    public Task<bool> ConfirmInterfaceVersionImpactAsync(
        QuickWindowDefinition definition,
        IReadOnlyList<QuickWindowOutdatedInvocation> impacted)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(impacted);
        var callers = string.Join(
            Environment.NewLine,
            impacted.Take(10).Select(item => $"· {item.OwnerElementId ?? "(non rattachee)"} / {item.OwnerCommandId ?? "-"}"));
        var confirmed = MessageBox.Show(
            this,
            $"Cette modification d'interface laissera {impacted.Count} invocation(s) a reparer:{Environment.NewLine}{callers}{Environment.NewLine}{Environment.NewLine}Build et export resteront bloques jusqu'a leur reparation. Continuer ?",
            "Invocations bientot Outdated",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
        return Task.FromResult(confirmed);
    }

    /// <summary>
    /// Opens the repair surface of the active definition: outdated invocations with their caller and reason,
    /// navigation to that caller and explicit port-by-port relinking (FR-UI-24).
    /// </summary>
    private async void OnQuickWindowRepairInvocationsRequested(object? sender, EventArgs e)
    {
        if (_modernProject is null || _hostedQuickWindowKey is not { } definitionKey)
        {
            SetStatus("Aucune fenetre rapide active.");
            return;
        }

        var snapshot = await _pageWorkspaceController.CaptureSnapshotAsync();
        var definition = snapshot.Project.EffectiveQuickWindows.FirstOrDefault(item => item.DefinitionKey == definitionKey);
        if (definition is null) return;

        var outdated = QuickWindowWorkspace.ListOutdatedInvocations(snapshot, definitionKey);
        if (outdated.Count == 0)
        {
            SetStatus("Aucune invocation a reparer pour cette fenetre rapide.");
            return;
        }

        var rows = outdated
            .Select(item => new QuickWindowRepairRowViewModel(item, ResolvePageLabel(snapshot, item.OwnerPageKey)))
            .ToArray();
        var dialog = new QuickWindowInvocationRepairDialog(
            definition,
            rows,
            snapshot.Project.EffectiveQuickWindowInvocations,
            ActiveSelectorTagCatalog)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true || dialog.SelectedInvocationKey is not { } invocationKey) return;

        if (dialog.NavigationRequested)
        {
            if (QuickWindowWorkspace.NavigateToInvocation(snapshot, invocationKey) is { } navigation)
            {
                NavigateToQuickWindowUsage(navigation);
            }

            return;
        }

        var mutation = QuickWindowWorkspace.RepairInvocation(snapshot, invocationKey, dialog.RepairedBindings);
        if (mutation.Result.Status != CommandResultStatus.Succeeded) return;
        await ApplyQuickWindowMutationAsync(mutation);
        if (mutation.After.Scenes.TryGetValue(mutation.After.Project.EffectiveQuickWindowInvocations
                .First(item => item.InvocationKey == invocationKey).OwnerPageKey ?? Guid.Empty, out var updatedScene) &&
            _activeSceneTab?.PageKey == updatedScene.PageKey)
        {
            _activeScene = updatedScene;
            MarkActiveSceneDirty();
            RefreshModernSceneUi();
        }
    }

    private static string ResolvePageLabel(PageWorkspaceSnapshot snapshot, Guid? pageKey) =>
        pageKey is { } key
            ? snapshot.Project.Scenes.FirstOrDefault(page => page.PageKey == key)?.EffectivePageCode ?? key.ToString("N")[..8]
            : "(non rattachee)";

    /// <inheritdoc />
    public void ReportQuickWindowStatus(string message) => SetStatus(message);
}
