# Element+ — Événements d'affichage d'état & de commande — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remplacer le système d'events Element+ actuel (classes CSS figées, conditions limitées à la visibilité) par deux systèmes découplés : un onglet **Événement d'affichage d'état** (liste ordonnée d'états pilotés par expressions, first-match-wins, effets riches cumulables) et un onglet **Événement de commande** (écriture tag à 4 modes + navigation/popup/URL/retour), avec un mini-parser d'expression maison.

**Architecture:** Nouveau namespace `ScadaBuilderV2.Domain.ElementEvents` (State/Command/Expressions), immutable records suivant le pattern `With...` existant de `ScadaScene`. Deux champs optionnels neufs sur `ScadaElement` (`StateConfig`, `CommandConfig`). Le parser d'expression produit un AST sérialisable consommé par un validateur (authoring uniquement — pas d'évaluateur runtime en C# dans cette itération). UI : liste réordonnable dans le panneau Propriétés + fenêtre modale d'édition par item (pattern `ElementEventDialog`), aperçu statique, bouton Test toggle.

**Tech Stack:** .NET 8 / WPF (`ScadaBuilderV2.App`), C# records immutables (`ScadaBuilderV2.Domain`), MSTest (`tests/ScadaBuilderV2.Tests`).

## Global Constraints

- Namespace neuf : `ScadaBuilderV2.Domain.ElementEvents` (sous-namespaces `State`, `Command`, `Expressions`). `Domain` ne référence aucun autre projet.
- Aucune migration de données : pas de projet `.sb2`/`.sep` en production. L'ancien modèle est supprimé, pas convertie.
- `ScadaActionKind.SetClass/RemoveClass/ToggleClass` et `ScadaActionKind.WriteTag` (legacy) sont **supprimés** du modèle — pas de compat shim.
- Toute mutation de `ScadaScene`/`ScadaElement` passe par une méthode `With...` immutable (jamais de mutation en place), suivant le pattern déjà établi (`WithObjectEvent`, `WithValueBinding`).
- Toute API publique porte un XML doc `<summary>` ; le code sensible au contrat cite `Decisions:`/`Contracts:`/`Tests:` en `<remarks>` (voir exemples dans `ScadaSceneModels.cs`).
- Défaut `QualityFallback` : `Opacity = 0.4`, `BorderColor = "#000000"`, `BorderWidth = 2`. Défaut `DefaultEffect` : vide (aucune propriété fixée, `null` partout).
- Grammaire d'expression V1 : littéraux, `{tag}`, unaires `! -`, binaires `+ - * / % == != < <= > >= && ||`, fonctions `ABS(x) MIN(a,b) MAX(a,b) BIT(tag,n)`. Aucune autre fonction.
- Une seule `ScadaAnimation` par état (`None|Blink|Pulse|Halo|Spin`).
- Le contrat runtime (`docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md`) et la dépréciation de `ACTIONS_EVENTS_CONTRACT_V2.md` sont rédigés dans ce plan, mais **aucune** implémentation TF100Web n'est faite.
- Documentation : `docs/README.md` §4 / decision register — toute décision nouvelle s'enregistre comme `DEC-xxxx` (numéro suivant celui du plus grand DEC existant à trouver au moment de l'implémentation).
- Chaque tâche se termine par un commit (voir Working conventions du `CLAUDE.md` : worktree propre avant de commencer, commit après chaque tâche validée).

---

## Task 1: Modèle d'effets — `ScadaEffectBlock` + `ScadaAnimation`

**Files:**
- Create: `src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaEffectBlock.cs`
- Test: `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaEffectBlockTests.cs`

**Interfaces:**
- Produces: `namespace ScadaBuilderV2.Domain.ElementEvents.State` — `enum ScadaAnimation { None, Blink, Pulse, Halo, Spin }` et `sealed record ScadaEffectBlock(string? BackgroundColor = null, string? BorderColor = null, double? BorderWidth = null, string? TextColor = null, string? TextContent = null, bool? TextVisible = null, bool? ElementVisible = null, double? Opacity = null, double? Rotation = null, ScadaAnimation? Animation = null)` avec propriété statique `ScadaEffectBlock.Empty` (toutes propriétés `null`).

- [ ] **Step 1: Write the failing test**

```csharp
using ScadaBuilderV2.Domain.ElementEvents.State;

namespace ScadaBuilderV2.Tests.ElementEvents;

[TestClass]
public sealed class ScadaEffectBlockTests
{
    [TestMethod]
    public void EmptyEffectBlockHasAllNullProperties()
    {
        var effect = ScadaEffectBlock.Empty;

        Assert.IsNull(effect.BackgroundColor);
        Assert.IsNull(effect.BorderColor);
        Assert.IsNull(effect.BorderWidth);
        Assert.IsNull(effect.TextColor);
        Assert.IsNull(effect.TextContent);
        Assert.IsNull(effect.TextVisible);
        Assert.IsNull(effect.ElementVisible);
        Assert.IsNull(effect.Opacity);
        Assert.IsNull(effect.Rotation);
        Assert.IsNull(effect.Animation);
    }

    [TestMethod]
    public void EffectBlockRecordSupportsWithExpressionOverrides()
    {
        var baseline = ScadaEffectBlock.Empty with { BackgroundColor = "#00FF00" };
        var updated = baseline with { Animation = ScadaAnimation.Blink };

        Assert.AreEqual("#00FF00", updated.BackgroundColor);
        Assert.AreEqual(ScadaAnimation.Blink, updated.Animation);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaEffectBlockTests"`
Expected: FAIL (compilation error — `ScadaBuilderV2.Domain.ElementEvents.State` namespace does not exist yet).

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace ScadaBuilderV2.Domain.ElementEvents.State;

/// <summary>
/// Single cumulative animation applied by an Element+ state effect.
/// </summary>
public enum ScadaAnimation
{
    None,
    Blink,
    Pulse,
    Halo,
    Spin
}

