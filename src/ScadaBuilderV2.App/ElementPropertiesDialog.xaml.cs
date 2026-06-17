using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App;

/// <summary>
/// Modal WPF authoring surface for Element+ properties.
/// </summary>
/// <remarks>
/// Contracts: docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md.
/// </remarks>
public partial class ElementPropertiesDialog : Window
{
    private readonly ScadaElement element;

    public ElementPropertiesDialog(ScadaElement element, string eventSummary)
    {
        ArgumentNullException.ThrowIfNull(element);
        this.element = element;
        InitializeComponent();
        LoadElement(element);
        SetEventSummary(eventSummary);
    }

    public ElementPropertiesDialogResult? Result { get; private set; }

    public Action? OpenEvents { get; set; }

    public void SetEventSummary(string eventSummary)
    {
        EventSummaryText.Text = string.IsNullOrWhiteSpace(eventSummary)
            ? "Aucun evenement"
            : eventSummary;
    }

    private void LoadElement(ScadaElement current)
    {
        var style = current.Style ?? (current.Kind == ScadaElementKind.Text ? ScadaElementStyle.DefaultText : ScadaElementStyle.DefaultInput);
        var data = current.Data ?? new ScadaElementData(null, null, null, null, null, null, null, null, null, false);
        var buttonBehavior = current.EffectiveButtonBehavior;
        var hoverStyle = buttonBehavior.EffectiveHover;

        ElementTitleText.Text = $"{current.UserLabel} ({current.Kind})";
        ElementNameTextBox.Text = current.DisplayName;
        PositionModeComboBox.SelectedIndex = current.Layout?.PositionMode == ElementPositionMode.Relative ? 1 : 0;
        ElementXTextBox.Text = current.Bounds.X.ToString("0.##");
        ElementYTextBox.Text = current.Bounds.Y.ToString("0.##");
        ElementWidthTextBox.Text = current.Bounds.Width.ToString("0.##");
        ElementHeightTextBox.Text = current.Bounds.Height.ToString("0.##");

        SelectComboBoxText(FontFamilyComboBox, style.FontFamily);
        FontSizeTextBox.Text = style.FontSize.ToString("0.##");
        SelectComboBoxText(BackgroundComboBox, style.Background);
        SelectComboBoxText(BorderStyleComboBox, style.BorderStyle);
        BorderWidthTextBox.Text = style.BorderWidth.ToString("0.##");
        ShadowNoneRadio.IsChecked = style.ShadowPreset == "None";
        ShadowSoftRadio.IsChecked = style.ShadowPreset == "Soft";
        ShadowRaisedRadio.IsChecked = style.ShadowPreset == "Raised";
        ShadowInsetRadio.IsChecked = style.ShadowPreset == "Inset";
        AdvancedCssTextBox.Text = style.AdvancedCss ?? "";

        ButtonTab.Visibility = current.Kind == ScadaElementKind.Button ? Visibility.Visible : Visibility.Collapsed;
        ButtonDisabledCheckBox.IsChecked = buttonBehavior.IsDisabled;
        ButtonHoverEnabledCheckBox.IsChecked = hoverStyle.Enabled;
        SelectComboBoxText(ButtonHoverBackgroundComboBox, hoverStyle.Background);
        SelectComboBoxText(ButtonHoverForegroundComboBox, hoverStyle.Foreground);
        SelectComboBoxText(ButtonHoverBorderColorComboBox, hoverStyle.BorderColor);

        ReadOnlyCheckBox.IsChecked = data.IsReadOnly;
        PlaceholderTextBox.Text = data.Placeholder ?? "";
        ValueTextBox.Text = data.Value?.ToString("0.##") ?? data.Text ?? "";
        MinimumTextBox.Text = data.Minimum?.ToString("0.##") ?? "";
        MaximumTextBox.Text = data.Maximum?.ToString("0.##") ?? "";
        DecimalsTextBox.Text = data.Decimals?.ToString() ?? "";
        UnitTextBox.Text = data.Unit ?? "";
        DisplayFormatTextBox.Text = data.DisplayFormat ?? "";
        TagBindingTextBox.Text = data.TagBinding ?? "";
        UpdateDataConstraintState();
    }

    private void OnReadOnlyChanged(object sender, RoutedEventArgs e)
    {
        UpdateDataConstraintState();
    }

