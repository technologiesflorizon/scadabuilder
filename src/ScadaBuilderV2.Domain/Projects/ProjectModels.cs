using ScadaBuilderV2.Domain.Versioning;
using System.Text.Json.Serialization;

namespace ScadaBuilderV2.Domain.Projects;

public sealed record CanvasSize(int Width, int Height)
{
    public static CanvasSize DefaultDesktop => new(1280, 873);
}

public enum ScadaPageType
{
    Default,
    Fragment,
    Header,
    Footer
}

public sealed record SceneBackgroundStyle(
    string Color,
    string? Image = null,
    string Size = "cover",
    string Repeat = "no-repeat",
    string Position = "center center",
    string Attachment = "scroll",
    string Origin = "padding-box",
    string Clip = "border-box",
    string BlendMode = "normal")
{
    public static SceneBackgroundStyle Default { get; } = new("#000000");

    public static SceneBackgroundStyle FromColor(string? color)
    {
        return new(string.IsNullOrWhiteSpace(color) ? Default.Color : color);
    }
}

public sealed record DevicePreset(
    string Id,
    string Name,
    DeviceClass DeviceClass,
    DeviceOrientation Orientation,
    CanvasSize Size,
    bool CanRotate);

public sealed record ScadaSceneReference(
    string Id,
    string Title,
    string RelativePath,
    ScadaPageType Type = ScadaPageType.Default,
    CanvasSize? CanvasSize = null,
    SceneBackgroundStyle? Background = null,
    bool IncludeInBuild = true,
    string? HeaderPageId = null,
    string? FooterPageId = null)
{
    public CanvasSize EffectiveCanvasSize => CanvasSize ?? global::ScadaBuilderV2.Domain.Projects.CanvasSize.DefaultDesktop;

    public SceneBackgroundStyle EffectiveBackground => Background ?? SceneBackgroundStyle.Default;
}

public sealed record ScadaProject(
    string Name,
    ScadaVersion Version,
    CanvasSize CanvasSize,
    ResponsiveMode ResponsiveMode,
    AuthoringMode AuthoringMode,
    IReadOnlyList<DevicePreset> DevicePresets,
    IReadOnlyList<ScadaSceneReference> Scenes,
    string ManifestVersion = "2.0",
    string? HomePageId = null,
    ScadaTagCatalog? TagCatalog = null)
{
    [JsonIgnore]
    public IReadOnlyList<ScadaSceneReference> Pages => Scenes;

    [JsonIgnore]
    public string? EffectiveHomePageId => ResolveHomePageId(Scenes, HomePageId);

    public static ScadaProject CreateDefault(string name)
    {
        return new ScadaProject(
            name,
            ScadaVersion.Initial,
            CanvasSize.DefaultDesktop,
            ResponsiveMode.Fixed,
            AuthoringMode.DesktopFirst,
            DefaultDevicePresets.All,
            Array.Empty<ScadaSceneReference>());
    }

    public static string? ResolveHomePageId(IReadOnlyList<ScadaSceneReference> scenes, string? configuredHomePageId)
    {
        if (!string.IsNullOrWhiteSpace(configuredHomePageId))
        {
            var configured = scenes.FirstOrDefault(scene =>
                string.Equals(scene.Id, configuredHomePageId, StringComparison.Ordinal));
            if (configured is { Type: ScadaPageType.Default, IncludeInBuild: true })
            {
                return configured.Id;
            }
        }

        return scenes.FirstOrDefault(scene =>
            scene.Type == ScadaPageType.Default &&
            scene.IncludeInBuild)?.Id;
    }
}

/// <summary>
/// Project-level catalog of TF100Web tags imported for Element+ binding authoring.
/// </summary>
/// <remarks>
/// Decisions: DEC-0015.
/// Contracts: docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md, docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs.
/// </remarks>
public sealed record ScadaTagCatalog(
    string Schema,
    IReadOnlyList<ScadaTagDefinition> Tags,
    string? SourceFileName = null,
    DateTimeOffset? ImportedAtUtc = null)
{
    /// <summary>
    /// Gets the number of imported tags available to authoring surfaces.
    /// </summary>
    [JsonIgnore]
    public int Count => Tags.Count;
}

/// <summary>
/// Describes one imported TF100Web tag that can be linked to Element+ inputs or events.
/// </summary>
public sealed record ScadaTagDefinition(
    string Id,
    string DisplayName,
    string? KeywordLabel = null,
    string? KeywordType = null,
    string? Device = null,
    string? Protocol = null,
    string? AddressUri = null,
    string? Datatype = null,
    bool Writeable = false,
    bool Enabled = true,
    string? Unit = null);

public enum ScadaBuildValidationSeverity
{
    Warning,
    Error
}

