using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.Tests.QuickWindows;

[TestClass]
public sealed class ParameterSheetParserTests
{
    private const string Example =
        "// Ventilateur condenseur 3\r\n" +
        "#1 := {FAN_03_RUN}\r\n" +
        "#2 := {FAN_03_FAULT}\r\n" +
        "#3 := {FAN_03_SPEED} * 0.1\r\n" +
        "#4 := \"Ventilateur 3\"\r\n" +
        "#5 := 1\r\n";

    [TestMethod]
    public void TheSpecExampleParsesIntoFiveClassifiedEntries()
    {
        var result = ParameterSheetParser.Parse(Example);

        Assert.IsTrue(result.IsValid, string.Join(" | ", result.Diagnostics.Select(d => $"{d.Line}:{d.Message}")));
        Assert.AreEqual(5, result.Entries.Count);
        Assert.AreEqual(ParameterValueKind.Tag, result.Entries[1].Kind);
        Assert.AreEqual(ParameterValueKind.Tag, result.Entries[2].Kind);
        Assert.AreEqual(ParameterValueKind.Expression, result.Entries[3].Kind);
        Assert.AreEqual(ParameterValueKind.Literal, result.Entries[4].Kind);
        Assert.AreEqual(ParameterValueKind.Expression, result.Entries[5].Kind, "R1: a numeric literal is a one-node Expression");
        Assert.AreEqual(2, result.Entries[1].Line);
        Assert.AreEqual("{FAN_03_SPEED} * 0.1", result.Entries[3].Source);
    }

    [TestMethod]
    public void ATrailingCommentIsStrippedButNotInsideAStringLiteral()
    {
        var result = ParameterSheetParser.Parse("#1 := {A} // commentaire\n#4 := \"http://x\"");

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("{A}", result.Entries[1].Source);
        Assert.AreEqual(ParameterValueKind.Literal, result.Entries[4].Kind);
        Assert.AreEqual("http://x", ((ScadaExprLiteralString)result.Entries[4].Node).Value);
    }

    [TestMethod]
    public void BlankAndWhitespaceLinesAreIgnored()
    {
        Assert.IsTrue(ParameterSheetParser.Parse("\n   \n\t\n").IsValid);
        Assert.AreEqual(0, ParameterSheetParser.Parse("\n   \n").Entries.Count);
    }

    [DataTestMethod]
    [DataRow("#0 := 1")]
    [DataRow("#1000 := 1")]
    [DataRow("#01 := 1")]
    [DataRow("1 := 1")]
    [DataRow("#1 = 1")]
    [DataRow("#1 :=")]
    public void AMalformedLineIsReportedWithItsLineNumber(string line)
    {
        var result = ParameterSheetParser.Parse("// en-tete\n" + line);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(2, result.Diagnostics.Single().Line);
    }

    [TestMethod]
    public void ADuplicatedNumberIsAnErrorNamingBothLines()
    {
        var result = ParameterSheetParser.Parse("#1 := {A}\n#2 := {B}\n#1 := {C}");

        Assert.IsFalse(result.IsValid);
        var message = result.Diagnostics.Single().Message;
        StringAssert.Contains(message, "#1");
        StringAssert.Contains(message, "ligne 1");
        Assert.AreEqual(3, result.Diagnostics.Single().Line);
    }

    [TestMethod]
    public void ARightHandSideMayNotReferenceAnotherPlaceholder()
    {
        var result = ParameterSheetParser.Parse("#1 := {#2}");

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Diagnostics.Single().Message, "#2");
    }

    [TestMethod]
    public void AnExpressionSyntaxErrorIsReportedOnItsLine()
    {
        var result = ParameterSheetParser.Parse("#1 := {A} +");

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(1, result.Diagnostics.Single().Line);
    }

    [TestMethod]
    public void ParsingIsDeterministic()
    {
        var first = ParameterSheetParser.Parse(Example);
        var second = ParameterSheetParser.Parse(Example);

        CollectionAssert.AreEqual(first.Entries.Keys.OrderBy(k => k).ToArray(), second.Entries.Keys.OrderBy(k => k).ToArray());
        Assert.AreEqual(first.Entries[3], second.Entries[3]);
    }

    [TestMethod]
    public void NineHundredNinetyNineLinesParse()
    {
        var text = string.Join("\n", Enumerable.Range(1, 999).Select(n => $"#{n} := {{T{n}}}"));

        var result = ParameterSheetParser.Parse(text);

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(999, result.Entries.Count);
    }

    [DataTestMethod]
    [DataRow("{A}", ParameterValueKind.Tag)]
    [DataRow("\"x\"", ParameterValueKind.Literal)]
    [DataRow("1", ParameterValueKind.Expression)]
    [DataRow("true", ParameterValueKind.Expression)]
    [DataRow("{A} * 0.1", ParameterValueKind.Expression)]
    [DataRow("{A} && {B}", ParameterValueKind.Expression)]
    public void ClassifyFollowsRulingR1(string source, ParameterValueKind expected)
    {
        var node = ScadaExpressionParser.Parse(source).Root!;

        Assert.AreEqual(expected, ParameterSheetParser.Classify(node));
    }
}
