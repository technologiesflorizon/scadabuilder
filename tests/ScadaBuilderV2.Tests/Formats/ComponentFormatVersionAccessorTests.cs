using System.Text.Json.Nodes;
using ScadaBuilderV2.Application.ElementStudio;
using ScadaBuilderV2.Application.Formats;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// The component module already had a version before this mechanism existed. It is wired in rather than
/// doubled: two numbers on one file would raise a question - which one is authoritative - that has no good
/// answer.
/// </summary>
[TestClass]
public sealed class ComponentFormatVersionAccessorTests
{
    [TestMethod]
    public void TheGenerationIsReadFromTheExistingMetadataField()
    {
        var document = JsonNode.Parse("""{"Metadata":{"Schema":"s","SchemaVersion":1,"Format":"json.sep"}}""")!;

        Assert.AreEqual(1, ComponentFormatVersionAccessor.ReadGeneration(document));
    }

    [TestMethod]
    public void AComponentWithoutMetadataIsGenerationZero()
    {
        Assert.AreEqual(0, ComponentFormatVersionAccessor.ReadGeneration(JsonNode.Parse("{}")!));
        Assert.AreEqual(0, ComponentFormatVersionAccessor.ReadGeneration(JsonNode.Parse("""{"Metadata":{}}""")!));
    }

    [TestMethod]
    public void WritingTheGenerationUpdatesTheSameFieldTheReaderUses()
    {
        var document = JsonNode.Parse("""{"Metadata":{"Schema":"s","SchemaVersion":1}}""")!;

        ComponentFormatVersionAccessor.WriteGeneration(document, 2);

        Assert.AreEqual(2, ComponentFormatVersionAccessor.ReadGeneration(document));
        Assert.AreEqual(2, document["Metadata"]!["SchemaVersion"]!.GetValue<int>());
    }

    [TestMethod]
    public void TheShippedGenerationMatchesTheComponentMetadataConstant()
    {
        Assert.AreEqual(
            ElementStudioComponentMetadata.CurrentSchemaVersion,
            ScadaBuilderV2.Domain.Projects.ScadaFormatGeneration.Component,
            "the two constants describe the same thing and must never drift.");
    }
}
