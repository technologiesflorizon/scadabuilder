using System.Collections.Generic;
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
        new OperatorItem("--", ""),
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

        PopulateTagComboBox();
        OperatorComboBox.ItemsSource = _operatorItems;
        OperatorComboBox.SelectedIndex = 4; // "=" par defaut
        BoolTrueRadio.IsChecked = true;
        VariableModeRadio.IsChecked = true; // Mode Variable par defaut

        if (existingRule is not null)
        {
            NameTextBox.Text = existingRule.Name;
            RestoreExpression(existingRule.Expression.Source);
            LoadEffect(existingRule.Effect);
        }

        RefreshEffectTypeComboBox();
        RefreshActiveEffectsList();
        if (_activeKinds.Count > 0)
        {
            ShowEffectEditor(_activeKinds.First());
            ActiveEffectsListBox.SelectedIndex = 0;
        }

        if (ExpressionModeRadio.IsChecked == true)
            ValidateExpression();
    }

private sealed record EffectListItem(EffectKind Kind, string Summary);

    private static readonly (EffectKind Kind, string Label)[] _effectTypeLabels =
    [
        (EffectKind.BackgroundColor, "Couleur de fond"),
        (EffectKind.Border, "Bordure"),
        (EffectKind.Text, "Texte"),
        (EffectKind.ElementVisible, "Visibilite"),
        (EffectKind.Opacity, "Opacite"),
        (EffectKind.Rotation, "Rotation"),
        (EffectKind.Animation, "Animation"),
        (EffectKind.ColorFilter, "Filtre de couleur")
    ];

    private readonly HashSet<EffectKind> _activeKinds = new();

    private void RefreshEffectTypeComboBox()
    {
        var available = _effectTypeLabels
            .Where(x => !_activeKinds.Contains(x.Kind))
            .Select(x => new EffectTypeItem(x.Kind, x.Label))
            .ToArray();
        EffectTypeComboBox.ItemsSource = available;
        if (available.Length > 0)
        {
            EffectTypeComboBox.SelectedIndex = 0;
        }
    }

    private void RefreshActiveEffectsList()
    {
        var items = _effectTypeLabels
            .Where(x => _activeKinds.Contains(x.Kind))
            .Select(x => new EffectListItem(x.Kind, BuildEffectSummary(x.Kind)))
            .ToArray();
        ActiveEffectsListBox.ItemsSource = items;
    }

    private string BuildEffectSummary(EffectKind kind) => kind switch
    {
        EffectKind.BackgroundColor => $"Couleur de fond: {BackgroundColorPicker.Value}",
        EffectKind.Border => $"Bordure: {BorderColorPicker.Value} ({BorderWidthTextBox.Text}px)",
        EffectKind.Text => $"Texte: {(string.IsNullOrWhiteSpace(TextContentTextBox.Text) ? "(vide)" : TextContentTextBox.Text)}",
        EffectKind.ElementVisible => $"Visibilite: {(ElementVisibleCheckBox.IsChecked == true ? "Visible" : "Masque")}",
        EffectKind.Opacity => $"Opacite: {OpacitySlider.Value:0.00}",
        EffectKind.Rotation => $"Rotation: {RotationTextBox.Text} deg",
        EffectKind.Animation => $"Animation: {AnimationComboBox.SelectedItem}",
        EffectKind.ColorFilter => $"Filtre de couleur: {ColorFilterColorPicker.Value} ({ColorFilterOpacitySlider.Value:0.00}){(ColorFilterHaloCheckBox.IsChecked == true ? ", halo" : "")}",
        _ => kind.ToString()
    };

    private void ShowEffectEditor(EffectKind kind)
    {
        EffectEditorPanel.Visibility = Visibility.Visible;
        BackgroundColorEditor.Visibility = kind == EffectKind.BackgroundColor ? Visibility.Visible : Visibility.Collapsed;
        BorderEditor.Visibility = kind == EffectKind.Border ? Visibility.Visible : Visibility.Collapsed;
        TextEditor.Visibility = kind == EffectKind.Text ? Visibility.Visible : Visibility.Collapsed;
        ElementVisibleEditor.Visibility = kind == EffectKind.ElementVisible ? Visibility.Visible : Visibility.Collapsed;
        OpacityEditor.Visibility = kind == EffectKind.Opacity ? Visibility.Visible : Visibility.Collapsed;
        RotationEditor.Visibility = kind == EffectKind.Rotation ? Visibility.Visible : Visibility.Collapsed;
        AnimationEditor.Visibility = kind == EffectKind.Animation ? Visibility.Visible : Visibility.Collapsed;
        ColorFilterEditor.Visibility = kind == EffectKind.ColorFilter ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnAddActiveEffectClick(object sender, RoutedEventArgs e)
    {
        if (EffectTypeComboBox.SelectedItem is not EffectTypeItem item)
        {
            return;
        }

        _activeKinds.Add(item.Kind);
        if (item.Kind == EffectKind.ColorFilter)
        {
            if (string.IsNullOrWhiteSpace(ColorFilterColorPicker.Value))
            {
                ColorFilterColorPicker.SetColor("#E53935");
            }
            if (string.IsNullOrWhiteSpace(ColorFilterHaloColorPicker.Value))
            {
                ColorFilterHaloColorPicker.SetColor(ColorFilterColorPicker.Value);
            }
        }

        RefreshEffectTypeComboBox();
        RefreshActiveEffectsList();
        ShowEffectEditor(item.Kind);
        UpdatePreview();
    }

    private void OnEditActiveEffectClick(object sender, RoutedEventArgs e)
    {
        if (ActiveEffectsListBox.SelectedItem is not EffectListItem item)
        {
            return;
        }

        ShowEffectEditor(item.Kind);
    }

    private void OnRemoveActiveEffectClick(object sender, RoutedEventArgs e)
    {
        if (ActiveEffectsListBox.SelectedItem is not EffectListItem item)
        {
            return;
        }

        _activeKinds.Remove(item.Kind);
        RefreshEffectTypeComboBox();
        RefreshActiveEffectsList();
        EffectEditorPanel.Visibility = Visibility.Collapsed;
        UpdatePreview();
    }

    private void OnColorFilterHaloChanged(object sender, RoutedEventArgs e)
    {
        ColorFilterHaloPanel.Visibility = ColorFilterHaloCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        RefreshActiveEffectsList();
    }

    private void RestoreExpression(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            VariableModeRadio.IsChecked = true;
            return;
        }

        // Try to match: {TagId} == true  or  {TagId} == false
        var boolMatch = System.Text.RegularExpressions.Regex.Match(
            source, @"^\{(.+?)}\s*==\s*(true|false)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (boolMatch.Success)
        {
            VariableModeRadio.IsChecked = true;
            SelectTagByName(boolMatch.Groups[1].Value);
            BoolTrueRadio.IsChecked = string.Equals(boolMatch.Groups[2].Value, "true", StringComparison.OrdinalIgnoreCase);
            BoolFalseRadio.IsChecked = !BoolTrueRadio.IsChecked;
            return;
        }

        // Try to match: {TagId} <op> <value>  (numeric comparison)
        var numericMatch = System.Text.RegularExpressions.Regex.Match(
            source, @"^\{(.+?)}\s*(!=|>=|>|==|<|<=)\s*(.+)$");
        if (numericMatch.Success)
        {
            VariableModeRadio.IsChecked = true;
            SelectTagByName(numericMatch.Groups[1].Value);
            ValueTextBox.Text = numericMatch.Groups[3].Value.Trim();
            var exprOp = numericMatch.Groups[2].Value;
            for (int i = 0; i < _operatorItems.Length; i++)
            {
                if (_operatorItems[i].Expression == exprOp)
                {
                    OperatorComboBox.SelectedIndex = i;
                    break;
                }
            }
            return;
        }

        // Try to match: bare {TagId}
        var bareMatch = System.Text.RegularExpressions.Regex.Match(source, @"^\{([^}]+)\}$");
        if (bareMatch.Success)
        {
            VariableModeRadio.IsChecked = true;
            SelectTagByName(bareMatch.Groups[1].Value);
            var tag = SelectedTag;
            if (tag is not null && !IsBooleanDatatype(tag.Datatype))
            {
                OperatorComboBox.SelectedIndex = 0; // "--"
                ValueTextBox.Text = "";
            }
            return;
        }

        // Fallback: Expression mode
        ExpressionModeRadio.IsChecked = true;
        ExpressionTextBox.Text = source;
    }

    private void SelectTagByName(string tagName)
    {
        // Match by DisplayName (primary — what expressions use)
        for (int i = 0; i < TagComboBox.Items.Count; i++)
        {
            if (TagComboBox.Items[i] is TagItem item)
            {
                var tag = (_tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
                    .FirstOrDefault(t => t.Id == item.TagId);
                if (tag is not null && string.Equals(tag.DisplayName, tagName, StringComparison.OrdinalIgnoreCase))
                {
                    TagComboBox.SelectedIndex = i;
                    return;
                }
            }
        }
        // Fallback: match by Id (backward compat with pre-existing expressions)
        for (int i = 0; i < TagComboBox.Items.Count; i++)
        {
            if (TagComboBox.Items[i] is TagItem item &&
                string.Equals(item.TagId, tagName, StringComparison.Ordinal))
            {
                TagComboBox.SelectedIndex = i;
                return;
            }
        }
        // Tag non trouve dans le catalogue : fallback Expression
        ExpressionModeRadio.IsChecked = true;
        ExpressionTextBox.Text = $"{{{tagName}}}";
    }

    public ScadaStateRule? Result { get; private set; }

    private void PopulateTagComboBox()
    {
        var items = (_tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
            .Where(tag => tag.Enabled)
            .OrderBy(tag => tag.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(tag => new TagItem(tag.Id, TagLabel(tag)))
            .ToArray();
        TagComboBox.ItemsSource = items;
    }

    private void LoadEffect(ScadaEffectBlock effect)
    {
        if (effect.BackgroundColor is not null)
        {
            _activeKinds.Add(EffectKind.BackgroundColor);
            BackgroundColorPicker.SetColor(effect.BackgroundColor);
        }

        if (effect.BorderColor is not null)
        {
            _activeKinds.Add(EffectKind.Border);
            BorderColorPicker.SetColor(effect.BorderColor);
            BorderWidthTextBox.Text = (effect.BorderWidth ?? 1).ToString(CultureInfo.InvariantCulture);
        }

        if (effect.TextContent is not null || effect.TextColor is not null || effect.TextVisible is not null)
        {
            _activeKinds.Add(EffectKind.Text);
            TextContentTextBox.Text = effect.TextContent ?? string.Empty;
            TextColorPicker.SetColor(effect.TextColor ?? "#000000");
            TextVisibleCheckBox.IsChecked = effect.TextVisible ?? true;
        }

        if (effect.ElementVisible is not null)
        {
            _activeKinds.Add(EffectKind.ElementVisible);
            ElementVisibleCheckBox.IsChecked = effect.ElementVisible;
        }

        if (effect.Opacity is not null)
        {
            _activeKinds.Add(EffectKind.Opacity);
            OpacitySlider.Value = effect.Opacity.Value;
        }

        if (effect.Rotation is not null)
        {
            _activeKinds.Add(EffectKind.Rotation);
            RotationTextBox.Text = effect.Rotation.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (effect.Animation is not null)
        {
            _activeKinds.Add(EffectKind.Animation);
            AnimationComboBox.SelectedItem = effect.Animation.Value;
        }

        if (effect.ColorFilterColor is not null)
        {
            _activeKinds.Add(EffectKind.ColorFilter);
            ColorFilterColorPicker.SetColor(effect.ColorFilterColor);
            ColorFilterOpacitySlider.Value = effect.ColorFilterOpacity ?? 1.0;
            ColorFilterHaloCheckBox.IsChecked = effect.ColorFilterHalo ?? false;
            ColorFilterHaloPanel.Visibility = ColorFilterHaloCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            ColorFilterHaloColorPicker.SetColor(effect.ColorFilterHaloColor ?? effect.ColorFilterColor);
        }
    }

    private void OnExpressionTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ValidateExpression();

    private void OnConditionModeChanged(object sender, RoutedEventArgs e)
    {
        var isVariable = VariableModeRadio.IsChecked == true;
        VariablePanel.Visibility = isVariable ? Visibility.Visible : Visibility.Collapsed;
        ExpressionPanel.Visibility = isVariable ? Visibility.Collapsed : Visibility.Visible;

        if (isVariable)
            OnTagSelectionChanged(sender, e);
        else
            ValidateExpression();
    }

    private void OnTagSelectionChanged(object sender, RoutedEventArgs e)
    {
        var tag = SelectedTag;
        if (tag is null)
        {
            BoolPanel.Visibility = Visibility.Collapsed;
            NumericPanel.Visibility = Visibility.Collapsed;
            return;
        }

        if (IsBooleanDatatype(tag.Datatype))
        {
            BoolPanel.Visibility = Visibility.Visible;
            NumericPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            BoolPanel.Visibility = Visibility.Collapsed;
            NumericPanel.Visibility = Visibility.Visible;
        }

        UpdateValidationFromVariable();
    }

    private void OnVariableValueChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        UpdateValidationFromVariable();

    private void UpdateValidationFromVariable()
    {
        var expression = BuildExpressionFromVariable();
        if (string.IsNullOrWhiteSpace(expression))
        {
            ExpressionValidationText.Text = "";
            return;
        }

        var result = ScadaExpressionValidator.Validate(expression, _tagCatalog);
        ExpressionValidationText.Text = result.IsValid
            ? "Condition valide."
            : string.Join(" ", result.Errors);
        ExpressionValidationText.Foreground = result.IsValid
            ? System.Windows.Media.Brushes.SeaGreen
            : System.Windows.Media.Brushes.Firebrick;
    }

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
            BackgroundColor: _activeKinds.Contains(EffectKind.BackgroundColor) ? BackgroundColorPicker.Value : null,
            BorderColor: _activeKinds.Contains(EffectKind.Border) ? BorderColorPicker.Value : null,
            BorderWidth: _activeKinds.Contains(EffectKind.Border) && double.TryParse(BorderWidthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var width) ? width : null,
            TextColor: _activeKinds.Contains(EffectKind.Text) ? TextColorPicker.Value : null,
            TextContent: _activeKinds.Contains(EffectKind.Text) ? TextContentTextBox.Text : null,
            TextVisible: _activeKinds.Contains(EffectKind.Text) ? TextVisibleCheckBox.IsChecked : null,
            ElementVisible: _activeKinds.Contains(EffectKind.ElementVisible) ? ElementVisibleCheckBox.IsChecked : null,
            Opacity: _activeKinds.Contains(EffectKind.Opacity) ? OpacitySlider.Value : null,
            Rotation: _activeKinds.Contains(EffectKind.Rotation) && double.TryParse(RotationTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var rotation) ? rotation : null,
            Animation: _activeKinds.Contains(EffectKind.Animation) ? (ScadaAnimation?)AnimationComboBox.SelectedItem : null,
            ColorFilterColor: _activeKinds.Contains(EffectKind.ColorFilter) ? ColorFilterColorPicker.Value : null,
            ColorFilterOpacity: _activeKinds.Contains(EffectKind.ColorFilter) ? ColorFilterOpacitySlider.Value : null,
            ColorFilterHalo: _activeKinds.Contains(EffectKind.ColorFilter) ? ColorFilterHaloCheckBox.IsChecked : null,
            ColorFilterHaloColor: _activeKinds.Contains(EffectKind.ColorFilter) && ColorFilterHaloCheckBox.IsChecked == true ? ColorFilterHaloColorPicker.Value : null);
    }

    private static bool IsBooleanDatatype(string? datatype)
    {
        return !string.IsNullOrWhiteSpace(datatype) &&
            (string.Equals(datatype, "bool", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(datatype, "boolean", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(datatype, "booléen", StringComparison.OrdinalIgnoreCase) ||
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
            if (BoolTrueRadio.IsChecked != true && BoolFalseRadio.IsChecked != true)
                return $"{{{tag.DisplayName}}}";
            var value = BoolTrueRadio.IsChecked == true ? "true" : "false";
            return $"{{{tag.DisplayName}}} == {value}";
        }

        var op = (OperatorComboBox.SelectedItem as OperatorItem)?.Expression ?? "==";
        if (string.IsNullOrEmpty(op))
            return $"{{{tag.DisplayName}}}";
        var val = ValueTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(val)) val = "0";
        return $"{{{tag.DisplayName}}} {op} {val}";
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var source = VariableModeRadio.IsChecked == true
            ? BuildExpressionFromVariable()
            : ExpressionTextBox.Text;

        var validation = ScadaExpressionValidator.Validate(source, _tagCatalog);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            errors.Add("Le nom est requis.");
        if (!validation.IsValid)
            errors.Add(string.Join(" ", validation.Errors));

        if (errors.Count > 0)
        {
            ExpressionValidationText.Text = string.Join(" | ", errors);
            ExpressionValidationText.Foreground = System.Windows.Media.Brushes.Firebrick;
            return;
        }

        Result = new ScadaStateRule(
            _ruleId,
            NameTextBox.Text.Trim(),
            Enabled: true,
            Expression: ScadaExpression.FromSource(source),
            Effect: BuildEffectFromUi());

        DialogResult = true;
    }

    private static string TagLabel(ScadaTagDefinition tag)
    {
        var name = tag.KeywordLabel ?? tag.DisplayName ?? tag.Id;
        return string.IsNullOrWhiteSpace(tag.Device) ? name : $"{name} ({tag.Device})";
    }

    private sealed record TagItem(string TagId, string DisplayName);
    private sealed record OperatorItem(string Display, string Expression)
    {
        public override string ToString() => Display;
    }
}
