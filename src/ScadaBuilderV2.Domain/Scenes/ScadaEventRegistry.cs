namespace ScadaBuilderV2.Domain.Scenes;

/// <summary>
/// Defines the supported object event triggers for Element+ runtime bindings.
/// </summary>
/// <param name="Key">Stable editor key used by the event authoring UI.</param>
/// <param name="RuntimeTrigger">Browser event name emitted in FT100/TF100Web export.</param>
/// <param name="FrenchLabel">French label shown to operators and designers.</param>
/// <param name="AllowsMultiple">Whether one Element+ can carry several bindings for the same trigger.</param>
/// <param name="SupportsConditions">Whether the trigger contract allows conditional execution.</param>
/// <param name="Description">Short industrial HMI usage description.</param>
public sealed record ScadaEventTriggerContract(
    string Key,
    string RuntimeTrigger,
    string FrenchLabel,
    bool AllowsMultiple,
    bool SupportsConditions,
    string Description);

/// <summary>
/// Defines the supported runtime action functions that can be attached to Element+ events.
/// </summary>
/// <param name="FunctionName">Stable function contract name used by authoring and documentation.</param>
/// <param name="Kind">Persisted scene action kind.</param>
/// <param name="FrenchLabel">French label shown in the event authoring UI.</param>
/// <param name="RequiredArguments">Required action arguments.</param>
/// <param name="Implemented">Whether the function is currently implemented in editor, save/reload, and export.</param>
/// <param name="Description">Short industrial HMI behavior description.</param>
public sealed record ScadaActionFunctionContract(
    string FunctionName,
    ScadaActionKind Kind,
    string FrenchLabel,
    IReadOnlyList<string> RequiredArguments,
    bool Implemented,
    string Description);

/// <summary>
/// Central registry for SCADA/HMI Element+ event names and action function contracts.
/// </summary>
/// <remarks>
/// Decisions: DEC-0011.
/// Contracts: docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs.
/// </remarks>
public static class ScadaEventRegistry
{
    /// <summary>
    /// Editor key for click-triggered Element+ events.
    /// </summary>
    public const string ClickKey = "OnClick";

    /// <summary>
    /// Editor key for pointer-release Element+ events.
    /// </summary>
    public const string ReleaseKey = "OnRelease";

    /// <summary>
    /// Editor key for pointer hover Element+ events.
    /// </summary>
    public const string HoverKey = "OnHover";

    /// <summary>
    /// Editor key for explicit pointer-enter Element+ events.
    /// </summary>
    public const string HoverEnterKey = "OnHoverEnter";

    /// <summary>
    /// Editor key for pointer-leave Element+ events.
    /// </summary>
    public const string HoverExitKey = "OnHoverExit";

    /// <summary>
    /// Runtime function name for page navigation events.
    /// </summary>
    public const string ChangePageFunction = "ChangePage";

    /// <summary>
    /// Runtime function name for writing a value to a TF100Web tag.
    /// </summary>
    public const string WriteTagFunction = "WriteTag";

    /// <summary>
    /// Browser runtime trigger for click events.
    /// </summary>
    public const string ClickRuntimeTrigger = "click";

    /// <summary>
    /// Browser runtime trigger for pointer-release events.
    /// </summary>
    public const string ReleaseRuntimeTrigger = "pointerup";

    /// <summary>
    /// Browser runtime trigger for pointer-enter hover events.
    /// </summary>
    public const string HoverRuntimeTrigger = "mouseenter";

    /// <summary>
    /// Browser runtime trigger for pointer-leave hover events.
    /// </summary>
    public const string HoverExitRuntimeTrigger = "mouseleave";

    private static readonly ScadaEventTriggerContract[] TriggerContracts =
    [
        new(ClickKey, ClickRuntimeTrigger, "Clic", true, true, "Declenche une ou plusieurs actions quand l'operateur clique l'Element+."),
        new(ReleaseKey, ReleaseRuntimeTrigger, "Relachement", true, true, "Declenche une ou plusieurs actions au relachement du pointeur."),
        new(HoverKey, HoverRuntimeTrigger, "Survol", true, true, "Declenche une ou plusieurs actions quand le pointeur entre sur l'Element+."),
        new(HoverEnterKey, HoverRuntimeTrigger, "Entree survol", true, true, "Alias explicite pour l'entree du pointeur sur l'Element+."),
        new(HoverExitKey, HoverExitRuntimeTrigger, "Sortie survol", true, true, "Declenche une ou plusieurs actions quand le pointeur quitte l'Element+.")
    ];

    private static readonly ScadaActionFunctionContract[] ActionContracts =
    [
        new(
            ChangePageFunction,
            ScadaActionKind.Navigate,
            "Changer de page",
            ["TargetPageId"],
            true,
            "Navigue vers une page compilee du projet FT100/TF100Web."),
        new("Show", ScadaActionKind.Show, "Afficher objet", ["TargetElementId"], false, "Affiche un Element+ cible."),
        new("Hide", ScadaActionKind.Hide, "Masquer objet", ["TargetElementId"], false, "Masque un Element+ cible."),
        new("ToggleVisibility", ScadaActionKind.ToggleVisibility, "Basculer visibilite", ["TargetElementId"], false, "Bascule la visibilite d'un Element+ cible."),
        new(WriteTagFunction, ScadaActionKind.WriteTag, "Ecrire tag", ["TagId", "Value"], true, "Ecrit une valeur dans un tag runtime.")
    ];

    /// <summary>
    /// Gets the supported event trigger contracts.
    /// </summary>
    public static IReadOnlyList<ScadaEventTriggerContract> Triggers => TriggerContracts;

    /// <summary>
    /// Gets the supported runtime action function contracts.
    /// </summary>
    public static IReadOnlyList<ScadaActionFunctionContract> Actions => ActionContracts;

    /// <summary>
    /// Finds a trigger contract by editor key or runtime browser trigger.
    /// </summary>
    public static ScadaEventTriggerContract? FindTrigger(string? keyOrRuntimeTrigger)
    {
        return TriggerContracts.FirstOrDefault(trigger =>
            string.Equals(trigger.Key, keyOrRuntimeTrigger, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trigger.RuntimeTrigger, keyOrRuntimeTrigger, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds an action function contract by function name or persisted action kind.
    /// </summary>
    public static ScadaActionFunctionContract? FindAction(string? functionNameOrKind)
    {
        return ActionContracts.FirstOrDefault(action =>
            string.Equals(action.FunctionName, functionNameOrKind, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action.Kind.ToString(), functionNameOrKind, StringComparison.OrdinalIgnoreCase));
    }
}
