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
}
