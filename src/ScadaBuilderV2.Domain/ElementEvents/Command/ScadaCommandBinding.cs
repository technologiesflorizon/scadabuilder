using System.Text.Json.Serialization;

namespace ScadaBuilderV2.Domain.ElementEvents.Command;

/// <summary>Pointer trigger that fires one Element+ command.</summary>
public enum ScadaCommandTrigger { OnClick, OnRelease, OnHover, OnHoverEnter, OnHoverExit }

/// <summary>Kind of runtime action performed by one Element+ command.</summary>
public enum ScadaCommandKind { WriteTag, Navigate, OpenPopup, TogglePopup, ClosePopup, OpenUrl, Back }

/// <summary>Write behavior for a <see cref="ScadaCommandKind.WriteTag"/> command.</summary>
public enum ScadaWriteMode { Momentary, Toggle, SetFixed, SetFromInput }

/// <summary>Optional operator confirmation shown before a command executes.</summary>
public sealed record ScadaConfirmation(string Message);

/// <summary>
/// One Element+ command: an operator-triggered action that writes a tag or navigates/opens
/// a popup/URL. Independent from display-state rules; never changes appearance directly.
/// </summary>
/// <remarks>
/// Decisions: DEC-0036.
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
    bool NewTab = false)
{
    /// <summary>
    /// Gets the tag id read for <see cref="ScadaWriteMode.Toggle"/>: <see cref="ReadTagId"/>
    /// if set, otherwise <see cref="WriteTagId"/>.
    /// </summary>
    [JsonIgnore]
    public string EffectiveReadTagId => ReadTagId ?? WriteTagId ?? string.Empty;
}
