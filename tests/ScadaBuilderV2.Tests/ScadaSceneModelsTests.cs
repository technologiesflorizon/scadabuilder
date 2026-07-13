using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ScadaSceneModelsTests
{
    [TestMethod]
    public void ScadaElementStyleDefaultsToNotFlipped()
    {
        var style = ScadaElementStyle.DefaultText;

        Assert.IsFalse(style.FlipHorizontally);
        Assert.IsFalse(style.FlipVertically);
    }

    [TestMethod]
    public void ScadaElementStyleWithExpressionTogglesFlipIndependently()
    {
        var style = ScadaElementStyle.DefaultText;

        var flippedHorizontally = style with { FlipHorizontally = true };
        Assert.IsTrue(flippedHorizontally.FlipHorizontally);
        Assert.IsFalse(flippedHorizontally.FlipVertically);

        var flippedBoth = flippedHorizontally with { FlipVertically = true };
        Assert.IsTrue(flippedBoth.FlipHorizontally);
        Assert.IsTrue(flippedBoth.FlipVertically);
    }

    [TestMethod]
    public void ScadaElementStyleAdvancedFieldsHaveBackwardCompatibleDefaults()
    {
        var style = ScadaElementStyle.DefaultText;

        Assert.AreEqual("Normal", style.FontWeight);
        Assert.AreEqual("Normal", style.FontStyle);
        Assert.IsNull(style.TextDecoration);
        Assert.AreEqual("Left", style.TextAlign);
        Assert.AreEqual("None", style.TextTransform);
        Assert.AreEqual(0, style.LetterSpacing);
        Assert.AreEqual(0, style.LineHeight);
        Assert.IsNull(style.BorderRadius);
    }

    [TestMethod]
    public void ScadaBorderRadiusNormalizesNegativeValuesAndDetectsUniformity()
    {
        var radius = new ScadaBorderRadius(-4, 8, 8, 8).Normalized();

        Assert.AreEqual(0, radius.TopLeft);
        Assert.AreEqual(8, radius.TopRight);
        Assert.IsFalse(radius.IsUniform);
        Assert.IsTrue(new ScadaBorderRadius(8, 8, 8, 8).IsUniform);
    }
}
