using ScadaBuilderV2.Infrastructure.ModernProjects;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// Locks the raw pre-read of a format version. It must answer without deserialising into a model the
/// binary may not understand, which is the whole point: refusing requires reading a file we cannot parse.
/// </summary>
[TestClass]
public sealed class ArtifactFormatVersionReaderTests
{
    [TestMethod]
    public void AnAbsentFieldIsGenerationZero()
    {
        Assert.AreEqual(0, ArtifactFormatVersionReader.ReadFormatVersion("""{"Name":"Projet"}"""));
    }

    [TestMethod]
    public void ThePresentFieldIsReturned()
    {
        Assert.AreEqual(3, ArtifactFormatVersionReader.ReadFormatVersion("""{"Name":"P","FormatVersion":3}"""));
    }

    [TestMethod]
    public void TheFieldIsReadCaseInsensitivelyLikeTheSerializer()
    {
        Assert.AreEqual(2, ArtifactFormatVersionReader.ReadFormatVersion("""{"formatversion":2}"""));
    }

    /// <summary>A file we cannot parse is generation zero, not a crash: the open pipeline reports it later.</summary>
    [TestMethod]
    public void MalformedJsonIsGenerationZeroRatherThanAnException()
    {
        Assert.AreEqual(0, ArtifactFormatVersionReader.ReadFormatVersion("{ not json"));
        Assert.AreEqual(0, ArtifactFormatVersionReader.ReadFormatVersion(""));
    }

    [TestMethod]
    public void ANonIntegerFieldIsGenerationZero()
    {
        Assert.AreEqual(0, ArtifactFormatVersionReader.ReadFormatVersion("""{"FormatVersion":"trois"}"""));
        Assert.AreEqual(0, ArtifactFormatVersionReader.ReadFormatVersion("""{"FormatVersion":null}"""));
    }

    [TestMethod]
    public void ARootThatIsNotAnObjectIsGenerationZero()
    {
        Assert.AreEqual(0, ArtifactFormatVersionReader.ReadFormatVersion("[1,2,3]"));
    }
}
