using ScadaBuilderV2.Domain.ElementEvents.State;

namespace ScadaBuilderV2.Tests.ElementEvents;

[TestClass]
public sealed class ScadaEffectBlockTests
{
    [TestMethod]
    public void EmptyEffectBlockHasAllNullProperties()
    {
        var effect = ScadaEffectBlock.Empty;

        Assert.IsNull(effect.BackgroundColor);
        Assert.IsNull(effect.BorderColor);
        Assert.IsNull(effect.BorderWidth);
        Assert.IsNull(effect.TextColor);
        Assert.IsNull(effect.TextContent);
        Assert.IsNull(effect.TextVisible);
        Assert.IsNull(effect.ElementVisible);
        Assert.IsNull(effect.Opacity);
        Assert.IsNull(effect.Rotation);
        Assert.IsNull(effect.Animation);
    }

    [TestMethod]
    public void EffectBlockRecordSupportsWithExpressionOverrides()
    {
        var baseline = ScadaEffectBlock.Empty with { BackgroundColor = "#00FF00" };
        var updated = baseline with { Animation = ScadaAnimation.Blink };

        Assert.AreEqual("#00FF00", updated.BackgroundColor);
        Assert.AreEqual(ScadaAnimation.Blink, updated.Animation);
    }
}
