# State Editor Effect Dialog & Hue Slider Fix — Implementation Plan

Date: 2026-07-09
Status: Draft implementation plan — pending execution approval
Document version: `V2.1.3.0001`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-09 | `V2.1.3.0001` | `c9f9b17` | Correction des frontières de commits compilables, des valeurs par défaut du dialogue, des tests contractuels et des commandes PowerShell. |
| 2026-07-09 | `V2.1.3.0000` | `c9f9b17` | Création du plan d'implémentation pour le dialogue de configuration d'effet, le stockage par dictionnaire et le correctif de teinte du ColorPicker. |

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the inline effect editor in `ElementStateRuleDialog` with a modal `EffectEditorDialog`, fix the frozen hue slider in `ColorPickerDialog`, and extract shared UI types.

**Architecture:** Extract `EffectKind` and `EffectTypeItem` from `ElementStateRuleDialog` into shared `internal` types. Create `EffectEditorDialog` (Window) with dynamic parameter panels gated by type selection. Store effect values in a `Dictionary<EffectKind, ScadaEffectBlock>` inside `ElementStateRuleDialog` instead of reading scattered WPF controls. Fix `ColorPickerDialog.ToHsv` with a `fallbackHue` parameter and force saturation/value on hue slider move.

**Tech Stack:** WPF (.NET 8-windows), C# 12, MSTest

## Global Constraints

- No changes to `ScadaEffectBlock`, `ScadaStateRule`, or `ScadaElementStateConfig` (domain model stable)
- `BuildEffectFromUi()` must produce the same cumulative `ScadaEffectBlock` as before
- Editor artifacts (dialog, preview) must not leak into `.sb2`/`.sep` export
- PowerShell commands must be run from `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`
- Do not commit an intentionally non-compiling intermediate state; each code commit must build
- `dotnet test --filter "FullyQualifiedName~Ft100SceneExporterTests"` must stay green
- APIs: XML docs on public surfaces
- Spec: `docs/superpowers/specs/2026-07-09-state-editor-effect-dialog-design.md`
- Version: `V2.1.3.0000`

---

### Task 1: Extract shared UI types from ElementStateRuleDialog

**Files:**
- Create: `src/ScadaBuilderV2.App/EffectKind.cs`
- Create: `src/ScadaBuilderV2.App/EffectTypeItem.cs`
- Modify: `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs:59-64`

**Interfaces:**
- Produces: `internal enum EffectKind` (8 members: BackgroundColor, Border, Text, ElementVisible, Opacity, Rotation, Animation, ColorFilter)
- Produces: `internal sealed record EffectTypeItem(EffectKind Kind, string Label)` with `ToString() => Label`
- Consumes: none (foundational)

- [ ] **Step 1: Create `EffectKind.cs`**

Create `src/ScadaBuilderV2.App/EffectKind.cs`:

```csharp
namespace ScadaBuilderV2.App;

/// <summary>
/// Categories of visual effects that can be applied by an Element+ state rule.
/// </summary>
internal enum EffectKind
{
    BackgroundColor,
    Border,
    Text,
    ElementVisible,
    Opacity,
    Rotation,
    Animation,
    ColorFilter
}
```

- [ ] **Step 2: Create `EffectTypeItem.cs`**

Create `src/ScadaBuilderV2.App/EffectTypeItem.cs`:

```csharp
namespace ScadaBuilderV2.App;

/// <summary>
/// Pairs an <see cref="EffectKind"/> with its French label for UI display.
/// </summary>
internal sealed record EffectTypeItem(EffectKind Kind, string Label)
{
    /// <inheritdoc />
    public override string ToString() => Label;
}
```

- [ ] **Step 3: Remove private types from `ElementStateRuleDialog.xaml.cs`**

In `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs`, delete lines 59-64 (the private `enum EffectKind` and private `record EffectTypeItem`). These are now provided by the new shared files above.

```csharp
// DELETE these lines:
// private enum EffectKind { BackgroundColor, Border, Text, ElementVisible, Opacity, Rotation, Animation, ColorFilter }
//
// private sealed record EffectTypeItem(EffectKind Kind, string Label)
// {
//     public override string ToString() => Label;
// }
```

