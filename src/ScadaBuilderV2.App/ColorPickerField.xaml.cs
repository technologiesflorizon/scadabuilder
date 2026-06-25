using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScadaBuilderV2.App;

/// <summary>
/// WPF field that displays a CSS color swatch and opens the modal color picker before committing a value.
/// </summary>
public partial class ColorPickerField : UserControl
{
    /// <summary>
    /// Dependency property for the CSS color value shown and edited by the field.
    /// </summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(string),
        typeof(ColorPickerField),
        new FrameworkPropertyMetadata("#FFFFFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    /// <summary>
    /// Routed event raised after the field value changes.
    /// </summary>
    public static readonly RoutedEvent ColorChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(ColorChanged),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(ColorPickerField));

    /// <summary>
    /// Initializes a new color picker field.
    /// </summary>
    public ColorPickerField()
    {
        InitializeComponent();
        UpdatePreview();
    }

    /// <summary>
    /// Occurs after the CSS color value has changed.
    /// </summary>
    public event RoutedEventHandler ColorChanged
    {
        add => AddHandler(ColorChangedEvent, value);
        remove => RemoveHandler(ColorChangedEvent, value);
    }

    /// <summary>
    /// Gets or sets the CSS color value shown by the field.
    /// </summary>
    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Sets the displayed CSS color value, falling back to white when the input is blank.
    /// </summary>
    /// <param name="value">The CSS color value to display.</param>
    public void SetColor(string value)
    {
        Value = string.IsNullOrWhiteSpace(value) ? "#FFFFFF" : value.Trim();
    }

    private static void OnValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ColorPickerField field)
        {
            return;
        }

        field.UpdatePreview();
        field.RaiseEvent(new RoutedEventArgs(ColorChangedEvent, field));
    }

    private void OnOpenPickerClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerDialog(Value)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            SetColor(dialog.SelectedColorValue);
        }
    }

    private void UpdatePreview()
    {
        if (ColorPreview is null || ColorText is null || TransparencyPattern is null)
        {
            return;
        }

        var value = string.IsNullOrWhiteSpace(Value) ? "#FFFFFF" : Value.Trim();
        ColorText.Text = value;

        if (ColorPickerDialog.TryParseCssColor(value, out var color))
        {
            ColorPreview.Background = new SolidColorBrush(color);
            ColorPreview.Opacity = color.A / 255.0;
            TransparencyPattern.Visibility = color.A < 255 ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        ColorPreview.Background = Brushes.Transparent;
        ColorPreview.Opacity = 1;
        TransparencyPattern.Visibility = Visibility.Visible;
    }
}
