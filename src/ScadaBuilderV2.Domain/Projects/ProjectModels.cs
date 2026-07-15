using System.Collections.Generic;
using ScadaBuilderV2.Domain.ElementEvents.State;
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
    string? FooterPageId = null,
    Guid PageKey = default,
    string? PageCode = null,
    PageOrigin? Origin = null,
    ImportProvenance? ImportProvenance = null,
    Guid? HeaderPageKey = null,
    Guid? FooterPageKey = null)
{
    /// <summary>Gets the human-visible page code, including compatibility fallback for pre-migration projects.</summary>
    [JsonIgnore]
    public string EffectivePageCode => string.IsNullOrWhiteSpace(PageCode) ? Id : PageCode;

    /// <summary>Gets whether the page is native or projected from an imported source.</summary>
    [JsonIgnore]
    public PageOrigin EffectiveOrigin => Origin ?? (ImportProvenance is null ? PageOrigin.Native : PageOrigin.Imported);

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
    ScadaTagCatalog? TagCatalog = null,
    Guid? HomePageKey = null)
{
    [JsonIgnore]
    public IReadOnlyList<ScadaSceneReference> Pages => Scenes;

    [JsonIgnore]
    public string? EffectiveHomePageId => ResolveHomePageId(Scenes, HomePageId);

    /// <summary>Gets the stable logical key of the effective compiled home page.</summary>
    [JsonIgnore]
    public Guid? EffectiveHomePageKey => ResolveHomePageKey(Scenes, HomePageKey, HomePageId);

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
                string.Equals(scene.EffectivePageCode, configuredHomePageId, StringComparison.Ordinal));
            if (configured is { Type: ScadaPageType.Default, IncludeInBuild: true })
            {
                return configured.EffectivePageCode;
            }
        }

        return scenes.FirstOrDefault(scene =>
            scene.Type == ScadaPageType.Default &&
            scene.IncludeInBuild)?.EffectivePageCode;
    }

    /// <summary>Resolves the stable logical key of the configured or fallback home page.</summary>
    public static Guid? ResolveHomePageKey(
        IReadOnlyList<ScadaSceneReference> scenes,
        Guid? configuredHomePageKey,
        string? configuredHomePageId)
    {
        if (configuredHomePageKey is { } key && key != Guid.Empty)
        {
            var configured = scenes.FirstOrDefault(scene => scene.PageKey == key);
            if (configured is { Type: ScadaPageType.Default, IncludeInBuild: true })
            {
                return configured.PageKey;
            }
        }

        var effectiveId = ResolveHomePageId(scenes, configuredHomePageId);
        return scenes.FirstOrDefault(scene =>
            string.Equals(scene.EffectivePageCode, effectiveId, StringComparison.Ordinal))?.PageKey is { } resolved && resolved != Guid.Empty
            ? resolved
            : null;
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
    string? PageId = null,
    Guid? PageKey = null,
    string? ElementId = null,
    string? CommandId = null,
    string? PropertyPath = null,
    Guid? TargetKey = null,
    string? SuggestedFix = null);

public static class ScadaProjectBuildValidator
{
    public static IReadOnlyList<ScadaBuildValidationIssue> Validate(ScadaProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Validate(project.Scenes, project.HomePageId, project.HomePageKey);
    }

    public static IReadOnlyList<ScadaBuildValidationIssue> Validate(
        ScadaProject project,
        IReadOnlyList<ScadaScene> scenes)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(scenes);