- [ ] **Step 4: Build and verify**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet build src/ScadaBuilderV2.App/ScadaBuilderV2.App.csproj
```
Expected: Build succeeds with no errors.

- [ ] **Step 5: Commit**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add src/ScadaBuilderV2.App/EffectKind.cs src/ScadaBuilderV2.App/EffectTypeItem.cs src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs
git commit -m "refactor: extract EffectKind and EffectTypeItem into shared internal types"
```

---

### Task 2: Fix frozen hue slider in ColorPickerDialog

**Files:**
- Modify: `src/ScadaBuilderV2.App/ColorPickerDialog.xaml.cs:127-136` (OnHueSliderChanged)
- Modify: `src/ScadaBuilderV2.App/ColorPickerDialog.xaml.cs:223` (UpdateColorControls call to ToHsv)
- Modify: `src/ScadaBuilderV2.App/ColorPickerDialog.xaml.cs:346-369` (ToHsv signature and body)

**Interfaces:**
- Consumes: none
- Produces: `ToHsv(Color color, double fallbackHue = 0)` — static, backward-compatible default
- Produces: `OnHueSliderChanged` — forces saturation ≥ 0 and value ≥ 0 before color reconstruction

- [ ] **Step 1: Add `fallbackHue` parameter to `ToHsv`**

In `ColorPickerDialog.xaml.cs`, change the `ToHsv` signature from:

```csharp
private static (double Hue, double Saturation, double Value) ToHsv(Color color)
```

To:

```csharp
private static (double Hue, double Saturation, double Value) ToHsv(Color color, double fallbackHue = 0)
```

In the method body, change line 355 from:

```csharp
var hue = delta == 0
    ? 0
```

To:

```csharp
var hue = delta == 0
    ? fallbackHue
```

- [ ] **Step 2: Pass `_hue` as fallback in `UpdateColorControls`**

In `UpdateColorControls`, change line 234 from:

```csharp
(var hue, var saturation, var value) = ToHsv(color);
```

To:

```csharp
(var hue, var saturation, var value) = ToHsv(color, fallbackHue: _hue);
```

- [ ] **Step 3: Force saturation and value in `OnHueSliderChanged`**

Replace the body of `OnHueSliderChanged` (lines 127-136) with:

```csharp
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
```

- [ ] **Step 4: Build and verify compilation**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet build src/ScadaBuilderV2.App/ScadaBuilderV2.App.csproj
```
Expected: Build succeeds.

- [ ] **Step 5: Commit**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add src/ScadaBuilderV2.App/ColorPickerDialog.xaml.cs
git commit -m "fix: prevent hue slider freeze on achromatic colors in ColorPickerDialog"
```

---

### Task 3: Create EffectEditorDialog XAML

**Files:**
- Create: `src/ScadaBuilderV2.App/EffectEditorDialog.xaml`

**Interfaces:**
- Produces: WPF Window with `x:Class="ScadaBuilderV2.App.EffectEditorDialog"`, named controls for each effect type's parameter panel
- Consumes: `EffectKind`, `EffectTypeItem` (from Task 1), `ColorPickerField` (existing)

- [ ] **Step 1: Create `EffectEditorDialog.xaml`**

Create `src/ScadaBuilderV2.App/EffectEditorDialog.xaml`:

