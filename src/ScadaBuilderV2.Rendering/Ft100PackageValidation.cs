using System.Text.Json;
using System.Text.RegularExpressions;

namespace ScadaBuilderV2.Rendering;

/// <summary>
/// Severity for FT100 package compatibility validation.
/// </summary>
public enum Ft100PackageValidationSeverity
{
    /// <summary>
    /// The issue does not block TF100Web intake, but should be reported to the operator.
    /// </summary>
    Warning,

    /// <summary>
    /// The issue blocks creation of a FT100-compatible .sb2 package.
    /// </summary>
    Error
}

/// <summary>
/// One compatibility issue found while validating the exported FT100 package shape.
/// </summary>
/// <param name="Severity">Whether the issue blocks archive creation.</param>
/// <param name="Code">Stable machine-readable issue code.</param>
/// <param name="Message">Human-readable issue detail.</param>
/// <param name="PageId">Optional page id associated with the issue.</param>
public sealed record Ft100PackageValidationIssue(
    Ft100PackageValidationSeverity Severity,
    string Code,
    string Message,
    string? PageId = null);

/// <summary>
/// Result of validating an exported package against the current TF100Web intake contract.
/// </summary>
/// <param name="Issues">All blocking and non-blocking validation findings.</param>
public sealed record Ft100PackageValidationResult(IReadOnlyList<Ft100PackageValidationIssue> Issues)
{
    /// <summary>
    /// True when no blocking validation issue was found.
    /// </summary>
    public bool IsValid => Issues.All(issue => issue.Severity != Ft100PackageValidationSeverity.Error);

    /// <summary>
    /// Blocking validation issues.
    /// </summary>
    public IReadOnlyList<Ft100PackageValidationIssue> Errors =>
        Issues.Where(issue => issue.Severity == Ft100PackageValidationSeverity.Error).ToArray();

    /// <summary>
    /// Non-blocking validation warnings.
    /// </summary>
    public IReadOnlyList<Ft100PackageValidationIssue> Warnings =>
        Issues.Where(issue => issue.Severity == Ft100PackageValidationSeverity.Warning).ToArray();
}

/// <summary>
/// Validates SCADA Builder V2 FT100 packages against the audited TF100Web fragment intake contract.
/// </summary>
/// <remarks>
/// Decisions: DEC-0003, DEC-0007, DEC-0026, DEC-0027.
/// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md.
/// Reference intake: F:\Projet\Git\TF100Web frontend/scada_package.py at commit 7d57600.
/// </remarks>
public static partial class Ft100PackageValidator
{
    private static readonly StringComparer PageIdComparer = StringComparer.Ordinal;

    /// <summary>
    /// Validates a directory that must contain the exported scada-builder-v2-ft100-package root.
    /// </summary>
    /// <param name="packageDirectory">Path to the scada-builder-v2-ft100-package directory.</param>
    /// <returns>Validation result with blocking errors and non-blocking warnings.</returns>
    public static Ft100PackageValidationResult ValidatePackageDirectory(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);

        var issues = new List<Ft100PackageValidationIssue>();
        var fullPackageDirectory = Path.GetFullPath(packageDirectory);
        if (!Directory.Exists(fullPackageDirectory))
        {
            issues.Add(Error("missing-package-directory", $"Package directory not found: {fullPackageDirectory}"));
            return new Ft100PackageValidationResult(issues);
        }

        if (!string.Equals(Path.GetFileName(fullPackageDirectory), Ft100SceneExporter.ProjectPackageDirectoryName, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error(
                "wrong-package-directory-name",
                $"Package directory must be named {Ft100SceneExporter.ProjectPackageDirectoryName}."));
        }

        var manifestPath = Path.Combine(fullPackageDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            issues.Add(Error("missing-root-manifest", "Root manifest.json is required."));
            return new Ft100PackageValidationResult(issues);
        }

        var manifest = ReadManifest(manifestPath, issues);
        if (manifest is null)
        {
            return new Ft100PackageValidationResult(issues);
        }

        using (manifest)
        {
            if (manifest.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Error("root-manifest-not-object", "Root manifest.json must contain a JSON object."));
                return new Ft100PackageValidationResult(issues);
            }

            var pages = ReadCompiledPages(manifest.RootElement, issues);
            if (pages.Count == 0)
            {
                issues.Add(Error("no-compiled-pages", "Root manifest must contain at least one compiled page."));
                return new Ft100PackageValidationResult(issues);
            }

            var pagesById = pages
                .GroupBy(page => page.Id, PageIdComparer)
                .ToDictionary(group => group.Key, group => group.First(), PageIdComparer);

