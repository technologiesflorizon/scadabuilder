using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.State;
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

/// <summary>
/// Identifies the standard Element+ shape primitive rendered by preview and FT100 export.
/// </summary>
public enum ScadaShapeKind
{
    /// <summary>
    /// Rectangular Element+ shape.
    /// </summary>
    Rectangle,

    /// <summary>
    /// Rectangular Element+ shape with rounded corners.
    /// </summary>
    RoundedRectangle,

    /// <summary>
    /// Ellipse or circle Element+ shape.
    /// </summary>
    Ellipse,

    /// <summary>
    /// Straight line Element+ shape.
    /// </summary>
    Line,

    /// <summary>
    /// Straight arrow Element+ shape.
    /// </summary>
    Arrow,

    /// <summary>
    /// Circular Element+ shape.
    /// </summary>
    Circle,

    /// <summary>
    /// Triangular Element+ shape.
    /// </summary>
    Triangle,

    /// <summary>
    /// Star Element+ shape.
    /// </summary>
    Star,

    /// <summary>
    /// Round HMI/SCADA status indicator lamp.
    /// </summary>
    IndicatorLamp,

    /// <summary>
    /// Horizontal HMI/SCADA value bar using data value as a percentage.
    /// </summary>
    HorizontalBar,

    /// <summary>
    /// Vertical HMI/SCADA value bar using data value as a percentage.
    /// </summary>
    VerticalBar,

    /// <summary>
    /// HMI/SCADA process tank symbol using data value as a fill percentage.
    /// </summary>
    Tank,

    /// <summary>
    /// Horizontal HMI/SCADA pipe segment.
    /// </summary>
    PipeHorizontal,

    /// <summary>
    /// Vertical HMI/SCADA pipe segment.
    /// </summary>
    PipeVertical,

    /// <summary>
    /// HMI/SCADA valve symbol.
    /// </summary>
    Valve,

    /// <summary>
    /// HMI/SCADA pump symbol.
    /// </summary>
    Pump,

    /// <summary>
    /// HMI/SCADA electric motor symbol.
    /// </summary>
    Motor,

    /// <summary>
    /// HMI/SCADA fan symbol.
    /// </summary>
    Fan,

    /// <summary>
    /// HMI/SCADA conveyor symbol.
    /// </summary>
    Conveyor,

    /// <summary>
    /// HMI/SCADA gauge or meter symbol.
    /// </summary>
    Gauge,

    /// <summary>
    /// HMI/SCADA electrical switch symbol.
    /// </summary>
    Switch,

    /// <summary>
    /// HMI/SCADA circuit breaker symbol.
    /// </summary>
    Breaker,

    /// <summary>
    /// HMI/SCADA transformer symbol.
    /// </summary>
    Transformer,

    /// <summary>
    /// HMI/SCADA alarm beacon symbol.
    /// </summary>
    AlarmBeacon
}

/// <summary>
/// Identifies the standard HMI/SCADA button preset rendered by preview and FT100 export.
/// </summary>
public enum ScadaButtonKind
{
    /// <summary>
    /// Generic command button.
    /// </summary>
    Command,

    /// <summary>
    /// Toggle button used for on/off operator commands.
    /// </summary>
    Toggle,

    /// <summary>
    /// Navigation button used to move between screens or popups.
    /// </summary>
    Navigation,

    /// <summary>
    /// Alarm acknowledgement button.
    /// </summary>
    AlarmAcknowledge,

    /// <summary>
    /// Emergency stop style button.
    /// </summary>
    EmergencyStop
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
    string? AdvancedCss,
    double Opacity = 1,
    double Rotation = 0,
    bool FlipHorizontally = false,
    bool FlipVertically = false,
    string FontWeight = "Normal",
    string FontStyle = "Normal",
    IReadOnlyList<string>? TextDecoration = null,
    string TextAlign = "Left",
    string TextTransform = "None",
    double LetterSpacing = 0,
    double LineHeight = 0,
    ScadaBorderRadius? BorderRadius = null)
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

/// <summary>
/// CSS border radius values for the four corners of an Element+ object, in pixels.
/// </summary>
/// <remarks>
/// Decisions: D13.
/// Contracts: docs/superpowers/specs/2026-07-13-element-plus-style-capability-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ScadaSceneModelsTests.cs.
/// </remarks>
public sealed record ScadaBorderRadius(
    double TopLeft = 0,
    double TopRight = 0,
    double BottomRight = 0,
    double BottomLeft = 0)
{
    [JsonIgnore]
    public bool IsUniform =>
        Math.Abs(TopLeft - TopRight) < 0.01 &&
        Math.Abs(TopRight - BottomRight) < 0.01 &&
        Math.Abs(BottomRight - BottomLeft) < 0.01;

    public ScadaBorderRadius Normalized() => new(
        Math.Max(0, TopLeft),
        Math.Max(0, TopRight),
        Math.Max(0, BottomRight),
        Math.Max(0, BottomLeft));

    public static ScadaBorderRadius None { get; } = new();
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
    bool IsReadOnly,
    string? ReadTagId = null,
    string? WriteTagId = null,
    double? ShapeStartX = null,
    double? ShapeStartY = null,
    double? ShapeEndX = null,
    double? ShapeEndY = null);

