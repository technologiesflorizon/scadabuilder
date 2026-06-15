using System.Text.Json;
using System.Text.Json.Serialization;
using ScadaBuilderV2.Application.ElementStudio;

namespace ScadaBuilderV2.Infrastructure.ElementStudio;

public sealed class ElementStudioComponentPackageStore :
    IElementStudioComponentPackageReader,
    IElementStudioComponentPackageWriter
{
    public const string FileExtension = ".sep";

    public async Task<string> WriteToLibraryAsync(
        ElementStudioComponentPackage package,
        string libraryRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);

        var path = GetDefaultComponentPath(libraryRoot, package);
        return await WriteToPathAsync(package, path, cancellationToken);
    }

    public async Task<string> WriteToPathAsync(
        ElementStudioComponentPackage package,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        EnsureSepPath(packagePath);
        Validate(package);

        var fullPath = Path.GetFullPath(packagePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var write = File.Create(fullPath);
        await JsonSerializer.SerializeAsync(write, package, CreateJsonSerializerOptions(), cancellationToken);
        return fullPath;
    }

    public async Task<ElementStudioComponentPackage> ReadFromPathAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        EnsureSepPath(packagePath);

        await using var read = File.OpenRead(Path.GetFullPath(packagePath));
        var package = await JsonSerializer.DeserializeAsync<ElementStudioComponentPackage>(
            read,
            CreateJsonSerializerOptions(),
            cancellationToken);

        if (package is null)
        {
            throw new InvalidDataException("Studio Element+ component package is empty or invalid.");
        }

        Validate(package);
        return package;
    }

    public static string GetDefaultComponentPath(string libraryRoot, ElementStudioComponentPackage package)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        ArgumentNullException.ThrowIfNull(package);

        var component = package.Component ?? throw new ArgumentException("Component package must contain exactly one component.", nameof(package));
        var packageFileName = $"{ToSafeFileName(component.Name)}{FileExtension}";
        return Path.Combine(libraryRoot, packageFileName);
    }

    public static JsonSerializerOptions CreateJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public static void Validate(ElementStudioComponentPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var metadata = package.Metadata ?? throw new InvalidDataException("Studio Element+ component package metadata is required.");
        if (!string.Equals(metadata.Schema, ElementStudioComponentMetadata.CurrentSchema, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Studio Element+ component package schema is not supported.");
        }

        if (metadata.SchemaVersion != ElementStudioComponentMetadata.CurrentSchemaVersion)
        {
            throw new InvalidDataException("Studio Element+ component package schema version is not supported.");
        }

        if (!string.Equals(metadata.Format, ElementStudioComponentMetadata.CurrentFormat, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Studio Element+ component package format is not supported.");
        }

        var component = package.Component ?? throw new InvalidDataException("Studio Element+ component package must contain exactly one component.");
        if (string.IsNullOrWhiteSpace(component.ComponentId))
        {
            throw new InvalidDataException("Studio Element+ component id is required.");
        }

        if (string.IsNullOrWhiteSpace(component.Name))
        {
            throw new InvalidDataException("Studio Element+ component name is required.");
        }

        if (component.Visual is null)
        {
            throw new InvalidDataException("Studio Element+ component visual model is required.");
        }

        var assetItems = component.Assets ?? Array.Empty<ElementStudioEmbeddedAsset>();
        foreach (var asset in assetItems)
        {
            ValidateEmbeddedAsset(asset);
        }

        var duplicateAssetId = assetItems
            .GroupBy(asset => asset.AssetId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateAssetId is not null)
        {
            throw new InvalidDataException($"Embedded asset id '{duplicateAssetId.Key}' is duplicated.");
        }

        var assets = assetItems.ToDictionary(asset => asset.AssetId, StringComparer.Ordinal);

        ValidateVisual(component.Visual, assets);
        foreach (var part in component.Parts ?? Array.Empty<ElementStudioComponentPart>())
        {
            ValidatePart(part, assets);
        }
    }

    private static void ValidateVisual(
        ElementStudioComponentVisual visual,
        IReadOnlyDictionary<string, ElementStudioEmbeddedAsset> assets)
    {
        switch (visual.Kind)
        {
            case ElementStudioComponentVisualKind.Svg when string.IsNullOrWhiteSpace(visual.SvgMarkup):
                throw new InvalidDataException("SVG component packages require SVG markup.");
            case ElementStudioComponentVisualKind.Image:
                ValidateAssetReference(visual.ImageAssetId, assets, "Image component packages require an embedded image asset.");
                break;
            case ElementStudioComponentVisualKind.Html when string.IsNullOrWhiteSpace(visual.HtmlMarkup):
                throw new InvalidDataException("HTML component packages require HTML markup.");
        }
    }

    private static void ValidatePart(
        ElementStudioComponentPart part,
        IReadOnlyDictionary<string, ElementStudioEmbeddedAsset> assets)
    {
        if (string.IsNullOrWhiteSpace(part.PartId))
        {
            throw new InvalidDataException("Studio Element+ component parts require an id.");
        }

        if (part.Kind == ElementStudioComponentPartKind.Image)
        {
            ValidateAssetReference(part.ImageAssetId, assets, "Image component parts require an embedded image asset.");
        }

        foreach (var child in part.Children ?? Array.Empty<ElementStudioComponentPart>())
        {
            ValidatePart(child, assets);
        }
    }

    private static void ValidateEmbeddedAsset(ElementStudioEmbeddedAsset asset)
    {
        if (string.IsNullOrWhiteSpace(asset.AssetId))
        {
            throw new InvalidDataException("Embedded assets require an id.");
        }

        if (string.IsNullOrWhiteSpace(asset.FileName))
        {
            throw new InvalidDataException("Embedded assets require a file name.");
        }

        if (string.IsNullOrWhiteSpace(asset.MediaType) || !asset.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Embedded assets must declare an image media type.");
        }

        if (string.IsNullOrWhiteSpace(asset.Base64Data))
        {
            throw new InvalidDataException("Embedded assets must contain base64 data.");
        }

        try
        {
            Convert.FromBase64String(asset.Base64Data);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Embedded asset data is not valid base64.", exception);
        }
    }

    private static void ValidateAssetReference(
        string? assetId,
        IReadOnlyDictionary<string, ElementStudioEmbeddedAsset> assets,
        string message)
    {
        if (string.IsNullOrWhiteSpace(assetId) || !assets.ContainsKey(assetId))
        {
            throw new InvalidDataException(message);
        }
    }

    private static void EnsureSepPath(string packagePath)
    {
        var extension = Path.GetExtension(packagePath);
        if (!string.Equals(extension, FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Studio Element+ component packages must use the .sep extension.", nameof(packagePath));
        }
    }

    private static string ToSafeFileName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var invalid = Path.GetInvalidFileNameChars().Append('/').Append('\\').ToHashSet();
        var chars = value
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray();

        return new string(chars);
    }
}
