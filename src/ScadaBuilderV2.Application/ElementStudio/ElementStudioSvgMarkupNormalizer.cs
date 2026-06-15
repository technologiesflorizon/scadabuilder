using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.ElementStudio;

public static partial class ElementStudioSvgMarkupNormalizer
{
    public static string NormalizeSvgMarkup(string? svgMarkup)
    {
        if (string.IsNullOrWhiteSpace(svgMarkup))
        {
            return "";
        }

        try
        {
            var document = XDocument.Parse(svgMarkup, LoadOptions.PreserveWhitespace);
            var root = document.Root;
            if (root is null || !IsElement(root, "svg"))
            {
                return svgMarkup;
            }

            var bounds = CalculateGeometryBounds(root);
            if (bounds is null)
            {
                return svgMarkup;
            }

            root.SetAttributeValue("viewBox", FormatViewBox(bounds));
            root.SetAttributeValue("width", FormatNumber(Math.Max(1, bounds.Width)));
            root.SetAttributeValue("height", FormatNumber(Math.Max(1, bounds.Height)));
            return document.ToString(SaveOptions.DisableFormatting);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Xml.XmlException)
        {
            return svgMarkup;
        }
    }

    private static SceneBounds? CalculateGeometryBounds(XElement svg)
    {
        var points = new List<(double X, double Y)>();
        foreach (var element in svg.Descendants())
        {
            var name = element.Name.LocalName;
            if (string.Equals(name, "polygon", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "polyline", StringComparison.OrdinalIgnoreCase))
            {
                points.AddRange(ReadPointList((string?)element.Attribute("points")));
                continue;
            }

            if (string.Equals(name, "line", StringComparison.OrdinalIgnoreCase))
            {
                AddPoint(points, element, "x1", "y1");
                AddPoint(points, element, "x2", "y2");
                continue;
            }

            if (string.Equals(name, "rect", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "image", StringComparison.OrdinalIgnoreCase))
            {
                AddBox(points, element);
                continue;
            }

            if (string.Equals(name, "circle", StringComparison.OrdinalIgnoreCase))
            {
                AddCircle(points, element);
                continue;
            }

            if (string.Equals(name, "ellipse", StringComparison.OrdinalIgnoreCase))
            {
                AddEllipse(points, element);
                continue;
            }

            if (string.Equals(name, "text", StringComparison.OrdinalIgnoreCase))
            {
                AddPoint(points, element, "x", "y");
            }
        }

        if (points.Count == 0)
        {
            return null;
        }

        var left = points.Min(point => point.X);
        var top = points.Min(point => point.Y);
        var right = points.Max(point => point.X);
        var bottom = points.Max(point => point.Y);
        return new SceneBounds(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    private static IEnumerable<(double X, double Y)> ReadPointList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        var numbers = NumberRegex()
            .Matches(value)
            .Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture))
            .ToArray();

        for (var index = 0; index + 1 < numbers.Length; index += 2)
        {
            yield return (numbers[index], numbers[index + 1]);
        }
    }

    private static void AddPoint(List<(double X, double Y)> points, XElement element, string xName, string yName)
    {
        if (TryReadDouble(element, xName, out var x) && TryReadDouble(element, yName, out var y))
        {
            points.Add((x, y));
        }
    }

    private static void AddBox(List<(double X, double Y)> points, XElement element)
    {
        if (!TryReadDouble(element, "x", out var x))
        {
            x = 0;
        }

        if (!TryReadDouble(element, "y", out var y))
        {
            y = 0;
        }

        if (TryReadDouble(element, "width", out var width) &&
            TryReadDouble(element, "height", out var height))
        {
            points.Add((x, y));
            points.Add((x + width, y + height));
        }
    }

    private static void AddCircle(List<(double X, double Y)> points, XElement element)
    {
        if (TryReadDouble(element, "cx", out var cx) &&
            TryReadDouble(element, "cy", out var cy) &&
            TryReadDouble(element, "r", out var r))
        {
            points.Add((cx - r, cy - r));
            points.Add((cx + r, cy + r));
        }
    }

    private static void AddEllipse(List<(double X, double Y)> points, XElement element)
    {
        if (TryReadDouble(element, "cx", out var cx) &&
            TryReadDouble(element, "cy", out var cy) &&
            TryReadDouble(element, "rx", out var rx) &&
            TryReadDouble(element, "ry", out var ry))
        {
            points.Add((cx - rx, cy - ry));
            points.Add((cx + rx, cy + ry));
        }
    }

    private static bool TryReadDouble(XElement element, string attributeName, out double value)
    {
        return double.TryParse(
            (string?)element.Attribute(attributeName),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool IsElement(XElement element, string localName)
    {
        return string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatViewBox(SceneBounds bounds)
    {
        return string.Join(
            " ",
            FormatNumber(bounds.X),
            FormatNumber(bounds.Y),
            FormatNumber(Math.Max(1, bounds.Width)),
            FormatNumber(Math.Max(1, bounds.Height)));
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"-?\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();
}
