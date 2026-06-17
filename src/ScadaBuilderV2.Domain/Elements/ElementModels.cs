using System.Globalization;
using System.Text.Encodings.Web;
using ScadaBuilderV2.Domain.Editor;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Domain.Elements;

public abstract class Element : EditorObject
{
    protected Element(
        string id,
        string name,
        ElementPlusObjectType elementType,
        SceneBounds bounds,
        ScadaElementStyle style,
        LegacySourceTrace? legacySource = null,
        ScadaElementLayout? layout = null)
        : base(id, name, EditorObjectKind.ElementPlus)
    {
        ElementType = elementType;
        Bounds = bounds;
        Style = style;
        LegacySource = legacySource;
        Layout = layout ?? ScadaElementLayout.Absolute;
    }

    public string Id => RuntimeId;

    public string Name
    {
        get => DisplayName;
        set => DisplayName = string.IsNullOrWhiteSpace(value) ? RuntimeId : value.Trim();
    }

    public ElementPlusObjectType ElementType { get; }

    public SceneBounds Bounds { get; set; }

    public ScadaElementLayout Layout { get; set; }

    public string? ParentId { get; private set; }

    public ScadaElementStyle Style { get; set; }

    public LegacySourceTrace? LegacySource { get; set; }

    public string HtmlCode { get; set; } = "";

    public string CssCode { get; set; } = "";

    public string JsCode { get; set; } = "";

    public abstract ScadaElementKind ScadaKind { get; }

    public abstract ScadaElementData ToScadaData();

    public virtual ScadaElement ToScadaElement()
    {
        return new ScadaElement(
            Id,
            Name,
            ScadaKind,
            Bounds,
            LegacySource,
            Layout,
            Style,
            ToScadaData());
    }

    public void AttachToParent(string parentId, SceneBounds relativeBounds)
    {
        ParentId = string.IsNullOrWhiteSpace(parentId)
            ? throw new ArgumentException("Parent id is required.", nameof(parentId))
            : parentId;
        Bounds = relativeBounds;
        Layout = new ScadaElementLayout(ElementPositionMode.Relative, ParentId);
        RegenerateCode();
    }

    public void DetachFromParent(SceneBounds absoluteBounds)
    {
        ParentId = null;
        Bounds = absoluteBounds;
        Layout = ScadaElementLayout.Absolute;
        RegenerateCode();
    }

    public virtual void RegenerateCode()
    {
        HtmlCode = BuildHtmlCode();
        CssCode = BuildCssCode();
        JsCode = BuildJsCode();
    }

    protected abstract string BuildHtmlCode();

    protected virtual string BuildCssCode()
    {
        return $$"""
        #{{CssIdentifier(Id)}} {
          position: absolute;
          left: {{Bounds.X.ToString("0.##", CultureInfo.InvariantCulture)}}px;
          top: {{Bounds.Y.ToString("0.##", CultureInfo.InvariantCulture)}}px;
          width: {{Bounds.Width.ToString("0.##", CultureInfo.InvariantCulture)}}px;
          height: {{Bounds.Height.ToString("0.##", CultureInfo.InvariantCulture)}}px;
          font-family: {{Style.FontFamily}};
          font-size: {{Style.FontSize.ToString("0.##", CultureInfo.InvariantCulture)}}px;
          color: {{Style.Foreground}};
          background: {{Style.Background}};
          border: {{Style.BorderWidth.ToString("0.##", CultureInfo.InvariantCulture)}}px {{Style.BorderStyle.ToLowerInvariant()}} {{Style.BorderColor}};
        }
        """;
    }

    protected virtual string BuildJsCode()
    {
        return "";
    }

    protected static string Html(string value)
    {
        return HtmlEncoder.Default.Encode(value);
    }

    protected static string Attribute(string value)
    {
        return HtmlEncoder.Default.Encode(value);
    }

    protected static string CssIdentifier(string value)
    {
        return string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character == '-' || character == '_' ? character : '_'));
    }
}