            ValidateHomePage(manifest.RootElement, pagesById, issues);
            foreach (var page in pages)
            {
                ValidatePage(fullPackageDirectory, page, pagesById, issues);
            }
        }

        return new Ft100PackageValidationResult(issues);
    }

    private static JsonDocument? ReadManifest(string manifestPath, List<Ft100PackageValidationIssue> issues)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(manifestPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            issues.Add(Error("invalid-root-manifest", $"Root manifest.json cannot be read: {ex.Message}"));
            return null;
        }
    }

    private static List<ValidatedPage> ReadCompiledPages(JsonElement root, List<Ft100PackageValidationIssue> issues)
    {
        if (!TryGetProperty(root, "Pages", out var pagesElement) || pagesElement.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error("missing-pages", "Root manifest must contain a Pages array."));
            return [];
        }

        var pages = new List<ValidatedPage>();
        foreach (var pageElement in pagesElement.EnumerateArray())
        {
            if (pageElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var includeInBuild = !TryGetProperty(pageElement, "IncludeInBuild", out var includeElement)
                || includeElement.ValueKind != JsonValueKind.False;
            if (!includeInBuild)
            {
                continue;
            }

            var id = ReadString(pageElement, "Id");
            if (string.IsNullOrWhiteSpace(id))
            {
                issues.Add(Error("missing-page-id", "Compiled page entry must contain a non-empty Id."));
                continue;
            }

            pages.Add(new ValidatedPage(
                id,
                ReadString(pageElement, "Type", "PageType").ToLowerInvariant(),
                ReadString(pageElement, "RelativePath"),
                ReadString(pageElement, "HeaderPageId"),
                ReadString(pageElement, "FooterPageId")));
        }

        foreach (var duplicateId in pages.GroupBy(page => page.Id, PageIdComparer).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            issues.Add(Error("duplicate-page-id", $"Compiled page id '{duplicateId}' appears more than once.", duplicateId));
        }

        return pages;
    }

    private static void ValidateHomePage(
        JsonElement root,
        Dictionary<string, ValidatedPage> pagesById,
        List<Ft100PackageValidationIssue> issues)
    {
        var homePageId = ReadString(root, "HomePageId");
        if (!string.IsNullOrWhiteSpace(homePageId) && !pagesById.ContainsKey(homePageId))
        {
            issues.Add(Warning("home-page-missing", $"HomePageId '{homePageId}' is not part of compiled pages.", homePageId));
        }

        if (!pagesById.Values.Any(page => string.Equals(page.Type, "default", StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(Warning("no-default-page", "No compiled default page was found."));
        }
    }

    private static void ValidatePage(
        string packageDirectory,
        ValidatedPage page,
        Dictionary<string, ValidatedPage> pagesById,
        List<Ft100PackageValidationIssue> issues)
    {
        var relativePath = string.IsNullOrWhiteSpace(page.RelativePath)
            ? $"{page.Id}/{page.Id}.html"
            : page.RelativePath;
        var htmlPath = SafeJoin(packageDirectory, relativePath);
        if (htmlPath is null)
        {
            issues.Add(Error("unsafe-page-path", $"Page '{page.Id}' has an unsafe RelativePath.", page.Id));
            return;
        }

        if (!File.Exists(htmlPath))
        {
            issues.Add(Error("missing-html", $"Page '{page.Id}' HTML file is missing.", page.Id));
            return;
        }

        var html = "";
        try
        {
            html = File.ReadAllText(htmlPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(Error("unreadable-html", $"Page '{page.Id}' HTML cannot be read: {ex.Message}", page.Id));
        }

        if (!string.IsNullOrEmpty(html))
        {
            ValidatePageHtml(page, html, issues);
        }

        var cssRelativePath = Path.Combine(
            Path.GetDirectoryName(relativePath.Replace('\\', Path.DirectorySeparatorChar)) ?? "",
            "css",
            $"{page.Id}.css");
        var cssPath = SafeJoin(packageDirectory, cssRelativePath);
        if (cssPath is null)
        {
            issues.Add(Error("unsafe-css-path", $"Page '{page.Id}' CSS path is unsafe.", page.Id));
        }
        else if (!File.Exists(cssPath))
        {
            issues.Add(Warning("missing-css", $"Page '{page.Id}' CSS file is missing.", page.Id));
        }
        else
        {
            ValidatePageCss(page, cssPath, issues);
        }

        ValidatePageReference(page, "HeaderPageId", page.HeaderPageId, "header", pagesById, issues);
        ValidatePageReference(page, "FooterPageId", page.FooterPageId, "footer", pagesById, issues);
    }

    private static void ValidatePageHtml(ValidatedPage page, string html, List<Ft100PackageValidationIssue> issues)
    {
        var rootId = $"ft100-{page.Id}";
        if (!HtmlIdRegex().Matches(html).Cast<Match>().Any(match => string.Equals(match.Groups["id"].Value, rootId, StringComparison.Ordinal)))
        {
            issues.Add(Error("missing-root", $"Page '{page.Id}' must contain root div id '{rootId}'.", page.Id));
        }

        var ids = HtmlIdRegex()
            .Matches(html)
            .Cast<Match>()
            .Select(match => match.Groups["id"].Value)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();
        foreach (var duplicate in ids.GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            issues.Add(Error("duplicate-dom-id", $"Page '{page.Id}' contains duplicate DOM id '{duplicate}'.", page.Id));
        }

        foreach (var id in ids.Where(id => !string.Equals(id, rootId, StringComparison.Ordinal) && !id.StartsWith($"{rootId}__", StringComparison.Ordinal)))
        {
            issues.Add(Error("unscoped-dom-id", $"Page '{page.Id}' contains non page-scoped DOM id '{id}'.", page.Id));
        }
    }

    private static void ValidatePageCss(ValidatedPage page, string cssPath, List<Ft100PackageValidationIssue> issues)
    {
        string css;
        try
        {
            css = File.ReadAllText(cssPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(Error("unreadable-css", $"Page '{page.Id}' CSS cannot be read: {ex.Message}", page.Id));
            return;
        }

        var rootSelector = $"#ft100-{page.Id}";
        AddCssErrorIf(CssRootSelectorRegex().IsMatch(css), "global-root-selector", "CSS must not emit package-global :root selectors.");
        AddCssErrorIf(CssHtmlBodySelectorRegex().IsMatch(css), "global-html-body-selector", "CSS must not emit package-global html/body selectors.");
        AddCssErrorIf(CssDataIdSelectorRegex().IsMatch(css), "global-data-id-selector", "CSS data-id selectors must be scoped to the page root.");
        AddCssErrorIf(CssFt100ClassSelectorRegex().IsMatch(css), "global-ft100-class-selector", "CSS ft100 class selectors must be scoped to the page root.");
        AddCssErrorIf(CssRawIdSelectorRegex().Matches(css).Cast<Match>().Any(match => !IsPageScopedIdSelector(match.Value, rootSelector)), "global-id-selector", "CSS id selectors must use the page root namespace.");

        void AddCssErrorIf(bool condition, string code, string message)
        {
            if (condition)
            {
                issues.Add(Error(code, $"Page '{page.Id}': {message}", page.Id));
            }
        }
    }

    // CSS authors and formatters may indent selectors; validation only cares about the emitted id token.
    private static bool IsPageScopedIdSelector(string selector, string rootSelector)
    {
        var normalizedSelector = selector.TrimStart();
        return string.Equals(normalizedSelector, rootSelector, StringComparison.Ordinal)
            || normalizedSelector.StartsWith($"{rootSelector}__", StringComparison.Ordinal);
    }

    private static void ValidatePageReference(
        ValidatedPage page,
        string fieldName,
        string referencedPageId,
        string expectedType,
        Dictionary<string, ValidatedPage> pagesById,
        List<Ft100PackageValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(referencedPageId))
        {
            return;
        }

        if (!pagesById.TryGetValue(referencedPageId, out var referencedPage))
        {
            issues.Add(Error($"missing-{fieldName.ToLowerInvariant()}", $"Page '{page.Id}' references missing {fieldName} '{referencedPageId}'.", page.Id));
            return;
        }

        if (!string.Equals(referencedPage.Type, expectedType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error($"wrong-{fieldName.ToLowerInvariant()}-type", $"Page '{page.Id}' references {fieldName} '{referencedPageId}' with type '{referencedPage.Type}', expected '{expectedType}'.", page.Id));
        }
    }

    private static string? SafeJoin(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var normalized = relativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized) || normalized.Split(Path.DirectorySeparatorChar).Any(part => part == ".."))
        {
            return null;
        }

        var rootFullPath = Path.GetFullPath(root);
        var candidate = Path.GetFullPath(Path.Combine(rootFullPath, normalized));
        return candidate.StartsWith(rootFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? candidate
            : null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString()?.Trim() ?? "";
            }
        }

        return "";
    }

    private static Ft100PackageValidationIssue Error(string code, string message, string? pageId = null) =>
        new(Ft100PackageValidationSeverity.Error, code, message, pageId);

    private static Ft100PackageValidationIssue Warning(string code, string message, string? pageId = null) =>
        new(Ft100PackageValidationSeverity.Warning, code, message, pageId);

    private sealed record ValidatedPage(
        string Id,
        string Type,
        string RelativePath,
        string HeaderPageId,
        string FooterPageId);

    [GeneratedRegex("""(?<![\w:-])id\s*=\s*["'](?<id>[^"']+)["']""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlIdRegex();

    [GeneratedRegex(@"(?m)^\s*:root\b", RegexOptions.CultureInvariant)]
    private static partial Regex CssRootSelectorRegex();

    [GeneratedRegex(@"(?m)^\s*(html|body)\b", RegexOptions.CultureInvariant)]
    private static partial Regex CssHtmlBodySelectorRegex();

    [GeneratedRegex(@"(?m)^\s*\[data-id=", RegexOptions.CultureInvariant)]
    private static partial Regex CssDataIdSelectorRegex();

    [GeneratedRegex(@"(?m)^\s*\.ft100-", RegexOptions.CultureInvariant)]
    private static partial Regex CssFt100ClassSelectorRegex();

    [GeneratedRegex(@"(?m)^\s*#[A-Za-z_][A-Za-z0-9_-]*", RegexOptions.CultureInvariant)]
    private static partial Regex CssRawIdSelectorRegex();
}
