using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ScadaBuilderV2.Application.ElementStudio;

namespace ScadaBuilderV2.Infrastructure.ElementStudio;

public sealed partial class ElementStudioImportPackageWriter : IElementStudioImportPackageWriter
{
    public const string FileExtension = ".ft1";

    public async Task<string> WriteToProjectAsync(
        ElementStudioImportPackage package,
        string projectsRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectsRoot);

        var path = GetDefaultPackagePath(projectsRoot, package);
        return await WriteToPathAsync(package, path, cancellationToken);
    }

    public async Task<string> WriteToPathAsync(
        ElementStudioImportPackage package,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        EnsureFt1Path(packagePath);

        var fullPath = Path.GetFullPath(packagePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var packageToWrite = CopyAndRewriteLocalImageAssets(package, fullPath);

        await using var write = File.Create(fullPath);
        await JsonSerializer.SerializeAsync(write, packageToWrite, CreateJsonSerializerOptions(), cancellationToken);
        return fullPath;
    }

    public static string GetDefaultPackagePath(string projectsRoot, ElementStudioImportPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectsRoot);

        var packageFileName = $"{ToSafeFileName(package.PackageId)}{FileExtension}";
        return Path.Combine(
            projectsRoot,
            package.SourceProjectId,
            ".studio",
            "imports",
            packageFileName);
    }

    public static JsonSerializerOptions CreateJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    private static void EnsureFt1Path(string packagePath)
    {
        var extension = Path.GetExtension(packagePath);
        if (!string.Equals(extension, FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Studio Element+ import packages must use the .ft1 extension.", nameof(packagePath));
        }
    }

    private static ElementStudioImportPackage CopyAndRewriteLocalImageAssets(
        ElementStudioImportPackage package,
        string packagePath)
    {
        var packageDirectory = Path.GetDirectoryName(packagePath);
        if (string.IsNullOrWhiteSpace(packageDirectory))
        {
            return package;
        }

        var assetsDirectory = Path.Combine(packageDirectory, "assets");
        var rewrittenItems = package.Items
            .Select(item => item with
            {
                LegacyMarkup = RewriteImageSources(item.LegacyMarkup, package, packageDirectory, assetsDirectory)
            })
            .ToArray();

        return package with { Items = rewrittenItems };
    }

    private static string? RewriteImageSources(
        string? markup,
        ElementStudioImportPackage package,
        string packageDirectory,
        string assetsDirectory)
    {
        if (string.IsNullOrWhiteSpace(markup))
        {
            return markup;
        }

        return SrcAttributeRegex().Replace(markup, match =>
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

            var sourcePath = ResolveImageSource(value, package, packageDirectory);
            if (sourcePath is null)
            {
                return match.Value;
            }

            Directory.CreateDirectory(assetsDirectory);
            var fileName = Path.GetFileName(sourcePath);
            var targetPath = Path.Combine(assetsDirectory, fileName);
            if (!File.Exists(targetPath))
            {
                File.Copy(sourcePath, targetPath);
            }

            return $"src={quote}assets/{fileName}{quote}";
        });
    }

    private static string? ResolveImageSource(
        string src,
        ElementStudioImportPackage package,
        string packageDirectory)
    {
        var normalized = src.Replace('/', Path.DirectorySeparatorChar);
        var sourceFileName = Path.GetFileName(normalized);
        var candidates = new List<string>();

        AddRelativeCandidate(candidates, packageDirectory, normalized);

        var projectRoot = Directory.GetParent(packageDirectory)?.Parent?.FullName;
        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            AddRelativeCandidate(candidates, projectRoot, normalized);
            AddRelativeCandidate(candidates, Path.Combine(projectRoot, "exports", package.SourceSceneId), normalized.Replace($"assets{Path.DirectorySeparatorChar}", $"images{Path.DirectorySeparatorChar}"));
            AddRelativeCandidate(candidates, Path.Combine(projectRoot, "exports", package.SourceSceneId, "images"), sourceFileName);
        }

        if (!string.IsNullOrWhiteSpace(package.SourcePagePath))
        {
            var sourceDirectory = Path.GetDirectoryName(package.SourcePagePath);
            if (!string.IsNullOrWhiteSpace(sourceDirectory))
            {
                AddRelativeCandidate(candidates, sourceDirectory, normalized);
                AddRelativeCandidate(candidates, Path.Combine(sourceDirectory, "assets"), sourceFileName);
                AddRelativeCandidate(candidates, Path.Combine(sourceDirectory, "..", "assets"), sourceFileName);
                AddRelativeCandidate(candidates, Path.Combine(sourceDirectory, "..", "dist", "assets"), sourceFileName);
                AddRelativeCandidate(candidates, Path.Combine(sourceDirectory, "..", "html_pages", "assets"), sourceFileName);
                AddRelativeCandidate(candidates, Path.Combine(sourceDirectory, "..", "08_web_modernized", "html_pages", "assets"), sourceFileName);
                AddRelativeCandidate(candidates, Path.Combine(sourceDirectory, "..", "dist", "assets", "html_pages", "assets"), sourceFileName);
            }
        }

        return candidates
            .Select(path => Path.GetFullPath(path))
            .FirstOrDefault(File.Exists);
    }

    private static void AddRelativeCandidate(List<string> candidates, string root, string relativePath)
    {
        if (!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(relativePath))
        {
            candidates.Add(Path.Combine(root, relativePath));
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

    [GeneratedRegex("""src=(?<quote>["'])(?<value>[^"']+)\k<quote>""", RegexOptions.IgnoreCase)]
    private static partial Regex SrcAttributeRegex();
}