```xml
<Window x:Class="ScadaBuilderV2.App.EffectEditorDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:ScadaBuilderV2.App"
        Title="Configurer un effet" Width="420" Height="440"
        WindowStartupLocation="CenterOwner" ResizeMode="NoResize">
    <Window.Resources>
        <SolidColorBrush x:Key="InkBrush" Color="#0F2A30"/>
        <SolidColorBrush x:Key="MutedBrush" Color="#5E7A82"/>
        <SolidColorBrush x:Key="PanelBrush" Color="#F7FBF5"/>
        <SolidColorBrush x:Key="BorderBrushSoft" Color="#DCE8DD"/>
        <Style x:Key="PrimaryButtonStyle" TargetType="Button">
            <Setter Property="Background" Value="#2090A0"/>
            <Setter Property="BorderBrush" Value="#0F7280"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="Padding" Value="12,6"/>
        </Style>
    </Window.Resources>
    <Grid Background="{StaticResource PanelBrush}" Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Type selection (Add mode) or label (Edit mode) -->
        <StackPanel Grid.Row="0" Margin="0,0,0,8">
            <TextBlock Text="Type" Foreground="{StaticResource MutedBrush}"/>
            <ListBox x:Name="TypeListBox" Height="130" Margin="0,2,0,0"
                     DisplayMemberPath="Label"
                     SelectionChanged="OnTypeSelectionChanged"/>
            <TextBlock x:Name="TypeLabel" Text="" FontWeight="SemiBold"
                       Foreground="{StaticResource InkBrush}" Visibility="Collapsed"/>
        </StackPanel>

        <!-- Dynamic parameter panels -->
        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
            <StackPanel x:Name="ParameterPanel" Visibility="Collapsed">

                <!-- Couleur de fond -->
                <StackPanel x:Name="BackgroundColorPanel" Visibility="Collapsed">
                    <TextBlock Text="Couleur de fond" Foreground="{StaticResource MutedBrush}"/>
                    <local:ColorPickerField x:Name="BgColorPicker" Margin="0,2,0,0"/>
                </StackPanel>

                <!-- Bordure -->
                <StackPanel x:Name="BorderPanel" Visibility="Collapsed">
                    <TextBlock Text="Couleur de bordure" Foreground="{StaticResource MutedBrush}"/>
                    <local:ColorPickerField x:Name="BorderColorPicker" Margin="0,2,0,0"/>
                    <TextBlock Text="Largeur (px)" Foreground="{StaticResource MutedBrush}" Margin="0,6,0,0"/>
                    <TextBox x:Name="BorderWidthBox" Width="80" Margin="0,2,0,0"
                             HorizontalAlignment="Left"/>
                </StackPanel>

                <!-- Texte -->
                <StackPanel x:Name="TextPanel" Visibility="Collapsed">
                    <TextBlock Text="Contenu" Foreground="{StaticResource MutedBrush}"/>
                    <TextBox x:Name="TextContentBox" Margin="0,2,0,4"/>
                    <TextBlock Text="Couleur du texte" Foreground="{StaticResource MutedBrush}"/>
                    <local:ColorPickerField x:Name="TextColorPicker" Margin="0,2,0,4"/>
                    <CheckBox x:Name="TextVisibleCheckBox" Content="Texte visible"/>
                </StackPanel>

                <!-- Visibilité élément -->
                <StackPanel x:Name="ElementVisiblePanel" Visibility="Collapsed">
                    <TextBlock Text="Visibilité de l'élément"
                               Foreground="{StaticResource MutedBrush}"/>
                    <CheckBox x:Name="ElementVisibleCheckBox" Content="Élément visible"
                              Margin="0,4,0,0"/>
                </StackPanel>

                <!-- Opacité -->
                <StackPanel x:Name="OpacityPanel" Visibility="Collapsed">
                    <TextBlock Text="Opacité" Foreground="{StaticResource MutedBrush}"/>
                    <Slider x:Name="OpacitySlider" Minimum="0" Maximum="1"
                            TickFrequency="0.01" IsMoveToPointEnabled="True"
                            ValueChanged="OnOpacitySliderChanged" Margin="0,4,0,0"/>
                    <TextBlock x:Name="OpacityValueText" Text="1.00"
                               HorizontalAlignment="Right" Foreground="{StaticResource MutedBrush}"/>
                </StackPanel>

                <!-- Rotation -->
                <StackPanel x:Name="RotationPanel" Visibility="Collapsed">
                    <TextBlock Text="Rotation (degrés)" Foreground="{StaticResource MutedBrush}"/>
                    <TextBox x:Name="RotationBox" Width="80" Margin="0,2,0,0"
                             HorizontalAlignment="Left"/>
                </StackPanel>

                <!-- Animation -->
                <StackPanel x:Name="AnimationPanel" Visibility="Collapsed">
                    <TextBlock Text="Animation" Foreground="{StaticResource MutedBrush}"/>
                    <ComboBox x:Name="AnimationComboBox" Margin="0,2,0,0"
                              HorizontalAlignment="Left" Width="160"/>
                </StackPanel>

                <!-- Filtre de couleur -->
                <StackPanel x:Name="ColorFilterPanel" Visibility="Collapsed">
                    <TextBlock Text="Couleur du filtre" Foreground="{StaticResource MutedBrush}"/>
                    <local:ColorPickerField x:Name="FilterColorPicker" Margin="0,2,0,4"/>
                    <TextBlock Text="Opacité du filtre" Foreground="{StaticResource MutedBrush}"/>
                    <Slider x:Name="FilterOpacitySlider" Minimum="0" Maximum="1"
                            TickFrequency="0.01" IsMoveToPointEnabled="True"
                            ValueChanged="OnFilterOpacitySliderChanged" Margin="0,4,0,4"/>
                    <TextBlock x:Name="FilterOpacityValueText" Text="1.00"
                               HorizontalAlignment="Right" Foreground="{StaticResource MutedBrush}"/>
                    <CheckBox x:Name="FilterHaloCheckBox" Content="Halo"
                              Checked="OnFilterHaloChanged" Unchecked="OnFilterHaloChanged"/>
                    <StackPanel x:Name="FilterHaloPanel" Visibility="Collapsed" Margin="0,4,0,0">
                        <TextBlock Text="Couleur du halo" Foreground="{StaticResource MutedBrush}"/>
                        <local:ColorPickerField x:Name="FilterHaloColorPicker" Margin="0,2,0,0"/>
                    </StackPanel>
                </StackPanel>

            </StackPanel>
        </ScrollViewer>

        <!-- Buttons -->
        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right"
                    Margin="0,8,0,0">
            <Button Content="Annuler" IsCancel="True" Margin="0,0,8,0"/>
            <Button x:Name="SaveButton" Content="Ajouter"
                    Style="{StaticResource PrimaryButtonStyle}" Click="OnSaveClick"/>
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 2: Continue directly to Task 4 before building**

Do not build or commit after creating only the XAML. The XAML references code-behind handlers
(`OnTypeSelectionChanged`, `OnSaveClick`, slider handlers, halo handlers) that are created in
Task 4, so this intermediate state is intentionally incomplete.

- [ ] **Step 3: Do not commit this task alone**

Task 3 and Task 4 share one commit after the app project builds successfully.

---

### Task 4: Create EffectEditorDialog code-behind

**Files:**
- Create: `src/ScadaBuilderV2.App/EffectEditorDialog.xaml.cs`
- Modify: `src/ScadaBuilderV2.App/EffectEditorDialog.xaml` (wire up new event handlers if needed)

**Interfaces:**
- Consumes: `EffectKind`, `EffectTypeItem` (Task 1), `ColorPickerDialog.TryParseCssColor`, `ScadaEffectBlock`, `ScadaAnimation`
- Produces: `EffectEditorDialog(ScadaEffectBlock? existingEffect, EffectKind? effectKind, IReadOnlySet<EffectKind> availableKinds)`
- Produces: `EffectKind ResultKind { get; }`
- Produces: `ScadaEffectBlock ResultEffect { get; }`

- [ ] **Step 1: Create `EffectEditorDialog.xaml.cs`**

Create `src/ScadaBuilderV2.App/EffectEditorDialog.xaml.cs`:

```csharp
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
public partial class EffectEditorDialog : Window
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
```

- [ ] **Step 2: Build and verify**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet build src/ScadaBuilderV2.App/ScadaBuilderV2.App.csproj
```
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add src/ScadaBuilderV2.App/EffectEditorDialog.xaml src/ScadaBuilderV2.App/EffectEditorDialog.xaml.cs
git commit -m "feat: add EffectEditorDialog with type selection and per-kind validation"
```

---

### Task 5: Modify ElementStateRuleDialog XAML (remove inline editor)

**Files:**
- Modify: `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml:77-145`

**Interfaces:**
- Consumes: `EffectEditorDialog` (Task 3-4)
- Produces: Simplified XAML with `ActiveEffectsListBox` and Add/Edit/Remove buttons only

- [ ] **Step 1: Replace the effects editing zone in XAML**

In `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml`, replace the entire `ScrollViewer` content (lines 77-151) with the simplified layout.

**Delete** lines 77 through 151 (everything inside the `ScrollViewer`, including its opening and the preview section that follows it).

**Replace with:**

```xml
        <ScrollViewer Grid.Row="1">
            <StackPanel>
                <TextBlock Text="Modifications d'apparence"
                           FontWeight="SemiBold"
                           Foreground="{StaticResource InkBrush}"
                           Margin="0,0,0,4"/>
                <ListBox x:Name="ActiveEffectsListBox"
                         Height="150"
                         SelectionMode="Single"
                         Margin="0,0,0,4"
                         DisplayMemberPath="Summary"
                         MouseDoubleClick="OnEditActiveEffectClick"/>
                <StackPanel Orientation="Horizontal" Margin="0,4,0,8">
                    <Button Content="+ Ajouter"
                            Click="OnAddActiveEffectClick"
                            Margin="0,0,6,0"/>
                    <Button Content="Éditer"
                            Click="OnEditActiveEffectClick"
                            Margin="0,0,6,0"/>
                    <Button Content="Supprimer"
                            Click="OnRemoveActiveEffectClick"/>
                </StackPanel>

                <TextBlock Text="Aperçu"
                           Foreground="{StaticResource MutedBrush}"
                           Margin="0,8,0,4"/>
                <Border x:Name="PreviewBorder"
                        Width="160" Height="90"
                        BorderBrush="{StaticResource BorderBrushSoft}"
                        BorderThickness="1">
                    <TextBlock x:Name="PreviewText"
                               HorizontalAlignment="Center"
                               VerticalAlignment="Center"/>
                </Border>
            </StackPanel>
        </ScrollViewer>
