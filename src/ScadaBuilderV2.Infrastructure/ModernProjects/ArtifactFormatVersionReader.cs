using System.Text.Json;

namespace ScadaBuilderV2.Infrastructure.ModernProjects;

/// <summary>Reads a persisted artifact's format generation without deserialising it.</summary>
/// <remarks>
/// Refusing a file the binary does not understand requires reading it first, and deserialising into the
/// current model is exactly what must not happen: unknown properties would be dropped silently and written
/// away on the next save. This reads the one field it needs from the raw document and nothing else.
///
/// Case-insensitivity mirrors `ModernProjectStore.JsonOptions.PropertyNameCaseInsensitive`, so the reader and
/// the serializer never disagree about which field they are looking at.
///
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C1, C2.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/ArtifactFormatVersionReaderTests.cs.
/// </remarks>
public static class ArtifactFormatVersionReader
{
    /// <summary>Name of the field carrying the generation in every module but the component.</summary>
    public const string FieldName = "FormatVersion";

    /// <summary>Returns the declared generation, or zero when the field is absent or unusable.</summary>
    /// <remarks>
    /// Malformed input answers zero rather than throwing. A file that cannot be parsed is not a version
    /// problem, and the open pipeline already reports it with its own diagnostic further down.
    /// </remarks>
    public static int ReadFormatVersion(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return 0;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return 0;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, FieldName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                return property.Value.ValueKind == JsonValueKind.Number
                    && property.Value.TryGetInt32(out var generation)
                        ? generation
                        : 0;
            }
        }
        catch (JsonException)
        {
            return 0;
        }

        return 0;
    }
}
