using ScadaBuilderV2.Application.ElementStudio;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ElementStudioComponentNamingTests
{
    [TestMethod]
    public void ResolveDefaultComponentNameUsesFirstImportedSourceName()
    {
        var result = ElementStudioComponentNaming.ResolveDefaultComponentName(new[] { "Condenseur", "Condenseur_shadow" });

        Assert.AreEqual("Condenseur", result);
    }

    [TestMethod]
    public void ResolveDefaultComponentNameSkipsBlankNamesBeforeFirstNonBlank()
    {
        var result = ElementStudioComponentNaming.ResolveDefaultComponentName(new[] { "", "   ", "Ventilateur" });

        Assert.AreEqual("Ventilateur", result);
    }

    [TestMethod]
    public void ResolveDefaultComponentNameFallsBackToPlaceholderWhenAllNamesBlank()
    {
        var result = ElementStudioComponentNaming.ResolveDefaultComponentName(new[] { "", "  " });

        Assert.AreEqual(ElementStudioComponentNaming.DefaultComponentName, result);
    }

    [TestMethod]
    public void ResolveDefaultComponentNameFallsBackToPlaceholderWhenNoItems()
    {
        var result = ElementStudioComponentNaming.ResolveDefaultComponentName(Array.Empty<string>());

        Assert.AreEqual(ElementStudioComponentNaming.DefaultComponentName, result);
    }

    [TestMethod]
    public void ResolveDefaultComponentNameThrowsOnNullList()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            ElementStudioComponentNaming.ResolveDefaultComponentName(null!));
    }
}