```

- [ ] **Step 2: Continue directly to Task 6 before building**

Do not build or commit after changing only the XAML. The existing code-behind still references
controls removed in this task (`EffectTypeComboBox`, `EffectEditorPanel`, and per-effect editor
controls). Task 6 removes those references.

- [ ] **Step 3: Do not commit this task alone**

Task 5 and Task 6 share one commit after the app project builds successfully.

---

### Task 6: Modify ElementStateRuleDialog code-behind (wire to dialog)

**Files:**
- Modify: `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs`

**Interfaces:**
- Consumes: `EffectEditorDialog` (Task 3-4), `EffectKind`, `EffectTypeItem` (Task 1)
- Produces: `BuildEffectFromUi()` — same return type, now reads from `Dictionary<EffectKind, ScadaEffectBlock>`

- [ ] **Step 1: Add dictionary field**

At the top of the class, add the dictionary field alongside `_activeKinds`:

```csharp
private readonly Dictionary<EffectKind, ScadaEffectBlock> _effectValues = new();
```

The `EffectListItem` private record and `_effectTypeLabels` static field both stay — they are still used by the ListBox display and the available-kinds computation in `OnAddActiveEffectClick`.

- [ ] **Step 2: Delete `RefreshEffectTypeComboBox`**

The `EffectTypeComboBox` is removed from XAML. Delete the method `RefreshEffectTypeComboBox` entirely (lines 82-93). The `RefreshActiveEffectsList` method is kept (still populates the ListBox).

- [ ] **Step 3: Rewrite `BuildEffectSummary` to read from dictionary**

Replace `BuildEffectSummary` (lines 104-115) with:

```csharp
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
```

- [ ] **Step 4: Rewrite `OnAddActiveEffectClick`**

Replace `OnAddActiveEffectClick` (lines 130-153) with:

```csharp
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
```

- [ ] **Step 5: Rewrite `OnEditActiveEffectClick`**

Replace `OnEditActiveEffectClick` (lines 156-163) with:

```csharp
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
```

- [ ] **Step 6: Rewrite `OnRemoveActiveEffectClick`**

Replace `OnRemoveActiveEffectClick` (lines 166-177) with:

```csharp
private void OnRemoveActiveEffectClick(object sender, RoutedEventArgs e)
{
    if (ActiveEffectsListBox.SelectedItem is not EffectListItem item)
        return;

    _activeKinds.Remove(item.Kind);
    _effectValues.Remove(item.Kind);
    RefreshActiveEffectsList();
    UpdatePreview();
}
```

- [ ] **Step 7: Delete `ShowEffectEditor` and `OnColorFilterHaloChanged`**

Remove `ShowEffectEditor` (lines 117-128) entirely. Remove `OnColorFilterHaloChanged` (lines 180-183) — this is now handled inside `EffectEditorDialog`.

- [ ] **Step 8: Rewrite `LoadEffect` to populate dictionary**

Replace `LoadEffect` (lines 290-346) with:

```csharp
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
```

- [ ] **Step 9: Rewrite `BuildEffectFromUi` to merge from dictionary**

Replace `BuildEffectFromUi` (lines 429-446) with:

```csharp
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
```

- [ ] **Step 10: Rewrite `UpdatePreview` to use merged effect**

Replace `UpdatePreview` (lines 418-427) with:

```csharp
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
```

- [ ] **Step 11: Update constructor**

In the constructor, remove the call to `RefreshEffectTypeComboBox()` and the `ShowEffectEditor` call. Replace:

```csharp
// Remove:
// RefreshEffectTypeComboBox();
// RefreshActiveEffectsList();
// if (_activeKinds.Count > 0)
// {
//     ShowEffectEditor(_activeKinds.First());
//     ActiveEffectsListBox.SelectedIndex = 0;
// }

