using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        RefreshActiveEffectsList();

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
    private readonly Dictionary<EffectKind, ScadaEffectBlock> _effectValues = new();

    private void RefreshActiveEffectsList()
    {
        var items = _effectTypeLabels
            .Where(x => _activeKinds.Contains(x.Kind))
            .Select(x => new EffectListItem(x.Kind, BuildEffectSummary(x.Kind)))
            .ToArray();
        ActiveEffectsListBox.ItemsSource = items;
    }

    private string BuildEffectSummary(EffectKind kind)
    {
        if (!_effectValues.TryGetValue(kind, out var effect))
            effect = ScadaEffectBlock.Empty;

        return kind switch
        {
            EffectKind.BackgroundColor => $"Couleur de fond: {effect.BackgroundColor ?? "-"}",
            EffectKind.Border => $"Bordure: {effect.BorderColor ?? "-"} ({effect.BorderWidth ?? 1}px)",
            EffectKind.Text => $"Texte: {(string.IsNullOrWhiteSpace(effect.TextContent) ? "(vide)" : effect.TextContent)}",
            EffectKind.ElementVisible => $"Visibilité: {(effect.ElementVisible != false ? "Visible" : "Masqué")}",
            EffectKind.Opacity => $"Opacité: {(effect.Opacity ?? 1.0):F2}",
            EffectKind.Rotation => $"Rotation: {effect.Rotation ?? 0} deg",
            EffectKind.Animation => $"Animation: {effect.Animation?.ToString() ?? "None"}",
            EffectKind.ColorFilter => $"Filtre: {effect.ColorFilterColor ?? "-"} ({effect.ColorFilterOpacity ?? 1.0:F2}){(effect.ColorFilterHalo == true ? ", halo" : "")}",
            _ => kind.ToString()
        };
    }

    private void OnAddActiveEffectClick(object sender, RoutedEventArgs e)
    {
        var availableKinds = new HashSet<EffectKind>(
            _effectTypeLabels.Select(x => x.Kind).Except(_activeKinds));

        if (availableKinds.Count == 0)
        {
            MessageBox.Show("Tous les types d'effet sont déjà ajoutés.", "Information",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new EffectEditorDialog(
            existingEffect: null,
            effectKind: null,
            availableKinds)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _activeKinds.Add(dialog.ResultKind);
            _effectValues[dialog.ResultKind] = dialog.ResultEffect;
            RefreshActiveEffectsList();
            SelectEffectInList(dialog.ResultKind);
            UpdatePreview();
        }
    }

    private void OnEditActiveEffectClick(object sender, RoutedEventArgs e)
    {
        if (ActiveEffectsListBox.SelectedItem is not EffectListItem item)
            return;

        var existingEffect = _effectValues.TryGetValue(item.Kind, out var eff)
            ? eff : ScadaEffectBlock.Empty;

        var dialog = new EffectEditorDialog(
            existingEffect: existingEffect,
            effectKind: item.Kind,
            availableKinds: new HashSet<EffectKind>())
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _effectValues[dialog.ResultKind] = dialog.ResultEffect;
            RefreshActiveEffectsList();
            UpdatePreview();
        }
    }

    private void OnRemoveActiveEffectClick(object sender, RoutedEventArgs e)
    {
        if (ActiveEffectsListBox.SelectedItem is not EffectListItem item)
            return;

        _activeKinds.Remove(item.Kind);
        _effectValues.Remove(item.Kind);
        RefreshActiveEffectsList();
        UpdatePreview();
    }

    private void SelectEffectInList(EffectKind kind)
    {
        for (int i = 0; i < ActiveEffectsListBox.Items.Count; i++)
        {
            if (ActiveEffectsListBox.Items[i] is EffectListItem item && item.Kind == kind)
            {
                ActiveEffectsListBox.SelectedIndex = i;
                return;
            }
        }
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
        // Primary: match by Id (canonical)
        for (int i = 0; i < TagComboBox.Items.Count; i++)
        {
            if (TagComboBox.Items[i] is TagItem item &&
                string.Equals(item.TagId, tagName, StringComparison.Ordinal))
            {
                TagComboBox.SelectedIndex = i;
                return;
            }
        }

        // Fallback 1: match by DisplayName (backward compat)
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

        // Fallback 2: match by KeywordLabel
        for (int i = 0; i < TagComboBox.Items.Count; i++)
        {
            if (TagComboBox.Items[i] is TagItem item)
            {
                var tag = (_tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
                    .FirstOrDefault(t => t.Id == item.TagId);
                if (tag?.KeywordLabel is not null &&
                    string.Equals(tag.KeywordLabel, tagName, StringComparison.OrdinalIgnoreCase))
                {
                    TagComboBox.SelectedIndex = i;
                    return;
                }
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
            _effectValues[EffectKind.BackgroundColor] = ScadaEffectBlock.Empty with
            {
                BackgroundColor = effect.BackgroundColor
            };
        }

        if (effect.BorderColor is not null || effect.BorderWidth is not null)
        {
            _activeKinds.Add(EffectKind.Border);
            _effectValues[EffectKind.Border] = ScadaEffectBlock.Empty with
            {
                BorderColor = effect.BorderColor,
                BorderWidth = effect.BorderWidth
            };
        }

        if (effect.TextContent is not null || effect.TextColor is not null || effect.TextVisible is not null)
        {
            _activeKinds.Add(EffectKind.Text);
            _effectValues[EffectKind.Text] = ScadaEffectBlock.Empty with
            {
                TextContent = effect.TextContent,
                TextColor = effect.TextColor,
                TextVisible = effect.TextVisible
            };
        }

        if (effect.ElementVisible is not null)
        {
            _activeKinds.Add(EffectKind.ElementVisible);
            _effectValues[EffectKind.ElementVisible] = ScadaEffectBlock.Empty with
            {
                ElementVisible = effect.ElementVisible
            };
        }

        if (effect.Opacity is not null)
        {
            _activeKinds.Add(EffectKind.Opacity);
            _effectValues[EffectKind.Opacity] = ScadaEffectBlock.Empty with
            {
                Opacity = effect.Opacity
            };
        }

        if (effect.Rotation is not null)
        {
            _activeKinds.Add(EffectKind.Rotation);
            _effectValues[EffectKind.Rotation] = ScadaEffectBlock.Empty with
            {
                Rotation = effect.Rotation
            };
        }

        if (effect.Animation is not null)
        {
            _activeKinds.Add(EffectKind.Animation);
            _effectValues[EffectKind.Animation] = ScadaEffectBlock.Empty with
            {
                Animation = effect.Animation
            };
        }

        if (effect.ColorFilterColor is not null)
        {
            _activeKinds.Add(EffectKind.ColorFilter);
            _effectValues[EffectKind.ColorFilter] = ScadaEffectBlock.Empty with
            {
                ColorFilterColor = effect.ColorFilterColor,
                ColorFilterOpacity = effect.ColorFilterOpacity,
                ColorFilterHalo = effect.ColorFilterHalo,
                ColorFilterHaloColor = effect.ColorFilterHaloColor
            };
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
        var merged = ScadaEffectBlock.Empty;
        foreach (var (_, block) in _effectValues)
        {
            merged = merged with
            {
                BackgroundColor = block.BackgroundColor ?? merged.BackgroundColor,
                BorderColor = block.BorderColor ?? merged.BorderColor,
                BorderWidth = block.BorderWidth ?? merged.BorderWidth,
                TextColor = block.TextColor ?? merged.TextColor,
                TextContent = block.TextContent ?? merged.TextContent,
                TextVisible = block.TextVisible ?? merged.TextVisible,
                ElementVisible = block.ElementVisible ?? merged.ElementVisible,
                Opacity = block.Opacity ?? merged.Opacity,
                Rotation = block.Rotation ?? merged.Rotation,
                Animation = block.Animation ?? merged.Animation,
                ColorFilterColor = block.ColorFilterColor ?? merged.ColorFilterColor,
                ColorFilterOpacity = block.ColorFilterOpacity ?? merged.ColorFilterOpacity,
                ColorFilterHalo = block.ColorFilterHalo ?? merged.ColorFilterHalo,
                ColorFilterHaloColor = block.ColorFilterHaloColor ?? merged.ColorFilterHaloColor
            };
        }
        return merged;
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

        var parsed = ScadaExpression.FromSource(source);
        ScadaExprNode? ast = parsed.Ast;
        if (ast is not null)
            ast = ResolveTagIds(ast, _tagCatalog, SelectedTag?.Id, SelectedTag?.DisplayName);
        var expression = ScadaExpression.FromAst(source, ast);

        Result = new ScadaStateRule(
            _ruleId,
            NameTextBox.Text.Trim(),
            Enabled: true,
            Expression: expression,
            Effect: BuildEffectFromUi());

        DialogResult = true;
    }

    /// <summary>
    /// Resolves <see cref="ScadaExprTagRef.TagId"/> for every tag reference in the AST.
    /// When the expression was built from the dropdown (single tag selected),
    /// <paramref name="selectedTagId"/> and <paramref name="selectedTagDisplayName"/>
    /// provide the canonical Id and bypass catalog lookup (D2).
    /// </summary>
    private static ScadaExprNode ResolveTagIds(
        ScadaExprNode node, ScadaTagCatalog? catalog,
        string? selectedTagId, string? selectedTagDisplayName)
    {
        return node switch
        {
            ScadaExprTagRef tagRef => ResolveSingleTagRef(
                tagRef, catalog, selectedTagId, selectedTagDisplayName),
            ScadaExprUnary unary =>
                new ScadaExprUnary(unary.Op, ResolveTagIds(
                    unary.Operand, catalog, selectedTagId, selectedTagDisplayName)),
            ScadaExprBinary binary =>
                new ScadaExprBinary(binary.Op,
                    ResolveTagIds(binary.Left, catalog, selectedTagId, selectedTagDisplayName),
                    ResolveTagIds(binary.Right, catalog, selectedTagId, selectedTagDisplayName)),
            ScadaExprFunc func =>
                new ScadaExprFunc(func.Name,
                    func.Args.Select(a => ResolveTagIds(
                        a, catalog, selectedTagId, selectedTagDisplayName)).ToArray()),
            _ => node
        };
    }

    /// <summary>
    /// Resolves a single TagRef:
    /// 1. If TagId is already present (re-edition), keep it.
    /// 2. If the selectedTagId matches this TagRef's TagName (dropdown-created expression),
    ///    use selectedTagId directly — avoids ambiguity when DisplayName is duplicated (D2).
    /// 3. Otherwise, try to resolve via the catalog.
    /// </summary>
    private static ScadaExprTagRef ResolveSingleTagRef(
        ScadaExprTagRef tagRef, ScadaTagCatalog? catalog,
        string? selectedTagId, string? selectedTagDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(tagRef.TagId))
            return tagRef; // déjà résolu (ré-édition)

        // Priorité dropdown : le tag sélectionné explicitement par l'utilisateur
        if (!string.IsNullOrWhiteSpace(selectedTagId) &&
            string.Equals(tagRef.TagName, selectedTagDisplayName, StringComparison.OrdinalIgnoreCase))
        {
            return new ScadaExprTagRef(tagRef.TagName, selectedTagId);
        }

        // Fallback catalogue pour les expressions manuelles
        if (catalog is not null)
        {
            var result = ScadaExpressionValidator.TryResolveTagReference(
                tagRef.TagName, catalog);
            if (result.Status == TagResolveStatus.Resolved && result.CanonicalId is not null)
                return new ScadaExprTagRef(tagRef.TagName, result.CanonicalId);
        }

        return tagRef; // non résolu : garder TagName sans TagId
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
