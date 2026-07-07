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
