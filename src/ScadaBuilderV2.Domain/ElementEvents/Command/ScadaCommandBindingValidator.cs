using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Domain.ElementEvents.Command;

/// <summary>
/// Validates that <see cref="ScadaCommandBinding"/> tag references use canonical Ids,
/// not human-readable labels.
/// </summary>
/// <remarks>
/// Decisions: DEC-0036, D9.
/// Contracts: docs/superpowers/specs/2026-07-09-expression-tag-id-reference.md §3.0.
/// </remarks>
public static class ScadaCommandBindingValidator
{
    /// <summary>
    /// Validates a single command binding's tag references against the catalog.
    /// Returns a list of issues (empty if valid).
    /// </summary>
    public static IReadOnlyList<string> ValidateCommandBinding(
        ScadaCommandBinding command, ScadaTagCatalog? catalog)
    {
        if (catalog?.Tags is null || catalog.Tags.Count == 0)
            return Array.Empty<string>();

        var issues = new List<string>();
        var tagIds = catalog.Tags
            .Select(t => t.Id)
            .ToHashSet(StringComparer.Ordinal);

        CheckTagId(command.WriteTagId, "WriteTagId", command.Name, catalog, tagIds, issues);
        CheckTagId(command.ReadTagId, "ReadTagId", command.Name, catalog, tagIds, issues);

        return issues;
    }

    private static void CheckTagId(
        string? tagValue, string field, string commandName,
        ScadaTagCatalog catalog, HashSet<string> canonicalIds,
        List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(tagValue)) return;
        if (canonicalIds.Contains(tagValue)) return; // already canonical

        var result = ScadaExpressionValidator.TryResolveTagReference(tagValue, catalog);
        if (result.Status == TagResolveStatus.Resolved && result.CanonicalId is not null)
        {
            issues.Add(
                $"La commande '{commandName}' utilise un libelle humain comme {field} " +
                $"('{tagValue}'). Remplacez-le par l'Id canonique '{result.CanonicalId}'.");
        }
    }
}
