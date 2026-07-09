using System.IO;
using System.Text.Json;
using ScadaBuilderV2.Application.ElementStudio;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.ElementStudio.App;

public sealed record ElementStudioLoadResult(
    ElementStudioImportPackage Package,
    IReadOnlyList<string> Diagnostics);

public static class ElementStudioPackageLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static ElementStudioLoadResult Load(string? packagePath)
    {
        var diagnostics = new List<string>();

        if (string.IsNullOrWhiteSpace(packagePath))
        {
            diagnostics.Add("Aucun package .ft1 fourni au lancement.");
            return new ElementStudioLoadResult(CreateEmptyPackage(), diagnostics);
        }

        if (!File.Exists(packagePath))
        {
            diagnostics.Add($"Package introuvable: {packagePath}");
            return new ElementStudioLoadResult(CreateEmptyPackage(packagePath), diagnostics);
        }

        try
        {
            var json = File.ReadAllText(packagePath);
            var package = JsonSerializer.Deserialize<ElementStudioImportPackage>(json, JsonOptions);

            if (package is null)
            {
                diagnostics.Add("Package .ft1 vide ou non reconnu.");
                return new ElementStudioLoadResult(CreateEmptyPackage(packagePath), diagnostics);
            }

            diagnostics.Add($"Package charge: {Path.GetFileName(packagePath)}");
            diagnostics.Add($"{package.Items.Count} element(s) importe(s).");
            return new ElementStudioLoadResult(Normalize(package, packagePath), diagnostics);
        }
        catch (JsonException exception)
        {
            diagnostics.Add($"Format .ft1 non parse pour l'instant: {exception.Message}");
            diagnostics.Add("Le Studio reste ouvert pour permettre l'integration progressive du writer.");
            return new ElementStudioLoadResult(CreateEmptyPackage(packagePath), diagnostics);
        }
        catch (IOException exception)
        {
            diagnostics.Add($"Lecture du package impossible: {exception.Message}");
            return new ElementStudioLoadResult(CreateEmptyPackage(packagePath), diagnostics);
        }
        catch (UnauthorizedAccessException exception)
        {
            diagnostics.Add($"Acces refuse au package: {exception.Message}");
            return new ElementStudioLoadResult(CreateEmptyPackage(packagePath), diagnostics);
        }
        catch (Exception exception)
        {
            diagnostics.Add($"Erreur inattendue au chargement du package: {exception.GetType().Name}: {exception.Message}");
            diagnostics.Add("Le Studio reste ouvert pour permettre le diagnostic du probleme.");
            return new ElementStudioLoadResult(CreateEmptyPackage(packagePath), diagnostics);
        }
    }

    private static ElementStudioImportPackage Normalize(ElementStudioImportPackage package, string packagePath)
    {
        var items = (package.Items ?? Array.Empty<ElementStudioLegacyItem>())
            .Select((item, index) => item with
            {
                SourceElementId = DefaultText(item.SourceElementId, $"legacy-{index + 1}"),
                SourceName = DefaultText(item.SourceName, $"Element {index + 1}"),
                LegacyType = DefaultText(item.LegacyType, "Legacy"),
                BoundsAbsolute = NormalizeBounds(item.BoundsAbsolute, index),
                BoundsRelativeToPackage = NormalizeBounds(item.BoundsRelativeToPackage, index),
                Style = item.Style ?? ElementStudioStyleSnapshot.Default
            })
            .OrderBy(item => item.ZIndex)
            .ToArray();

        var hasValidBounds = package.Bounds is not null && package.Bounds.HasPositiveSize;
        return package with
        {
            PackageId = DefaultText(package.PackageId, Path.GetFileNameWithoutExtension(packagePath)),
            SourceProjectId = DefaultText(package.SourceProjectId, "Projet inconnu"),
            SourceSceneId = DefaultText(package.SourceSceneId, "Scene inconnue"),
            SourcePagePath = DefaultText(package.SourcePagePath, packagePath),
            TargetLibraryPath = string.IsNullOrWhiteSpace(package.TargetLibraryPath) ? null : package.TargetLibraryPath,
            Bounds = hasValidBounds ? package.Bounds! : CalculateBounds(items),
            Items = items
        };
    }

    private static ElementStudioImportPackage CreateEmptyPackage(string? packagePath = null)
    {
        return new ElementStudioImportPackage(
            Metadata: ElementStudioPackageMetadata.Current("V2.0.0.0000"),
            PackageId: string.IsNullOrWhiteSpace(packagePath)
                ? "studio-empty"
                : Path.GetFileNameWithoutExtension(packagePath),
            SourceProjectId: "Projet non charge",
            SourceSceneId: "Scene non chargee",
            SourcePagePath: packagePath ?? string.Empty,
            TargetLibraryPath: null,
            Bounds: new SceneBounds(0, 0, 960, 540),
            Items: Array.Empty<ElementStudioLegacyItem>());
    }

    private static SceneBounds NormalizeBounds(SceneBounds? bounds, int index)
    {
        if (bounds is not null && bounds.HasPositiveSize)
        {
            return bounds;
        }

        return new SceneBounds(40 + index * 24, 40 + index * 24, 160, 72);
    }

    private static SceneBounds CalculateBounds(IReadOnlyList<ElementStudioLegacyItem> items)
    {
        if (items.Count == 0)
        {
            return new SceneBounds(0, 0, 960, 540);
        }

        var left = items.Min(item => item.BoundsRelativeToPackage.X);
        var top = items.Min(item => item.BoundsRelativeToPackage.Y);
        var right = items.Max(item => item.BoundsRelativeToPackage.X + item.BoundsRelativeToPackage.Width);
        var bottom = items.Max(item => item.BoundsRelativeToPackage.Y + item.BoundsRelativeToPackage.Height);

        return new SceneBounds(left, top, Math.Max(320, right - left), Math.Max(240, bottom - top));
    }

    private static string DefaultText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
