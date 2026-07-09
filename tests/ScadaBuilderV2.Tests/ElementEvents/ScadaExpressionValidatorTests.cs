using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Tests.ElementEvents;

[TestClass]
public sealed class ScadaExpressionValidatorTests
{
    private static ScadaTagCatalog CreateResolverCatalog() => new(
        "tf100web-scada-tags-v1",
        new[]
        {
            new ScadaTagDefinition("tf100.mapping.196", "Noeud1_N15_04_Commande_MC_120C",
                KeywordLabel: "MC_120C", Datatype: "bool"),
            new ScadaTagDefinition("tf100.mapping.195", "Noeud1_N15_03_Commande_MC_120A",
                KeywordLabel: "MC_120A", Datatype: "bool"),
            new ScadaTagDefinition("tf100.mapping.200", "DuplicateLabel", Datatype: "float"),
            new ScadaTagDefinition("tf100.mapping.201", "DuplicateLabel", Datatype: "bool"),
        });

    [TestMethod]
    public void Resolve_ById_ReturnsResolved()
    {
        var result = ScadaExpressionValidator.TryResolveTagReference(
            "tf100.mapping.196", CreateResolverCatalog());
        Assert.AreEqual(TagResolveStatus.Resolved, result.Status);
        Assert.AreEqual("tf100.mapping.196", result.CanonicalId);
    }

    [TestMethod]
    public void Resolve_ByDisplayName_ReturnsResolved()
    {
        var result = ScadaExpressionValidator.TryResolveTagReference(
            "Noeud1_N15_04_Commande_MC_120C", CreateResolverCatalog());
        Assert.AreEqual(TagResolveStatus.Resolved, result.Status);
        Assert.AreEqual("tf100.mapping.196", result.CanonicalId);
    }

    [TestMethod]
    public void Resolve_ByKeywordLabel_ReturnsResolved()
    {
        var result = ScadaExpressionValidator.TryResolveTagReference(
            "MC_120C", CreateResolverCatalog());
        Assert.AreEqual(TagResolveStatus.Resolved, result.Status);
        Assert.AreEqual("tf100.mapping.196", result.CanonicalId);
    }

    [TestMethod]
    public void Resolve_IdHasPriorityOverLabel()
    {
        var catalog = new ScadaTagCatalog("v1", new[]
        {
            new ScadaTagDefinition("MC_120C", "AutreNom", Datatype: "bool"),
            new ScadaTagDefinition("tf100.mapping.196", "Noeud1_N15_04_Commande_MC_120C",
                KeywordLabel: "MC_120C", Datatype: "bool"),
        });
        var result = ScadaExpressionValidator.TryResolveTagReference("MC_120C", catalog);
        Assert.AreEqual(TagResolveStatus.Resolved, result.Status);
        Assert.AreEqual("MC_120C", result.CanonicalId,
            "Id match must take priority over KeywordLabel match.");
    }

    [TestMethod]
    public void Resolve_Unknown_ReturnsUnresolved()
    {
        var result = ScadaExpressionValidator.TryResolveTagReference(
            "TagInexistant", CreateResolverCatalog());
        Assert.AreEqual(TagResolveStatus.Unresolved, result.Status);
        Assert.IsNull(result.CanonicalId);
    }

    [TestMethod]
    public void Resolve_DuplicateLabel_ReturnsAmbiguous()
    {
        var result = ScadaExpressionValidator.TryResolveTagReference(
            "DuplicateLabel", CreateResolverCatalog());
        Assert.AreEqual(TagResolveStatus.Ambiguous, result.Status);
        Assert.IsNull(result.CanonicalId);
        Assert.IsTrue(result.Matches.Count == 2);
    }

    [TestMethod]
    public void Resolve_NullCatalog_ReturnsUnresolved()
    {
        var result = ScadaExpressionValidator.TryResolveTagReference("anything", null);
        Assert.AreEqual(TagResolveStatus.Unresolved, result.Status);
    }

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
