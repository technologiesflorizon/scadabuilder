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
