using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ScadaBuilderV2.App;

/// <summary>
/// Modal color picker used by Element+ property fields before committing color changes.
/// </summary>
public partial class ColorPickerDialog : Window
{
    private bool _isUpdatingColorControls;
    private bool _isDraggingColorPicker;
    private double _hue;
    private double _saturation;
    private double _value;

    private readonly string _initialColor;

    /// <summary>
    /// Initializes a new color picker dialog with the current CSS color value.
    /// </summary>
    /// <param name="currentValue">The current CSS color value.</param>
    public ColorPickerDialog(string currentValue)
    {
        InitializeComponent();
        _initialColor = string.IsNullOrWhiteSpace(currentValue) ? "#FFFFFF" : currentValue.Trim();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        SetColorControls(_initialColor);
    }

    /// <summary>
    /// Gets the CSS color value selected when the dialog is saved.
    /// </summary>
    public string SelectedColorValue { get; private set; } = "#FFFFFF";

    /// <summary>
    /// Attempts to parse a CSS color value into a WPF color.
    /// </summary>
    /// <param name="value">The CSS color value to parse.</param>
    /// <param name="color">The parsed color when successful.</param>
    /// <returns><see langword="true"/> when the value can be parsed; otherwise <see langword="false"/>.</returns>
    public static bool TryParseCssColor(string value, out Color color)
    {
        color = Colors.Transparent;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var start = trimmed.IndexOf('(');
            var end = trimmed.IndexOf(')');
            if (start >= 0 && end > start)
            {
                var parts = trimmed[(start + 1)..end]
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 &&
                    TryParseByte(parts[0], out var red) &&
                    TryParseByte(parts[1], out var green) &&
                    TryParseByte(parts[2], out var blue))
                {
                    color = Color.FromRgb(red, green, blue);
                    return true;
                }
            }
        }

        try
        {
            var parsed = ColorConverter.ConvertFromString(trimmed);
            if (parsed is Color converted)
            {
                color = converted;
                return true;
            }
        }
        catch (FormatException)
        {
            return false;
        }

        return false;
    }

    private void OnColorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingColorControls || !AreColorControlsReady())
        {
            return;
        }

        if (TryParseCssColor(ColorTextBox.Text.Trim(), out var color))
        {
            ValidationText.Text = "";
            UpdateColorControls(color, updateText: false);
        }
        else
        {
            ValidationText.Text = "Valeur couleur invalide.";
        }
    }

    private void OnRgbSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingColorControls || !AreColorControlsReady())
        {
            return;
        }

        var color = Color.FromRgb(
            (byte)Math.Round(RedSlider.Value),
            (byte)Math.Round(GreenSlider.Value),
            (byte)Math.Round(BlueSlider.Value));
        UpdateColorControls(color, updateText: true);
    }

    private void OnHueSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingColorControls || !AreColorControlsReady())
            return;

        _hue = HueSlider.Value;
        if (_saturation <= 0) _saturation = 1.0;  // sortir du gris/blanc
        if (_value <= 0) _value = 1.0;            // sortir du noir
        var color = FromHsv(_hue, _saturation, _value);
        UpdateColorControls(color, updateText: true);
    }

    private void OnSaturationValuePickerMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!AreColorControlsReady())
        {
            return;
        }

        _isDraggingColorPicker = true;
        SaturationValuePicker.CaptureMouse();
        UpdateSaturationValueFromPoint(e.GetPosition(SaturationValuePicker));
    }

    private void OnSaturationValuePickerMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingColorPicker || !AreColorControlsReady())
        {
            return;
        }

        UpdateSaturationValueFromPoint(e.GetPosition(SaturationValuePicker));
    }

    private void OnSaturationValuePickerMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingColorPicker)
        {
            return;
        }

        _isDraggingColorPicker = false;
        SaturationValuePicker.ReleaseMouseCapture();
        UpdateSaturationValueFromPoint(e.GetPosition(SaturationValuePicker));
    }

    private void OnSaturationValuePickerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (AreColorControlsReady())
        {
            UpdateSaturationValueSelector();
        }
    }

    private void OnSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string color })
        {
            SetColorControls(color);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!TryParseCssColor(ColorTextBox.Text.Trim(), out var color))
        {
            ValidationText.Text = "Valeur couleur invalide.";
            return;
        }

        SelectedColorValue = ToCssHex(color);
        DialogResult = true;
    }

    private void SetColorControls(string cssColor)
    {
        if (!AreColorControlsReady())
        {
            return;
        }

        if (TryParseCssColor(cssColor, out var color))
        {
            ValidationText.Text = "";
            UpdateColorControls(color, updateText: true);
            return;
        }

        ColorTextBox.Text = cssColor;
        ValidationText.Text = "Valeur couleur invalide.";
    }

    private void UpdateColorControls(Color color, bool updateText)
    {
        if (!AreColorControlsReady())
        {
            return;
        }

        _isUpdatingColorControls = true;
        try
        {
            ColorPreview.Background = new SolidColorBrush(color);
            (var hue, var saturation, var value) = ToHsv(color, fallbackHue: _hue);
            _hue = hue;
            _saturation = saturation;
            _value = value;
            HueSlider.Value = hue;
            HueColorLayer.Fill = new SolidColorBrush(FromHsv(hue, 1, 1));
            UpdateSaturationValueSelector();
            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;
            RedValueText.Text = color.R.ToString(CultureInfo.InvariantCulture);
            GreenValueText.Text = color.G.ToString(CultureInfo.InvariantCulture);
            BlueValueText.Text = color.B.ToString(CultureInfo.InvariantCulture);
            if (updateText)
            {
                ColorTextBox.Text = ToCssHex(color);
            }
        }
        finally
        {
            _isUpdatingColorControls = false;
        }
    }

    private bool AreColorControlsReady()
    {
        return ColorPreview is not null &&
            ColorTextBox is not null &&
            SaturationValuePicker is not null &&
            SaturationValueSelectorTransform is not null &&
            HueColorLayer is not null &&
            HueSlider is not null &&
            RedSlider is not null &&
            GreenSlider is not null &&
            BlueSlider is not null &&
            RedValueText is not null &&
            GreenValueText is not null &&
            BlueValueText is not null;
    }

    private static bool TryParseByte(string raw, out byte value)
    {
        value = 0;
        var normalized = raw.Trim();
        if (normalized.EndsWith("%", StringComparison.Ordinal))
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

    private void UpdateSaturationValueFromPoint(Point point)
    {
        var width = Math.Max(1, SaturationValuePicker.ActualWidth);
        var height = Math.Max(1, SaturationValuePicker.ActualHeight);
        _saturation = Math.Clamp(point.X / width, 0, 1);
        _value = 1 - Math.Clamp(point.Y / height, 0, 1);
        UpdateColorControls(FromHsv(_hue, _saturation, _value), updateText: true);
    }

    private void UpdateSaturationValueSelector()
    {
        var width = Math.Max(1, SaturationValuePicker.ActualWidth);
        var height = Math.Max(1, SaturationValuePicker.ActualHeight);
        SaturationValueSelectorTransform.X = Math.Clamp(_saturation * width - 7, -7, width - 7);
        SaturationValueSelectorTransform.Y = Math.Clamp((1 - _value) * height - 7, -7, height - 7);
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

    private static (double Hue, double Saturation, double Value) ToHsv(Color color, double fallbackHue = 0)
    {
        var red = color.R / 255d;
        var green = color.G / 255d;
        var blue = color.B / 255d;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;

        var hue = delta == 0
            ? fallbackHue
            : max == red
                ? 60 * (((green - blue) / delta) % 6)
                : max == green
                    ? 60 * (((blue - red) / delta) + 2)
                    : 60 * (((red - green) / delta) + 4);
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
