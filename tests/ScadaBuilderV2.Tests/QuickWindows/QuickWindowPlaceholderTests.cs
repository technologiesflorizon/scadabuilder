using System.Text.Json;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.Tests.QuickWindows;

[TestClass]
public sealed class QuickWindowPlaceholderTests
{
    [DataTestMethod]
    [DataRow("#1", true, 1)]
    [DataRow("#999", true, 999)]
    [DataRow("#0", false, 0)]
    [DataRow("#01", false, 0)]
    [DataRow("#1000", false, 0)]
    [DataRow(" #1", false, 0)]
    [DataRow("FAN_03", false, 0)]
    public void TryParseNameAcceptsOnlyAnExactValidToken(string value, bool expected, int expectedNumber)
    {
        Assert.AreEqual(expected, QuickWindowPlaceholder.TryParseName(value, out var number));
        if (expected) Assert.AreEqual(expectedNumber, number);
    }

    [TestMethod]
    public void FindInTextSkipsEscapesAndLongerNumbers()
    {
        var tokens = QuickWindowPlaceholder.FindInText(@"Ventilateur #4 - pompe \#2 - #10 - #1000");

        CollectionAssert.AreEqual(new int?[] { 4, 10 }, tokens.Where(t => t.Number is not null).Select(t => t.Number).ToArray());
        Assert.IsFalse(tokens.Any(t => t.Raw == "#2"));
        Assert.IsFalse(tokens.Any(t => t.Raw == "#1"), "#1 inside #1000 is not a token");
    }

    [TestMethod]
    public void FindInTextReportsInvalidTokensWithoutANumber()
    {
        var token = QuickWindowPlaceholder.FindInText("Valeur #0").Single();

        Assert.AreEqual("#0", token.Raw);
        Assert.IsNull(token.Number);
    }

    [TestMethod]
    public void UnescapeTurnsBackslashHashIntoHash()
    {
        Assert.AreEqual("Pompe #2", QuickWindowPlaceholder.UnescapeText(@"Pompe \#2"));
    }
}

[TestClass]
public sealed class ParameterSheetProjectModelTests
{
    [TestMethod]
    public void QuickWindowIsAppendedLastSoNoPersistedValueMoves()
    {
        var values = Enum.GetValues<ScadaPageType>();

        Assert.AreEqual(ScadaPageType.QuickWindow, values[^1]);
        Assert.AreEqual(0, (int)ScadaPageType.Default);
        Assert.AreEqual(1, (int)ScadaPageType.Fragment);
    }

    [TestMethod]
    public void ACommandRoundTripsItsSheetAndPageKeys()
    {
        var sheet = Guid.NewGuid();
        var page = Guid.NewGuid();
        var command = new ScadaCommandBinding("c1", "Ouvrir", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
            ParameterSheetKey: sheet, QuickWindowPageKey: page);

        var back = JsonSerializer.Deserialize<ScadaCommandBinding>(JsonSerializer.Serialize(command))!;

        Assert.AreEqual(sheet, back.ParameterSheetKey);
        Assert.AreEqual(page, back.QuickWindowPageKey);
    }

    [TestMethod]
    public void AProjectWithoutSheetsHasAnEmptyEffectiveList()
    {
        Assert.AreEqual(0, ScadaProject.CreateDefault("p").EffectiveParameterSheets.Count);
    }

    [TestMethod]
    public void ExpressionAstIsOmittedFromJsonWhenNullAndPresentWhenSet()
    {
        var absent = QuickWindowBinding.Absent(Guid.NewGuid());
        Assert.IsFalse(JsonSerializer.Serialize(absent).Contains("ExpressionAst"),
            "Contract 12.4: ExpressionAst must be present only when SourceKind is Expression.");

        var withAst = absent with { ExpressionAst = new ScadaExprLiteralNumber(1) };
        Assert.IsTrue(JsonSerializer.Serialize(withAst).Contains("ExpressionAst"));
    }
}
