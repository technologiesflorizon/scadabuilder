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

    [TestMethod]
    public void ColorFilterProperties_DefaultToNull()
    {
        var effect = ScadaEffectBlock.Empty;
        Assert.IsNull(effect.ColorFilterColor);
        Assert.IsNull(effect.ColorFilterOpacity);
        Assert.IsNull(effect.ColorFilterHalo);
        Assert.IsNull(effect.ColorFilterHaloColor);
    }

    [TestMethod]
    public void ColorFilterProperties_CanBeSetIndependentlyOfBackgroundColor()
    {
        var effect = ScadaEffectBlock.Empty with
        {
            ColorFilterColor = "#E53935",
            ColorFilterOpacity = 0.35,
            ColorFilterHalo = true,
            ColorFilterHaloColor = "#E53935"
        };
        Assert.IsNull(effect.BackgroundColor);
        Assert.AreEqual("#E53935", effect.ColorFilterColor);
        Assert.AreEqual(0.35, effect.ColorFilterOpacity);
        Assert.IsTrue(effect.ColorFilterHalo);
        Assert.AreEqual("#E53935", effect.ColorFilterHaloColor);
    }
}
