using System.Collections.Generic;
using ScadaBuilderV2.Domain.Versioning;
using ScadaBuilderV2.Domain.Scenes;
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
/// Decisions: DEC-0015, DEC-0016.
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
    string? Unit = null)
{
    /// <summary>
    /// Gets the tag label used in authoring selectors.
    /// </summary>
    [JsonIgnore]
    public string AuthoringLabel => string.Join(
        " | ",
        new[]
        {
            string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName,
            string.IsNullOrWhiteSpace(Datatype) ? null : Datatype,
            string.IsNullOrWhiteSpace(Device) ? null : Device
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
}

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
        ScadaProject project,
        IReadOnlyList<ScadaScene> scenes)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(scenes);

        var issues = Validate(project.Scenes, project.HomePageId).ToList();
        var tagsById = (project.TagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag.Id))
            .ToDictionary(tag => tag.Id, StringComparer.Ordinal);
        var sceneIdsToBuild = project.Scenes
            .Where(reference => reference.IncludeInBuild)
            .Select(reference => reference.Id)
            .ToHashSet(StringComparer.Ordinal);
        var pagesById = project.Scenes.ToDictionary(reference => reference.Id, StringComparer.Ordinal);

        foreach (var scene in scenes.Where(scene => sceneIdsToBuild.Contains(scene.Id)))
        {
            ValidateSceneValueBindings(issues, scene, tagsById);
            ValidateSceneActions(issues, scene, tagsById, pagesById);
            AuditOrphanedEventBindings(issues, scene);
            ValidateSceneCommandBindings(issues, scene, project.TagCatalog);
        }

        return issues;
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

    private static void ValidateSceneValueBindings(
        List<ScadaBuildValidationIssue> issues,
        ScadaScene scene,
        IReadOnlyDictionary<string, ScadaTagDefinition> tagsById)
    {
        foreach (var element in FlattenElements(scene.Elements))
        {
            var readTagId = element.Data?.ReadTagId;
            if (!string.IsNullOrWhiteSpace(readTagId) && !tagsById.ContainsKey(readTagId))
            {
                issues.Add(new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Error,
                    "tag.read-missing",
                    $"Element '{element.Id}' references missing read tag '{readTagId}'.",
                    scene.Id));
            }

            var writeTagId = element.Data?.WriteTagId;
            if (string.IsNullOrWhiteSpace(writeTagId))
            {
                continue;
            }

            if (!tagsById.TryGetValue(writeTagId, out var tag))
            {
                issues.Add(new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Error,
                    "tag.write-missing",
                    $"Element '{element.Id}' references missing write tag '{writeTagId}'.",
                    scene.Id));
                continue;
            }

            if (!element.IsWritableInput)
            {
                issues.Add(new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Error,
                    "tag.write-readonly-element",
                    $"Element '{element.Id}' cannot write tag '{writeTagId}' because it is not an editable input.",
                    scene.Id));
            }

            if (!tag.Writeable)
            {
                issues.Add(new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Error,
                    "tag.write-readonly-tag",
                    $"Element '{element.Id}' cannot write tag '{writeTagId}' because the tag is read-only.",
                    scene.Id));
            }
        }
    }

    private static void ValidateSceneActions(
        List<ScadaBuildValidationIssue> issues,
        ScadaScene scene,
        IReadOnlyDictionary<string, ScadaTagDefinition> tagsById,
        IReadOnlyDictionary<string, ScadaSceneReference> pagesById)
    {
        var elementIds = FlattenElements(scene.Elements)
            .Select(element => element.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var action in scene.ActionDefinitions)
        {
            if (action.Kind is ScadaActionKind.Show or ScadaActionKind.Hide or ScadaActionKind.ToggleVisibility)
            {
                if (string.IsNullOrWhiteSpace(action.TargetElementId) || !elementIds.Contains(action.TargetElementId))
                {
                    issues.Add(new ScadaBuildValidationIssue(
                        ScadaBuildValidationSeverity.Error,
                        "action.target-missing",
                        $"Action '{action.Id}' references missing target Element+ '{action.TargetElementId}'.",
                        scene.Id));
                }
            }

            if (action.Kind is ScadaActionKind.MountFragment or ScadaActionKind.ClosePopup or ScadaActionKind.TogglePopup)
            {
                ValidatePopupFragmentTarget(issues, scene, action, pagesById);
                ValidatePopupOptions(issues, scene, action, elementIds);
            }

            ValidateActionCondition(issues, scene, action, tagsById);
        }
    }

    // Keeps advanced popup placement model-backed and rejects stale host region targets before export.
    private static void ValidatePopupOptions(
        List<ScadaBuildValidationIssue> issues,
        ScadaScene scene,
        ScadaActionDefinition action,
        IReadOnlySet<string> elementIds)
    {
        if (action.PopupOptions is not { Position: ScadaPopupPosition.HostRegion } popupOptions)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(popupOptions.HostRegionId) || !elementIds.Contains(popupOptions.HostRegionId))
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                "popup.host-region-missing",
                $"Action '{action.Id}' references missing popup host Element+ '{popupOptions.HostRegionId}'.",
                scene.Id));
        }
    }

    // Enforces that popup actions can only mount compiled fragment pages.
    private static void ValidatePopupFragmentTarget(
        List<ScadaBuildValidationIssue> issues,
        ScadaScene scene,
        ScadaActionDefinition action,
        IReadOnlyDictionary<string, ScadaSceneReference> pagesById)
    {
        if (string.IsNullOrWhiteSpace(action.TargetPageId) ||
            !pagesById.TryGetValue(action.TargetPageId, out var targetPage))
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                "popup.fragment-missing",
                $"Action '{action.Id}' references missing popup fragment '{action.TargetPageId}'.",
                scene.Id));
            return;
        }

        if (targetPage.Type != ScadaPageType.Fragment)
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                "popup.target-not-fragment",
                $"Action '{action.Id}' references page '{targetPage.Id}' as popup, but its type is {targetPage.Type}.",
                scene.Id));
        }

        if (!targetPage.IncludeInBuild)
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                "popup.fragment-not-compiled",
                $"Action '{action.Id}' references popup fragment '{targetPage.Id}', but that page is not included in build.",
                scene.Id));
        }
    }

    private static void ValidateActionCondition(
        List<ScadaBuildValidationIssue> issues,
        ScadaScene scene,
        ScadaActionDefinition action,
        IReadOnlyDictionary<string, ScadaTagDefinition> tagsById)
    {
        if (action.Condition is not null)
        {
            ValidateSingleActionCondition(issues, scene, action, action.Condition, tagsById);
        }

        if (action.ConditionGroup is null)
        {
            return;
        }

        if (action.ConditionGroup.Conditions.Count == 0)
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                "condition.group-empty",
                $"Action '{action.Id}' condition group must contain at least one condition.",
                scene.Id));
            return;
        }

        foreach (var condition in action.ConditionGroup.Conditions)
        {
            ValidateSingleActionCondition(issues, scene, action, condition, tagsById);
        }
    }

    // Keeps single and compound condition validation consistent.
    private static void ValidateSingleActionCondition(
        List<ScadaBuildValidationIssue> issues,
        ScadaScene scene,
        ScadaActionDefinition action,
        ScadaActionCondition condition,
        IReadOnlyDictionary<string, ScadaTagDefinition> tagsById)
    {
        if (string.IsNullOrWhiteSpace(condition.TagId) ||
            !tagsById.TryGetValue(condition.TagId, out var tag))
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                "condition.tag-missing",
                $"Action '{action.Id}' references missing condition tag '{condition.TagId}'.",
                scene.Id));
            return;
        }

        if (condition.Operator is ScadaConditionOperator.True or ScadaConditionOperator.False)
        {
            if (!IsBooleanDatatype(tag.Datatype))
            {
                issues.Add(new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Error,
                    "condition.boolean-operator-nonboolean-tag",
                    $"Action '{action.Id}' uses a boolean condition operator on non-boolean tag '{tag.Id}'.",
                    scene.Id));
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(condition.CompareValue))
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                "condition.value-missing",
                $"Action '{action.Id}' condition requires a comparison value.",
                scene.Id));
        }
    }

    private static bool IsBooleanDatatype(string? datatype)
    {
        return !string.IsNullOrWhiteSpace(datatype) &&
            (string.Equals(datatype, "bool", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(datatype, "boolean", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(datatype, "booléen", StringComparison.OrdinalIgnoreCase));
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

    public static void AuditOrphanedEventBindings(
        List<ScadaBuildValidationIssue> issues,
        ScadaScene scene)
    {
        foreach (var element in FlattenElements(scene.Elements))
        {
            if (element.Events is not { Count: > 0 })
                continue;

            var hasCommandConfig = element is { EffectiveCommandConfig.Commands.Count: > 0 };
            if (hasCommandConfig)
                continue;

            var navigateEvents = element.Events
                .Where(e => e.ActionId?.Contains("changepage", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            if (navigateEvents.Count > 0)
            {
                var eventIds = string.Join(", ", navigateEvents.Select(e => e.ActionId));
                issues.Add(new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Warning,
                    "DEC-ORPHAN-EVENTS",
                    $"Scene '{scene.Id}': element '{element.Id}' ('{element.DisplayName}') has legacy " +
                    $"navigate EventBinding(s) [{eventIds}] without a CommandConfig equivalent. " +
                    $"These events will not function in the TF100Web #scada-host runtime. " +
                    $"Remove the EventBindings or add a CommandConfig with a Navigate command.",
                    scene.Id));
            }
        }
    }

    private static void ValidateSceneCommandBindings(
        List<ScadaBuildValidationIssue> issues,
        ScadaScene scene,
        ScadaTagCatalog? catalog)
    {
        if (catalog is null) return;

        foreach (var element in FlattenElements(scene.Elements))
        {
            if (element.EffectiveCommandConfig.Commands.Count == 0)
                continue;

            foreach (var cmd in element.EffectiveCommandConfig.Commands)
            {
                var cmdIssues = ElementEvents.Command.ScadaCommandBindingValidator
                    .ValidateCommandBinding(cmd, catalog);
                foreach (var issue in cmdIssues)
                {
                    issues.Add(new ScadaBuildValidationIssue(
                        ScadaBuildValidationSeverity.Warning,
                        "DEC-CMD-TAGID",
                        $"Scene '{scene.Id}', element '{element.Id}': {issue}",
                        scene.Id));
                }
            }
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
