using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App;

/// <summary>
/// Pure composition of a `{...}` tag token and of the Variable-mode expression `ElementStateRuleDialog`
/// builds from it, extracted (no WPF usings) so the exact logic both it and `ExpressionCreationDialog` use
/// to turn a selected tag into expression source text can be locked by a WPF-free unit test.
/// </summary>
/// <remarks>
/// Fix round 2 (DEC-0052, bug report on `Popup_Fan_Condenseur`): both dialogs used to compose
/// `{tag.DisplayName}` directly. <see cref="QuickWindowPagePlaceholderCatalog"/>'s `#N` entries once put the
/// parameter sheet's source description in <c>DisplayName</c> (fix round 1), so selecting `#1` composed
/// `{#1 — {BP_1A_AUTO}}` - unparsable ("Caractere inattendu : '}'"). `DisplayName` now always equals
/// <see cref="ScadaTagDefinition.Id"/> for a placeholder entry, but this helper composes from
/// <see cref="ScadaTagDefinition.Id"/> directly regardless, so it stays correct even for an ordinary project
/// tag if that invariant were ever violated by mistake.
///
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5, §6.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ScadaTagExpressionComposerTests.cs.
/// </remarks>
public static class ScadaTagExpressionComposer
{
    /// <summary>Wraps <paramref name="tagId"/> as a `{...}` expression tag token.</summary>
    public static string WrapTagToken(string tagId) => $"{{{tagId}}}";

    /// <summary>Wraps <paramref name="tag"/>'s <see cref="ScadaTagDefinition.Id"/> as a `{...}` expression tag token.</summary>
    public static string WrapTagToken(ScadaTagDefinition tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return WrapTagToken(tag.Id);
    }

    /// <summary>
    /// Composes `ElementStateRuleDialog`'s Variable-mode expression: a lone tag token for a Boolean tag
    /// with no explicit true/false choice yet, `{tag} == true`/`{tag} == false` for a Boolean tag with one,
    /// or `{tag} operator value` otherwise (<paramref name="rawValue"/> defaults to `0` when blank, and a
    /// blank/absent <paramref name="comparisonOperator"/> - the "--" operator choice - yields the lone token).
    /// </summary>
    public static string ComposeVariableExpression(
        ScadaTagDefinition tag,
        bool isBooleanDatatype,
        bool? boolValue,
        string? comparisonOperator,
        string? rawValue)
    {
        var token = WrapTagToken(tag);

        if (isBooleanDatatype)
        {
            return boolValue is { } value ? $"{token} == {(value ? "true" : "false")}" : token;
        }

        if (string.IsNullOrEmpty(comparisonOperator))
        {
            return token;
        }

        var trimmedValue = rawValue?.Trim();
        var effectiveValue = string.IsNullOrWhiteSpace(trimmedValue) ? "0" : trimmedValue;
        return $"{token} {comparisonOperator} {effectiveValue}";
    }
}
