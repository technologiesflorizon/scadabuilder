using System.Text.RegularExpressions;

namespace ScadaBuilderV2.Domain.QuickWindows;

/// <summary>One `#…` token found in a displayed text; <see cref="Number"/> is null for an invalid token.</summary>
public sealed record QuickWindowPlaceholderToken(string Raw, int Index, int? Number);

/// <summary>Recognises `#N` placeholder tokens of a quick-window page.</summary>
/// <remarks>
/// A token is `#` followed by 1 to 3 digits and not by a further digit; `\#` escapes it. Tokens are only
/// searched in texts and binding fields, never in styles: `#0F2A30` is a colour.
///
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §4.4.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPlaceholderTests.cs.
/// </remarks>
public static partial class QuickWindowPlaceholder
{
    /// <summary>Returns whether <paramref name="value"/> is exactly one valid token, `#1` to `#999`.</summary>
    public static bool TryParseName(string? value, out int number)
    {
        number = 0;
        if (value is null || !ExactRegex().IsMatch(value)) return false;
        number = int.Parse(value.AsSpan(1), System.Globalization.CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Finds every unescaped token of <paramref name="text"/>, valid or not.</summary>
    public static IReadOnlyList<QuickWindowPlaceholderToken> FindInText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        return TokenRegex().Matches(text)
            .Select(match => new QuickWindowPlaceholderToken(
                match.Value,
                match.Index,
                TryParseName(match.Value, out var number) ? number : null))
            .ToArray();
    }

    /// <summary>Turns every `\#` into a literal `#`.</summary>
    public static string UnescapeText(string text) => text.Replace(@"\#", "#", StringComparison.Ordinal);

    [GeneratedRegex(@"^#[1-9][0-9]{0,2}$", RegexOptions.CultureInvariant)]
    private static partial Regex ExactRegex();

    [GeneratedRegex(@"(?<!\\)#[0-9]{1,3}(?![0-9A-Fa-f])", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