/// <summary>
/// Identifies which runtime tag value binding is edited on an Element+ object.
/// </summary>
public enum ScadaValueBindingKind
{
    /// <summary>
    /// Runtime binding that reads a tag value into the Element+ object.
    /// </summary>
    Read,

    /// <summary>
    /// Runtime binding that writes the operator-entered Element+ value to a tag.
    /// </summary>
    Write
}

/// <summary>
/// Defines the hover visual style for an Element+ button.
/// </summary>
/// <param name="Enabled">Whether hover styling is active for the button when the button is not disabled.</param>
/// <param name="Background">CSS background applied on hover.</param>
/// <param name="Foreground">CSS foreground applied on hover.</param>
/// <param name="BorderColor">CSS border color applied on hover.</param>
public sealed record ScadaButtonHoverStyle(
    bool Enabled,
    string Background,
    string Foreground,
    string BorderColor)
{
    /// <summary>
    /// Gets the default industrial HMI hover treatment for active Element+ buttons.
    /// </summary>
    public static ScadaButtonHoverStyle Default { get; } = new(true, "#EAF5F7", "#0F2A30", "#2090A0");
}

/// <summary>
/// Defines the pressed or active visual style for an Element+ button.
/// </summary>
/// <param name="Enabled">Whether pressed styling is active for the button when the button is not disabled.</param>
/// <param name="Background">CSS background applied while pressed or active.</param>
/// <param name="Foreground">CSS foreground applied while pressed or active.</param>
/// <param name="BorderColor">CSS border color applied while pressed or active.</param>
public sealed record ScadaButtonPressedStyle(
    bool Enabled,
    string Background,
    string Foreground,
    string BorderColor)
{
    /// <summary>
    /// Gets the default industrial HMI pressed treatment for active Element+ buttons.
    /// </summary>
    public static ScadaButtonPressedStyle Default { get; } = new(true, "#0F7280", "#FFFFFF", "#0F2A30");
}

/// <summary>
/// Defines button-specific runtime behavior for an Element+ button.
/// </summary>
/// <param name="IsDisabled">Whether the button is disabled in preview/export runtime.</param>
/// <param name="Hover">Optional hover style override. A null value uses the default hover style.</param>
/// <param name="Pressed">Optional pressed style override. A null value uses the default pressed style.</param>
public sealed record ScadaButtonBehavior(
    bool IsDisabled,
    ScadaButtonHoverStyle? Hover = null,
    ScadaButtonPressedStyle? Pressed = null)
{
    /// <summary>
    /// Gets the default behavior for enabled Element+ buttons.
    /// </summary>
    public static ScadaButtonBehavior Default { get; } = new(false, ScadaButtonHoverStyle.Default, ScadaButtonPressedStyle.Default);

    /// <summary>
    /// Gets the effective hover style, including defaults for older scenes.
    /// </summary>
    public ScadaButtonHoverStyle EffectiveHover => Hover ?? ScadaButtonHoverStyle.Default;

    /// <summary>
    /// Gets the effective pressed style, including defaults for older scenes.
    /// </summary>
    public ScadaButtonPressedStyle EffectivePressed => Pressed ?? ScadaButtonPressedStyle.Default;
}

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
    MountFragment,
    ClosePopup,
    TogglePopup,
    ReadValue,
    WriteValue
}

/// <summary>
/// Supported deterministic comparison operators for tag-backed runtime conditions.
/// </summary>
public enum ScadaConditionOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    True,
    False
}

/// <summary>
/// Determines how several runtime tag conditions are combined.
/// </summary>
public enum ScadaConditionGroupMode
{
    All,
    Any
}

/// <summary>
/// Determines how runtime condition evaluation behaves when a required tag value is unavailable.
/// </summary>
public enum ScadaMissingConditionPolicy
{
    BlockAction,
    AllowAction
}

/// <summary>
/// Describes a deterministic tag condition attached to a runtime action.
/// </summary>
/// <remarks>
/// Decisions: DEC-0017.
/// Contracts: docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs, tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.
/// </remarks>
public sealed record ScadaActionCondition(
    string TagId,
    ScadaConditionOperator Operator,
    string? CompareValue = null);

/// <summary>
/// Describes a deterministic group of tag conditions attached to a runtime action.
/// </summary>
/// <remarks>
/// Decisions: DEC-0023.
/// Contracts: docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs, tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.
/// </remarks>
public sealed record ScadaActionConditionGroup(
    IReadOnlyList<ScadaActionCondition> Conditions,
    ScadaConditionGroupMode Mode = ScadaConditionGroupMode.All,
    ScadaMissingConditionPolicy MissingTagPolicy = ScadaMissingConditionPolicy.BlockAction);

