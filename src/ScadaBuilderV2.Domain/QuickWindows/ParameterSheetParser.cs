using System.Text.RegularExpressions;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;

namespace ScadaBuilderV2.Domain.QuickWindows;

/// <summary>Parses a parameter-sheet text into classified entries; total and pure.</summary>
/// <remarks>
/// A line is `#N := expression`, N from 1 to 999 without a leading zero. `//` starts a comment outside a
/// string literal. Blank lines are ignored and line order carries no meaning. A number defined twice is an
/// error rather than "last wins", which is what keeps the sheet idempotent.
///
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §4.3.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetParserTests.cs.
/// </remarks>
public static partial class ParameterSheetParser
{
    /// <summary>Parses <paramref name="text"/>; never throws.</summary>
    public static ParameterSheetParseResult Parse(string? text)
    {
        var entries = new Dictionary<int, ParameterEntry>();
        var diagnostics = new List<ParameterSheetDiagnostic>();
        var lines = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var content = StripComment(lines[index]).Trim();
            if (content.Length == 0) continue;

            var match = LineRegex().Match(content);
            if (!match.Success)
            {
                diagnostics.Add(new(lineNumber, "Ligne invalide : attendu « #N := valeur », N de 1 à 999 sans zéro de tête."));
                continue;
            }

            var number = int.Parse(match.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture);
            var source = match.Groups["rhs"].Value.Trim();
            var parsed = ScadaExpressionParser.Parse(source);
            if (parsed.Root is null)
            {
                diagnostics.Add(new(lineNumber, $"#{number} : {string.Join(" ", parsed.Errors)}"));
                continue;
            }

            var nested = FindPlaceholderReference(parsed.Root);
            if (nested is not null)
            {
                diagnostics.Add(new(lineNumber, $"#{number} ne peut pas référencer le paramètre {nested}."));
                continue;
            }

            if (entries.TryGetValue(number, out var first))
            {
                diagnostics.Add(new(lineNumber, $"#{number} est déjà défini à la ligne {first.Line}."));
                continue;
            }

            entries[number] = new ParameterEntry(number, lineNumber, Classify(parsed.Root), source, parsed.Root);
        }

        return new ParameterSheetParseResult(entries, diagnostics);
    }

    /// <summary>Classifies one parsed right-hand side (ruling R1).</summary>
    public static ParameterValueKind Classify(ScadaExprNode node) => node switch
    {
        ScadaExprTagRef => ParameterValueKind.Tag,
        ScadaExprLiteralString => ParameterValueKind.Literal,
        _ => ParameterValueKind.Expression
    };

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inString = !inString;
            else if (!inString && line[i] == '/' && i + 1 < line.Length && line[i + 1] == '/') return line[..i];
        }

        return line;
    }

    private static string? FindPlaceholderReference(ScadaExprNode node) => node switch
    {
        ScadaExprTagRef tag when AnyPlaceholderRegex().IsMatch(tag.TagName) => tag.TagName,
        ScadaExprUnary unary => FindPlaceholderReference(unary.Operand),
        ScadaExprBinary binary => FindPlaceholderReference(binary.Left) ?? FindPlaceholderReference(binary.Right),
        ScadaExprFunc func => func.Args.Select(FindPlaceholderReference).FirstOrDefault(found => found is not null),
        _ => null
    };

    [GeneratedRegex(@"^#(?<n>[1-9][0-9]{0,2})\s*:=\s*(?<rhs>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex LineRegex();

    [GeneratedRegex(@"^#[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AnyPlaceholderRegex();
}
