namespace ScadaBuilderV2.Domain.ElementEvents.Expressions;

/// <summary>
/// Stores one Element+ state condition as source text plus its parsed AST and referenced tag names.
/// The AST is the runtime contract source of truth; <see cref="Source"/> is kept for re-editing.
/// </summary>
/// <remarks>
/// Decisions: DEC-0036.
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionTests.cs.
/// </remarks>
public sealed record ScadaExpression(string Source, ScadaExprNode? Ast, IReadOnlyList<string> ReferencedTags)
{
    /// <summary>Parses <paramref name="source"/> and builds a <see cref="ScadaExpression"/>.</summary>
    public static ScadaExpression FromSource(string source)
    {
        var parseResult = ScadaExpressionParser.Parse(source);
        if (parseResult.Root is null)
        {
            return new ScadaExpression(source, null, Array.Empty<string>());
        }

        var tags = new List<string>();
        CollectTagRefs(parseResult.Root, tags);
        return new ScadaExpression(source, parseResult.Root, tags);
    }

    private static void CollectTagRefs(ScadaExprNode node, List<string> tags)
    {
        switch (node)
        {
            case ScadaExprTagRef tagRef:
                tags.Add(tagRef.TagName);
                break;
            case ScadaExprUnary unary:
                CollectTagRefs(unary.Operand, tags);
                break;
            case ScadaExprBinary binary:
                CollectTagRefs(binary.Left, tags);
                CollectTagRefs(binary.Right, tags);
                break;
            case ScadaExprFunc func:
                foreach (var arg in func.Args)
                {
                    CollectTagRefs(arg, tags);
                }

                break;
        }
    }
}
