using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Rendering;

public sealed partial class Ft100SceneExporter
{
    /// <summary>
    /// Directory name expected at the top level of FT100 project packages.
    /// </summary>
    public const string ProjectPackageDirectoryName = "scada-builder-v2-ft100-package";

    /// <summary>
    /// FT100 SCADA Builder V2 archive extension imported by TF100Web.
    /// </summary>
    public const string ProjectArchiveExtension = ".sb2";

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<Ft100SceneExportResult> ExportAsync(
        ScadaScene scene,
        string sourceHtmlPath,
        string exportDirectory,
        ScadaProject? project = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceHtmlPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportDirectory);

        if (!scene.IncludeInBuild)
        {
            throw new InvalidOperationException($"Scene '{scene.Id}' is not included in build.");
        }

        if (project is not null)
        {
            var errors = ScadaProjectBuildValidator.Validate(project, [scene])
                .Where(issue => issue.Severity == ScadaBuildValidationSeverity.Error)
                .ToArray();
            if (errors.Length > 0)
            {
                throw new InvalidOperationException(errors[0].Message);
            }
        }

        if (!File.Exists(sourceHtmlPath))
        {
            throw new FileNotFoundException("Source HTML not found.", sourceHtmlPath);
        }

        var sceneDirectory = Path.Combine(exportDirectory, scene.Id);
        var cssDirectory = Path.Combine(sceneDirectory, "css");
        var imagesDirectory = Path.Combine(sceneDirectory, "images");
        Directory.CreateDirectory(sceneDirectory);
        Directory.CreateDirectory(cssDirectory);
        Directory.CreateDirectory(imagesDirectory);

        var sourceHtml = await File.ReadAllTextAsync(sourceHtmlPath, cancellationToken);
        var sourceContent = RemoveSuppressedSourceElements(
            ExtractPageContent(sourceHtml),
            scene.GetSuppressedSourceElementIds());
        var copiedImages = CopyAndRewriteImageAssets(
            sourceContent,
            Path.GetDirectoryName(Path.GetFullPath(sourceHtmlPath))!,
            imagesDirectory,
            out var rewrittenSourceContent);
        var normalizedSourceContent = ApplyLegacyTextOverrides(
            RepairLegacyTextEncoding(rewrittenSourceContent),
            scene.TextOverrides);
        normalizedSourceContent = ApplyLegacySourceInlineBounds(normalizedSourceContent, scene);
        normalizedSourceContent = ScopeSourceDomIds(normalizedSourceContent, Ft100ExportScope.ForScene(scene));

        var cssFileName = $"{scene.Id}.css";
        var htmlFileName = $"{scene.Id}.html";
        var htmlPath = Path.Combine(sceneDirectory, htmlFileName);
        var cssPath = Path.Combine(cssDirectory, cssFileName);
        var manifestPath = Path.Combine(sceneDirectory, "manifest.json");

        await File.WriteAllTextAsync(
            cssPath,
            BuildCss(scene),
            Encoding.UTF8,
            cancellationToken);

        await File.WriteAllTextAsync(
            htmlPath,
            BuildHtml(scene, cssFileName, normalizedSourceContent),
            Encoding.UTF8,
            cancellationToken);

        await File.WriteAllTextAsync(
            manifestPath,
            BuildManifest(scene, project),
            Encoding.UTF8,
            cancellationToken);

        await File.WriteAllTextAsync(
            Path.Combine(sceneDirectory, "README.txt"),
            BuildReadme(scene),
            Encoding.UTF8,
            cancellationToken);

