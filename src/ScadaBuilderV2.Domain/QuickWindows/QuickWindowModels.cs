using System.Text.Json.Serialization;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Domain.QuickWindows;

/// <summary>
/// Visual content shared between pages and quick windows by composition.
/// Bounded to canvas size, background, elements, styles and assets required for rendering.
/// No navigation, route, header/footer, interface or lifecycle policy is contained here.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-019, FR-020.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §8.2.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowDomainTests.cs.
/// </remarks>
public sealed record VisualContent(
    CanvasSize CanvasSize,
    SceneBackgroundStyle? Background = null,
    IReadOnlyList<ScadaElement>? Elements = null)
{
    /// <summary>Gets the effective canvas size, falling back to desktop default.</summary>
    [JsonIgnore]
    public CanvasSize EffectiveCanvasSize => CanvasSize ?? Projects.CanvasSize.DefaultDesktop;

    /// <summary>Gets the effective background, falling back to default black.</summary>
    [JsonIgnore]
    public SceneBackgroundStyle EffectiveBackground => Background ?? SceneBackgroundStyle.Default;

    /// <summary>Gets the effective element list, empty when not set.</summary>
    [JsonIgnore]
    public IReadOnlyList<ScadaElement> EffectiveElements => Elements ?? Array.Empty<ScadaElement>();

    /// <summary>Creates a VisualContent snapshot from a scene.</summary>
    public static VisualContent FromScene(ScadaScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return new VisualContent(scene.CanvasSize, scene.EffectiveBackground, scene.Elements);
    }

    /// <summary>Projects this visual content into a scene shell for rendering or preview.</summary>
    public ScadaScene ToScene(string id, string title, Guid pageKey = default, string? pageCode = null)
    {
        var effectiveBackground = EffectiveBackground;
        return new ScadaScene(
            Id: string.IsNullOrWhiteSpace(pageCode) ? id : pageCode,
            Title: title,
            CanvasSize: EffectiveCanvasSize,
            Elements: EffectiveElements,
            BackgroundColor: effectiveBackground.Color,
            Background: effectiveBackground,
            PageKey: pageKey,
            PageCode: pageCode ?? id,
            IncludeInBuild: true);
    }
}

/// <summary>
/// Typed interface families for a quick window definition.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-005.
/// </remarks>
public enum QuickWindowInterfaceFamily
{
    ReadState = 0,
    WriteCommand = 1,
    PublicParameter = 2,
    PrivateVariable = 3,
    PrivateConstant = 4
}

/// <summary>
/// Data type for a local interface member.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-005.
/// </remarks>
public enum QuickWindowDataType
{
    Boolean = 0,
    Integer = 1,
    Decimal = 2,
    String = 3,
    Enum = 4
}

/// <summary>
/// Access mode for a local interface member.
/// </summary>
public enum QuickWindowMemberAccess
{
    Read = 0,
    Write = 1,
    ReadWrite = 2,
    Internal = 3
}

/// <summary>
/// Presentation position for a quick window. V1 only supports Center.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-UI-04.
/// </remarks>
public enum QuickWindowPosition
{
    Center = 0
}

/// <summary>
/// Bounded chrome customization for the runtime host frame.
/// X geometry, behaviors, backdrop and guardrails remain host/theme-owned.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-UI-11.
/// </remarks>
public sealed record QuickWindowChrome(
    string? TitleBarColor = null,
    string? BorderColor = null,
    string? Shadow = null);

/// <summary>
/// Presentation defaults for a quick window definition. The host adds chrome outside CanvasSize.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-UI-02, FR-UI-03, FR-UI-04, FR-UI-05, FR-UI-06, FR-017, FR-UI-11.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowDomainTests.cs.
/// </remarks>
public sealed record QuickWindowPresentationDefaults(
    string? Title = null,
    QuickWindowPosition Position = QuickWindowPosition.Center,
    bool Backdrop = true,
    QuickWindowChrome? Chrome = null,
    bool IsDraggable = true,
    bool IsResizable = false,
    bool IsViewportConstrained = true)
{
    /// <summary>Gets effective presentation with defaults when null.</summary>
    public static QuickWindowPresentationDefaults EffectiveOrDefault(QuickWindowPresentationDefaults? value) => value ?? new QuickWindowPresentationDefaults();

    /// <summary>Gets the effective title, falling back to definition DisplayName when null.</summary>
    public string EffectiveTitle(string displayName) => string.IsNullOrWhiteSpace(Title) ? displayName : Title;
}