    private void UpdateDataConstraintState()
    {
        var canEditInputConstraints = element.Kind == ScadaElementKind.InputNumeric && ReadOnlyCheckBox.IsChecked != true;
        MinimumTextBox.IsEnabled = canEditInputConstraints;
        MaximumTextBox.IsEnabled = canEditInputConstraints;
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = "";

        if (!TryReadDouble(ElementXTextBox.Text, "X", out var x) ||
            !TryReadDouble(ElementYTextBox.Text, "Y", out var y) ||
            !TryReadDouble(ElementWidthTextBox.Text, "Largeur", out var width) ||
            !TryReadDouble(ElementHeightTextBox.Text, "Hauteur", out var height) ||
            !TryReadDouble(FontSizeTextBox.Text, "Taille police", out var fontSize) ||
            !TryReadDouble(BorderWidthTextBox.Text, "Largeur bordure", out var borderWidth))
        {
            return;
        }

        var minimum = ReadNullableDouble(MinimumTextBox.Text);
        var maximum = ReadNullableDouble(MaximumTextBox.Text);
        var value = element.Kind == ScadaElementKind.InputNumeric
            ? ReadNullableDouble(ValueTextBox.Text)
            : null;
        var decimals = ReadNullableInt(DecimalsTextBox.Text);

        Result = new ElementPropertiesDialogResult(
            DisplayName: string.IsNullOrWhiteSpace(ElementNameTextBox.Text) ? element.Id : ElementNameTextBox.Text.Trim(),
            Bounds: new SceneBounds(
                Math.Max(0, x),
                Math.Max(0, y),
                Math.Max(4, width),
                Math.Max(4, height)),
            PositionMode: PositionModeComboBox.SelectedIndex == 1 ? ElementPositionMode.Relative : ElementPositionMode.Absolute,
            FontFamily: GetComboBoxText(FontFamilyComboBox, "Segoe UI"),
            FontSize: Math.Max(6, fontSize),
            Background: GetComboBoxText(BackgroundComboBox, "#FFFFFF"),
            BorderStyle: GetComboBoxText(BorderStyleComboBox, "Solid"),
            BorderWidth: Math.Max(0, borderWidth),
            ShadowPreset: GetSelectedShadowPreset(),
            AdvancedCss: string.IsNullOrWhiteSpace(AdvancedCssTextBox.Text) ? null : AdvancedCssTextBox.Text,
            ButtonDisabled: ButtonDisabledCheckBox.IsChecked == true,
            ButtonHoverEnabled: ButtonHoverEnabledCheckBox.IsChecked == true,
            ButtonHoverBackground: GetComboBoxText(ButtonHoverBackgroundComboBox, ScadaButtonHoverStyle.Default.Background),
            ButtonHoverForeground: GetComboBoxText(ButtonHoverForegroundComboBox, ScadaButtonHoverStyle.Default.Foreground),
            ButtonHoverBorderColor: GetComboBoxText(ButtonHoverBorderColorComboBox, ScadaButtonHoverStyle.Default.BorderColor),
            Placeholder: string.IsNullOrWhiteSpace(PlaceholderTextBox.Text) ? null : PlaceholderTextBox.Text,
            Text: element.Kind is ScadaElementKind.InputText or ScadaElementKind.Text or ScadaElementKind.Button
                ? ValueTextBox.Text
                : null,
            Value: value,
            Minimum: minimum,
            Maximum: maximum,
            Decimals: decimals,
            Unit: string.IsNullOrWhiteSpace(UnitTextBox.Text) ? null : UnitTextBox.Text,
            DisplayFormat: string.IsNullOrWhiteSpace(DisplayFormatTextBox.Text) ? null : DisplayFormatTextBox.Text,
            TagBinding: string.IsNullOrWhiteSpace(TagBindingTextBox.Text) ? null : TagBindingTextBox.Text,
            IsReadOnly: ReadOnlyCheckBox.IsChecked == true);

        DialogResult = true;
    }

    private void OnOpenEventsClick(object sender, RoutedEventArgs e)
    {
        OpenEvents?.Invoke();
    }

    private bool TryReadDouble(string value, string fieldName, out double result)
    {
        if (double.TryParse(value, out result))
        {
            return true;
        }

        ValidationText.Text = $"{fieldName}: valeur numerique invalide.";
        return false;
    }

    private static double? ReadNullableDouble(string value)
    {
        return double.TryParse(value, out var parsed) ? parsed : null;
    }

    private static int? ReadNullableInt(string value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string GetComboBoxText(ComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem is ComboBoxItem item && item.Content is string text
            ? text
            : string.IsNullOrWhiteSpace(comboBox.Text) ? fallback : comboBox.Text.Trim();
    }

    private static void SelectComboBoxText(ComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Content is string text && string.Equals(text, value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.Text = value;
    }

    private string GetSelectedShadowPreset()
    {
        if (ShadowSoftRadio.IsChecked == true)
        {
            return "Soft";
        }

        if (ShadowRaisedRadio.IsChecked == true)
        {
            return "Raised";
        }

        if (ShadowInsetRadio.IsChecked == true)
        {
            return "Inset";
        }

        return "None";
    }
}

public sealed record ElementPropertiesDialogResult(
    string DisplayName,
    SceneBounds Bounds,
    ElementPositionMode PositionMode,
    string FontFamily,
    double FontSize,
    string Background,
    string BorderStyle,
    double BorderWidth,
    string ShadowPreset,
    string? AdvancedCss,
    bool ButtonDisabled,
    bool ButtonHoverEnabled,
    string ButtonHoverBackground,
    string ButtonHoverForeground,
    string ButtonHoverBorderColor,
    string? Placeholder,
    string? Text,
    double? Value,
    double? Minimum,
    double? Maximum,
    int? Decimals,
    string? Unit,
    string? DisplayFormat,
    string? TagBinding,
    bool IsReadOnly);