// Replace with:
RefreshActiveEffectsList();
```

- [ ] **Step 12: Add `SelectEffectInList` helper**

Add a new private method:

```csharp
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
```

- [ ] **Step 13: Build and verify**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet build src/ScadaBuilderV2.App/ScadaBuilderV2.App.csproj
```
Expected: Build succeeds with no errors.

- [ ] **Step 14: Commit**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs
git commit -m "refactor: wire ElementStateRuleDialog to EffectEditorDialog, use dictionary storage"
```

---

### Task 7: Run existing tests & verify no regressions

**Files:**
- No new files. Run existing test suite.

- [ ] **Step 1: Run the full test suite**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet test ScadaBuilderV2.sln
```
Expected: All existing tests pass.

- [ ] **Step 2: Run export-specific tests**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~Ft100SceneExporterTests"
```
Expected: All export tests pass. This confirms the `.sb2` contract is unchanged.

- [ ] **Step 3: Run ElementEvents tests**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaEffectBlockTests"
```
Expected: All pass.

- [ ] **Step 4: Run context menu / contract tests (reference ColorPickerField)**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~WebViewContextMenuScriptTests"
```
Expected: All pass (these reference `ColorPickerField` and `ColorPickerDialog` element names — verify no breakage).

- [ ] **Step 5: Record validation in the implementation closeout**

Do not create an empty commit for test execution. Record the commands run and their results in the
implementation closeout instead.

---

### Task 8: Add targeted contract tests

**Files:**
- Create: `tests/ScadaBuilderV2.Tests/ElementEvents/StateEditorEffectDialogContractTests.cs`

**Interfaces:**
- Tests the WPF implementation contract by reading source files, because `ScadaBuilderV2.Tests`
  does not currently reference the WPF app project and the relevant color math/event handlers are
  private UI code.
- Verifies `ToHsv` has `fallbackHue`, `UpdateColorControls` passes `_hue`, `OnHueSliderChanged`
  exits achromatic colors, `EffectEditorDialog` initializes default add-mode values, and
  `ElementStateRuleDialog` no longer contains the removed inline editor controls.

- [ ] **Step 1: Create contract test file**

Create `tests/ScadaBuilderV2.Tests/ElementEvents/StateEditorEffectDialogContractTests.cs`:

```csharp
namespace ScadaBuilderV2.Tests.ElementEvents;

