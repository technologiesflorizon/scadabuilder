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
