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
/// Decisions: DEC-0003, DEC-0007, DEC-0026, DEC-0027, DEC-0042.
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

            ValidateNoInternalPageKeys(manifest.RootElement, issues);
            var manifestVersion = ValidateManifestVersion(manifest.RootElement, issues);
            var tags = ReadTags(manifest.RootElement);

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
            ValidateRuntimePageTargets(manifest.RootElement, pagesById, issues);
            foreach (var page in pages)
            {
                ValidatePage(fullPackageDirectory, page, pagesById, manifestVersion, tags, issues);
            }
        }

        ValidateRuntimeJs(fullPackageDirectory, issues);

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

    private static void ValidateNoInternalPageKeys(
        JsonElement element,
        List<Ft100PackageValidationIssue> issues)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.EndsWith("PageKey", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(Error(
                        "internal-page-key-exported",
                        $"Internal page identity field '{property.Name}' must not be present in an .sb2 manifest."));
                }

                ValidateNoInternalPageKeys(property.Value, issues);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ValidateNoInternalPageKeys(item, issues);
            }
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
                ReadString(pageElement, "FooterPageId"),
                pageElement));
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

    private static void ValidateRuntimePageTargets(
        JsonElement root,
        Dictionary<string, ValidatedPage> pagesById,
        List<Ft100PackageValidationIssue> issues)
    {
        if (TryGetProperty(root, "Actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
        {
            foreach (var action in actions.EnumerateArray())
            {
                ValidateRuntimeTarget(
                    action,
                    ReadString(action, "Id"),
                    null,
                    "action",
                    pagesById,
                    issues);
            }
        }

        if (!TryGetProperty(root, "Pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var page in pages.EnumerateArray())
        {
            var pageId = ReadString(page, "Id");
            if (!TryGetProperty(page, "Objects", out var objects) || objects.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var element in objects.EnumerateArray())
            {
                if (!TryGetProperty(element, "CommandConfig", out var config) || config.ValueKind != JsonValueKind.Object ||
                    !TryGetProperty(config, "Commands", out var commands) || commands.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var command in commands.EnumerateArray())
                {
                    ValidateRuntimeTarget(
                        command,
                        ReadString(command, "Id"),
                        pageId,
                        "command",
                        pagesById,
                        issues);
                }
            }
        }
    }

    private static void ValidateRuntimeTarget(
        JsonElement definition,
        string definitionId,
        string? sourcePageId,
        string definitionRole,
        Dictionary<string, ValidatedPage> pagesById,
        List<Ft100PackageValidationIssue> issues)
    {
        var kind = ReadString(definition, "Kind");
        var expectedType = kind.ToLowerInvariant() switch
        {
            "navigate" => "default",
            "mountfragment" or "openpopup" or "togglepopup" or "closepopup" => "fragment",
            _ => null
        };
        if (expectedType is null)
        {
            return;
        }

        var targetPageId = ReadString(definition, "TargetPageId");
        if (string.IsNullOrWhiteSpace(targetPageId) || !pagesById.TryGetValue(targetPageId, out var targetPage))
        {
            issues.Add(Error(
                $"{definitionRole}-target-page-missing",
                $"Runtime {definitionRole} '{definitionId}' references missing page '{targetPageId}'.",
                sourcePageId));
            return;
        }

        if (!string.Equals(targetPage.Type, expectedType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error(
                $"{definitionRole}-target-page-wrong-type",
                $"Runtime {definitionRole} '{definitionId}' targets page '{targetPageId}' of type '{targetPage.Type}', expected '{expectedType}'.",
                sourcePageId));
        }
    }

    private static void ValidatePage(
        string packageDirectory,
        ValidatedPage page,
        Dictionary<string, ValidatedPage> pagesById,
        string manifestVersion,
        IReadOnlyDictionary<string, ValidatedTag> tags,
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
            ValidateTableCellBindings(page, html, manifestVersion, tags, issues);
        }

        var cssDirRelativePath = Path.Combine(
            Path.GetDirectoryName(relativePath.Replace('\\', Path.DirectorySeparatorChar)) ?? "",
            "css");
        var cssDir = SafeJoin(packageDirectory, cssDirRelativePath);
        if (cssDir is null)
        {
            issues.Add(Error("unsafe-css-path", $"Page '{page.Id}' CSS path is unsafe.", page.Id));
        }
        else
        {
            // CSS filenames carry a content hash for cache-busting (e.g. "win00008.a1b2c3d4.css"),
            // so the plain "{page.Id}.css" name generally won't exist. Fall back to the hashed pattern.
            var cssPath = Path.Combine(cssDir, $"{page.Id}.css");
            if (!File.Exists(cssPath) && Directory.Exists(cssDir))
            {
                var hashedMatches = Directory.GetFiles(cssDir, $"{page.Id}.*.css");
                if (hashedMatches.Length == 1)
                {
                    cssPath = hashedMatches[0];
                }
                else if (hashedMatches.Length > 1)
                {
                    issues.Add(Error("multiple-css", $"Page '{page.Id}' has multiple CSS files matching the content-hash pattern.", page.Id));
                    cssPath = hashedMatches[0];
                }
            }

            if (!File.Exists(cssPath))
            {
                issues.Add(Warning("missing-css", $"Page '{page.Id}' CSS file is missing.", page.Id));
            }
            else
            {
                ValidatePageCss(page, cssPath, issues);
            }
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

    private static string ValidateManifestVersion(JsonElement root, ICollection<Ft100PackageValidationIssue> issues)
    {
        var version = ReadString(root, "ManifestVersion");
        if (string.IsNullOrWhiteSpace(version))
        {
            issues.Add(Warning("manifest-version-legacy", "ManifestVersion is absent; validating as legacy 2.1."));
            return "2.1";
        }

        if (version is not "2.1" and not "2.2")
        {
            issues.Add(Error("manifest-version-unsupported", $"ManifestVersion '{version}' is unsupported; expected 2.1 or 2.2."));
        }
        return version;
    }

    private static IReadOnlyDictionary<string, ValidatedTag> ReadTags(JsonElement root)
    {
        if (!TryGetProperty(root, "Tags", out var tagsElement) || tagsElement.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<string, ValidatedTag>(StringComparer.Ordinal);
        }

        return tagsElement.EnumerateArray()
            .Where(tag => tag.ValueKind == JsonValueKind.Object)
            .Select(tag => new ValidatedTag(
                ReadString(tag, "Id"),
                !TryGetProperty(tag, "Enabled", out var enabled) || enabled.ValueKind != JsonValueKind.False,
                TryGetProperty(tag, "Writeable", out var writeable) && writeable.ValueKind == JsonValueKind.True))
            .Where(tag => !string.IsNullOrWhiteSpace(tag.Id))
            .GroupBy(tag => tag.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    private static void ValidateTableCellBindings(
        ValidatedPage page,
        string html,
        string manifestVersion,
        IReadOnlyDictionary<string, ValidatedTag> tags,
        ICollection<Ft100PackageValidationIssue> issues)
    {
        if (!TryGetProperty(page.Manifest, "Objects", out var objects) || objects.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var targetIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in objects.EnumerateArray())
        {
            if (!TryGetProperty(element, "TableCellBindings", out var bindings) || bindings.ValueKind != JsonValueKind.Array || bindings.GetArrayLength() == 0)
            {
                continue;
            }

            var elementId = ReadString(element, "Id");
            var pathPrefix = $"Elements[{elementId}].Table.Cells";
            if (manifestVersion != "2.2")
            {
                issues.Add(Error("table-cell.version", $"Page '{page.Id}' uses TableCellBindings but ManifestVersion is '{manifestVersion}', expected 2.2.", page.Id));
            }
            if (!string.Equals(ReadString(element, "Kind"), "Table", StringComparison.Ordinal))
            {
                issues.Add(Error("table-cell.owner-kind", $"Element '{elementId}' owns TableCellBindings but is not Kind Table.", page.Id));
            }

            var wrapperId = $"ft100-{CssIdentifier(page.Id)}__{CssIdentifier(elementId)}";
            var wrapperTag = FindOpeningTag(html, wrapperId);
            if (wrapperTag.Success && HasRuntimeBindingAttributes(wrapperTag.Value))
            {
                issues.Add(Error("table-cell.wrapper-binding", $"Table wrapper '{elementId}' must not receive cell runtime binding attributes.", page.Id));
            }

            foreach (var binding in bindings.EnumerateArray())
            {
                var row = ReadInt(binding, "Row");
                var column = ReadInt(binding, "Column");
                var path = $"{pathPrefix}[{row},{column}]";
                var targetId = ReadString(binding, "TargetId");
                if (row < 0 || column < 0)
                {
                    AddCellError("table-cell.coordinates", "Row and Column must be non-negative.");
                }
                if (!string.Equals(ReadString(binding, "Kind"), "InputNumeric", StringComparison.Ordinal))
                {
                    AddCellError("table-cell.kind", "Kind must be InputNumeric.");
                }

                var expectedTarget = $"{CssIdentifier(elementId)}__cell-{row}-{column}";
                if (!string.Equals(targetId, expectedTarget, StringComparison.Ordinal) || targetId.StartsWith("ft100-", StringComparison.Ordinal))
                {
                    AddCellError("table-cell.target-id", $"TargetId must be the unscoped id '{expectedTarget}'.");
                }
                if (!targetIds.Add(targetId))
                {
                    AddCellError("table-cell.target-duplicate", $"TargetId '{targetId}' appears more than once on the page.");
                }

                var scopedTarget = $"ft100-{CssIdentifier(page.Id)}__{targetId}";
                var cellTag = FindOpeningTag(html, scopedTarget);
                if (!cellTag.Success || !string.Equals(cellTag.Groups["tag"].Value, "td", StringComparison.OrdinalIgnoreCase))
                {
                    AddCellError("table-cell.target-missing", $"The exact <td> target '{scopedTarget}' is missing.");
                }
                else
                {
                    if (!HasAttribute(cellTag.Value, "data-row", row.ToString(System.Globalization.CultureInfo.InvariantCulture)) ||
                        !HasAttribute(cellTag.Value, "data-column", column.ToString(System.Globalization.CultureInfo.InvariantCulture)) ||
                        !HasAttribute(cellTag.Value, "data-scada-table-cell-kind", "InputNumeric"))
                    {
                        AddCellError("table-cell.target-shape", "The target <td> does not match its row, column, and InputNumeric anchor metadata.");
                    }
                }
                var inputTag = FindOpeningTag(html, $"{scopedTarget}__input");
                if (!inputTag.Success || !string.Equals(inputTag.Groups["tag"].Value, "input", StringComparison.OrdinalIgnoreCase) ||
                    !HasAttribute(inputTag.Value, "type", "number"))
                {
                    AddCellError("table-cell.input-missing", "The target cell must contain its rendered <input type=\"number\">.");
                }

                ValidateBindingData(binding, path, page.Id, tags, issues);

                void AddCellError(string code, string message) =>
                    issues.Add(Error(code, $"{path}: {message}", page.Id));
            }
        }
    }

    private static void ValidateBindingData(
        JsonElement binding,
        string path,
        string pageId,
        IReadOnlyDictionary<string, ValidatedTag> tags,
        ICollection<Ft100PackageValidationIssue> issues)
    {
        if (!TryGetProperty(binding, "Data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            Add("table-cell.data", "Data object is required.");
            return;
        }
        if (!TryGetProperty(binding, "ValueBindings", out var values) || values.ValueKind != JsonValueKind.Object)
        {
            Add("table-cell.value-bindings", "ValueBindings object is required.");
            return;
        }

        var readTagId = ReadString(values, "ReadTagId");
        var writeTagId = ReadString(values, "WriteTagId");
        if (string.IsNullOrWhiteSpace(readTagId) && string.IsNullOrWhiteSpace(writeTagId))
        {
            Add("table-cell.value-bindings-empty", "At least one read or write tag is required.");
        }
        if (!string.IsNullOrWhiteSpace(readTagId) && (!tags.TryGetValue(readTagId, out var readTag) || !readTag.Enabled))
        {
            Add("table-cell.read-tag", $"Read tag '{readTagId}' is missing or disabled.");
        }
        if (!string.IsNullOrWhiteSpace(writeTagId))
        {
            if (!tags.TryGetValue(writeTagId, out var writeTag) || !writeTag.Enabled)
                Add("table-cell.write-tag", $"Write tag '{writeTagId}' is missing or disabled.");
            else if (!writeTag.Writeable)
                Add("table-cell.write-tag-readonly", $"Write tag '{writeTagId}' is not writeable.");
        }

        var readOnly = TryGetProperty(data, "IsReadOnly", out var readOnlyElement) && readOnlyElement.ValueKind == JsonValueKind.True;
        if (readOnly && !string.IsNullOrWhiteSpace(writeTagId))
        {
            Add("table-cell.readonly-write", "A read-only numeric cell cannot carry a write binding.");
        }
        var minimum = ReadDouble(data, "Min");
        var maximum = ReadDouble(data, "Max");
        var step = ReadDouble(data, "Step");
        if (minimum.HasValue && maximum.HasValue && minimum > maximum)
            Add("table-cell.range", "Min cannot exceed Max.");
        if (step.HasValue && (!double.IsFinite(step.Value) || step <= 0))
            Add("table-cell.step", "Step must be finite and greater than zero.");
        if (!IsSupportedDisplayFormat(ReadString(data, "DisplayFormat")))
            Add("table-cell.display-format", "DisplayFormat is not supported.");

        void Add(string code, string message) => issues.Add(Error(code, $"{path}: {message}", pageId));
    }

    private static Match FindOpeningTag(string html, string id) => new Regex(
        $"<(?<tag>[a-zA-Z][\\w:-]*)\\b(?=[^>]*\\bid\\s*=\\s*[\"']{Regex.Escape(id)}[\"'])[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1)).Match(html);

    private static bool HasAttribute(string tag, string name, string value) => Regex.IsMatch(
        tag,
        $"\\b{Regex.Escape(name)}\\s*=\\s*[\"']{Regex.Escape(value)}[\"']",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static bool HasRuntimeBindingAttributes(string tag) =>
        tag.Contains("data-scada-role", StringComparison.OrdinalIgnoreCase) ||
        tag.Contains("data-scada-mapping-id", StringComparison.OrdinalIgnoreCase) ||
        tag.Contains("data-scada-write-mapping-id", StringComparison.OrdinalIgnoreCase);

    private static int ReadInt(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) && value.TryGetInt32(out var result) ? result : -1;

    private static double? ReadDouble(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var result) ? result : null;

    private static bool IsSupportedDisplayFormat(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (value.StartsWith("fixed:", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(value[6..], out var decimals) && decimals >= 0;
        return value.Any(character => character == '#') && value.Count(character => character == '.') <= 1 && value.All(character => character is '#' or '.');
    }

    private static string CssIdentifier(string value) =>
        string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));

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

    private static void ValidateRuntimeJs(string packageDirectory, List<Ft100PackageValidationIssue> issues)
    {
        var runtimeJsFiles = Directory.GetFiles(packageDirectory, "scada-runtime.*.js");
        if (runtimeJsFiles.Length == 0)
        {
            issues.Add(Error("missing-runtime-js", "Missing scada-runtime.<hash>.js at package root."));
        }
        else if (runtimeJsFiles.Length > 1)
        {
            issues.Add(Error("multiple-runtime-js", "Multiple scada-runtime.<hash>.js files found. Exactly one is required."));
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
        string FooterPageId,
        JsonElement Manifest);

    private sealed record ValidatedTag(string Id, bool Enabled, bool Writeable);

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
