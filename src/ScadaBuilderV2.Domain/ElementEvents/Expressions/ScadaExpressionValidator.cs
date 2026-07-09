using System.Linq;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Domain.ElementEvents.Expressions;

/// <summary>
/// Result of validating one Element+ state condition expression against the project tag catalog.
/// </summary>
public sealed record ScadaExprValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> ReferencedTagNames);

/// <summary>Outcome of resolving a tag reference against the project catalog.</summary>
public enum TagResolveStatus
{
    /// <summary>Exactly one match found; <see cref="TagResolveResult.CanonicalId"/> is set.</summary>
    Resolved,
    /// <summary>No match found in the catalog.</summary>
    Unresolved,
    /// <summary>Multiple tags match the same label; resolution is ambiguous.</summary>
    Ambiguous
}

/// <summary>Result of <see cref="ScadaExpressionValidator.TryResolveTagReference"/>.</summary>
public sealed record TagResolveResult(
    TagResolveStatus Status,
    string? CanonicalId,
    IReadOnlyList<string> Matches)
{
    public static TagResolveResult ForResolved(string canonicalId) =>
        new(TagResolveStatus.Resolved, canonicalId, new[] { canonicalId });

    public static TagResolveResult ForUnresolved() =>
        new(TagResolveStatus.Unresolved, null, Array.Empty<string>());

    public static TagResolveResult ForAmbiguous(IReadOnlyList<string> matches) =>
        new(TagResolveStatus.Ambiguous, null, matches);
}

/// <summary>
/// Validates Element+ state condition expressions: syntax, tag existence, function arity,
/// boolean root type, and literal division by zero.
/// </summary>
/// <remarks>
/// Decisions: DEC-0036.
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionValidatorTests.cs.
/// </remarks>
public static class ScadaExpressionValidator
{
    private static readonly Dictionary<string, int> FunctionArity = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ABS"] = 1,
        ["MIN"] = 2,
        ["MAX"] = 2,
        ["BIT"] = 2
    };

    /// <summary>
    /// Resolves a tag reference value (from <c>{...}</c> in an expression) against the
    /// project tag catalog. Resolution order: exact <see cref="ScadaTagDefinition.Id"/>,
    /// then <see cref="ScadaTagDefinition.DisplayName"/>, then
    /// <see cref="ScadaTagDefinition.KeywordLabel"/>.
    /// </summary>
    /// <param name="value">The tag reference text (content between { and }).</param>
    /// <param name="catalog">The project tag catalog, or null to skip resolution.</param>
    public static TagResolveResult TryResolveTagReference(string value, ScadaTagCatalog? catalog)
    {
        if (string.IsNullOrWhiteSpace(value))
            return TagResolveResult.ForUnresolved();

        if (catalog?.Tags is null || catalog.Tags.Count == 0)
            return TagResolveResult.ForUnresolved();

        var tags = catalog.Tags;

        // 1. Exact match by Id (canonical — highest priority)
        var byId = tags.FirstOrDefault(t =>
            string.Equals(t.Id, value, StringComparison.Ordinal));
        if (byId is not null)
            return TagResolveResult.ForResolved(byId.Id);

        // 2. Match by DisplayName
        var byDisplayName = tags
            .Where(t => string.Equals(t.DisplayName, value, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (byDisplayName.Length == 1)
            return TagResolveResult.ForResolved(byDisplayName[0].Id);
        if (byDisplayName.Length > 1)
            return TagResolveResult.ForAmbiguous(
                byDisplayName.Select(t => t.Id).ToArray());

        // 3. Match by KeywordLabel
        var byKeyword = tags
            .Where(t => t.KeywordLabel is not null &&
                        string.Equals(t.KeywordLabel, value, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (byKeyword.Length == 1)
            return TagResolveResult.ForResolved(byKeyword[0].Id);
        if (byKeyword.Length > 1)
            return TagResolveResult.ForAmbiguous(
                byKeyword.Select(t => t.Id).ToArray());

        return TagResolveResult.ForUnresolved();
    }

    /// <summary>Validates <paramref name="source"/> and reports every problem found.</summary>
    public static ScadaExprValidationResult Validate(string source, ScadaTagCatalog? tagCatalog)
    {
        var parseResult = ScadaExpressionParser.Parse(source);
        if (parseResult.Root is null)
        {
            return new ScadaExprValidationResult(false, parseResult.Errors, Array.Empty<string>());
        }

        var errors = new List<string>();
        var referencedTags = new List<string>();

        WalkAndValidate(parseResult.Root, errors, referencedTags, tagCatalog);

        if (!IsBooleanNode(parseResult.Root))
        {
            errors.Add("La condition doit s'evaluer en booleen (utilisez une comparaison ou un operateur logique a la racine).");
        }

        return new ScadaExprValidationResult(errors.Count == 0, errors, referencedTags);
    }

    private static void WalkAndValidate(
        ScadaExprNode node, List<string> errors, List<string> referencedTags,
        ScadaTagCatalog? catalog)
    {
        switch (node)
        {
            case ScadaExprTagRef tagRef:
                referencedTags.Add(tagRef.TagName);
                if (catalog is not null)
                {
                    var resolveResult = TryResolveTagReference(tagRef.TagName, catalog);
                    switch (resolveResult.Status)
                    {
                        case TagResolveStatus.Unresolved:
                            errors.Add($"Le tag '{tagRef.TagName}' n'existe pas dans le catalogue du projet.");
                            break;
                        case TagResolveStatus.Ambiguous:
                            errors.Add($"Le tag '{tagRef.TagName}' est ambigu : plusieurs tags correspondent " +
                                       $"({string.Join(", ", resolveResult.Matches)}). Utilisez l'Id canonique.");
                            break;
                    }
                }
                break;

            case ScadaExprUnary unary:
                WalkAndValidate(unary.Operand, errors, referencedTags, catalog);
                break;

            case ScadaExprBinary binary:
                WalkAndValidate(binary.Left, errors, referencedTags, catalog);
                WalkAndValidate(binary.Right, errors, referencedTags, catalog);
                if (binary.Op == ScadaExprBinaryOp.Divide && IsLiteralZero(binary.Right))
                {
                    errors.Add("Division par zero litterale detectee.");
                }
                break;

            case ScadaExprFunc func:
                if (!FunctionArity.TryGetValue(func.Name, out var expectedArity))
                {
                    errors.Add($"Fonction inconnue : '{func.Name}'. Fonctions supportees : ABS, MIN, MAX, BIT.");
                }
                else if (func.Args.Count != expectedArity)
                {
                    errors.Add($"La fonction '{func.Name}' attend {expectedArity} argument(s), {func.Args.Count} fourni(s).");
                }
                foreach (var arg in func.Args)
                {
                    WalkAndValidate(arg, errors, referencedTags, catalog);
                }
                break;
        }
    }

    private static bool IsLiteralZero(ScadaExprNode node) =>
        node is ScadaExprLiteralNumber number && number.Value == 0;

    private static bool IsBooleanNode(ScadaExprNode node) => node switch
    {
        ScadaExprLiteralBool => true,
        ScadaExprUnary unary => unary.Op == ScadaExprUnaryOp.Not,
        ScadaExprBinary binary => binary.Op is ScadaExprBinaryOp.Equal or ScadaExprBinaryOp.NotEqual
            or ScadaExprBinaryOp.LessThan or ScadaExprBinaryOp.LessThanOrEqual
            or ScadaExprBinaryOp.GreaterThan or ScadaExprBinaryOp.GreaterThanOrEqual
            or ScadaExprBinaryOp.And or ScadaExprBinaryOp.Or,
        _ => false
    };
}
