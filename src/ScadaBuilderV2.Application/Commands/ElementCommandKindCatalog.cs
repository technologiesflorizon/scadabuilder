using ScadaBuilderV2.Domain.ElementEvents.Command;

namespace ScadaBuilderV2.Application.Commands;

/// <summary>
/// One entry of the "Type" list offered by the element-command editor: a fixed <see cref="ScadaCommandKind"/>
/// with its French display label, whether it may be selected in the current context, and, when it may not,
/// why.
/// </summary>
public sealed record ElementCommandKindOption(
    ScadaCommandKind Kind,
    string Label,
    bool IsEnabled,
    string? DisabledReason = null);

/// <summary>
/// Builds the "Type" list of the element-command editor: always the six <see cref="ScadaCommandKind"/> values,
/// in enum order, never filtered.
/// </summary>
/// <remarks>
/// Before this policy existed, `ElementCommandDialog` removed `OpenQuickWindow`/`CloseQuickWindow` from
/// `KindComboBox.ItemsSource` whenever the caller's quick-window context did not allow them, and removed any
/// kind already present in `usedKinds`. An operator with zero quick-window definitions in the project (or
/// reaching the dialog through the "Propriete" panel's own "+ Ajouter"/"Editer" buttons, which built the
/// dialog without any quick-window context at all) could never find "Ouvrir une fenetre rapide" - the entry
/// was not disabled, it was simply absent. This catalog replaces removal with a fixed six-entry list whose
/// entries are individually enabled or disabled with an explained reason, so the option is always visible and,
/// when it cannot be used, says why on hover.
///
/// Decisions: DEC-0050.
/// Tests: tests/ScadaBuilderV2.Tests/Commands/ElementCommandKindCatalogTests.cs.
/// </remarks>
public static class ElementCommandKindCatalog
{
    /// <summary>Reason shown when a kind is already used by another command of the same element.</summary>
    public const string AlreadyUsedReason = "Ce type est déjà utilisé par une autre commande de cet élément.";

    /// <summary>Reason shown when `OpenQuickWindow` has no quick-window definition to target.</summary>
    public const string NoQuickWindowDefinitionReason =
        "Aucune fenêtre rapide n'existe dans ce projet. Créez-en une dans le panneau « Fenêtres rapides ».";

    /// <summary>Reason shown when `CloseQuickWindow` is offered outside a quick-window's own content.</summary>
    public const string NotQuickWindowContentReason = "Disponible seulement depuis le contenu d'une fenêtre rapide.";

    /// <summary>
    /// Creates the six "Type" entries for one authoring context, in <see cref="ScadaCommandKind"/> enum order.
    /// </summary>
    /// <param name="usedKinds">Kinds already carried by another command of the edited element.</param>
    /// <param name="editedKind">
    /// The kind of the command currently being edited, when editing rather than adding; a kind equal to this
    /// one is never disabled by <paramref name="usedKinds"/>.
    /// </param>
    /// <param name="hasQuickWindowDefinitions">Whether the project has at least one quick-window definition.</param>
    /// <param name="isQuickWindowContent">Whether the caller surface is authoring inside a quick-window's own content.</param>
    /// <returns>The six entries, in enum order. Nothing is ever omitted.</returns>
    public static IReadOnlyList<ElementCommandKindOption> CreateOptions(
        IReadOnlyCollection<ScadaCommandKind> usedKinds,
        ScadaCommandKind? editedKind,
        bool hasQuickWindowDefinitions,
        bool isQuickWindowContent)
    {
        var options = new List<ElementCommandKindOption>(6);
        foreach (var kind in Enum.GetValues<ScadaCommandKind>())
        {
            options.Add(CreateOption(kind, usedKinds, editedKind, hasQuickWindowDefinitions, isQuickWindowContent));
        }

        return options;
    }

    private static ElementCommandKindOption CreateOption(
        ScadaCommandKind kind,
        IReadOnlyCollection<ScadaCommandKind> usedKinds,
        ScadaCommandKind? editedKind,
        bool hasQuickWindowDefinitions,
        bool isQuickWindowContent)
    {
        var label = Label(kind);

        // Rule order is deliberate: a kind already used by another command wins over any other reason, even
        // when that other reason also applies (e.g. an already-used OpenQuickWindow with zero definitions
        // still reads "already used", not "no quick window exists").
        if (usedKinds.Contains(kind) && kind != editedKind)
        {
            return new ElementCommandKindOption(kind, label, IsEnabled: false, AlreadyUsedReason);
        }

        if (kind == ScadaCommandKind.OpenQuickWindow && !hasQuickWindowDefinitions)
        {
            return new ElementCommandKindOption(kind, label, IsEnabled: false, NoQuickWindowDefinitionReason);
        }

        if (kind == ScadaCommandKind.CloseQuickWindow && !isQuickWindowContent)
        {
            return new ElementCommandKindOption(kind, label, IsEnabled: false, NotQuickWindowContentReason);
        }

        return new ElementCommandKindOption(kind, label, IsEnabled: true);
    }

    private static string Label(ScadaCommandKind kind) => kind switch
    {
        ScadaCommandKind.WriteTag => "Écrire un tag",
        ScadaCommandKind.Navigate => "Aller à une page",
        ScadaCommandKind.OpenQuickWindow => "Ouvrir une fenêtre rapide",
        ScadaCommandKind.CloseQuickWindow => "Fermer la fenêtre rapide",
        ScadaCommandKind.OpenUrl => "Ouvrir une adresse web",
        ScadaCommandKind.Back => "Revenir en arrière",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown ScadaCommandKind."),
    };
}
