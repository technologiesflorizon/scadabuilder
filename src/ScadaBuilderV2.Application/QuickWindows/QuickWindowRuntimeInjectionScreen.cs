using System.Text.RegularExpressions;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;

namespace ScadaBuilderV2.Application.QuickWindows;

/// <summary>
/// Builder-side mirror of <c>_isInjection</c>/<c>_isAstInjection</c> in the shared runtime
/// (<c>src/ScadaBuilderV2.Rendering/Runtime/quick-window-runtime.js</c>), so a binding value the runtime would
/// refuse at mount (<c>injection-rejected</c>) is refused at validation instead.
/// </summary>
/// <remarks>
/// <see cref="Patterns"/> carries the JavaScript regular expressions exactly as written in the runtime's
/// <c>INJECTION_PATTERNS</c>; a test compares the two lists so they cannot drift apart.
///
/// Decisions: DEC-0052.
/// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §11b.1.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetLoweringTests.cs.
/// </remarks>
public static class QuickWindowRuntimeInjectionScreen
{
    /// <summary>The runtime's <c>INJECTION_PATTERNS</c>, as JavaScript regex literals, in the runtime's order.</summary>
    public static IReadOnlyList<string> Patterns { get; } =
    [
        @"/<\s*script/i",
        @"/<\s*\/\s*script/i",
        @"/javascript\s*:/i",
        @"/\.\.\//",
        @"/^\s*#/",
        @"/on[a-z]+\s*=/i"
    ];

    private static readonly Regex[] Compiled = Patterns.Select(ToRegex).ToArray();

    /// <summary>Returns the first runtime pattern <paramref name="value"/> matches, or null when the runtime accepts it.</summary>
    public static string? FindRejectingPattern(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        for (var i = 0; i < Compiled.Length; i++)
        {
            if (Compiled[i].IsMatch(value)) return Patterns[i];
        }

        return null;
    }

    /// <summary>
    /// Returns the first string an expression AST carries (a <c>tagRef</c> name or a <c>literalString</c> value, the
    /// strings <c>_isAstInjection</c> inspects) that the runtime would reject, with its pattern; null otherwise.
    /// </summary>
    public static (string Value, string Pattern)? FindRejectedAstString(ScadaExprNode? node)
    {
        switch (node)
        {
            case ScadaExprTagRef tag:
                return Check(tag.TagName);
            case ScadaExprLiteralString literal:
                return Check(literal.Value);
            case ScadaExprUnary unary:
                return FindRejectedAstString(unary.Operand);
            case ScadaExprBinary binary:
                return FindRejectedAstString(binary.Left) ?? FindRejectedAstString(binary.Right);
            case ScadaExprFunc func:
                foreach (var argument in func.Args ?? [])
                {
                    if (FindRejectedAstString(argument) is { } found) return found;
                }

                return null;
            default:
                return null;
        }
    }

    private static (string Value, string Pattern)? Check(string? value) =>
        FindRejectingPattern(value) is { } pattern ? (value!, pattern) : null;

    private static Regex ToRegex(string literal)
    {
        var closing = literal.LastIndexOf('/');
        var body = literal[1..closing];
        var flags = literal[(closing + 1)..];
        var options = RegexOptions.CultureInvariant;
        if (flags.Contains('i')) options |= RegexOptions.IgnoreCase;
        return new Regex(body, options);
    }
}
