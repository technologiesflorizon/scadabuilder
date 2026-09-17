using System.Text.Json.Nodes;

namespace ScadaBuilderV2.Application.Formats;

/// <summary>Converts one artifact from one format generation to a later one.</summary>
/// <remarks>
/// A converter is a pure function on the deserialised tree: no disk, no network, no clock. That is what makes
/// it testable from a frozen input and what makes a conversion deterministic, so converting the same file
/// twice or on two machines produces identical bytes.
///
/// Converters are written one step at a time; the registry composes the chain. A converter may span several
/// steps when the change is trivial, but never over a step another converter claims.
///
/// No descending converter exists, today or later: it would have to invent the data it does not hold.
///
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C3, C7.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/ArtifactConverterRegistryTests.cs.
/// </remarks>
public interface IArtifactConverter
{
    /// <summary>Module this converter operates on.</summary>
    ArtifactModule Module { get; }

    /// <summary>Generation this converter accepts.</summary>
    int FromVersion { get; }

    /// <summary>Generation this converter produces.</summary>
    int ToVersion { get; }

    /// <summary>One sentence naming what this step changes, shown to the operator in the conversion plan.</summary>
    string StepDescription { get; }

    /// <summary>Returns the converted document. The input may be mutated and returned.</summary>
    JsonNode Convert(JsonNode document);
}
