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

    [TestMethod]
    public void FromAst_PrefersTagIdOverTagName()
    {
        var tagRef = new ScadaExprTagRef("PE_16", "tf100.mapping.161");
        var expr = ScadaExpression.FromAst(
            "{PE_16} == true",
            new ScadaExprBinary(ScadaExprBinaryOp.Equal,
                tagRef,
                new ScadaExprLiteralBool(true)));

        CollectionAssert.Contains(expr.ReferencedTags.ToList(), "tf100.mapping.161");
    }

    [TestMethod]
    public void FromAst_LegacyTagRefWithoutTagId_UsesTagName()
    {
        var tagRef = new ScadaExprTagRef("LegacyName");
        var expr = ScadaExpression.FromAst(
            "{LegacyName} == true",
            new ScadaExprBinary(ScadaExprBinaryOp.Equal,
                tagRef,
                new ScadaExprLiteralBool(true)));

        CollectionAssert.Contains(expr.ReferencedTags.ToList(), "LegacyName");
    }

    [TestMethod]
    public void FromAst_MultipleTagRefs_MixedTagIds()
    {
        var left = new ScadaExprTagRef("PE_16", "tf100.mapping.161");
        var right = new ScadaExprTagRef("LegacyName");
        var expr = ScadaExpression.FromAst(
            "{PE_16} && {LegacyName}",
            new ScadaExprBinary(ScadaExprBinaryOp.And,
                left,
                right));

        var tags = expr.ReferencedTags.ToList();
        CollectionAssert.Contains(tags, "tf100.mapping.161");
        CollectionAssert.Contains(tags, "LegacyName");
    }
}
