using System.Text.Json.Serialization;

namespace ScadaBuilderV2.Domain.ElementEvents.Command;

/// <summary>Pointer trigger that fires one Element+ command.</summary>
public enum ScadaCommandTrigger { OnClick, OnRelease, OnHover, OnHoverEnter, OnHoverExit }

/// <summary>Kind of runtime action performed by one Element+ command.</summary>
/// <remarks>
/// Statut : DEC-0050 décommissionné comme modèle d'authoring (remplacé par DEC-0052) ; conservé car DEC-0052 en
/// dépend : OpenQuickWindow, CloseQuickWindow et QuickWindowInvocationKey sont assignés par l'abaissement et exigés
/// par la validation build et le runtime. La clause de retrait des anciens types de commande ci-dessous reste en
/// vigueur.
/// Decisions: DEC-0050 removes OpenPopup/TogglePopup/ClosePopup (never completed end-to-end) and adds OpenQuickWindow/CloseQuickWindow. Legacy popup actions remain as ScadaActionKind phased-out residues, never as modern command proofs.
/// </remarks>
public enum ScadaCommandKind { WriteTag, Navigate, OpenQuickWindow, CloseQuickWindow, OpenUrl, Back }

/// <summary>Write behavior for a <see cref="ScadaCommandKind.WriteTag"/> command.</summary>
public enum ScadaWriteMode { Momentary, Toggle, SetFixed, SetFromInput }

/// <summary>Optional operator confirmation shown before a command executes.</summary>
public sealed record ScadaConfirmation(string Message);

/// <summary>
/// One Element+ command: an operator-triggered action that writes a tag or navigates/opens
/// a popup/URL. Independent from display-state rules; never changes appearance directly.
/// </summary>
/// <remarks>
/// Decisions: DEC-0036, DEC-0054.
/// InterlockTagId: Toggle only; written to 0 before WriteTagId is written to 1 (docs/superpowers/specs/2026-10-13-toggle-interlock-design.md).
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementCommandConfigTests.cs.
/// </remarks>
public sealed record ScadaCommandBinding(
    string Id,
    string Name,
    bool Enabled,
    ScadaCommandTrigger Trigger,
    ScadaCommandKind Kind,
    ScadaConfirmation? Confirmation = null,
    string? WriteTagId = null,
    string? ReadTagId = null,
    ScadaWriteMode? WriteMode = null,
    string? OnValue = null,
    string? OffValue = null,
    string? FixedValue = null,
    string? TargetPageId = null,
    string? Url = null,
    bool NewTab = false,
    Guid? TargetPageKey = null,
    Guid? QuickWindowInvocationKey = null,
    Guid? ParameterSheetKey = null,
    Guid? QuickWindowPageKey = null,
    string? InterlockTagId = null)
{
    /// <summary>
    /// Gets the tag id read for <see cref="ScadaWriteMode.Toggle"/>: <see cref="ReadTagId"/>
    /// if set, otherwise <see cref="WriteTagId"/>.
    /// </summary>
    [JsonIgnore]
    public string EffectiveReadTagId => ReadTagId ?? WriteTagId ?? string.Empty;

    /// <summary>Gets whether this command is a quick-window command.</summary>
    [JsonIgnore]
    public bool IsQuickWindowCommand => Kind is ScadaCommandKind.OpenQuickWindow or ScadaCommandKind.CloseQuickWindow;

    /// <summary>Gets whether this command requires a QuickWindowInvocationKey.</summary>
    [JsonIgnore]
    public bool RequiresInvocationKey => Kind == ScadaCommandKind.OpenQuickWindow;
}
