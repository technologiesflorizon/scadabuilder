using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>Operator decision on a paste that crosses the page ↔ quick-window boundary (FR-UI-23).</summary>
public enum QuickWindowPasteDecision
{
    /// <summary>Nothing is pasted.</summary>
    Cancel = 0,

    /// <summary>Every refused reference is removed and the properties stay unbound.</summary>
    PasteWithoutBindings = 1
}

/// <summary>One prepared paste after the fail-closed boundary analysis.</summary>
/// <param name="Elements">The objects to insert, already stripped when the operator accepted it.</param>
/// <param name="Analysis">The refused references found by the validator.</param>
/// <param name="Decision">What the operator chose when the analysis refused the payload.</param>
public sealed record QuickWindowPastePlan(
    IReadOnlyList<ScadaElement> Elements,
    QuickWindowClipboardAnalysis Analysis,
    QuickWindowPasteDecision Decision)
{
    /// <summary>Gets whether the caller may insert <see cref="Elements"/>.</summary>
    public bool IsAllowed => Decision != QuickWindowPasteDecision.Cancel;

    /// <summary>Gets whether references were removed to make the paste acceptable.</summary>
    public bool WasStripped => IsAllowed && !Analysis.IsAllowed;
}

/// <summary>Visual callback boundary between the quick-window workspace and the WPF shell.</summary>
/// <remarks>
/// Decisions: DEC-0050, FR-UI-12, FR-UI-14, FR-UI-25.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowShellContractTests.cs.
/// </remarks>
public interface IQuickWindowWorkspaceHost
{
    /// <summary>Shows one quick-window definition in the shared editor with its bounded context.</summary>
    Task ActivateQuickWindowAsync(QuickWindowEditorContext context, QuickWindowDefinition definition);

    /// <summary>Asks the operator to name a new or duplicated definition; null cancels.</summary>
    Task<string?> RequestQuickWindowNameAsync(string title, string proposedName);

    /// <summary>Asks the operator to confirm a referential deletion, listing its callers.</summary>
    Task<bool> ConfirmQuickWindowDeletionAsync(QuickWindowDefinition definition, IReadOnlyList<QuickWindowUsage> usages);

    /// <summary>
    /// Asks the operator to confirm deleting one parameter sheet that is still referenced by at least one
    /// `Ouvrir une fenêtre rapide` command, listing every caller (DEC-0052).
    /// </summary>
    Task<bool> ConfirmParameterSheetDeletionAsync(ParameterSheet sheet, IReadOnlyList<ParameterSheetUsage> usages);

    /// <summary>
    /// Opens the shared member dialog on one draft and returns the authored member; null cancels.
    /// The same dialog serves creation and advanced edition (FR-UI-16).
    /// </summary>
    Task<QuickWindowInterfaceMember?> RequestInterfaceMemberAsync(
        string title,
        QuickWindowInterfaceMemberDraft draft,
        IReadOnlyList<QuickWindowInterfaceMember> siblings);

    /// <summary>Asks the operator to confirm the removal of a member that is still referenced (FR-UI-17).</summary>
    Task<bool> ConfirmInterfaceMemberDeletionAsync(
        QuickWindowInterfaceMember member,
        IReadOnlyList<QuickWindowUsage> usages);

    /// <summary>
    /// Shows the refused references of a boundary-crossing paste and returns the operator decision.
    /// Only `Annuler` and `Coller sans liaisons` may be offered (FR-UI-23).
    /// </summary>
    Task<QuickWindowPasteDecision> ResolveQuickWindowPasteAsync(QuickWindowClipboardAnalysis analysis);

    /// <summary>
    /// Asks the operator to confirm an interface change that will leave invocations `Outdated`, showing how
    /// many are impacted before anything is applied (FR-UI-24).
    /// </summary>
    Task<bool> ConfirmInterfaceVersionImpactAsync(
        QuickWindowDefinition definition,
        IReadOnlyList<QuickWindowOutdatedInvocation> impacted);

    /// <summary>Reports one workspace status message.</summary>
    void ReportQuickWindowStatus(string message);
}