        var issues = Validate(project.Scenes, project.HomePageId, project.HomePageKey).ToList();
        var tagsById = (project.TagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag.Id))
            .ToDictionary(tag => tag.Id, StringComparer.Ordinal);
        var sceneKeysToBuild = project.Scenes
            .Where(reference => reference.IncludeInBuild)
            .Select(reference => reference.PageKey)
            .Where(key => key != Guid.Empty)
            .ToHashSet();
        var sceneCodesToBuild = project.Scenes
            .Where(reference => reference.IncludeInBuild)
            .Select(reference => reference.EffectivePageCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pagesById = project.Scenes
            .GroupBy(reference => reference.EffectivePageCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var pagesByKey = project.Scenes
            .Where(reference => reference.PageKey != Guid.Empty)
            .GroupBy(reference => reference.PageKey)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var scene in scenes.Where(scene =>
                     (scene.PageKey != Guid.Empty && sceneKeysToBuild.Contains(scene.PageKey)) ||
                     sceneCodesToBuild.Contains(scene.EffectivePageCode)))
        {
            ValidateSceneValueBindings(issues, scene, tagsById);
            ValidateSceneActions(issues, scene, tagsById, pagesById, pagesByKey);
            AuditOrphanedEventBindings(issues, scene);
            ValidateSceneCommandBindings(issues, scene, project.TagCatalog, pagesById, pagesByKey);
        }

        return issues;
    }

    public static IReadOnlyList<ScadaBuildValidationIssue> Validate(
        IReadOnlyList<ScadaSceneReference> pages,
        string? homePageId)
    {
        return Validate(pages, homePageId, null);
    }

    private static IReadOnlyList<ScadaBuildValidationIssue> Validate(
        IReadOnlyList<ScadaSceneReference> pages,
        string? homePageId,
        Guid? homePageKey)
    {
        var issues = new List<ScadaBuildValidationIssue>();
        var pagesById = pages
            .GroupBy(page => page.EffectivePageCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var pagesByKey = pages
            .Where(page => page.PageKey != Guid.Empty)
            .GroupBy(page => page.PageKey)
            .ToDictionary(group => group.Key, group => group.First());
        var compiledPages = pages.Where(page => page.IncludeInBuild).ToArray();
        if (!compiledPages.Any(page => page.Type == ScadaPageType.Default))
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                "build.no-default-page",
                "At least one compiled Default page is required."));
        }

        if ((homePageKey is { } configuredHomeKey && configuredHomeKey != Guid.Empty) || !string.IsNullOrWhiteSpace(homePageId))
        {
            var homePage = ResolvePage(homePageKey, homePageId, pagesByKey, pagesById);
            if (homePage is null)
            {
                issues.Add(new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Error,
                    "build.home-missing",
                    $"Home page '{homePageId ?? homePageKey?.ToString()}' does not exist.",
                    homePageId,
                    PropertyPath: "Project.HomePageKey",
                    TargetKey: homePageKey,
                    SuggestedFix: "Select an existing compiled Default page as the home page."));
            }
            else
            {
                if (homePage.Type != ScadaPageType.Default)
                {
                    issues.Add(new ScadaBuildValidationIssue(
                        ScadaBuildValidationSeverity.Error,
                        "build.home-not-default",
                        $"Home page '{homePage.Id}' must be a Default page.",
                        homePage.EffectivePageCode,
                        homePage.PageKey,
                        PropertyPath: "Project.HomePageKey",
                        TargetKey: homePage.PageKey,
                        SuggestedFix: "Choose a Default page as the home page."));
                }

                if (!homePage.IncludeInBuild)
                {
                    issues.Add(new ScadaBuildValidationIssue(
                        ScadaBuildValidationSeverity.Error,
                        "build.home-not-compiled",
                        $"Home page '{homePage.Id}' must be included in build.",
                        homePage.EffectivePageCode,
                        homePage.PageKey,
                        PropertyPath: "Project.HomePageKey",
                        TargetKey: homePage.PageKey,
                        SuggestedFix: "Include the home page in the build."));
                }
            }
        }

        foreach (var page in compiledPages)
        {
            ValidateCompositionReference(
                issues,
                pagesById,
                pagesByKey,
                page,
                page.HeaderPageKey,
                page.HeaderPageId,
                ScadaPageType.Header,
                "header");
            ValidateCompositionReference(
                issues,
                pagesById,
                pagesByKey,
                page,
                page.FooterPageKey,
                page.FooterPageId,
                ScadaPageType.Footer,
                "footer");

            if ((page.Type == ScadaPageType.Header || page.Type == ScadaPageType.Footer) &&
                (page.HeaderPageKey is not null || page.FooterPageKey is not null ||
                 !string.IsNullOrWhiteSpace(page.HeaderPageId) || !string.IsNullOrWhiteSpace(page.FooterPageId)))
            {
                issues.Add(new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Error,
                    "build.layout-page-composes-layout",
                    $"Page '{page.Id}' is a {page.Type} page and cannot reference a header or footer.",
                    page.EffectivePageCode,
                    page.PageKey,
                    PropertyPath: "Page.Type",
                    SuggestedFix: "Remove header and footer composition from layout pages."));
            }
        }

        return issues;
    }

    private static void ValidateCompositionReference(
        List<ScadaBuildValidationIssue> issues,
        IReadOnlyDictionary<string, ScadaSceneReference> pagesById,
        IReadOnlyDictionary<Guid, ScadaSceneReference> pagesByKey,
        ScadaSceneReference page,
        Guid? referencedPageKey,
        string? referencedPageId,
        ScadaPageType expectedType,
        string role)
    {
        if ((referencedPageKey is null || referencedPageKey == Guid.Empty) && string.IsNullOrWhiteSpace(referencedPageId))
        {
            return;
        }

        var referencedPage = ResolvePage(referencedPageKey, referencedPageId, pagesByKey, pagesById);
        if (referencedPage is null)
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                $"build.{role}-missing",
                $"Page '{page.EffectivePageCode}' references missing {role} page '{referencedPageId ?? referencedPageKey?.ToString()}'.",
                page.EffectivePageCode,
                page.PageKey,
                PropertyPath: $"Page.{char.ToUpperInvariant(role[0])}{role[1..]}PageKey",
                TargetKey: referencedPageKey,
                SuggestedFix: $"Select an existing compiled {expectedType} page or clear the {role}."));
            return;
        }

        if (referencedPage.Type != expectedType)
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                $"build.{role}-wrong-type",
                $"Page '{page.Id}' references '{referencedPage.Id}' as {role}, but its type is {referencedPage.Type}.",
                page.EffectivePageCode,
                page.PageKey,
                PropertyPath: $"Page.{char.ToUpperInvariant(role[0])}{role[1..]}PageKey",
                TargetKey: referencedPage.PageKey,
                SuggestedFix: $"Select a page whose type is {expectedType}."));
        }

        if (!referencedPage.IncludeInBuild)
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                $"build.{role}-not-compiled",
                $"Page '{page.Id}' references {role} page '{referencedPage.Id}', but that page is not included in build.",
                page.EffectivePageCode,
                page.PageKey,
                PropertyPath: $"Page.{char.ToUpperInvariant(role[0])}{role[1..]}PageKey",
                TargetKey: referencedPage.PageKey,
                SuggestedFix: $"Include the {role} page in the build."));
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

            ValidateTableCellValueBindings(issues, scene, element, tagsById);

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

    private static void ValidateTableCellValueBindings(
        List<ScadaBuildValidationIssue> issues,
        ScadaScene scene,
        ScadaElement element,
        IReadOnlyDictionary<string, ScadaTagDefinition> tagsById)
    {
        if (element.Kind != ScadaElementKind.Table || element.Table is null)
        {
            return;
        }

        foreach (var cell in element.Table.EffectiveCells.Where(cell => cell.ValueBindings is not null))
        {
            var path = $"Elements[{element.Id}].Table.Cells[{cell.Row},{cell.Column}]";
            var content = cell.EffectiveContent;
            if (content.Kind != ScadaTableCellContentKind.InputNumeric)
            {
                AddTableCellIssue(issues, scene, element, path, "table-cell.target-not-numeric", "La cellule liee doit etre de type InputNumeric.");
            }

            var readTagId = cell.ValueBindings?.ReadTagId;
            if (!string.IsNullOrWhiteSpace(readTagId) &&
                (!tagsById.TryGetValue(readTagId, out var readTag) || !readTag.Enabled))
            {
                AddTableCellIssue(issues, scene, element, path, "table-cell.tag.read-missing", $"Le tag de lecture actif '{readTagId}' est absent du catalogue.");
            }

            var writeTagId = cell.ValueBindings?.WriteTagId;
            if (!string.IsNullOrWhiteSpace(writeTagId))
            {
                if (!tagsById.TryGetValue(writeTagId, out var writeTag) || !writeTag.Enabled)
                {
                    AddTableCellIssue(issues, scene, element, path, "table-cell.tag.write-missing", $"Le tag d'ecriture actif '{writeTagId}' est absent du catalogue.");
                }
                else if (!writeTag.Writeable)
                {
                    AddTableCellIssue(issues, scene, element, path, "table-cell.tag.write-readonly", $"Le tag '{writeTagId}' n'est pas ecrivable.");
                }

                if (content.IsReadOnly)
                {
                    AddTableCellIssue(issues, scene, element, path, "table-cell.write-readonly-input", "Une cellule en lecture seule ne peut pas porter un binding d'ecriture.");
                }
            }

            if (content.Minimum.HasValue && content.Maximum.HasValue && content.Minimum > content.Maximum)
            {
                AddTableCellIssue(issues, scene, element, path, "table-cell.numeric-range", "Le minimum ne peut pas depasser le maximum.");
            }
            if (content.Step.HasValue && (!double.IsFinite(content.Step.Value) || content.Step <= 0))
            {
                AddTableCellIssue(issues, scene, element, path, "table-cell.numeric-step", "Le pas doit etre un nombre fini superieur a zero.");
            }
            if (content.NumericValue.HasValue &&
                (!double.IsFinite(content.NumericValue.Value) ||
                 content.Minimum.HasValue && content.NumericValue < content.Minimum ||
                 content.Maximum.HasValue && content.NumericValue > content.Maximum))
            {
                AddTableCellIssue(issues, scene, element, path, "table-cell.numeric-initial-value", "La valeur initiale doit etre finie et respecter minimum/maximum.");
            }
            if (!IsSupportedTableDisplayFormat(content.DisplayFormat))
            {
                AddTableCellIssue(issues, scene, element, path, "table-cell.display-format", "Le format d'affichage numerique n'est pas supporte.");
            }
        }
    }

    private static void AddTableCellIssue(
        ICollection<ScadaBuildValidationIssue> issues,
        ScadaScene scene,
        ScadaElement element,
        string path,
        string code,
        string message) =>
        issues.Add(new ScadaBuildValidationIssue(
            ScadaBuildValidationSeverity.Error,
            code,
            message,
            scene.EffectivePageCode,
            scene.PageKey,
            ElementId: element.Id,
            PropertyPath: path));

    private static bool IsSupportedTableDisplayFormat(string? displayFormat)
    {
        if (string.IsNullOrWhiteSpace(displayFormat))
        {
            return true;
        }

        var value = displayFormat.Trim();
        if (value.StartsWith("fixed:", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(value[6..], out var decimals) && decimals >= 0;
        }

        var separatorCount = value.Count(character => character == '.');
        return value.Any(character => character == '#') &&
               separatorCount <= 1 &&
               value.All(character => character is '#' or '.');
    }

    private static void ValidateSceneActions(
        List<ScadaBuildValidationIssue> issues,
        ScadaScene scene,
        IReadOnlyDictionary<string, ScadaTagDefinition> tagsById,
        IReadOnlyDictionary<string, ScadaSceneReference> pagesById,
        IReadOnlyDictionary<Guid, ScadaSceneReference> pagesByKey)
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
                        scene.EffectivePageCode,
                        scene.PageKey,
                        ElementId: action.TargetElementId,
                        CommandId: action.Id,
                        PropertyPath: $"Scene.Actions[{action.Id}].TargetElementId",
                        SuggestedFix: "Select an existing Element+ target or remove the action."));
                }
            }

            if (action.Kind == ScadaActionKind.Navigate)
            {
                ValidateActionPageTarget(issues, scene, action, ScadaPageType.Default, "navigation", pagesById, pagesByKey);
            }

            if (action.Kind is ScadaActionKind.MountFragment or ScadaActionKind.ClosePopup or ScadaActionKind.TogglePopup)
            {
                ValidateActionPageTarget(issues, scene, action, ScadaPageType.Fragment, "popup", pagesById, pagesByKey);
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

    private static void ValidateActionPageTarget(
        List<ScadaBuildValidationIssue> issues,
        ScadaScene scene,
        ScadaActionDefinition action,
        ScadaPageType expectedType,
        string role,
        IReadOnlyDictionary<string, ScadaSceneReference> pagesById,
        IReadOnlyDictionary<Guid, ScadaSceneReference> pagesByKey)
    {
        var targetPage = ResolvePage(action.TargetPageKey, action.TargetPageId, pagesByKey, pagesById);
        var isPopup = expectedType == ScadaPageType.Fragment;
        if (targetPage is null)
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                isPopup ? "popup.fragment-missing" : "navigate.page-missing",
                $"Action '{action.Id}' references missing {role} page '{action.TargetPageId ?? action.TargetPageKey?.ToString()}'.",
                scene.EffectivePageCode,
                scene.PageKey,
                CommandId: action.Id,
                PropertyPath: $"Scene.Actions[{action.Id}].TargetPageKey",
                TargetKey: action.TargetPageKey,
                SuggestedFix: $"Select an existing compiled {expectedType} page."));
            return;
        }

        if (targetPage.Type != expectedType)
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                isPopup ? "popup.target-not-fragment" : "navigate.target-not-default",
                $"Action '{action.Id}' references page '{targetPage.EffectivePageCode}' as {role}, but its type is {targetPage.Type}.",
                scene.EffectivePageCode,
                scene.PageKey,
                CommandId: action.Id,
                PropertyPath: $"Scene.Actions[{action.Id}].TargetPageKey",
                TargetKey: targetPage.PageKey,
                SuggestedFix: $"Select a page whose type is {expectedType}."));
        }

        if (!targetPage.IncludeInBuild)
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                isPopup ? "popup.fragment-not-compiled" : "navigate.page-not-compiled",
                $"Action '{action.Id}' references {role} page '{targetPage.EffectivePageCode}', but that page is not included in build.",
                scene.EffectivePageCode,
                scene.PageKey,
                CommandId: action.Id,
                PropertyPath: $"Scene.Actions[{action.Id}].TargetPageKey",
                TargetKey: targetPage.PageKey,
                SuggestedFix: "Include the target page in the build or select another page."));
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

    /// <summary>
    /// Emits a warning for every element that still carries legacy
    /// <see cref="ScadaElement.EventBindings"/>. EventBindings are
    /// decommissioned and are not exported as runtime-active data.
    /// Elements should be migrated to <see cref="ScadaElement.CommandConfig"/>
    /// or <see cref="ScadaElement.StateConfig"/>.
    /// </summary>
    /// <remarks>
    /// Contracts: docs/superpowers/specs/2026-07-09-export-group-runtime-wrapper.md §6.
    /// Tests: tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.
    /// </remarks>
    public static void AuditOrphanedEventBindings(
        List<ScadaBuildValidationIssue> issues,
        ScadaScene scene)
    {
        foreach (var element in FlattenElements(scene.Elements))
        {
            if (element.EventBindings.Count == 0)
                continue;

            var hasModernConfig = HasModernRuntimeConfig(element);

            var extra = hasModernConfig
                ? " L'element possede aussi une configuration moderne (CommandConfig/StateConfig) qui sera exportee."
                : " Aucune configuration moderne (CommandConfig/StateConfig) ne remplace ces EventBindings. L'element risque d'etre inactif dans TF100Web.";

            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Warning,
                "event-bindings-decommissioned",
                $"Scene '{scene.Id}', element '{element.Id}' ({element.DisplayName}): " +
                $"EventBindings decommissionnes detectes ({element.EventBindings.Count} binding(s)). " +
                $"Authoring attendu : CommandConfig ou StateConfig. " +
                $"Les EventBindings ne sont pas exportes comme runtime TF100Web.{extra}",
                scene.Id));
        }
    }

    private static bool HasModernRuntimeConfig(ScadaElement element)
    {
        var stateConfig = element.EffectiveStateConfig;
        var fallback = stateConfig.QualityFallback;
        var defaultFallback = ScadaElementStateConfig.Default.QualityFallback;

        return element.EffectiveCommandConfig.Commands.Count > 0
            || stateConfig.States.Count > 0
            || stateConfig.ReadVariable is not null
            || fallback.Opacity != defaultFallback.Opacity
            || fallback.BorderColor != defaultFallback.BorderColor
            || fallback.BorderWidth != defaultFallback.BorderWidth
            || stateConfig.DefaultEffect != ScadaEffectBlock.Empty;
    }

    private static void ValidateSceneCommandBindings(
        List<ScadaBuildValidationIssue> issues,
        ScadaScene scene,
        ScadaTagCatalog? catalog,
        IReadOnlyDictionary<string, ScadaSceneReference> pagesById,
        IReadOnlyDictionary<Guid, ScadaSceneReference> pagesByKey)
    {
        foreach (var element in FlattenElements(scene.Elements))
        {
            if (element.EffectiveCommandConfig.Commands.Count == 0)
                continue;

            foreach (var cmd in element.EffectiveCommandConfig.Commands)
            {
                if (cmd.Kind == ElementEvents.Command.ScadaCommandKind.Navigate)
                {
                    ValidateCommandPageTarget(issues, scene, element, cmd, ScadaPageType.Default, "navigation", pagesById, pagesByKey);
                }
                else if (cmd.Kind is ElementEvents.Command.ScadaCommandKind.OpenPopup or
                         ElementEvents.Command.ScadaCommandKind.TogglePopup or
                         ElementEvents.Command.ScadaCommandKind.ClosePopup)
                {
                    ValidateCommandPageTarget(issues, scene, element, cmd, ScadaPageType.Fragment, "popup", pagesById, pagesByKey);
                }

                if (catalog is not null)
                {
                    var cmdIssues = ElementEvents.Command.ScadaCommandBindingValidator
                        .ValidateCommandBinding(cmd, catalog);
                    foreach (var issue in cmdIssues)
                    {
                        issues.Add(new ScadaBuildValidationIssue(
                            ScadaBuildValidationSeverity.Warning,
                            "DEC-CMD-TAGID",
                            $"Scene '{scene.Id}', element '{element.Id}': {issue}",
                            scene.EffectivePageCode,
                            scene.PageKey,
                            element.Id,
                            cmd.Id,
                            $"Scene.Elements[{element.Id}].CommandConfig.Commands[{cmd.Id}]",
                            SuggestedFix: "Select a valid tag for this command."));
                    }
                }
            }
        }
    }

    private static void ValidateCommandPageTarget(
        List<ScadaBuildValidationIssue> issues,
        ScadaScene scene,
        ScadaElement element,
        ElementEvents.Command.ScadaCommandBinding command,
        ScadaPageType expectedType,
        string role,
        IReadOnlyDictionary<string, ScadaSceneReference> pagesById,
        IReadOnlyDictionary<Guid, ScadaSceneReference> pagesByKey)
    {
        var targetPage = ResolvePage(command.TargetPageKey, command.TargetPageId, pagesByKey, pagesById);
        var propertyPath = $"Scene.Elements[{element.Id}].CommandConfig.Commands[{command.Id}].TargetPageKey";
        var codePrefix = expectedType == ScadaPageType.Fragment ? "command.popup" : "command.navigate";
        if (targetPage is null)
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                $"{codePrefix}-missing",
                $"Command '{command.Id}' references missing {role} page '{command.TargetPageId ?? command.TargetPageKey?.ToString()}'.",
                scene.EffectivePageCode,
                scene.PageKey,
                element.Id,
                command.Id,
                propertyPath,
                command.TargetPageKey,
                $"Select an existing compiled {expectedType} page."));
            return;
        }

        if (targetPage.Type != expectedType)
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                $"{codePrefix}-wrong-type",
                $"Command '{command.Id}' references page '{targetPage.EffectivePageCode}' as {role}, but its type is {targetPage.Type}.",
                scene.EffectivePageCode,
                scene.PageKey,
                element.Id,
                command.Id,
                propertyPath,
                targetPage.PageKey,
                $"Select a page whose type is {expectedType}."));
        }

        if (!targetPage.IncludeInBuild)
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                $"{codePrefix}-not-compiled",
                $"Command '{command.Id}' references {role} page '{targetPage.EffectivePageCode}', but that page is not included in build.",
                scene.EffectivePageCode,
                scene.PageKey,
                element.Id,
                command.Id,
                propertyPath,
                targetPage.PageKey,
                "Include the target page in the build or select another page."));
        }
    }

    private static ScadaSceneReference? ResolvePage(
        Guid? targetKey,
        string? targetCode,
        IReadOnlyDictionary<Guid, ScadaSceneReference> pagesByKey,
        IReadOnlyDictionary<string, ScadaSceneReference> pagesByCode)
    {
        if (targetKey is { } key && key != Guid.Empty)
        {
            return pagesByKey.GetValueOrDefault(key);
        }

        return !string.IsNullOrWhiteSpace(targetCode) && pagesByCode.TryGetValue(targetCode, out var page)
            ? page
            : null;
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