[TestClass]
public sealed class StateEditorEffectDialogContractTests
{
    [TestMethod]
    public void ColorPickerDialog_PreservesHueFallbackForAchromaticColors()
    {
        var source = ReadAppFile("ColorPickerDialog.xaml.cs");

        StringAssert.Contains(source, "ToHsv(Color color, double fallbackHue = 0)");
        StringAssert.Contains(source, "? fallbackHue");
        StringAssert.Contains(source, "ToHsv(color, fallbackHue: _hue)");
    }

    [TestMethod]
    public void ColorPickerDialog_HueSliderEscapesGrayWhiteAndBlack()
    {
        var source = ReadAppFile("ColorPickerDialog.xaml.cs");

        StringAssert.Contains(source, "if (_saturation <= 0) _saturation = 1.0;");
        StringAssert.Contains(source, "if (_value <= 0) _value = 1.0;");
        StringAssert.Contains(source, "FromHsv(_hue, _saturation, _value)");
    }

    [TestMethod]
    public void EffectEditorDialog_AddModeInitializesDefaultValues()
    {
        var source = ReadAppFile("EffectEditorDialog.xaml.cs");

        StringAssert.Contains(source, "PopulateFields(ScadaEffectBlock.Empty)");
        StringAssert.Contains(source, "OpacitySlider.Value = effect.Opacity ?? 1.0");
        StringAssert.Contains(source, "FilterOpacitySlider.Value = effect.ColorFilterOpacity ?? 1.0");
        StringAssert.Contains(source, "FilterColorPicker.SetColor(effect.ColorFilterColor ?? \"#E53935\")");
    }

