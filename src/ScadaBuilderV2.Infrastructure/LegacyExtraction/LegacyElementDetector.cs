using System.Globalization;
using System.Text.RegularExpressions;
using ScadaBuilderV2.Domain.Legacy;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Infrastructure.LegacyExtraction;

public sealed class LegacyElementDetector
{
    private static readonly Regex TagRegex = new(
        @"<(?<tag>div|button|img|span|rect|line|polygon|polyline|circle|ellipse|path)\b(?<attrs>[^>]*)>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)')",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StylePropertyRegex = new(
        @"(?<name>[-a-zA-Z]+)\s*:\s*(?<value>[^;]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<LegacyExtractionCandidate> DetectAtomicElements(
        LegacySourceDocument sourceDocument,
        string html)
    {
        ArgumentNullException.ThrowIfNull(sourceDocument);
        ArgumentException.ThrowIfNullOrWhiteSpace(html);

        var candidates = new List<LegacyExtractionCandidate>();
        foreach (Match match in TagRegex.Matches(html))
        {
            var tag = match.Groups["tag"].Value;
            var attributes = ParseAttributes(match.Groups["attrs"].Value);
            if (!attributes.TryGetValue("data-id", out var sourceElementId) ||
                string.IsNullOrWhiteSpace(sourceElementId))
            {
                continue;
            }

            var bounds = TryGetBounds(tag, attributes);
            if (bounds is null)
            {
                continue;
            }

            var name = attributes.TryGetValue("data-name", out var rawName) && !string.IsNullOrWhiteSpace(rawName)
                ? rawName.Trim()
                : $"{tag}-{sourceElementId}";
            var suggestedKind = SuggestKind(tag, attributes);

            candidates.Add(new LegacyExtractionCandidate(
                $"candidate-{sourceDocument.Id}-{sourceElementId}",
                sourceDocument,
                sourceElementId.Trim(),
                name,
                suggestedKind,
                bounds,
                LegacyExtractionState.Candidate));
        }

        return candidates;
    }

    private static Dictionary<string, string> ParseAttributes(string rawAttributes)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex.Matches(rawAttributes))
        {
            attributes[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        return attributes;
    }

    private static SceneBounds? TryGetBounds(string tag, IReadOnlyDictionary<string, string> attributes)
    {
        if (attributes.TryGetValue("style", out var style))
        {
            var styleValues = ParseStyle(style);
            if (TryGetStyleNumber(styleValues, "left", out var x) &&
                TryGetStyleNumber(styleValues, "top", out var y) &&
                TryGetStyleNumber(styleValues, "width", out var width) &&
                TryGetStyleNumber(styleValues, "height", out var height))
            {
                return new SceneBounds(x, y, width, height);
            }
        }

        return tag.ToLowerInvariant() switch
        {
            "rect" => TryGetRectBounds(attributes),
            "line" => TryGetLineBounds(attributes),
            "circle" => TryGetCircleBounds(attributes),
            "ellipse" => TryGetEllipseBounds(attributes),
            "polygon" or "polyline" => TryGetPointsBounds(attributes),
            _ => null
        };
    }

    private static Dictionary<string, string> ParseStyle(string style)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in StylePropertyRegex.Matches(style))
        {
            values[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        return values;
    }

    private static bool TryGetStyleNumber(IReadOnlyDictionary<string, string> styleValues, string name, out double value)
    {
        value = 0;
        if (!styleValues.TryGetValue(name, out var raw))
        {
            return false;
        }

        return TryParseNumber(raw.Replace("px", "", StringComparison.OrdinalIgnoreCase), out value);
    }

    private static SceneBounds? TryGetRectBounds(IReadOnlyDictionary<string, string> attributes)
    {
        return TryGetNumber(attributes, "x", out var x) &&
            TryGetNumber(attributes, "y", out var y) &&
            TryGetNumber(attributes, "width", out var width) &&
            TryGetNumber(attributes, "height", out var height)
            ? new SceneBounds(x, y, width, height)
            : null;
    }

    private static SceneBounds? TryGetLineBounds(IReadOnlyDictionary<string, string> attributes)
    {
        if (!TryGetNumber(attributes, "x1", out var x1) ||
            !TryGetNumber(attributes, "y1", out var y1) ||
            !TryGetNumber(attributes, "x2", out var x2) ||
            !TryGetNumber(attributes, "y2", out var y2))
        {
            return null;
        }

        var x = Math.Min(x1, x2);
        var y = Math.Min(y1, y2);
        var width = Math.Max(1, Math.Abs(x2 - x1));
        var height = Math.Max(1, Math.Abs(y2 - y1));
        return new SceneBounds(x, y, width, height);
    }

    private static SceneBounds? TryGetCircleBounds(IReadOnlyDictionary<string, string> attributes)
    {
        return TryGetNumber(attributes, "cx", out var cx) &&
            TryGetNumber(attributes, "cy", out var cy) &&
            TryGetNumber(attributes, "r", out var r)
            ? new SceneBounds(cx - r, cy - r, r * 2, r * 2)
            : null;
    }

    private static SceneBounds? TryGetEllipseBounds(IReadOnlyDictionary<string, string> attributes)
    {
        return TryGetNumber(attributes, "cx", out var cx) &&
            TryGetNumber(attributes, "cy", out var cy) &&
            TryGetNumber(attributes, "rx", out var rx) &&
            TryGetNumber(attributes, "ry", out var ry)
            ? new SceneBounds(cx - rx, cy - ry, rx * 2, ry * 2)
            : null;
    }

    private static SceneBounds? TryGetPointsBounds(IReadOnlyDictionary<string, string> attributes)
    {
        if (!attributes.TryGetValue("points", out var rawPoints))
        {
            return null;
        }

        var numbers = Regex.Matches(rawPoints, @"-?\d+(?:\.\d+)?")
            .Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture))
            .ToArray();
        if (numbers.Length < 4 || numbers.Length % 2 != 0)
        {
            return null;
        }

        var xs = numbers.Where((_, index) => index % 2 == 0).ToArray();
        var ys = numbers.Where((_, index) => index % 2 == 1).ToArray();
        var minX = xs.Min();
        var minY = ys.Min();
        return new SceneBounds(minX, minY, Math.Max(1, xs.Max() - minX), Math.Max(1, ys.Max() - minY));
    }

    private static ScadaElementKind SuggestKind(string tag, IReadOnlyDictionary<string, string> attributes)
    {
        if (attributes.TryGetValue("data-type", out var dataType))
        {
            return dataType.ToLowerInvariant() switch
            {
                "text" => ScadaElementKind.Text,
                "image" => ScadaElementKind.Image,
                "button" => ScadaElementKind.Button,
                "group" => ScadaElementKind.Group,
                _ => ScadaElementKind.Custom
            };
        }

        return tag.ToLowerInvariant() switch
        {
            "img" => ScadaElementKind.Image,
            "button" => ScadaElementKind.Button,
            "div" or "span" => ScadaElementKind.Text,
            "rect" or "line" or "polygon" or "polyline" or "circle" or "ellipse" or "path" => ScadaElementKind.Shape,
            _ => ScadaElementKind.Custom
        };
    }

    private static bool TryGetNumber(IReadOnlyDictionary<string, string> attributes, string name, out double value)
    {
        value = 0;
        return attributes.TryGetValue(name, out var raw) && TryParseNumber(raw, out value);
    }

    private static bool TryParseNumber(string raw, out double value)
    {
        return double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
