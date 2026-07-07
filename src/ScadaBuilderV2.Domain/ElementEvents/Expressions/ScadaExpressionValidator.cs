using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Domain.ElementEvents.Expressions;

/// <summary>
/// Result of validating one Element+ state condition expression against the project tag catalog.
/// </summary>
public sealed record ScadaExprValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> ReferencedTagNames);

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
        var knownTagNames = tagCatalog?.Tags
            .Select(tag => tag.DisplayName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Walk(parseResult.Root, errors, referencedTags, knownTagNames);

        if (!IsBooleanNode(parseResult.Root))
        {
            errors.Add("La condition doit s'evaluer en booleen (utilisez une comparaison ou un operateur logique a la racine).");
        }

        return new ScadaExprValidationResult(errors.Count == 0, errors, referencedTags);
    }

    private static void Walk(ScadaExprNode node, List<string> errors, List<string> referencedTags, HashSet<string>? knownTagNames)
    {
        switch (node)
        {
            case ScadaExprTagRef tagRef:
                referencedTags.Add(tagRef.TagName);
                if (knownTagNames is not null && !knownTagNames.Contains(tagRef.TagName))
                {
                    errors.Add($"Le tag '{tagRef.TagName}' n'existe pas dans le catalogue du projet.");
                }

                break;

            case ScadaExprUnary unary:
                Walk(unary.Operand, errors, referencedTags, knownTagNames);
                break;

            case ScadaExprBinary binary:
                Walk(binary.Left, errors, referencedTags, knownTagNames);
                Walk(binary.Right, errors, referencedTags, knownTagNames);
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
                    Walk(arg, errors, referencedTags, knownTagNames);
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