public sealed class ShapeElement : Element
{
    public ShapeElement(
        string id,
        string name,
        SceneBounds bounds,
        ScadaElementStyle style,
        string pathData = "",
        LegacySourceTrace? legacySource = null,
        ScadaElementLayout? layout = null)
        : base(id, name, ElementPlusObjectType.Shape, bounds, style, legacySource, layout)
    {
        PathData = pathData;
        RegenerateCode();
    }

    public override ScadaElementKind ScadaKind => ScadaElementKind.Shape;

    public string PathData { get; set; }

    public override ScadaElementData ToScadaData()
    {
        return new ScadaElementData(PathData, null, null, null, null, null, null, null, null, false);
    }

    protected override string BuildHtmlCode()
    {
        var id = Attribute(Id);
        var name = Attribute(Name);
        return string.IsNullOrWhiteSpace(PathData)
            ? $"""<div id="{id}" class="scada-element scada-shape" data-name="{name}"></div>"""
            : $"""<svg id="{id}" class="scada-element scada-shape" data-name="{name}" viewBox="0 0 {Bounds.Width.ToString("0.##", CultureInfo.InvariantCulture)} {Bounds.Height.ToString("0.##", CultureInfo.InvariantCulture)}"><path d="{Attribute(PathData)}"></path></svg>""";
    }
}

public sealed class ElementGroup : Element
{
    private readonly List<Element> _children = [];

    public ElementGroup(
        string id,
        string name,
        SceneBounds bounds,
        IEnumerable<Element>? children = null,
        ScadaElementStyle? style = null,
        LegacySourceTrace? legacySource = null,
        ScadaElementLayout? layout = null)
        : base(id, name, ElementPlusObjectType.Group, bounds, style ?? ScadaElementStyle.DefaultText, legacySource, layout)
    {
        if (children is not null)
        {
            _children.AddRange(children);
        }

        RegenerateCode();
    }

    public override ScadaElementKind ScadaKind => ScadaElementKind.Group;

    public IReadOnlyList<Element> Children => _children;

    public override ScadaElementData ToScadaData()
    {
        return new ScadaElementData(null, null, null, null, null, null, null, null, null, false);
    }

    public override ScadaElement ToScadaElement()
    {
        return new ScadaElement(
            Id,
            Name,
            ScadaKind,
            Bounds,
            LegacySource,
            Layout,
            Style,
            ToScadaData(),
            _children.Select(child => child.ToScadaElement()).ToArray());
    }

    public void AddChild(Element child)
    {
        if (_children.Any(existing => existing.Id == child.Id))
        {
            return;
        }

        _children.Add(child);
        RegenerateCode();
    }

    public bool RemoveChild(string childId)
    {
        var removed = _children.RemoveAll(child => child.Id == childId) > 0;
        if (removed)
        {
            RegenerateCode();
        }

        return removed;
    }

    public bool ContainsDescendant(string elementId)
    {
        return _children.Any(child => child.Id == elementId ||
            child is ElementGroup group && group.ContainsDescendant(elementId));
    }

    protected override string BuildHtmlCode()
    {
        var id = Attribute(Id);
        var name = Attribute(Name);
        var childrenHtml = string.Concat(_children.Select(child => child.HtmlCode));
        return $"""<div id="{id}" class="scada-element scada-group" data-name="{name}">{childrenHtml}</div>""";
    }
}

public sealed class NumericInput : Element
{
    public NumericInput(
        string id,
        string name,
        SceneBounds bounds,
        ScadaElementStyle style,
        bool isReadOnly,
        double? value = null,
        double? minimum = null,
        double? maximum = null,
        int? decimals = 0,
        string? unit = null,
        string? displayFormat = null,
        string? tagBinding = null,
        LegacySourceTrace? legacySource = null,
        ScadaElementLayout? layout = null)
        : base(id, name, ElementPlusObjectType.Numeric, bounds, style, legacySource, layout)
    {
        Value = value;
        Minimum = minimum;
        Maximum = maximum;
        Decimals = decimals;
        Unit = unit;
        DisplayFormat = displayFormat;
        TagBinding = tagBinding;
        IsReadOnly = isReadOnly;
        RegenerateCode();
    }

    public override ScadaElementKind ScadaKind => ScadaElementKind.InputNumeric;

