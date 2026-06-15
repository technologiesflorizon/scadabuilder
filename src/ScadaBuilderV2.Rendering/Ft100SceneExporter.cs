using System.Globalization;
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
    public const string ProjectPackageDirectoryName = "scada-builder-v2-ft100-package";

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
            var errors = ScadaProjectBuildValidator.Validate(project)
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

        var errors = ScadaProjectBuildValidator.Validate(project)
            .Where(issue => issue.Severity == ScadaBuildValidationSeverity.Error)
            .ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(errors[0].Message);
        }

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

    private static string BuildHtml(ScadaScene scene, string cssFileName, string sourceContent)
    {
        var title = HtmlEncoder.Default.Encode(scene.Title);
        var sceneId = HtmlEncoder.Default.Encode(scene.Id);
        var modernElements = string.Concat(scene.Elements.Select(element => BuildElementHtml(element, 0, 0)));
        var runtimeScript = BuildRuntimeScript(scene);

        return $$"""
<!doctype html>
<html lang="fr">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{title}}</title>
  <link rel="stylesheet" href="css/{{HtmlEncoder.Default.Encode(cssFileName)}}">
</head>
<body>
  <div id="ft100-{{sceneId}}" class="ft100-scada-scene ft100-scada-scene--{{sceneId}}">
    <div class="ft100-source-layer">
{{Indent(sourceContent, 6)}}
    </div>
    <div class="ft100-elementplus-layer">
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

    private static string BuildElementHtml(ScadaElement element, double parentX, double parentY)
    {
        if (element.IsLegacyStatic)
        {
            return "";
        }

        var absoluteX = parentX + element.Bounds.X;
        var absoluteY = parentY + element.Bounds.Y;
        if (element.Kind == ScadaElementKind.Group)
        {
            return string.Concat(element.ChildElements.Select(child => BuildElementHtml(child, absoluteX, absoluteY)));
        }

        var id = HtmlEncoder.Default.Encode(CssIdentifier(element.Id));
        var name = HtmlEncoder.Default.Encode(element.DisplayName);
        var kind = HtmlEncoder.Default.Encode(element.Kind.ToString());
        var content = BuildElementContent(element);
        var eventAttribute = element.EventBindings.Count == 0
            ? ""
            : $" data-scada-events=\"{HtmlEncoder.Default.Encode(JsonSerializer.Serialize(element.EventBindings, ManifestJsonOptions))}\"";

        return $$"""
<div id="{{id}}" class="ft100-element ft100-element--{{kind}}" data-name="{{name}}"{{eventAttribute}}>
  {{content}}
</div>
""";
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
            _ => ""
        };
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
        return $"""<input type="{type}" value="{encodedValue}" placeholder="{placeholder}"{readOnly}>""";
    }

    private static string BuildCss(ScadaScene scene)
    {
        var css = new StringBuilder();
        css.AppendLine(":root {");
        css.AppendLine($"  --ft100-scada-width: {Format(scene.CanvasSize.Width)}px;");
        css.AppendLine($"  --ft100-scada-height: {Format(scene.CanvasSize.Height)}px;");
        css.AppendLine("}");
        css.AppendLine();
        css.AppendLine("html, body { margin: 0; padding: 0; }");
        css.AppendLine(".ft100-scada-scene {");
        css.AppendLine("  position: relative;");
        css.AppendLine("  width: var(--ft100-scada-width);");
        css.AppendLine("  height: var(--ft100-scada-height);");
        AppendBackgroundCss(css, scene.EffectiveBackground);
        css.AppendLine("  overflow: hidden;");
        css.AppendLine("}");
        css.AppendLine(".ft100-source-layer, .ft100-legacy-layer, .ft100-elementplus-layer { position: absolute; inset: 0; }");
        css.AppendLine(".ft100-elementplus-layer { pointer-events: none; }");
        css.AppendLine(".ft100-source-layer .shape-layer, .ft100-legacy-layer .shape-layer { position: absolute; left: 0; top: 0; pointer-events: none; }");
        css.AppendLine(".ft100-source-layer .layer, .ft100-legacy-layer .layer { position: absolute; margin: 0; }");
        css.AppendLine(".ft100-element { position: absolute; box-sizing: border-box; overflow: visible; pointer-events: auto; }");
        css.AppendLine(".ft100-element svg { display: block; width: 100%; height: 100%; overflow: visible; }");
        css.AppendLine(".ft100-element input { width: 100%; height: 100%; box-sizing: border-box; }");

        foreach (var legacyId in scene.GetSuppressedSourceElementIds().OrderBy(id => id, StringComparer.Ordinal))
        {
            css.AppendLine($"[data-id=\"{CssEscape(legacyId)}\"] {{ display: none !important; }}");
        }

        foreach (var element in scene.Elements)
        {
            AppendElementCss(css, element, 0, 0);
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

    private static void AppendElementCss(StringBuilder css, ScadaElement element, double parentX, double parentY)
    {
        var absoluteX = parentX + element.Bounds.X;
        var absoluteY = parentY + element.Bounds.Y;
        if (element.Kind == ScadaElementKind.Group)
        {
            foreach (var child in element.ChildElements)
            {
                AppendElementCss(css, child, absoluteX, absoluteY);
            }

            return;
        }

        if (element.IsLegacyStatic && !string.IsNullOrWhiteSpace(element.LegacySource?.SourceElementId))
        {
            css.AppendLine();
            css.AppendLine($"[data-id=\"{CssEscape(element.LegacySource.SourceElementId)}\"] {{");
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
        css.AppendLine($"#{CssIdentifier(element.Id)} {{");
        css.AppendLine($"  left: {Format(absoluteX)}px;");
        css.AppendLine($"  top: {Format(absoluteY)}px;");
        css.AppendLine($"  width: {Format(element.Bounds.Width)}px;");
        css.AppendLine($"  height: {Format(element.Bounds.Height)}px;");
        css.AppendLine($"  color: {style.Foreground};");
        css.AppendLine($"  background: {style.Background};");
        css.AppendLine($"  font-family: {style.FontFamily};");
        css.AppendLine($"  font-size: {Format(style.FontSize)}px;");
        css.AppendLine($"  border: {Format(style.BorderWidth)}px {NormalizeBorderStyle(style.BorderStyle)} {style.BorderColor};");
        css.AppendLine($"  box-shadow: {ShadowCss(style.ShadowPreset)};");
        if (!string.IsNullOrWhiteSpace(style.AdvancedCss))
        {
            css.AppendLine($"  {style.AdvancedCss}");
        }
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
Copy the content of the div with id ft100-{scene.Id} into the target FT100 container,
include css/{scene.Id}.css, and serve images/ next to that CSS/HTML path or preserve the relative paths.
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
            Actions = scene.ActionDefinitions
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
            Actions = actions
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
                .Where(element => element.Kind != ScadaElementKind.Group)
                .Select(element => new
                {
                    element.Id,
                    element.DisplayName,
                    Kind = element.Kind.ToString(),
                    Events = element.EventBindings
                })
                .ToArray()
        };
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
""";
    }

    private static string BuildRuntimeScript(ScadaScene scene)
    {
        var actionsJson = JsonSerializer.Serialize(
            scene.ActionDefinitions.ToDictionary(action => action.Id, StringComparer.Ordinal),
            ManifestJsonOptions);
        return $$"""
(function () {
  const actions = {{actionsJson}};

  function navigate(targetPageId) {
    if (!targetPageId) {
      return;
    }

    window.location.href = '../' + encodeURIComponent(targetPageId) + '/' + encodeURIComponent(targetPageId) + '.html';
  }

  function applyAction(action) {
    if (!action || !action.Kind) {
      return;
    }

    const kind = String(action.Kind).toLowerCase();
    if (kind === 'navigate') {
      navigate(action.TargetPageId);
      return;
    }

    const target = action.TargetElementId ? document.getElementById(action.TargetElementId.replace(/[^a-zA-Z0-9_-]/g, '_')) : null;
    if (!target) {
      return;
    }

    if (kind === 'show') {
      target.hidden = false;
    } else if (kind === 'hide') {
      target.hidden = true;
    } else if (kind === 'togglevisibility') {
      target.hidden = !target.hidden;
    } else if (kind === 'setclass' && action.ClassName) {
      target.classList.add(action.ClassName);
    } else if (kind === 'toggleclass' && action.ClassName) {
      target.classList.toggle(action.ClassName);
    }
  }

  document.querySelectorAll('[data-scada-events]').forEach(function (element) {
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
        applyAction(actions[binding.ActionId]);
      });
    });
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
