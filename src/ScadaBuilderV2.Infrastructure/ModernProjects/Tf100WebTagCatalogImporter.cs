using System.Text.Json;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Infrastructure.ModernProjects;

/// <summary>
/// Imports TF100Web tag export files into the SCADA Builder V2 project tag catalog.
/// </summary>
/// <remarks>
/// Decisions: DEC-0015.
/// Contracts: docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md, docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs.
/// </remarks>
public sealed class Tf100WebTagCatalogImporter
{
    /// <summary>
    /// Gets the supported TF100Web tag export schema.
    /// </summary>
    public const string SupportedSchema = "tf100web-scada-tags-v1";

    /// <summary>
    /// Reads and validates a TF100Web tag JSON export file.
    /// </summary>
    public async Task<ScadaTagCatalog> ImportAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var read = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(read, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var schema = GetString(root, "schema");
        if (!string.Equals(schema, SupportedSchema, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Schema de tags non supporte: '{schema ?? "(absent)"}'.");
        }

        if (!root.TryGetProperty("tags", out var tagsElement) || tagsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Le fichier de tags ne contient pas de tableau 'tags'.");
        }

        var tags = tagsElement
            .EnumerateArray()
            .Where(tag => tag.ValueKind == JsonValueKind.Object)
            .Select(ReadTag)
            .Where(tag => !string.IsNullOrWhiteSpace(tag.Id))
            .GroupBy(tag => tag.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(tag => tag.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(tag => tag.Id, StringComparer.Ordinal)
            .ToArray();

        if (tags.Length == 0)
        {
            throw new InvalidOperationException("Le fichier de tags ne contient aucun tag valide.");
        }

        return new ScadaTagCatalog(
            SupportedSchema,
            tags,
            Path.GetFileName(path),
            DateTimeOffset.UtcNow);
    }

    private static ScadaTagDefinition ReadTag(JsonElement tag)
    {
        var id = GetString(tag, "id") ?? "";
        var keywordLabel = GetString(tag, "keyword_label");
        var device = GetString(tag, "device");
        var datatype = GetString(tag, "datatype_label") ?? GetString(tag, "datatype");
        var displayName = string.IsNullOrWhiteSpace(keywordLabel) ? id.Trim() : keywordLabel;

        return new ScadaTagDefinition(
            id.Trim(),
            displayName,
            keywordLabel,
            GetString(tag, "keyword_type"),
            device,
            GetString(tag, "protocol"),
            GetString(tag, "address_uri"),
            datatype,
            GetBoolean(tag, "writeable"),
            GetBoolean(tag, "enabled", defaultValue: true),
            GetString(tag, "unit"));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;
    }

    private static bool GetBoolean(JsonElement element, string propertyName, bool defaultValue = false)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True
            ? true
            : element.TryGetProperty(propertyName, out value) && value.ValueKind == JsonValueKind.False
                ? false
                : defaultValue;
    }
}
