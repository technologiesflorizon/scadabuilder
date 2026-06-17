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
    /// Runtime function name for opening a compiled fragment page as a popup.
    /// </summary>
    public const string OpenPopupFunction = "OpenPopup";

    /// <summary>
    /// Runtime function name for closing a compiled fragment popup.
    /// </summary>
    public const string ClosePopupFunction = "ClosePopup";

    /// <summary>
    /// Runtime function name for toggling a compiled fragment popup.
    /// </summary>
    public const string TogglePopupFunction = "TogglePopup";

    /// <summary>
    /// Runtime function name for showing a target Element+ object.
    /// </summary>
    public const string ShowFunction = "Show";

    /// <summary>
    /// Runtime function name for hiding a target Element+ object.
    /// </summary>
    public const string HideFunction = "Hide";

    /// <summary>
    /// Runtime function name for toggling a target Element+ object visibility.
    /// </summary>
    public const string ToggleVisibilityFunction = "ToggleVisibility";

    /// <summary>
    /// Runtime function name for showing the standard runtime border on a target Element+ object.
    /// </summary>
    public const string ShowBorderFunction = "ShowBorder";

    /// <summary>
    /// Runtime function name for hiding the standard runtime border on a target Element+ object.
    /// </summary>
    public const string HideBorderFunction = "HideBorder";

    /// <summary>
    /// Runtime function name for toggling the standard runtime border on a target Element+ object.
    /// </summary>
    public const string ToggleBorderFunction = "ToggleBorder";

    /// <summary>
    /// Runtime function names for standard visual effect actions.
    /// </summary>
    public const string StartBlinkEffectFunction = "StartBlinkEffect";
    public const string StopBlinkEffectFunction = "StopBlinkEffect";
    public const string ToggleBlinkEffectFunction = "ToggleBlinkEffect";
    public const string StartGlowEffectFunction = "StartGlowEffect";
    public const string StopGlowEffectFunction = "StopGlowEffect";
    public const string ToggleGlowEffectFunction = "ToggleGlowEffect";
    public const string StartPulseEffectFunction = "StartPulseEffect";
    public const string StopPulseEffectFunction = "StopPulseEffect";
    public const string TogglePulseEffectFunction = "TogglePulseEffect";
    public const string StartAlarmEffectFunction = "StartAlarmEffect";
    public const string StopAlarmEffectFunction = "StopAlarmEffect";
    public const string ToggleAlarmEffectFunction = "ToggleAlarmEffect";
    public const string StartDegradedEffectFunction = "StartDegradedEffect";
    public const string StopDegradedEffectFunction = "StopDegradedEffect";
    public const string ToggleDegradedEffectFunction = "ToggleDegradedEffect";

    /// <summary>
    /// Page-scoped CSS class used by runtime border highlight actions.
    /// </summary>
    public const string RuntimeBorderHighlightClass = "scada-runtime-border-highlight";

    /// <summary>
    /// Page-scoped CSS classes used by standard runtime visual effect actions.
    /// </summary>
    public const string RuntimeBlinkEffectClass = "scada-runtime-effect-blink";
    public const string RuntimeGlowEffectClass = "scada-runtime-effect-glow";
    public const string RuntimePulseEffectClass = "scada-runtime-effect-pulse";
    public const string RuntimeAlarmEffectClass = "scada-runtime-effect-alarm";
    public const string RuntimeDegradedEffectClass = "scada-runtime-effect-degraded";

    /// <summary>
    /// Runtime function name for writing a value to a TF100Web tag.
    /// </summary>
    public const string WriteTagFunction = "WriteTag";

    /// <summary>
    /// Authoring function name for binding an Element+ value display to a tag.
    /// </summary>
    public const string ReadValueFunction = "ReadValue";

    /// <summary>
    /// Authoring function name for binding an editable Element+ input to a tag write.
    /// </summary>
    public const string WriteValueFunction = "WriteValue";

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
        new(
            OpenPopupFunction,
            ScadaActionKind.MountFragment,
            "Ouvrir popup",
            ["TargetPageId"],
            true,
            "Ouvre une page fragment compilee dans une popup runtime."),
        new(
            ClosePopupFunction,
            ScadaActionKind.ClosePopup,
            "Fermer popup",
            ["TargetPageId"],
            true,
            "Ferme une popup runtime ouverte pour un fragment compile."),
        new(
            TogglePopupFunction,
            ScadaActionKind.TogglePopup,
            "Basculer popup",
            ["TargetPageId"],
            true,
            "Ouvre ou ferme une popup runtime pour un fragment compile."),
        new(ReadValueFunction, ScadaActionKind.ReadValue, "Lire valeur", ["TagId"], true, "Lie un tag runtime a la valeur affichee par un Element+."),
        new(WriteValueFunction, ScadaActionKind.WriteValue, "Ecrire valeur", ["TagId"], true, "Lie la valeur saisie par l'operateur a un tag runtime."),
        new(ShowFunction, ScadaActionKind.Show, "Afficher objet", ["TargetElementId"], true, "Affiche un Element+ cible."),
        new(HideFunction, ScadaActionKind.Hide, "Masquer objet", ["TargetElementId"], true, "Masque un Element+ cible."),
        new(ToggleVisibilityFunction, ScadaActionKind.ToggleVisibility, "Basculer visibilite", ["TargetElementId"], true, "Bascule la visibilite d'un Element+ cible."),
        new(ShowBorderFunction, ScadaActionKind.SetClass, "Afficher bordure", ["TargetElementId"], true, "Affiche la bordure runtime standard sur un Element+ cible."),
        new(HideBorderFunction, ScadaActionKind.RemoveClass, "Masquer bordure", ["TargetElementId"], true, "Masque la bordure runtime standard sur un Element+ cible."),
        new(ToggleBorderFunction, ScadaActionKind.ToggleClass, "Basculer bordure", ["TargetElementId"], true, "Bascule la bordure runtime standard sur un Element+ cible."),
        new(StartBlinkEffectFunction, ScadaActionKind.SetClass, "Demarrer clignotement", ["TargetElementId"], true, "Demarre l'effet runtime clignotement sur un Element+ cible."),
        new(StopBlinkEffectFunction, ScadaActionKind.RemoveClass, "Arreter clignotement", ["TargetElementId"], true, "Arrete l'effet runtime clignotement sur un Element+ cible."),
        new(ToggleBlinkEffectFunction, ScadaActionKind.ToggleClass, "Basculer clignotement", ["TargetElementId"], true, "Bascule l'effet runtime clignotement sur un Element+ cible."),
        new(StartGlowEffectFunction, ScadaActionKind.SetClass, "Demarrer halo", ["TargetElementId"], true, "Demarre l'effet runtime halo sur un Element+ cible."),
        new(StopGlowEffectFunction, ScadaActionKind.RemoveClass, "Arreter halo", ["TargetElementId"], true, "Arrete l'effet runtime halo sur un Element+ cible."),
        new(ToggleGlowEffectFunction, ScadaActionKind.ToggleClass, "Basculer halo", ["TargetElementId"], true, "Bascule l'effet runtime halo sur un Element+ cible."),
        new(StartPulseEffectFunction, ScadaActionKind.SetClass, "Demarrer pulsation", ["TargetElementId"], true, "Demarre l'effet runtime pulsation sur un Element+ cible."),
        new(StopPulseEffectFunction, ScadaActionKind.RemoveClass, "Arreter pulsation", ["TargetElementId"], true, "Arrete l'effet runtime pulsation sur un Element+ cible."),
        new(TogglePulseEffectFunction, ScadaActionKind.ToggleClass, "Basculer pulsation", ["TargetElementId"], true, "Bascule l'effet runtime pulsation sur un Element+ cible."),
        new(StartAlarmEffectFunction, ScadaActionKind.SetClass, "Demarrer alarme visuelle", ["TargetElementId"], true, "Demarre l'effet runtime alarme visuelle sur un Element+ cible."),
        new(StopAlarmEffectFunction, ScadaActionKind.RemoveClass, "Arreter alarme visuelle", ["TargetElementId"], true, "Arrete l'effet runtime alarme visuelle sur un Element+ cible."),
        new(ToggleAlarmEffectFunction, ScadaActionKind.ToggleClass, "Basculer alarme visuelle", ["TargetElementId"], true, "Bascule l'effet runtime alarme visuelle sur un Element+ cible."),
        new(StartDegradedEffectFunction, ScadaActionKind.SetClass, "Demarrer traitement degrade", ["TargetElementId"], true, "Demarre l'effet runtime traitement degrade sur un Element+ cible."),
        new(StopDegradedEffectFunction, ScadaActionKind.RemoveClass, "Arreter traitement degrade", ["TargetElementId"], true, "Arrete l'effet runtime traitement degrade sur un Element+ cible."),
        new(ToggleDegradedEffectFunction, ScadaActionKind.ToggleClass, "Basculer traitement degrade", ["TargetElementId"], true, "Bascule l'effet runtime traitement degrade sur un Element+ cible."),
        new(WriteTagFunction, ScadaActionKind.WriteTag, "Ecrire tag", ["TagId", "Value"], false, "Compatibilite legacy pour ecriture de valeur fixe.")
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

    /// <summary>
    /// Determines whether a function is one of the standard runtime visual effect functions.
    /// </summary>
    public static bool IsVisualEffectFunction(string? functionName)
    {
        return TryResolveVisualEffectFunction(functionName, out _, out _);
    }

    /// <summary>
    /// Resolves a standard visual effect function to its persisted class action and CSS class.
    /// </summary>
    public static bool TryResolveVisualEffectFunction(
        string? functionName,
        out ScadaActionKind actionKind,
        out string className)
    {
        (actionKind, className) = functionName switch
        {
            StartBlinkEffectFunction => (ScadaActionKind.SetClass, RuntimeBlinkEffectClass),
            StopBlinkEffectFunction => (ScadaActionKind.RemoveClass, RuntimeBlinkEffectClass),
            ToggleBlinkEffectFunction => (ScadaActionKind.ToggleClass, RuntimeBlinkEffectClass),
            StartGlowEffectFunction => (ScadaActionKind.SetClass, RuntimeGlowEffectClass),
            StopGlowEffectFunction => (ScadaActionKind.RemoveClass, RuntimeGlowEffectClass),
            ToggleGlowEffectFunction => (ScadaActionKind.ToggleClass, RuntimeGlowEffectClass),
            StartPulseEffectFunction => (ScadaActionKind.SetClass, RuntimePulseEffectClass),
            StopPulseEffectFunction => (ScadaActionKind.RemoveClass, RuntimePulseEffectClass),
            TogglePulseEffectFunction => (ScadaActionKind.ToggleClass, RuntimePulseEffectClass),
            StartAlarmEffectFunction => (ScadaActionKind.SetClass, RuntimeAlarmEffectClass),
            StopAlarmEffectFunction => (ScadaActionKind.RemoveClass, RuntimeAlarmEffectClass),
            ToggleAlarmEffectFunction => (ScadaActionKind.ToggleClass, RuntimeAlarmEffectClass),
            StartDegradedEffectFunction => (ScadaActionKind.SetClass, RuntimeDegradedEffectClass),
            StopDegradedEffectFunction => (ScadaActionKind.RemoveClass, RuntimeDegradedEffectClass),
            ToggleDegradedEffectFunction => (ScadaActionKind.ToggleClass, RuntimeDegradedEffectClass),
            _ => (default, string.Empty)
        };

        return !string.IsNullOrWhiteSpace(className);
    }
}
