using System.Text.Json.Nodes;
using ScadaBuilderV2.Application.Formats;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Infrastructure.ModernProjects.Converters;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// Locks the converter that absorbs the page-identity rules `ModernProjectMigration` used to re-derive on
/// every load. Once a project is converted and saved, those rules are dead code and are deleted; that is the
/// gain shape-sniffing can never deliver.
/// </summary>
[TestClass]
public sealed class ProjectGeneration1ConverterTests
{
    private static readonly ProjectGeneration1Converter Converter = new();

    [TestMethod]
    public void ItDeclaresTheStepItCovers()
    {
        Assert.AreEqual(ArtifactModule.Project, Converter.Module);
        Assert.AreEqual(0, Converter.FromVersion);
        Assert.AreEqual(1, Converter.ToVersion);
        Assert.IsFalse(string.IsNullOrWhiteSpace(Converter.StepDescription));
    }

    [TestMethod]
    public void ItStampsTheGenerationItProduces()
    {
        var document = Converter.Convert(JsonNode.Parse("""{"Name":"P","Scenes":[]}""")!);

        Assert.AreEqual(1, document["FormatVersion"]!.GetValue<int>());
    }

    [TestMethod]
    public void APageWithoutAKeyReceivesAStableOne()
    {
        var document = Converter.Convert(JsonNode.Parse("""
        {"Name":"P","Scenes":[{"Id":"win00001","Title":"Accueil","PageCode":"win00001"}]}
        """)!);

        var key = document["Scenes"]![0]!["PageKey"]!.GetValue<string>();
        Assert.AreNotEqual(Guid.Empty.ToString(), key);
        Assert.IsTrue(Guid.TryParse(key, out _));
    }

    /// <summary>Converting twice produces identical bytes: a key derived once is derived the same way again.</summary>
    [TestMethod]
    public void ConversionIsDeterministicAcrossRuns()
    {
        const string source = """{"Name":"P","Scenes":[{"Id":"win00001","PageCode":"win00001"}]}""";

        var first = Converter.Convert(JsonNode.Parse(source)!).ToJsonString();
        var second = Converter.Convert(JsonNode.Parse(source)!).ToJsonString();

        Assert.AreEqual(first, second, "a converted project must not depend on when or where it was converted.");
    }

    [TestMethod]
    public void AnExistingPageKeyIsPreserved()
    {
        const string key = "11111111-2222-3333-4444-555555555555";
        var document = Converter.Convert(JsonNode.Parse($$"""
        {"Name":"P","Scenes":[{"Id":"win00001","PageKey":"{{key}}"}]}
        """)!);

        Assert.AreEqual(key, document["Scenes"]![0]!["PageKey"]!.GetValue<string>());
    }

    /// <summary>
    /// Ruling 17: the converter must agree exactly with the derivation already used everywhere else in the
    /// product, not invent a second, incompatible one. This is the test that makes a future deletion of the
    /// overlapping rule in ModernProjectMigration provable.
    /// </summary>
    [TestMethod]
    public void AConvertedKeyMatchesPageKeyFactoryExactly()
    {
        var document = Converter.Convert(JsonNode.Parse("""
        {"Name":"P","Scenes":[{"Id":"win00001","PageCode":"win00001"}]}
        """)!);

        var key = document["Scenes"]![0]!["PageKey"]!.GetValue<string>();
        var expected = PageKeyFactory.CreateDeterministic("P", "win00001").ToString("D");

        Assert.AreEqual(expected, key);
    }

    /// <summary>
    /// This is the test that actually separates the correct derivation from the deleted <c>DeriveKey</c>: the
    /// old, buggy derivation was a function of the page code alone and ignored the project name entirely, so
    /// two pages with the same code but different project names would have collided under it. Two different
    /// codes (the prior, non-discriminating version of this test) would have passed under either derivation
    /// and guarded nothing.
    /// </summary>
    [TestMethod]
    public void TwoProjectsWithTheSamePageCodeReceiveDifferentKeys()
    {
        var first = Converter.Convert(JsonNode.Parse("""
        {"Name":"Alpha","Scenes":[{"Id":"win00001","PageCode":"win00001"}]}
        """)!)["Scenes"]![0]!["PageKey"]!.GetValue<string>();

        var second = Converter.Convert(JsonNode.Parse("""
        {"Name":"Beta","Scenes":[{"Id":"win00001","PageCode":"win00001"}]}
        """)!)["Scenes"]![0]!["PageKey"]!.GetValue<string>();

        Assert.AreNotEqual(first, second);
    }

    /// <summary>
    /// Pins the actual bytes <see cref="PageKeyFactory.CreateDeterministic"/> produces for one known
    /// (project name, page code) pair. Nothing else in the repository pins this literal, and a conversion
    /// writes the resulting identity permanently to disk: if the salt or byte layout of
    /// <c>CreateDeterministic</c> ever changed, every already-converted project's page identity would shift
    /// silently. Asserting against a hardcoded literal (computed once by actually running the code, not by
    /// calling <c>CreateDeterministic</c> a second time) is what would catch that.
    /// </summary>
    [TestMethod]
    public void AConvertedKeyMatchesThePinnedLiteralForAKnownNameAndCode()
    {
        var document = Converter.Convert(JsonNode.Parse("""
        {"Name":"P","Scenes":[{"Id":"win00001","PageCode":"win00001"}]}
        """)!);

        var key = document["Scenes"]![0]!["PageKey"]!.GetValue<string>();

        Assert.AreEqual("e6ce2175-1047-5dbc-ac17-fdaa0840b615", key);
    }
}