/// <summary>
/// Defines one quick window interface member. Every member has a stable key, typed contract and family.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-005, FR-006, FR-018.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §8.3.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowDomainTests.cs.
/// </remarks>
public sealed record QuickWindowInterfaceMember(
    Guid MemberKey,
    string Name,
    QuickWindowInterfaceFamily Family,
    QuickWindowDataType DataType,
    QuickWindowMemberAccess Access,
    bool Required = false,
    string? DefaultValue = null,
    string? Description = null)
{
    /// <summary>Gets whether this member is publicly bindable by an invocation.</summary>
    [JsonIgnore]
    public bool IsPublic => Family is QuickWindowInterfaceFamily.ReadState
        or QuickWindowInterfaceFamily.WriteCommand
        or QuickWindowInterfaceFamily.PublicParameter;

    /// <summary>Gets whether this member is private to the window instance.</summary>
    [JsonIgnore]
    public bool IsPrivate => !IsPublic;
}

/// <summary>
/// Persistent quick window definition. PageDefinition and QuickWindowDefinition each compose a VisualContent
/// without inheritance. Identity is DefinitionKey; InstanceKey does not exist.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-001, FR-008, FR-015, FR-019, FR-027.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §1.2, §8.2.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowDomainTests.cs.
/// </remarks>
public sealed record QuickWindowDefinition(
    Guid DefinitionKey,
    string Code,
    string DisplayName,
    int InterfaceVersion,
    VisualContent Content,
    IReadOnlyList<QuickWindowInterfaceMember> InterfaceMembers,
    QuickWindowPresentationDefaults? PresentationDefaults = null)
{
    /// <summary>Gets the effective code, same as PageCodePolicy effective code.</summary>
    [JsonIgnore]
    public string EffectiveCode => string.IsNullOrWhiteSpace(Code) ? DefinitionKey.ToString("N")[..8] : Code;

    /// <summary>Gets effective presentation defaults.</summary>
    [JsonIgnore]
    public QuickWindowPresentationDefaults EffectivePresentation => QuickWindowPresentationDefaults.EffectiveOrDefault(PresentationDefaults);

    /// <summary>Gets the effective visual content with defaults.</summary>
    [JsonIgnore]
    public VisualContent EffectiveContent => Content ?? new VisualContent(Projects.CanvasSize.DefaultDesktop);

    /// <summary>Gets the display title for hosting: Presentation Title or DisplayName fallback.</summary>
    [JsonIgnore]
    public string EffectiveTitle => EffectivePresentation.EffectiveTitle(DisplayName);

    /// <summary>Creates a new empty window definition with default canvas and no members.</summary>
    public static QuickWindowDefinition CreateEmpty(string code, string displayName)
    {
        return new QuickWindowDefinition(
            Guid.NewGuid(),
            code,
            displayName,
            InterfaceVersion: 1,
            Content: new VisualContent(Projects.CanvasSize.DefaultDesktop),
            InterfaceMembers: Array.Empty<QuickWindowInterfaceMember>(),
            PresentationDefaults: new QuickWindowPresentationDefaults());
    }
}

/// <summary>
/// Namespace helper for deterministic DOM/CSS isolation per definition.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-020.
/// </remarks>
public static class QuickWindowNamespace
{
    /// <summary>Computes a stable namespace derived from definition key, e.g. qw-a1b2c3d4.</summary>
    public static string ForDefinition(Guid definitionKey)
    {
        if (definitionKey == Guid.Empty)
            throw new ArgumentException("DefinitionKey must be non-empty.", nameof(definitionKey));
        return $"qw-{definitionKey.ToString("N")[..8]}";
    }

    /// <summary>Builds a host root attribute set for one instance.</summary>
    public static IReadOnlyDictionary<string, string> RootAttributes(Guid definitionKey, Guid invocationKey, Guid runtimeInstanceId, long generation)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["data-qw-def"] = ForDefinition(definitionKey),
            ["data-qw-inv"] = invocationKey.ToString("N"),
            ["data-qw-inst"] = runtimeInstanceId.ToString("N"),
            ["data-qw-generation"] = generation.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }
}
