using System.Globalization;
using System.Windows;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App;

public partial class ElementStateRuleDialog : Window
{
    private readonly ScadaTagCatalog? _tagCatalog;
    private readonly string _ruleId;

    private static readonly OperatorItem[] _operatorItems =
    [
        new OperatorItem("<>", "!="),
        new OperatorItem(">=", ">="),
        new OperatorItem(">", ">"),
        new OperatorItem("=", "=="),
        new OperatorItem("<", "<"),
        new OperatorItem("<=", "<=")
    ];

    public ElementStateRuleDialog(ScadaStateRule? existingRule, ScadaTagCatalog? tagCatalog)
    {
        InitializeComponent();
        _tagCatalog = tagCatalog;
        _ruleId = existingRule?.Id ?? Guid.NewGuid().ToString("n");

        AnimationComboBox.ItemsSource = Enum.GetValues<ScadaAnimation>();

        if (existingRule is not null)
        {
            NameTextBox.Text = existingRule.Name;
            ExpressionTextBox.Text = existingRule.Expression.Source;
            LoadEffect(existingRule.Effect);
        }

        ValidateExpression();
    }

    public ScadaStateRule? Result { get; private set; }

    private void PopulateTagComboBox()
    {
        var items = (_tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
            .Where(tag => tag.Enabled)
            .OrderBy(tag => tag.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(tag => new TagItem(tag.Id, tag.AuthoringLabel))
            .ToArray();
        TagComboBox.ItemsSource = items;
        if (items.Length > 0)
            TagComboBox.SelectedIndex = 0;
    }

    private void LoadEffect(ScadaEffectBlock effect)
    {
        if (effect.BackgroundColor is not null)
        {
            BackgroundEnabledCheckBox.IsChecked = true;
            BackgroundColorPicker.SetColor(effect.BackgroundColor);
        }

        if (effect.BorderColor is not null)
        {
            BorderEnabledCheckBox.IsChecked = true;
            BorderColorPicker.SetColor(effect.BorderColor);
            BorderWidthTextBox.Text = (effect.BorderWidth ?? 1).ToString(CultureInfo.InvariantCulture);
        }

        if (effect.TextContent is not null || effect.TextColor is not null || effect.TextVisible is not null)
        {
            TextEnabledCheckBox.IsChecked = true;
            TextContentTextBox.Text = effect.TextContent ?? string.Empty;
            TextColorPicker.SetColor(effect.TextColor ?? "#000000");
            TextVisibleCheckBox.IsChecked = effect.TextVisible ?? true;
        }

        if (effect.ElementVisible is not null)
        {
            ElementVisibleEnabledCheckBox.IsChecked = true;
            ElementVisibleCheckBox.IsChecked = effect.ElementVisible;
        }

        if (effect.Opacity is not null)
        {
            OpacityEnabledCheckBox.IsChecked = true;
            OpacitySlider.Value = effect.Opacity.Value;
        }

        if (effect.Rotation is not null)
        {
            RotationEnabledCheckBox.IsChecked = true;
            RotationTextBox.Text = effect.Rotation.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (effect.Animation is not null)
        {
            AnimationEnabledCheckBox.IsChecked = true;
            AnimationComboBox.SelectedItem = effect.Animation.Value;
        }
    }

    private void OnEffectToggleChanged(object sender, RoutedEventArgs e) => UpdatePreview();

    private void OnExpressionTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ValidateExpression();

    private void ValidateExpression()
    {
        var result = ScadaExpressionValidator.Validate(ExpressionTextBox.Text, _tagCatalog);
        ExpressionValidationText.Text = result.IsValid
            ? "Condition valide."
            : string.Join(" ", result.Errors);
        ExpressionValidationText.Foreground = result.IsValid
            ? System.Windows.Media.Brushes.SeaGreen
            : System.Windows.Media.Brushes.Firebrick;
    }

    private void UpdatePreview()
    {
        var effect = BuildEffectFromUi();
        PreviewBorder.Background = effect.BackgroundColor is not null
            ? new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(effect.BackgroundColor))
            : System.Windows.Media.Brushes.Transparent;
        PreviewBorder.Opacity = effect.Opacity ?? 1.0;
        PreviewText.Text = effect.TextContent ?? string.Empty;
    }

    private ScadaEffectBlock BuildEffectFromUi()
    {
        return new ScadaEffectBlock(
            BackgroundColor: BackgroundEnabledCheckBox.IsChecked == true ? BackgroundColorPicker.Value : null,
            BorderColor: BorderEnabledCheckBox.IsChecked == true ? BorderColorPicker.Value : null,
            BorderWidth: BorderEnabledCheckBox.IsChecked == true && double.TryParse(BorderWidthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var width) ? width : null,
            TextColor: TextEnabledCheckBox.IsChecked == true ? TextColorPicker.Value : null,
            TextContent: TextEnabledCheckBox.IsChecked == true ? TextContentTextBox.Text : null,
            TextVisible: TextEnabledCheckBox.IsChecked == true ? TextVisibleCheckBox.IsChecked : null,
            ElementVisible: ElementVisibleEnabledCheckBox.IsChecked == true ? ElementVisibleCheckBox.IsChecked : null,
            Opacity: OpacityEnabledCheckBox.IsChecked == true ? OpacitySlider.Value : null,
            Rotation: RotationEnabledCheckBox.IsChecked == true && double.TryParse(RotationTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var rotation) ? rotation : null,
            Animation: AnimationEnabledCheckBox.IsChecked == true ? (ScadaAnimation?)AnimationComboBox.SelectedItem : null);
    }

    private static bool IsBooleanDatatype(string? datatype)
    {
        return !string.IsNullOrWhiteSpace(datatype) &&
            (string.Equals(datatype, "bool", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(datatype, "boolean", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(datatype, "digital", StringComparison.OrdinalIgnoreCase));
    }

    private ScadaTagDefinition? SelectedTag =>
        (TagComboBox.SelectedItem as TagItem) is { } item
            ? (_tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
                .FirstOrDefault(t => t.Id == item.TagId)
            : null;

    private string BuildExpressionFromVariable()
    {
        var tag = SelectedTag;
        if (tag is null) return string.Empty;

        if (IsBooleanDatatype(tag.Datatype))
        {
            var value = BoolTrueRadio.IsChecked == true ? "true" : "false";
            return $"{{{tag.Id}}} == {value}";
        }

        var op = (OperatorComboBox.SelectedItem as OperatorItem)?.Expression ?? "==";
        var val = ValueTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(val)) val = "0";
        return $"{{{tag.Id}}} {op} {val}";
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var validation = ScadaExpressionValidator.Validate(ExpressionTextBox.Text, _tagCatalog);
        if (!validation.IsValid || string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            ValidateExpression();
            return;
        }

        Result = new ScadaStateRule(
            _ruleId,
            NameTextBox.Text.Trim(),
            Enabled: true,
            Expression: ScadaExpression.FromSource(ExpressionTextBox.Text),
            Effect: BuildEffectFromUi());

        DialogResult = true;
    }

    private sealed record TagItem(string TagId, string DisplayName);
    private sealed record OperatorItem(string Display, string Expression)
    {
        public override string ToString() => Display;
    }
}
