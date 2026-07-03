using ScadaBuilderV2.Application.ElementStudio;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ElementStudioSourceProjectIdTests
{
    [TestMethod]
    public void ResolveEffectiveProjectIdReturnsReferenceProjectWhenNoProjectLoaded()
    {
        var result = ElementStudioSourceProjectId.ResolveEffectiveProjectId("Projet non charge");

        Assert.AreEqual("AMR_REF_SCADA_V2", result);
    }

    [TestMethod]
    public void ResolveEffectiveProjectIdReturnsReferenceProjectWhenSourceProjectIdUnknown()
    {
        var result = ElementStudioSourceProjectId.ResolveEffectiveProjectId("Projet inconnu");

        Assert.AreEqual("AMR_REF_SCADA_V2", result);
    }

    [TestMethod]
    public void ResolveEffectiveProjectIdReturnsReferenceProjectWhenNullOrWhitespace()
    {
        Assert.AreEqual("AMR_REF_SCADA_V2", ElementStudioSourceProjectId.ResolveEffectiveProjectId(null));
        Assert.AreEqual("AMR_REF_SCADA_V2", ElementStudioSourceProjectId.ResolveEffectiveProjectId("   "));
    }

    [TestMethod]
    public void ResolveEffectiveProjectIdPreservesRealProjectId()
    {
        var result = ElementStudioSourceProjectId.ResolveEffectiveProjectId("AMR_REF_SCADA_V2");

        Assert.AreEqual("AMR_REF_SCADA_V2", result);
    }

    [TestMethod]
    public void ResolveEffectiveProjectIdPreservesUnrecognizedRealProjectId()
    {
        var result = ElementStudioSourceProjectId.ResolveEffectiveProjectId("SomeOtherProject");

        Assert.AreEqual("SomeOtherProject", result);
    }
}
