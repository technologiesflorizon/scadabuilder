using System.Text.Json;
using ScadaBuilderV2.Application.ElementStudio;

namespace ScadaBuilderV2.Infrastructure.ElementStudio;

public sealed class ElementPlusLibraryReader
{
    private readonly IElementStudioComponentPackageReader packageReader;

    public ElementPlusLibraryReader()
        : this(new ElementStudioComponentPackageStore())
    {
    }

    public ElementPlusLibraryReader(IElementStudioComponentPackageReader packageReader)
    {
        this.packageReader = packageReader;
    }

    public async Task<ElementPlusLibrarySnapshot> ReadAsync(
        string libraryRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);

        var diagnostics = new List<string>();
        if (!Directory.Exists(libraryRoot))
        {
            return new ElementPlusLibrarySnapshot(Array.Empty<ElementPlusLibraryItem>(), diagnostics);
        }

        var items = new List<ElementPlusLibraryItem>();
        foreach (var path in Directory.EnumerateFiles(libraryRoot, "*.sep", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var package = await ReadPackageWithRetryAsync(path, cancellationToken);
                var component = package.Component;
                var fileInfo = new FileInfo(path);
                items.Add(new ElementPlusLibraryItem(
                    component.ComponentId,
                    component.Name,
                    component.Category,
                    component.Visual.Kind,
                    component.Bounds,
                    CountParts(component.Parts ?? Array.Empty<ElementStudioComponentPart>()),
                    fileInfo.FullName,
                    GetPreviewMarkup(component),
                    fileInfo.LastWriteTimeUtc,
                    component.TagList,
                    component.Provenance));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or ArgumentException)
            {
                diagnostics.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        return new ElementPlusLibrarySnapshot(items, diagnostics);
    }

    private static string? GetPreviewMarkup(ElementStudioComponent component)
    {
        var markup = component.Visual.Kind switch
        {
            ElementStudioComponentVisualKind.Svg => ElementStudioSvgMarkupNormalizer.NormalizeSvgMarkup(component.Visual.SvgMarkup),
            ElementStudioComponentVisualKind.Html => component.Visual.HtmlMarkup,
            _ => null
        };

        return string.IsNullOrWhiteSpace(markup) ? null : markup;
    }

    private async Task<ElementStudioComponentPackage> ReadPackageWithRetryAsync(
        string path,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await packageReader.ReadFromPathAsync(path, cancellationToken);
            }
            catch (Exception ex) when (attempt < maxAttempts && ex is IOException or JsonException)
            {
                await Task.Delay(120 * attempt, cancellationToken);
            }
        }

        return await packageReader.ReadFromPathAsync(path, cancellationToken);
    }

    private static int CountParts(IReadOnlyList<ElementStudioComponentPart> parts)
    {
        var count = 0;
        foreach (var part in parts)
        {
            count++;
            count += CountParts(part.ChildParts);
        }

        return count;
    }
}
