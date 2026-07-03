using ScadaBuilderV2.Application.ElementStudio;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ElementStudioComponentCopyNamingTests
{
    [TestMethod]
    public void GenerateCopyNameReturnsFirstSuffixWhenNoCollision()
    {
        var result = ElementStudioComponentCopyNaming.GenerateCopyName("Vanne", Array.Empty<string>());

        Assert.AreEqual("Vanne_copie1", result);
    }

    [TestMethod]
    public void GenerateCopyNameSkipsExistingSuffixes()
    {
        var result = ElementStudioComponentCopyNaming.GenerateCopyName(
            "Vanne",
            new[] { "Vanne_copie1", "Vanne_copie2" });

        Assert.AreEqual("Vanne_copie3", result);
    }

    [TestMethod]
    public void GenerateCopyNameIgnoresUnrelatedNames()
    {
        var result = ElementStudioComponentCopyNaming.GenerateCopyName(
            "Vanne",
            new[] { "Pompe", "Vanne_copie1" });

        Assert.AreEqual("Vanne_copie2", result);
    }

    [TestMethod]
    public void GenerateCopyNameThrowsOnEmptyBaseName()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            ElementStudioComponentCopyNaming.GenerateCopyName("", Array.Empty<string>()));
    }
}
