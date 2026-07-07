using System.Text.Json;
using System.Text.Json.Serialization;

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
[JsonConverter(typeof(ScadaExpressionConverter))]
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

/// <summary>
/// Serializes <see cref="ScadaExpression"/> without the <see cref="ScadaExpression.Ast"/>,
/// which is a polymorphic cache reconstructed from <see cref="ScadaExpression.Source"/> on read.
/// </summary>
public sealed class ScadaExpressionConverter : JsonConverter<ScadaExpression>
{
    private sealed class WireDto
    {
        public string Source { get; set; } = "";
        public List<string> ReferencedTags { get; set; } = new();
    }

    public override ScadaExpression? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dto = JsonSerializer.Deserialize<WireDto>(ref reader, options);
        if (dto is null || string.IsNullOrWhiteSpace(dto.Source))
            return null;
        return ScadaExpression.FromSource(dto.Source);
    }

    public override void Write(Utf8JsonWriter writer, ScadaExpression value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Source", value.Source);
        writer.WritePropertyName("ReferencedTags");
        JsonSerializer.Serialize(writer, value.ReferencedTags, options);
        writer.WriteEndObject();
    }
}
