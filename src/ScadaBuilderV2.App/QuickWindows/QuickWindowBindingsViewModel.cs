using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// Bounded quick-window authoring context of one caller surface: which definitions may be opened, which
/// parent ports may be forwarded and whether the caller lives inside a quick-window content.
/// </summary>
/// <remarks>
/// A page never offers `CloseQuickWindow`; a quick-window content offers it as `Self` without any free
/// target. No Toggle command kind exists: `DEC-0050` removed the popup command kinds entirely.
///
/// Statut : DEC-0050 décommissionné comme modèle d'authoring (remplacé par DEC-0052) ; conservé car DEC-0052 en
/// dépend : QuickWindowCommandAuthoringContext alimente ElementCommandDialog (CloseQuickWindow(Self) sur une page de
/// type Fenêtre rapide) et ElementPropertiesDialog. Ses arguments ParentPorts et FindInvocation sont des résidus
/// DEC-0050. L'éditeur de liaisons qui partageait ce fichier a été retiré le 2026-10-06.
/// Decisions: DEC-0050 (décommissionné), DEC-0052, FR-011, FR-013, FR-UI-18, FR-UI-19.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §9.2.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowBindingAuthoringTests.cs.
/// </remarks>
public sealed record QuickWindowCommandAuthoringContext(
    IReadOnlyList<QuickWindowDefinition> Definitions,
    IReadOnlyList<QuickWindowInterfaceMember> ParentPorts,
    bool IsQuickWindowContent = false,
    Func<Guid, QuickWindowInvocation?>? FindInvocation = null)
{
    /// <summary>Gets the context of a surface that cannot author any quick-window command.</summary>
    public static QuickWindowCommandAuthoringContext Empty { get; } = new([], []);

    /// <summary>Gets the definitions that may be opened, in project order.</summary>
    public IReadOnlyList<QuickWindowDefinition> EffectiveDefinitions => Definitions ?? [];

    /// <summary>Gets the public ports of the hosting definition that a binding may forward.</summary>
    public IReadOnlyList<QuickWindowInterfaceMember> EffectiveParentPorts =>
        (ParentPorts ?? []).Where(member => member.IsPublic).ToArray();

    /// <summary>Gets whether an `OpenQuickWindow` command may be authored on this surface.</summary>
    public bool CanOpenQuickWindow => EffectiveDefinitions.Count > 0;

    /// <summary>Gets whether a `CloseQuickWindow(Self)` command may be authored on this surface.</summary>
    public bool CanCloseQuickWindow => IsQuickWindowContent;

    /// <summary>Returns the quick-window command kinds offered by this surface, in authoring order.</summary>
    public IReadOnlyList<ScadaCommandKind> AllowedCommandKinds()
    {
        var kinds = new List<ScadaCommandKind>();
        if (CanOpenQuickWindow) kinds.Add(ScadaCommandKind.OpenQuickWindow);
        if (CanCloseQuickWindow) kinds.Add(ScadaCommandKind.CloseQuickWindow);
        return kinds;
    }

    /// <summary>Finds one definition offered by this context.</summary>
    public QuickWindowDefinition? Find(Guid definitionKey) =>
        EffectiveDefinitions.FirstOrDefault(definition => definition.DefinitionKey == definitionKey);

    /// <summary>Resolves the persisted invocation of one caller command, when it already exists.</summary>
    public QuickWindowInvocation? ResolveInvocation(Guid? invocationKey) =>
        invocationKey is { } key && key != Guid.Empty ? FindInvocation?.Invoke(key) : null;

    /// <summary>Resolves the definition targeted by one persisted invocation.</summary>
    public QuickWindowDefinition? ResolveDefinitionOfInvocation(Guid? invocationKey) =>
        ResolveInvocation(invocationKey) is { } invocation ? Find(invocation.DefinitionKey) : null;
}
