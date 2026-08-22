using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

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
    QuickWindowDependencyAnalyzer? dependencyAnalyzer = null)
{
    private readonly QuickWindowDefinitionService definitions = definitionService ?? new QuickWindowDefinitionService();
    private readonly QuickWindowDependencyAnalyzer analyzer = dependencyAnalyzer ?? new QuickWindowDependencyAnalyzer();

    /// <summary>Gets the searchable inventory bound to the project group.</summary>
    public QuickWindowsPanelViewModel Panel { get; } = new();

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
        await host.ActivateQuickWindowAsync(ActiveContext, definition);
    }

    /// <summary>Clears the quick-window context when a page becomes the active surface again.</summary>
    public void ClearActiveContext() => ActiveContext = null;

    /// <summary>Lists the callers of one definition for the usage navigation surface.</summary>
    public IReadOnlyList<QuickWindowUsage> ListUsages(PageWorkspaceSnapshot snapshot, Guid definitionKey) =>
        definitions.ListUsages(snapshot, definitionKey);

    /// <summary>Lists the outdated invocations of one definition for the repair surface.</summary>
    public IReadOnlyList<QuickWindowOutdatedInvocation> ListOutdatedInvocations(PageWorkspaceSnapshot snapshot, Guid definitionKey) =>
        definitions.ListOutdatedInvocations(snapshot, definitionKey);

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
