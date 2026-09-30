using ScadaBuilderV2.Domain.ElementEvents.Expressions;

namespace ScadaBuilderV2.Domain.QuickWindows;

/// <summary>
/// One project parameter sheet: a declarative text mapping each `#N` placeholder of a quick-window page to a
/// tag, a literal or an expression.
/// </summary>
/// <remarks>
/// <see cref="Text"/> is the single source of truth; every entry is derived from it by
/// <see cref="ParameterSheetParser"/>.
///
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §4.2, §4.3.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetParserTests.cs.
/// </remarks>
public sealed record ParameterSheet(Guid SheetKey, string Code, string DisplayName, string Text);

/// <summary>Nature of one parameter value, derived from its parsed right-hand side (ruling R1).</summary>
public enum ParameterValueKind
{
    /// <summary>Exactly one tag reference.</summary>
    Tag,
    /// <summary>Exactly one string literal.</summary>
    Literal,
    /// <summary>Any other expression, including a lone number or boolean.</summary>
    Expression
}

/// <summary>One valid `#N := …` line of a parameter sheet.</summary>
public sealed record ParameterEntry(int Number, int Line, ParameterValueKind Kind, string Source, ScadaExprNode Node);

/// <summary>One problem found on a parameter-sheet line, 1-based.</summary>
public sealed record ParameterSheetDiagnostic(int Line, string Message);

/// <summary>The total, deterministic result of parsing one parameter-sheet text.</summary>
public sealed record ParameterSheetParseResult(
    IReadOnlyDictionary<int, ParameterEntry> Entries,
    IReadOnlyList<ParameterSheetDiagnostic> Diagnostics)
{
    /// <summary>Gets whether the text produced no diagnostic.</summary>
    public bool IsValid => Diagnostics.Count == 0;
}