    [TestMethod]
    public void ElementStateRuleDialog_RemovesInlineEffectEditorControls()
    {
        var xaml = ReadAppFile("ElementStateRuleDialog.xaml");
        var code = ReadAppFile("ElementStateRuleDialog.xaml.cs");

        Assert.IsFalse(xaml.Contains("EffectEditorPanel", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("EffectTypeComboBox", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("ShowEffectEditor", StringComparison.Ordinal));
        StringAssert.Contains(code, "Dictionary<EffectKind, ScadaEffectBlock>");
        StringAssert.Contains(code, "new EffectEditorDialog(");
    }

    [TestMethod]
    public void EffectEditorDialog_ExposesRequiredResultContract()
    {
        var source = ReadAppFile("EffectEditorDialog.xaml.cs");

        StringAssert.Contains(source, "public EffectKind ResultKind");
        StringAssert.Contains(source, "public ScadaEffectBlock ResultEffect");
        StringAssert.Contains(source, "ColorPickerDialog.TryParseCssColor");
    }

    private static string ReadAppFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "ScadaBuilderV2.App", fileName);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate src/ScadaBuilderV2.App/{fileName} from test output directory.");
        return string.Empty;
    }
}
```

- [ ] **Step 2: Run the new contract tests**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~StateEditorEffectDialogContractTests"
```
Expected: All 5 tests pass.

- [ ] **Step 3: Commit**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add tests/ScadaBuilderV2.Tests/ElementEvents/StateEditorEffectDialogContractTests.cs
git commit -m "test: add state editor effect dialog contract tests"
```

---

### Task 9: Manual verification checklist

No code changes. Execute each item and confirm.

- [ ] **Step 1: Launch the app and open the state editor**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet run --project src/ScadaBuilderV2.App
```
Open a project with Element+ objects, open Properties, navigate to the State tab, open a state rule.

- [ ] **Step 2: Verify Add flow**
    - Click "+ Ajouter" → `EffectEditorDialog` opens with type list
    - Select each effect type one at a time, configure, click "Ajouter"
    - Verify each appears in the effects list with the correct summary text
    - Verify the preview updates

- [ ] **Step 3: Verify Edit flow**
    - Double-click an effect in the list → `EffectEditorDialog` opens in Edit mode
    - Modify parameters, click "Enregistrer"
    - Verify the summary text updates

- [ ] **Step 4: Verify Delete flow**
    - Select an effect, click "Supprimer" → removed from list
    - Verify preview updates

- [ ] **Step 5: Verify ColorPicker hue slider**
    - Open any ColorPickerDialog (click a ColorPickerField)
    - Start from white (#FFFFFF) → move the hue slider → color should change and slider should not snap back
    - Start from black (#000000) → move the hue slider → same
    - Start from gray → move the hue slider → same

- [ ] **Step 6: Verify Export**
    - Export a scene with states to `.sb2`
    - Inspect the output to confirm `ScadaEffectBlock` properties are present and correct

- [ ] **Step 7: Run full test suite one final time**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet test ScadaBuilderV2.sln
```
Expected: All green.

---

### Task 10: Update changelog and final commit

- [ ] **Step 1: Update spec changelog commit hash**

In `docs/superpowers/specs/2026-07-09-state-editor-effect-dialog-design.md`, update the `PENDING` commit hash in the changelog table to the final commit.

- [ ] **Step 2: Final commit**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add docs/superpowers/specs/2026-07-09-state-editor-effect-dialog-design.md
git commit -m "docs: finalize state editor effect dialog spec with commit reference"
```

---

## Plan Summary

| Task | Description | Files |
|---|---|---|
| 1 | Extract shared types | `EffectKind.cs` (new), `EffectTypeItem.cs` (new), `ElementStateRuleDialog.xaml.cs` |
| 2 | Fix hue slider | `ColorPickerDialog.xaml.cs` |
| 3 | EffectEditorDialog XAML | `EffectEditorDialog.xaml` (new) |
| 4 | EffectEditorDialog code-behind | `EffectEditorDialog.xaml.cs` (new) |
| 5 | Simplify ElementStateRuleDialog XAML | `ElementStateRuleDialog.xaml` |
| 6 | Wire ElementStateRuleDialog to dialog | `ElementStateRuleDialog.xaml.cs` |
| 7 | Run existing tests | none |
| 8 | Add targeted contract tests | `StateEditorEffectDialogContractTests.cs` (new) |
| 9 | Manual verification | none |
| 10 | Finalize docs | spec changelog |