    public double? Value { get; set; }

    public double? Minimum { get; set; }

    public double? Maximum { get; set; }

    public int? Decimals { get; set; }

    public string? Unit { get; set; }

    public string? DisplayFormat { get; set; }

    public string? TagBinding { get; set; }

    public bool IsReadOnly { get; set; }

    public string DisplayText => FormatDisplayText();

    public override ScadaElementData ToScadaData()
    {
        return new ScadaElementData(
            null,
            "0",
            Value,
            Minimum,
            Maximum,
            Decimals,
            Unit,
            DisplayFormat,
            TagBinding,
            IsReadOnly);
    }

    protected override string BuildHtmlCode()
    {
        var id = Attribute(Id);
        var name = Attribute(Name);
        if (IsReadOnly)
        {
            return $"""<span id="{id}" class="scada-element scada-numeric-input scada-readonly" data-name="{name}">{Html(DisplayText)}</span>""";
        }

        var value = Attribute(Value?.ToString(CultureInfo.InvariantCulture) ?? "");
        var min = Minimum.HasValue ? $" min=\"{Minimum.Value.ToString(CultureInfo.InvariantCulture)}\"" : "";
        var max = Maximum.HasValue ? $" max=\"{Maximum.Value.ToString(CultureInfo.InvariantCulture)}\"" : "";
        var decimals = NumericDisplayFormat.CountFractionalPlaceholders(DisplayFormat) ?? Decimals;
        var step = decimals.HasValue && decimals.Value > 0
            ? Math.Pow(10, -decimals.Value).ToString(CultureInfo.InvariantCulture)
            : "1";

        return $"""<input id="{id}" class="scada-element scada-numeric-input" data-name="{name}" type="number" value="{value}" step="{step}"{min}{max} />""";
    }

    private string FormatDisplayText()
    {
        if (Value.HasValue)
        {
            return NumericDisplayFormat.Format(Value.Value, DisplayFormat, Decimals);
        }

        return string.IsNullOrWhiteSpace(DisplayFormat) ? "" : DisplayFormat;
    }
}

/// <summary>
/// Formats Element+ numeric values from the active display-format contract.
/// </summary>
/// <remarks>
/// Decisions: DEC-0030.
/// Contracts: docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/ElementGroupTests.cs.
/// </remarks>
internal static class NumericDisplayFormat
{
    /// <summary>
    /// Applies a hash mask such as <c>##.#</c> or falls back to legacy decimal precision.
    /// </summary>
    public static string Format(double value, string? displayFormat, int? fallbackDecimals = null)
    {
        var format = string.IsNullOrWhiteSpace(displayFormat) ? null : displayFormat.Trim();
        if (!string.IsNullOrWhiteSpace(format) && IsHashMask(format))
        {
            var decimals = CountFractionalPlaceholders(format) ?? 0;
            var totalDigits = format.Count(character => character == '#');
            var scaled = Math.Round(value / Math.Pow(10, decimals), decimals, MidpointRounding.AwayFromZero);
            var absoluteLimit = Math.Pow(10, Math.Max(1, totalDigits - decimals)) - Math.Pow(10, -decimals);
            var clamped = Math.Max(-absoluteLimit, Math.Min(absoluteLimit, scaled));
            return clamped.ToString($"F{decimals}", CultureInfo.InvariantCulture);
        }

        if (fallbackDecimals.HasValue)
        {
            return value.ToString($"F{Math.Max(0, fallbackDecimals.Value)}", CultureInfo.InvariantCulture);
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Returns the number of fractional hash placeholders in a display mask.
    /// </summary>
    public static int? CountFractionalPlaceholders(string? displayFormat)
    {
        if (string.IsNullOrWhiteSpace(displayFormat))
        {
            return null;
        }

        var separatorIndex = displayFormat.IndexOf('.', StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return 0;
        }

        return displayFormat[(separatorIndex + 1)..].Count(character => character == '#');
    }

    private static bool IsHashMask(string displayFormat)
    {
        return displayFormat.Length > 0
            && displayFormat.Any(character => character == '#')
            && displayFormat.All(character => character == '#' || character == '.');
    }
}
