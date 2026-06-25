using System.Windows.Media;

namespace ScadaBuilderV2.App;

// Pure color math helpers (CSS parsing, hex formatting, HSV<->RGB conversion).
// Extracted from MainWindow.xaml.cs as a behavior-preserving split; these are
// stateless statics with no UI/field dependencies. See docs/04_editor refactor trace.
public partial class MainWindow
{
    private static bool TryParseCssColor(string cssColor, out Color color)
    {
        color = Colors.Transparent;
        if (string.IsNullOrWhiteSpace(cssColor))
        {
            return false;
        }

        var value = cssColor.Trim();
        if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var start = value.IndexOf('(');
            var end = value.IndexOf(')');
            if (start >= 0 && end > start)
            {
                var parts = value[(start + 1)..end]
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 &&
                    TryParseByte(parts[0], out var r) &&
                    TryParseByte(parts[1], out var g) &&
                    TryParseByte(parts[2], out var b))
                {
                    color = Color.FromRgb(r, g, b);
                    return true;
                }
            }
        }

        try
        {
            var converted = ColorConverter.ConvertFromString(value);
            if (converted is Color parsed)
            {
                color = parsed;
                return true;
            }
        }
        catch (FormatException)
        {
            return false;
        }

        return false;
    }

    private static bool TryParseByte(string raw, out byte value)
    {
        value = 0;
        var normalized = raw.Trim();
        if (normalized.EndsWith('%'))
        {
            if (double.TryParse(normalized.TrimEnd('%'), out var percent))
            {
                value = (byte)Math.Clamp(Math.Round(percent / 100 * 255), 0, 255);
                return true;
            }

            return false;
        }

        if (double.TryParse(normalized, out var number))
        {
            value = (byte)Math.Clamp(Math.Round(number), 0, 255);
            return true;
        }

        return false;
    }

    private static string ToCssHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static Color FromHsv(double hue, double saturation, double value)
    {
        hue = ((hue % 360) + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);

        var chroma = value * saturation;
        var x = chroma * (1 - Math.Abs((hue / 60 % 2) - 1));
        var m = value - chroma;

        (var r1, var g1, var b1) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };

        return Color.FromRgb(
            ToColorByte((r1 + m) * 255),
            ToColorByte((g1 + m) * 255),
            ToColorByte((b1 + m) * 255));
    }

    private static (double Hue, double Saturation, double Value) ToHsv(Color color)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var hue = delta == 0
            ? 0
            : max == r
                ? 60 * (((g - b) / delta) % 6)
                : max == g
                    ? 60 * (((b - r) / delta) + 2)
                    : 60 * (((r - g) / delta) + 4);
        if (hue < 0)
        {
            hue += 360;
        }

        var saturation = max == 0 ? 0 : delta / max;
        return (hue, saturation, max);
    }

    private static byte ToColorByte(double value)
    {
        return (byte)Math.Clamp(Math.Round(value), 0, 255);
    }
}
