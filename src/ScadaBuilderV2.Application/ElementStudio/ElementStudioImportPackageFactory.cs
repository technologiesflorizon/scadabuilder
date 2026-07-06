using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.ElementStudio;

public static class ElementStudioImportPackageFactory
{
    public static ElementStudioImportPackage Create(
        string packageId,
        string sourceProjectId,
        string sourceSceneId,
        string sourcePagePath,
        IEnumerable<ElementStudioLegacyItem> items,
        ElementStudioPackageMetadata metadata,
        string? targetLibraryPath = null,
        string? componentName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceProjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSceneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePagePath);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(metadata);

        var itemArray = items.ToArray();
        if (itemArray.Length == 0)
        {
            throw new ArgumentException("At least one legacy item is required.", nameof(items));
        }

        var packageBounds = CalculateOuterBounds(itemArray.Select(item => item.BoundsAbsolute));
        var relativeItems = itemArray
            .Select(item => item with
            {
                BoundsRelativeToPackage = new SceneBounds(
                    item.BoundsAbsolute.X - packageBounds.X,
                    item.BoundsAbsolute.Y - packageBounds.Y,
                    item.BoundsAbsolute.Width,
                    item.BoundsAbsolute.Height)
            })
            .ToArray();

        return new ElementStudioImportPackage(
            metadata,
            packageId,
            sourceProjectId,
            sourceSceneId,
            sourcePagePath,
            targetLibraryPath,
            packageBounds,
            relativeItems,
            componentName);
    }

    private static SceneBounds CalculateOuterBounds(IEnumerable<SceneBounds> bounds)
    {
        var boundsArray = bounds.ToArray();
        var left = boundsArray.Min(bound => bound.X);
        var top = boundsArray.Min(bound => bound.Y);
        var right = boundsArray.Max(bound => bound.X + bound.Width);
        var bottom = boundsArray.Max(bound => bound.Y + bound.Height);

        return new SceneBounds(left, top, right - left, bottom - top);
    }
}
