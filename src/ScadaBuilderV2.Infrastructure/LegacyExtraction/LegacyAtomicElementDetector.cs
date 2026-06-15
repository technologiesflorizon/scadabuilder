using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ScadaBuilderV2.Domain.Legacy;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Infrastructure.LegacyExtraction;

public sealed class LegacyAtomicElementDetector
{
    private static readonly Regex TagRegex = new(
        @"<(?<name>[a-zA-Z][\w:-]*)(?<attrs>(?:\s+[^<>]*?)?)\s*/?>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[a-zA-Z_:][\w:.-]*)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s""'=<>`]+))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly HashSet<string> SvgShapeTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "rect",
        "line",
        "circle",
        "ellipse",
        "path",
        "polygon",
        "polyline"
    };

    public IReadOnlyList<LegacyExtractionCandidate> Detect(
        string html,
        LegacySourceDocument sourceDocument)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(sourceDocument);

        var candidates = new List<LegacyExtractionCandidate>();

        foreach (Match tagMatch in TagRegex.Matches(html))
        {
            var tagName = tagMatch.Groups["name"].Value;
            var attributes = ParseAttributes(tagMatch.Groups["attrs"].Value);
            if (!attributes.TryGetValue("data-id", out var sourceElementId) ||
                string.IsNullOrWhiteSpace(sourceElementId))
            {
                continue;
            }

            var isSvgShape = SvgShapeTags.Contains(tagName);
            if (!isSvgShape && !HasAbsolutePosition(attributes))
            {
                continue;
            }

            var displayName = ReadFirstNonBlank(attributes, "data-name", "name", "title") ?? sourceElementId;
            var suggestedKind = isSvgShape
                ? ScadaElementKind.Shape
                : SuggestKind(ReadFirstNonBlank(attributes, "data-type", "class", "role", "type"));
            var sourceBounds = isSvgShape
                ? ReadSvgBounds(tagName, attributes)
                : ReadStyleBounds(attributes);

            candidates.Add(new LegacyExtractionCandidate(
                Id: $"{sourceDocument.Id}:{sourceElementId}",
                SourceDocument: sourceDocument,
                SourceElementId: sourceElementId,
                SuggestedDisplayName: displayName,
                SuggestedKind: suggestedKind,
                SourceBounds: sourceBounds,
                State: LegacyExtractionState.Candidate));
        }

        return candidates;
    }

    private static Dictionary<string, string> ParseAttributes(string attributeText)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match attributeMatch in AttributeRegex.Matches(attributeText))
        {
            var name = attributeMatch.Groups["name"].Value;
            var value = WebUtility.HtmlDecode(attributeMatch.Groups["value"].Value);
            attributes[name] = value;
        }

        return attributes;
    }

    private static bool HasAbsolutePosition(IReadOnlyDictionary<string, string> attributes)
    {
        if (!attributes.TryGetValue("style", out var style))
        {
            return false;
        }

        var declarations = ParseStyle(style);
        return declarations.TryGetValue("position", out var position) &&
            string.Equals(position, "absolute", StringComparison.OrdinalIgnoreCase);
    }

    private static SceneBounds ReadStyleBounds(IReadOnlyDictionary<string, string> attributes)
    {
        if (!attributes.TryGetValue("style", out var style))
        {
            return new SceneBounds(0, 0, 0, 0);
        }

        var declarations = ParseStyle(style);
        return new SceneBounds(
            ReadCssNumber(declarations, "left"),
            ReadCssNumber(declarations, "top"),
            ReadCssNumber(declarations, "width"),
            ReadCssNumber(declarations, "height"));
    }

    private static Dictionary<string, string> ParseStyle(string style)
    {
        var declarations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = declaration.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var property = declaration[..separator].Trim();
            var value = declaration[(separator + 1)..].Trim();
            if (property.Length > 0 && value.Length > 0)
            {
                declarations[property] = value;
            }
        }

        return declarations;
    }

    private static SceneBounds ReadSvgBounds(
        string tagName,
        IReadOnlyDictionary<string, string> attributes)
    {
        if (string.Equals(tagName, "line", StringComparison.OrdinalIgnoreCase))
        {
            var x1 = ReadAttributeNumber(attributes, "x1");
            var y1 = ReadAttributeNumber(attributes, "y1");
            var x2 = ReadAttributeNumber(attributes, "x2");
            var y2 = ReadAttributeNumber(attributes, "y2");

            return new SceneBounds(
                Math.Min(x1, x2),
                Math.Min(y1, y2),
                Math.Abs(x2 - x1),
                Math.Abs(y2 - y1));
        }

        if (string.Equals(tagName, "circle", StringComparison.OrdinalIgnoreCase))
        {
            var radius = ReadAttributeNumber(attributes, "r");
            return new SceneBounds(
                ReadAttributeNumber(attributes, "cx") - radius,
                ReadAttributeNumber(attributes, "cy") - radius,
                radius * 2,
                radius * 2);
        }

        if (string.Equals(tagName, "ellipse", StringComparison.OrdinalIgnoreCase))
        {
            var radiusX = ReadAttributeNumber(attributes, "rx");
            var radiusY = ReadAttributeNumber(attributes, "ry");
            return new SceneBounds(
                ReadAttributeNumber(attributes, "cx") - radiusX,
                ReadAttributeNumber(attributes, "cy") - radiusY,
                radiusX * 2,
                radiusY * 2);
        }

        return new SceneBounds(
            ReadAttributeNumber(attributes, "x"),
            ReadAttributeNumber(attributes, "y"),
            ReadAttributeNumber(attributes, "width"),
            ReadAttributeNumber(attributes, "height"));
    }

    private static double ReadCssNumber(
        IReadOnlyDictionary<string, string> declarations,
        string property)
    {
        return declarations.TryGetValue(property, out var value)
            ? ParseNumber(value)
            : 0;
    }

    private static double ReadAttributeNumber(
        IReadOnlyDictionary<string, string> attributes,
        string name)
    {
        return attributes.TryGetValue(name, out var value)
            ? ParseNumber(value)
            : 0;
    }

    private static double ParseNumber(string value)
    {
        var match = Regex.Match(
            value,
            @"[-+]?(?:\d+(?:\.\d*)?|\.\d+)",
            RegexOptions.CultureInvariant);

        return match.Success &&
            double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : 0;
    }

    private static string? ReadFirstNonBlank(
        IReadOnlyDictionary<string, string> attributes,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (attributes.TryGetValue(name, out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static ScadaElementKind SuggestKind(string? legacyType)
    {
        if (string.IsNullOrWhiteSpace(legacyType))
        {
            return ScadaElementKind.Custom;
        }

        var normalized = legacyType.ToLowerInvariant();
        if (normalized.Contains("button"))
        {
            return ScadaElementKind.Button;
        }

        if (normalized.Contains("text") ||
            normalized.Contains("label"))
        {
            return ScadaElementKind.Text;
        }

        if (normalized.Contains("image") ||
            normalized.Contains("img"))
        {
            return ScadaElementKind.Image;
        }

        if (normalized.Contains("shape") ||
            normalized.Contains("rect") ||
            normalized.Contains("line"))
        {
            return ScadaElementKind.Shape;
        }

        if (normalized.Contains("group"))
        {
            return ScadaElementKind.Group;
        }

        if (normalized.Contains("container") ||
            normalized.Contains("layer") ||
            normalized.Contains("panel") ||
            normalized.Contains("div"))
        {
            return ScadaElementKind.Container;
        }

        return ScadaElementKind.Custom;
    }
}
