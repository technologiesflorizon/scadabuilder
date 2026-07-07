using System.Globalization;

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
/// Decisions: DEC-0036.
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
