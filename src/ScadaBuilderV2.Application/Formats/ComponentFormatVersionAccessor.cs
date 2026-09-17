using System.Text.Json.Nodes;

namespace ScadaBuilderV2.Application.Formats;

/// <summary>Reads and writes the `.sep` generation through the field the format already shipped.</summary>
/// <remarks>
/// `ElementStudioComponentMetadata.SchemaVersion` predates this mechanism and already describes the shape of
/// a component file. It is therefore the component module's format version, used as-is. Adding a parallel
/// `FormatVersion` beside it would create two numbers for one file and a question about which is
/// authoritative that has no good answer.
///
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C1.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/ComponentFormatVersionAccessorTests.cs.
/// </remarks>
public static class ComponentFormatVersionAccessor
{
    private const string MetadataField = "Metadata";
    private const string VersionField = "SchemaVersion";

    /// <summary>Returns the component's generation, or zero when the metadata block is absent.</summary>
    public static int ReadGeneration(JsonNode document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var version = document[MetadataField]?[VersionField];
        return version is not null && version.GetValueKind() == System.Text.Json.JsonValueKind.Number
            ? version.GetValue<int>()
            : 0;
    }

    /// <summary>Writes the component's generation, creating the metadata block when it is absent.</summary>
    public static void WriteGeneration(JsonNode document, int generation)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document[MetadataField] is not JsonObject metadata)
        {
            metadata = [];
            document[MetadataField] = metadata;
        }
        metadata[VersionField] = generation;
    }
}