/// <summary>
/// Owns the quick-window group of the project: inventory, creation, duplication, rename, selection,
/// opening in the shared editor and fail-closed deletion. No WPF type and no file I/O.
/// </summary>
/// <remarks>
/// The shell keeps only the wiring: every quick-window decision belongs here or to the Application
/// services it delegates to. Mutations are prepared as immutable workspace snapshots and applied by the
/// caller through the existing page workspace pipeline, so undo/redo stays on the single stack.
///
/// Decisions: DEC-0050, FR-001, FR-033, FR-035, FR-UI-12, FR-UI-13, FR-UI-22, FR-UI-25.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §9.1.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowShellContractTests.cs.
/// </remarks>
public sealed class QuickWindowWorkspaceController(
    IQuickWindowWorkspaceHost host,
    QuickWindowDefinitionService? definitionService = null,
    QuickWindowDependencyAnalyzer? dependencyAnalyzer = null,
    QuickWindowInvocationService? invocationService = null)
{
    private readonly QuickWindowDefinitionService definitions = definitionService ?? new QuickWindowDefinitionService();
    private readonly QuickWindowDependencyAnalyzer analyzer = dependencyAnalyzer ?? new QuickWindowDependencyAnalyzer();
    private readonly QuickWindowInvocationService invocations = invocationService ?? new QuickWindowInvocationService();

    /// <summary>Gets the searchable inventory bound to the project group.</summary>
    public QuickWindowsPanelViewModel Panel { get; } = new();

    /// <summary>Gets the parameter-sheet inventory listed by the "Fenêtres rapides" panel (DEC-0052).</summary>
    public ParameterSheetsPanelViewModel SheetsPanel { get; } = new();

    /// <summary>Gets the `Interface locale` panel of the active definition, replacing the project tag catalogue.</summary>
    public QuickWindowInterfacePanelViewModel InterfacePanel { get; } = new();

    /// <summary>Gets the editor context of the active quick window, or null when a page is active.</summary>
    public QuickWindowEditorContext? ActiveContext { get; private set; }

    /// <summary>Reloads the inventory, usage counters and outdated counters from one workspace snapshot.</summary>
    public void Load(PageWorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var all = snapshot.Project.EffectiveQuickWindows;
        var analysis = analyzer.Analyze(snapshot);
        var usages = all.ToDictionary(
            definition => definition.DefinitionKey,
            definition => analysis.GetInbound(definition.DefinitionKey).Count);
        var outdated = all.ToDictionary(
            definition => definition.DefinitionKey,
            definition => definitions.ListOutdatedInvocations(snapshot, definition.DefinitionKey).Count);
        Panel.Load(all, usages, outdated);
        SheetsPanel.Load(snapshot.Project.EffectiveParameterSheets);
    }

    /// <summary>Creates one empty definition after asking for its name, then opens it.</summary>
    public async Task<QuickWindowWorkspaceMutation?> CreateAsync(PageWorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var name = await host.RequestQuickWindowNameAsync("Nouvelle fenêtre rapide", ProposeCode(snapshot, "fenetre"));
        if (string.IsNullOrWhiteSpace(name)) return null;

        var code = ProposeCode(snapshot, PageCodePolicy.DeriveCodeFromDisplayName(name));
        var mutation = definitions.CreateEmpty(snapshot, code, name);
        if (mutation.Result.Status != CommandResultStatus.Succeeded)
        {
            host.ReportQuickWindowStatus(FormatRefusal("Création de la fenêtre rapide refusée", mutation));
            return mutation;
        }

        host.ReportQuickWindowStatus($"Fenêtre rapide '{name}' créée.");
        return mutation;
    }

    /// <summary>
    /// Duplicates one definition into an independent one: new key, unique derived code, full content copy,
    /// no invocation carried over and no shared reference (FR-033).
    /// </summary>
    public async Task<QuickWindowWorkspaceMutation?> DuplicateAsync(PageWorkspaceSnapshot snapshot, Guid definitionKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var source = Find(snapshot, definitionKey);
        if (source is null) return null;

        var proposed = ProposeCode(snapshot, source.EffectiveCode);
        var name = await host.RequestQuickWindowNameAsync("Dupliquer la fenêtre rapide", proposed);
        if (string.IsNullOrWhiteSpace(name)) return null;

        var code = ProposeCode(snapshot, PageCodePolicy.DeriveCodeFromDisplayName(name));
        var created = definitions.CreateEmpty(snapshot, code, name);
        if (created.Result.Status != CommandResultStatus.Succeeded)
        {
            host.ReportQuickWindowStatus(FormatRefusal("Duplication refusée", created));
            return created;
        }

        var copy = created.After.Project.EffectiveQuickWindows.Single(item => !snapshot.Project.EffectiveQuickWindows.Any(existing => existing.DefinitionKey == item.DefinitionKey));
        var filled = copy with
        {
            InterfaceVersion = source.InterfaceVersion,
            Content = source.EffectiveContent,
            InterfaceMembers = source.EffectiveInterfaceMembers,
            PresentationDefaults = source.PresentationDefaults
        };
        var mutation = definitions.Update(created.After, filled);
        if (mutation.Result.Status != CommandResultStatus.Succeeded)
        {
            host.ReportQuickWindowStatus(FormatRefusal("Duplication refusée", mutation));
            return mutation;
        }

        host.ReportQuickWindowStatus($"Fenêtre rapide '{source.DisplayName}' dupliquée en '{name}'.");
        return mutation with { Before = snapshot };
    }

    /// <summary>Renames one definition without changing its durable key.</summary>
    public async Task<QuickWindowWorkspaceMutation?> RenameAsync(PageWorkspaceSnapshot snapshot, Guid definitionKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var definition = Find(snapshot, definitionKey);
        if (definition is null) return null;

        var name = await host.RequestQuickWindowNameAsync("Renommer la fenêtre rapide", definition.DisplayName);
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, definition.DisplayName, StringComparison.Ordinal)) return null;

        var mutation = definitions.Update(snapshot, definition with { DisplayName = name });
        host.ReportQuickWindowStatus(mutation.Result.Status == CommandResultStatus.Succeeded
            ? $"Fenêtre rapide renommée en '{name}'."
            : FormatRefusal("Renommage refusé", mutation));
        return mutation;
    }

    /// <summary>Deletes one definition; any caller blocks the operation and is listed for navigation.</summary>
    public async Task<QuickWindowWorkspaceMutation?> DeleteAsync(PageWorkspaceSnapshot snapshot, Guid definitionKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var definition = Find(snapshot, definitionKey);
        if (definition is null) return null;

        var usages = definitions.ListUsages(snapshot, definitionKey);
        if (!await host.ConfirmQuickWindowDeletionAsync(definition, usages)) return null;

        var mutation = definitions.Delete(snapshot, definitionKey);
        host.ReportQuickWindowStatus(mutation.Result.Status == CommandResultStatus.Succeeded
            ? $"Fenêtre rapide '{definition.DisplayName}' supprimée."
            : "Suppression refusée : la définition est encore référencée.");
        return mutation;
    }

    /// <summary>Creates one empty parameter sheet after asking for its name (DEC-0052).</summary>
    /// <remarks>
    /// Mirrors <see cref="CreateAsync"/>: a free, human-readable name is always accepted, and the portable
    /// technical <see cref="ParameterSheet.Code"/> is derived from it and made unique among sheets, never
    /// re-typed by the operator.
    /// </remarks>
    public Task<QuickWindowWorkspaceMutation?> CreateSheetAsync(PageWorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return CreateSheetAsync(snapshot.Project, () => Task.FromResult(snapshot));
    }

    /// <summary>
    /// Creates one empty parameter sheet, asking for its name BEFORE the workspace snapshot is captured.
    /// </summary>
    /// <param name="currentProject">The in-memory project, used only to propose a name.</param>
    /// <param name="captureSnapshot">Captures the coherent workspace snapshot once the operator confirmed; never called on cancel.</param>
    /// <remarks>
    /// Capturing a snapshot reads every non-open scene of the project, so asking first makes the dialog appear
    /// at once and a cancelled dialog costs nothing. The mutation's <c>Before</c> is the snapshot captured after
    /// the dialog, so the operation stays one undoable transition.
    /// Decisions: DEC-0052.
    /// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetAskBeforeCaptureTests.cs.
    /// </remarks>
    public async Task<QuickWindowWorkspaceMutation?> CreateSheetAsync(
        ScadaProject currentProject,
        Func<Task<PageWorkspaceSnapshot>> captureSnapshot)
    {
        ArgumentNullException.ThrowIfNull(currentProject);
        ArgumentNullException.ThrowIfNull(captureSnapshot);
        var name = await host.RequestQuickWindowNameAsync("Nouvelle feuille de paramètres", ProposeSheetCode(currentProject, "feuille"));
        if (string.IsNullOrWhiteSpace(name)) return null;

        var snapshot = await captureSnapshot();
        var code = ProposeSheetCode(snapshot.Project, PageCodePolicy.DeriveCodeFromDisplayName(name));
        var validation = PageCodePolicy.Validate(code, snapshot.Project.EffectiveParameterSheets.Select(sheet => sheet.Code));
        if (!validation.IsValid)
        {
            var blocked = BlockedSheetMutation(snapshot, "create parameter sheet", validation.Errors);
            host.ReportQuickWindowStatus(FormatRefusal("Création de la feuille de paramètres refusée", blocked));
            return blocked;
        }

        var sheet = new ParameterSheet(Guid.NewGuid(), code, name, string.Empty);
        var sheets = snapshot.Project.EffectiveParameterSheets.Append(sheet).ToArray();
        var after = snapshot with { Version = snapshot.Version + 1, Project = snapshot.Project with { ParameterSheets = sheets } };
        var mutation = new QuickWindowWorkspaceMutation(
            snapshot,
            after,
            CommandResult.Success("Feuille de paramètres créée.", workspaceDirty: true),
            "create parameter sheet",
            sheet.SheetKey);
        host.ReportQuickWindowStatus($"Feuille de paramètres '{name}' créée.");
        return mutation;
    }

    /// <summary>Renames one sheet without changing its durable key nor its technical code.</summary>
    public Task<QuickWindowWorkspaceMutation?> RenameSheetAsync(PageWorkspaceSnapshot snapshot, Guid sheetKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return RenameSheetAsync(snapshot.Project, sheetKey, () => Task.FromResult(snapshot));
    }

    /// <summary>
    /// Renames one sheet, asking for the new name BEFORE the workspace snapshot is captured (see
    /// <see cref="CreateSheetAsync(ScadaProject, Func{Task{PageWorkspaceSnapshot}})"/>).
    /// </summary>
    public async Task<QuickWindowWorkspaceMutation?> RenameSheetAsync(
        ScadaProject currentProject,
        Guid sheetKey,
        Func<Task<PageWorkspaceSnapshot>> captureSnapshot)
    {
        ArgumentNullException.ThrowIfNull(currentProject);
        ArgumentNullException.ThrowIfNull(captureSnapshot);
        var current = FindSheet(currentProject, sheetKey);
        if (current is null) return null;

        var name = await host.RequestQuickWindowNameAsync("Renommer la feuille de paramètres", current.DisplayName);
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, current.DisplayName, StringComparison.Ordinal)) return null;

        var snapshot = await captureSnapshot();
        var sheet = FindSheet(snapshot.Project, sheetKey);
        if (sheet is null) return null;

        var mutation = UpdateSheet(snapshot, sheet with { DisplayName = name }, "rename parameter sheet");
        host.ReportQuickWindowStatus($"Feuille de paramètres renommée en '{name}'.");
        return mutation;
    }

    /// <summary>Duplicates one sheet into an independent one: new key, unique derived code, full text copy.</summary>
    public Task<QuickWindowWorkspaceMutation?> DuplicateSheetAsync(PageWorkspaceSnapshot snapshot, Guid sheetKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return DuplicateSheetAsync(snapshot.Project, sheetKey, () => Task.FromResult(snapshot));
    }

    /// <summary>
    /// Duplicates one sheet, asking for the copy's name BEFORE the workspace snapshot is captured (see
    /// <see cref="CreateSheetAsync(ScadaProject, Func{Task{PageWorkspaceSnapshot}})"/>).
    /// </summary>
    public async Task<QuickWindowWorkspaceMutation?> DuplicateSheetAsync(
        ScadaProject currentProject,
        Guid sheetKey,
        Func<Task<PageWorkspaceSnapshot>> captureSnapshot)
    {
        ArgumentNullException.ThrowIfNull(currentProject);
        ArgumentNullException.ThrowIfNull(captureSnapshot);
        var current = FindSheet(currentProject, sheetKey);
        if (current is null) return null;

        var proposed = ProposeSheetCode(currentProject, current.Code);
        var name = await host.RequestQuickWindowNameAsync("Dupliquer la feuille de paramètres", proposed);
        if (string.IsNullOrWhiteSpace(name)) return null;

        var snapshot = await captureSnapshot();
        var source = FindSheet(snapshot.Project, sheetKey);
        if (source is null) return null;

        var code = ProposeSheetCode(snapshot.Project, PageCodePolicy.DeriveCodeFromDisplayName(name));
        var validation = PageCodePolicy.Validate(code, snapshot.Project.EffectiveParameterSheets.Select(sheet => sheet.Code));
        if (!validation.IsValid)
        {
            var blocked = BlockedSheetMutation(snapshot, "duplicate parameter sheet", validation.Errors);
            host.ReportQuickWindowStatus(FormatRefusal("Duplication de la feuille de paramètres refusée", blocked));
            return blocked;
        }

        var copy = new ParameterSheet(Guid.NewGuid(), code, name, source.Text);
        var sheets = snapshot.Project.EffectiveParameterSheets.Append(copy).ToArray();
        var after = snapshot with { Version = snapshot.Version + 1, Project = snapshot.Project with { ParameterSheets = sheets } };
        var mutation = new QuickWindowWorkspaceMutation(
            snapshot,
            after,
            CommandResult.Success("Feuille de paramètres dupliquée.", workspaceDirty: true),
            "duplicate parameter sheet",
            copy.SheetKey);
        host.ReportQuickWindowStatus($"Feuille de paramètres '{source.DisplayName}' dupliquée en '{name}'.");
        return mutation;
    }

    /// <summary>Replaces one sheet's text as a single undoable transition, applied by the sheet editor's "Enregistrer".</summary>
    public Task<QuickWindowWorkspaceMutation?> UpdateSheetTextAsync(PageWorkspaceSnapshot snapshot, Guid sheetKey, string text)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var sheet = FindSheet(snapshot, sheetKey);
        if (sheet is null) return Task.FromResult<QuickWindowWorkspaceMutation?>(null);

        var normalized = text ?? string.Empty;
        if (string.Equals(normalized, sheet.Text, StringComparison.Ordinal))
            return Task.FromResult<QuickWindowWorkspaceMutation?>(null);

        var mutation = UpdateSheet(snapshot, sheet with { Text = normalized }, "edit parameter sheet");
        host.ReportQuickWindowStatus($"Feuille de paramètres '{sheet.DisplayName}' enregistrée.");
        return Task.FromResult<QuickWindowWorkspaceMutation?>(mutation);
    }

    /// <summary>
    /// Deletes one parameter sheet. A sheet still referenced by at least one `Ouvrir une fenêtre rapide`
    /// command asks the operator to confirm first, listing every caller — otherwise the deletion only
    /// surfaces as `parameter-sheet.sheet-missing` at the next validation or export, long after undo has
    /// stopped covering it. A sheet with no caller deletes directly, exactly as before (fix round 1).
    /// </summary>
    public async Task<QuickWindowWorkspaceMutation?> DeleteSheetAsync(PageWorkspaceSnapshot snapshot, Guid sheetKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var sheet = FindSheet(snapshot, sheetKey);
        if (sheet is null) return null;

        var usages = ParameterSheetUsageFinder.Find(snapshot, sheetKey);
        if (usages.Count > 0 && !await host.ConfirmParameterSheetDeletionAsync(sheet, usages))
        {
            host.ReportQuickWindowStatus($"Suppression de la feuille de paramètres '{sheet.DisplayName}' annulée.");
            return null;
        }

        var sheets = snapshot.Project.EffectiveParameterSheets.Where(item => item.SheetKey != sheetKey).ToArray();
        var after = snapshot with { Version = snapshot.Version + 1, Project = snapshot.Project with { ParameterSheets = sheets } };
        var mutation = new QuickWindowWorkspaceMutation(
            snapshot,
            after,
            CommandResult.Success("Feuille de paramètres supprimée.", workspaceDirty: true),
            "delete parameter sheet",
            sheetKey);
        host.ReportQuickWindowStatus($"Feuille de paramètres '{sheet.DisplayName}' supprimée.");
        return mutation;
    }

    /// <summary>Opens one definition in the shared editor and publishes its bounded context.</summary>
    public async Task OpenAsync(PageWorkspaceSnapshot snapshot, Guid definitionKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var definition = Find(snapshot, definitionKey);
        if (definition is null) return;

        ActiveContext = QuickWindowEditorContext.ForQuickWindow(definition.DefinitionKey, definition.EffectiveCode, definition.DisplayName);
        LoadInterface(snapshot, definitionKey);
        await host.ActivateQuickWindowAsync(ActiveContext, definition);
    }

    /// <summary>Clears the quick-window context when a page becomes the active surface again.</summary>
    public void ClearActiveContext()
    {
        ActiveContext = null;
        InterfacePanel.Clear();
    }

    /// <summary>Reloads the `Interface locale` panel of one definition with its per-member usage counters.</summary>
    public void LoadInterface(PageWorkspaceSnapshot snapshot, Guid definitionKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var definition = Find(snapshot, definitionKey);
        if (definition is null)
        {
            InterfacePanel.Clear();
            return;
        }

        var analysis = analyzer.Analyze(snapshot);
        var usages = definition.EffectiveInterfaceMembers.ToDictionary(
            member => member.MemberKey,
            member => analysis.GetMemberUsages(definitionKey, member.MemberKey));
        InterfacePanel.Load(definition, usages);
    }

    /// <summary>Adds one member authored through the shared dialog.</summary>
    public async Task<QuickWindowWorkspaceMutation?> AddInterfaceMemberAsync(PageWorkspaceSnapshot snapshot, Guid definitionKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var definition = Find(snapshot, definitionKey);
        if (definition is null) return null;

        var draft = QuickWindowInterfaceMemberDraft.ForNew(ProposeMemberName(definition));
        var member = await host.RequestInterfaceMemberAsync(
            "Nouveau membre d'interface",
            draft,
            definition.EffectiveInterfaceMembers);
        if (member is null) return null;

        return await ApplyMembersAsync(
            snapshot,
            definition,
            definition.EffectiveInterfaceMembers.Append(member).ToArray(),
            $"Membre '{member.Name}' ajouté.");
    }

    /// <summary>Edits the advanced properties of one member through the shared dialog (FR-UI-16).</summary>
    public async Task<QuickWindowWorkspaceMutation?> EditInterfaceMemberAsync(
        PageWorkspaceSnapshot snapshot,
        Guid definitionKey,
        Guid memberKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var definition = Find(snapshot, definitionKey);
        var current = definition?.EffectiveInterfaceMembers.FirstOrDefault(item => item.MemberKey == memberKey);
        if (definition is null || current is null) return null;

        var member = await host.RequestInterfaceMemberAsync(
            "Propriétés du membre",
            QuickWindowInterfaceMemberDraft.From(current),
            definition.EffectiveInterfaceMembers);
        if (member is null) return null;

        return await ApplyMembersAsync(
            snapshot,
            definition,
            Replace(definition, member),
            $"Membre '{member.Name}' mis à jour.");
    }

    /// <summary>Applies one inline table edit of a common member property (FR-UI-16).</summary>
    public async Task<QuickWindowWorkspaceMutation?> ApplyInlineInterfaceEditAsync(
        PageWorkspaceSnapshot snapshot,
        Guid definitionKey,
        QuickWindowInterfaceMember member)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(member);
        var definition = Find(snapshot, definitionKey);
        var current = definition?.EffectiveInterfaceMembers.FirstOrDefault(item => item.MemberKey == member.MemberKey);
        if (definition is null || current is null || current == member) return null;

        var issues = QuickWindowValidation.ValidateMember(member, definition.EffectiveInterfaceMembers);
        if (issues.Count > 0)
        {
            host.ReportQuickWindowStatus($"Modification refusée : {issues[0]}");
            return null;
        }

        return await ApplyMembersAsync(
            snapshot,
            definition,
            Replace(definition, member),
            $"Membre '{member.Name}' mis à jour.");
    }

    /// <summary>Deletes one member; a referenced member requires an explicit confirmation (FR-UI-17).</summary>
    public async Task<QuickWindowWorkspaceMutation?> DeleteInterfaceMemberAsync(
        PageWorkspaceSnapshot snapshot,
        Guid definitionKey,
        Guid memberKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var definition = Find(snapshot, definitionKey);
        var member = definition?.EffectiveInterfaceMembers.FirstOrDefault(item => item.MemberKey == memberKey);
        if (definition is null || member is null) return null;

        var usages = ListMemberUsages(snapshot, definitionKey, memberKey);
        if (usages.Count > 0 && !await host.ConfirmInterfaceMemberDeletionAsync(member, usages))
        {
            host.ReportQuickWindowStatus($"Suppression du membre '{member.Name}' annulée.");
            return null;
        }

        return await ApplyMembersAsync(
            snapshot,
            definition,
            definition.EffectiveInterfaceMembers.Where(item => item.MemberKey != memberKey).ToArray(),
            $"Membre '{member.Name}' supprimé.",
            confirmReferencedMemberRemoval: usages.Count > 0);
    }

    /// <summary>
    /// Saves one typed invocation authored by the `Liaisons` tab, together with its caller command, as a
    /// single prepared transition. Incomplete bindings stay saveable and are returned as diagnostics; a
    /// broken owner or definition reference blocks the mutation.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0050, FR-009, FR-010, FR-UI-18, FR-UI-19.
    /// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowBindingAuthoringTests.cs.
    /// </remarks>
    public QuickWindowWorkspaceMutation SaveInvocation(
        PageWorkspaceSnapshot snapshot,
        Guid ownerPageKey,
        string ownerElementId,
        QuickWindowInvocationAuthoringRequest request)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);
        var mutation = invocations.Upsert(snapshot, new UpsertQuickWindowInvocationRequest(
            ownerPageKey,
            ownerElementId,
            request.CommandId,
            request.DefinitionKey,
            request.Bindings ?? [],
            request.InvocationKey,
            request.TitleOverride));
        host.ReportQuickWindowStatus(mutation.Result.Status == CommandResultStatus.Succeeded
            ? "Liaisons de la fenêtre rapide enregistrées."
            : $"Enregistrement des liaisons refusé : {mutation.Result.Message}");
        return mutation;
    }

    /// <summary>
    /// Applies a command edit that migrates an `OpenQuickWindow` command from a legacy
    /// `QuickWindowInvocationKey` to a `ParameterSheetKey`/`QuickWindowPageKey` pair, retiring the
    /// now-orphaned invocation from the project in the same transition (controller fix round 1, Task 10).
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0050, DEC-0052.
    /// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInvocationMigrationTests.cs.
    /// </remarks>
    public QuickWindowWorkspaceMutation RetireMigratedInvocation(
        PageWorkspaceSnapshot snapshot,
        Guid ownerPageKey,
        string ownerElementId,
        string ownerCommandId,
        ScadaCommandBinding migratedCommand,
        Guid retiredInvocationKey)
    {
        var mutation = invocations.RetireMigratedInvocation(
            snapshot, ownerPageKey, ownerElementId, ownerCommandId, migratedCommand, retiredInvocationKey);
        host.ReportQuickWindowStatus(mutation.Result.Status == CommandResultStatus.Succeeded
            ? "Commande migrée vers une feuille de paramètres ; l'ancienne liaison a été retirée."
            : $"Migration de la commande refusée : {mutation.Result.Message}");
        return mutation;
    }

    /// <summary>Removes one invocation and clears exactly the caller command that owned it.</summary>
    public QuickWindowWorkspaceMutation RemoveInvocation(PageWorkspaceSnapshot snapshot, Guid invocationKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return invocations.RemoveInvocation(snapshot, invocationKey);
    }

    /// <summary>Deletes one caller element and every invocation owned by its command subtree.</summary>
    public QuickWindowWorkspaceMutation DeleteCaller(PageWorkspaceSnapshot snapshot, Guid pageKey, string elementId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return invocations.DeleteCaller(snapshot, pageKey, elementId);
    }

    /// <summary>
    /// Validates one paste, duplication or library instantiation against its target context and returns the
    /// prepared plan. A payload crossing the page ↔ quick-window boundary is refused by default; the operator
    /// may only cancel or accept the `Coller sans liaisons` variant (FR-031, FR-034, FR-UI-23).
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0050, FR-031, FR-034, FR-UI-23.
    /// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowClipboardTests.cs.
    /// </remarks>
    public async Task<QuickWindowPastePlan> PreparePasteAsync(
        IReadOnlyList<ScadaElement> elements,
        QuickWindowClipboardTarget target,
        ScadaProject? project,
        IReadOnlySet<string>? targetElementIds = null)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(target);
        var analysis = QuickWindowClipboardValidator.Analyze(elements, target, project, targetElementIds);
        if (analysis.IsAllowed)
            return new QuickWindowPastePlan(elements, analysis, QuickWindowPasteDecision.PasteWithoutBindings);

        var decision = await host.ResolveQuickWindowPasteAsync(analysis);
        if (decision == QuickWindowPasteDecision.Cancel)
        {
            host.ReportQuickWindowStatus($"Collage annulé : {analysis.Issues.Count} référence(s) non résoluble(s) dans ce contexte.");
            return new QuickWindowPastePlan([], analysis, QuickWindowPasteDecision.Cancel);
        }

        var stripped = QuickWindowClipboardValidator.StripRefusedReferences(elements, analysis);
        host.ReportQuickWindowStatus($"Collé sans liaisons : {analysis.Issues.Count} référence(s) retirée(s), propriétés laissées Non lié.");
        return new QuickWindowPastePlan(stripped, analysis, decision);
    }

    /// <summary>Lists every invocation binding that references one interface member (FR-UI-17).</summary>
    public IReadOnlyList<QuickWindowUsage> ListMemberUsages(PageWorkspaceSnapshot snapshot, Guid definitionKey, Guid memberKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return analyzer.Analyze(snapshot).GetMemberUsages(definitionKey, memberKey);
    }

    /// <summary>Produces a non-mutating navigation result for one member usage (FR-UI-17).</summary>
    public QuickWindowWorkspaceMutation? NavigateToMemberUsage(
        PageWorkspaceSnapshot snapshot,
        Guid definitionKey,
        Guid memberKey,
        int usageIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var usages = ListMemberUsages(snapshot, definitionKey, memberKey);
        if (usageIndex < 0 || usageIndex >= usages.Count)
        {
            host.ReportQuickWindowStatus("Cette utilisation n'existe plus.");
            return null;
        }

        var usage = usages[usageIndex];
        var result = CommandResult.NoChange(
            "Utilisation du membre sélectionnée.",
            pageToSelectKey: usage.OwnerPageKey,
            pageToOpenKey: usage.OwnerPageKey);
        return new QuickWindowWorkspaceMutation(
            snapshot,
            snapshot,
            result,
            "navigate to quick-window member usage",
            definitionKey,
            usage.InvocationKey,
            usage);
    }

    /// <summary>Lists the callers of one definition for the usage navigation surface.</summary>
    public IReadOnlyList<QuickWindowUsage> ListUsages(PageWorkspaceSnapshot snapshot, Guid definitionKey) =>
        definitions.ListUsages(snapshot, definitionKey);

    /// <summary>
    /// Returns the invocations that the candidate interface would leave `Outdated`, before anything is
    /// applied, so the operator can be shown their exact count (FR-UI-24).
    /// </summary>
    public static IReadOnlyList<QuickWindowOutdatedInvocation> PreviewOutdatedImpact(
        PageWorkspaceSnapshot snapshot,
        QuickWindowDefinition current,
        QuickWindowDefinition candidate)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candidate);
        return snapshot.Project.EffectiveQuickWindowInvocations
            .Where(invocation => invocation.DefinitionKey == candidate.DefinitionKey)
            .Select(invocation => (invocation, reasons: QuickWindowInterfaceCompatibility.BreakingReasonsFor(current, candidate, invocation)))
            .Where(entry => entry.reasons.Count > 0)
            .Select(entry => new QuickWindowOutdatedInvocation(
                entry.invocation.InvocationKey,
                candidate.DefinitionKey,
                candidate.InterfaceVersion,
                entry.invocation.InterfaceVersion,
                entry.invocation.OwnerPageKey,
                entry.invocation.OwnerElementId,
                entry.invocation.OwnerCommandId,
                entry.reasons))
            .ToArray();
    }

    /// <summary>
    /// Prepares one interface transition: the local interface version is incremented only when the public
    /// contract actually changed, so a private edit or a rename never invalidates a persisted invocation.
    /// </summary>
    private async Task<QuickWindowWorkspaceMutation?> ApplyMembersAsync(
        PageWorkspaceSnapshot snapshot,
        QuickWindowDefinition current,
        IReadOnlyList<QuickWindowInterfaceMember> members,
        string successMessage,
        bool confirmReferencedMemberRemoval = false)
    {
        var candidate = current with { InterfaceMembers = members };
        if (QuickWindowInterfaceCompatibility.Classify(current, candidate).RequiresVersionIncrement)
        {
            candidate = candidate with { InterfaceVersion = current.InterfaceVersion + 1 };
        }

        // No interface change silently outdates an invocation: the impacted count is shown first (FR-UI-24).
        var impacted = PreviewOutdatedImpact(snapshot, current, candidate);
        if (impacted.Count > 0 && !await host.ConfirmInterfaceVersionImpactAsync(current, impacted))
        {
            host.ReportQuickWindowStatus($"Modification annulée : {impacted.Count} invocation(s) seraient à réparer.");
            return null;
        }

        var mutation = definitions.Update(snapshot, candidate, confirmReferencedMemberRemoval);
        if (mutation.Result.Status != CommandResultStatus.Succeeded)
        {
            var reason = mutation.Result.Diagnostics.FirstOrDefault()?.Message;
            host.ReportQuickWindowStatus(string.IsNullOrWhiteSpace(reason)
                ? "Modification de l'interface locale refusée."
                : $"Modification de l'interface locale refusée : {reason}");
            return mutation;
        }

        LoadInterface(mutation.After, candidate.DefinitionKey);
        host.ReportQuickWindowStatus(successMessage);
        return mutation;
    }

    private static IReadOnlyList<QuickWindowInterfaceMember> Replace(
        QuickWindowDefinition definition,
        QuickWindowInterfaceMember member) =>
        definition.EffectiveInterfaceMembers
            .Select(item => item.MemberKey == member.MemberKey ? member : item)
            .ToArray();

    private static string ProposeMemberName(QuickWindowDefinition definition)
    {
        var taken = definition.EffectiveInterfaceMembers
            .Select(member => member.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < 1000; index++)
        {
            var candidate = $"membre{index}";
            if (!taken.Contains(candidate)) return candidate;
        }

        return $"membre_{Guid.NewGuid().ToString("N")[..6]}";
    }

    private static QuickWindowDefinition? Find(PageWorkspaceSnapshot snapshot, Guid definitionKey) =>
        snapshot.Project.EffectiveQuickWindows.FirstOrDefault(item => item.DefinitionKey == definitionKey);

    private static ParameterSheet? FindSheet(ScadaProject project, Guid sheetKey) =>
        project.EffectiveParameterSheets.FirstOrDefault(sheet => sheet.SheetKey == sheetKey);

    private static ParameterSheet? FindSheet(PageWorkspaceSnapshot snapshot, Guid sheetKey) =>
        snapshot.Project.EffectiveParameterSheets.FirstOrDefault(item => item.SheetKey == sheetKey);

    /// <summary>Replaces one sheet by key and wraps the transition as a prepared workspace mutation.</summary>
    private static QuickWindowWorkspaceMutation UpdateSheet(PageWorkspaceSnapshot snapshot, ParameterSheet candidate, string label)
    {
        var sheets = snapshot.Project.EffectiveParameterSheets
            .Select(item => item.SheetKey == candidate.SheetKey ? candidate : item)
            .ToArray();
        var after = snapshot with { Version = snapshot.Version + 1, Project = snapshot.Project with { ParameterSheets = sheets } };
        return new QuickWindowWorkspaceMutation(
            snapshot,
            after,
            CommandResult.Success("Feuille de paramètres mise à jour.", workspaceDirty: true),
            label,
            candidate.SheetKey);
    }

    /// <summary>Builds a blocked, no-op mutation reporting one or more sheet-code validation errors.</summary>
    private static QuickWindowWorkspaceMutation BlockedSheetMutation(
        PageWorkspaceSnapshot snapshot,
        string label,
        IReadOnlyList<string> errors)
    {
        var diagnostics = errors.Select(message => new ScadaBuildValidationIssue(
            ScadaBuildValidationSeverity.Error,
            "parameter-sheet.code-invalid",
            message,
            PropertyPath: "Project.ParameterSheets",
            SuggestedFix: "Choisissez un autre nom pour la feuille.")).ToArray();
        return new QuickWindowWorkspaceMutation(
            snapshot,
            snapshot,
            CommandResult.Blocked("Parameter-sheet mutation was blocked.", diagnostics),
            label);
    }

    /// <summary>Proposes a deterministic unique code for a new or duplicated parameter sheet.</summary>
    private static string ProposeSheetCode(ScadaProject project, string seed)
    {
        var taken = project.EffectiveParameterSheets
            .Select(sheet => sheet.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(seed)) return seed;
        for (var index = 2; index < 1000; index++)
        {
            var candidate = $"{seed}_{index}";
            if (!taken.Contains(candidate)) return candidate;
        }

        return $"{seed}_{Guid.NewGuid().ToString("N")[..6]}";
    }

    /// <summary>Appends the first diagnostic reason to a French refusal prefix, when one is available.</summary>
    /// <remarks>
    /// Contracts: keeps the existing status prefix; falls back to `"{prefix}."` alone when the mutation
    /// carries no diagnostic, so a validation path added later never regresses to a blank reason.
    /// </remarks>
    private static string FormatRefusal(string prefix, QuickWindowWorkspaceMutation mutation)
    {
        var reason = mutation.Result.Diagnostics.FirstOrDefault()?.Message;
        return string.IsNullOrWhiteSpace(reason) ? $"{prefix}." : $"{prefix} : {reason}.";
    }

    private static string ProposeCode(PageWorkspaceSnapshot snapshot, string seed)
    {
        var taken = snapshot.Project.EffectiveQuickWindows
            .Select(definition => definition.EffectiveCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(seed)) return seed;
        for (var index = 2; index < 1000; index++)
        {
            var candidate = $"{seed}_{index}";
            if (!taken.Contains(candidate)) return candidate;
        }

        return $"{seed}_{Guid.NewGuid().ToString("N")[..6]}";
    }
}
