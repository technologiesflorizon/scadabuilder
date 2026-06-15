using ScadaBuilderV2.Domain.Versioning;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class VersioningTests
{
    [TestMethod]
    public void IterationBumpOnlyIncrementsFourDigitIteration()
    {
        var version = new ScadaVersion(0, 0, 1);

        var next = version.Bump(VersionBumpKind.Iteration);

        Assert.AreEqual("V2.0.0.0002", next.ToString());
    }

    [TestMethod]
    public void FeatureBumpResetsIteration()
    {
        var version = new ScadaVersion(0, 0, 42);

        var next = version.Bump(VersionBumpKind.Feature);

        Assert.AreEqual("V2.0.1.0000", next.ToString());
    }

    [TestMethod]
    public void ProductionBumpResetsFeatureAndIteration()
    {
        var version = new ScadaVersion(0, 8, 141);

        var next = version.Bump(VersionBumpKind.Production);

        Assert.AreEqual("V2.1.0.0000", next.ToString());
    }
}
