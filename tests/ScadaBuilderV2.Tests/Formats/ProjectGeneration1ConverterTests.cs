using System.IO;
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

    /// <summary>
    /// Fix round 2 of the Task 7 review: <c>PageKeyFactory.CreateDeterministic</c> throws an unfiltered
    /// <see cref="ArgumentException"/> on a blank project name, the same as it does on a blank page code.
    /// A truncated or hand-edited <c>project.json</c> with no <c>Name</c> is invalid data, not a caller-bug
    /// signal, so it must surface as <see cref="InvalidDataException"/> — a type
    /// <c>ProjectWorkspaceRepository.OpenAsync</c>'s catch filter already handles.
    /// </summary>
    [TestMethod]
    public void AMissingProjectNameFailsConversionWithInvalidDataException()
    {
        var exception = Assert.ThrowsException<InvalidDataException>(() =>
            Converter.Convert(JsonNode.Parse("""
            {"Scenes":[{"Id":"win00001","PageCode":"win00001"}]}
            """)!));

        StringAssert.Contains(exception.Message, "Name");
    }

    /// <summary>
    /// Symmetric test for the round-1 fix: a page with neither <c>PageCode</c> nor <c>Id</c> has no identity to
    /// settle. The guard must reject it with <see cref="InvalidDataException"/> (not the unfiltered
    /// <see cref="ArgumentException"/> <see cref="PageKeyFactory.CreateDeterministic"/> would throw), and the
    /// message must identify which page failed -- by index, and by whatever identifying field it does carry --
    /// since that identification is what makes the guard useful in the field.
    /// </summary>
    [TestMethod]
    public void APageWithNoCodeAndNoIdFailsConversionWithInvalidDataException()
    {
        var exception = Assert.ThrowsException<InvalidDataException>(() =>
            Converter.Convert(JsonNode.Parse("""
            {"Name":"P","Scenes":[{"Title":"Accueil"}]}
            """)!));

        StringAssert.Contains(exception.Message, "ne porte aucun code de page");
        StringAssert.Contains(exception.Message, "index 0");
        StringAssert.Contains(exception.Message, "Accueil");
    }

    /// <summary>
    /// A distinct shape from the missing-field case above: the page carries a <c>PageCode</c> that is present
    /// but blank. <c>PageCode ?? Id ?? ""</c> does not fall through to <c>Id</c> here because the value is not
    /// null, so this exercises a different branch of the guard than the missing-field test does, and the
    /// message must still identify the page -- here by <c>Id</c>, since one is present.
    /// </summary>
    [TestMethod]
    public void APageWithAWhitespacePageCodeFailsConversionWithInvalidDataException()
    {
        var exception = Assert.ThrowsException<InvalidDataException>(() =>
            Converter.Convert(JsonNode.Parse("""
            {"Name":"P","Scenes":[{"Id":"win00002","PageCode":"   "}]}
            """)!));

        StringAssert.Contains(exception.Message, "ne porte aucun code de page");
        StringAssert.Contains(exception.Message, "win00002");
    }
}