/// <summary>
/// Describes an optional, cumulative set of visual overrides applied when an Element+ state matches.
/// Every property is optional; a null property leaves the design-time appearance unchanged.
/// </summary>
/// <remarks>
/// Decisions: DEC-PENDING.
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaEffectBlockTests.cs.
/// </remarks>
public sealed record ScadaEffectBlock(
    string? BackgroundColor = null,
    string? BorderColor = null,
    double? BorderWidth = null,
    string? TextColor = null,
    string? TextContent = null,
    bool? TextVisible = null,
    bool? ElementVisible = null,
    double? Opacity = null,
    double? Rotation = null,
    ScadaAnimation? Animation = null)
{
    /// <summary>
    /// Gets an effect block with every property unset.
    /// </summary>
    public static ScadaEffectBlock Empty { get; } = new();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaEffectBlockTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaEffectBlock.cs tests/ScadaBuilderV2.Tests/ElementEvents/ScadaEffectBlockTests.cs
git commit -m "feat: add ScadaEffectBlock and ScadaAnimation for Element+ state effects"
```

---

## Task 2: AST d'expression — `ScadaExprNode`

**Files:**
- Create: `src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExprNode.cs`
- Test: `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExprNodeTests.cs`

**Interfaces:**
- Produces: `namespace ScadaBuilderV2.Domain.ElementEvents.Expressions` — abstract record `ScadaExprNode` avec 5 dérivées : `ScadaExprLiteralNumber(double Value)`, `ScadaExprLiteralBool(bool Value)`, `ScadaExprLiteralString(string Value)`, `ScadaExprTagRef(string TagName)`, `ScadaExprUnary(ScadaExprUnaryOp Op, ScadaExprNode Operand)`, `ScadaExprBinary(ScadaExprBinaryOp Op, ScadaExprNode Left, ScadaExprNode Right)`, `ScadaExprFunc(string Name, IReadOnlyList<ScadaExprNode> Args)`. Enums `ScadaExprUnaryOp { Not, Negate }`, `ScadaExprBinaryOp { Add, Subtract, Multiply, Divide, Modulo, Equal, NotEqual, LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual, And, Or }`.

- [ ] **Step 1: Write the failing test**

```csharp
using ScadaBuilderV2.Domain.ElementEvents.Expressions;

namespace ScadaBuilderV2.Tests.ElementEvents;

[TestClass]
public sealed class ScadaExprNodeTests
{
    [TestMethod]
    public void BinaryNodeHoldsOperatorAndOperands()
    {
        ScadaExprNode left = new ScadaExprTagRef("Temp");
        ScadaExprNode right = new ScadaExprLiteralNumber(80);
        var node = new ScadaExprBinary(ScadaExprBinaryOp.GreaterThanOrEqual, left, right);

        Assert.AreEqual(ScadaExprBinaryOp.GreaterThanOrEqual, node.Op);
        Assert.AreEqual("Temp", ((ScadaExprTagRef)node.Left).TagName);
        Assert.AreEqual(80, ((ScadaExprLiteralNumber)node.Right).Value);
    }

    [TestMethod]
    public void FuncNodeHoldsNameAndArguments()
    {
        var node = new ScadaExprFunc("BIT", new ScadaExprNode[] { new ScadaExprTagRef("Status"), new ScadaExprLiteralNumber(3) });

        Assert.AreEqual("BIT", node.Name);
        Assert.AreEqual(2, node.Args.Count);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaExprNodeTests"`
Expected: FAIL (namespace/types don't exist).

- [ ] **Step 3: Write minimal implementation**

```csharp
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
/// Decisions: DEC-PENDING.
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExprNodeTests.cs.
/// </remarks>
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaExprNodeTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExprNode.cs tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExprNodeTests.cs
git commit -m "feat: add ScadaExprNode AST hierarchy for state condition expressions"
```

---

## Task 3: Parser d'expression — `ScadaExpressionParser`

**Files:**
- Create: `src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpressionParser.cs`
- Test: `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionParserTests.cs`

**Interfaces:**
- Consumes: `ScadaExprNode` hierarchy from Task 2 (`ScadaExprLiteralNumber`, `ScadaExprLiteralBool`, `ScadaExprTagRef`, `ScadaExprUnary`, `ScadaExprBinary`, `ScadaExprFunc`, `ScadaExprUnaryOp`, `ScadaExprBinaryOp`).
- Produces: `static class ScadaExpressionParser` with `static ScadaExprParseResult Parse(string source)`. `sealed record ScadaExprParseResult(ScadaExprNode? Root, IReadOnlyList<string> Errors)` — `Errors` empty and `Root` non-null on success; `Root` null and `Errors` populated on syntax failure. Precedence (low→high): `||`, `&&`, equality (`== !=`), relational (`< <= > >=`), additive (`+ -`), multiplicative (`* / %`), unary (`! -`), primary (literals, `{tag}`, `FUNC(args)`, parens).

- [ ] **Step 1: Write the failing test**

```csharp
using ScadaBuilderV2.Domain.ElementEvents.Expressions;

namespace ScadaBuilderV2.Tests.ElementEvents;

[TestClass]
public sealed class ScadaExpressionParserTests
{
    [TestMethod]
    public void ParsesSimpleComparison()
    {
        var result = ScadaExpressionParser.Parse("{Temp} >= 80");

        Assert.AreEqual(0, result.Errors.Count);
        var binary = (ScadaExprBinary)result.Root!;
        Assert.AreEqual(ScadaExprBinaryOp.GreaterThanOrEqual, binary.Op);
        Assert.AreEqual("Temp", ((ScadaExprTagRef)binary.Left).TagName);
        Assert.AreEqual(80, ((ScadaExprLiteralNumber)binary.Right).Value);
    }

    [TestMethod]
    public void RespectsOperatorPrecedenceForArithmeticAndLogical()
    {
        var result = ScadaExpressionParser.Parse("({Temp} * 1.8 / {Flow}) && {Run}");

        Assert.AreEqual(0, result.Errors.Count);
        var and = (ScadaExprBinary)result.Root!;
        Assert.AreEqual(ScadaExprBinaryOp.And, and.Op);
        var divide = (ScadaExprBinary)and.Left;
        Assert.AreEqual(ScadaExprBinaryOp.Divide, divide.Op);
        var multiply = (ScadaExprBinary)divide.Left;
        Assert.AreEqual(ScadaExprBinaryOp.Multiply, multiply.Op);
        Assert.IsInstanceOfType(and.Right, typeof(ScadaExprTagRef));
    }

    [TestMethod]
    public void ParsesFunctionCallWithMultipleArguments()
    {
        var result = ScadaExpressionParser.Parse("BIT({Status}, 3)");

        Assert.AreEqual(0, result.Errors.Count);
        var func = (ScadaExprFunc)result.Root!;
        Assert.AreEqual("BIT", func.Name);
        Assert.AreEqual(2, func.Args.Count);
    }

    [TestMethod]
    public void ReturnsErrorForUnbalancedParentheses()
    {
        var result = ScadaExpressionParser.Parse("({Temp} > 80");

        Assert.IsNull(result.Root);
        Assert.IsTrue(result.Errors.Count > 0);
    }

    [TestMethod]
    public void ReturnsErrorForEmptySource()
    {
        var result = ScadaExpressionParser.Parse("   ");

        Assert.IsNull(result.Root);
        Assert.IsTrue(result.Errors.Count > 0);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaExpressionParserTests"`
Expected: FAIL (`ScadaExpressionParser` does not exist).

- [ ] **Step 3: Write minimal implementation**

```csharp
using System.Globalization;
using System.Text;

namespace ScadaBuilderV2.Domain.ElementEvents.Expressions;

/// <summary>
/// Result of parsing one Element+ state condition expression source string into an AST.
/// </summary>
public sealed record ScadaExprParseResult(ScadaExprNode? Root, IReadOnlyList<string> Errors)
{
    /// <summary>Creates a successful parse result.</summary>
    public static ScadaExprParseResult Success(ScadaExprNode root) => new(root, Array.Empty<string>());

    /// <summary>Creates a failed parse result carrying one or more error messages.</summary>
    public static ScadaExprParseResult Failure(params string[] errors) => new(null, errors);
}

/// <summary>
/// Parses Element+ state condition expression source text into a <see cref="ScadaExprNode"/> AST.
/// Grammar: literals, <c>{tag}</c> references, unary <c>! -</c>, binary
/// <c>+ - * / % == != &lt; &lt;= &gt; &gt;= &amp;&amp; ||</c>, and functions <c>ABS/MIN/MAX/BIT</c>.
/// </summary>
/// <remarks>
/// Decisions: DEC-PENDING.
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionParserTests.cs.
/// </remarks>
public static class ScadaExpressionParser
{
    private enum TokenKind { Number, String, Ident, TagRef, Op, LParen, RParen, Comma, End }

    private sealed record Token(TokenKind Kind, string Text);

    /// <summary>Parses <paramref name="source"/> into an AST, or returns syntax errors.</summary>
    public static ScadaExprParseResult Parse(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return ScadaExprParseResult.Failure("Expression vide.");
        }

        List<Token> tokens;
        try
        {
            tokens = Tokenize(source);
        }
        catch (FormatException ex)
        {
            return ScadaExprParseResult.Failure(ex.Message);
        }

        var cursor = new Cursor(tokens);
        try
        {
            var node = ParseOr(cursor);
            if (cursor.Current.Kind != TokenKind.End)
            {
                return ScadaExprParseResult.Failure($"Jeton inattendu : '{cursor.Current.Text}'.");
            }

            return ScadaExprParseResult.Success(node);
        }
        catch (FormatException ex)
        {
            return ScadaExprParseResult.Failure(ex.Message);
        }
    }

    private sealed class Cursor(List<Token> tokens)
    {
        private int _index;
        public Token Current => tokens[_index];
        public Token Advance() => tokens[_index++];
    }

    private static List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '{')
            {
                var end = source.IndexOf('}', i + 1);
                if (end < 0) throw new FormatException("Accolade '{' non refermee.");
                tokens.Add(new Token(TokenKind.TagRef, source.Substring(i + 1, end - i - 1).Trim()));
                i = end + 1;
                continue;
            }

            if (c == '"')
            {
                var end = source.IndexOf('"', i + 1);
                if (end < 0) throw new FormatException("Chaine non refermee.");
                tokens.Add(new Token(TokenKind.String, source.Substring(i + 1, end - i - 1)));
                i = end + 1;
                continue;
            }

            if (char.IsDigit(c))
            {
                var start = i;
                while (i < source.Length && (char.IsDigit(source[i]) || source[i] == '.')) i++;
                tokens.Add(new Token(TokenKind.Number, source[start..i]));
                continue;
            }

            if (char.IsLetter(c))
            {
                var start = i;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) i++;
                tokens.Add(new Token(TokenKind.Ident, source[start..i]));
                continue;
            }

            if (c == '(') { tokens.Add(new Token(TokenKind.LParen, "(")); i++; continue; }
            if (c == ')') { tokens.Add(new Token(TokenKind.RParen, ")")); i++; continue; }
            if (c == ',') { tokens.Add(new Token(TokenKind.Comma, ",")); i++; continue; }

            if (c == '&' && i + 1 < source.Length && source[i + 1] == '&') { tokens.Add(new Token(TokenKind.Op, "&&")); i += 2; continue; }
            if (c == '|' && i + 1 < source.Length && source[i + 1] == '|') { tokens.Add(new Token(TokenKind.Op, "||")); i += 2; continue; }
            if (c == '=' && i + 1 < source.Length && source[i + 1] == '=') { tokens.Add(new Token(TokenKind.Op, "==")); i += 2; continue; }
            if (c == '!' && i + 1 < source.Length && source[i + 1] == '=') { tokens.Add(new Token(TokenKind.Op, "!=")); i += 2; continue; }
            if (c == '<' && i + 1 < source.Length && source[i + 1] == '=') { tokens.Add(new Token(TokenKind.Op, "<=")); i += 2; continue; }
            if (c == '>' && i + 1 < source.Length && source[i + 1] == '=') { tokens.Add(new Token(TokenKind.Op, ">=")); i += 2; continue; }

            if ("+-*/%<>!".IndexOf(c) >= 0) { tokens.Add(new Token(TokenKind.Op, c.ToString())); i++; continue; }

            throw new FormatException($"Caractere inattendu : '{c}'.");
        }

        tokens.Add(new Token(TokenKind.End, string.Empty));
        return tokens;
    }

    private static ScadaExprNode ParseOr(Cursor cursor)
    {
        var left = ParseAnd(cursor);
        while (cursor.Current.Kind == TokenKind.Op && cursor.Current.Text == "||")
        {
            cursor.Advance();
            left = new ScadaExprBinary(ScadaExprBinaryOp.Or, left, ParseAnd(cursor));
        }

        return left;
    }

    private static ScadaExprNode ParseAnd(Cursor cursor)
    {
        var left = ParseEquality(cursor);
        while (cursor.Current.Kind == TokenKind.Op && cursor.Current.Text == "&&")
        {
            cursor.Advance();
            left = new ScadaExprBinary(ScadaExprBinaryOp.And, left, ParseEquality(cursor));
        }

        return left;
    }

    private static ScadaExprNode ParseEquality(Cursor cursor)
    {
        var left = ParseRelational(cursor);
        while (cursor.Current.Kind == TokenKind.Op && (cursor.Current.Text == "==" || cursor.Current.Text == "!="))
        {
            var op = cursor.Advance().Text == "==" ? ScadaExprBinaryOp.Equal : ScadaExprBinaryOp.NotEqual;
            left = new ScadaExprBinary(op, left, ParseRelational(cursor));
        }

        return left;
    }

    private static ScadaExprNode ParseRelational(Cursor cursor)
    {
        var left = ParseAdditive(cursor);
        while (cursor.Current.Kind == TokenKind.Op && cursor.Current.Text is "<" or "<=" or ">" or ">=")
        {
            var opText = cursor.Advance().Text;
            var op = opText switch
            {
                "<" => ScadaExprBinaryOp.LessThan,
                "<=" => ScadaExprBinaryOp.LessThanOrEqual,
                ">" => ScadaExprBinaryOp.GreaterThan,
                _ => ScadaExprBinaryOp.GreaterThanOrEqual
            };
            left = new ScadaExprBinary(op, left, ParseAdditive(cursor));
        }

        return left;
    }

    private static ScadaExprNode ParseAdditive(Cursor cursor)
    {
        var left = ParseMultiplicative(cursor);
        while (cursor.Current.Kind == TokenKind.Op && cursor.Current.Text is "+" or "-")
        {
            var op = cursor.Advance().Text == "+" ? ScadaExprBinaryOp.Add : ScadaExprBinaryOp.Subtract;
            left = new ScadaExprBinary(op, left, ParseMultiplicative(cursor));
        }

        return left;
    }

    private static ScadaExprNode ParseMultiplicative(Cursor cursor)
    {
        var left = ParseUnary(cursor);
        while (cursor.Current.Kind == TokenKind.Op && cursor.Current.Text is "*" or "/" or "%")
        {
            var op = cursor.Advance().Text switch { "*" => ScadaExprBinaryOp.Multiply, "/" => ScadaExprBinaryOp.Divide, _ => ScadaExprBinaryOp.Modulo };
            left = new ScadaExprBinary(op, left, ParseUnary(cursor));
        }

        return left;
    }

    private static ScadaExprNode ParseUnary(Cursor cursor)
    {
        if (cursor.Current.Kind == TokenKind.Op && cursor.Current.Text == "!")
        {
            cursor.Advance();
            return new ScadaExprUnary(ScadaExprUnaryOp.Not, ParseUnary(cursor));
        }

        if (cursor.Current.Kind == TokenKind.Op && cursor.Current.Text == "-")
        {
            cursor.Advance();
            return new ScadaExprUnary(ScadaExprUnaryOp.Negate, ParseUnary(cursor));
        }

        return ParsePrimary(cursor);
    }

    private static ScadaExprNode ParsePrimary(Cursor cursor)
    {
        var token = cursor.Current;

        if (token.Kind == TokenKind.Number)
        {
            cursor.Advance();
            return new ScadaExprLiteralNumber(double.Parse(token.Text, CultureInfo.InvariantCulture));
        }

        if (token.Kind == TokenKind.String)
        {
            cursor.Advance();
            return new ScadaExprLiteralString(token.Text);
        }

        if (token.Kind == TokenKind.TagRef)
        {
            cursor.Advance();
            return new ScadaExprTagRef(token.Text);
        }

        if (token.Kind == TokenKind.Ident)
        {
            if (string.Equals(token.Text, "true", StringComparison.OrdinalIgnoreCase))
            {
                cursor.Advance();
                return new ScadaExprLiteralBool(true);
            }

            if (string.Equals(token.Text, "false", StringComparison.OrdinalIgnoreCase))
            {
                cursor.Advance();
                return new ScadaExprLiteralBool(false);
            }

            cursor.Advance();
            if (cursor.Current.Kind == TokenKind.LParen)
            {
                cursor.Advance();
                var args = new List<ScadaExprNode>();
                if (cursor.Current.Kind != TokenKind.RParen)
                {
                    args.Add(ParseOr(cursor));
                    while (cursor.Current.Kind == TokenKind.Comma)
                    {
                        cursor.Advance();
                        args.Add(ParseOr(cursor));
                    }
                }

                if (cursor.Current.Kind != TokenKind.RParen)
                {
                    throw new FormatException($"Parenthese fermante attendue apres les arguments de '{token.Text}'.");
                }

                cursor.Advance();
                return new ScadaExprFunc(token.Text.ToUpperInvariant(), args);
            }

            throw new FormatException($"Identifiant inattendu : '{token.Text}'. Utilisez {{tag}} pour reference un tag.");
        }

        if (token.Kind == TokenKind.LParen)
        {
            cursor.Advance();
            var inner = ParseOr(cursor);
            if (cursor.Current.Kind != TokenKind.RParen)
            {
                throw new FormatException("Parenthese fermante ')' manquante.");
            }

            cursor.Advance();
            return inner;
        }

        throw new FormatException(token.Kind == TokenKind.End
            ? "Fin d'expression inattendue."
            : $"Jeton inattendu : '{token.Text}'.");
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaExpressionParserTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpressionParser.cs tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionParserTests.cs
git commit -m "feat: add ScadaExpressionParser for state condition expressions"
```

---

## Task 4: Validateur d'expression — `ScadaExpressionValidator`

**Files:**
- Create: `src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpressionValidator.cs`
- Test: `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionValidatorTests.cs`

**Interfaces:**
- Consumes: `ScadaExprNode` hierarchy (Task 2), `ScadaExprParseResult`/`ScadaExpressionParser.Parse` (Task 3), `ScadaTagCatalog`/`ScadaTagDefinition` from `ScadaBuilderV2.Domain.Projects` (existing).
- Produces: `static class ScadaExpressionValidator` with `static ScadaExprValidationResult Validate(string source, ScadaTagCatalog? tagCatalog)`. `sealed record ScadaExprValidationResult(bool IsValid, IReadOnlyList<string> Errors, IReadOnlyList<string> ReferencedTagNames)`. Validates: parse succeeds; every `{tag}` name exists in `tagCatalog.Tags` (by `DisplayName`, case-insensitive) when `tagCatalog` is not null; function arity (`ABS`=1, `MIN`=2, `MAX`=2, `BIT`=2) and known function names only; root node's static type resolves to boolean (see type-inference rule below); literal division by zero (`x / 0` where right operand is a `ScadaExprLiteralNumber` equal to 0) is a validation error.

Type-inference rule for the "root must be boolean" check: a node is boolean if it is `ScadaExprLiteralBool`, or `ScadaExprUnary` with `Op == Not`, or `ScadaExprBinary` with `Op` in `{Equal, NotEqual, LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual, And, Or}`. Anything else (arithmetic result, bare tag ref, bare literal number/string, `BIT`/`ABS`/`MIN`/`MAX` call) is **not** boolean at the root.

- [ ] **Step 1: Write the failing test**

```csharp
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Tests.ElementEvents;

[TestClass]
public sealed class ScadaExpressionValidatorTests
{
    private static ScadaTagCatalog CreateCatalog() => new(
        "tf100web-scada-tags-v1",
        new[]
        {
            new ScadaTagDefinition("tag-temp", "Temp", Datatype: "float"),
            new ScadaTagDefinition("tag-run", "Run", Datatype: "bool")
        });

    [TestMethod]
    public void ValidBooleanComparisonPasses()
    {
        var result = ScadaExpressionValidator.Validate("{Temp} >= 80", CreateCatalog());

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(0, result.Errors.Count);
        CollectionAssert.Contains(result.ReferencedTagNames.ToList(), "Temp");
    }

    [TestMethod]
    public void UnknownTagNameFailsValidation()
    {
        var result = ScadaExpressionValidator.Validate("{Unknown} > 1", CreateCatalog());

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Unknown")));
    }

    [TestMethod]
    public void NonBooleanRootFailsValidation()
    {
        var result = ScadaExpressionValidator.Validate("{Temp} * 1.8", CreateCatalog());

        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void LiteralDivisionByZeroFailsValidation()
    {
        var result = ScadaExpressionValidator.Validate("({Temp} / 0) > 1", CreateCatalog());

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("zero", StringComparison.OrdinalIgnoreCase) || e.Contains("zéro")));
    }

    [TestMethod]
    public void UnknownFunctionNameFailsValidation()
    {
        var result = ScadaExpressionValidator.Validate("ROUND({Temp}) > 1", CreateCatalog());

        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void WrongArityForBitFailsValidation()
    {
        var result = ScadaExpressionValidator.Validate("BIT({Temp}) == true", CreateCatalog());

        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void NullTagCatalogSkipsTagExistenceCheck()
    {
        var result = ScadaExpressionValidator.Validate("{AnyTag} == true", null);

        Assert.IsTrue(result.IsValid);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaExpressionValidatorTests"`
Expected: FAIL (`ScadaExpressionValidator` does not exist).

- [ ] **Step 3: Write minimal implementation**

```csharp
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
/// Decisions: DEC-PENDING.
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaExpressionValidatorTests"`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpressionValidator.cs tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionValidatorTests.cs
git commit -m "feat: add ScadaExpressionValidator for tag existence, arity, and boolean-root checks"
```

---

## Task 5: `ScadaExpression` wrapper (source + AST + tags référencés)

**Files:**
- Create: `src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpression.cs`
- Test: `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionTests.cs`

**Interfaces:**
- Consumes: `ScadaExprNode` (Task 2), `ScadaExpressionParser.Parse` (Task 3).
- Produces: `sealed record ScadaExpression(string Source, ScadaExprNode? Ast, IReadOnlyList<string> ReferencedTags)` with static factory `ScadaExpression.FromSource(string source)` that parses `source` via `ScadaExpressionParser.Parse`, extracts referenced tag names by walking the resulting AST (empty list if parse failed), and returns the record. This is the type stored on `ScadaStateRule.Expression` (Task 6).

- [ ] **Step 1: Write the failing test**

```csharp
using ScadaBuilderV2.Domain.ElementEvents.Expressions;

namespace ScadaBuilderV2.Tests.ElementEvents;

[TestClass]
public sealed class ScadaExpressionTests
{
    [TestMethod]
    public void FromSourceParsesAndExtractsReferencedTags()
    {
        var expression = ScadaExpression.FromSource("{Temp} >= 80 && {Run}");

        Assert.AreEqual("{Temp} >= 80 && {Run}", expression.Source);
        Assert.IsNotNull(expression.Ast);
        CollectionAssert.AreEquivalent(new[] { "Temp", "Run" }, expression.ReferencedTags.ToList());
    }

    [TestMethod]
    public void FromSourceWithSyntaxErrorHasNullAstAndEmptyTags()
    {
        var expression = ScadaExpression.FromSource("{Temp} >");

        Assert.IsNull(expression.Ast);
        Assert.AreEqual(0, expression.ReferencedTags.Count);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaExpressionTests"`
Expected: FAIL (`ScadaExpression` does not exist).

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace ScadaBuilderV2.Domain.ElementEvents.Expressions;

/// <summary>
/// Stores one Element+ state condition as source text plus its parsed AST and referenced tag names.
/// The AST is the runtime contract source of truth; <see cref="Source"/> is kept for re-editing.
/// </summary>
/// <remarks>
/// Decisions: DEC-PENDING.
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaExpressionTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpression.cs tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionTests.cs
git commit -m "feat: add ScadaExpression wrapper combining source, AST, and referenced tags"
```

---

## Task 6: Modèle d'état — `ScadaStateRule` + `ScadaElementStateConfig`

**Files:**
- Create: `src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaStateRule.cs`
- Create: `src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaElementStateConfig.cs`
- Test: `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementStateConfigTests.cs`

**Interfaces:**
- Consumes: `ScadaEffectBlock` (Task 1), `ScadaExpression` (Task 5).
- Produces: `sealed record ScadaStateRule(string Id, string Name, bool Enabled, ScadaExpression Expression, ScadaEffectBlock Effect)`. `sealed record ScadaElementStateConfig(ScadaEffectBlock QualityFallback, ScadaEffectBlock DefaultEffect, IReadOnlyList<ScadaStateRule> States)` with static `ScadaElementStateConfig.Default` (QualityFallback = `Opacity 0.4, BorderColor "#000000", BorderWidth 2`; DefaultEffect = `ScadaEffectBlock.Empty`; States = empty list).

- [ ] **Step 1: Write the failing test**

```csharp
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;

namespace ScadaBuilderV2.Tests.ElementEvents;

[TestClass]
public sealed class ScadaElementStateConfigTests
{
    [TestMethod]
    public void DefaultConfigHasSensibleQualityFallbackAndEmptyStates()
    {
        var config = ScadaElementStateConfig.Default;

        Assert.AreEqual(0.4, config.QualityFallback.Opacity);
        Assert.AreEqual("#000000", config.QualityFallback.BorderColor);
        Assert.AreEqual(2, config.QualityFallback.BorderWidth);
        Assert.IsNull(config.DefaultEffect.BackgroundColor);
        Assert.AreEqual(0, config.States.Count);
    }

    [TestMethod]
    public void StateRuleCarriesNameExpressionAndEffect()
    {
        var rule = new ScadaStateRule(
            "state-1",
            "Alarme haute",
            Enabled: true,
            Expression: ScadaExpression.FromSource("{Temp} > 80"),
            Effect: ScadaEffectBlock.Empty with { BackgroundColor = "#E53935" });

        Assert.AreEqual("Alarme haute", rule.Name);
        Assert.IsTrue(rule.Enabled);
        Assert.AreEqual("#E53935", rule.Effect.BackgroundColor);
        CollectionAssert.Contains(rule.Expression.ReferencedTags.ToList(), "Temp");
    }

    [TestMethod]
    public void ConfigWithStatesPreservesListOrder()
    {
        var first = new ScadaStateRule("s1", "First", true, ScadaExpression.FromSource("{A} == true"), ScadaEffectBlock.Empty);
        var second = new ScadaStateRule("s2", "Second", true, ScadaExpression.FromSource("{B} == true"), ScadaEffectBlock.Empty);
        var config = ScadaElementStateConfig.Default with { States = new[] { first, second } };

        Assert.AreEqual("First", config.States[0].Name);
        Assert.AreEqual("Second", config.States[1].Name);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaElementStateConfigTests"`
Expected: FAIL (types don't exist).

- [ ] **Step 3: Write minimal implementation**

`ScadaStateRule.cs`:
```csharp
using ScadaBuilderV2.Domain.ElementEvents.Expressions;

namespace ScadaBuilderV2.Domain.ElementEvents.State;

/// <summary>
/// One user-named state in an Element+ state list. Evaluated top-to-bottom; the first rule
/// whose expression is true wins (first-match-wins). A rule whose expression references a
/// tag with an unavailable (null) value is skipped rather than treated as false.
/// </summary>
/// <remarks>
/// Decisions: DEC-PENDING.
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementStateConfigTests.cs.
/// </remarks>
public sealed record ScadaStateRule(
    string Id,
    string Name,
    bool Enabled,
    ScadaExpression Expression,
    ScadaEffectBlock Effect);
```

`ScadaElementStateConfig.cs`:
```csharp
namespace ScadaBuilderV2.Domain.ElementEvents.State;

/// <summary>
/// Element+ display-state configuration: an ordered, first-match-wins list of
/// <see cref="ScadaStateRule"/>, plus two editable fallbacks (quality and default/rest).
/// </summary>
/// <remarks>
/// Decisions: DEC-PENDING.
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementStateConfigTests.cs.
/// </remarks>
public sealed record ScadaElementStateConfig(
    ScadaEffectBlock QualityFallback,
    ScadaEffectBlock DefaultEffect,
    IReadOnlyList<ScadaStateRule> States)
{
    /// <summary>
    /// Gets the default configuration: no states, empty rest appearance, and the standard
    /// "no data" quality fallback (semi-transparent, black border).
    /// </summary>
    public static ScadaElementStateConfig Default { get; } = new(
        QualityFallback: ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
        DefaultEffect: ScadaEffectBlock.Empty,
        States: Array.Empty<ScadaStateRule>());
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaElementStateConfigTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaStateRule.cs src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaElementStateConfig.cs tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementStateConfigTests.cs
git commit -m "feat: add ScadaStateRule and ScadaElementStateConfig models"
```

---

## Task 7: Modèle de commande — `ScadaCommandBinding` + `ScadaElementCommandConfig`

**Files:**
- Create: `src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBinding.cs`
- Create: `src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaElementCommandConfig.cs`
- Test: `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementCommandConfigTests.cs`

**Interfaces:**
- Produces: enums `ScadaCommandTrigger { OnClick, OnRelease, OnHover, OnHoverEnter, OnHoverExit }`, `ScadaCommandKind { WriteTag, Navigate, OpenPopup, TogglePopup, ClosePopup, OpenUrl, Back }`, `ScadaWriteMode { Momentary, Toggle, SetFixed, SetFromInput }`. Records `sealed record ScadaConfirmation(string Message)`, `sealed record ScadaCommandBinding(string Id, string Name, bool Enabled, ScadaCommandTrigger Trigger, ScadaCommandKind Kind, ScadaConfirmation? Confirmation = null, string? WriteTagId = null, string? ReadTagId = null, ScadaWriteMode? WriteMode = null, string? OnValue = null, string? OffValue = null, string? FixedValue = null, string? TargetPageId = null, string? Url = null, bool NewTab = false)` with computed `[JsonIgnore] public string EffectiveReadTagId => ReadTagId ?? WriteTagId ?? string.Empty;` for Toggle mode. `sealed record ScadaElementCommandConfig(IReadOnlyList<ScadaCommandBinding> Commands)` with static `ScadaElementCommandConfig.Default` (empty `Commands`).

- [ ] **Step 1: Write the failing test**

```csharp
using ScadaBuilderV2.Domain.ElementEvents.Command;

namespace ScadaBuilderV2.Tests.ElementEvents;

[TestClass]
public sealed class ScadaElementCommandConfigTests
{
    [TestMethod]
    public void DefaultConfigHasNoCommands()
    {
        var config = ScadaElementCommandConfig.Default;

        Assert.AreEqual(0, config.Commands.Count);
    }

    [TestMethod]
    public void ToggleCommandFallsBackToWriteTagIdWhenReadTagIdMissing()
    {
        var command = new ScadaCommandBinding(
            "cmd-1", "Demarrer pompe", Enabled: true,
            Trigger: ScadaCommandTrigger.OnClick,
            Kind: ScadaCommandKind.WriteTag,
            WriteTagId: "tag-cmd-start",
            WriteMode: ScadaWriteMode.Toggle);

        Assert.AreEqual("tag-cmd-start", command.EffectiveReadTagId);
    }

    [TestMethod]
    public void ToggleCommandUsesExplicitReadTagIdWhenProvided()
    {
        var command = new ScadaCommandBinding(
            "cmd-1", "Demarrer pompe", Enabled: true,
            Trigger: ScadaCommandTrigger.OnClick,
            Kind: ScadaCommandKind.WriteTag,
            WriteTagId: "tag-cmd-start",
            ReadTagId: "tag-status-running",
            WriteMode: ScadaWriteMode.Toggle);

        Assert.AreEqual("tag-status-running", command.EffectiveReadTagId);
    }

    [TestMethod]
    public void ConfirmationIsOptional()
    {
        var withConfirmation = new ScadaCommandBinding(
            "cmd-1", "Reset", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
            Confirmation: new ScadaConfirmation("Confirmer le reset ?"));

        Assert.IsNotNull(withConfirmation.Confirmation);
        Assert.AreEqual("Confirmer le reset ?", withConfirmation.Confirmation!.Message);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaElementCommandConfigTests"`
Expected: FAIL (types don't exist).

- [ ] **Step 3: Write minimal implementation**

`ScadaCommandBinding.cs`:
```csharp
using System.Text.Json.Serialization;

namespace ScadaBuilderV2.Domain.ElementEvents.Command;

/// <summary>Pointer trigger that fires one Element+ command.</summary>
public enum ScadaCommandTrigger { OnClick, OnRelease, OnHover, OnHoverEnter, OnHoverExit }

/// <summary>Kind of runtime action performed by one Element+ command.</summary>
public enum ScadaCommandKind { WriteTag, Navigate, OpenPopup, TogglePopup, ClosePopup, OpenUrl, Back }

/// <summary>Write behavior for a <see cref="ScadaCommandKind.WriteTag"/> command.</summary>
public enum ScadaWriteMode { Momentary, Toggle, SetFixed, SetFromInput }

/// <summary>Optional operator confirmation shown before a command executes.</summary>
public sealed record ScadaConfirmation(string Message);

/// <summary>
/// One Element+ command: an operator-triggered action that writes a tag or navigates/opens
/// a popup/URL. Independent from display-state rules; never changes appearance directly.
/// </summary>
/// <remarks>
/// Decisions: DEC-PENDING.
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementCommandConfigTests.cs.
/// </remarks>
public sealed record ScadaCommandBinding(
    string Id,
    string Name,
    bool Enabled,
    ScadaCommandTrigger Trigger,
    ScadaCommandKind Kind,
    ScadaConfirmation? Confirmation = null,
    string? WriteTagId = null,
    string? ReadTagId = null,
    ScadaWriteMode? WriteMode = null,
    string? OnValue = null,
    string? OffValue = null,
    string? FixedValue = null,
    string? TargetPageId = null,
    string? Url = null,
    bool NewTab = false)
{
    /// <summary>
    /// Gets the tag id read for <see cref="ScadaWriteMode.Toggle"/>: <see cref="ReadTagId"/>
    /// if set, otherwise <see cref="WriteTagId"/>.
    /// </summary>
    [JsonIgnore]
    public string EffectiveReadTagId => ReadTagId ?? WriteTagId ?? string.Empty;
}
```

`ScadaElementCommandConfig.cs`:
```csharp
namespace ScadaBuilderV2.Domain.ElementEvents.Command;

/// <summary>
/// Element+ command configuration: an unordered set of independent <see cref="ScadaCommandBinding"/>,
/// each bound to its own trigger.
/// </summary>
/// <remarks>
/// Decisions: DEC-PENDING.
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementCommandConfigTests.cs.
/// </remarks>
public sealed record ScadaElementCommandConfig(IReadOnlyList<ScadaCommandBinding> Commands)
{
    /// <summary>Gets the default configuration: no commands.</summary>
    public static ScadaElementCommandConfig Default { get; } = new(Array.Empty<ScadaCommandBinding>());
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaElementCommandConfigTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBinding.cs src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaElementCommandConfig.cs tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementCommandConfigTests.cs
git commit -m "feat: add ScadaCommandBinding and ScadaElementCommandConfig models"
```

---

## Task 8: Accrocher les configs sur `ScadaElement` + méthodes `With...`

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs` (add fields to `ScadaElement` record; add `WithElementStateConfig`/`WithElementCommandConfig` methods to `ScadaScene`)
- Test: `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaSceneElementEventsTests.cs`

**Interfaces:**
- Consumes: `ScadaElementStateConfig` (Task 6), `ScadaElementCommandConfig` (Task 7), existing `ScadaElement`/`ScadaScene` (`FindElementRecursive`, `WithReplacedElementRecursive` — already used by `WithObjectEvent` at line 1221 of `ScadaSceneModels.cs`).
- Produces: two new optional positional parameters on `ScadaElement`: `ScadaElementStateConfig? StateConfig = null` and `ScadaElementCommandConfig? CommandConfig = null` (append after existing `ButtonKind` parameter — order among trailing optional named parameters is not contract-breaking). Computed properties `[JsonIgnore] public ScadaElementStateConfig EffectiveStateConfig => StateConfig ?? ScadaElementStateConfig.Default;` and `[JsonIgnore] public ScadaElementCommandConfig EffectiveCommandConfig => CommandConfig ?? ScadaElementCommandConfig.Default;`. Two new methods on `ScadaScene`: `public ScadaScene WithElementStateConfig(string elementId, ScadaElementStateConfig config)` and `public ScadaScene WithElementCommandConfig(string elementId, ScadaElementCommandConfig config)`, both following the exact pattern of `WithValueBinding` (find element, no-op if missing, `WithReplacedElementRecursive(element with { ... })`).

- [ ] **Step 1: Write the failing test**

```csharp
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.ElementEvents;

[TestClass]
public sealed class ScadaSceneElementEventsTests
{
    [TestMethod]
    public void ElementWithoutConfigsExposesDefaults()
    {
        var element = ScadaElement.CreateInputText("el-1", "El1", 0, 0);

        Assert.AreEqual(0, element.EffectiveStateConfig.States.Count);
        Assert.AreEqual(0, element.EffectiveCommandConfig.Commands.Count);
    }

    [TestMethod]
    public void WithElementStateConfigReplacesConfigOnMatchingElement()
    {
        var scene = ScadaScene.CreateEmpty("scene-1", "Main", new CanvasSize(800, 600));
        var element = ScadaElement.CreateInputText("el-1", "El1", 0, 0);
        scene = scene with { Elements = new[] { element } };

        var rule = new ScadaStateRule(
            "s1", "Alarme", true,
            ScadaExpression.FromSource("{Temp} > 80"),
            ScadaEffectBlock.Empty with { BackgroundColor = "#E53935" });
        var config = ScadaElementStateConfig.Default with { States = new[] { rule } };

        var updated = scene.WithElementStateConfig("el-1", config);
        var updatedElement = updated.FindElementRecursive("el-1");

        Assert.AreEqual(1, updatedElement!.EffectiveStateConfig.States.Count);
        Assert.AreEqual("Alarme", updatedElement.EffectiveStateConfig.States[0].Name);
    }

    [TestMethod]
    public void WithElementCommandConfigReplacesConfigOnMatchingElement()
    {
        var scene = ScadaScene.CreateEmpty("scene-1", "Main", new CanvasSize(800, 600));
        var element = ScadaElement.CreateInputText("el-1", "El1", 0, 0);
        scene = scene with { Elements = new[] { element } };

        var command = new ScadaCommandBinding("c1", "Demarrer", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag, WriteTagId: "tag-1", WriteMode: ScadaWriteMode.Toggle);
        var config = ScadaElementCommandConfig.Default with { Commands = new[] { command } };

        var updated = scene.WithElementCommandConfig("el-1", config);
        var updatedElement = updated.FindElementRecursive("el-1");

        Assert.AreEqual(1, updatedElement!.EffectiveCommandConfig.Commands.Count);
        Assert.AreEqual("Demarrer", updatedElement.EffectiveCommandConfig.Commands[0].Name);
    }

    [TestMethod]
    public void WithElementStateConfigIsNoOpWhenElementMissing()
    {
        var scene = ScadaScene.CreateEmpty("scene-1", "Main", new CanvasSize(800, 600));

        var updated = scene.WithElementStateConfig("does-not-exist", ScadaElementStateConfig.Default);

        Assert.AreEqual(scene, updated);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaSceneElementEventsTests"`
Expected: FAIL (compilation error — `StateConfig`/`CommandConfig`/`WithElementStateConfig`/`WithElementCommandConfig` don't exist).

- [ ] **Step 3: Write minimal implementation**

In `src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs`, add the using at the top:

```csharp
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.State;
```

Add two trailing optional parameters to the `ScadaElement` record declaration (after `ScadaButtonKind? ButtonKind = null`):

```csharp
ScadaButtonKind? ButtonKind = null,
ScadaElementStateConfig? StateConfig = null,
ScadaElementCommandConfig? CommandConfig = null)
```

Add computed properties inside the `ScadaElement` record body, next to `EventBindings`:

```csharp
[JsonIgnore]
public ScadaElementStateConfig EffectiveStateConfig => StateConfig ?? ScadaElementStateConfig.Default;

[JsonIgnore]
public ScadaElementCommandConfig EffectiveCommandConfig => CommandConfig ?? ScadaElementCommandConfig.Default;
```

Add two methods on `ScadaScene`, immediately after `WithValueBinding` (around line 1268):

```csharp
/// <summary>
/// Replaces the display-state configuration of one Element+ object.
/// </summary>
/// <remarks>
/// Decisions: DEC-PENDING.
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaSceneElementEventsTests.cs.
/// </remarks>
public ScadaScene WithElementStateConfig(string elementId, ScadaElementStateConfig config)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
    ArgumentNullException.ThrowIfNull(config);

    var element = FindElementRecursive(elementId);
    if (element is null)
    {
        return this;
    }

    return WithReplacedElementRecursive(element with { StateConfig = config });
}

/// <summary>
/// Replaces the command configuration of one Element+ object.
/// </summary>
/// <remarks>
/// Decisions: DEC-PENDING.
/// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaSceneElementEventsTests.cs.
/// </remarks>
public ScadaScene WithElementCommandConfig(string elementId, ScadaElementCommandConfig config)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
    ArgumentNullException.ThrowIfNull(config);

    var element = FindElementRecursive(elementId);
    if (element is null)
    {
        return this;
    }

    return WithReplacedElementRecursive(element with { CommandConfig = config });
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaSceneElementEventsTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Run full domain test suite to check for regressions**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaBuilderV2.Tests"`
Expected: PASS (all existing tests still pass — adding optional trailing record parameters is additive and must not break existing `ScadaElement.Create*` factory calls or positional-argument callers).

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs tests/ScadaBuilderV2.Tests/ElementEvents/ScadaSceneElementEventsTests.cs
git commit -m "feat: attach StateConfig/CommandConfig to ScadaElement with immutable With... methods"
```

---

## Task 9: Suppression du modèle legacy (`SetClass`/`RemoveClass`/`ToggleClass`/`WriteTag`)

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs` (remove enum members and any `With...` methods that only produce them — locate via Step 1 grep)
- Modify: `src/ScadaBuilderV2.App/ElementEventDialog.xaml.cs` (remove UI paths that author `SetClass`/`RemoveClass`/`ToggleClass`/`WriteTag`)
- Modify: any `Ft100SceneExporter` switch/case handling these kinds (locate via Step 1 grep)
- Test: existing tests in `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs` and `tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs` must be updated/removed where they assert the removed behavior

**Interfaces:**
- Consumes: nothing new. This task is subtractive.
- Produces: `ScadaActionKind` enum reduced to `Navigate, Show, Hide, ToggleVisibility, MountFragment, ClosePopup, TogglePopup, ReadValue, WriteValue` (removes `SetClass, RemoveClass, ToggleClass, WriteTag`).

- [ ] **Step 1: Locate every usage of the four removed action kinds**

Run: `grep -rn "SetClass\|RemoveClass\|ToggleClass\|ScadaActionKind.WriteTag" src/ tests/`

Record every file and line returned — each one is a required edit target for this task. Do not proceed until you have the full list (expected areas: `ScadaSceneModels.cs` enum + any `With*BorderEvent`/`With*Effect*` methods, `ElementEventDialog.xaml.cs` combobox population and `OnAddClick` switch, `Ft100SceneExporterTests.cs`, `OfficialSceneDomainTests.cs`, possibly `Ft100SceneExporter.cs` rendering switch).

- [ ] **Step 2: Remove the four enum members**

In `src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs`, change:

```csharp
public enum ScadaActionKind
{
    Navigate,
    Show,
    Hide,
    ToggleVisibility,
    SetClass,
    RemoveClass,
    ToggleClass,
    MountFragment,
    ClosePopup,
    TogglePopup,
    WriteTag,
    ReadValue,
    WriteValue
}
```

to:

```csharp
public enum ScadaActionKind
{
    Navigate,
    Show,
    Hide,
    ToggleVisibility,
    MountFragment,
    ClosePopup,
    TogglePopup,
    ReadValue,
    WriteValue
}
```

- [ ] **Step 3: Remove every method/UI path found in Step 1 that only exists to author the removed kinds**

For each file found in Step 1 (border effects, visual effects, `WriteTag` legacy authoring): delete the method body / UI branch entirely rather than leaving a dead `default: throw` — do not add a compatibility shim. If a method mixes removed-kind logic with logic still needed (e.g. a shared switch statement), remove only the `case ScadaActionKind.SetClass:` / `RemoveClass` / `ToggleClass` / `WriteTag` branches, keeping the rest intact.

- [ ] **Step 4: Build to surface every remaining compile error**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: compile errors pointing at every remaining reference to the removed enum members. Fix each by deleting the dead code path (never by re-adding a stub).

- [ ] **Step 5: Update or remove tests asserting removed behavior**

Open `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs` and `tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`; delete any `[TestMethod]` that exclusively exercises `SetClass`/`RemoveClass`/`ToggleClass`/`WriteTag` border/effect authoring. Do not delete tests that exercise `Show`/`Hide`/`ToggleVisibility`/`Navigate`/popups/`ReadValue`/`WriteValue` — those kinds remain.

- [ ] **Step 6: Run full test suite**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: PASS, zero references to removed enum members anywhere in the solution (`grep -rn "SetClass\|RemoveClass\|ToggleClass\|ScadaActionKind.WriteTag" src/ tests/` returns nothing).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: remove legacy SetClass/RemoveClass/ToggleClass/WriteTag action kinds"
```

---

## Task 10: Fenêtre d'édition d'état — `ElementStateRuleDialog`

**Files:**
- Create: `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml`
- Create: `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs`

**Interfaces:**
- Consumes: `ScadaStateRule`, `ScadaEffectBlock`, `ScadaAnimation` (Task 1, 6), `ScadaExpression`, `ScadaExpressionValidator`, `ScadaExprValidationResult` (Task 4, 5), `ScadaTagCatalog` (existing), `ColorPickerField` (existing, `src/ScadaBuilderV2.App/ColorPickerField.xaml.cs` — `Value` dependency property, `ColorChanged` event, `SetColor(string)` method).
- Produces: `public sealed partial class ElementStateRuleDialog : Window` with constructor `public ElementStateRuleDialog(ScadaStateRule? existingRule, ScadaTagCatalog? tagCatalog)` (null `existingRule` = create-new mode) and public result property `public ScadaStateRule? Result { get; private set; }` set on OK, read by the caller after `ShowDialog() == true`.

This task has no automated test (WPF dialog UI) — verification is manual per the project's `run`/UI conventions. Follow `ElementEventDialog.xaml`'s resource dictionary (brushes `InkBrush`, `MutedBrush`, `PanelBrush`, `BorderBrushSoft`, `PrimaryButtonStyle`) for visual consistency.

- [ ] **Step 1: Create the XAML window shell**

```xml
<Window x:Class="ScadaBuilderV2.App.ElementStateRuleDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:ScadaBuilderV2.App"
        Title="Editeur d'etat" Width="620" Height="640"
        WindowStartupLocation="CenterOwner" ResizeMode="NoResize">
    <Window.Resources>
        <SolidColorBrush x:Key="InkBrush" Color="#0F2A30"/>
        <SolidColorBrush x:Key="MutedBrush" Color="#5E7A82"/>
        <SolidColorBrush x:Key="PanelBrush" Color="#F7FBF5"/>
        <SolidColorBrush x:Key="BorderBrushSoft" Color="#DCE8DD"/>
        <Style x:Key="PrimaryButtonStyle" TargetType="Button">
            <Setter Property="Background" Value="#2090A0"/>
            <Setter Property="BorderBrush" Value="#0F7280"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="Padding" Value="12,6"/>
        </Style>
    </Window.Resources>
    <Grid Background="{StaticResource PanelBrush}" Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0" Margin="0,0,0,8">
            <TextBlock Text="Nom" Foreground="{StaticResource MutedBrush}"/>
            <TextBox x:Name="NameTextBox" Margin="0,2,0,8"/>
            <TextBlock Text="Condition" Foreground="{StaticResource MutedBrush}"/>
            <TextBox x:Name="ExpressionTextBox" Margin="0,2,0,2" AcceptsReturn="False"
                     TextChanged="OnExpressionTextChanged"/>
            <TextBlock x:Name="ExpressionValidationText" TextWrapping="Wrap" Margin="0,0,0,8"/>
        </StackPanel>

        <ScrollViewer Grid.Row="1">
            <StackPanel>
                <CheckBox x:Name="BackgroundEnabledCheckBox" Content="Couleur de fond" Checked="OnEffectToggleChanged" Unchecked="OnEffectToggleChanged"/>
                <local:ColorPickerField x:Name="BackgroundColorPicker" Margin="20,2,0,8"/>

                <CheckBox x:Name="BorderEnabledCheckBox" Content="Bordure" Checked="OnEffectToggleChanged" Unchecked="OnEffectToggleChanged"/>
                <StackPanel Orientation="Horizontal" Margin="20,2,0,8">
                    <local:ColorPickerField x:Name="BorderColorPicker"/>
                    <TextBox x:Name="BorderWidthTextBox" Width="60" Margin="8,0,0,0"/>
                </StackPanel>

                <CheckBox x:Name="TextEnabledCheckBox" Content="Texte" Checked="OnEffectToggleChanged" Unchecked="OnEffectToggleChanged"/>
                <StackPanel Margin="20,2,0,8">
                    <TextBox x:Name="TextContentTextBox" Margin="0,0,0,4"/>
                    <local:ColorPickerField x:Name="TextColorPicker"/>
                    <CheckBox x:Name="TextVisibleCheckBox" Content="Visible" Margin="0,4,0,0"/>
                </StackPanel>

                <CheckBox x:Name="ElementVisibleEnabledCheckBox" Content="Visibilite de l'element" Checked="OnEffectToggleChanged" Unchecked="OnEffectToggleChanged"/>
                <CheckBox x:Name="ElementVisibleCheckBox" Content="Visible" Margin="20,2,0,8"/>

                <CheckBox x:Name="OpacityEnabledCheckBox" Content="Opacite" Checked="OnEffectToggleChanged" Unchecked="OnEffectToggleChanged"/>
                <Slider x:Name="OpacitySlider" Minimum="0" Maximum="1" Margin="20,2,0,8"/>

                <CheckBox x:Name="RotationEnabledCheckBox" Content="Rotation (deg)" Checked="OnEffectToggleChanged" Unchecked="OnEffectToggleChanged"/>
                <TextBox x:Name="RotationTextBox" Width="80" Margin="20,2,0,8" HorizontalAlignment="Left"/>

                <CheckBox x:Name="AnimationEnabledCheckBox" Content="Animation" Checked="OnEffectToggleChanged" Unchecked="OnEffectToggleChanged"/>
                <ComboBox x:Name="AnimationComboBox" Margin="20,2,0,8" HorizontalAlignment="Left" Width="160"/>

                <TextBlock Text="Apercu" Foreground="{StaticResource MutedBrush}" Margin="0,8,0,4"/>
                <Border x:Name="PreviewBorder" Width="160" Height="90" BorderBrush="{StaticResource BorderBrushSoft}" BorderThickness="1">
                    <TextBlock x:Name="PreviewText" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
            </StackPanel>
        </ScrollViewer>

        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,8,0,0">
            <Button Content="Annuler" IsCancel="True" Margin="0,0,8,0"/>
            <Button Content="Enregistrer" Style="{StaticResource PrimaryButtonStyle}" Click="OnSaveClick"/>
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 2: Write the code-behind**

```csharp
using System.Globalization;
using System.Windows;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App;

public partial class ElementStateRuleDialog : Window
{
    private readonly ScadaTagCatalog? _tagCatalog;
    private readonly string _ruleId;

    public ElementStateRuleDialog(ScadaStateRule? existingRule, ScadaTagCatalog? tagCatalog)
    {
        InitializeComponent();
        _tagCatalog = tagCatalog;
        _ruleId = existingRule?.Id ?? Guid.NewGuid().ToString("n");

        AnimationComboBox.ItemsSource = Enum.GetValues<ScadaAnimation>();

        if (existingRule is not null)
        {
            NameTextBox.Text = existingRule.Name;
            ExpressionTextBox.Text = existingRule.Expression.Source;
            LoadEffect(existingRule.Effect);
        }

        ValidateExpression();
    }

    public ScadaStateRule? Result { get; private set; }

    private void LoadEffect(ScadaEffectBlock effect)
    {
        if (effect.BackgroundColor is not null)
        {
            BackgroundEnabledCheckBox.IsChecked = true;
            BackgroundColorPicker.SetColor(effect.BackgroundColor);
        }

        if (effect.BorderColor is not null)
        {
            BorderEnabledCheckBox.IsChecked = true;
            BorderColorPicker.SetColor(effect.BorderColor);
            BorderWidthTextBox.Text = (effect.BorderWidth ?? 1).ToString(CultureInfo.InvariantCulture);
        }

        if (effect.TextContent is not null || effect.TextColor is not null || effect.TextVisible is not null)
        {
            TextEnabledCheckBox.IsChecked = true;
            TextContentTextBox.Text = effect.TextContent ?? string.Empty;
            TextColorPicker.SetColor(effect.TextColor ?? "#000000");
            TextVisibleCheckBox.IsChecked = effect.TextVisible ?? true;
        }

        if (effect.ElementVisible is not null)
        {
            ElementVisibleEnabledCheckBox.IsChecked = true;
            ElementVisibleCheckBox.IsChecked = effect.ElementVisible;
        }

        if (effect.Opacity is not null)
        {
            OpacityEnabledCheckBox.IsChecked = true;
            OpacitySlider.Value = effect.Opacity.Value;
        }

        if (effect.Rotation is not null)
        {
            RotationEnabledCheckBox.IsChecked = true;
            RotationTextBox.Text = effect.Rotation.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (effect.Animation is not null)
        {
            AnimationEnabledCheckBox.IsChecked = true;
            AnimationComboBox.SelectedItem = effect.Animation.Value;
        }
    }

    private void OnEffectToggleChanged(object sender, RoutedEventArgs e) => UpdatePreview();

    private void OnExpressionTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ValidateExpression();

    private void ValidateExpression()
    {
        var result = ScadaExpressionValidator.Validate(ExpressionTextBox.Text, _tagCatalog);
        ExpressionValidationText.Text = result.IsValid
            ? "Condition valide."
            : string.Join(" ", result.Errors);
        ExpressionValidationText.Foreground = result.IsValid
            ? System.Windows.Media.Brushes.SeaGreen
            : System.Windows.Media.Brushes.Firebrick;
    }

    private void UpdatePreview()
    {
        var effect = BuildEffectFromUi();
        PreviewBorder.Background = effect.BackgroundColor is not null
            ? new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(effect.BackgroundColor))
            : System.Windows.Media.Brushes.Transparent;
        PreviewBorder.Opacity = effect.Opacity ?? 1.0;
        PreviewText.Text = effect.TextContent ?? string.Empty;
    }

    private ScadaEffectBlock BuildEffectFromUi()
    {
        return new ScadaEffectBlock(
            BackgroundColor: BackgroundEnabledCheckBox.IsChecked == true ? BackgroundColorPicker.Value : null,
            BorderColor: BorderEnabledCheckBox.IsChecked == true ? BorderColorPicker.Value : null,
            BorderWidth: BorderEnabledCheckBox.IsChecked == true && double.TryParse(BorderWidthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var width) ? width : null,
            TextColor: TextEnabledCheckBox.IsChecked == true ? TextColorPicker.Value : null,
            TextContent: TextEnabledCheckBox.IsChecked == true ? TextContentTextBox.Text : null,
            TextVisible: TextEnabledCheckBox.IsChecked == true ? TextVisibleCheckBox.IsChecked : null,
            ElementVisible: ElementVisibleEnabledCheckBox.IsChecked == true ? ElementVisibleCheckBox.IsChecked : null,
            Opacity: OpacityEnabledCheckBox.IsChecked == true ? OpacitySlider.Value : null,
            Rotation: RotationEnabledCheckBox.IsChecked == true && double.TryParse(RotationTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var rotation) ? rotation : null,
            Animation: AnimationEnabledCheckBox.IsChecked == true ? (ScadaAnimation?)AnimationComboBox.SelectedItem : null);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var validation = ScadaExpressionValidator.Validate(ExpressionTextBox.Text, _tagCatalog);
        if (!validation.IsValid || string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            ValidateExpression();
            return;
        }

        Result = new ScadaStateRule(
            _ruleId,
            NameTextBox.Text.Trim(),
            Enabled: true,
            Expression: ScadaExpression.FromSource(ExpressionTextBox.Text),
            Effect: BuildEffectFromUi());

        DialogResult = true;
    }
}
```

- [ ] **Step 3: Build the App project**

Run: `dotnet build src/ScadaBuilderV2.App/ScadaBuilderV2.App.csproj`
Expected: build succeeds with zero errors.

- [ ] **Step 4: Manual verification**

Run: `dotnet run --project src/ScadaBuilderV2.App`
Open a scene, select an Element+. This dialog is not yet wired to any button (wiring happens in Task 12) — for this task, verify only that the project builds and no XAML parse errors appear in the designer/output window when the app starts.

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs
git commit -m "feat: add ElementStateRuleDialog for editing one state rule with static preview"
```

---

## Task 11: Fenêtre d'édition de commande — `ElementCommandDialog`

**Files:**
- Create: `src/ScadaBuilderV2.App/ElementCommandDialog.xaml`
- Create: `src/ScadaBuilderV2.App/ElementCommandDialog.xaml.cs`

**Interfaces:**
- Consumes: `ScadaCommandBinding`, `ScadaCommandKind`, `ScadaCommandTrigger`, `ScadaWriteMode`, `ScadaConfirmation` (Task 7), `ScadaTagCatalog`/`ScadaTagDefinition` (existing), `ScadaSceneReference` (existing, used the same way as in `ElementEventDialog`'s `pageReferences` parameter).
- Produces: `public sealed partial class ElementCommandDialog : Window` with constructor `public ElementCommandDialog(ScadaCommandBinding? existingCommand, IReadOnlyList<ScadaSceneReference> pageReferences, ScadaTagCatalog? tagCatalog)` and `public ScadaCommandBinding? Result { get; private set; }`.

No automated test (WPF dialog) — manual verification per project convention.

- [ ] **Step 1: Create the XAML window shell**

```xml
<Window x:Class="ScadaBuilderV2.App.ElementCommandDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Editeur de commande" Width="520" Height="520"
        WindowStartupLocation="CenterOwner" ResizeMode="NoResize">
    <Window.Resources>
        <SolidColorBrush x:Key="MutedBrush" Color="#5E7A82"/>
        <SolidColorBrush x:Key="PanelBrush" Color="#F7FBF5"/>
        <Style x:Key="PrimaryButtonStyle" TargetType="Button">
            <Setter Property="Background" Value="#2090A0"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="Padding" Value="12,6"/>
        </Style>
    </Window.Resources>
    <Grid Background="{StaticResource PanelBrush}" Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0">
            <TextBlock Text="Nom" Foreground="{StaticResource MutedBrush}"/>
            <TextBox x:Name="NameTextBox" Margin="0,2,0,8"/>

            <TextBlock Text="Declencheur" Foreground="{StaticResource MutedBrush}"/>
            <ComboBox x:Name="TriggerComboBox" Margin="0,2,0,8"/>

            <TextBlock Text="Type" Foreground="{StaticResource MutedBrush}"/>
            <ComboBox x:Name="KindComboBox" Margin="0,2,0,8" SelectionChanged="OnKindChanged"/>

            <StackPanel x:Name="WriteTagPanel" Visibility="Collapsed">
                <TextBlock Text="Tag ecriture" Foreground="{StaticResource MutedBrush}"/>
                <ComboBox x:Name="WriteTagComboBox" Margin="0,2,0,8"/>
                <TextBlock Text="Tag lecture (optionnel)" Foreground="{StaticResource MutedBrush}"/>
                <ComboBox x:Name="ReadTagComboBox" Margin="0,2,0,8"/>
                <TextBlock Text="Mode" Foreground="{StaticResource MutedBrush}"/>
                <ComboBox x:Name="WriteModeComboBox" Margin="0,2,0,8" SelectionChanged="OnWriteModeChanged"/>
                <StackPanel x:Name="MomentaryValuesPanel" Orientation="Horizontal" Visibility="Collapsed" Margin="0,0,0,8">
                    <TextBox x:Name="OnValueTextBox" Width="100"/>
                    <TextBox x:Name="OffValueTextBox" Width="100" Margin="8,0,0,0"/>
                </StackPanel>
                <TextBox x:Name="FixedValueTextBox" Margin="0,0,0,8" Visibility="Collapsed"/>
            </StackPanel>

            <StackPanel x:Name="PagePanel" Visibility="Collapsed">
                <TextBlock Text="Page cible" Foreground="{StaticResource MutedBrush}"/>
                <ComboBox x:Name="TargetPageComboBox" Margin="0,2,0,8"/>
            </StackPanel>

            <StackPanel x:Name="UrlPanel" Visibility="Collapsed">
                <TextBlock Text="URL" Foreground="{StaticResource MutedBrush}"/>
                <TextBox x:Name="UrlTextBox" Margin="0,2,0,8"/>
                <CheckBox x:Name="NewTabCheckBox" Content="Nouvel onglet"/>
            </StackPanel>

            <CheckBox x:Name="ConfirmationCheckBox" Content="Demander confirmation" Margin="0,8,0,4"/>
            <TextBox x:Name="ConfirmationMessageTextBox" Margin="0,0,0,8"/>
        </StackPanel>

        <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="Annuler" IsCancel="True" Margin="0,0,8,0"/>
            <Button Content="Enregistrer" Style="{StaticResource PrimaryButtonStyle}" Click="OnSaveClick"/>
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 2: Write the code-behind**

```csharp
using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App;

public partial class ElementCommandDialog : Window
{
    private sealed record TagItem(string Id, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly IReadOnlyList<ScadaSceneReference> _pageReferences;
    private readonly string _commandId;

    public ElementCommandDialog(ScadaCommandBinding? existingCommand, IReadOnlyList<ScadaSceneReference> pageReferences, ScadaTagCatalog? tagCatalog)
    {
        InitializeComponent();
        _pageReferences = pageReferences;
        _commandId = existingCommand?.Id ?? Guid.NewGuid().ToString("n");

        TriggerComboBox.ItemsSource = Enum.GetValues<ScadaCommandTrigger>();
        KindComboBox.ItemsSource = Enum.GetValues<ScadaCommandKind>();
        WriteModeComboBox.ItemsSource = Enum.GetValues<ScadaWriteMode>();
        TargetPageComboBox.ItemsSource = pageReferences;

        var tagItems = (tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
            .Where(tag => tag.Enabled)
            .OrderBy(tag => tag.DisplayName)
            .Select(tag => new TagItem(tag.Id, tag.AuthoringLabel))
            .ToArray();
        WriteTagComboBox.ItemsSource = tagItems;
        ReadTagComboBox.ItemsSource = tagItems;

        if (existingCommand is not null)
        {
            NameTextBox.Text = existingCommand.Name;
            TriggerComboBox.SelectedItem = existingCommand.Trigger;
            KindComboBox.SelectedItem = existingCommand.Kind;
            WriteTagComboBox.SelectedItem = tagItems.FirstOrDefault(t => t.Id == existingCommand.WriteTagId);
            ReadTagComboBox.SelectedItem = tagItems.FirstOrDefault(t => t.Id == existingCommand.ReadTagId);
            WriteModeComboBox.SelectedItem = existingCommand.WriteMode;
            OnValueTextBox.Text = existingCommand.OnValue ?? string.Empty;
            OffValueTextBox.Text = existingCommand.OffValue ?? string.Empty;
            FixedValueTextBox.Text = existingCommand.FixedValue ?? string.Empty;
            TargetPageComboBox.SelectedItem = pageReferences.FirstOrDefault(p => p.Id == existingCommand.TargetPageId);
            UrlTextBox.Text = existingCommand.Url ?? string.Empty;
            NewTabCheckBox.IsChecked = existingCommand.NewTab;
            ConfirmationCheckBox.IsChecked = existingCommand.Confirmation is not null;
            ConfirmationMessageTextBox.Text = existingCommand.Confirmation?.Message ?? string.Empty;
        }
        else
        {
            TriggerComboBox.SelectedIndex = 0;
            KindComboBox.SelectedIndex = 0;
        }

        UpdateKindPanels();
        UpdateWriteModePanels();
    }

    public ScadaCommandBinding? Result { get; private set; }

    private void OnKindChanged(object sender, SelectionChangedEventArgs e) => UpdateKindPanels();

    private void OnWriteModeChanged(object sender, SelectionChangedEventArgs e) => UpdateWriteModePanels();

    private void UpdateKindPanels()
    {
        var kind = (ScadaCommandKind?)KindComboBox.SelectedItem ?? ScadaCommandKind.WriteTag;
        WriteTagPanel.Visibility = kind == ScadaCommandKind.WriteTag ? Visibility.Visible : Visibility.Collapsed;
        PagePanel.Visibility = kind is ScadaCommandKind.Navigate or ScadaCommandKind.OpenPopup or ScadaCommandKind.TogglePopup or ScadaCommandKind.ClosePopup
            ? Visibility.Visible
            : Visibility.Collapsed;
        UrlPanel.Visibility = kind == ScadaCommandKind.OpenUrl ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateWriteModePanels()
    {
        var mode = (ScadaWriteMode?)WriteModeComboBox.SelectedItem;
        MomentaryValuesPanel.Visibility = mode == ScadaWriteMode.Momentary ? Visibility.Visible : Visibility.Collapsed;
        FixedValueTextBox.Visibility = mode == ScadaWriteMode.SetFixed ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text) || TriggerComboBox.SelectedItem is null || KindComboBox.SelectedItem is null)
        {
            return;
        }

        var kind = (ScadaCommandKind)KindComboBox.SelectedItem;
        Result = new ScadaCommandBinding(
            _commandId,
            NameTextBox.Text.Trim(),
            Enabled: true,
            Trigger: (ScadaCommandTrigger)TriggerComboBox.SelectedItem,
            Kind: kind,
            Confirmation: ConfirmationCheckBox.IsChecked == true
                ? new ScadaConfirmation(ConfirmationMessageTextBox.Text.Trim())
                : null,
            WriteTagId: (WriteTagComboBox.SelectedItem as TagItem)?.Id,
            ReadTagId: (ReadTagComboBox.SelectedItem as TagItem)?.Id,
            WriteMode: (ScadaWriteMode?)WriteModeComboBox.SelectedItem,
            OnValue: string.IsNullOrWhiteSpace(OnValueTextBox.Text) ? null : OnValueTextBox.Text,
            OffValue: string.IsNullOrWhiteSpace(OffValueTextBox.Text) ? null : OffValueTextBox.Text,
            FixedValue: string.IsNullOrWhiteSpace(FixedValueTextBox.Text) ? null : FixedValueTextBox.Text,
            TargetPageId: (TargetPageComboBox.SelectedItem as ScadaSceneReference)?.Id,
            Url: string.IsNullOrWhiteSpace(UrlTextBox.Text) ? null : UrlTextBox.Text,
            NewTab: NewTabCheckBox.IsChecked == true);

        DialogResult = true;
    }
}
```

- [ ] **Step 3: Build the App project**

Run: `dotnet build src/ScadaBuilderV2.App/ScadaBuilderV2.App.csproj`
Expected: build succeeds with zero errors. If `ScadaSceneReference` has no public `Id`/settable display member matching `ToString()` expectations for the `TargetPageComboBox`, check its actual shape in `ScadaSceneModels.cs` (`grep -n "record ScadaSceneReference" src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs`) and adjust the binding to use `DisplayMemberPath` accordingly before proceeding.

- [ ] **Step 4: Commit**

```bash
git add src/ScadaBuilderV2.App/ElementCommandDialog.xaml src/ScadaBuilderV2.App/ElementCommandDialog.xaml.cs
git commit -m "feat: add ElementCommandDialog for editing one command binding"
```

---

## Task 12: Onglets panneau Propriétés + listes réordonnables + bouton Test

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml` (replace the `Evenement` `TabItem` at ~line 1009 with two new tabs)
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs` (replace `OnOpenSelectedElementEventsClick`/`OpenElementEventDialog`/`AddElementEventFromDialog`/`DeleteElementEventFromDialog` with state-list and command-list handlers)

**Interfaces:**
- Consumes: `ElementStateRuleDialog` (Task 10), `ElementCommandDialog` (Task 11), `ScadaScene.WithElementStateConfig`/`WithElementCommandConfig` (Task 8), `ScadaElement.EffectiveStateConfig`/`EffectiveCommandConfig` (Task 8).
- Produces: two `ListBox` controls (`StateRulesListBox`, `CommandsListBox`) reflecting the selected element's `EffectiveStateConfig.States` / `EffectiveCommandConfig.Commands`, with add/edit/delete/reorder/test wired to the domain methods from Task 8. Every mutation must go through the existing undo/redo history mechanism the same way `AddElementEventFromDialog` did (push a history action, then refresh).

- [ ] **Step 1: Replace the `Evenement` TabItem in MainWindow.xaml**

Locate the existing block (around line 1009):

```xml
<TabItem Header="Evenement">
    <StackPanel Margin="8">
        <TextBlock Text="Evenements runtime"/>
        <TextBlock x:Name="ElementEventsSummaryText"
                   Text="Aucun evenement"
                   Margin="0,3,0,8"
                   TextWrapping="Wrap"
                   Foreground="{StaticResource MutedBrush}"/>
        <Button x:Name="OpenElementEventsButton"
                Content="Evenement"
                HorizontalAlignment="Left"
                Click="OnOpenSelectedElementEventsClick"/>
    </StackPanel>
</TabItem>
```

Replace it with two tabs:

```xml
<TabItem Header="Etat">
    <StackPanel Margin="8">
        <TextBlock Text="Evenement d'affichage d'etat"/>
        <StackPanel Orientation="Horizontal" Margin="0,6,0,6">
            <Button Content="Repos..." Click="OnEditDefaultStateEffectClick" Margin="0,0,6,0"/>
            <Button Content="Qualite..." Click="OnEditQualityFallbackEffectClick"/>
        </StackPanel>
        <ListBox x:Name="StateRulesListBox" Height="220" SelectionMode="Single"/>
        <StackPanel Orientation="Horizontal" Margin="0,6,0,0">
            <Button Content="+ Ajouter" Click="OnAddStateRuleClick" Margin="0,0,6,0"/>
            <Button Content="Monter" Click="OnMoveStateRuleUpClick" Margin="0,0,6,0"/>
            <Button Content="Descendre" Click="OnMoveStateRuleDownClick" Margin="0,0,6,0"/>
            <Button Content="Editer" Click="OnEditStateRuleClick" Margin="0,0,6,0"/>
            <Button Content="Supprimer" Click="OnDeleteStateRuleClick" Margin="0,0,6,0"/>
            <ToggleButton x:Name="TestStateRuleToggle" Content="Test" Click="OnTestStateRuleToggleClick"/>
        </StackPanel>
    </StackPanel>
</TabItem>
<TabItem Header="Commande">
    <StackPanel Margin="8">
        <TextBlock Text="Evenement de commande"/>
        <ListBox x:Name="CommandsListBox" Height="240" SelectionMode="Single" Margin="0,6,0,6"/>
        <StackPanel Orientation="Horizontal">
            <Button Content="+ Ajouter" Click="OnAddCommandClick" Margin="0,0,6,0"/>
            <Button Content="Editer" Click="OnEditCommandClick" Margin="0,0,6,0"/>
            <Button Content="Supprimer" Click="OnDeleteCommandClick"/>
        </StackPanel>
    </StackPanel>
</TabItem>
```

- [ ] **Step 2: Locate the existing event-handler region to replace in MainWindow.xaml.cs**

Run: `grep -n "OnOpenSelectedElementEventsClick\|OpenElementEventDialog\|AddElementEventFromDialog\|DeleteElementEventFromDialog" src/ScadaBuilderV2.App/MainWindow.xaml.cs`

Note the line ranges of each method — they are deleted in Step 3 and replaced with the new handlers.

- [ ] **Step 3: Replace the old handlers with state/command list handlers**

Delete `OnOpenSelectedElementEventsClick`, `OpenElementEventDialog`, `AddElementEventFromDialog`, `DeleteElementEventFromDialog` (found in Step 2), and add:

```csharp
private ScadaStateRule? _testedStateRule;

private void RefreshStateAndCommandTabs()
{
    var element = _activeScene?.FindElementRecursive(_selectedSceneObject?.Id ?? string.Empty);
    StateRulesListBox.ItemsSource = element?.EffectiveStateConfig.States;
    CommandsListBox.ItemsSource = element?.EffectiveCommandConfig.Commands;
}

private void OnAddStateRuleClick(object sender, RoutedEventArgs e)
{
    if (_activeScene is null || _selectedSceneObject is null) return;

    var dialog = new ElementStateRuleDialog(null, _modernProject?.TagCatalog) { Owner = this };
    if (dialog.ShowDialog() != true || dialog.Result is null) return;

    var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
    if (element is null) return;

    var config = element.EffectiveStateConfig with
    {
        States = element.EffectiveStateConfig.States.Append(dialog.Result).ToArray()
    };
    _activeScene = _activeScene.WithElementStateConfig(_selectedSceneObject.Id, config);
    RefreshStateAndCommandTabs();
}

private void OnEditStateRuleClick(object sender, RoutedEventArgs e)
{
    if (_activeScene is null || _selectedSceneObject is null) return;
    if (StateRulesListBox.SelectedItem is not ScadaStateRule selected) return;

    var dialog = new ElementStateRuleDialog(selected, _modernProject?.TagCatalog) { Owner = this };
    if (dialog.ShowDialog() != true || dialog.Result is null) return;

    var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
    if (element is null) return;

    var states = element.EffectiveStateConfig.States
        .Select(rule => rule.Id == dialog.Result.Id ? dialog.Result : rule)
        .ToArray();
    _activeScene = _activeScene.WithElementStateConfig(_selectedSceneObject.Id, element.EffectiveStateConfig with { States = states });
    RefreshStateAndCommandTabs();
}

private void OnDeleteStateRuleClick(object sender, RoutedEventArgs e)
{
    if (_activeScene is null || _selectedSceneObject is null) return;
    if (StateRulesListBox.SelectedItem is not ScadaStateRule selected) return;

    var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
    if (element is null) return;

    var states = element.EffectiveStateConfig.States.Where(rule => rule.Id != selected.Id).ToArray();
    _activeScene = _activeScene.WithElementStateConfig(_selectedSceneObject.Id, element.EffectiveStateConfig with { States = states });
    RefreshStateAndCommandTabs();
}

private void OnMoveStateRuleUpClick(object sender, RoutedEventArgs e) => MoveSelectedStateRule(-1);

private void OnMoveStateRuleDownClick(object sender, RoutedEventArgs e) => MoveSelectedStateRule(1);

private void MoveSelectedStateRule(int offset)
{
    if (_activeScene is null || _selectedSceneObject is null) return;
    if (StateRulesListBox.SelectedItem is not ScadaStateRule selected) return;

    var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
    if (element is null) return;

    var states = element.EffectiveStateConfig.States.ToList();
    var index = states.FindIndex(rule => rule.Id == selected.Id);
    var newIndex = index + offset;
    if (index < 0 || newIndex < 0 || newIndex >= states.Count) return;

    (states[index], states[newIndex]) = (states[newIndex], states[index]);
    _activeScene = _activeScene.WithElementStateConfig(_selectedSceneObject.Id, element.EffectiveStateConfig with { States = states });
    RefreshStateAndCommandTabs();
}

private void OnEditDefaultStateEffectClick(object sender, RoutedEventArgs e) => EditFallbackEffect(isQuality: false);

private void OnEditQualityFallbackEffectClick(object sender, RoutedEventArgs e) => EditFallbackEffect(isQuality: true);

private void EditFallbackEffect(bool isQuality)
{
    if (_activeScene is null || _selectedSceneObject is null) return;

    var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
    if (element is null) return;

    var placeholderRule = new ScadaStateRule(
        "fallback-editor",
        isQuality ? "Qualite" : "Repos",
        true,
        ScadaExpression.FromSource("true"),
        isQuality ? element.EffectiveStateConfig.QualityFallback : element.EffectiveStateConfig.DefaultEffect);

    var dialog = new ElementStateRuleDialog(placeholderRule, _modernProject?.TagCatalog) { Owner = this };
    if (dialog.ShowDialog() != true || dialog.Result is null) return;

    var updatedConfig = isQuality
        ? element.EffectiveStateConfig with { QualityFallback = dialog.Result.Effect }
        : element.EffectiveStateConfig with { DefaultEffect = dialog.Result.Effect };
    _activeScene = _activeScene.WithElementStateConfig(_selectedSceneObject.Id, updatedConfig);
    RefreshStateAndCommandTabs();
}

private void OnTestStateRuleToggleClick(object sender, RoutedEventArgs e)
{
    if (StateRulesListBox.SelectedItem is not ScadaStateRule selected)
    {
        TestStateRuleToggle.IsChecked = false;
        return;
    }

    _testedStateRule = TestStateRuleToggle.IsChecked == true ? selected : null;
    // Applying _testedStateRule's Effect onto the live canvas element is a rendering
    // concern outside this task's scope; Task 12 wires the toggle state and selection
    // only. A future task applies ScadaEffectBlock to the WebView2 canvas element.
}

private void OnAddCommandClick(object sender, RoutedEventArgs e)
{
    if (_activeScene is null || _selectedSceneObject is null) return;

    var dialog = new ElementCommandDialog(null, GetCurrentSceneReferences(), _modernProject?.TagCatalog) { Owner = this };
    if (dialog.ShowDialog() != true || dialog.Result is null) return;

    var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
    if (element is null) return;

    var config = element.EffectiveCommandConfig with
    {
        Commands = element.EffectiveCommandConfig.Commands.Append(dialog.Result).ToArray()
    };
    _activeScene = _activeScene.WithElementCommandConfig(_selectedSceneObject.Id, config);
    RefreshStateAndCommandTabs();
}

private void OnEditCommandClick(object sender, RoutedEventArgs e)
{
    if (_activeScene is null || _selectedSceneObject is null) return;
    if (CommandsListBox.SelectedItem is not ScadaCommandBinding selected) return;

    var dialog = new ElementCommandDialog(selected, GetCurrentSceneReferences(), _modernProject?.TagCatalog) { Owner = this };
    if (dialog.ShowDialog() != true || dialog.Result is null) return;

    var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
    if (element is null) return;

    var commands = element.EffectiveCommandConfig.Commands
        .Select(command => command.Id == dialog.Result.Id ? dialog.Result : command)
        .ToArray();
    _activeScene = _activeScene.WithElementCommandConfig(_selectedSceneObject.Id, element.EffectiveCommandConfig with { Commands = commands });
    RefreshStateAndCommandTabs();
}

private void OnDeleteCommandClick(object sender, RoutedEventArgs e)
{
    if (_activeScene is null || _selectedSceneObject is null) return;
    if (CommandsListBox.SelectedItem is not ScadaCommandBinding selected) return;

    var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
    if (element is null) return;

    var commands = element.EffectiveCommandConfig.Commands.Where(command => command.Id != selected.Id).ToArray();
    _activeScene = _activeScene.WithElementCommandConfig(_selectedSceneObject.Id, element.EffectiveCommandConfig with { Commands = commands });
    RefreshStateAndCommandTabs();
}
```

Add the required usings at the top of `MainWindow.xaml.cs` if not already present:

```csharp
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
```

Find the existing element-selection-changed handler (search `grep -n "_selectedSceneObject = " src/ScadaBuilderV2.App/MainWindow.xaml.cs` to locate it) and add a call to `RefreshStateAndCommandTabs();` wherever `ElementEventsSummaryText` was previously being refreshed, so the two new tabs repopulate on selection change.

- [ ] **Step 4: Build the App project**

Run: `dotnet build src/ScadaBuilderV2.App/ScadaBuilderV2.App.csproj`
Expected: build succeeds. Fix any remaining reference to the deleted `ElementEventsSummaryText`/`OpenElementEventsButton` controls (they were removed from XAML in Step 1 — any leftover code-behind reference is a compile error to resolve by deletion).

- [ ] **Step 5: Manual verification**

Run: `dotnet run --project src/ScadaBuilderV2.App`
Open a project, select an Element+, open the Propriétés panel: confirm the "Etat" and "Commande" tabs appear (replacing "Evenement"), that "+ Ajouter" on the Etat tab opens `ElementStateRuleDialog` and saving adds a row to `StateRulesListBox`, and that "+ Ajouter" on the Commande tab opens `ElementCommandDialog` and saving adds a row to `CommandsListBox`. Confirm Monter/Descendre reorder the list.

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml src/ScadaBuilderV2.App/MainWindow.xaml.cs
git commit -m "feat: replace Evenement tab with Etat and Commande tabs wired to new domain models"
```

---

## Task 13: Contrat runtime + note de dépréciation

**Files:**
- Create: `docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md`
- Modify: `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md` (add deprecation notice)

**Interfaces:**
- Consumes: nothing (documentation only).
- Produces: nothing consumed by later tasks — this is the final task.

- [ ] **Step 1: Write the runtime contract document**

```markdown
# Element+ State & Command Runtime Contract (V1)

Date: 2026-07-07
Status: Contract specification — NOT YET IMPLEMENTED by TF100Web
Owner: SCADA Builder V2 authoring team (this document). Implementation owner: TF100Web team (F:\Projet\Git\TF100Web), future iteration.

## 1. Purpose

Defines the serialized format and evaluation semantics that TF100Web must implement to
render Element+ display-state rules and execute Element+ commands at runtime. SCADA
Builder V2 authors and validates this data; TF100Web is the sole runtime consumer.

Design source: `docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md`.

## 2. Serialized shapes (JSON, per Element+)

```json
{
  "stateConfig": {
    "qualityFallback": { "opacity": 0.4, "borderColor": "#000000", "borderWidth": 2 },
    "defaultEffect": {},
    "states": [
      {
        "id": "state-1",
        "name": "Alarme haute",
        "enabled": true,
        "expression": { "source": "{Temp} > 80", "ast": { "...": "see §3" } },
        "effect": { "backgroundColor": "#E53935", "animation": "Blink" }
      }
    ]
  },
  "commandConfig": {
    "commands": [
      {
        "id": "cmd-1",
        "name": "Demarrer pompe",
        "enabled": true,
        "trigger": "OnClick",
        "kind": "WriteTag",
        "writeTagId": "tag-cmd-start",
        "readTagId": null,
        "writeMode": "Toggle",
        "confirmation": { "message": "Demarrer la pompe ?" }
      }
    ]
  }
}
```

Effect block properties (`backgroundColor`, `borderColor`, `borderWidth`, `textColor`,
`textContent`, `textVisible`, `elementVisible`, `opacity`, `rotation`, `animation`) are all
optional; an absent/null property means "leave current appearance unchanged for this
property" — TF100Web must not default it, only skip applying it.

## 3. Expression AST format

The AST, not `expression.source`, is authoritative at runtime. Node shapes:

```json
{ "type": "literalNumber", "value": 80 }
{ "type": "literalBool", "value": true }
{ "type": "literalString", "value": "text" }
{ "type": "tagRef", "tagName": "Temp" }
{ "type": "unary", "op": "Not" | "Negate", "operand": { "...": "node" } }
{ "type": "binary", "op": "Add|Subtract|Multiply|Divide|Modulo|Equal|NotEqual|LessThan|LessThanOrEqual|GreaterThan|GreaterThanOrEqual|And|Or", "left": {}, "right": {} }
{ "type": "func", "name": "ABS|MIN|MAX|BIT", "args": [ { "...": "node" } ] }
```

`BIT(tag, n)` returns the boolean value of bit `n` (0-indexed, least significant bit) of
the integer value of `tag`.

## 4. Evaluation semantics (must match exactly)

```
1. LISTE  → iterate states top to bottom:
     • if any tag referenced by THIS state's expression is null (unavailable)
         → SKIP this state, continue to the next.
     • otherwise evaluate the expression:
         - true              → apply this state's effect. STOP. (first-match-wins)
         - false             → continue.
         - evaluation error (e.g. runtime division by zero)
                              → treat as false, raise the error flag (§5), continue.

2. ALL STATES UNEVALUABLE → if every state was skipped in step 1 (all had >= 1 null tag,
     or there are zero states) → apply qualityFallback. STOP.

3. DEFAULT → some states were evaluable but none matched → apply defaultEffect. STOP.

4. ERROR FLAG (cross-cutting, non-blocking):
     • if any expression raised an evaluation error during the pass
         → render a small error badge/overlay on the element
         → any textContent driven by the applied effect becomes "---" instead of its
           interpolated value.
     • does not prevent the match/default effect from being applied.
```

"Null" = the tag has never been read, or TF100Web's own quality/connectivity flag marks
it unavailable. There is no stale-timeout in V1 — a value refreshed at any point in the
past is not null.

## 5. Command execution semantics

- `WriteTag` + `Momentary`: write `onValue` on trigger-press, `offValue` on release.
- `WriteTag` + `Toggle`: read current value of `readTagId` (fallback: `writeTagId`), write
  its logical negation to `writeTagId`.
- `WriteTag` + `SetFixed`: write `fixedValue` verbatim on trigger.
- `WriteTag` + `SetFromInput`: write the operator-entered runtime value (no design-time
  value is stored).
- `Navigate`/`OpenPopup`/`TogglePopup`/`ClosePopup`/`OpenUrl`/`Back`: same semantics as the
  existing `ScadaActionKind.Navigate`/`MountFragment`/`TogglePopup`/`ClosePopup` runtime
  behavior described in `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md` §8, §12; `OpenUrl`
  and `Back` are new and have no prior runtime behavior to match.
- If `confirmation` is present, TF100Web must show `confirmation.message` and require
  operator acknowledgement before executing the command's effect.

## 6. Non-goals for this contract version

- No live simulator wire format — the Builder's static preview never calls this runtime.
- No stale-timeout quality detection.
- No functions beyond `ABS/MIN/MAX/BIT`.
- No cumulative multi-animation composition beyond the single `animation` field.

## 7. Implementation status

Not implemented. This document specifies the contract for a future TF100Web-side
implementation iteration. Do not mark any section of this contract "Implemented" until
TF100Web code exists and is tested against it.
```

- [ ] **Step 2: Add the deprecation notice to the old contract**

In `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`, insert immediately after the header (after line 5, before `## Historique des changements`):

```markdown
> **DEPRECATED (2026-07-07):** `SetClass`/`RemoveClass`/`ToggleClass`/`WriteTag` (legacy)
> action kinds and the border/visual-effect authoring described in §3, §8, §9 have been
> removed from the domain model. Element+ display-state and command authoring is now
> specified in `docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md`
> and `docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md`. `Navigate`,
> `Show`/`Hide`/`ToggleVisibility`, `MountFragment`/`ClosePopup`/`TogglePopup`, and
> `ReadValue`/`WriteValue` remain valid until fully absorbed by the new Etat/Commande tabs.
```

- [ ] **Step 3: Run the documentation validator**

Run: `powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1`
Expected: passes (no broken links, no format violations). Fix any reported issue in the two edited/created files before proceeding.

- [ ] **Step 4: Commit**

```bash
git add docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md
git commit -m "docs: add state/command runtime contract V1 and deprecate legacy events contract"
```
