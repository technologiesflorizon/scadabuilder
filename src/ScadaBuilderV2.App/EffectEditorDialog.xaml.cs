using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.Domain.ElementEvents.State;

namespace ScadaBuilderV2.App;

/// <summary>
/// Modal dialog for adding or editing a single visual effect inside an Element+ state rule.
/// In Add mode the user picks the effect kind from a list; in Edit mode the kind is fixed.
/// </summary>
/// <remarks>
/// Decisions: D2, D3, D4, D7 from spec 2026-07-09.
/// </remarks>
internal partial class EffectEditorDialog : Window
{
    private readonly EffectKind? _fixedKind;
    private readonly IReadOnlySet<EffectKind> _availableKinds;
    private EffectKind _selectedKind;

    private static readonly (EffectKind Kind, string Label)[] _effectTypeLabels =
    [
        (EffectKind.BackgroundColor, "Couleur de fond"),
        (EffectKind.Border, "Bordure"),
        (EffectKind.Text, "Texte"),
        (EffectKind.ElementVisible, "Visibilité"),
        (EffectKind.Opacity, "Opacité"),
        (EffectKind.Rotation, "Rotation"),
        (EffectKind.Animation, "Animation"),
        (EffectKind.ColorFilter, "Filtre de couleur")
    ];

    /// <summary>
    /// Initializes the effect editor dialog.
    /// </summary>
    /// <param name="existingEffect">Current effect values, or null for Add mode.</param>
    /// <param name="effectKind">Fixed effect kind for Edit mode, or null for Add mode.</param>
    /// <param name="availableKinds">Kinds not yet used in the parent rule (Add mode only).</param>
    public EffectEditorDialog(
        ScadaEffectBlock? existingEffect,
        EffectKind? effectKind,
        IReadOnlySet<EffectKind> availableKinds)
    {
        InitializeComponent();
        _fixedKind = effectKind;
        _availableKinds = availableKinds;

        // Populate animation combo
        AnimationComboBox.ItemsSource = System.Enum.GetValues<ScadaAnimation>();
        AnimationComboBox.SelectedIndex = 0;

        if (effectKind is not null)
        {
            // Edit mode: type is fixed, label only
            TypeListBox.Visibility = Visibility.Collapsed;
            TypeLabel.Text = GetLabel(effectKind.Value);
            TypeLabel.Visibility = Visibility.Visible;
            SaveButton.Content = "Enregistrer";
            _selectedKind = effectKind.Value;
            ShowParameterPanel(_selectedKind);
            PopulateFields(existingEffect ?? ScadaEffectBlock.Empty);
        }
        else
        {
            // Add mode: show available kinds in list
            var availableItems = _effectTypeLabels
                .Where(x => availableKinds.Contains(x.Kind))
                .Select(x => new EffectTypeItem(x.Kind, x.Label))
                .ToArray();
            TypeListBox.ItemsSource = availableItems;
            if (availableItems.Length > 0)
            {
                TypeListBox.SelectedIndex = 0;
                if (TypeListBox.SelectedItem is EffectTypeItem selected)
                {
                    _selectedKind = selected.Kind;
                    ShowParameterPanel(_selectedKind);
                    PopulateFields(ScadaEffectBlock.Empty);
                }
            }
        }
    }

    /// <summary>
    /// Gets the effect kind configured by the user.
    /// </summary>
    public EffectKind ResultKind => _selectedKind;

    /// <summary>
    /// Gets the partial <see cref="ScadaEffectBlock"/> with the user's configured values.
    /// </summary>
    public ScadaEffectBlock ResultEffect { get; private set; } = ScadaEffectBlock.Empty;

