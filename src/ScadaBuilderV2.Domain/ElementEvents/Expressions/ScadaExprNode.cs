using System.Text.Json.Serialization;

namespace ScadaBuilderV2.Domain.ElementEvents.Expressions;

/// <summary>
/// Unary operators supported by the Element+ state condition expression grammar.
/// </summary>
public enum ScadaExprUnaryOp { Not, Negate }

/// <summary>
/// Binary operators supported by the Element+ state condition expression grammar.
/// </summary>
public enum ScadaExprBinaryOp
{
    Add, Subtract, Multiply, Divide, Modulo,
    Equal, NotEqual, LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual,
    And, Or
}

/// <summary>
/// Base type for one node of a parsed Element+ state condition expression AST.
/// This AST, not the source text, is the serialized runtime contract source of truth.
/// </summary>
/// <remarks>
/// Decisions: DEC-0036.
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExprNodeTests.cs.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ScadaExprLiteralNumber), "literalNumber")]
[JsonDerivedType(typeof(ScadaExprLiteralBool), "literalBool")]
[JsonDerivedType(typeof(ScadaExprLiteralString), "literalString")]
[JsonDerivedType(typeof(ScadaExprTagRef), "tagRef")]
[JsonDerivedType(typeof(ScadaExprUnary), "unary")]
[JsonDerivedType(typeof(ScadaExprBinary), "binary")]
[JsonDerivedType(typeof(ScadaExprFunc), "func")]
public abstract record ScadaExprNode;

/// <summary>Numeric literal expression node.</summary>
public sealed record ScadaExprLiteralNumber(double Value) : ScadaExprNode;

/// <summary>Boolean literal expression node.</summary>
public sealed record ScadaExprLiteralBool(bool Value) : ScadaExprNode;

/// <summary>String literal expression node.</summary>
public sealed record ScadaExprLiteralString(string Value) : ScadaExprNode;

/// <summary>References one project tag by name, e.g. <c>{Temp}</c>.</summary>
public sealed record ScadaExprTagRef(string TagName) : ScadaExprNode;

/// <summary>Unary operator expression node.</summary>
public sealed record ScadaExprUnary(ScadaExprUnaryOp Op, ScadaExprNode Operand) : ScadaExprNode;

/// <summary>Binary operator expression node.</summary>
public sealed record ScadaExprBinary(ScadaExprBinaryOp Op, ScadaExprNode Left, ScadaExprNode Right) : ScadaExprNode;

/// <summary>Function call expression node, e.g. <c>BIT(Status, 3)</c>.</summary>
public sealed record ScadaExprFunc(string Name, IReadOnlyList<ScadaExprNode> Args) : ScadaExprNode;