        return new Ft100SceneExportResult(sceneDirectory, htmlPath, cssPath, imagesDirectory, copiedImages);
    }

    public async Task<Ft100ProjectExportResult> ExportProjectAsync(
        ScadaProject project,
        IReadOnlyList<Ft100ProjectPageExportInput> pages,
        string exportDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportDirectory);

        var compiledPageIds = project.Scenes
            .Where(page => page.IncludeInBuild)
            .Select(page => page.Id)
            .ToHashSet(StringComparer.Ordinal);
        var pageInputsById = pages
            .Where(page => page.Scene.IncludeInBuild)
            .ToDictionary(page => page.Scene.Id, StringComparer.Ordinal);
        var missingPageIds = compiledPageIds
            .Where(id => !pageInputsById.ContainsKey(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (missingPageIds.Length > 0)
        {
            throw new InvalidOperationException($"Compiled page '{missingPageIds[0]}' has no export input.");
        }

        var errors = ScadaProjectBuildValidator.Validate(
                project,
                pageInputsById.Values.Select(input => input.Scene).ToArray())
            .Where(issue => issue.Severity == ScadaBuildValidationSeverity.Error)
            .ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(errors[0].Message);
        }

        var packageDirectory = ResolveProjectPackageDirectory(exportDirectory);
        RecreateProjectPackageDirectory(exportDirectory, packageDirectory);

        var pageResults = new List<Ft100SceneExportResult>();
        foreach (var pageId in compiledPageIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            var input = pageInputsById[pageId];
            pageResults.Add(await ExportAsync(input.Scene, input.SourceHtmlPath, packageDirectory, project, cancellationToken));
        }

        var manifestPath = Path.Combine(packageDirectory, "manifest.json");
        await File.WriteAllTextAsync(
            manifestPath,
            BuildProjectManifest(project, pageInputsById.Values.Select(input => input.Scene).ToArray()),
            Encoding.UTF8,
            cancellationToken);

        await File.WriteAllTextAsync(
            Path.Combine(packageDirectory, "README.txt"),
            BuildProjectReadme(project, pageResults.Count),
            Encoding.UTF8,
            cancellationToken);

        return new Ft100ProjectExportResult(
            packageDirectory,
            manifestPath,
            pageResults,
            pageResults.Sum(result => result.CopiedImageCount));
    }

    /// <summary>
    /// Exports the project as a FT100-compatible .sb2 archive.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0003, DEC-0007, DEC-0026, DEC-0027.
    /// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md.
    /// </remarks>
    public async Task<Ft100ProjectArchiveExportResult> ExportProjectArchiveAsync(
        ScadaProject project,
        IReadOnlyList<Ft100ProjectPageExportInput> pages,
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        var fullArchivePath = Path.GetFullPath(archivePath);
        if (!string.Equals(Path.GetExtension(fullArchivePath), ProjectArchiveExtension, StringComparison.OrdinalIgnoreCase))
        {
            fullArchivePath += ProjectArchiveExtension;
        }

        var archiveDirectory = Path.GetDirectoryName(fullArchivePath);
        if (string.IsNullOrWhiteSpace(archiveDirectory))
        {
            throw new InvalidOperationException("Invalid FT100 .sb2 archive destination.");
        }

        Directory.CreateDirectory(archiveDirectory);
        var stagingRoot = Path.Combine(Path.GetTempPath(), "scada-builder-v2-sb2", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(stagingRoot);
            var packageResult = await ExportProjectAsync(project, pages, stagingRoot, cancellationToken);
            var validation = Ft100PackageValidator.ValidatePackageDirectory(packageResult.ExportDirectory);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.Errors[0].Message);
            }

            if (File.Exists(fullArchivePath))
            {
                File.Delete(fullArchivePath);
            }

            ZipFile.CreateFromDirectory(
                stagingRoot,
                fullArchivePath,
                CompressionLevel.Optimal,
                includeBaseDirectory: false);

            return new Ft100ProjectArchiveExportResult(
                fullArchivePath,
                ProjectPackageDirectoryName,
                $"{ProjectPackageDirectoryName}/manifest.json",
                packageResult.PageResults.Count,
                packageResult.CopiedImageCount,
                validation);
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    private static string ResolveProjectPackageDirectory(string selectedDirectory)
    {
        var fullSelectedDirectory = Path.GetFullPath(selectedDirectory);
        return string.Equals(Path.GetFileName(fullSelectedDirectory), ProjectPackageDirectoryName, StringComparison.OrdinalIgnoreCase)
            ? fullSelectedDirectory
            : Path.Combine(fullSelectedDirectory, ProjectPackageDirectoryName);
    }

    private static void RecreateProjectPackageDirectory(string selectedDirectory, string packageDirectory)
    {
        var fullSelectedDirectory = Path.GetFullPath(selectedDirectory);
        var fullPackageDirectory = Path.GetFullPath(packageDirectory);
        var selectedIsPackage = string.Equals(fullSelectedDirectory, fullPackageDirectory, StringComparison.OrdinalIgnoreCase);
        var parentDirectory = selectedIsPackage
            ? Path.GetDirectoryName(fullPackageDirectory)
            : fullSelectedDirectory;

        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            throw new InvalidOperationException("Invalid FT100 package destination.");
        }

        var fullParentDirectory = Path.GetFullPath(parentDirectory);
        var expectedPackageDirectory = selectedIsPackage
            ? fullSelectedDirectory
            : Path.GetFullPath(Path.Combine(fullParentDirectory, ProjectPackageDirectoryName));
        if (!string.Equals(fullPackageDirectory, expectedPackageDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid FT100 package directory.");
        }

        Directory.CreateDirectory(fullParentDirectory);
        if (Directory.Exists(fullPackageDirectory))
        {
            Directory.Delete(fullPackageDirectory, recursive: true);
        }

        Directory.CreateDirectory(fullPackageDirectory);
    }

    private static string ExtractPageContent(string sourceHtml)
    {
        var match = PageDivRegex().Match(sourceHtml);
        if (!match.Success)
        {
            return sourceHtml;
        }

        var start = match.Index + match.Length;
        var end = FindMatchingDivEnd(sourceHtml, start);
        return end <= start
            ? sourceHtml[start..]
            : sourceHtml[start..end];
    }

    private static int FindMatchingDivEnd(string html, int contentStart)
    {
        var depth = 1;
        foreach (Match match in DivTokenRegex().Matches(html, contentStart))
        {
            if (match.Value.StartsWith("</", StringComparison.Ordinal))
            {
                depth--;
                if (depth == 0)
                {
                    return match.Index;
                }
            }
            else
            {
                depth++;
            }
        }

        return -1;
    }

    private static int CopyAndRewriteImageAssets(
        string html,
        string sourceDirectory,
        string imagesDirectory,
        out string rewrittenHtml)
    {
        var copied = 0;
        rewrittenHtml = SrcAttributeRegex().Replace(html, match =>
        {
            var quote = match.Groups["quote"].Value;
            var value = System.Net.WebUtility.HtmlDecode(match.Groups["value"].Value);
            if (string.IsNullOrWhiteSpace(value) ||
                value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return match.Value;
            }

            var normalized = value.Replace('/', Path.DirectorySeparatorChar);
            var sourcePath = Path.GetFullPath(Path.Combine(sourceDirectory, normalized));
            if (!File.Exists(sourcePath))
            {
                return match.Value;
            }

            var fileName = Path.GetFileName(sourcePath);
            var targetPath = Path.Combine(imagesDirectory, fileName);
            if (!File.Exists(targetPath))
            {
                File.Copy(sourcePath, targetPath);
                copied++;
            }

            return $"src={quote}images/{HtmlEncoder.Default.Encode(fileName)}{quote}";
        });

        return copied;
    }

    private static string RemoveSuppressedSourceElements(string html, IReadOnlySet<string> sourceElementIds)
    {
        var updated = html;
        foreach (var sourceElementId in sourceElementIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            var escapedDataId = Regex.Escape(sourceElementId);
            updated = LegacySelfClosingElementWithDataIdRegex(escapedDataId).Replace(updated, "");
            updated = LegacyElementWithDataIdRegex(escapedDataId).Replace(updated, "");
        }

        return updated;
    }

    private static string RepairLegacyTextEncoding(string html)
    {
        var current = html;
        for (var attempt = 0; attempt < 3 && ContainsLegacyMojibake(current); attempt++)
        {
            var repaired = Encoding.UTF8.GetString(GetWindows1252Bytes(current));
            if (string.Equals(repaired, current, StringComparison.Ordinal))
            {
                break;
            }

            current = repaired;
        }

        return current;
    }

    private static byte[] GetWindows1252Bytes(string value)
    {
        var bytes = new byte[value.Length];
        for (var index = 0; index < value.Length; index++)
        {
            bytes[index] = value[index] switch
            {
                <= '\u00FF' => (byte)value[index],
                '\u20AC' => 0x80,
                '\u201A' => 0x82,
                '\u0192' => 0x83,
                '\u201E' => 0x84,
                '\u2026' => 0x85,
                '\u2020' => 0x86,
                '\u2021' => 0x87,
                '\u02C6' => 0x88,
                '\u2030' => 0x89,
                '\u0160' => 0x8A,
                '\u2039' => 0x8B,
                '\u0152' => 0x8C,
                '\u017D' => 0x8E,
                '\u2018' => 0x91,
                '\u2019' => 0x92,
                '\u201C' => 0x93,
                '\u201D' => 0x94,
                '\u2022' => 0x95,
                '\u2013' => 0x96,
                '\u2014' => 0x97,
                '\u02DC' => 0x98,
                '\u2122' => 0x99,
                '\u0161' => 0x9A,
                '\u203A' => 0x9B,
                '\u0153' => 0x9C,
                '\u017E' => 0x9E,
                '\u0178' => 0x9F,
                _ => (byte)'?'
            };
        }

        return bytes;
    }

    private static bool ContainsLegacyMojibake(string value)
    {
        return value.Contains('Ã', StringComparison.Ordinal) ||
            value.Contains('Â', StringComparison.Ordinal) ||
            value.Contains('�', StringComparison.Ordinal);
    }

    private static string ApplyLegacyTextOverrides(string html, IReadOnlyList<LegacyTextOverride> overrides)
    {
        if (overrides.Count == 0)
        {
            return html;
        }

        var updated = html;
        foreach (var overrideItem in overrides)
        {
            if (string.IsNullOrWhiteSpace(overrideItem.SourceElementId))
            {
                continue;
            }

            var encodedText = HtmlEncoder.Default.Encode(overrideItem.Text);
            updated = LegacyElementWithDataIdRegex(Regex.Escape(overrideItem.SourceElementId)).Replace(
                updated,
                match => $"{match.Groups["open"].Value}{encodedText}{match.Groups["close"].Value}");
        }

        return updated;
    }

    private static string ApplyLegacySourceInlineBounds(string html, ScadaScene scene)
    {
        var sourceBounds = FlattenElementsWithAbsoluteBounds(scene.Elements, 0, 0)
            .Where(item => item.Element.IsLegacyStatic && !string.IsNullOrWhiteSpace(item.Element.LegacySource?.SourceElementId))
            .Select(item => new
            {
                SourceElementId = item.Element.LegacySource!.SourceElementId,
                Style = BuildSourceProjectionInlineStyle(item.Element, item.X, item.Y)
            })
            .GroupBy(item => item.SourceElementId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        if (sourceBounds.Length == 0)
        {
            return html;
        }

        var updated = html;
        foreach (var item in sourceBounds)
        {
            var escapedDataId = Regex.Escape(item.SourceElementId!);
            updated = LegacyElementWithDataIdRegex(escapedDataId).Replace(
                updated,
                match => ShouldApplyLegacyInlineBounds(match.Groups["open"].Value)
                    ? $"{ApplyInlineStyleToTag(match.Groups["open"].Value, item.Style)}{match.Groups["content"].Value}{match.Groups["close"].Value}"
                    : match.Value);
            updated = LegacySelfClosingElementWithDataIdRegex(escapedDataId).Replace(
                updated,
                match => ShouldApplyLegacyInlineBounds(match.Value)
                    ? ApplyInlineStyleToTag(match.Value, item.Style)
                    : match.Value);
        }

        return updated;
    }

    private static string ScopeSourceDomIds(string html, Ft100ExportScope scope)
    {
        var occurrenceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var referenceRewrites = new Dictionary<string, string>(StringComparer.Ordinal);
        var updated = SourceIdAttributeRegex().Replace(html, match =>
        {
            var id = match.Groups["value"].Value.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                return match.Value;
            }

            var occurrence = occurrenceCounts.TryGetValue(id, out var currentCount)
                ? currentCount + 1
                : 1;
            occurrenceCounts[id] = occurrence;

            var scopedId = CreateScopedSourceDomId(scope, id, occurrence);
            referenceRewrites.TryAdd(id, scopedId);
            return $"{match.Groups["prefix"].Value}{match.Groups["quote"].Value}{HtmlEncoder.Default.Encode(scopedId)}{match.Groups["quote"].Value}";
        });

        foreach (var rewrite in referenceRewrites)
        {
            updated = RewriteSourceDomIdReference(updated, rewrite.Key, rewrite.Value);
        }

        return updated;
    }

    private static string CreateScopedSourceDomId(Ft100ExportScope scope, string sourceId, int occurrence)
    {
        var baseId = CssIdentifier(sourceId);
        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = "source";
        }

        var suffix = occurrence <= 1
            ? ""
            : $"-{occurrence.ToString(CultureInfo.InvariantCulture)}";
        return $"{scope.RootDomId}__legacy-{baseId}{suffix}";
    }

    private static string RewriteSourceDomIdReference(string html, string sourceId, string scopedId)
    {
        return html
            .Replace($"url(#{sourceId})", $"url(#{scopedId})", StringComparison.Ordinal)
            .Replace($"url('#{sourceId}')", $"url('#{scopedId}')", StringComparison.Ordinal)
            .Replace($"url(\"#{sourceId}\")", $"url(\"#{scopedId}\")", StringComparison.Ordinal)
            .Replace($"href=\"#{sourceId}\"", $"href=\"#{scopedId}\"", StringComparison.Ordinal)
            .Replace($"href='#{sourceId}'", $"href='#{scopedId}'", StringComparison.Ordinal)
            .Replace($"xlink:href=\"#{sourceId}\"", $"xlink:href=\"#{scopedId}\"", StringComparison.Ordinal)
            .Replace($"xlink:href='#{sourceId}'", $"xlink:href='#{scopedId}'", StringComparison.Ordinal);
    }

    private static bool ShouldApplyLegacyInlineBounds(string tag)
    {
        var tagMatch = TagNameRegex().Match(tag);
        if (!tagMatch.Success)
        {
            return false;
        }

        var tagName = tagMatch.Groups["tag"].Value;
        if (IsSvgShapeTag(tagName))
        {
            return false;
        }

        var classMatch = ClassAttributeRegex().Match(tag);
        if (!classMatch.Success)
        {
            return false;
        }

        var classes = classMatch.Groups["value"].Value
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return classes.Contains("layer", StringComparer.Ordinal);
    }

    private static bool IsSvgShapeTag(string tagName)
    {
        return tagName.Equals("rect", StringComparison.OrdinalIgnoreCase)
            || tagName.Equals("path", StringComparison.OrdinalIgnoreCase)
            || tagName.Equals("circle", StringComparison.OrdinalIgnoreCase)
            || tagName.Equals("ellipse", StringComparison.OrdinalIgnoreCase)
            || tagName.Equals("line", StringComparison.OrdinalIgnoreCase)
            || tagName.Equals("polyline", StringComparison.OrdinalIgnoreCase)
            || tagName.Equals("polygon", StringComparison.OrdinalIgnoreCase)
            || tagName.Equals("text", StringComparison.OrdinalIgnoreCase)
            || tagName.Equals("use", StringComparison.OrdinalIgnoreCase)
            || tagName.Equals("image", StringComparison.OrdinalIgnoreCase);
    }

    private static string ApplyInlineStyleToTag(string tag, string geometryStyle)
    {
        var styleMatch = StyleAttributeRegex().Match(tag);
        if (styleMatch.Success)
        {
            var existingStyle = styleMatch.Groups["value"].Value.Trim();
            var combinedStyle = string.IsNullOrWhiteSpace(existingStyle)
                ? geometryStyle
                : $"{existingStyle.TrimEnd(';')};{geometryStyle}";
            return StyleAttributeRegex().Replace(
                tag,
                $" style=\"{EncodeLegacyStyleAttribute(combinedStyle)}\"",
                count: 1);
        }

        var insertAt = tag.EndsWith("/>", StringComparison.Ordinal)
            ? tag.Length - 2
            : tag.Length - 1;
        return tag.Insert(insertAt, $" style=\"{EncodeLegacyStyleAttribute(geometryStyle)}\"");
    }

    private static string EncodeLegacyStyleAttribute(string style)
    {
        return style.Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    private static string BuildHtml(ScadaScene scene, string cssFileName, string sourceContent)
    {
        var scope = Ft100ExportScope.ForScene(scene);
        var title = HtmlEncoder.Default.Encode(scene.Title);
        var pageId = HtmlEncoder.Default.Encode(scene.Id);
        var rootDomId = HtmlEncoder.Default.Encode(scope.RootDomId);
        var pageClass = HtmlEncoder.Default.Encode(CssIdentifier(scene.Id));
        var sceneType = HtmlEncoder.Default.Encode(ToManifestPageType(scene.PageType));
        var rootStyle = HtmlEncoder.Default.Encode(BuildSceneRootInlineStyle(scene));
        var modernElements = string.Concat(scene.Elements.Select(element => BuildElementHtml(element, 0, 0, scope)));
        var runtimeScript = BuildRuntimeScript(scene, scope);

        return $$"""
<!doctype html>
<html lang="fr">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{title}}</title>
  <link rel="stylesheet" href="css/{{HtmlEncoder.Default.Encode(cssFileName)}}">
</head>
<body style="margin:0;padding:0;">
  <div id="{{rootDomId}}" class="ft100-scada-scene ft100-scada-scene--{{pageClass}}" data-scada-page-id="{{pageId}}" data-scada-page-type="{{sceneType}}" data-scada-width="{{Format(scene.CanvasSize.Width)}}" data-scada-height="{{Format(scene.CanvasSize.Height)}}" style="{{rootStyle}}">
    <div class="ft100-source-layer" style="position:absolute;inset:0;">
{{Indent(sourceContent, 6)}}
    </div>
    <div class="ft100-elementplus-layer" style="position:absolute;inset:0;pointer-events:none;">
{{Indent(modernElements, 6)}}
    </div>
  </div>
  <script>
{{Indent(runtimeScript, 4)}}
  </script>
</body>
</html>
""";
    }

    private static string BuildElementHtml(ScadaElement element, double parentX, double parentY, Ft100ExportScope scope)
    {
        if (element.IsLegacyStatic)
        {
            return "";
        }

        var absoluteX = parentX + element.Bounds.X;
        var absoluteY = parentY + element.Bounds.Y;
        if (element.Kind == ScadaElementKind.Group)
        {
            if (element.EventBindings.Count > 0)
            {
                var groupId = HtmlEncoder.Default.Encode(scope.ElementDomId(element.Id));
                var groupSceneElementId = HtmlEncoder.Default.Encode(element.Id);
                var groupName = HtmlEncoder.Default.Encode(element.DisplayName);
                var groupKind = HtmlEncoder.Default.Encode(element.Kind.ToString());
                var groupInlineStyle = HtmlEncoder.Default.Encode(BuildRuntimeGroupInlineStyle(element, absoluteX, absoluteY));
                var groupEventAttribute = BuildEventAttribute(element);
                var groupValueBindingAttributes = BuildValueBindingAttributes(element);
                var children = string.Concat(element.ChildElements.Select(child => BuildElementHtml(child, 0, 0, scope)));

                return $$"""
<div id="{{groupId}}" class="ft100-element ft100-element--{{groupKind}}" data-scada-element-id="{{groupSceneElementId}}" data-name="{{groupName}}" style="{{groupInlineStyle}}"{{groupEventAttribute}}{{groupValueBindingAttributes}}>
{{Indent(children, 2)}}
</div>
""";
            }

            return string.Concat(element.ChildElements.Select(child => BuildElementHtml(child, absoluteX, absoluteY, scope)));
        }

        var id = HtmlEncoder.Default.Encode(scope.ElementDomId(element.Id));
        var sceneElementId = HtmlEncoder.Default.Encode(element.Id);
        var name = HtmlEncoder.Default.Encode(element.DisplayName);
        var kind = HtmlEncoder.Default.Encode(element.Kind.ToString());
        var inlineStyle = HtmlEncoder.Default.Encode(BuildElementInlineStyle(element, absoluteX, absoluteY));
        var content = BuildElementContent(element);
        var eventAttribute = BuildEventAttribute(element);
        var valueBindingAttributes = BuildValueBindingAttributes(element);

        return $$"""
<div id="{{id}}" class="ft100-element ft100-element--{{kind}}" data-scada-element-id="{{sceneElementId}}" data-name="{{name}}" style="{{inlineStyle}}"{{eventAttribute}}{{valueBindingAttributes}}>
  {{content}}
</div>
""";
    }

    private static string BuildEventAttribute(ScadaElement element)
    {
        return element.EventBindings.Count == 0
            ? ""
            : $" data-scada-events=\"{HtmlEncoder.Default.Encode(JsonSerializer.Serialize(element.EventBindings, ManifestJsonOptions))}\"";
    }

    private static string BuildValueBindingAttributes(ScadaElement element)
    {
        var attributes = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(element.Data?.ReadTagId))
        {
            attributes.Append(" data-scada-read-tag=\"");
            attributes.Append(HtmlEncoder.Default.Encode(element.Data.ReadTagId));
            attributes.Append('"');
        }

        if (!string.IsNullOrWhiteSpace(element.Data?.WriteTagId))
        {
            attributes.Append(" data-scada-write-tag=\"");
            attributes.Append(HtmlEncoder.Default.Encode(element.Data.WriteTagId));
            attributes.Append('"');
        }

        return attributes.ToString();
    }

    private static string BuildElementContent(ScadaElement element)
    {
        var data = element.Data ?? new ScadaElementData(null, null, null, null, null, null, null, null, null, false);
        return element.Kind switch
        {
            ScadaElementKind.Custom => data.Text ?? "",
            ScadaElementKind.Text => HtmlEncoder.Default.Encode(data.Text ?? element.DisplayName),
            ScadaElementKind.InputNumeric when data.IsReadOnly => HtmlEncoder.Default.Encode(
                data.Value?.ToString(CultureInfo.InvariantCulture) ?? data.DisplayFormat ?? data.Placeholder ?? ""),
            ScadaElementKind.InputNumeric => BuildInput(element, "number"),
            ScadaElementKind.InputText => BuildInput(element, "text"),
            ScadaElementKind.Shape => BuildShape(element),
            ScadaElementKind.Button => BuildButton(element),
            _ => ""
        };
    }

    private static string BuildShape(ScadaElement element)
    {
        var style = element.Style ?? ScadaElementStyle.DefaultText;
        var data = element.Data ?? new ScadaElementData(null, null, null, null, null, null, null, null, null, false);
        var width = Math.Max(1, element.Bounds.Width);
        var height = Math.Max(1, element.Bounds.Height);
        var strokeWidth = Math.Max(0, style.BorderWidth);
        var halfStroke = Math.Max(1, strokeWidth / 2);
        var stroke = HtmlEncoder.Default.Encode(style.BorderColor);
        var fill = element.EffectiveShapeKind is ScadaShapeKind.Line or ScadaShapeKind.Arrow
            ? "transparent"
            : HtmlEncoder.Default.Encode(style.Background);
        var dashArray = ShapeDashArray(style.BorderStyle);
        var dashAttribute = string.IsNullOrWhiteSpace(dashArray)
            ? ""
            : $" stroke-dasharray=\"{dashArray}\"";
        var svgId = HtmlEncoder.Default.Encode($"shape-{CssIdentifier(element.Id)}");
        var markerId = HtmlEncoder.Default.Encode($"arrow-{CssIdentifier(element.Id)}");
        var gradientId = HtmlEncoder.Default.Encode($"lamp-gradient-{CssIdentifier(element.Id)}");
        var common = $"stroke=\"{stroke}\" stroke-width=\"{Format(strokeWidth)}\"{dashAttribute} vector-effect=\"non-scaling-stroke\"";
        var body = element.EffectiveShapeKind switch
        {
            ScadaShapeKind.IndicatorLamp =>
                $"""<defs><radialGradient id="{gradientId}" cx="35%" cy="28%" r="70%"><stop offset="0%" stop-color="#ffffff" stop-opacity="0.85"/><stop offset="42%" stop-color="{fill}"/><stop offset="100%" stop-color="{stroke}"/></radialGradient></defs><circle cx="{Format(width / 2)}" cy="{Format(height / 2)}" r="{Format(Math.Max(0, Math.Min(width, height) / 2 - halfStroke))}" fill="url(#{gradientId})" {common}/>""",
            ScadaShapeKind.HorizontalBar =>
                BuildBarShape(width, height, strokeWidth, halfStroke, stroke, fill, data.Value, vertical: false, common),
            ScadaShapeKind.VerticalBar =>
                BuildBarShape(width, height, strokeWidth, halfStroke, stroke, fill, data.Value, vertical: true, common),
            ScadaShapeKind.Ellipse =>
                $"""<ellipse cx="{Format(width / 2)}" cy="{Format(height / 2)}" rx="{Format(Math.Max(0, (width / 2) - halfStroke))}" ry="{Format(Math.Max(0, (height / 2) - halfStroke))}" fill="{fill}" {common}/>""",
            ScadaShapeKind.Line =>
                $"""<line x1="{Format(halfStroke)}" y1="{Format(height / 2)}" x2="{Format(Math.Max(halfStroke, width - halfStroke))}" y2="{Format(height / 2)}" {common}/>""",
            ScadaShapeKind.Arrow =>
                $"""<defs><marker id="{markerId}" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M 0 0 L 10 5 L 0 10 z" fill="{stroke}"/></marker></defs><line x1="{Format(halfStroke)}" y1="{Format(height / 2)}" x2="{Format(Math.Max(halfStroke, width - halfStroke - 7))}" y2="{Format(height / 2)}" marker-end="url(#{markerId})" {common}/>""",
            ScadaShapeKind.RoundedRectangle =>
                $"""<rect x="{Format(halfStroke)}" y="{Format(halfStroke)}" width="{Format(Math.Max(0, width - strokeWidth))}" height="{Format(Math.Max(0, height - strokeWidth))}" rx="{Format(Math.Min(width, height) * 0.12)}" ry="{Format(Math.Min(width, height) * 0.12)}" fill="{fill}" {common}/>""",
            _ =>
                $"""<rect x="{Format(halfStroke)}" y="{Format(halfStroke)}" width="{Format(Math.Max(0, width - strokeWidth))}" height="{Format(Math.Max(0, height - strokeWidth))}" fill="{fill}" {common}/>"""
        };

        return $"""<svg id="{svgId}" viewBox="0 0 {Format(width)} {Format(height)}" width="100%" height="100%" preserveAspectRatio="none" style="display:block;pointer-events:none;">{body}</svg>""";
    }

    private static string BuildBarShape(
        double width,
        double height,
        double strokeWidth,
        double halfStroke,
        string stroke,
        string fill,
        double? value,
        bool vertical,
        string common)
    {
        var percent = ClampPercent(value);
        var innerX = halfStroke + 3;
        var innerY = halfStroke + 3;
        var innerWidth = Math.Max(0, width - strokeWidth - 6);
        var innerHeight = Math.Max(0, height - strokeWidth - 6);
        var cornerRadius = Format(Math.Min(8, Math.Min(width, height) * 0.2));
        var fillCornerRadius = Format(Math.Min(5, Math.Min(innerWidth, innerHeight) * 0.18));
        var track = $"""<rect x="{Format(halfStroke)}" y="{Format(halfStroke)}" width="{Format(Math.Max(0, width - strokeWidth))}" height="{Format(Math.Max(0, height - strokeWidth))}" rx="{cornerRadius}" fill="#f7fbf5" {common}/>""";
        var fillRect = vertical
            ? $"""<rect x="{Format(innerX)}" y="{Format(innerY + innerHeight - (innerHeight * percent / 100))}" width="{Format(innerWidth)}" height="{Format(innerHeight * percent / 100)}" rx="{fillCornerRadius}" fill="{fill}"/>"""
            : $"""<rect x="{Format(innerX)}" y="{Format(innerY)}" width="{Format(innerWidth * percent / 100)}" height="{Format(innerHeight)}" rx="{fillCornerRadius}" fill="{fill}"/>""";
        return track + fillRect;
    }

    private static string BuildButton(ScadaElement element)
    {
        var data = element.Data ?? new ScadaElementData(null, null, null, null, null, null, null, null, null, false);
        var label = HtmlEncoder.Default.Encode(data.Text ?? data.Placeholder ?? element.DisplayName);
        return $"""<button type="button" style="width:100%;height:100%;box-sizing:border-box;font:inherit;color:inherit;background:transparent;border:0;">{label}</button>""";
    }

    private static string BuildInput(ScadaElement element, string type)
    {
        var data = element.Data ?? new ScadaElementData(null, null, null, null, null, null, null, null, null, false);
        var value = type == "number"
            ? data.Value?.ToString(CultureInfo.InvariantCulture) ?? ""
            : data.Text ?? "";
        var placeholder = HtmlEncoder.Default.Encode(data.Placeholder ?? "");
        var encodedValue = HtmlEncoder.Default.Encode(value);
        var readOnly = data.IsReadOnly ? " readonly" : "";
        return $"""<input type="{type}" value="{encodedValue}" placeholder="{placeholder}" style="width:100%;height:100%;box-sizing:border-box;"{readOnly}>""";
    }

    private static string BuildSceneRootInlineStyle(ScadaScene scene)
    {
        var style = new StringBuilder();
        style.Append("position:relative;");
        style.Append($"width:{Format(scene.CanvasSize.Width)}px;");
        style.Append($"height:{Format(scene.CanvasSize.Height)}px;");
        style.Append("overflow:hidden;");
        style.Append($"background-color:{scene.EffectiveBackground.Color};");
        return style.ToString();
    }

    private static string BuildRuntimeGroupInlineStyle(ScadaElement element, double absoluteX, double absoluteY)
    {
        var style = new StringBuilder();
        style.Append("position:absolute;");
        style.Append("box-sizing:border-box;");
        style.Append("overflow:visible;");
        style.Append("pointer-events:auto;");
        style.Append($"left:{Format(absoluteX)}px;");
        style.Append($"top:{Format(absoluteY)}px;");
        style.Append($"width:{Format(element.Bounds.Width)}px;");
        style.Append($"height:{Format(element.Bounds.Height)}px;");
        style.Append("background:transparent;");
        style.Append("border:0;");
        return style.ToString();
    }

    private static string BuildElementInlineStyle(ScadaElement element, double absoluteX, double absoluteY)
    {
        var style = element.Style ?? ScadaElementStyle.DefaultText;
        var css = new StringBuilder();
        css.Append("position:absolute;");
        css.Append("box-sizing:border-box;");
        css.Append("overflow:visible;");
        css.Append("pointer-events:auto;");
        css.Append($"left:{Format(absoluteX)}px;");
        css.Append($"top:{Format(absoluteY)}px;");
        css.Append($"width:{Format(element.Bounds.Width)}px;");
        css.Append($"height:{Format(element.Bounds.Height)}px;");
        css.Append($"color:{style.Foreground};");
        css.Append($"background:{(element.Kind == ScadaElementKind.Shape ? "transparent" : style.Background)};");
        css.Append($"font-family:{style.FontFamily};");
        css.Append($"font-size:{Format(style.FontSize)}px;");
        css.Append(element.Kind == ScadaElementKind.Shape
            ? "border:0 none transparent;"
            : $"border:{Format(style.BorderWidth)}px {NormalizeBorderStyle(style.BorderStyle)} {style.BorderColor};");
        css.Append($"box-shadow:{ShadowCss(style.ShadowPreset)};");
        if (!string.IsNullOrWhiteSpace(style.AdvancedCss))
        {
            css.Append(style.AdvancedCss);
        }

        return css.ToString();
    }

    private static string BuildSourceProjectionInlineStyle(ScadaElement element, double absoluteX, double absoluteY)
    {
        return string.Concat(
            "position:absolute !important;",
            $"left:{Format(absoluteX)}px !important;",
            $"top:{Format(absoluteY)}px !important;",
            $"width:{Format(element.Bounds.Width)}px !important;",
            $"height:{Format(element.Bounds.Height)}px !important;",
            "box-sizing:border-box;");
    }

    private static string BuildCss(ScadaScene scene)
    {
        var css = new StringBuilder();
        var scope = Ft100ExportScope.ForScene(scene);
        css.AppendLine($"{scope.RootSelector}.ft100-scada-scene {{");
        css.AppendLine("  position: relative;");
        css.AppendLine($"  width: {Format(scene.CanvasSize.Width)}px;");
        css.AppendLine($"  height: {Format(scene.CanvasSize.Height)}px;");
        AppendBackgroundCss(css, scene.EffectiveBackground);
        css.AppendLine("  overflow: hidden;");
        css.AppendLine("  isolation: isolate;");
        css.AppendLine("}");
        css.AppendLine($"{scope.Descendant(".ft100-source-layer")}, {scope.Descendant(".ft100-legacy-layer")}, {scope.Descendant(".ft100-elementplus-layer")} {{ position: absolute; inset: 0; }}");
        css.AppendLine($"{scope.Descendant(".ft100-elementplus-layer")} {{ pointer-events: none; }}");
        css.AppendLine($"{scope.Descendant(".ft100-source-layer .shape-layer")}, {scope.Descendant(".ft100-legacy-layer .shape-layer")} {{ position: absolute; left: 0; top: 0; pointer-events: none; }}");
        css.AppendLine($"{scope.Descendant(".ft100-source-layer .layer")}, {scope.Descendant(".ft100-legacy-layer .layer")} {{ position: absolute; margin: 0; }}");
        css.AppendLine($"{scope.Descendant(".ft100-element")} {{ position: absolute; box-sizing: border-box; overflow: visible; pointer-events: auto; }}");
        css.AppendLine($"{scope.Descendant(".ft100-element--Button")}, {scope.Descendant("[data-scada-events]")} {{ cursor: pointer; }}");
        css.AppendLine($"{scope.Descendant(".ft100-element--Button *")}, {scope.Descendant("[data-scada-events] *")} {{ cursor: pointer; }}");
        css.AppendLine($"{scope.Descendant(".ft100-element--Button:active")}, {scope.Descendant("[data-scada-events]:active")} {{ cursor: pointer; }}");
        css.AppendLine($"{scope.Descendant($".{ScadaEventRegistry.RuntimeBorderHighlightClass}")} {{ outline: 2px solid #00a3ff; outline-offset: 2px; box-shadow: 0 0 0 1px rgba(255, 255, 255, 0.85), 0 0 8px rgba(0, 163, 255, 0.65); }}");
        css.AppendLine($"@keyframes {scope.AnimationName("scada-blink")} {{ 0%, 100% {{ opacity: 1; }} 50% {{ opacity: 0.35; }} }}");
        css.AppendLine($"@keyframes {scope.AnimationName("scada-pulse")} {{ 0%, 100% {{ transform: scale(1); }} 50% {{ transform: scale(1.035); }} }}");
        css.AppendLine($"{scope.Descendant($".{ScadaEventRegistry.RuntimeBlinkEffectClass}")} {{ animation: {scope.AnimationName("scada-blink")} 1s steps(2, start) infinite; }}");
        css.AppendLine($"{scope.Descendant($".{ScadaEventRegistry.RuntimeGlowEffectClass}")} {{ box-shadow: 0 0 0 2px rgba(0, 163, 255, 0.55), 0 0 18px rgba(0, 163, 255, 0.85) !important; }}");
        css.AppendLine($"{scope.Descendant($".{ScadaEventRegistry.RuntimePulseEffectClass}")} {{ animation: {scope.AnimationName("scada-pulse")} 1.25s ease-in-out infinite; transform-origin: center; }}");
        css.AppendLine($"{scope.Descendant($".{ScadaEventRegistry.RuntimeAlarmEffectClass}")} {{ outline: 3px solid #f43f3f; outline-offset: 2px; box-shadow: 0 0 0 1px rgba(255, 255, 255, 0.85), 0 0 14px rgba(244, 63, 63, 0.8) !important; }}");
        css.AppendLine($"{scope.Descendant($".{ScadaEventRegistry.RuntimeDegradedEffectClass}")} {{ filter: grayscale(0.75) contrast(0.9); opacity: 0.72; }}");
        css.AppendLine($"{scope.Descendant(".ft100-element svg")} {{ display: block; width: 100%; height: 100%; overflow: visible; }}");
        css.AppendLine($"{scope.Descendant(".ft100-element input")} {{ width: 100%; height: 100%; box-sizing: border-box; }}");
        css.AppendLine($"{scope.Descendant(".ft100-element button")} {{ width: 100%; height: 100%; box-sizing: border-box; font: inherit; color: inherit; }}");

        foreach (var legacyId in scene.GetSuppressedSourceElementIds().OrderBy(id => id, StringComparer.Ordinal))
        {
            css.AppendLine($"{scope.SourceDataIdSelector(legacyId)} {{ display: none !important; }}");
        }

        foreach (var element in scene.Elements)
        {
            AppendElementCss(css, element, 0, 0, scope);
        }

        return css.ToString();
    }

    private static void AppendBackgroundCss(StringBuilder css, SceneBackgroundStyle background)
    {
        css.AppendLine($"  background-color: {background.Color};");
        if (!string.IsNullOrWhiteSpace(background.Image))
        {
            css.AppendLine($"  background-image: url(\"{CssEscape(background.Image)}\");");
            css.AppendLine($"  background-size: {background.Size};");
            css.AppendLine($"  background-repeat: {background.Repeat};");
            css.AppendLine($"  background-position: {background.Position};");
            css.AppendLine($"  background-attachment: {background.Attachment};");
            css.AppendLine($"  background-origin: {background.Origin};");
            css.AppendLine($"  background-clip: {background.Clip};");
            css.AppendLine($"  background-blend-mode: {background.BlendMode};");
        }
    }

    private static void AppendElementCss(StringBuilder css, ScadaElement element, double parentX, double parentY, Ft100ExportScope scope)
    {
        var absoluteX = parentX + element.Bounds.X;
        var absoluteY = parentY + element.Bounds.Y;
        if (element.Kind == ScadaElementKind.Group)
        {
            if (element.EventBindings.Count > 0)
            {
                css.AppendLine();
                css.AppendLine($"{scope.ElementSelector(element.Id)} {{");
                css.AppendLine($"  left: {Format(absoluteX)}px;");
                css.AppendLine($"  top: {Format(absoluteY)}px;");
                css.AppendLine($"  width: {Format(element.Bounds.Width)}px;");
                css.AppendLine($"  height: {Format(element.Bounds.Height)}px;");
                css.AppendLine("  background: transparent;");
                css.AppendLine("  border: 0;");
                css.AppendLine("  box-shadow: none;");
                css.AppendLine("}");

                foreach (var child in element.ChildElements)
                {
                    AppendElementCss(css, child, 0, 0, scope);
                }

                return;
            }

            foreach (var child in element.ChildElements)
            {
                AppendElementCss(css, child, absoluteX, absoluteY, scope);
            }

            return;
        }

        if (element.IsLegacyStatic && !string.IsNullOrWhiteSpace(element.LegacySource?.SourceElementId))
        {
            css.AppendLine();
            css.AppendLine($"{scope.SourceDataIdSelector(element.LegacySource.SourceElementId)} {{");
            css.AppendLine("  position: absolute !important;");
            css.AppendLine($"  left: {Format(absoluteX)}px !important;");
            css.AppendLine($"  top: {Format(absoluteY)}px !important;");
            css.AppendLine($"  width: {Format(element.Bounds.Width)}px;");
            css.AppendLine($"  height: {Format(element.Bounds.Height)}px;");
            css.AppendLine("}");
            return;
        }

        var style = element.Style ?? ScadaElementStyle.DefaultText;
        css.AppendLine();
        css.AppendLine($"{scope.ElementSelector(element.Id)} {{");
        css.AppendLine($"  left: {Format(absoluteX)}px;");
        css.AppendLine($"  top: {Format(absoluteY)}px;");
        css.AppendLine($"  width: {Format(element.Bounds.Width)}px;");
        css.AppendLine($"  height: {Format(element.Bounds.Height)}px;");
        css.AppendLine($"  color: {style.Foreground};");
        css.AppendLine($"  background: {(element.Kind == ScadaElementKind.Shape ? "transparent" : style.Background)};");
        css.AppendLine($"  font-family: {style.FontFamily};");
        css.AppendLine($"  font-size: {Format(style.FontSize)}px;");
        css.AppendLine(element.Kind == ScadaElementKind.Shape
            ? "  border: 0 none transparent;"
            : $"  border: {Format(style.BorderWidth)}px {NormalizeBorderStyle(style.BorderStyle)} {style.BorderColor};");
        css.AppendLine($"  box-shadow: {ShadowCss(style.ShadowPreset)};");
        if (!string.IsNullOrWhiteSpace(style.AdvancedCss))
        {
            css.AppendLine($"  {style.AdvancedCss}");
        }
        css.AppendLine("}");

        if (element.Kind == ScadaElementKind.Button)
        {
            AppendButtonHoverCss(css, element, scope);
        }
    }

    private static void AppendButtonHoverCss(StringBuilder css, ScadaElement element, Ft100ExportScope scope)
    {
        var behavior = element.EffectiveButtonBehavior;
        var hover = behavior.EffectiveHover;
        if (behavior.IsDisabled || !hover.Enabled)
        {
            return;
        }

        css.AppendLine();
        css.AppendLine($"{scope.ElementSelector(element.Id)}:hover {{");
        css.AppendLine($"  background: {hover.Background};");
        css.AppendLine($"  color: {hover.Foreground};");
        css.AppendLine($"  border-color: {hover.BorderColor};");
        css.AppendLine("}");
    }

    private static string BuildReadme(ScadaScene scene)
    {
        return $"""
FT100 export - {scene.Id}

Files:
- {scene.Id}.html: complete static HTML wrapper for validation and Django integration.
- css/{scene.Id}.css: scene and Element+ CSS.
- images/: copied image assets referenced by the source scene.
- manifest.json: Django-readable page, object event, and action manifest.

Django integration:
Load this page through the package manifest when possible.
For fragment composition, inject the complete div with id ft100-{scene.Id} into the target slot,
include css/{scene.Id}.css, and size the slot from manifest Width/Height or the div data-scada-width/data-scada-height attributes.
The div and Element+ objects carry critical inline geometry as a deployment guardrail, but css/{scene.Id}.css remains the complete runtime stylesheet.
Serve images/ next to that CSS/HTML path or preserve the relative paths.
""";
    }

    private static string BuildManifest(ScadaScene scene, ScadaProject? project)
    {
        var homePageId = project?.EffectiveHomePageId;
        var manifest = new
        {
            Name = scene.Title,
            ManifestVersion = "2.1",
            HomePageId = homePageId,
            Pages = new[] { BuildManifestPage(scene, homePageId, projectRelativePath: false) },
            Actions = scene.ActionDefinitions,
            Tags = project?.TagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>()
        };

        return JsonSerializer.Serialize(manifest, ManifestJsonOptions);
    }

    private static string BuildProjectManifest(ScadaProject project, IReadOnlyList<ScadaScene> scenes)
    {
        var homePageId = project.EffectiveHomePageId;
        var scenesById = scenes.ToDictionary(scene => scene.Id, StringComparer.Ordinal);
        var exportedScenes = project.Scenes
            .Where(reference => reference.IncludeInBuild)
            .Select(reference => scenesById[reference.Id])
            .ToArray();
        var actions = exportedScenes
            .SelectMany(scene => scene.ActionDefinitions)
            .GroupBy(action => action.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var manifest = new
        {
            Name = project.Name,
            ManifestVersion = "2.1",
            HomePageId = homePageId,
            Pages = exportedScenes
                .Select(scene => BuildManifestPage(scene, homePageId, projectRelativePath: true))
                .ToArray(),
            Actions = actions,
            Tags = project.TagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>()
        };

        return JsonSerializer.Serialize(manifest, ManifestJsonOptions);
    }

    private static object BuildManifestPage(ScadaScene scene, string? homePageId, bool projectRelativePath)
    {
        var requiredDisplaySize = CalculateRequiredDisplaySize(scene);
        return new
        {
            Id = scene.Id,
            Name = scene.Title,
            Type = ToManifestPageType(scene.PageType),
            IncludeInBuild = scene.IncludeInBuild,
            IsHome = string.Equals(homePageId, scene.Id, StringComparison.Ordinal),
            HeaderPageId = scene.HeaderPageId,
            FooterPageId = scene.FooterPageId,
            RelativePath = projectRelativePath ? $"{scene.Id}/{scene.Id}.html" : $"{scene.Id}.html",
            Width = scene.CanvasSize.Width,
            Height = scene.CanvasSize.Height,
            RequiredDisplayWidth = requiredDisplaySize.Width,
            RequiredDisplayHeight = requiredDisplaySize.Height,
            Background = scene.EffectiveBackground,
            Objects = FlattenElements(scene.Elements)
                .Where(ShouldExportManifestObject)
                .Select(element => new
                {
                    element.Id,
                    element.DisplayName,
                    Kind = element.Kind.ToString(),
                    ShapeKind = element.Kind == ScadaElementKind.Shape ? element.EffectiveShapeKind.ToString() : null,
                    ButtonBehavior = element.Kind == ScadaElementKind.Button ? element.EffectiveButtonBehavior : null,
                    Data = BuildManifestElementData(element),
                    Events = element.EventBindings,
                    ValueBindings = new
                    {
                        ReadTagId = element.Data?.ReadTagId,
                        WriteTagId = element.Data?.WriteTagId
                    }
                })
                .ToArray()
        };
    }

    private static object? BuildManifestElementData(ScadaElement element)
    {
        if (element.Kind != ScadaElementKind.InputNumeric)
        {
            return null;
        }

        return new
        {
            DisplayFormat = element.Data?.DisplayFormat,
            IsReadOnly = element.Data?.IsReadOnly ?? false,
            Min = element.Data?.Minimum,
            Max = element.Data?.Maximum
        };
    }

    private static bool ShouldExportManifestObject(ScadaElement element)
    {
        return element.Kind != ScadaElementKind.Group || element.EventBindings.Count > 0;
    }

    private static CanvasSize CalculateRequiredDisplaySize(ScadaScene scene)
    {
        var width = (double)scene.CanvasSize.Width;
        var height = (double)scene.CanvasSize.Height;

        foreach (var bounds in FlattenExportedElementBounds(scene.Elements, 0, 0))
        {
            width = Math.Max(width, bounds.X + Math.Max(0, bounds.Width));
            height = Math.Max(height, bounds.Y + Math.Max(0, bounds.Height));
        }

        return new CanvasSize(
            Math.Max(1, (int)Math.Ceiling(width)),
            Math.Max(1, (int)Math.Ceiling(height)));
    }

    private static IEnumerable<SceneBounds> FlattenExportedElementBounds(
        IEnumerable<ScadaElement> elements,
        double parentX,
        double parentY)
    {
        foreach (var element in elements)
        {
            var absoluteX = parentX + element.Bounds.X;
            var absoluteY = parentY + element.Bounds.Y;
            if (element.Kind == ScadaElementKind.Group)
            {
                if (element.EventBindings.Count > 0)
                {
                    yield return new SceneBounds(absoluteX, absoluteY, element.Bounds.Width, element.Bounds.Height);
                }

                foreach (var childBounds in FlattenExportedElementBounds(element.ChildElements, absoluteX, absoluteY))
                {
                    yield return childBounds;
                }
            }
            else
            {
                yield return new SceneBounds(absoluteX, absoluteY, element.Bounds.Width, element.Bounds.Height);
            }
        }
    }

    private static string BuildProjectReadme(ScadaProject project, int pageCount)
    {
        return $"""
FT100 project export - {project.Name}

Files:
- manifest.json: project-level Django-readable manifest for compiled pages.
- <page-id>/<page-id>.html: browser-openable page wrapper for each compiled page.
- <page-id>/css/<page-id>.css: page CSS.
- <page-id>/images/: copied image assets referenced by that page.

Compiled pages: {pageCount}
Home page: {project.EffectiveHomePageId ?? "(fallback unavailable)"}

Composition:
Use the root manifest as the page inventory. Render default pages by composing the referenced header page, body page, and footer page as separate slots.
Each slot must preserve the complete exported page root and load that page's CSS from the same package version.
Apply any viewport scale to the composed page container, not independently to header, body, and footer slots.
""";
    }

    private static string BuildRuntimeScript(ScadaScene scene, Ft100ExportScope scope)
    {
        var actionsJson = JsonSerializer.Serialize(
            scene.ActionDefinitions.ToDictionary(action => action.Id, StringComparer.Ordinal),
            ManifestJsonOptions);
        var rootIdJson = JsonSerializer.Serialize(scope.RootDomId);
        return $$"""
(function () {
  const root = document.getElementById({{rootIdJson}});
  if (!root) {
    return;
  }

  const actions = {{actionsJson}};

  function dispatchRuntimeEvent(name, detail) {
    window.dispatchEvent(new CustomEvent(name, {
      detail: Object.assign({
        pageId: root.getAttribute('data-scada-page-id'),
        rootId: root.id
      }, detail || {})
    }));
  }

  function reportRuntimeError(error, context) {
    dispatchRuntimeEvent('scada-builder-runtime-error', {
      message: error && error.message ? error.message : String(error),
      context: context || null
    });
  }

  window.scadaBuilderRuntime = Object.assign(window.scadaBuilderRuntime || {}, {
    pageId: root.getAttribute('data-scada-page-id'),
    rootId: root.id,
    actions: actions,
    dispatch: dispatchRuntimeEvent
  });

  function navigate(targetPageId) {
    if (!targetPageId) {
      return;
    }

    window.location.href = '../' + encodeURIComponent(targetPageId) + '/' + encodeURIComponent(targetPageId) + '.html';
  }

  function normalizePopupOptions(options) {
    return Object.assign({
      Position: 'center',
      SizePreset: 'large',
      AllowMultiple: false,
      ResetOnOpen: true,
      HostRegionId: null
    }, options || {});
  }

  function getPopupBaseId(targetPageId) {
    return root.id + '__popup_' + sanitizeElementId(targetPageId);
  }

  function getPopupOverlays(targetPageId) {
    return Array.from(root.querySelectorAll('[data-scada-popup-page-id="' + cssEscape(targetPageId) + '"]'));
  }

  function closePopupLocal(targetPageId) {
    const existing = getPopupOverlays(targetPageId).pop();
    if (!existing) {
      return false;
    }

    existing.remove();
    window.dispatchEvent(new CustomEvent('scada-builder-popup-closed', {
      detail: { pageId: targetPageId }
    }));
    return true;
  }

  function postPopupRequestToParent(action, targetPageId) {
    if (window.parent && window.parent !== window) {
      window.parent.postMessage({
        source: 'scada-builder-v2',
        action: action,
        pageId: targetPageId
      }, '*');
      return true;
    }

    return false;
  }

  function getPopupHost(options) {
    if (String(options.Position || '').toLowerCase() !== 'hostregion' || !options.HostRegionId) {
      return root;
    }

    const host = getPageElement(options.HostRegionId) || root;
    if (host !== root && window.getComputedStyle(host).position === 'static') {
      host.style.position = 'relative';
    }

    return host;
  }

  function applyPopupSize(panel, options) {
    const size = String(options.SizePreset || 'large').toLowerCase();
    if (size === 'small') {
      panel.style.width = '360px';
      panel.style.height = '240px';
      panel.style.maxWidth = '60%';
      panel.style.maxHeight = '60%';
    } else if (size === 'medium') {
      panel.style.width = '560px';
      panel.style.height = '420px';
      panel.style.maxWidth = '75%';
      panel.style.maxHeight = '75%';
    } else if (size === 'fullscreen') {
      panel.style.width = '100%';
      panel.style.height = '100%';
      panel.style.maxWidth = '100%';
      panel.style.maxHeight = '100%';
    } else {
      panel.style.width = '80%';
      panel.style.height = '80%';
      panel.style.maxWidth = '960px';
      panel.style.maxHeight = '720px';
    }
  }

  function applyPopupPlacement(overlay, panel, options) {
    const position = String(options.Position || 'center').toLowerCase();
    overlay.style.display = 'flex';
    overlay.style.alignItems = 'center';
    overlay.style.justifyContent = 'center';
    if (position === 'topleft') {
      overlay.style.alignItems = 'flex-start';
      overlay.style.justifyContent = 'flex-start';
    } else if (position === 'topright') {
      overlay.style.alignItems = 'flex-start';
      overlay.style.justifyContent = 'flex-end';
    } else if (position === 'bottomleft') {
      overlay.style.alignItems = 'flex-end';
      overlay.style.justifyContent = 'flex-start';
    } else if (position === 'bottomright') {
      overlay.style.alignItems = 'flex-end';
      overlay.style.justifyContent = 'flex-end';
    } else if (position === 'dockleft') {
      overlay.style.alignItems = 'stretch';
      overlay.style.justifyContent = 'flex-start';
      panel.style.height = '100%';
      panel.style.maxHeight = '100%';
    } else if (position === 'dockright') {
      overlay.style.alignItems = 'stretch';
      overlay.style.justifyContent = 'flex-end';
      panel.style.height = '100%';
      panel.style.maxHeight = '100%';
    } else if (position === 'docktop') {
      overlay.style.alignItems = 'flex-start';
      overlay.style.justifyContent = 'stretch';
      panel.style.width = '100%';
      panel.style.maxWidth = '100%';
    } else if (position === 'dockbottom') {
      overlay.style.alignItems = 'flex-end';
      overlay.style.justifyContent = 'stretch';
      panel.style.width = '100%';
      panel.style.maxWidth = '100%';
    } else if (position === 'hostregion') {
      overlay.style.position = 'absolute';
      overlay.style.inset = '0';
      panel.style.width = '100%';
      panel.style.height = '100%';
      panel.style.maxWidth = '100%';
      panel.style.maxHeight = '100%';
    }
  }

  function openPopup(targetPageId, popupOptions) {
    if (!targetPageId) {
      return;
    }

    const options = normalizePopupOptions(popupOptions);
    if (!options.AllowMultiple) {
      while (closePopupLocal(targetPageId)) {
      }
    }

    const host = getPopupHost(options);
    const overlay = document.createElement('div');
    overlay.id = options.AllowMultiple ? getPopupBaseId(targetPageId) + '_' + Date.now().toString(36) : getPopupBaseId(targetPageId);
    overlay.setAttribute('data-scada-popup-page-id', targetPageId);
    overlay.setAttribute('data-scada-popup-position', options.Position || 'Center');
    overlay.setAttribute('data-scada-popup-size', options.SizePreset || 'Large');
    overlay.style.position = 'absolute';
    overlay.style.inset = '0';
    overlay.style.zIndex = '10000';
    overlay.style.background = 'rgba(0, 0, 0, 0.28)';
    overlay.style.pointerEvents = 'auto';

    const panel = document.createElement('div');
    panel.style.position = 'relative';
    panel.style.background = '#fff';
    panel.style.border = '1px solid rgba(15, 42, 48, 0.24)';
    panel.style.boxShadow = '0 16px 42px rgba(15, 42, 48, 0.28)';
    applyPopupSize(panel, options);
    applyPopupPlacement(overlay, panel, options);

    const close = document.createElement('button');
    close.type = 'button';
    close.textContent = 'Fermer';
    close.style.position = 'absolute';
    close.style.top = '8px';
    close.style.right = '8px';
    close.style.zIndex = '1';
    close.addEventListener('click', function () {
      closePopupLocal(targetPageId);
    });

    const iframe = document.createElement('iframe');
    iframe.title = targetPageId;
    const iframeUrl = '../' + encodeURIComponent(targetPageId) + '/' + encodeURIComponent(targetPageId) + '.html';
    iframe.src = options.ResetOnOpen === false ? iframeUrl : iframeUrl + '?scadaPopupInstance=' + encodeURIComponent(overlay.id);
    iframe.style.width = '100%';
    iframe.style.height = '100%';
    iframe.style.border = '0';

    panel.appendChild(close);
    panel.appendChild(iframe);
    overlay.appendChild(panel);
    overlay.addEventListener('click', function (event) {
      if (event.target === overlay) {
        close.click();
      }
    });
    host.appendChild(overlay);
    window.dispatchEvent(new CustomEvent('scada-builder-popup-opened', {
      detail: { pageId: targetPageId, elementId: root.getAttribute('data-scada-page-id'), options: options }
    }));
  }

  function closePopup(targetPageId) {
    if (!targetPageId) {
      return;
    }

    if (!closePopupLocal(targetPageId)) {
      postPopupRequestToParent('closePopup', targetPageId);
    }
  }

  function togglePopup(targetPageId, popupOptions) {
    if (!targetPageId) {
      return;
    }

    if (closePopupLocal(targetPageId)) {
      return;
    }

    if (!postPopupRequestToParent('togglePopup', targetPageId)) {
      openPopup(targetPageId, popupOptions);
    }
  }

  window.addEventListener('message', function (event) {
    const detail = event.data || {};
    if (detail.source !== 'scada-builder-v2' || !detail.pageId) {
      return;
    }

    if (detail.action === 'closePopup') {
      closePopupLocal(detail.pageId);
    } else if (detail.action === 'togglePopup') {
      if (!closePopupLocal(detail.pageId)) {
            openPopup(detail.pageId, null);
          }
        }
      });

  function sanitizeElementId(value) {
    return String(value || '').replace(/[^a-zA-Z0-9_-]/g, '_');
  }

  function cssEscape(value) {
    return String(value || '').replace(/\\/g, '\\\\').replace(/"/g, '\\"');
  }

  function getPageElement(elementId) {
    if (!elementId) {
      return null;
    }

    const target = document.getElementById(root.id + '__' + sanitizeElementId(elementId));
    return target && root.contains(target) ? target : null;
  }

  function readValueFromElement(element) {
    const input = element.matches('input, textarea, select') ? element : element.querySelector('input, textarea, select');
    if (input) {
      return input.value;
    }

    return element.textContent || '';
  }

  function writeValueToElement(element, value) {
    const input = element.matches('input, textarea, select') ? element : element.querySelector('input, textarea, select');
    const normalized = value === undefined || value === null ? '' : String(value);
    if (input) {
      input.value = normalized;
      input.dispatchEvent(new Event('input', { bubbles: true }));
      return;
    }

    const button = element.querySelector('button');
    if (button) {
      button.textContent = normalized;
      return;
    }

    element.textContent = normalized;
  }

  window.scadaBuilderTagValues = window.scadaBuilderTagValues || {};
  const readBindingsByTag = {};

  function registerReadBinding(element) {
    const tagId = element.getAttribute('data-scada-read-tag');
    if (!tagId) {
      return;
    }

    if (!readBindingsByTag[tagId]) {
      readBindingsByTag[tagId] = [];
    }

    readBindingsByTag[tagId].push(element);
  }

  function applyTagValue(tagId, value, meta) {
    if (!tagId) {
      return;
    }

    window.scadaBuilderTagValues[tagId] = value;
    (readBindingsByTag[tagId] || []).forEach(function (element) {
      writeValueToElement(element, value);
      window.dispatchEvent(new CustomEvent('scada-builder-tag-value-applied', {
        detail: {
          tagId: tagId,
          value: value,
          elementId: element.getAttribute('data-scada-element-id'),
          meta: meta || null
        }
      }));
    });
  }

  window.scadaBuilderSetTagValue = function (tagId, value, meta) {
    applyTagValue(tagId, value, meta);
  };

  root.querySelectorAll('[data-scada-read-tag]').forEach(function (element) {
    registerReadBinding(element);
    const tagId = element.getAttribute('data-scada-read-tag');
    window.dispatchEvent(new CustomEvent('scada-builder-read-tag-request', {
      detail: { tagId: tagId, elementId: element.getAttribute('data-scada-element-id') }
    }));
  });

  window.addEventListener('scada-builder-tag-value', function (event) {
    const detail = event.detail || {};
    applyTagValue(detail.tagId, detail.value, detail);
  });

  root.querySelectorAll('[data-scada-write-tag]').forEach(function (element) {
    const target = element.matches('input, textarea, select') ? element : element.querySelector('input, textarea, select');
    const eventTarget = target || element;
    eventTarget.addEventListener('change', function () {
      const payload = {
        tagId: element.getAttribute('data-scada-write-tag'),
        value: readValueFromElement(element),
        elementId: element.getAttribute('data-scada-element-id')
      };
      if (window.tf100webScadaBuilder && typeof window.tf100webScadaBuilder.writeTag === 'function') {
        window.tf100webScadaBuilder.writeTag(payload.tagId, payload.value, payload);
      }
      window.dispatchEvent(new CustomEvent('scada-builder-write-value', { detail: payload }));
    });
  });

  function getRuntimeTagValue(tagId) {
    if (!tagId) {
      return undefined;
    }

    if (window.tf100webScadaBuilder && typeof window.tf100webScadaBuilder.getTagValue === 'function') {
      return window.tf100webScadaBuilder.getTagValue(tagId);
    }

    if (window.scadaBuilderTagValues && Object.prototype.hasOwnProperty.call(window.scadaBuilderTagValues, tagId)) {
      return window.scadaBuilderTagValues[tagId];
    }

    return undefined;
  }

  function parseBoolean(value) {
    if (typeof value === 'boolean') {
      return value;
    }

    const normalized = String(value).trim().toLowerCase();
    if (normalized === 'true' || normalized === '1' || normalized === 'on') {
      return true;
    }

    if (normalized === 'false' || normalized === '0' || normalized === 'off') {
      return false;
    }

    return undefined;
  }

  function evaluateCondition(condition) {
    return evaluateConditionResult(condition) === true;
  }

  function evaluateConditionResult(condition) {
    if (!condition || !condition.TagId || !condition.Operator) {
      return true;
    }

    const actual = getRuntimeTagValue(condition.TagId);
    if (actual === undefined || actual === null) {
      return null;
    }

    const operator = String(condition.Operator).toLowerCase();
    if (operator === 'true' || operator === 'false') {
      const booleanValue = parseBoolean(actual);
      if (booleanValue === undefined) {
        return false;
      }

      return operator === 'true' ? booleanValue : !booleanValue;
    }

    const expected = condition.CompareValue;
    const actualNumber = Number(actual);
    const expectedNumber = Number(expected);
    const useNumeric = Number.isFinite(actualNumber) && Number.isFinite(expectedNumber);
    const left = useNumeric ? actualNumber : String(actual);
    const right = useNumeric ? expectedNumber : String(expected);

    if (operator === 'equals') {
      return left === right;
    }
    if (operator === 'notequals') {
      return left !== right;
    }
    if (!useNumeric) {
      return false;
    }
    if (operator === 'greaterthan') {
      return left > right;
    }
    if (operator === 'greaterthanorequal') {
      return left >= right;
    }
    if (operator === 'lessthan') {
      return left < right;
    }
    if (operator === 'lessthanorequal') {
      return left <= right;
    }

    return false;
  }

  function evaluateConditionGroup(group) {
    if (!group || !Array.isArray(group.Conditions) || group.Conditions.length === 0) {
      return true;
    }

    const missingPolicy = String(group.MissingTagPolicy || 'blockAction').toLowerCase();
    const results = group.Conditions.map(function (condition) {
      return evaluateConditionResult(condition);
    });
    if (results.some(function (result) { return result === null; })) {
      return missingPolicy === 'allowaction';
    }

    const mode = String(group.Mode || 'all').toLowerCase();
    if (mode === 'any') {
      return results.some(function (result) { return result === true; });
    }

    return results.every(function (result) { return result === true; });
  }

  function evaluateActionConditions(action) {
    return evaluateCondition(action.Condition) && evaluateConditionGroup(action.ConditionGroup);
  }

  function applyAction(action) {
    if (!action || !action.Kind) {
      return false;
    }

    if (!evaluateActionConditions(action)) {
      return false;
    }

    const kind = String(action.Kind).toLowerCase();
    if (kind === 'navigate') {
      navigate(action.TargetPageId);
      return true;
    }

    if (kind === 'mountfragment') {
      openPopup(action.TargetPageId, action.PopupOptions);
      return true;
    }

    if (kind === 'closepopup') {
      closePopup(action.TargetPageId);
      return true;
    }

    if (kind === 'togglepopup') {
      togglePopup(action.TargetPageId, action.PopupOptions);
      return true;
    }

    if (kind === 'writetag') {
      const payload = { tagId: action.TagId, value: action.Value };
      if (window.tf100webScadaBuilder && typeof window.tf100webScadaBuilder.writeTag === 'function') {
        window.tf100webScadaBuilder.writeTag(payload.tagId, payload.value, payload);
      }
      window.dispatchEvent(new CustomEvent('scada-builder-write-tag', { detail: payload }));
      return true;
    }

    const target = getPageElement(action.TargetElementId);
    if (!target) {
      return false;
    }

    if (kind === 'show') {
      target.hidden = false;
    } else if (kind === 'hide') {
      target.hidden = true;
    } else if (kind === 'togglevisibility') {
      target.hidden = !target.hidden;
    } else if (kind === 'setclass' && action.ClassName) {
      target.classList.add(action.ClassName);
    } else if (kind === 'removeclass' && action.ClassName) {
      target.classList.remove(action.ClassName);
    } else if (kind === 'toggleclass' && action.ClassName) {
      target.classList.toggle(action.ClassName);
    } else {
      return false;
    }

    return true;
  }

  root.querySelectorAll('[data-scada-events]').forEach(function (element) {
    let bindings = [];
    try {
      bindings = JSON.parse(element.getAttribute('data-scada-events') || '[]');
    } catch {
      bindings = [];
    }

    bindings.forEach(function (binding) {
      if (!binding || !binding.Trigger || !binding.ActionId) {
        return;
      }

      element.addEventListener(binding.Trigger, function (event) {
        if (binding.PreventDefault) {
          event.preventDefault();
        }
        if (binding.StopPropagation) {
          event.stopPropagation();
        }
        try {
          if (applyAction(actions[binding.ActionId])) {
            dispatchRuntimeEvent('scada-builder-action-executed', {
              actionId: binding.ActionId,
              trigger: binding.Trigger,
              elementId: element.getAttribute('data-scada-element-id')
            });
          }
        } catch (error) {
          reportRuntimeError(error, {
            actionId: binding.ActionId,
            trigger: binding.Trigger,
            elementId: element.getAttribute('data-scada-element-id')
          });
        }
      });
    });
  });
  dispatchRuntimeEvent('scada-builder-page-ready', {
    actionCount: Object.keys(actions).length
  });
})();
""";
    }

    private static IEnumerable<ScadaElement> FlattenElements(IEnumerable<ScadaElement> elements)
    {
        foreach (var element in elements)
        {
            yield return element;
            foreach (var child in FlattenElements(element.ChildElements))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<(ScadaElement Element, double X, double Y)> FlattenElementsWithAbsoluteBounds(
        IEnumerable<ScadaElement> elements,
        double parentX,
        double parentY)
    {
        foreach (var element in elements)
        {
            var absoluteX = parentX + element.Bounds.X;
            var absoluteY = parentY + element.Bounds.Y;
            yield return (element, absoluteX, absoluteY);
            foreach (var child in FlattenElementsWithAbsoluteBounds(element.ChildElements, absoluteX, absoluteY))
            {
                yield return child;
            }
        }
    }

    private static string ToManifestPageType(ScadaPageType pageType)
    {
        return pageType switch
        {
            ScadaPageType.Fragment => "fragment",
            ScadaPageType.Header => "header",
            ScadaPageType.Footer => "footer",
            _ => "default"
        };
    }

    private static string Indent(string value, int spaces)
    {
        var prefix = new string(' ', spaces);
        return string.Join(Environment.NewLine, value.ReplaceLineEndings("\n").Split('\n').Select(line => prefix + line));
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string NormalizeBorderStyle(string value)
    {
        return string.Equals(value, "None", StringComparison.OrdinalIgnoreCase)
            ? "none"
            : value.ToLowerInvariant();
    }

    private static string ShapeDashArray(string value)
    {
        return value switch
        {
            "Dashed" => "8 5",
            "Dotted" => "2 4",
            _ => ""
        };
    }

    private static double ClampPercent(double? value)
    {
        return Math.Max(0, Math.Min(100, value ?? 65));
    }

    private static string ShadowCss(string preset)
    {
        return preset switch
        {
            "Soft" => "0 8px 18px rgba(15,42,48,.16)",
            "Raised" => "0 12px 26px rgba(15,42,48,.24)",
            "Inset" => "inset 0 2px 8px rgba(15,42,48,.22)",
            _ => "none"
        };
    }

    private sealed class Ft100ExportScope
    {
        private Ft100ExportScope(string sceneId)
        {
            RootDomId = $"ft100-{CssIdentifier(sceneId)}";
            RootSelector = $"#{RootDomId}";
        }

        public string RootDomId { get; }

        public string RootSelector { get; }

        public static Ft100ExportScope ForScene(ScadaScene scene)
        {
            return new Ft100ExportScope(scene.Id);
        }

        public string Descendant(string selector)
        {
            return $"{RootSelector} {selector}";
        }

        public string SourceDataIdSelector(string sourceElementId)
        {
            return $"{RootSelector} [data-id=\"{CssEscape(sourceElementId)}\"]";
        }

        public string ElementDomId(string elementId)
        {
            return $"{RootDomId}__{CssIdentifier(elementId)}";
        }

        public string ElementSelector(string elementId)
        {
            return $"{RootSelector} #{ElementDomId(elementId)}";
        }

        public string AnimationName(string name)
        {
            return $"{RootDomId}-{CssIdentifier(name)}";
        }
    }

    private static string CssIdentifier(string value)
    {
        return string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));
    }

    private static string CssEscape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    [GeneratedRegex("""<div\b[^>]*class=["'][^"']*\bpage\b[^"']*["'][^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex PageDivRegex();

    [GeneratedRegex("""</?div\b[^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex DivTokenRegex();

    [GeneratedRegex("""src=(?<quote>["'])(?<value>[^"']+)\k<quote>""", RegexOptions.IgnoreCase)]
    private static partial Regex SrcAttributeRegex();

    [GeneratedRegex("""(?<prefix>(?<![\w:-])id\s*=\s*)(?<quote>["'])(?<value>[^"']+)\k<quote>""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourceIdAttributeRegex();

    [GeneratedRegex("""\sstyle\s*=\s*(?<quote>["'])(?<value>.*?)\k<quote>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleAttributeRegex();

    [GeneratedRegex("""^<\s*(?<tag>[a-zA-Z][\w:-]*)\b""", RegexOptions.IgnoreCase)]
    private static partial Regex TagNameRegex();

    [GeneratedRegex("""\sclass\s*=\s*(?<quote>["'])(?<value>.*?)\k<quote>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ClassAttributeRegex();

    private static Regex LegacyElementWithDataIdRegex(string escapedDataId)
    {
        return new Regex(
            $"""(?<open><(?<tag>[a-zA-Z][\w:-]*)\b(?=[^>]*\bdata-id\s*=\s*["']{escapedDataId}["'])[^>]*>)(?<content>.*?)(?<close></\k<tag>>)""",
            RegexOptions.IgnoreCase | RegexOptions.Singleline,
            TimeSpan.FromSeconds(1));
    }

    private static Regex LegacySelfClosingElementWithDataIdRegex(string escapedDataId)
    {
        return new Regex(
            $"""<(?<tag>[a-zA-Z][\w:-]*)\b(?=[^>]*\bdata-id\s*=\s*["']{escapedDataId}["'])[^>]*/>""",
            RegexOptions.IgnoreCase | RegexOptions.Singleline,
            TimeSpan.FromSeconds(1));
    }
}
