using ScadaBuilderV2.Domain.Projects;
using System.Text.Json.Serialization;

namespace ScadaBuilderV2.Domain.Scenes;

public enum ScadaElementKind
{
    Text,
    InputText,
    InputNumeric,
    Image,
    Shape,
    Group,
    Button,
    Container,
    LegacyStatic,
    Custom
}

public enum ElementPositionMode
{
    Absolute,
    Relative
}

public sealed record SceneBounds(double X, double Y, double Width, double Height)
{
    [JsonIgnore]
    public bool HasPositiveSize => Width > 0 && Height > 0;
}

public sealed record ScadaElementLayout(
    ElementPositionMode PositionMode,
    string? RelativeToElementId)
{
    public static ScadaElementLayout Absolute { get; } = new(ElementPositionMode.Absolute, null);
}

public sealed record ScadaElementStyle(
    string FontFamily,
    double FontSize,
    string Foreground,
    string Background,
    string BorderColor,
    double BorderWidth,
    string BorderStyle,
    string ShadowPreset,
    string? AdvancedCss)
{
    public static ScadaElementStyle DefaultText { get; } = new(
        "Segoe UI",
        16,
        "#0F2A30",
        "Transparent",
        "Transparent",
        0,
        "None",
        "None",
        null);

    public static ScadaElementStyle DefaultInput { get; } = new(
        "Segoe UI",
        14,
        "#0F2A30",
        "#FFFFFF",
        "#8AA0A6",
        1,
        "Solid",
        "None",
        null);
}

public sealed record ScadaElementData(
    string? Text,
    string? Placeholder,
    double? Value,
    double? Minimum,
    double? Maximum,
    int? Decimals,
    string? Unit,
    string? DisplayFormat,
    string? TagBinding,
    bool IsReadOnly);

public sealed record LegacySourceTrace(
    string SourceSystem,
    string SourceDocumentId,
    string? SourceElementId,
    string? SourceElementName,
    string? SourcePath)
{
    [JsonIgnore]
    public bool HasElementReference => !string.IsNullOrWhiteSpace(SourceElementId);
}

public sealed record LegacyTextOverride(
    string SourceElementId,
    string Text);

public sealed record ScadaObjectEventBinding(
    string Trigger,
    string ActionId,
    bool StopPropagation = false,
    bool PreventDefault = false);

public enum ScadaActionKind
{
    Navigate,
    Show,
    Hide,
    ToggleVisibility,
    SetClass,
    ToggleClass,
    MountFragment,
    WriteTag
}

public sealed record ScadaActionDefinition(
    string Id,
    ScadaActionKind Kind,
    string? TargetPageId = null,
    string? TargetElementId = null,
    string? ClassName = null,
    string? TagId = null,
    string? Value = null);

public sealed record LegacyElementPayload(
    string LegacyType,
    string Text,
    bool IsTextLike,
    string FontFamily,
    double FontSize,
    string Foreground,
    string Background,
    string? LegacyMarkup,
    string? RawMetadataJson);

public sealed record ScadaElement(
    string Id,
    string DisplayName,
    ScadaElementKind Kind,
    SceneBounds Bounds,
    LegacySourceTrace? LegacySource,
    ScadaElementLayout? Layout = null,
    ScadaElementStyle? Style = null,
    ScadaElementData? Data = null,
    IReadOnlyList<ScadaElement>? Children = null,
    LegacyElementPayload? LegacyPayload = null,
    IReadOnlyList<ScadaObjectEventBinding>? Events = null)
{
    [JsonIgnore]
    public string UserLabel => string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName;

    [JsonIgnore]
    public bool IsImportedFromLegacy => LegacySource is not null;

    [JsonIgnore]
    public bool IsLegacyStatic => Kind == ScadaElementKind.LegacyStatic && LegacySource?.HasElementReference == true;

    [JsonIgnore]
    public IReadOnlyList<ScadaElement> ChildElements => Children ?? Array.Empty<ScadaElement>();

    [JsonIgnore]
    public IReadOnlyList<ScadaObjectEventBinding> EventBindings => Events ?? Array.Empty<ScadaObjectEventBinding>();

    public static ScadaElement CreateText(string id, string displayName, double x, double y)
    {
        return new ScadaElement(
            id,
            displayName,
            ScadaElementKind.Text,
            new SceneBounds(x, y, 180, 28),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData("Texte", null, null, null, null, null, null, null, null, false));
    }

    public static ScadaElement CreateInputText(string id, string displayName, double x, double y)
    {
        return new ScadaElement(
            id,
            displayName,
            ScadaElementKind.InputText,
            new SceneBounds(x, y, 180, 32),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultInput,
            new ScadaElementData(null, "Texte", null, null, null, null, null, null, null, false));
    }

    public static ScadaElement CreateInputNumeric(string id, string displayName, double x, double y, bool isReadOnly = false)
    {
        return new ScadaElement(
            id,
            displayName,
            ScadaElementKind.InputNumeric,
            new SceneBounds(x, y, 180, 32),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultInput,
            new ScadaElementData(null, "0", 0, null, null, 0, null, "0", null, isReadOnly));
    }

    public static ScadaElement CreateLegacyStatic(
        string id,
        string displayName,
        SceneBounds bounds,
        LegacySourceTrace legacySource,
        LegacyElementPayload legacyPayload)
    {
        return new ScadaElement(
            id,
            displayName,
            ScadaElementKind.LegacyStatic,
            bounds,
            legacySource,
            ScadaElementLayout.Absolute,
            new ScadaElementStyle(
                string.IsNullOrWhiteSpace(legacyPayload.FontFamily) ? "Segoe UI" : legacyPayload.FontFamily,
                legacyPayload.FontSize > 0 ? legacyPayload.FontSize : 12,
                string.IsNullOrWhiteSpace(legacyPayload.Foreground) ? "#000000" : legacyPayload.Foreground,
                string.IsNullOrWhiteSpace(legacyPayload.Background) ? "Transparent" : legacyPayload.Background,
                "Transparent",
                0,
                "None",
                "None",
                null),
            new ScadaElementData(
                legacyPayload.Text,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false),
            null,
            legacyPayload);
    }
}

