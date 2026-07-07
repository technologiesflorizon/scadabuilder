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