public sealed record ScadaBuildValidationIssue(
    ScadaBuildValidationSeverity Severity,
    string Code,
    string Message,
    string? PageId = null);

public static class ScadaProjectBuildValidator
{
    public static IReadOnlyList<ScadaBuildValidationIssue> Validate(ScadaProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Validate(project.Scenes, project.HomePageId);
    }

    public static IReadOnlyList<ScadaBuildValidationIssue> Validate(
        IReadOnlyList<ScadaSceneReference> pages,
        string? homePageId)
    {
        var issues = new List<ScadaBuildValidationIssue>();
        var pagesById = pages.ToDictionary(page => page.Id, StringComparer.Ordinal);
        var compiledPages = pages.Where(page => page.IncludeInBuild).ToArray();
        if (!compiledPages.Any(page => page.Type == ScadaPageType.Default))
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                "build.no-default-page",
                "At least one compiled Default page is required."));
        }

        if (!string.IsNullOrWhiteSpace(homePageId))
        {
            if (!pagesById.TryGetValue(homePageId, out var homePage))
            {
                issues.Add(new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Error,
                    "build.home-missing",
                    $"Home page '{homePageId}' does not exist.",
                    homePageId));
            }
            else
            {
                if (homePage.Type != ScadaPageType.Default)
                {
                    issues.Add(new ScadaBuildValidationIssue(
                        ScadaBuildValidationSeverity.Error,
                        "build.home-not-default",
                        $"Home page '{homePage.Id}' must be a Default page.",
                        homePage.Id));
                }

                if (!homePage.IncludeInBuild)
                {
                    issues.Add(new ScadaBuildValidationIssue(
                        ScadaBuildValidationSeverity.Error,
                        "build.home-not-compiled",
                        $"Home page '{homePage.Id}' must be included in build.",
                        homePage.Id));
                }
            }
        }

        foreach (var page in compiledPages)
        {
            ValidateCompositionReference(
                issues,
                pagesById,
                page,
                page.HeaderPageId,
                ScadaPageType.Header,
                "header");
            ValidateCompositionReference(
                issues,
                pagesById,
                page,
                page.FooterPageId,
                ScadaPageType.Footer,
                "footer");

            if ((page.Type == ScadaPageType.Header || page.Type == ScadaPageType.Footer) &&
                (!string.IsNullOrWhiteSpace(page.HeaderPageId) || !string.IsNullOrWhiteSpace(page.FooterPageId)))
            {
                issues.Add(new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Error,
                    "build.layout-page-composes-layout",
                    $"Page '{page.Id}' is a {page.Type} page and cannot reference a header or footer.",
                    page.Id));
            }
        }

        return issues;
    }

    private static void ValidateCompositionReference(
        List<ScadaBuildValidationIssue> issues,
        IReadOnlyDictionary<string, ScadaSceneReference> pagesById,
        ScadaSceneReference page,
        string? referencedPageId,
        ScadaPageType expectedType,
        string role)
    {
        if (string.IsNullOrWhiteSpace(referencedPageId))
        {
            return;
        }

        if (!pagesById.TryGetValue(referencedPageId, out var referencedPage))
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                $"build.{role}-missing",
                $"Page '{page.Id}' references missing {role} page '{referencedPageId}'.",
                page.Id));
            return;
        }

        if (referencedPage.Type != expectedType)
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                $"build.{role}-wrong-type",
                $"Page '{page.Id}' references '{referencedPage.Id}' as {role}, but its type is {referencedPage.Type}.",
                page.Id));
        }

        if (!referencedPage.IncludeInBuild)
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                $"build.{role}-not-compiled",
                $"Page '{page.Id}' references {role} page '{referencedPage.Id}', but that page is not included in build.",
                page.Id));
        }
    }
}

public static class DefaultDevicePresets
{
    public static IReadOnlyList<DevicePreset> All { get; } =
    [
        new("desktop-1280x873", "Desktop reference", DeviceClass.Desktop, DeviceOrientation.Landscape, new CanvasSize(1280, 873), false),
        new("desktop-1920x1080", "Desktop 16:9", DeviceClass.Desktop, DeviceOrientation.Landscape, new CanvasSize(1920, 1080), false),
        new("ipad-landscape", "iPad landscape", DeviceClass.Tablet, DeviceOrientation.Landscape, new CanvasSize(1180, 820), true),
        new("ipad-portrait", "iPad portrait", DeviceClass.Tablet, DeviceOrientation.Portrait, new CanvasSize(820, 1180), true),
        new("iphone-landscape", "iPhone landscape", DeviceClass.Mobile, DeviceOrientation.Landscape, new CanvasSize(932, 430), true),
        new("iphone-portrait", "iPhone portrait", DeviceClass.Mobile, DeviceOrientation.Portrait, new CanvasSize(430, 932), true)
    ];
}
