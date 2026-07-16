using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ScadaBuilderV2.Application.RuntimeContracts;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.RuntimeContracts;
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

    /// <summary>
    /// Options used for state/command config serialization in both manifest and
    /// HTML data attributes. Uses camelCase so the JS runtime can read properties
    /// directly (the JS modules use camelCase per convention).
    /// </summary>
    private static readonly JsonSerializerOptions StateCommandJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<Ft100SceneExportResult> ExportAsync(
        ScadaScene scene,
        string? sourceHtmlPath,
        string exportDirectory,
        ScadaProject? project = null,
        CancellationToken cancellationToken = default,
        string? runtimeHash = null,
        Ft100ManifestProfile manifestProfile = Ft100ManifestProfile.Strict23)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportDirectory);

        if (project is not null)
        {
            var identityProjection = PageRuntimeIdentityResolver.Project(project, [scene]);
            project = identityProjection.Project;
            scene = identityProjection.Scenes[0];
        }

        var runtimeCapabilities = ScadaRuntimeCapabilityAnalyzer.Analyze(
            project ?? ScadaProject.CreateDefault("Standalone FT100 export"),
            [scene]);
        EnsureRuntimeCapabilitiesExportable(runtimeCapabilities, manifestProfile);

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

        var tagCatalog = project?.TagCatalog;
        var warnings = new List<string>();

        if (scene.EffectiveOrigin == PageOrigin.Imported && string.IsNullOrWhiteSpace(sourceHtmlPath))
        {
            throw new InvalidOperationException($"Imported page '{scene.EffectivePageCode}' has no resolved HTML projection.");
        }

        if (!string.IsNullOrWhiteSpace(sourceHtmlPath) && !File.Exists(sourceHtmlPath))
        {
            throw new FileNotFoundException("Source HTML not found.", sourceHtmlPath);
        }

        runtimeHash ??= ExportSharedRuntime(exportDirectory);
        var runtimeSha256 = Sha256Hash(Path.Combine(exportDirectory, $"scada-runtime.{runtimeHash}.js"));

        var sceneDirectory = Path.Combine(exportDirectory, scene.Id);
        var cssDirectory = Path.Combine(sceneDirectory, "css");
        var imagesDirectory = Path.Combine(sceneDirectory, "images");
        Directory.CreateDirectory(sceneDirectory);
        Directory.CreateDirectory(cssDirectory);
        Directory.CreateDirectory(imagesDirectory);

        var copiedImages = 0;
        var rewrittenSourceContent = string.Empty;
        if (!string.IsNullOrWhiteSpace(sourceHtmlPath))
        {
            var sourceHtml = await File.ReadAllTextAsync(sourceHtmlPath, cancellationToken);
            var sourceContent = RemoveSuppressedSourceElements(
                ExtractPageContent(sourceHtml),
                scene.GetSuppressedSourceElementIds());
            copiedImages = CopyAndRewriteImageAssets(
                sourceContent,
                Path.GetDirectoryName(Path.GetFullPath(sourceHtmlPath))!,
                imagesDirectory,
                out rewrittenSourceContent);
        }
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
            BuildDocumentCss(scene),
            Encoding.UTF8,
            cancellationToken);

        // Content-hash CSS filename for immutable cache-busting (§4.1).
        var cssHash = ContentHash(cssPath);
        if (!string.IsNullOrEmpty(cssHash))
        {
            var hashedCssFileName = $"{scene.Id}.{cssHash}.css";
            var hashedCssPath = Path.Combine(cssDirectory, hashedCssFileName);
            File.Move(cssPath, hashedCssPath);
            cssFileName = hashedCssFileName;
            cssPath = hashedCssPath;
        }

        await File.WriteAllTextAsync(
            htmlPath,
            BuildDocumentHtml(
                scene,
                $"css/{cssFileName}",
                normalizedSourceContent,
                $"../scada-runtime.{runtimeHash}.js",
                tagCatalog,
                warnings),
            Encoding.UTF8,
            cancellationToken);

        await File.WriteAllTextAsync(
            manifestPath,
            BuildManifest(scene, project, warnings, runtimeCapabilities, runtimeSha256, manifestProfile),
            Encoding.UTF8,
            cancellationToken);

        await File.WriteAllTextAsync(
            Path.Combine(sceneDirectory, "README.txt"),
            BuildReadme(scene),
            Encoding.UTF8,
            cancellationToken);

        return new Ft100SceneExportResult(sceneDirectory, htmlPath, cssPath, imagesDirectory, copiedImages, warnings);
    }

    public async Task<Ft100ProjectExportResult> ExportProjectAsync(
        ScadaProject project,
        IReadOnlyList<Ft100ProjectPageExportInput> pages,
        string exportDirectory,
        CancellationToken cancellationToken = default,
        Ft100ManifestProfile manifestProfile = Ft100ManifestProfile.Strict23)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportDirectory);

        var identityProjection = PageRuntimeIdentityResolver.Project(
            project,
            pages.Select(page => page.Scene).ToArray());
        project = identityProjection.Project;
        pages = pages
            .Select((page, index) => page with { Scene = identityProjection.Scenes[index] })
            .ToArray();

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

        var exportedScenes = pageInputsById.Values.Select(input => input.Scene).ToArray();
        var runtimeCapabilities = ScadaRuntimeCapabilityAnalyzer.Analyze(project, exportedScenes);
        EnsureRuntimeCapabilitiesExportable(runtimeCapabilities, manifestProfile);

        var packageDirectory = ResolveProjectPackageDirectory(exportDirectory);
        RecreateProjectPackageDirectory(exportDirectory, packageDirectory);
        var runtimeHash = ExportSharedRuntime(packageDirectory);
        var runtimeSha256 = Sha256Hash(Path.Combine(packageDirectory, $"scada-runtime.{runtimeHash}.js"));

        var pageResults = new List<Ft100SceneExportResult>();
        foreach (var pageId in compiledPageIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            var input = pageInputsById[pageId];
            pageResults.Add(await ExportAsync(
                input.Scene,
                input.SourceHtmlPath,
                packageDirectory,
                project,
                cancellationToken,
                runtimeHash,
                manifestProfile));
        }

        var manifestWarnings = pageResults
            .SelectMany(page => page.Warnings)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var manifestPath = Path.Combine(packageDirectory, "manifest.json");
        await File.WriteAllTextAsync(
            manifestPath,
            BuildProjectManifest(project, exportedScenes, manifestWarnings, runtimeCapabilities, runtimeSha256, manifestProfile),
            Encoding.UTF8,
            cancellationToken);

        var projectWarnings = manifestWarnings
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        await File.WriteAllTextAsync(
            Path.Combine(packageDirectory, "README.txt"),
            BuildProjectReadme(project, pageResults.Count),
            Encoding.UTF8,
            cancellationToken);

        return new Ft100ProjectExportResult(
            packageDirectory,
            manifestPath,
            pageResults,
            pageResults.Sum(result => result.CopiedImageCount),
            projectWarnings);
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
        CancellationToken cancellationToken = default,
        Ft100ManifestProfile manifestProfile = Ft100ManifestProfile.Strict23)
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
            var packageResult = await ExportProjectAsync(project, pages, stagingRoot, cancellationToken, manifestProfile);
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

    private static readonly string[] RuntimeModuleOrder =
    [
        "expression-evaluator.js",
        "effect-applier.js",
        "state-engine.js",
        "animation-controller.js",
        "command-dispatcher.js",
        "tag-bridge.js",
        "input-edit-guard.js",
        "confirmation-modal.js",
        "scada-runtime.js"
    ];

    /// <summary>Returns the concatenated SCADA runtime JavaScript from embedded resource modules.</summary>
    /// <remarks>
    /// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md.
    /// Tests: tests/ScadaBuilderV2.Tests/Runtime/RuntimeJsModulesTests.cs.
    /// </remarks>
    public static string GetRuntimeScript()
    {
        var assembly = typeof(Ft100SceneExporter).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();
        var sb = new StringBuilder();
        var version = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        foreach (var moduleName in RuntimeModuleOrder)
        {
            var match = resourceNames.FirstOrDefault(name =>
                name.EndsWith(moduleName, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                throw new InvalidOperationException(
                    $"Runtime module '{moduleName}' not found as embedded resource. " +
                    "Ensure the file is included in the project with build action EmbeddedResource.");
            }

            using var stream = assembly.GetManifestResourceStream(match);
            if (stream == null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            // Only replace the version placeholder, not arbitrary {{...}} patterns in tag names.
            content = content.Replace("{{RUNTIME_VERSION}}", version)
                             .Replace("{{SCADA_RUNTIME_VERSION}}", version);
            sb.Append(content);
            sb.Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Computes an 8-character lowercase hex content hash of the file at <paramref name="filePath"/>.
    /// </summary>
    public static string ContentHash(string filePath)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = File.ReadAllBytes(filePath);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }

    /// <summary>Computes the complete lowercase SHA-256 hash of a packaged artifact.</summary>
    public static string Sha256Hash(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>
    /// Exports the shared SCADA runtime JavaScript to the package directory, returning its content hash.
    /// The file is written as <c>scada-runtime.&lt;hash&gt;.js</c>.
    /// </summary>
    private static string ExportSharedRuntime(string packageDirectory)
    {
        Directory.CreateDirectory(packageDirectory);
        var runtimeScript = GetRuntimeScript();
        var tempDir = Path.Combine(Path.GetTempPath(), "scada-builder-v2-runtime", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "runtime.js");
        File.WriteAllText(tempFile, runtimeScript, Encoding.UTF8);
        try
        {
            var hash = ContentHash(tempFile);
            var finalPath = Path.Combine(packageDirectory, $"scada-runtime.{hash}.js");
            File.Move(tempFile, finalPath);
            return hash;
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
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

            // Content-hash image filename for immutable cache-busting (§4.1).
            var imageHash = ContentHash(targetPath);
            if (!string.IsNullOrEmpty(imageHash))
            {
                var ext = Path.GetExtension(fileName);
                var baseName = Path.GetFileNameWithoutExtension(fileName);
                var hashedFileName = $"{baseName}.{imageHash}{ext}";
                var hashedTargetPath = Path.Combine(imagesDirectory, hashedFileName);
                if (!File.Exists(hashedTargetPath))
                {
                    File.Move(targetPath, hashedTargetPath);
                }
                fileName = hashedFileName;
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

    /// <summary>
    /// Normalizes a <see cref="ScadaElementStateConfig"/> for export by replacing
    /// <see cref="ScadaExprTagRef.TagName"/> with the canonical identifier in all
    /// expression ASTs. Returns the normalized config and adds warnings for
    /// unresolved references. Throws on ambiguous references.
    /// </summary>
    private static ScadaElementStateConfig NormalizeStateConfigForExport(
        ScadaElementStateConfig config,
        ScadaTagCatalog? catalog,
        List<string> warnings)
    {
        if (config.States.Count == 0) return config;

        var hasAmbiguous = false;
        var ambiguousMessages = new List<string>();

        var normalizedStates = config.States.Select(state =>
        {
            if (state.Expression?.Ast is null) return state;

            var (normalizedAst, ambiguous) = NormalizeAstForExport(
                state.Expression.Ast, catalog, state.Expression.Source);
            if (ambiguous.Count > 0)
            {
                hasAmbiguous = true;
                ambiguousMessages.AddRange(ambiguous);
            }

            EmitUnresolvedWarnings(normalizedAst, state.Expression.Source, warnings);

            var referencedTags = CollectCanonicalTags(normalizedAst);
            var normalizedExpression = new ScadaExpression(
                state.Expression.Source, normalizedAst, referencedTags);

            return new ScadaStateRule(state.Id, state.Name, state.Enabled,
                normalizedExpression, state.Effect);
        }).ToArray();

        if (hasAmbiguous)
        {
            throw new InvalidOperationException(
                "L'export est bloque car des references de tag dans les expressions d'etat " +
                "sont ambigues :\n" + string.Join("\n", ambiguousMessages));
        }

        return new ScadaElementStateConfig(
            config.QualityFallback, config.DefaultEffect, normalizedStates, config.ReadVariable);
    }

    private static (ScadaExprNode Node, List<string> Ambiguous) NormalizeAstForExport(
        ScadaExprNode node, ScadaTagCatalog? catalog, string expressionSource)
    {
        return node switch
        {
            ScadaExprTagRef tagRef =>
                (NormalizeTagRefForExport(tagRef, catalog, expressionSource, out var amb), amb),
            ScadaExprUnary unary =>
                NormalizeUnaryForExport(unary, catalog, expressionSource),
            ScadaExprBinary binary =>
                NormalizeBinaryForExport(binary, catalog, expressionSource),
            ScadaExprFunc func =>
                NormalizeFuncForExport(func, catalog, expressionSource),
            _ => (node, new List<string>())
        };
    }

    private static ScadaExprTagRef NormalizeTagRefForExport(
        ScadaExprTagRef tagRef, ScadaTagCatalog? catalog, string source,
        out List<string> ambiguous)
    {
        ambiguous = new List<string>();

        // Priority 1: TagId already present → direct normalization
        if (!string.IsNullOrWhiteSpace(tagRef.TagId))
            return new ScadaExprTagRef(tagRef.TagId, tagRef.TagId);

        // Priority 2: resolve TagName via catalog
        if (catalog is not null)
        {
            var result = ScadaExpressionValidator.TryResolveTagReference(
                tagRef.TagName, catalog);
            switch (result.Status)
            {
                case TagResolveStatus.Resolved when result.CanonicalId is not null:
                    return new ScadaExprTagRef(result.CanonicalId, result.CanonicalId);
                case TagResolveStatus.Ambiguous:
                    ambiguous.Add(
                        $"Expression \"{source}\" : le tag '{{{tagRef.TagName}}}' est ambigu " +
                        $"({string.Join(", ", result.Matches)}). Remplacez-le par l'Id canonique.");
                    break;
            }
        }

        // Unresolved: leave TagName as-is → qualityFallback at runtime
        return tagRef;
    }

    private static (ScadaExprNode Node, List<string> Ambiguous) NormalizeUnaryForExport(
        ScadaExprUnary unary, ScadaTagCatalog? catalog, string source)
    {
        var (operand, amb) = NormalizeAstForExport(unary.Operand, catalog, source);
        return (new ScadaExprUnary(unary.Op, operand), amb);
    }

    private static (ScadaExprNode Node, List<string> Ambiguous) NormalizeBinaryForExport(
        ScadaExprBinary binary, ScadaTagCatalog? catalog, string source)
    {
        var (left, ambL) = NormalizeAstForExport(binary.Left, catalog, source);
        var (right, ambR) = NormalizeAstForExport(binary.Right, catalog, source);
        var allAmb = new List<string>();
        allAmb.AddRange(ambL);
        allAmb.AddRange(ambR);
        return (new ScadaExprBinary(binary.Op, left, right), allAmb);
    }

    private static (ScadaExprNode Node, List<string> Ambiguous) NormalizeFuncForExport(
        ScadaExprFunc func, ScadaTagCatalog? catalog, string source)
    {
        var allAmb = new List<string>();
        var normalizedArgs = func.Args.Select(arg =>
        {
            var (a, amb) = NormalizeAstForExport(arg, catalog, source);
            allAmb.AddRange(amb);
            return a;
        }).ToArray();
        return (new ScadaExprFunc(func.Name, normalizedArgs), allAmb);
    }

    private static void EmitUnresolvedWarnings(
        ScadaExprNode node, string expressionSource, List<string> warnings)
    {
        switch (node)
        {
            case ScadaExprTagRef tagRef:
                if (!tagRef.TagName.StartsWith("tf100.mapping.", StringComparison.OrdinalIgnoreCase))
                {
                    AddWarningOnce(
                        warnings,
                        $"Expression \"{expressionSource}\" : la reference '{{{tagRef.TagName}}}' " +
                        "n'a pas pu etre resolue en Id canonique. Le runtime appliquera qualityFallback.");
                }
                break;
            case ScadaExprUnary unary:
                EmitUnresolvedWarnings(unary.Operand, expressionSource, warnings);
                break;
            case ScadaExprBinary binary:
                EmitUnresolvedWarnings(binary.Left, expressionSource, warnings);
                EmitUnresolvedWarnings(binary.Right, expressionSource, warnings);
                break;
            case ScadaExprFunc func:
                foreach (var arg in func.Args)
                    EmitUnresolvedWarnings(arg, expressionSource, warnings);
                break;
        }
    }

    private static void AddWarningOnce(List<string> warnings, string warning)
    {
        if (!warnings.Contains(warning, StringComparer.Ordinal))
        {
            warnings.Add(warning);
        }
    }

    private static IReadOnlyList<string> CollectCanonicalTags(ScadaExprNode node)
    {
        var tags = new List<string>();
        CollectTagsForExport(node, tags);
        return tags;
    }

    private static void CollectTagsForExport(ScadaExprNode node, List<string> tags)
    {
        switch (node)
        {
            case ScadaExprTagRef tagRef:
                tags.Add(tagRef.TagName);
                break;
            case ScadaExprUnary unary:
                CollectTagsForExport(unary.Operand, tags);
                break;
            case ScadaExprBinary binary:
                CollectTagsForExport(binary.Left, tags);
                CollectTagsForExport(binary.Right, tags);
                break;
            case ScadaExprFunc func:
                foreach (var arg in func.Args)
                    CollectTagsForExport(arg, tags);
                break;
        }
    }

    internal static string BuildDocumentHtml(
        ScadaScene scene,
        string stylesheetHref,
        string sourceContent,
        string? runtimeScriptSource,
        ScadaTagCatalog? tagCatalog, List<string> warnings)
    {
        var scope = Ft100ExportScope.ForScene(scene);
        var title = HtmlEncoder.Default.Encode(scene.Title);
        var pageId = HtmlEncoder.Default.Encode(scene.Id);
        var rootDomId = HtmlEncoder.Default.Encode(scope.RootDomId);
        var pageClass = HtmlEncoder.Default.Encode(CssIdentifier(scene.Id));
        var sceneType = HtmlEncoder.Default.Encode(ToManifestPageType(scene.PageType));
        var rootStyle = HtmlEncoder.Default.Encode(BuildSceneRootInlineStyle(scene));
        var modernElements = string.Concat(scene.Elements.Select(
            element => BuildElementHtml(element, 0, 0, scope, tagCatalog, warnings)));
        var runtimeScript = string.IsNullOrWhiteSpace(runtimeScriptSource)
            ? string.Empty
            : $"  <script src=\"{HtmlEncoder.Default.Encode(runtimeScriptSource)}\" defer></script>";

        return $$"""
<!doctype html>
<html lang="fr">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{title}}</title>
  <link rel="stylesheet" href="{{HtmlEncoder.Default.Encode(stylesheetHref)}}">
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
{{runtimeScript}}
</body>
</html>
""";
    }

    private static string BuildElementHtml(ScadaElement element, double parentX, double parentY, Ft100ExportScope scope,
        ScadaTagCatalog? tagCatalog, List<string> warnings)
    {
        if (element.IsLegacyStatic)
        {
            return "";
        }

        var absoluteX = parentX + element.Bounds.X;
        var absoluteY = parentY + element.Bounds.Y;
        if (element.Kind == ScadaElementKind.Group)
        {
            if (GroupRequiresRuntimeWrapper(element))
            {
                var groupId = HtmlEncoder.Default.Encode(scope.ElementDomId(element.Id));
                var groupSceneElementId = HtmlEncoder.Default.Encode(element.Id);
                var groupName = HtmlEncoder.Default.Encode(element.DisplayName);
                var groupKind = HtmlEncoder.Default.Encode(element.Kind.ToString());
                var groupInlineStyle = HtmlEncoder.Default.Encode(BuildRuntimeGroupInlineStyle(element, absoluteX, absoluteY));
                var groupValueBindingAttributes = BuildValueBindingAttributes(element);
                var groupStateCommandAttributes = BuildStateCommandAttributes(element, tagCatalog, warnings);
                var children = string.Concat(element.ChildElements.Select(child => BuildElementHtml(child, 0, 0, scope, tagCatalog, warnings)));

                return $$"""
<div id="{{groupId}}" class="ft100-element ft100-element--{{groupKind}}" data-scada-element-id="{{groupSceneElementId}}" data-name="{{groupName}}" style="{{groupInlineStyle}}"{{groupValueBindingAttributes}}{{groupStateCommandAttributes}}>
{{Indent(children, 2)}}
</div>
""";
            }

            return string.Concat(element.ChildElements.Select(child => BuildElementHtml(child, absoluteX, absoluteY, scope, tagCatalog, warnings)));
        }

        var id = HtmlEncoder.Default.Encode(scope.ElementDomId(element.Id));
        var sceneElementId = HtmlEncoder.Default.Encode(element.Id);
        var name = HtmlEncoder.Default.Encode(element.DisplayName);
        var kind = HtmlEncoder.Default.Encode(element.Kind.ToString());
        var inlineStyle = HtmlEncoder.Default.Encode(BuildElementInlineStyle(element, absoluteX, absoluteY));
        var content = BuildElementContent(element, scope);
        var valueBindingAttributes = BuildValueBindingAttributes(element);
        var buttonRuntimeAttributes = BuildButtonRuntimeAttributes(element);
        var stateCommandAttributes = BuildStateCommandAttributes(element, tagCatalog, warnings);

        return $$"""
<div id="{{id}}" class="ft100-element ft100-element--{{kind}}" data-scada-element-id="{{sceneElementId}}" data-name="{{name}}" style="{{inlineStyle}}"{{valueBindingAttributes}}{{buttonRuntimeAttributes}}{{stateCommandAttributes}}>
  {{content}}
</div>
""";
    }

    /// <summary>
    /// Decommissioned: <c>data-scada-events</c> is no longer emitted as an active
    /// runtime contract. This method returns an empty string unconditionally.
    /// The legacy implementation is preserved for reference until physical
    /// removal of the decommissioned EventBindings code.
    /// </summary>
    /// <remarks>
    /// Contracts: docs/superpowers/specs/2026-07-09-export-group-runtime-wrapper.md §4.2.
    /// </remarks>
    private static string BuildEventAttribute(ScadaElement element)
    {
        _ = element;
        return "";
    }

    private static string BuildButtonRuntimeAttributes(ScadaElement element)
    {
        if (element.Kind != ScadaElementKind.Button)
        {
            return "";
        }

        var buttonKind = HtmlEncoder.Default.Encode(element.EffectiveButtonKind.ToString());
        var attributes = $" data-scada-button-kind=\"{buttonKind}\"";
        if (element.EffectiveButtonBehavior.IsDisabled)
        {
            attributes += " data-scada-disabled=\"true\" aria-disabled=\"true\"";
        }

        return element.EffectiveButtonKind == ScadaButtonKind.Toggle
            ? attributes + " data-scada-toggle-state=\"off\""
            : attributes;
    }

    private static string BuildStateCommandAttributes(
        ScadaElement element, ScadaTagCatalog? catalog, List<string> warnings)
    {
        var stateConfig = element.EffectiveStateConfig;
        var commandConfig = element.EffectiveCommandConfig;
        var hasStateConfig = stateConfig.States.Count > 0
            || stateConfig.ReadVariable is not null
            || HasNonDefaultFallback(stateConfig);
        var hasCommandConfig = commandConfig.Commands.Count > 0;

        if (!hasStateConfig && !hasCommandConfig)
        {
            return "";
        }

        var attributes = new StringBuilder();
        if (hasStateConfig)
        {
            var exportConfig = NormalizeStateConfigForExport(stateConfig, catalog, warnings);
            var json = JsonSerializer.Serialize(exportConfig, StateCommandJsonOptions);
            attributes.Append(" data-scada-state-config=\"");
            attributes.Append(HtmlEncoder.Default.Encode(json));
            attributes.Append('"');
        }

        if (hasCommandConfig)
        {
            var json = JsonSerializer.Serialize(BuildRuntimeCommandConfig(commandConfig), StateCommandJsonOptions);
            attributes.Append(" data-scada-command-config=\"");
            attributes.Append(HtmlEncoder.Default.Encode(json));
            attributes.Append('"');
        }

        return attributes.ToString();
    }

    private static bool HasNonDefaultFallback(ScadaElementStateConfig config)
    {
        var fallback = config.QualityFallback;
        var defaultFallback = ScadaElementStateConfig.Default.QualityFallback;
        return fallback.Opacity != defaultFallback.Opacity
            || fallback.BorderColor != defaultFallback.BorderColor
            || fallback.BorderWidth != defaultFallback.BorderWidth
            || config.DefaultEffect != ScadaEffectBlock.Empty;
    }

    /// <summary>
    /// Determines whether a Group element requires a runtime DOM wrapper in the
    /// exported output. Only modern runtime data is considered; legacy
    /// <see cref="ScadaElement.EventBindings"/> and
    /// <see cref="ScadaElementData.ReadTagId"/>/<see cref="ScadaElementData.WriteTagId"/>
    /// are intentionally excluded (decommissioned paths).
    /// </summary>
    /// <remarks>
    /// Contracts: docs/superpowers/specs/2026-07-09-export-group-runtime-wrapper.md.
    /// Tests: tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.
    /// </remarks>
    private static bool GroupRequiresRuntimeWrapper(ScadaElement element)
    {
        if (element.Kind != ScadaElementKind.Group)
            return false;

        var commandConfig = element.EffectiveCommandConfig;
        var stateConfig = element.EffectiveStateConfig;

        return commandConfig.Commands.Count > 0
            || stateConfig.States.Count > 0
            || stateConfig.ReadVariable is not null
            || HasNonDefaultFallback(stateConfig);
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

    private static string BuildElementContent(ScadaElement element, Ft100ExportScope scope)
    {
        var data = element.Data ?? new ScadaElementData(null, null, null, null, null, null, null, null, null, false);
        return element.Kind switch
        {
            ScadaElementKind.Custom => ForceCustomSvgAspectRatio(ScopeSvgIds(data.Text ?? "", scope, element.Id)),
            ScadaElementKind.Text => $"<span data-scada-text>{HtmlEncoder.Default.Encode(data.Text ?? element.DisplayName)}</span>",
            ScadaElementKind.InputNumeric when data.IsReadOnly => HtmlEncoder.Default.Encode(
                data.Value?.ToString(CultureInfo.InvariantCulture) ?? data.DisplayFormat ?? data.Placeholder ?? ""),
            ScadaElementKind.InputNumeric => BuildInput(element, "number"),
            ScadaElementKind.InputText => BuildInput(element, "text"),
            ScadaElementKind.Shape => BuildShape(element, scope),
            ScadaElementKind.Button => BuildButton(element),
            ScadaElementKind.Table => ModernTableHtmlRenderer.Render(element, scope.ElementDomId(element.Id)),
            _ => ""
        };
    }

    private static string BuildShape(ScadaElement element, Ft100ExportScope scope)
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
        var svgId = HtmlEncoder.Default.Encode($"{scope.ElementDomId(element.Id)}__shape");
        var markerId = HtmlEncoder.Default.Encode($"{scope.ElementDomId(element.Id)}__arrow");
        var gradientId = HtmlEncoder.Default.Encode($"{scope.ElementDomId(element.Id)}__lamp-gradient");
        var common = $"stroke=\"{stroke}\" stroke-width=\"{Format(strokeWidth)}\"{dashAttribute} vector-effect=\"non-scaling-stroke\"";
        var body = element.EffectiveShapeKind switch
        {
            ScadaShapeKind.IndicatorLamp =>
                $"""<defs><radialGradient id="{gradientId}" cx="35%" cy="28%" r="70%"><stop offset="0%" stop-color="#ffffff" stop-opacity="0.85"/><stop offset="42%" stop-color="{fill}"/><stop offset="100%" stop-color="{stroke}"/></radialGradient></defs><circle cx="{Format(width / 2)}" cy="{Format(height / 2)}" r="{Format(Math.Max(0, Math.Min(width, height) / 2 - halfStroke))}" fill="url(#{gradientId})" {common}/>""",
            ScadaShapeKind.HorizontalBar =>
                BuildBarShape(width, height, strokeWidth, halfStroke, stroke, fill, data.Value, vertical: false, common),
            ScadaShapeKind.VerticalBar =>
                BuildBarShape(width, height, strokeWidth, halfStroke, stroke, fill, data.Value, vertical: true, common),
            ScadaShapeKind.Tank =>
                BuildTankShape(width, height, strokeWidth, halfStroke, fill, data.Value, common),
            ScadaShapeKind.PipeHorizontal =>
                BuildPipeShape(width, height, strokeWidth, halfStroke, fill, vertical: false, common),
            ScadaShapeKind.PipeVertical =>
                BuildPipeShape(width, height, strokeWidth, halfStroke, fill, vertical: true, common),
            ScadaShapeKind.Valve =>
                BuildValveShape(width, height, halfStroke, fill, common),
            ScadaShapeKind.Pump =>
                BuildPumpShape(width, height, halfStroke, stroke, fill, common),
            ScadaShapeKind.Motor =>
                BuildMotorShape(width, height, strokeWidth, halfStroke, stroke, fill, common),
            ScadaShapeKind.Fan =>
                BuildFanShape(width, height, halfStroke, stroke, fill, common),
            ScadaShapeKind.Conveyor =>
                BuildConveyorShape(width, height, strokeWidth, halfStroke, fill, common),
            ScadaShapeKind.Gauge =>
                BuildGaugeShape(width, height, strokeWidth, halfStroke, stroke, fill, data.Value, common),
            ScadaShapeKind.Switch =>
                BuildSwitchShape(width, height, halfStroke, common),
            ScadaShapeKind.Breaker =>
                BuildBreakerShape(width, height, strokeWidth, halfStroke, stroke, common),
            ScadaShapeKind.Transformer =>
                BuildTransformerShape(width, height, common),
            ScadaShapeKind.AlarmBeacon =>
                BuildAlarmBeaconShape(width, height, strokeWidth, stroke, fill, common),
            ScadaShapeKind.Circle =>
                $"""<circle cx="{Format(width / 2)}" cy="{Format(height / 2)}" r="{Format(Math.Max(0, Math.Min(width, height) / 2 - halfStroke))}" fill="{fill}" {common}/>""",
            ScadaShapeKind.Ellipse =>
                $"""<ellipse cx="{Format(width / 2)}" cy="{Format(height / 2)}" rx="{Format(Math.Max(0, (width / 2) - halfStroke))}" ry="{Format(Math.Max(0, (height / 2) - halfStroke))}" fill="{fill}" {common}/>""",
            ScadaShapeKind.Triangle =>
                $"""<polygon points="{Format(width / 2)},{Format(halfStroke)} {Format(Math.Max(halfStroke, width - halfStroke))},{Format(Math.Max(halfStroke, height - halfStroke))} {Format(halfStroke)},{Format(Math.Max(halfStroke, height - halfStroke))}" fill="{fill}" {common}/>""",
            ScadaShapeKind.Star =>
                $"""<polygon points="{BuildStarPoints(width, height, halfStroke)}" fill="{fill}" {common}/>""",
            ScadaShapeKind.Line =>
                $"""<line x1="{Format(data.ShapeStartX ?? halfStroke)}" y1="{Format(data.ShapeStartY ?? height / 2)}" x2="{Format(data.ShapeEndX ?? Math.Max(halfStroke, width - halfStroke))}" y2="{Format(data.ShapeEndY ?? height / 2)}" {common}/>""",
            ScadaShapeKind.Arrow =>
                $"""<defs><marker id="{markerId}" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M 0 0 L 10 5 L 0 10 z" fill="{stroke}"/></marker></defs><line x1="{Format(data.ShapeStartX ?? halfStroke)}" y1="{Format(data.ShapeStartY ?? height / 2)}" x2="{Format(data.ShapeEndX ?? Math.Max(halfStroke, width - halfStroke - 7))}" y2="{Format(data.ShapeEndY ?? height / 2)}" marker-end="url(#{markerId})" {common}/>""",
            ScadaShapeKind.RoundedRectangle =>
                $"""<rect x="{Format(halfStroke)}" y="{Format(halfStroke)}" width="{Format(Math.Max(0, width - strokeWidth))}" height="{Format(Math.Max(0, height - strokeWidth))}" rx="{Format(Math.Min(width, height) * 0.12)}" ry="{Format(Math.Min(width, height) * 0.12)}" fill="{fill}" {common}/>""",
            _ =>
                $"""<rect x="{Format(halfStroke)}" y="{Format(halfStroke)}" width="{Format(Math.Max(0, width - strokeWidth))}" height="{Format(Math.Max(0, height - strokeWidth))}" fill="{fill}" {common}/>"""
        };

        return $"""<svg id="{svgId}" viewBox="0 0 {Format(width)} {Format(height)}" width="100%" height="100%" preserveAspectRatio="none" style="display:block;pointer-events:none;">{body}</svg>""";
    }

    private static string BuildStarPoints(double width, double height, double halfStroke)
    {
        var centerX = width / 2;
        var centerY = height / 2;
        var outerRadius = Math.Max(0, Math.Min(width, height) / 2 - halfStroke);
        var innerRadius = outerRadius * 0.45;
        return string.Join(
            " ",
            Enumerable.Range(0, 10).Select(index =>
            {
                var radius = index % 2 == 0 ? outerRadius : innerRadius;
                var angle = (-90 + (index * 36)) * Math.PI / 180;
                return $"{Format(centerX + Math.Cos(angle) * radius)},{Format(centerY + Math.Sin(angle) * radius)}";
            }));
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

    private static string BuildTankShape(
        double width,
        double height,
        double strokeWidth,
        double halfStroke,
        string fill,
        double? value,
        string common)
    {
        var percent = ClampPercent(value);
        var bodyTop = halfStroke + 8;
        var bodyHeight = Math.Max(0, height - strokeWidth - 16);
        var bodyWidth = Math.Max(0, width - strokeWidth);
        var innerX = halfStroke + 6;
        var innerY = bodyTop + 6;
        var innerWidth = Math.Max(0, bodyWidth - 12);
        var innerHeight = Math.Max(0, bodyHeight - 12);
        var fillHeight = innerHeight * percent / 100;
        var radius = Format(Math.Min(10, width * 0.12));
        var topRadiusY = Format(Math.Max(3, Math.Min(12, height * 0.08)));

        return
            $"""<rect x="{Format(halfStroke)}" y="{Format(bodyTop)}" width="{Format(bodyWidth)}" height="{Format(bodyHeight)}" rx="{radius}" fill="#f7fbf5" {common}/>""" +
            $"""<rect x="{Format(innerX)}" y="{Format(innerY + innerHeight - fillHeight)}" width="{Format(innerWidth)}" height="{Format(fillHeight)}" rx="{Format(Math.Min(5, width * 0.06))}" fill="{fill}"/>""" +
            $"""<ellipse cx="{Format(width / 2)}" cy="{Format(bodyTop)}" rx="{Format(Math.Max(0, bodyWidth / 2))}" ry="{topRadiusY}" fill="#f7fbf5" {common}/>""";
    }

    private static string BuildPipeShape(
        double width,
        double height,
        double strokeWidth,
        double halfStroke,
        string fill,
        bool vertical,
        string common)
    {
        var x = vertical ? width * 0.25 : halfStroke;
        var y = vertical ? halfStroke : height * 0.25;
        var pipeWidth = vertical ? width * 0.5 : Math.Max(0, width - strokeWidth);
        var pipeHeight = vertical ? Math.Max(0, height - strokeWidth) : height * 0.5;
        var radius = Format(Math.Min(8, Math.Min(width, height) * 0.2));
        return $"""<rect x="{Format(x)}" y="{Format(y)}" width="{Format(pipeWidth)}" height="{Format(pipeHeight)}" rx="{radius}" fill="{fill}" {common}/>""";
    }

    private static string BuildValveShape(
        double width,
        double height,
        double halfStroke,
        string fill,
        string common)
    {
        return
            $"""<polygon points="{Format(halfStroke)},{Format(halfStroke)} {Format(width / 2)},{Format(height / 2)} {Format(halfStroke)},{Format(height - halfStroke)}" fill="{fill}" {common}/>""" +
            $"""<polygon points="{Format(width - halfStroke)},{Format(halfStroke)} {Format(width / 2)},{Format(height / 2)} {Format(width - halfStroke)},{Format(height - halfStroke)}" fill="{fill}" {common}/>""" +
            $"""<line x1="{Format(width / 2)}" y1="{Format(halfStroke)}" x2="{Format(width / 2)}" y2="{Format(height / 2)}" {common}/>""";
    }

    private static string BuildPumpShape(
        double width,
        double height,
        double halfStroke,
        string stroke,
        string fill,
        string common)
    {
        var radius = Math.Max(0, Math.Min(width, height) * 0.38 - halfStroke);
        var cx = width * 0.42;
        var cy = height / 2;
        var outletX = cx + radius * 0.65;
        var outletY = cy - Math.Max(5, radius * 0.22);
        var outletWidth = Math.Max(8, width - outletX - halfStroke);
        var outletHeight = Math.Max(10, radius * 0.44);

        return
            $"""<circle cx="{Format(cx)}" cy="{Format(cy)}" r="{Format(radius)}" fill="{fill}" {common}/>""" +
            $"""<rect x="{Format(outletX)}" y="{Format(outletY)}" width="{Format(outletWidth)}" height="{Format(outletHeight)}" fill="{fill}" {common}/>""" +
            $"""<path d="M {Format(cx - radius * 0.35)} {Format(cy - radius * 0.25)} L {Format(cx + radius * 0.42)} {Format(cy)} L {Format(cx - radius * 0.35)} {Format(cy + radius * 0.25)} Z" fill="{stroke}"/>""";
    }

    private static string BuildMotorShape(
        double width,
        double height,
        double strokeWidth,
        double halfStroke,
        string stroke,
        string fill,
        string common)
    {
        return
            $"""<rect x="{Format(halfStroke + width * 0.08)}" y="{Format(halfStroke + height * 0.18)}" width="{Format(Math.Max(0, width * 0.7 - strokeWidth))}" height="{Format(Math.Max(0, height * 0.58 - strokeWidth))}" rx="{Format(Math.Min(10, height * 0.16))}" fill="{fill}" {common}/>""" +
            $"""<rect x="{Format(width * 0.78)}" y="{Format(height * 0.42)}" width="{Format(Math.Max(6, width * 0.16 - halfStroke))}" height="{Format(Math.Max(6, height * 0.16))}" fill="#f7fbf5" {common}/>""" +
            $"""<text x="{Format(width * 0.42)}" y="{Format(height * 0.56)}" text-anchor="middle" font-size="{Format(Math.Max(12, Math.Min(width, height) * 0.24))}" font-family="Segoe UI, Arial, sans-serif" font-weight="700" fill="{stroke}">M</text>""";
    }

    private static string BuildFanShape(
        double width,
        double height,
        double halfStroke,
        string stroke,
        string fill,
        string common)
    {
        var cx = width / 2;
        var cy = height / 2;
        var radius = Math.Max(0, Math.Min(width, height) * 0.44 - halfStroke);
        var body = $"""<circle cx="{Format(cx)}" cy="{Format(cy)}" r="{Format(radius)}" fill="#f7fbf5" {common}/>""";
        var bladeVectors = new[] { (Dx: 0d, Dy: -1d), (Dx: 0.86, Dy: 0.5), (Dx: -0.86, Dy: 0.5) };
        var blades = string.Concat(bladeVectors.Select(vector =>
            $"""<path d="M {Format(cx)} {Format(cy)} Q {Format(cx + vector.Dx * radius * 0.48)} {Format(cy + vector.Dy * radius * 0.48)} {Format(cx + vector.Dx * radius * 0.18 - vector.Dy * radius * 0.28)} {Format(cy + vector.Dy * radius * 0.18 + vector.Dx * radius * 0.28)} Q {Format(cx + vector.Dx * radius * 0.72)} {Format(cy + vector.Dy * radius * 0.72)} {Format(cx + vector.Dx * radius * 0.86 - vector.Dy * radius * 0.14)} {Format(cy + vector.Dy * radius * 0.86 + vector.Dx * radius * 0.14)} Q {Format(cx + vector.Dx * radius * 0.45)} {Format(cy + vector.Dy * radius * 0.45)} {Format(cx)} {Format(cy)} Z" fill="{fill}" {common}/>"""));
        var hub = $"""<circle cx="{Format(cx)}" cy="{Format(cy)}" r="{Format(Math.Max(4, radius * 0.16))}" fill="{stroke}"/>""";
        return body + blades + hub;
    }

    private static string BuildConveyorShape(
        double width,
        double height,
        double strokeWidth,
        double halfStroke,
        string fill,
        string common)
    {
        var belt = $"""<rect x="{Format(halfStroke)}" y="{Format(height * 0.25)}" width="{Format(Math.Max(0, width - strokeWidth))}" height="{Format(height * 0.42)}" rx="{Format(Math.Min(8, height * 0.18))}" fill="{fill}" {common}/>""";
        var rollers = string.Concat(new[] { 0.18, 0.5, 0.82 }.Select(position =>
            $"""<circle cx="{Format(width * position)}" cy="{Format(height * 0.72)}" r="{Format(Math.Max(4, height * 0.1))}" fill="#f7fbf5" {common}/>"""));
        var topLine = $"""<line x1="{Format(halfStroke + 6)}" y1="{Format(height * 0.36)}" x2="{Format(width - halfStroke - 6)}" y2="{Format(height * 0.36)}" {common}/>""";
        return belt + rollers + topLine;
    }

    private static string BuildGaugeShape(
        double width,
        double height,
        double strokeWidth,
        double halfStroke,
        string stroke,
        string fill,
        double? value,
        string common)
    {
        var percent = ClampPercent(value);
        var cx = width / 2;
        var cy = height * 0.58;
        var radius = Math.Max(0, Math.Min(width, height) * 0.42 - halfStroke);
        var angle = (-140 + (percent * 280 / 100)) * Math.PI / 180;
        return
            $"""<circle cx="{Format(cx)}" cy="{Format(cy)}" r="{Format(radius)}" fill="#f7fbf5" {common}/>""" +
            $"""<line x1="{Format(cx)}" y1="{Format(cy)}" x2="{Format(cx + Math.Cos(angle) * radius * 0.72)}" y2="{Format(cy + Math.Sin(angle) * radius * 0.72)}" stroke="{stroke}" stroke-width="{Format(Math.Max(2, strokeWidth + 1))}" vector-effect="non-scaling-stroke"/>""" +
            $"""<circle cx="{Format(cx)}" cy="{Format(cy)}" r="{Format(Math.Max(3, radius * 0.08))}" fill="{fill}" {common}/>""";
    }

    private static string BuildSwitchShape(
        double width,
        double height,
        double halfStroke,
        string common)
    {
        var y = height * 0.55;
        var leftX = width * 0.22;
        var rightX = width * 0.78;
        var terminalRadius = Math.Max(4, Math.Min(width, height) * 0.08);
        return
            $"""<line x1="{Format(halfStroke)}" y1="{Format(y)}" x2="{Format(width - halfStroke)}" y2="{Format(y)}" {common}/>""" +
            $"""<circle cx="{Format(leftX)}" cy="{Format(y)}" r="{Format(terminalRadius)}" fill="#f7fbf5" {common}/>""" +
            $"""<circle cx="{Format(rightX)}" cy="{Format(y)}" r="{Format(terminalRadius)}" fill="#f7fbf5" {common}/>""" +
            $"""<line x1="{Format(leftX)}" y1="{Format(y)}" x2="{Format(width * 0.64)}" y2="{Format(height * 0.28)}" {common}/>""";
    }

    private static string BuildBreakerShape(
        double width,
        double height,
        double strokeWidth,
        double halfStroke,
        string stroke,
        string common)
    {
        return
            $"""<rect x="{Format(halfStroke + width * 0.14)}" y="{Format(halfStroke + height * 0.12)}" width="{Format(Math.Max(0, width * 0.72 - strokeWidth))}" height="{Format(Math.Max(0, height * 0.76 - strokeWidth))}" rx="{Format(Math.Min(8, height * 0.12))}" fill="#f7fbf5" {common}/>""" +
            $"""<line x1="{Format(width * 0.37)}" y1="{Format(height * 0.68)}" x2="{Format(width * 0.63)}" y2="{Format(height * 0.34)}" stroke="{stroke}" stroke-width="{Format(Math.Max(2, strokeWidth + 1))}" vector-effect="non-scaling-stroke"/>""" +
            $"""<text x="{Format(width * 0.5)}" y="{Format(height * 0.48)}" text-anchor="middle" font-size="{Format(Math.Max(10, Math.Min(width, height) * 0.18))}" font-family="Segoe UI, Arial, sans-serif" font-weight="700" fill="{stroke}">CB</text>""";
    }

    private static string BuildTransformerShape(
        double width,
        double height,
        string common)
    {
        var core =
            $"""<line x1="{Format(width * 0.47)}" y1="{Format(height * 0.2)}" x2="{Format(width * 0.47)}" y2="{Format(height * 0.8)}" {common}/>""" +
            $"""<line x1="{Format(width * 0.53)}" y1="{Format(height * 0.2)}" x2="{Format(width * 0.53)}" y2="{Format(height * 0.8)}" {common}/>""";
        var coils = string.Concat(new[] { 0.28, 0.72 }.SelectMany(cx =>
            new[] { 0.34, 0.5, 0.66 }.Select(cy =>
                $"""<ellipse cx="{Format(width * cx)}" cy="{Format(height * cy)}" rx="{Format(Math.Max(5, width * 0.12))}" ry="{Format(Math.Max(5, height * 0.11))}" fill="none" {common}/>""")));
        return core + coils;
    }

    private static string BuildAlarmBeaconShape(
        double width,
        double height,
        double strokeWidth,
        string stroke,
        string fill,
        string common)
    {
        var rays = string.Concat(new[]
        {
            (X1: 0.5, Y1: 0.04, X2: 0.5, Y2: 0.16),
            (X1: 0.16, Y1: 0.32, X2: 0.28, Y2: 0.42),
            (X1: 0.84, Y1: 0.32, X2: 0.72, Y2: 0.42)
        }.Select(ray =>
            $"""<line x1="{Format(width * ray.X1)}" y1="{Format(height * ray.Y1)}" x2="{Format(width * ray.X2)}" y2="{Format(height * ray.Y2)}" stroke="{stroke}" stroke-width="{Format(Math.Max(2, strokeWidth + 1))}" vector-effect="non-scaling-stroke"/>"""));
        return
            $"""<rect x="{Format(width * 0.22)}" y="{Format(height * 0.72)}" width="{Format(width * 0.56)}" height="{Format(height * 0.14)}" rx="{Format(Math.Min(6, height * 0.05))}" fill="#f7fbf5" {common}/>""" +
            $"""<path d="M {Format(width * 0.24)} {Format(height * 0.72)} Q {Format(width * 0.5)} {Format(height * 0.12)} {Format(width * 0.76)} {Format(height * 0.72)} Z" fill="{fill}" {common}/>""" +
            rays;
    }

    private static string BuildButton(ScadaElement element)
    {
        var data = element.Data ?? new ScadaElementData(null, null, null, null, null, null, null, null, null, false);
        var label = HtmlEncoder.Default.Encode(data.Text ?? data.Placeholder ?? element.DisplayName);
        var buttonKind = HtmlEncoder.Default.Encode(element.EffectiveButtonKind.ToString());
        var disabled = element.EffectiveButtonBehavior.IsDisabled
            ? " disabled aria-disabled=\"true\""
            : "";
        return $"""<button type="button" data-scada-button-kind="{buttonKind}"{disabled} style="width:100%;height:100%;box-sizing:border-box;font:inherit;color:inherit;background:transparent;border:0;"><span data-scada-text>{label}</span></button>""";
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
        AppendStructuredStyle(css, style, string.Empty, inline: true);
        css.Append(element.Kind == ScadaElementKind.Shape
            ? "border:0 none transparent;"
            : $"border:{Format(style.BorderWidth)}px {NormalizeBorderStyle(style.BorderStyle)} {style.BorderColor};");
        css.Append($"box-shadow:{ShadowCss(style.ShadowPreset)};");
        css.Append($"opacity:{Format(Math.Clamp(style.Opacity, 0, 1))};");
        css.Append("transform-origin:center center;");
        var scaleX = style.FlipHorizontally ? -1 : 1;
        var scaleY = style.FlipVertically ? -1 : 1;
        css.Append($"transform:rotate({Format(style.Rotation)}deg) scaleX({scaleX}) scaleY({scaleY});");
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

    internal static string BuildDocumentCss(ScadaScene scene)
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
        css.AppendLine($"{scope.Descendant(".ft100-element--Button")}, {scope.Descendant("[data-scada-command-config]")} {{ cursor: pointer; }}");
        css.AppendLine($"{scope.Descendant(".ft100-element--Button *")}, {scope.Descendant("[data-scada-command-config] *")} {{ cursor: pointer; }}");
        css.AppendLine($"{scope.Descendant(".ft100-element--Button:active")}, {scope.Descendant("[data-scada-command-config]:active")} {{ cursor: pointer; }}");
        css.AppendLine($"{scope.Descendant(".ft100-element--Button[data-scada-disabled=\"true\"]")}, {scope.Descendant(".ft100-element--Button[data-scada-disabled=\"true\"] *")} {{ cursor: not-allowed; opacity: 0.62; }}");
        css.AppendLine($"{scope.Descendant(".ft100-element svg")} {{ display: block; width: 100%; height: 100%; overflow: visible; }}");
        css.AppendLine($"{scope.Descendant(".ft100-element input")} {{ width: 100%; height: 100%; box-sizing: border-box; }}");
        css.AppendLine($"{scope.Descendant(".ft100-element button")} {{ width: 100%; height: 100%; box-sizing: border-box; font: inherit; color: inherit; }}");
        css.AppendLine($"{scope.Descendant(".scada-modern-table")} {{ display: grid; width: 100%; height: 100%; overflow: hidden; }}");
        css.AppendLine($"{scope.Descendant(".scada-modern-table__cell")} {{ display: flex; box-sizing: border-box; min-width: 0; min-height: 0; overflow: hidden; }}");
        css.AppendLine($"{scope.Descendant(".scada-modern-table__cell input")} {{ width: 100%; height: 100%; min-width: 0; border: 0; padding: 0; background: transparent; color: inherit; font: inherit; }}");
        css.AppendLine($"{scope.Descendant(".scada-modern-table > tbody")} {{ display: contents; }}");

        foreach (var legacyId in scene.GetSuppressedSourceElementIds().OrderBy(id => id, StringComparer.Ordinal))
        {
            css.AppendLine($"{scope.SourceDataIdSelector(legacyId)} {{ display: none !important; }}");
        }

        foreach (var element in scene.Elements)
        {
            AppendElementCss(css, element, 0, 0, scope);
        }

        AppendAnimationKeyframes(css, scope);

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
            if (GroupRequiresRuntimeWrapper(element))
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
        AppendStructuredStyle(css, style, "  ", inline: false);
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
            AppendButtonPressedCss(css, element, scope);
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

    private static void AppendButtonPressedCss(StringBuilder css, ScadaElement element, Ft100ExportScope scope)
    {
        var behavior = element.EffectiveButtonBehavior;
        var pressed = behavior.EffectivePressed;
        if (behavior.IsDisabled || !pressed.Enabled)
        {
            return;
        }

        css.AppendLine();
        css.AppendLine($"{scope.ElementSelector(element.Id)}:active,");
        css.AppendLine($"{scope.ElementSelector(element.Id)}[data-scada-toggle-state=\"on\"] {{");
        css.AppendLine($"  background: {pressed.Background};");
        css.AppendLine($"  color: {pressed.Foreground};");
        css.AppendLine($"  border-color: {pressed.BorderColor};");
        css.AppendLine("}");
    }

    private static void AppendAnimationKeyframes(StringBuilder css, Ft100ExportScope scope)
    {
        css.AppendLine();
        css.AppendLine($"@keyframes {scope.AnimationName("scada-blink")} {{ 0%,100%{{opacity:1}} 50%{{opacity:0.15}} }}");
        css.AppendLine($"@keyframes {scope.AnimationName("scada-pulse")} {{ 0%,100%{{transform:scale(1)}} 50%{{transform:scale(1.05)}} }}");
        css.AppendLine($"@keyframes {scope.AnimationName("scada-halo")} {{ 0%,100%{{box-shadow:0 0 2px currentColor}} 50%{{box-shadow:0 0 14px currentColor}} }}");
        css.AppendLine($"@keyframes {scope.AnimationName("scada-spin")} {{ 0%{{transform:rotate(0deg)}} 100%{{transform:rotate(360deg)}} }}");
        css.AppendLine();
        css.AppendLine($".scada-anim-blink {{ animation: {scope.AnimationName("scada-blink")} 0.6s step-end infinite; }}");
        css.AppendLine($".scada-anim-pulse {{ animation: {scope.AnimationName("scada-pulse")} 1s ease-in-out infinite; }}");
        css.AppendLine($".scada-anim-halo  {{ animation: {scope.AnimationName("scada-halo")} 1.8s ease-in-out infinite; }}");
        css.AppendLine($".scada-anim-spin  {{ animation: {scope.AnimationName("scada-spin")} 1.2s linear infinite; }}");
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

    private static string BuildManifest(
        ScadaScene scene,
        ScadaProject? project,
        List<string> warnings,
        ScadaRuntimeCapabilityAnalysis runtimeCapabilities,
        string runtimeSha256,
        Ft100ManifestProfile manifestProfile)
    {
        var tagCatalog = project?.TagCatalog;
        var homePageId = project?.EffectiveHomePageId;
        var manifest = new
        {
            Name = scene.Title,
            ManifestVersion = ManifestVersion(manifestProfile),
            HomePageId = homePageId,
            Pages = new[] { BuildManifestPage(scene, homePageId, projectRelativePath: false, tagCatalog, warnings) },
            Actions = scene.ActionDefinitions.Select(BuildRuntimeAction).ToArray(),
            Tags = project?.TagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>()
        };

        return SerializeManifest(manifest, BuildRuntimeContract(runtimeCapabilities, runtimeSha256, manifestProfile));
    }

    private static string BuildProjectManifest(
        ScadaProject project,
        IReadOnlyList<ScadaScene> scenes,
        List<string> warnings,
        ScadaRuntimeCapabilityAnalysis runtimeCapabilities,
        string runtimeSha256,
        Ft100ManifestProfile manifestProfile)
    {
        var tagCatalog = project.TagCatalog;
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
            .Select(BuildRuntimeAction)
            .ToArray();
        var manifest = new
        {
            Name = project.Name,
            ManifestVersion = ManifestVersion(manifestProfile),
            HomePageId = homePageId,
            Pages = exportedScenes
                .Select(scene => BuildManifestPage(scene, homePageId, projectRelativePath: true, tagCatalog, warnings))
                .ToArray(),
            Actions = actions,
            Tags = project.TagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>()
        };

        return SerializeManifest(manifest, BuildRuntimeContract(runtimeCapabilities, runtimeSha256, manifestProfile));
    }

    private static string SerializeManifest(object manifest, Ft100RuntimeContractManifest? runtimeContract)
    {
        var root = JsonSerializer.SerializeToNode(manifest, ManifestJsonOptions)?.AsObject()
            ?? throw new InvalidOperationException("FT100 manifest serialization returned no JSON object.");
        if (runtimeContract is not null)
        {
            root["RuntimeContract"] = JsonSerializer.SerializeToNode(runtimeContract, ManifestJsonOptions);
        }
        return root.ToJsonString(ManifestJsonOptions);
    }

    private static Ft100RuntimeContractManifest? BuildRuntimeContract(
        ScadaRuntimeCapabilityAnalysis analysis,
        string runtimeSha256,
        Ft100ManifestProfile manifestProfile) =>
        manifestProfile == Ft100ManifestProfile.Strict23
            ? new Ft100RuntimeContractManifest(
                ScadaRuntimeCapabilityCatalog.ContractVersion,
                analysis.RequiredCapabilities.Select(capability => capability.Id).ToArray(),
                runtimeSha256)
            : null;

    private static string ManifestVersion(Ft100ManifestProfile manifestProfile) => manifestProfile switch
    {
        Ft100ManifestProfile.Strict23 => "2.3",
        Ft100ManifestProfile.Compatibility22 => "2.2",
        Ft100ManifestProfile.Compatibility21 => "2.1",
        _ => throw new ArgumentOutOfRangeException(nameof(manifestProfile), manifestProfile, "Unknown FT100 manifest profile.")
    };

    private static void EnsureRuntimeCapabilitiesExportable(
        ScadaRuntimeCapabilityAnalysis analysis,
        Ft100ManifestProfile manifestProfile)
    {
        if (manifestProfile != Ft100ManifestProfile.Strict23 || analysis.BlockedCapabilities.Count == 0)
        {
            return;
        }

        var blocked = string.Join(", ", analysis.BlockedCapabilities.Select(capability => capability.Id));
        throw new InvalidOperationException($"Strict manifest 2.3 export is blocked by unsupported runtime capabilities: {blocked}.");
    }

    private static object BuildManifestPage(ScadaScene scene, string? homePageId, bool projectRelativePath,
        ScadaTagCatalog? tagCatalog, List<string> warnings)
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
                    ButtonKind = element.Kind == ScadaElementKind.Button ? element.EffectiveButtonKind.ToString() : null,
                    ButtonBehavior = element.Kind == ScadaElementKind.Button ? element.EffectiveButtonBehavior : null,
                    Data = BuildManifestElementData(element),
                    Events = Array.Empty<ScadaObjectEventBinding>(),
                    ValueBindings = new
                    {
                        ReadTagId = element.Data?.ReadTagId,
                        WriteTagId = element.Data?.WriteTagId
                    },
                    TableCellBindings = Ft100TableCellBindingManifestBuilder.Build(element, tagCatalog, warnings),
                    StateConfig = element.EffectiveStateConfig.States.Count > 0
                        || element.EffectiveStateConfig.ReadVariable is not null
                        || HasNonDefaultFallback(element.EffectiveStateConfig)
                        ? NormalizeStateConfigForExport(element.EffectiveStateConfig, tagCatalog, warnings) : null,
                    CommandConfig = element.EffectiveCommandConfig.Commands.Count > 0
                        ? BuildRuntimeCommandConfig(element.EffectiveCommandConfig) : null
                })
                .ToArray()
        };
    }

    private static object BuildRuntimeAction(ScadaActionDefinition action)
    {
        return new
        {
            action.Id,
            action.Kind,
            action.TargetPageId,
            action.TargetElementId,
            action.ClassName,
            action.TagId,
            action.Value,
            action.Condition,
            action.PopupOptions,
            action.ConditionGroup
        };
    }

    private static object BuildRuntimeCommandConfig(ScadaElementCommandConfig config)
    {
        return new
        {
            Commands = config.Commands.Select(command => new
            {
                command.Id,
                command.Name,
                command.Enabled,
                command.Trigger,
                command.Kind,
                command.Confirmation,
                command.WriteTagId,
                command.ReadTagId,
                command.WriteMode,
                command.OnValue,
                command.OffValue,
                command.FixedValue,
                command.TargetPageId,
                command.Url,
                command.NewTab
            }).ToArray()
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
        return element.Kind != ScadaElementKind.Group || GroupRequiresRuntimeWrapper(element);
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
                if (GroupRequiresRuntimeWrapper(element))
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

    private static void AppendStructuredStyle(StringBuilder css, ScadaElementStyle style, string indent, bool inline)
    {
        var separator = inline ? string.Empty : Environment.NewLine;
        void Append(string declaration) => css.Append(inline ? declaration : $"{indent}{declaration}{separator}");

        Append($"font-weight:{NormalizeCssToken(style.FontWeight, "normal")};");
        Append($"font-style:{NormalizeCssToken(style.FontStyle, "normal")};");
        if (style.TextDecoration is { Count: > 0 })
        {
            var decorations = string.Join(' ', style.TextDecoration
                .Select(value => NormalizeCssToken(value, string.Empty))
                .Where(value => value.Length > 0));
            if (decorations.Length > 0)
            {
                Append($"text-decoration:{decorations};");
            }
        }
        Append($"text-align:{NormalizeCssToken(style.TextAlign, "left")};");
        Append($"text-transform:{NormalizeCssToken(style.TextTransform, "none")};");
        Append($"letter-spacing:{Format(style.LetterSpacing)}px;");
        Append($"line-height:{(style.LineHeight > 0 ? $"{Format(style.LineHeight)}px" : "normal")};");
        var radius = (style.BorderRadius ?? ScadaBorderRadius.None).Normalized();
        if (!radius.IsUniform || radius.TopLeft > 0)
        {
            Append($"border-radius:{Format(radius.TopLeft)}px {Format(radius.TopRight)}px {Format(radius.BottomRight)}px {Format(radius.BottomLeft)}px;");
        }
        if (!inline)
        {
            Append($"opacity:{Format(Math.Clamp(style.Opacity, 0, 1))};");
            Append("transform-origin:center center;");
            var scaleX = style.FlipHorizontally ? -1 : 1;
            var scaleY = style.FlipVertically ? -1 : 1;
            Append($"transform:rotate({Format(style.Rotation)}deg) scaleX({scaleX}) scaleY({scaleY});");
        }
    }

    private static string NormalizeCssToken(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim() switch
        {
            "LineThrough" => "line-through",
            _ => value.Trim().ToLowerInvariant()
        };
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

        /// <summary>
        /// Selects a legacy shape by its original numeric <c>data-id</c>, scoped to the
        /// source/legacy overlay layers only. Custom (library .sep) components preserve the
        /// same <c>data-id</c> on their own copies of converted legacy shapes (see ScopeSvgIds,
        /// which only rewrites "id" attributes and url(#...) references, not "data-id"), so an
        /// unscoped "[data-id=...]" selector would also match — and, for the suppression rule,
        /// incorrectly hide — the shape's live copy inside .ft100-elementplus-layer.
        /// </summary>
        public string SourceDataIdSelector(string sourceElementId)
        {
            var idSelector = $"[data-id=\"{CssEscape(sourceElementId)}\"]";
            return $"{RootSelector} .ft100-source-layer {idSelector}, {RootSelector} .ft100-legacy-layer {idSelector}";
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
            return $"{RootDomId}---{CssIdentifier(name)}";
        }
    }

    private static string CssIdentifier(string value)
    {
        return string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));
    }

    /// <summary>
    /// Rewrites <c>id="..."</c> attributes and <c>url(#...)</c> references inside SVG markup
    /// so that each element instance gets unique DOM ids. Library .sep components share the
    /// same internal part ids (e.g. <c>Element001</c>); without scoping, placing the same
    /// component twice on a page produces duplicate DOM ids.
    /// </summary>
    private static string ScopeSvgIds(string svgMarkup, Ft100ExportScope scope, string elementId)
    {
        if (string.IsNullOrWhiteSpace(svgMarkup))
            return svgMarkup;

        // Elements share library-instance ids (e.g. "Element001"); the page root alone doesn't
        // disambiguate two instances on the same page, so the element id must be part of the
        // prefix too. Using scope.ElementDomId keeps the required "{rootDomId}__" prefix intact.
        var prefix = $"{scope.ElementDomId(elementId)}__svg-";

        // Rewrite id="X" → id="{rootDomId}__{elementId}__svg-X"
        var scoped = SvgIdAttributeRegex().Replace(svgMarkup, match =>
        {
            var originalId = match.Groups["value"].Value;
            if (originalId.Contains("__", StringComparison.Ordinal))
                return match.Value;

            return $"{match.Groups["prefix"].Value}{match.Groups["quote"].Value}{prefix}{originalId}{match.Groups["quote"].Value}";
        });

        // Rewrite url(#X) → url(#{rootDomId}__{elementId}__svg-X)
        scoped = SvgUrlRefRegex().Replace(scoped, match =>
        {
            var refId = match.Groups["ref"].Value;
            if (refId.Contains("__", StringComparison.Ordinal))
                return match.Value;

            return $"url(#{match.Groups["prefix"].Value}{prefix}{refId}{match.Groups["suffix"].Value}";
        });

        return scoped;
    }

    private static string CssEscape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    /// <summary>
    /// Forces <c>preserveAspectRatio="none"</c> on every root-level &lt;svg&gt; tag in a Custom
    /// (library .sep component) element's markup, mirroring the WebView preview's runtime
    /// normalization (see MainWindow.WebViewScript.cs, Custom element handling) so an instance
    /// resized on canvas stretches to fill its Bounds instead of being letterboxed/cropped by
    /// the browser's default "xMidYMid meet" aspect-ratio preservation when the component's
    /// native viewBox doesn't match the placed Bounds.
    /// </summary>
    private static string ForceCustomSvgAspectRatio(string svgMarkup)
    {
        if (string.IsNullOrWhiteSpace(svgMarkup))
            return svgMarkup;

        const string attribute = "preserveAspectRatio=\"none\"";
        return SvgOpenTagRegex().Replace(svgMarkup, match =>
        {
            var tag = match.Value;
            return PreserveAspectRatioAttributeRegex().IsMatch(tag)
                ? PreserveAspectRatioAttributeRegex().Replace(tag, attribute)
                : tag[..^1] + " " + attribute + ">";
        });
    }

    [GeneratedRegex("""<svg\b[^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex SvgOpenTagRegex();

    [GeneratedRegex("""preserveAspectRatio\s*=\s*["'][^"']*["']""", RegexOptions.IgnoreCase)]
    private static partial Regex PreserveAspectRatioAttributeRegex();

    [GeneratedRegex("""(?<prefix>(?<![\w:-])id\s*=\s*)(?<quote>["'])(?<value>[^"']+)\k<quote>""", RegexOptions.IgnoreCase)]
    private static partial Regex SvgIdAttributeRegex();

    [GeneratedRegex("""url\(\s*#(?<prefix>\s*)(?<ref>[^)\s]+?)(?<suffix>\s*)\)""", RegexOptions.IgnoreCase)]
    private static partial Regex SvgUrlRefRegex();

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