    private void OnTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TypeListBox.SelectedItem is EffectTypeItem item)
        {
            _selectedKind = item.Kind;
            ShowParameterPanel(_selectedKind);
            PopulateFields(ScadaEffectBlock.Empty);
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_fixedKind is null && TypeListBox.SelectedItem is null)
        {
            MessageBox.Show("Veuillez sélectionner un type d'effet.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!ValidateCurrentKind(out var message))
        {
            MessageBox.Show(message, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultEffect = BuildEffectForKind(_selectedKind);
        DialogResult = true;
    }

    private void OnOpacitySliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValueText is not null)
            OpacityValueText.Text = OpacitySlider.Value.ToString("F2", CultureInfo.InvariantCulture);
    }

    private void OnFilterOpacitySliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FilterOpacityValueText is not null)
            FilterOpacityValueText.Text = FilterOpacitySlider.Value.ToString("F2", CultureInfo.InvariantCulture);
    }

    private void OnFilterHaloChanged(object sender, RoutedEventArgs e)
    {
        FilterHaloPanel.Visibility = FilterHaloCheckBox.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ShowParameterPanel(EffectKind kind)
    {
        ParameterPanel.Visibility = Visibility.Visible;
        BackgroundColorPanel.Visibility = kind == EffectKind.BackgroundColor ? Visibility.Visible : Visibility.Collapsed;
        BorderPanel.Visibility = kind == EffectKind.Border ? Visibility.Visible : Visibility.Collapsed;
        TextPanel.Visibility = kind == EffectKind.Text ? Visibility.Visible : Visibility.Collapsed;
        ElementVisiblePanel.Visibility = kind == EffectKind.ElementVisible ? Visibility.Visible : Visibility.Collapsed;
        OpacityPanel.Visibility = kind == EffectKind.Opacity ? Visibility.Visible : Visibility.Collapsed;
        RotationPanel.Visibility = kind == EffectKind.Rotation ? Visibility.Visible : Visibility.Collapsed;
        AnimationPanel.Visibility = kind == EffectKind.Animation ? Visibility.Visible : Visibility.Collapsed;
        ColorFilterPanel.Visibility = kind == EffectKind.ColorFilter ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool ValidateCurrentKind(out string message)
    {
        message = string.Empty;

        switch (_selectedKind)
        {
            case EffectKind.BackgroundColor:
                if (!IsValidCssColor(BgColorPicker.Value))
                { message = "La couleur de fond n'est pas valide."; return false; }
                break;

            case EffectKind.Border:
                if (!IsValidCssColor(BorderColorPicker.Value))
                { message = "La couleur de bordure n'est pas valide."; return false; }
                if (!double.TryParse(BorderWidthBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) || w <= 0)
                { message = "La largeur de bordure doit être un nombre positif."; return false; }
                break;

            case EffectKind.Rotation:
                if (!double.TryParse(RotationBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                { message = "La rotation doit être un nombre valide."; return false; }
                break;

            case EffectKind.ColorFilter:
                if (!IsValidCssColor(FilterColorPicker.Value))
                { message = "La couleur du filtre n'est pas valide."; return false; }
                if (FilterHaloCheckBox.IsChecked == true && !IsValidCssColor(FilterHaloColorPicker.Value))
                { message = "La couleur du halo n'est pas valide."; return false; }
                break;
        }

        return true;
    }

    private ScadaEffectBlock BuildEffectForKind(EffectKind kind)
    {
        return kind switch
        {
            EffectKind.BackgroundColor => ScadaEffectBlock.Empty with
            {
                BackgroundColor = BgColorPicker.Value
            },
            EffectKind.Border => ScadaEffectBlock.Empty with
            {
                BorderColor = BorderColorPicker.Value,
                BorderWidth = double.TryParse(BorderWidthBox.Text, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var bw) ? bw : null
            },
            EffectKind.Text => ScadaEffectBlock.Empty with
            {
                TextContent = TextContentBox.Text.Trim().Length > 0 ? TextContentBox.Text.Trim() : null,
                TextColor = TextColorPicker.Value,
                TextVisible = TextVisibleCheckBox.IsChecked
            },
            EffectKind.ElementVisible => ScadaEffectBlock.Empty with
            {
                ElementVisible = ElementVisibleCheckBox.IsChecked
            },
            EffectKind.Opacity => ScadaEffectBlock.Empty with
            {
                Opacity = OpacitySlider.Value
            },
            EffectKind.Rotation => ScadaEffectBlock.Empty with
            {
                Rotation = double.TryParse(RotationBox.Text, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var rot) ? rot : null
            },
            EffectKind.Animation => ScadaEffectBlock.Empty with
            {
                Animation = (ScadaAnimation?)AnimationComboBox.SelectedItem
            },
            EffectKind.ColorFilter => ScadaEffectBlock.Empty with
            {
                ColorFilterColor = FilterColorPicker.Value,
                ColorFilterOpacity = FilterOpacitySlider.Value,
                ColorFilterHalo = FilterHaloCheckBox.IsChecked,
                ColorFilterHaloColor = FilterHaloCheckBox.IsChecked == true
                    ? FilterHaloColorPicker.Value : null
            },
            _ => ScadaEffectBlock.Empty
        };
    }

    private void PopulateFields(ScadaEffectBlock effect)
    {
        switch (_selectedKind)
        {
            case EffectKind.BackgroundColor:
                if (effect.BackgroundColor is not null)
                    BgColorPicker.SetColor(effect.BackgroundColor);
                break;

            case EffectKind.Border:
                if (effect.BorderColor is not null)
                    BorderColorPicker.SetColor(effect.BorderColor);
                BorderWidthBox.Text = (effect.BorderWidth ?? 1).ToString(CultureInfo.InvariantCulture);
                break;

            case EffectKind.Text:
                TextContentBox.Text = effect.TextContent ?? string.Empty;
                TextColorPicker.SetColor(effect.TextColor ?? "#000000");
                TextVisibleCheckBox.IsChecked = effect.TextVisible ?? true;
                break;

            case EffectKind.ElementVisible:
                ElementVisibleCheckBox.IsChecked = effect.ElementVisible ?? true;
                break;

            case EffectKind.Opacity:
                OpacitySlider.Value = effect.Opacity ?? 1.0;
                OpacityValueText.Text = (effect.Opacity ?? 1.0).ToString("F2", CultureInfo.InvariantCulture);
                break;

            case EffectKind.Rotation:
                RotationBox.Text = effect.Rotation?.ToString(CultureInfo.InvariantCulture) ?? "0";
                break;

            case EffectKind.Animation:
                AnimationComboBox.SelectedItem = effect.Animation ?? ScadaAnimation.None;
                break;

            case EffectKind.ColorFilter:
                FilterColorPicker.SetColor(effect.ColorFilterColor ?? "#E53935");
                FilterOpacitySlider.Value = effect.ColorFilterOpacity ?? 1.0;
                FilterOpacityValueText.Text = (effect.ColorFilterOpacity ?? 1.0).ToString("F2", CultureInfo.InvariantCulture);
                FilterHaloCheckBox.IsChecked = effect.ColorFilterHalo ?? false;
                FilterHaloPanel.Visibility = FilterHaloCheckBox.IsChecked == true
                    ? Visibility.Visible : Visibility.Collapsed;
                FilterHaloColorPicker.SetColor(effect.ColorFilterHaloColor ?? effect.ColorFilterColor ?? "#E53935");
                break;
        }
    }

    private static bool IsValidCssColor(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && ColorPickerDialog.TryParseCssColor(value.Trim(), out _);
    }

    private static string GetLabel(EffectKind kind)
    {
        foreach (var (k, label) in _effectTypeLabels)
            if (k == kind) return label;
        return kind.ToString();
    }
}
