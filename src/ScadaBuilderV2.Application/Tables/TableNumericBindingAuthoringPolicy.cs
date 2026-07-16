namespace ScadaBuilderV2.Application.Tables;

/// <summary>Represents normalized read and write bindings for numeric-cell authoring.</summary>
/// <param name="ReadTagId">The effective read tag id.</param>
/// <param name="WriteTagId">The effective write tag id.</param>
/// <param name="ReadDefaultedFromWrite">Whether the read tag was defaulted from the write tag.</param>
public sealed record TableNumericBindingDraft(
    string? ReadTagId,
    string? WriteTagId,
    bool ReadDefaultedFromWrite);

/// <summary>Applies numeric-cell binding defaults before typed edit requests are created.</summary>
/// <remarks>
/// Decisions: DEC-0043.
/// Contracts: docs/superpowers/specs/2026-07-15-table-numeric-cell-authoring-correction-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/TableNumericBindingAuthoringPolicyTests.cs.
/// </remarks>
public static class TableNumericBindingAuthoringPolicy
{
    /// <summary>Defaults an empty read binding from a writable binding and removes writes from read-only inputs.</summary>
    public static TableNumericBindingDraft Normalize(string? readTagId, string? writeTagId, bool isReadOnly)
    {
        var read = NormalizeTagId(readTagId);
        var write = isReadOnly ? null : NormalizeTagId(writeTagId);
        var defaulted = read is null && write is not null;
        return new(defaulted ? write : read, write, defaulted);
    }

    private static string? NormalizeTagId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
