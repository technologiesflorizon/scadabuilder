using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.QuickWindows;
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
    }

    /// <summary>Creates one empty definition after asking for its name, then opens it.</summary>
    public async Task<QuickWindowWorkspaceMutation?> CreateAsync(PageWorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var name = await host.RequestQuickWindowNameAsync("Nouvelle fenêtre rapide", ProposeCode(snapshot, "fenetre"));
        if (string.IsNullOrWhiteSpace(name)) return null;

        var mutation = definitions.CreateEmpty(snapshot, name, name);
        if (mutation.Result.Status != CommandResultStatus.Succeeded)
        {
            host.ReportQuickWindowStatus("Création de la fenêtre rapide refusée.");
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

        var created = definitions.CreateEmpty(snapshot, name, name);
        if (created.Result.Status != CommandResultStatus.Succeeded)
        {
            host.ReportQuickWindowStatus("Duplication refusée.");
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
            host.ReportQuickWindowStatus("Duplication refusée.");
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

        var mutation = definitions.Update(snapshot, definition with { Code = name, DisplayName = name });
        host.ReportQuickWindowStatus(mutation.Result.Status == CommandResultStatus.Succeeded
            ? $"Fenêtre rapide renommée en '{name}'."
            : "Renommage refusé.");
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

        return ApplyMembers(
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

        return ApplyMembers(
            snapshot,
            definition,
            Replace(definition, member),
            $"Membre '{member.Name}' mis à jour.");
    }

    /// <summary>Applies one inline table edit of a common member property (FR-UI-16).</summary>
    public QuickWindowWorkspaceMutation? ApplyInlineInterfaceEdit(
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

        return ApplyMembers(
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

        return ApplyMembers(
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

    /// <summary>Lists the outdated invocations of one definition for the repair surface.</summary>
    public IReadOnlyList<QuickWindowOutdatedInvocation> ListOutdatedInvocations(PageWorkspaceSnapshot snapshot, Guid definitionKey) =>
        definitions.ListOutdatedInvocations(snapshot, definitionKey);

    /// <summary>
    /// Prepares one interface transition: the local interface version is incremented only when the public
    /// contract actually changed, so a private edit or a rename never invalidates a persisted invocation.
    /// </summary>
    private QuickWindowWorkspaceMutation ApplyMembers(
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