public sealed record ScadaScene(
    string Id,
    string Title,
    CanvasSize CanvasSize,
    IReadOnlyList<ScadaElement> Elements,
    string BackgroundColor = "#000000",
    IReadOnlyList<LegacyTextOverride>? LegacyTextOverrides = null,
    bool LegacyElementsMaterialized = false,
    IReadOnlyList<string>? RemovedSourceElementIds = null,
    ScadaPageType PageType = ScadaPageType.Default,
    SceneBackgroundStyle? Background = null,
    IReadOnlyList<ScadaActionDefinition>? Actions = null,
    bool IncludeInBuild = true,
    string? HeaderPageId = null,
    string? FooterPageId = null)
{
    [JsonIgnore]
    public SceneBackgroundStyle EffectiveBackground => Background ?? SceneBackgroundStyle.FromColor(BackgroundColor);

    [JsonIgnore]
    public IReadOnlyList<ScadaActionDefinition> ActionDefinitions => Actions ?? Array.Empty<ScadaActionDefinition>();

    [JsonIgnore]
    public IReadOnlyList<LegacyTextOverride> TextOverrides => LegacyTextOverrides ?? Array.Empty<LegacyTextOverride>();

    [JsonIgnore]
    public IReadOnlySet<string> RemovedSourceIds => (RemovedSourceElementIds ?? Array.Empty<string>())
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .ToHashSet(StringComparer.Ordinal);

    public static ScadaScene CreateEmpty(string id, string title, CanvasSize canvasSize)
    {
        return new ScadaScene(id, title, canvasSize, Array.Empty<ScadaElement>(), "#000000", Array.Empty<LegacyTextOverride>());
    }

    public ScadaScene WithElement(ScadaElement element)
    {
        return this with { Elements = Elements.Append(element).ToArray() };
    }

    public ScadaScene WithCommittedElementPlusConversion(ScadaElement element)
    {
        var convertedSourceElementIds = GetLegacySourceElementIds(element);
        var elements = Elements
            .Where(existing => existing.Id != element.Id)
            .Where(existing => !GetLegacySourceElementIds(existing).Overlaps(convertedSourceElementIds))
            .Append(element)
            .ToArray();

        var converted = this with { Elements = elements };
        return convertedSourceElementIds.Count == 0
            ? converted
            : converted.WithoutLegacyTextOverrides(convertedSourceElementIds);
    }

    public ScadaScene WithReplacedElement(ScadaElement element)
    {
        return this with
        {
            Elements = Elements.Select(existing => existing.Id == element.Id ? element : existing).ToArray()
        };
    }

    public ScadaScene WithReplacedElementRecursive(ScadaElement element)
    {
        return this with
        {
            Elements = Elements.Select(existing => ReplaceElementRecursive(existing, element)).ToArray()
        };
    }

    public ScadaScene WithoutElement(string elementId)
    {
        return this with
        {
            Elements = Elements.Where(existing => existing.Id != elementId).ToArray()
        };
    }

    public ScadaScene WithoutElementRecursive(string elementId)
    {
        return this with
        {
            Elements = Elements
                .Where(existing => existing.Id != elementId)
                .Select(existing => RemoveElementRecursive(existing, elementId))
                .ToArray()
        };
    }

    public ScadaScene WithoutSceneObjects(IEnumerable<string> elementIds)
    {
        var ids = elementIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0)
        {
            return this;
        }

        return this with
        {
            Elements = Elements
                .Where(existing => !ids.Contains(existing.Id))
                .Select(existing => RemoveElementsRecursive(existing, ids))
                .ToArray()
        };
    }

    public ScadaScene WithRemovedSourceElementIds(IEnumerable<string> sourceElementIds)
    {
        var ids = GetRemovedSourceIdSet();
        foreach (var sourceElementId in sourceElementIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            ids.Add(sourceElementId);
        }

        return this with
        {
            RemovedSourceElementIds = ids.OrderBy(id => id, StringComparer.Ordinal).ToArray()
        };
    }

    public ScadaScene WithoutRemovedSourceElementIds(IEnumerable<string> sourceElementIds)
    {
        var ids = GetRemovedSourceIdSet();
        foreach (var sourceElementId in sourceElementIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            ids.Remove(sourceElementId);
        }

        return this with
        {
            RemovedSourceElementIds = ids.Count == 0
                ? Array.Empty<string>()
                : ids.OrderBy(id => id, StringComparer.Ordinal).ToArray()
        };
    }

    public ScadaElement? FindLegacyStaticBySourceElementId(string sourceElementId)
    {
        return FlattenElements(Elements)
            .FirstOrDefault(element =>
                element.IsLegacyStatic &&
                string.Equals(element.LegacySource?.SourceElementId, sourceElementId, StringComparison.Ordinal));
    }

    public IReadOnlyList<ScadaElement> GetLegacyStaticElements()
    {
        return FlattenElements(Elements)
            .Where(element => element.IsLegacyStatic)
            .ToArray();
    }

    public ScadaScene WithLegacyElementsMaterialized()
    {
        return this with { LegacyElementsMaterialized = true };
    }

    public ScadaElement? FindElementRecursive(string elementId)
    {
        return Elements
            .Select(element => FindElementRecursive(element, elementId))
            .FirstOrDefault(element => element is not null);
    }

    public ScadaElement? FindParentOf(string elementId)
    {
        return Elements
            .Select(element => FindParentRecursive(element, elementId))
            .FirstOrDefault(element => element is not null);
    }

    public ScadaScene WithUngroupedElement(string groupId)
    {
        var group = FindElementRecursive(groupId);
        if (group is null || group.Kind != ScadaElementKind.Group)
        {
            return this;
        }

        if (group.ChildElements.Count == 0)
        {
            return WithoutElementRecursive(groupId);
        }

        var parent = FindParentOf(groupId);
        var ungrouped = group.ChildElements
            .Select(child => child with
            {
                Bounds = new SceneBounds(
                    group.Bounds.X + child.Bounds.X,
                    group.Bounds.Y + child.Bounds.Y,
                    child.Bounds.Width,
                    child.Bounds.Height),
                Layout = parent is null
                    ? ScadaElementLayout.Absolute
                    : new ScadaElementLayout(ElementPositionMode.Relative, parent.Id)
            })
            .ToArray();

        return parent is null
            ? this with
            {
                Elements = Elements
                    .Where(element => element.Id != groupId)
                    .Concat(ungrouped)
                    .ToArray()
            }
            : WithReplacedElementRecursive(parent with
            {
                Children = parent.ChildElements
                    .SelectMany(child => child.Id == groupId ? ungrouped : [child])
                    .ToArray()
            });
    }

    public IReadOnlySet<string> GetConvertedLegacySourceElementIds()
    {
        return Elements
            .Where(element => !element.IsLegacyStatic)
            .SelectMany(GetLegacySourceElementIds)
            .ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlySet<string> GetSuppressedSourceElementIds()
    {
        var ids = GetConvertedLegacySourceElementIds().ToHashSet(StringComparer.Ordinal);
        ids.UnionWith(RemovedSourceIds);
        return ids;
    }

    private HashSet<string> GetRemovedSourceIdSet()
    {
        return (RemovedSourceElementIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
    }

    public ScadaScene WithBackgroundColor(string backgroundColor)
    {
        var normalized = string.IsNullOrWhiteSpace(backgroundColor) ? SceneBackgroundStyle.Default.Color : backgroundColor;
        return this with
        {
            BackgroundColor = normalized,
            Background = EffectiveBackground with { Color = normalized }
        };
    }

    public ScadaScene WithBackground(SceneBackgroundStyle background)
    {
        ArgumentNullException.ThrowIfNull(background);
        return this with
        {
            BackgroundColor = background.Color,
            Background = background
        };
    }

    public ScadaScene WithCanvasSize(CanvasSize canvasSize)
    {
        return this with { CanvasSize = canvasSize };
    }

    public ScadaScene WithPageType(ScadaPageType pageType)
    {
        return this with { PageType = pageType };
    }

    public ScadaScene WithIncludeInBuild(bool includeInBuild)
    {
        return this with { IncludeInBuild = includeInBuild };
    }

    public ScadaScene WithPageComposition(string? headerPageId, string? footerPageId)
    {
        return this with
        {
            HeaderPageId = string.IsNullOrWhiteSpace(headerPageId) ? null : headerPageId,
            FooterPageId = string.IsNullOrWhiteSpace(footerPageId) ? null : footerPageId
        };
    }

    public ScadaScene WithAction(ScadaActionDefinition action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var actions = ActionDefinitions
            .Where(existing => !string.Equals(existing.Id, action.Id, StringComparison.Ordinal))
            .Append(action)
            .ToArray();

        return this with { Actions = actions };
    }

    public ScadaScene WithObjectEvent(string elementId, ScadaObjectEventBinding eventBinding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        ArgumentNullException.ThrowIfNull(eventBinding);

        var element = FindElementRecursive(elementId);
        if (element is null)
        {
            return this;
        }

        var events = element.EventBindings
            .Where(existing =>
                !string.Equals(existing.Trigger, eventBinding.Trigger, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.ActionId, eventBinding.ActionId, StringComparison.Ordinal))
            .Append(eventBinding)
            .ToArray();

        return WithReplacedElementRecursive(element with { Events = events });
    }

    public ScadaScene WithoutLegacyTextOverrides(IEnumerable<string> sourceElementIds)
    {
        var ids = sourceElementIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (ids.Count == 0)
        {
            return this;
        }

        return this with
        {
            LegacyTextOverrides = TextOverrides
                .Where(overrideItem => !ids.Contains(overrideItem.SourceElementId))
                .ToArray()
        };
    }

    public ScadaScene WithoutConvertedLegacyTextOverrides()
    {
        return WithoutLegacyTextOverrides(GetConvertedLegacySourceElementIds());
    }

    public ScadaScene WithLegacyTextOverride(string sourceElementId, string text)
    {
        var normalized = (LegacyTextOverrides ?? Array.Empty<LegacyTextOverride>())
            .Where(overrideItem => overrideItem.SourceElementId != sourceElementId)
            .Append(new LegacyTextOverride(sourceElementId, text))
            .ToArray();

        return this with { LegacyTextOverrides = normalized };
    }

    private static HashSet<string> GetLegacySourceElementIds(ScadaElement element)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(element.LegacySource?.SourceElementId))
        {
            ids.Add(element.LegacySource.SourceElementId);
        }

        foreach (var child in element.ChildElements)
        {
            ids.UnionWith(GetLegacySourceElementIds(child));
        }

        return ids;
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

    private static ScadaElement ReplaceElementRecursive(ScadaElement current, ScadaElement replacement)
    {
        if (current.Id == replacement.Id)
        {
            return replacement;
        }

        return current.ChildElements.Count == 0
            ? current
            : current with
            {
                Children = current.ChildElements
                    .Select(child => ReplaceElementRecursive(child, replacement))
                    .ToArray()
            };
    }

    private static ScadaElement RemoveElementRecursive(ScadaElement current, string elementId)
    {
        return current.ChildElements.Count == 0
            ? current
            : current with
            {
                Children = current.ChildElements
                    .Where(child => child.Id != elementId)
                    .Select(child => RemoveElementRecursive(child, elementId))
                    .ToArray()
            };
    }

    private static ScadaElement RemoveElementsRecursive(ScadaElement current, IReadOnlySet<string> elementIds)
    {
        return current.ChildElements.Count == 0
            ? current
            : current with
            {
                Children = current.ChildElements
                    .Where(child => !elementIds.Contains(child.Id))
                    .Select(child => RemoveElementsRecursive(child, elementIds))
                    .ToArray()
            };
    }

    private static ScadaElement? FindElementRecursive(ScadaElement current, string elementId)
    {
        if (current.Id == elementId)
        {
            return current;
        }

        return current.ChildElements
            .Select(child => FindElementRecursive(child, elementId))
            .FirstOrDefault(child => child is not null);
    }

    private static ScadaElement? FindParentRecursive(ScadaElement current, string elementId)
    {
        if (current.ChildElements.Any(child => child.Id == elementId))
        {
            return current;
        }

        return current.ChildElements
            .Select(child => FindParentRecursive(child, elementId))
            .FirstOrDefault(parent => parent is not null);
    }
}
