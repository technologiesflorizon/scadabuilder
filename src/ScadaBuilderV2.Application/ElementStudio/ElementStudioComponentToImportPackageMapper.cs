using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.ElementStudio;

/// <summary>
/// Converts a saved `.sep` component back into an <see cref="ElementStudioImportPackage"/> so an
/// already-published library component can be re-opened and re-edited in Studio Element+ through
/// the existing import pipeline, instead of only being buildable from fresh legacy captures.
/// </summary>
/// <remarks>
/// Decisions: DEC-0035. Contracts: MENUS_AND_SURFACES_CONTRACT_V2.md rule 25.
/// Tests: ElementStudioComponentToImportPackageMapperTests.cs.
/// </remarks>
public static class ElementStudioComponentToImportPackageMapper
{
    public static ElementStudioImportPackage ToEditablePackage(
        ElementStudioComponentPackage sepPackage,
        string sepFilePath,
        string createdByVersion)
    {
        ArgumentNullException.ThrowIfNull(sepPackage);
        ArgumentException.ThrowIfNullOrWhiteSpace(sepFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdByVersion);

        var component = sepPackage.Component;
        var flattenedParts = FlattenParts(component.Parts);
        if (flattenedParts.Count == 0)
        {
            throw new InvalidOperationException(
                $"Le composant '{component.Name}' ne contient aucune partie editable.");
        }

        var items = flattenedParts
            .Select((part, index) => new ElementStudioLegacyItem(
                part.PartId,
                part.Name,
                part.Kind.ToString(),
                part.Bounds,
                new SceneBounds(0, 0, 0, 0),
                part.Geometry,
                part.HtmlMarkup,
                part.Text,
                part.Style,
                index,
                null))
            .ToArray();

        var sourceTrace = component.SourceTrace;
        var targetLibraryPath = Path.GetDirectoryName(sepFilePath);
        if (string.IsNullOrWhiteSpace(targetLibraryPath))
        {
            throw new ArgumentException(
                $"Impossible de resoudre le dossier contenant '{sepFilePath}'.", nameof(sepFilePath));
        }

        return ElementStudioImportPackageFactory.Create(
            $"studio_edit_{component.ComponentId}_{Guid.NewGuid():N}",
            sourceTrace?.SourceProjectId ?? "AMR_REF_SCADA_V2",
            sourceTrace?.SourceSceneId ?? component.ComponentId,
            sourceTrace?.SourcePagePath ?? sepFilePath,
            items,
            ElementStudioPackageMetadata.Current(createdByVersion),
            targetLibraryPath,
            component.Name);
    }

    private static List<ElementStudioComponentPart> FlattenParts(IReadOnlyList<ElementStudioComponentPart> parts)
    {
        var flattened = new List<ElementStudioComponentPart>();
        foreach (var part in parts)
        {
            if (part.Kind == ElementStudioComponentPartKind.Group && part.ChildParts.Count > 0)
            {
                flattened.AddRange(FlattenParts(part.ChildParts));
            }
            else
            {
                flattened.Add(part);
            }
        }

        return flattened;
    }
}