/// <summary>
/// Supported popup placement presets for FT100/TF100Web runtime overlays.
/// </summary>
public enum ScadaPopupPosition
{
    Center,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    DockLeft,
    DockRight,
    DockTop,
    DockBottom,
    HostRegion
}

/// <summary>
/// Supported popup size presets for compiled fragment popup actions.
/// </summary>
public enum ScadaPopupSizePreset
{
    Small,
    Medium,
    Large,
    Fullscreen
}

/// <summary>
/// Describes advanced runtime behavior for popup fragment actions.
/// </summary>
/// <remarks>
/// Decisions: DEC-0022.
/// Contracts: docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md, docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs, tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs, tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.
/// </remarks>
public sealed record ScadaPopupOptions(
    ScadaPopupPosition Position = ScadaPopupPosition.Center,
    ScadaPopupSizePreset SizePreset = ScadaPopupSizePreset.Large,
    bool AllowMultiple = false,
    bool ResetOnOpen = true,
    string? HostRegionId = null);

public sealed record ScadaActionDefinition(
    string Id,
    ScadaActionKind Kind,
    string? TargetPageId = null,
    string? TargetElementId = null,
    string? ClassName = null,
    string? TagId = null,
    string? Value = null,
    ScadaActionCondition? Condition = null,
    ScadaPopupOptions? PopupOptions = null,
    ScadaActionConditionGroup? ConditionGroup = null);

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
    IReadOnlyList<ScadaObjectEventBinding>? Events = null,
    ScadaButtonBehavior? ButtonBehavior = null,
    ScadaShapeKind? ShapeKind = null,
    ScadaButtonKind? ButtonKind = null,
    ScadaElementStateConfig? StateConfig = null,
    ScadaElementCommandConfig? CommandConfig = null)
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

    [JsonIgnore]
    public ScadaElementStateConfig EffectiveStateConfig => StateConfig ?? ScadaElementStateConfig.Default;

    [JsonIgnore]
    public ScadaElementCommandConfig EffectiveCommandConfig => CommandConfig ?? ScadaElementCommandConfig.Default;

    /// <summary>
    /// Gets whether this Element+ can provide an operator-entered runtime value.
    /// </summary>
    [JsonIgnore]
    public bool IsWritableInput => (Kind == ScadaElementKind.InputText || Kind == ScadaElementKind.InputNumeric) &&
        Data?.IsReadOnly != true;

    /// <summary>
    /// Gets the effective button behavior, including default hover feedback for older scenes.
    /// </summary>
    [JsonIgnore]
    public ScadaButtonBehavior EffectiveButtonBehavior => Kind == ScadaElementKind.Button
        ? ButtonBehavior ?? ScadaButtonBehavior.Default
        : ScadaButtonBehavior.Default;

    /// <summary>
    /// Gets the persisted button preset, defaulting older button elements to command buttons.
    /// </summary>
    [JsonIgnore]
    public ScadaButtonKind EffectiveButtonKind => Kind == ScadaElementKind.Button
        ? ButtonKind ?? ScadaButtonKind.Command
        : ScadaButtonKind.Command;

    /// <summary>
    /// Gets the persisted shape primitive, defaulting older shape elements to rectangles.
    /// </summary>
    [JsonIgnore]
    public ScadaShapeKind EffectiveShapeKind => Kind == ScadaElementKind.Shape
        ? ShapeKind ?? ScadaShapeKind.Rectangle
        : ScadaShapeKind.Rectangle;

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

    /// <summary>
    /// Creates a model-backed standard Element+ shape for scene insertion.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0004, DEC-0008.
    /// Contracts: docs/05_studio_element_plus/STUDIO_ELEMENT_PLUS_SEP_CONTRACT_V2.md.
    /// Tests: tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs, tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.
    /// </remarks>
    public static ScadaElement CreateShape(string id, string displayName, ScadaShapeKind shapeKind, double x, double y)
    {
        var bounds = shapeKind switch
        {
            ScadaShapeKind.Line or ScadaShapeKind.Arrow => new SceneBounds(x, y, 140, 32),
            ScadaShapeKind.IndicatorLamp => new SceneBounds(x, y, 64, 64),
            ScadaShapeKind.HorizontalBar => new SceneBounds(x, y, 160, 32),
            ScadaShapeKind.VerticalBar => new SceneBounds(x, y, 48, 140),
            ScadaShapeKind.Tank => new SceneBounds(x, y, 96, 140),
            ScadaShapeKind.PipeHorizontal => new SceneBounds(x, y, 160, 32),
            ScadaShapeKind.PipeVertical => new SceneBounds(x, y, 32, 160),
            ScadaShapeKind.Valve => new SceneBounds(x, y, 96, 64),
            ScadaShapeKind.Pump => new SceneBounds(x, y, 96, 72),
            ScadaShapeKind.Motor => new SceneBounds(x, y, 96, 72),
            ScadaShapeKind.Fan => new SceneBounds(x, y, 88, 88),
            ScadaShapeKind.Conveyor => new SceneBounds(x, y, 180, 56),
            ScadaShapeKind.Gauge => new SceneBounds(x, y, 88, 88),
            ScadaShapeKind.Switch => new SceneBounds(x, y, 96, 56),
            ScadaShapeKind.Breaker => new SceneBounds(x, y, 96, 72),
            ScadaShapeKind.Transformer => new SceneBounds(x, y, 112, 80),
            ScadaShapeKind.AlarmBeacon => new SceneBounds(x, y, 72, 88),
            ScadaShapeKind.Ellipse => new SceneBounds(x, y, 96, 72),
            ScadaShapeKind.Circle => new SceneBounds(x, y, 88, 88),
            ScadaShapeKind.Triangle or ScadaShapeKind.Star => new SceneBounds(x, y, 96, 88),
            _ => new SceneBounds(x, y, 120, 72)
        };
        var data = shapeKind is ScadaShapeKind.HorizontalBar or ScadaShapeKind.VerticalBar or ScadaShapeKind.Tank or ScadaShapeKind.Gauge
            ? new ScadaElementData(null, null, 65, 0, 100, null, null, "0", null, false)
            : new ScadaElementData(null, null, null, null, null, null, null, null, null, false);
        return new ScadaElement(
            id,
            displayName,
            ScadaElementKind.Shape,
            bounds,
            null,
            ScadaElementLayout.Absolute,
            new ScadaElementStyle(
                "Segoe UI",
                14,
                "#0F2A30",
                shapeKind is ScadaShapeKind.Line or ScadaShapeKind.Arrow ? "Transparent" : "#DFF3E7",
                "#2090A0",
                2,
                "Solid",
                "None",
                null),
            data,
            ShapeKind: shapeKind);
    }

    /// <summary>
    /// Creates a model-backed Element+ button for scene insertion.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0012.
    /// Contracts: docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md.
    /// Tests: tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs.
    /// </remarks>
    public static ScadaElement CreateButton(string id, string displayName, double x, double y, ScadaButtonKind buttonKind = ScadaButtonKind.Command)
    {
        var (bounds, style, text) = CreateButtonDefaults(buttonKind, displayName, x, y);
        return new ScadaElement(
            id,
            displayName,
            ScadaElementKind.Button,
            bounds,
            null,
            ScadaElementLayout.Absolute,
            style,
            new ScadaElementData(text, null, null, null, null, null, null, buttonKind.ToString(), null, false),
            ButtonBehavior: ScadaButtonBehavior.Default,
            ButtonKind: buttonKind);
    }

    private static (SceneBounds Bounds, ScadaElementStyle Style, string Text) CreateButtonDefaults(
        ScadaButtonKind buttonKind,
        string displayName,
        double x,
        double y)
    {
        var bounds = buttonKind switch
        {
            ScadaButtonKind.Navigation => new SceneBounds(x, y, 140, 40),
            ScadaButtonKind.AlarmAcknowledge => new SceneBounds(x, y, 132, 40),
            ScadaButtonKind.EmergencyStop => new SceneBounds(x, y, 96, 96),
            _ => new SceneBounds(x, y, 120, 40)
        };
        var style = buttonKind switch
        {
            ScadaButtonKind.Toggle => ScadaElementStyle.DefaultInput with
            {
                Background = "#DFF3E7",
                BorderColor = "#2090A0",
                BorderWidth = 2
            },
            ScadaButtonKind.Navigation => ScadaElementStyle.DefaultInput with
            {
                Background = "#EAF5F7",
                BorderColor = "#2090A0",
                BorderWidth = 2
            },
            ScadaButtonKind.AlarmAcknowledge => ScadaElementStyle.DefaultInput with
            {
                Background = "#FFF7D6",
                BorderColor = "#C78A00",
                BorderWidth = 2
            },
            ScadaButtonKind.EmergencyStop => ScadaElementStyle.DefaultInput with
            {
                Foreground = "#FFFFFF",
                Background = "#C62828",
                BorderColor = "#7F1515",
                BorderWidth = 3,
                ShadowPreset = "Raised"
            },
            _ => ScadaElementStyle.DefaultInput
        };
        var text = buttonKind switch
        {
            ScadaButtonKind.Toggle => "Marche / Arret",
            ScadaButtonKind.Navigation => "Navigation",
            ScadaButtonKind.AlarmAcknowledge => "Acquitter",
            ScadaButtonKind.EmergencyStop => "STOP",
            _ => displayName
        };
        return (bounds, style, text);
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

    public ScadaScene WithElementBroughtForward(string elementId)
    {
        var list = Elements.ToList();
        var idx = list.FindIndex(e => e.Id == elementId);
        if (idx < 0 || idx >= list.Count - 1) return this;
        (list[idx], list[idx + 1]) = (list[idx + 1], list[idx]);
        return this with { Elements = list };
    }

    public ScadaScene WithElementSentBackward(string elementId)
    {
        var list = Elements.ToList();
        var idx = list.FindIndex(e => e.Id == elementId);
        if (idx <= 0) return this;
        (list[idx], list[idx - 1]) = (list[idx - 1], list[idx]);
        return this with { Elements = list };
    }

    public ScadaScene WithElementBroughtToFront(string elementId)
    {
        var list = Elements.ToList();
        var idx = list.FindIndex(e => e.Id == elementId);
        if (idx < 0 || idx == list.Count - 1) return this;
        var element = list[idx];
        list.RemoveAt(idx);
        list.Add(element);
        return this with { Elements = list };
    }

    public ScadaScene WithElementSentToBack(string elementId)
    {
        var list = Elements.ToList();
        var idx = list.FindIndex(e => e.Id == elementId);
        if (idx <= 0) return this;
        var element = list[idx];
        list.RemoveAt(idx);
        list.Insert(0, element);
        return this with { Elements = list };
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

    /// <summary>
    /// Groups two or more existing Element+ scene objects under a new group while preserving visual positions.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0006, DEC-0010.
    /// Contracts: docs/04_editor/COMMANDS_CONTRACT_V2.md, docs/04_editor/SELECTION_CONTRACT_V2.md.
    /// Tests: tests/ScadaBuilderV2.Tests/ScadaSceneGroupTests.cs.
    /// </remarks>
    public ScadaScene WithGroupedElements(string groupId, string groupName, IEnumerable<string> elementIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        var selectedIds = elementIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (selectedIds.Length < 2)
        {
            throw new InvalidOperationException("A group requires at least two Element+ objects.");
        }

        if (selectedIds.Contains(groupId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("A group cannot contain itself.");
        }

        var selected = selectedIds
            .Select(id => new SceneElementSelection(
                FindElementRecursive(id) ?? throw new InvalidOperationException($"Element '{id}' was not found."),
                FindParentOf(id),
                GetAbsoluteBounds(id)))
            .ToArray();

        if (selected.Any(item => item.Element.IsLegacyStatic))
        {
            throw new InvalidOperationException("Legacy source elements must be converted to Element+ before grouping.");
        }

        foreach (var item in selected)
        {
            if (selected.Any(candidate =>
                !string.Equals(candidate.Element.Id, item.Element.Id, StringComparison.Ordinal) &&
                ContainsElement(item.Element, candidate.Element.Id)))
            {
                throw new InvalidOperationException("A group selection cannot contain both a group and one of its descendants.");
            }
        }

        var parentKeys = selected
            .Select(item => item.Parent?.Id ?? "")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (parentKeys.Length != 1)
        {
            throw new InvalidOperationException("Grouped Element+ objects must share the same parent.");
        }

        var parent = selected[0].Parent;
        var siblings = parent is null ? Elements : parent.ChildElements;
        var siblingIndexes = siblings
            .Select((element, index) => new { element.Id, Index = index })
            .ToDictionary(item => item.Id, item => item.Index, StringComparer.Ordinal);
        selected = selected
            .Select(item => item with { SiblingIndex = siblingIndexes[item.Element.Id] })
            .OrderBy(item => item.SiblingIndex)
            .ToArray();
        var groupBoundsAbsolute = CalculateOuterBounds(selected.Select(item => item.AbsoluteBounds));
        var parentAbsoluteBounds = parent is null ? new SceneBounds(0, 0, 0, 0) : GetAbsoluteBounds(parent.Id);
        var groupBounds = new SceneBounds(
            groupBoundsAbsolute.X - parentAbsoluteBounds.X,
            groupBoundsAbsolute.Y - parentAbsoluteBounds.Y,
            groupBoundsAbsolute.Width,
            groupBoundsAbsolute.Height);
        var children = selected
            .Select(item => item.Element with
            {
                Bounds = new SceneBounds(
                    item.AbsoluteBounds.X - groupBoundsAbsolute.X,
                    item.AbsoluteBounds.Y - groupBoundsAbsolute.Y,
                    item.AbsoluteBounds.Width,
                    item.AbsoluteBounds.Height),
                Layout = new ScadaElementLayout(ElementPositionMode.Relative, groupId)
            })
            .ToArray();

        var group = new ScadaElement(
            groupId,
            string.IsNullOrWhiteSpace(groupName) ? groupId : groupName,
            ScadaElementKind.Group,
            groupBounds,
            null,
            parent is null
                ? ScadaElementLayout.Absolute
                : new ScadaElementLayout(ElementPositionMode.Relative, parent.Id),
            ScadaElementStyle.DefaultText with
            {
                Background = "Transparent",
                BorderColor = "#2090A0",
                BorderWidth = 1,
                BorderStyle = "Dashed"
            },
            null,
            children);

        var selectedIdSet = selectedIds.ToHashSet(StringComparer.Ordinal);
        var groupInsertionIndex = selected.Max(item => item.SiblingIndex);
        return parent is null
            ? this with { Elements = ReplaceSelectedSiblingsWithGroup(Elements, selectedIdSet, group, groupInsertionIndex) }
            : WithReplacedElementRecursive(parent with
            {
                Children = ReplaceSelectedSiblingsWithGroup(parent.ChildElements, selectedIdSet, group, groupInsertionIndex)
            });
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

    /// <summary>
    /// Adds or replaces model-backed runtime value tag bindings on one Element+ object.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0016.
    /// Contracts: docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md.
    /// Tests: tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs, tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs.
    /// </remarks>
    public ScadaScene WithValueBinding(string elementId, string? readTagId = null, string? writeTagId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);

        var element = FindElementRecursive(elementId);
        if (element is null)
        {
            return this;
        }

        var data = element.Data ?? CreateDefaultElementData(element);
        var updatedData = data with
        {
            ReadTagId = NormalizeOptionalTagId(readTagId, data.ReadTagId),
            WriteTagId = NormalizeOptionalTagId(writeTagId, data.WriteTagId)
        };

        return WithReplacedElementRecursive(element with { Data = updatedData });
    }

    /// <summary>
    /// Replaces the display-state configuration of one Element+ object.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0036.
    /// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
    /// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaSceneElementEventsTests.cs.
    /// </remarks>
    public ScadaScene WithElementStateConfig(string elementId, ScadaElementStateConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        ArgumentNullException.ThrowIfNull(config);

        var element = FindElementRecursive(elementId);
        if (element is null)
        {
            return this;
        }

        return WithReplacedElementRecursive(element with { StateConfig = config });
    }

    /// <summary>
    /// Replaces the command configuration of one Element+ object.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0036.
    /// Contracts: docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md.
    /// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ScadaSceneElementEventsTests.cs.
    /// </remarks>
    public ScadaScene WithElementCommandConfig(string elementId, ScadaElementCommandConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        ArgumentNullException.ThrowIfNull(config);

        var element = FindElementRecursive(elementId);
        if (element is null)
        {
            return this;
        }

        return WithReplacedElementRecursive(element with { CommandConfig = config });
    }

    /// <summary>
    /// Removes one model-backed runtime value binding from one Element+ object.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0016.
    /// Contracts: docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md.
    /// Tests: tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs, tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs.
    /// </remarks>
    public ScadaScene WithoutValueBinding(string elementId, ScadaValueBindingKind bindingKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);

        var element = FindElementRecursive(elementId);
        if (element?.Data is null)
        {
            return this;
        }

        var updatedData = bindingKind == ScadaValueBindingKind.Read
            ? element.Data with { ReadTagId = null }
            : element.Data with { WriteTagId = null };

        return WithReplacedElementRecursive(element with { Data = updatedData });
    }

    /// <summary>
    /// Removes one model-backed Element+ event by index and prunes its generated action when no event references it.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0011.
    /// Contracts: docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md.
    /// Tests: tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs.
    /// </remarks>
    public ScadaScene WithoutObjectEventAt(string elementId, int eventIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);

        var element = FindElementRecursive(elementId);
        if (element is null || eventIndex < 0 || eventIndex >= element.EventBindings.Count)
        {
            return this;
        }

        var removed = element.EventBindings[eventIndex];
        var events = element.EventBindings
            .Where((_, index) => index != eventIndex)
            .ToArray();
        var updatedScene = WithReplacedElementRecursive(element with { Events = events });
        var removedActionStillReferenced = FlattenElements(updatedScene.Elements)
            .SelectMany(existing => existing.EventBindings)
            .Any(binding => string.Equals(binding.ActionId, removed.ActionId, StringComparison.Ordinal));
        return removedActionStillReferenced
            ? updatedScene
            : updatedScene with
            {
                Actions = updatedScene.ActionDefinitions
                    .Where(action => !string.Equals(action.Id, removed.ActionId, StringComparison.Ordinal))
                    .ToArray()
            };
    }

    /// <summary>
    /// Adds a model-backed Element+ event that navigates to another compiled page.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0011.
    /// Contracts: docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md.
    /// Tests: tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs, tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.
    /// </remarks>
    public ScadaScene WithChangePageEvent(string elementId, string triggerKeyOrRuntimeName, string targetPageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerKeyOrRuntimeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPageId);

        var trigger = ScadaEventRegistry.FindTrigger(triggerKeyOrRuntimeName) ??
            throw new InvalidOperationException($"Event trigger '{triggerKeyOrRuntimeName}' is not registered.");
        var action = new ScadaActionDefinition(
            CreateActionId(elementId, trigger.RuntimeTrigger, ScadaEventRegistry.ChangePageFunction, targetPageId),
            ScadaActionKind.Navigate,
            TargetPageId: targetPageId.Trim());

        return WithAction(action)
            .WithObjectEvent(
                elementId,
                new ScadaObjectEventBinding(
                    trigger.RuntimeTrigger,
                    action.Id,
                    StopPropagation: true,
                    PreventDefault: false));
    }

    /// <summary>
    /// Adds a model-backed Element+ event that opens a compiled fragment page as a runtime popup.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0019.
    /// Contracts: docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md.
    /// Tests: tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs, tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.
    /// </remarks>
    public ScadaScene WithOpenPopupEvent(
        string elementId,
        string triggerKeyOrRuntimeName,
        string targetPageId,
        ScadaPopupOptions? popupOptions = null)
    {
        return WithPopupEvent(elementId, triggerKeyOrRuntimeName, ScadaActionKind.MountFragment, targetPageId, popupOptions);
    }

    /// <summary>
    /// Adds a model-backed Element+ event that closes a compiled fragment runtime popup.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0020.
    /// Contracts: docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md.
    /// Tests: tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs, tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.
    /// </remarks>
    public ScadaScene WithClosePopupEvent(
        string elementId,
        string triggerKeyOrRuntimeName,
        string targetPageId,
        ScadaPopupOptions? popupOptions = null)
    {
        return WithPopupEvent(elementId, triggerKeyOrRuntimeName, ScadaActionKind.ClosePopup, targetPageId, popupOptions);
    }

    /// <summary>
    /// Adds a model-backed Element+ event that toggles a compiled fragment runtime popup.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0020.
    /// Contracts: docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md.
    /// Tests: tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs, tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.
    /// </remarks>
    public ScadaScene WithTogglePopupEvent(
        string elementId,
        string triggerKeyOrRuntimeName,
        string targetPageId,
        ScadaPopupOptions? popupOptions = null)
    {
        return WithPopupEvent(elementId, triggerKeyOrRuntimeName, ScadaActionKind.TogglePopup, targetPageId, popupOptions);
    }

    // Centralizes popup action creation so every popup function keeps the same fragment target contract.
    private ScadaScene WithPopupEvent(
        string elementId,
        string triggerKeyOrRuntimeName,
        ScadaActionKind actionKind,
        string targetPageId,
        ScadaPopupOptions? popupOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerKeyOrRuntimeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPageId);

        if (actionKind is not (ScadaActionKind.MountFragment or ScadaActionKind.ClosePopup or ScadaActionKind.TogglePopup))
        {
            throw new InvalidOperationException($"Action kind '{actionKind}' is not a popup action.");
        }

        var trigger = ScadaEventRegistry.FindTrigger(triggerKeyOrRuntimeName) ??
            throw new InvalidOperationException($"Event trigger '{triggerKeyOrRuntimeName}' is not registered.");
        var functionName = actionKind switch
        {
            ScadaActionKind.ClosePopup => ScadaEventRegistry.ClosePopupFunction,
            ScadaActionKind.TogglePopup => ScadaEventRegistry.TogglePopupFunction,
            _ => ScadaEventRegistry.OpenPopupFunction
        };
        var action = new ScadaActionDefinition(
            CreateActionId(elementId, trigger.RuntimeTrigger, functionName, targetPageId),
            actionKind,
            TargetPageId: targetPageId.Trim(),
            PopupOptions: popupOptions);

        return WithAction(action)
            .WithObjectEvent(
                elementId,
                new ScadaObjectEventBinding(
                    trigger.RuntimeTrigger,
                    action.Id,
                    StopPropagation: true,
                    PreventDefault: false));
    }

    /// <summary>
    /// Adds a model-backed Element+ event that changes target object visibility.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0017.
    /// Contracts: docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md.
    /// Tests: tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs, tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.
    /// </remarks>
    public ScadaScene WithObjectVisibilityEvent(
        string elementId,
        string triggerKeyOrRuntimeName,
        ScadaActionKind actionKind,
        string targetElementId,
        ScadaActionCondition? condition = null,
        ScadaActionConditionGroup? conditionGroup = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerKeyOrRuntimeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetElementId);

        if (actionKind is not (ScadaActionKind.Show or ScadaActionKind.Hide or ScadaActionKind.ToggleVisibility))
        {
            throw new InvalidOperationException($"Action kind '{actionKind}' is not an object visibility action.");
        }

        var trigger = ScadaEventRegistry.FindTrigger(triggerKeyOrRuntimeName) ??
            throw new InvalidOperationException($"Event trigger '{triggerKeyOrRuntimeName}' is not registered.");
        var functionName = actionKind switch
        {
            ScadaActionKind.Show => ScadaEventRegistry.ShowFunction,
            ScadaActionKind.Hide => ScadaEventRegistry.HideFunction,
            _ => ScadaEventRegistry.ToggleVisibilityFunction
        };
        var conditionId = condition is null
            ? null
            : string.Join("_", new[] { condition.TagId, condition.Operator.ToString(), condition.CompareValue }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var targetId = string.Join("_", new[] { targetElementId, conditionId }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var action = new ScadaActionDefinition(
            CreateActionId(elementId, trigger.RuntimeTrigger, functionName, targetId),
            actionKind,
            TargetElementId: targetElementId.Trim(),
            Condition: condition,
            ConditionGroup: conditionGroup);

        return WithAction(action)
            .WithObjectEvent(
                elementId,
                new ScadaObjectEventBinding(
                    trigger.RuntimeTrigger,
                    action.Id,
                    StopPropagation: true,
            PreventDefault: false));
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

    private SceneBounds GetAbsoluteBounds(string elementId)
    {
        foreach (var element in Elements)
        {
            if (TryGetAbsoluteBounds(element, elementId, 0, 0, out var bounds))
            {
                return bounds;
            }
        }

        throw new InvalidOperationException($"Element '{elementId}' was not found.");
    }

    private static bool TryGetAbsoluteBounds(
        ScadaElement current,
        string elementId,
        double offsetX,
        double offsetY,
        out SceneBounds bounds)
    {
        var currentAbsoluteX = offsetX + current.Bounds.X;
        var currentAbsoluteY = offsetY + current.Bounds.Y;
        if (string.Equals(current.Id, elementId, StringComparison.Ordinal))
        {
            bounds = new SceneBounds(currentAbsoluteX, currentAbsoluteY, current.Bounds.Width, current.Bounds.Height);
            return true;
        }

        foreach (var child in current.ChildElements)
        {
            if (TryGetAbsoluteBounds(child, elementId, currentAbsoluteX, currentAbsoluteY, out bounds))
            {
                return true;
            }
        }

        bounds = new SceneBounds(0, 0, 0, 0);
        return false;
    }

    private static SceneBounds CalculateOuterBounds(IEnumerable<SceneBounds> bounds)
    {
        var items = bounds.ToArray();
        var left = items.Min(item => item.X);
        var top = items.Min(item => item.Y);
        var right = items.Max(item => item.X + item.Width);
        var bottom = items.Max(item => item.Y + item.Height);
        return new SceneBounds(left, top, right - left, bottom - top);
    }

    private static IReadOnlyList<ScadaElement> ReplaceSelectedSiblingsWithGroup(
        IReadOnlyList<ScadaElement> siblings,
        IReadOnlySet<string> selectedIds,
        ScadaElement group,
        int groupInsertionIndex)
    {
        var result = new List<ScadaElement>();
        var insertedGroup = false;
        for (var index = 0; index < siblings.Count; index++)
        {
            var sibling = siblings[index];
            if (!selectedIds.Contains(sibling.Id))
            {
                result.Add(sibling);
                continue;
            }

            if (!insertedGroup && index == groupInsertionIndex)
            {
                result.Add(group);
                insertedGroup = true;
            }
        }

        if (!insertedGroup)
        {
            result.Add(group);
        }

        return result;
    }

    private static bool ContainsElement(ScadaElement current, string elementId)
    {
        return current.ChildElements.Any(child =>
            string.Equals(child.Id, elementId, StringComparison.Ordinal) ||
            ContainsElement(child, elementId));
    }

    private static string CreateActionId(string elementId, string trigger, string functionName, string targetId)
    {
        return string.Join(
            "_",
            new[] { "action", SanitizeIdPart(functionName), SanitizeIdPart(trigger), SanitizeIdPart(elementId), SanitizeIdPart(targetId) }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static ScadaElementData CreateDefaultElementData(ScadaElement element)
    {
        return element.Kind switch
        {
            ScadaElementKind.Text => new ScadaElementData("Texte", null, null, null, null, null, null, null, null, false),
            ScadaElementKind.InputText => new ScadaElementData(null, "Texte", null, null, null, null, null, null, null, false),
            ScadaElementKind.InputNumeric => new ScadaElementData(null, "0", 0, null, null, 0, null, "0", null, false),
            _ => new ScadaElementData(null, null, null, null, null, null, null, null, null, false)
        };
    }

    private static string? NormalizeOptionalTagId(string? candidate, string? current)
    {
        return candidate is null
            ? current
            : string.IsNullOrWhiteSpace(candidate)
                ? null
                : candidate.Trim();
    }

    private static string SanitizeIdPart(string value)
    {
        var normalized = new string(value
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray());
        return normalized.Trim('_').ToLowerInvariant();
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

    private sealed record SceneElementSelection(
        ScadaElement Element,
        ScadaElement? Parent,
        SceneBounds AbsoluteBounds)
    {
        public int SiblingIndex { get; init; }
    }
}
