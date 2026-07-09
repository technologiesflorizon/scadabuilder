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
