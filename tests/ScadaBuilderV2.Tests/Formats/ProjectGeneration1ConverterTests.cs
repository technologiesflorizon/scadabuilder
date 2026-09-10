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

    [TestMethod]
    public void TwoPagesWithDifferentCodesReceiveDifferentKeys()
    {
        var document = Converter.Convert(JsonNode.Parse("""
        {"Name":"P","Scenes":[
            {"Id":"win00001","PageCode":"win00001"},
            {"Id":"win00002","PageCode":"win00002"}
        ]}
        """)!);

        var first = document["Scenes"]![0]!["PageKey"]!.GetValue<string>();
        var second = document["Scenes"]![1]!["PageKey"]!.GetValue<string>();

        Assert.AreNotEqual(first, second);
    }
}
