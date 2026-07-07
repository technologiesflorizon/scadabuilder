using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;
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
    private readonly IReadOnlyList<ScadaSceneReference> pageReferences;
    private readonly ScadaTagCatalog? tagCatalog;
    private ScadaElement currentElement;

    public ElementPropertiesDialog(
        ScadaElement element,
        IReadOnlyList<ScadaSceneReference> pageReferences,
        ScadaTagCatalog? tagCatalog)
    {
        ArgumentNullException.ThrowIfNull(element);
        this.element = element;
        this.pageReferences = pageReferences;
        this.tagCatalog = tagCatalog;
        currentElement = element;
        InitializeComponent();
        LoadElement(element);
        RefreshStateAndCommandLists();
    }

    public ElementPropertiesDialogResult? Result { get; private set; }

    /// <summary>
    /// Invoked to persist a new state config on the underlying element; returns the latest element.
    /// </summary>
    public Func<ScadaElementStateConfig, ScadaElement>? SaveStateConfig { get; set; }

    /// <summary>
    /// Invoked to persist a new command config on the underlying element; returns the latest element.
    /// </summary>
    public Func<ScadaElementCommandConfig, ScadaElement>? SaveCommandConfig { get; set; }

    private void RefreshStateAndCommandLists()
    {
        StateRulesListBox.ItemsSource = currentElement.EffectiveStateConfig.States;
        CommandsListBox.ItemsSource = currentElement.EffectiveCommandConfig.Commands;
    }

    private void OnAddStateRuleClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ElementStateRuleDialog(null, tagCatalog) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null || SaveStateConfig is null)
        {
            return;
        }

        var config = currentElement.EffectiveStateConfig with
        {
            States = currentElement.EffectiveStateConfig.States.Append(dialog.Result).ToArray()
        };
        currentElement = SaveStateConfig(config);
        RefreshStateAndCommandLists();
    }

    private void OnEditStateRuleClick(object sender, RoutedEventArgs e)
    {
        if (StateRulesListBox.SelectedItem is not ScadaStateRule selected || SaveStateConfig is null)
        {
            return;
        }

        var dialog = new ElementStateRuleDialog(selected, tagCatalog) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        var states = currentElement.EffectiveStateConfig.States
            .Select(rule => rule.Id == dialog.Result.Id ? dialog.Result : rule)
            .ToArray();
        currentElement = SaveStateConfig(currentElement.EffectiveStateConfig with { States = states });
        RefreshStateAndCommandLists();
    }

    private void OnDeleteStateRuleClick(object sender, RoutedEventArgs e)
    {
        if (StateRulesListBox.SelectedItem is not ScadaStateRule selected || SaveStateConfig is null)
        {
            return;
        }

        var states = currentElement.EffectiveStateConfig.States.Where(rule => rule.Id != selected.Id).ToArray();
        currentElement = SaveStateConfig(currentElement.EffectiveStateConfig with { States = states });
        RefreshStateAndCommandLists();
    }

    private void OnMoveStateRuleUpClick(object sender, RoutedEventArgs e) => MoveSelectedStateRule(-1);

    private void OnMoveStateRuleDownClick(object sender, RoutedEventArgs e) => MoveSelectedStateRule(1);

    private void MoveSelectedStateRule(int offset)
    {
        if (StateRulesListBox.SelectedItem is not ScadaStateRule selected || SaveStateConfig is null)
        {
            return;
        }

        var states = currentElement.EffectiveStateConfig.States.ToList();
        var index = states.FindIndex(rule => rule.Id == selected.Id);
        var newIndex = index + offset;
        if (index < 0 || newIndex < 0 || newIndex >= states.Count)
        {
            return;
        }

        (states[index], states[newIndex]) = (states[newIndex], states[index]);
        currentElement = SaveStateConfig(currentElement.EffectiveStateConfig with { States = states });
        RefreshStateAndCommandLists();
    }

    private void OnEditDefaultStateEffectClick(object sender, RoutedEventArgs e) => EditFallbackEffect(isQuality: false);

    private void OnEditQualityFallbackEffectClick(object sender, RoutedEventArgs e) => EditFallbackEffect(isQuality: true);

    private void EditFallbackEffect(bool isQuality)
    {
        if (SaveStateConfig is null)
        {
            return;
        }

        var placeholderRule = new ScadaStateRule(
            "fallback-editor",
            isQuality ? "Qualite" : "Repos",
            true,
            ScadaExpression.FromSource("true"),
            isQuality ? currentElement.EffectiveStateConfig.QualityFallback : currentElement.EffectiveStateConfig.DefaultEffect);

        var dialog = new ElementStateRuleDialog(placeholderRule, tagCatalog) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        var updatedConfig = isQuality
            ? currentElement.EffectiveStateConfig with { QualityFallback = dialog.Result.Effect }
            : currentElement.EffectiveStateConfig with { DefaultEffect = dialog.Result.Effect };
        currentElement = SaveStateConfig(updatedConfig);
        RefreshStateAndCommandLists();
    }

    private void OnAddCommandClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ElementCommandDialog(null, pageReferences, tagCatalog) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null || SaveCommandConfig is null)
        {
            return;
        }

        var config = currentElement.EffectiveCommandConfig with
        {
            Commands = currentElement.EffectiveCommandConfig.Commands.Append(dialog.Result).ToArray()
        };
        currentElement = SaveCommandConfig(config);
        RefreshStateAndCommandLists();
    }

    private void OnEditCommandClick(object sender, RoutedEventArgs e)
    {
        if (CommandsListBox.SelectedItem is not ScadaCommandBinding selected || SaveCommandConfig is null)
        {
            return;
        }

        var dialog = new ElementCommandDialog(selected, pageReferences, tagCatalog) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        var commands = currentElement.EffectiveCommandConfig.Commands
            .Select(command => command.Id == dialog.Result.Id ? dialog.Result : command)
            .ToArray();
        currentElement = SaveCommandConfig(currentElement.EffectiveCommandConfig with { Commands = commands });
        RefreshStateAndCommandLists();
    }

    private void OnDeleteCommandClick(object sender, RoutedEventArgs e)
    {
        if (CommandsListBox.SelectedItem is not ScadaCommandBinding selected || SaveCommandConfig is null)
        {
            return;
        }

        var commands = currentElement.EffectiveCommandConfig.Commands.Where(command => command.Id != selected.Id).ToArray();
        currentElement = SaveCommandConfig(currentElement.EffectiveCommandConfig with { Commands = commands });
        RefreshStateAndCommandLists();
    }

    private void LoadElement(ScadaElement current)
    {
        var style = current.Style ?? (current.Kind == ScadaElementKind.Text ? ScadaElementStyle.DefaultText : ScadaElementStyle.DefaultInput);
        var data = current.Data ?? new ScadaElementData(null, null, null, null, null, null, null, null, null, false);
        var buttonBehavior = current.EffectiveButtonBehavior;
        var hoverStyle = buttonBehavior.EffectiveHover;
        var pressedStyle = buttonBehavior.EffectivePressed;

        ElementTitleText.Text = $"{current.UserLabel} ({current.Kind})";
        ElementNameTextBox.Text = current.DisplayName;
        PositionModeComboBox.SelectedIndex = current.Layout?.PositionMode == ElementPositionMode.Relative ? 1 : 0;
        ElementXTextBox.Text = current.Bounds.X.ToString("0.##");
        ElementYTextBox.Text = current.Bounds.Y.ToString("0.##");
        ElementWidthTextBox.Text = current.Bounds.Width.ToString("0.##");
        ElementHeightTextBox.Text = current.Bounds.Height.ToString("0.##");

        SelectComboBoxText(FontFamilyComboBox, style.FontFamily);
        FontSizeTextBox.Text = style.FontSize.ToString("0.##");
        BackgroundColorPicker.SetColor(style.Background);
        var isBorderTransparent = string.Equals(style.BorderColor, "Transparent", StringComparison.OrdinalIgnoreCase);
        BorderTransparentCheckBox.IsChecked = isBorderTransparent;
        BorderColorPicker.IsEnabled = !isBorderTransparent;
        if (!isBorderTransparent)
        {
            BorderColorPicker.SetColor(style.BorderColor);
        }
        SelectComboBoxText(BorderStyleComboBox, style.BorderStyle);
        BorderWidthTextBox.Text = style.BorderWidth.ToString("0.##");
        ShadowNoneRadio.IsChecked = style.ShadowPreset == "None";
        ShadowSoftRadio.IsChecked = style.ShadowPreset == "Soft";
        ShadowRaisedRadio.IsChecked = style.ShadowPreset == "Raised";
        ShadowInsetRadio.IsChecked = style.ShadowPreset == "Inset";
        OpacityTextBox.Text = style.Opacity.ToString("0.##");
        RotationTextBox.Text = style.Rotation.ToString("0.##");
        AdvancedCssTextBox.Text = style.AdvancedCss ?? "";

        ButtonTab.Visibility = current.Kind == ScadaElementKind.Button ? Visibility.Visible : Visibility.Collapsed;
        ButtonDisabledCheckBox.IsChecked = buttonBehavior.IsDisabled;
        ButtonHoverEnabledCheckBox.IsChecked = hoverStyle.Enabled;
        ButtonHoverBackgroundColorPicker.SetColor(hoverStyle.Background);
        ButtonHoverForegroundColorPicker.SetColor(hoverStyle.Foreground);
        ButtonHoverBorderColorPicker.SetColor(hoverStyle.BorderColor);
        ButtonPressedEnabledCheckBox.IsChecked = pressedStyle.Enabled;
        ButtonPressedBackgroundColorPicker.SetColor(pressedStyle.Background);
        ButtonPressedForegroundColorPicker.SetColor(pressedStyle.Foreground);
        ButtonPressedBorderColorPicker.SetColor(pressedStyle.BorderColor);

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

    private void OnBorderTransparentChanged(object sender, RoutedEventArgs e)
    {
        BorderColorPicker.IsEnabled = BorderTransparentCheckBox.IsChecked != true;
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
            !TryReadDouble(BorderWidthTextBox.Text, "Largeur bordure", out var borderWidth) ||
            !TryReadDouble(OpacityTextBox.Text, "Opacite", out var opacity) ||
            !TryReadDouble(RotationTextBox.Text, "Rotation", out var rotation))
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
            Background: GetColorPickerValue(BackgroundColorPicker, "#FFFFFF"),
            BorderColor: BorderTransparentCheckBox.IsChecked == true
                ? "Transparent"
                : GetColorPickerValue(BorderColorPicker, "#8AA0A6"),
            BorderStyle: GetComboBoxText(BorderStyleComboBox, "Solid"),
            BorderWidth: Math.Max(0, borderWidth),
            ShadowPreset: GetSelectedShadowPreset(),
            Opacity: Math.Clamp(opacity, 0, 1),
            Rotation: rotation,
            AdvancedCss: string.IsNullOrWhiteSpace(AdvancedCssTextBox.Text) ? null : AdvancedCssTextBox.Text,
            ButtonDisabled: ButtonDisabledCheckBox.IsChecked == true,
            ButtonHoverEnabled: ButtonHoverEnabledCheckBox.IsChecked == true,
            ButtonHoverBackground: GetColorPickerValue(ButtonHoverBackgroundColorPicker, ScadaButtonHoverStyle.Default.Background),
            ButtonHoverForeground: GetColorPickerValue(ButtonHoverForegroundColorPicker, ScadaButtonHoverStyle.Default.Foreground),
            ButtonHoverBorderColor: GetColorPickerValue(ButtonHoverBorderColorPicker, ScadaButtonHoverStyle.Default.BorderColor),
            ButtonPressedEnabled: ButtonPressedEnabledCheckBox.IsChecked == true,
            ButtonPressedBackground: GetColorPickerValue(ButtonPressedBackgroundColorPicker, ScadaButtonPressedStyle.Default.Background),
            ButtonPressedForeground: GetColorPickerValue(ButtonPressedForegroundColorPicker, ScadaButtonPressedStyle.Default.Foreground),
            ButtonPressedBorderColor: GetColorPickerValue(ButtonPressedBorderColorPicker, ScadaButtonPressedStyle.Default.BorderColor),
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

    private static string GetColorPickerValue(ColorPickerField colorPicker, string fallback)
    {
        return string.IsNullOrWhiteSpace(colorPicker.Value) ? fallback : colorPicker.Value.Trim();
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
    string BorderColor,
    string BorderStyle,
    double BorderWidth,
    string ShadowPreset,
    double Opacity,
    double Rotation,
    string? AdvancedCss,
    bool ButtonDisabled,
    bool ButtonHoverEnabled,
    string ButtonHoverBackground,
    string ButtonHoverForeground,
    string ButtonHoverBorderColor,
    bool ButtonPressedEnabled,
    string ButtonPressedBackground,
    string ButtonPressedForeground,
    string ButtonPressedBorderColor,
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
