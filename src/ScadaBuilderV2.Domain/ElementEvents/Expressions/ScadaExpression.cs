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
/// Serializes <see cref="ScadaExpression"/> with its source text, referenced tags, and
/// polymorphic AST. The AST is the runtime contract source of truth; <see cref="ScadaExpression.Source"/>
/// is kept for re-editing in the Builder UI.
/// </summary>
public sealed class ScadaExpressionConverter : JsonConverter<ScadaExpression>
{
    private sealed class WireDto
    {
        public string Source { get; set; } = "";
        public List<string> ReferencedTags { get; set; } = new();
        public ScadaExprNode? Ast { get; set; }
    }

    public override ScadaExpression? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dto = JsonSerializer.Deserialize<WireDto>(ref reader, options);
        if (dto is null || string.IsNullOrWhiteSpace(dto.Source))
            return null;
        // Prefer the deserialized AST if present (runtime round-trip);
        // otherwise fall back to re-parsing from source (legacy data).
        var ast = dto.Ast ?? ScadaExpressionParser.Parse(dto.Source).Root;
        return new ScadaExpression(dto.Source, ast, dto.ReferencedTags);
    }

    public override void Write(Utf8JsonWriter writer, ScadaExpression value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("source", value.Source);
        writer.WritePropertyName("referencedTags");
        JsonSerializer.Serialize(writer, value.ReferencedTags, options);
        if (value.Ast is not null)
        {
            writer.WritePropertyName("ast");
            JsonSerializer.Serialize(writer, value.Ast, options);
        }
        writer.WriteEndObject();
    }
}
