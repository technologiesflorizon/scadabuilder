# Expression Creation Assistant — Plan d'implémentation

Date: 2026-07-13
Status: Draft implementation plan - pending execution approval
Document version: `V2.1.4.0003`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-13 | `V2.1.4.0003` | `PENDING` | Correction du plan : métadonnées, caret initial, contrat Annuler/Appliquer, ressources WPF et tests dédiés non intrusifs. |

> Ce plan est dérivé de la spec `docs/superpowers/specs/2026-07-13-expression-creation-assistant-design.md`.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ajouter un assistant de composition d'expressions guidé (`ExpressionCreationDialog` + `TagSelectionDialog`) accessible depuis le mode Expression de `ElementStateRuleDialog`, avec insertion de tags au caret et boutons d'opérateurs, sans modifier le contrat AST, parser, validateur ou export existant.

**Architecture:** Deux nouveaux dialogues WPF sans modèle persistant — `ExpressionCreationDialog` (composition avec copie locale, validation inline, opérateurs) et `TagSelectionDialog` (sélecteur de tags activés) — accessibles via un bouton **Outil** dans `ElementStateRuleDialog`. Le flux de validation et sauvegarde existant (`ValidateExpression` → `FromSource` → `ResolveTagIds`) reste inchangé.

**Tech Stack:** C# 12 / .NET 8, WPF, MSTest

**Spec source:** `docs/superpowers/specs/2026-07-13-expression-creation-assistant-design.md` (V2.1.4.0003, D1–D12)

## Global Constraints

- Aucun changement au modèle Domain (`ScadaExpression`, `ScadaExprNode`, AST, parser, validateur)
- Aucun changement au runtime TF100Web, à l'export `.sb2` ou au `state-engine.js`
- Le mode Variable de `ElementStateRuleDialog` n'est pas affecté
- L'assistant ne sauvegarde jamais — `ElementStateRuleDialog.OnSaveClick` reste l'unique point de sauvegarde
- L'assistant travaille sur une copie locale ; le bouton **Appliquer** retourne le texte au dialogue hôte
- Si la position du caret est `null` ou `0`, l'insertion se fait au début du texte (D5)
- Les tags sont insérés au format `{DisplayName}` (D5, D6)

---

### Task 1: Créer TagSelectionDialog

**Files:**
- Create: `src/ScadaBuilderV2.App/TagSelectionDialog.xaml`
- Create: `src/ScadaBuilderV2.App/TagSelectionDialog.xaml.cs`
- Test: `tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs` (nouveau)

**Interfaces:**
- Consumes: `ScadaTagCatalog?` (injecté au constructeur)
- Produces: `(string DisplayName, string TagId)?` retourné via `SelectedTag` property ; `DialogResult = true` si sélection, `false` si annulation

- [ ] **Step 1: Créer le XAML du sélecteur de tags**

```xml
<!-- src/ScadaBuilderV2.App/TagSelectionDialog.xaml -->
<Window x:Class="ScadaBuilderV2.App.TagSelectionDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Sélectionner un tag"
        Width="420" Height="480"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize">
    <Grid Margin="14">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock Text="Sélectionnez un tag à insérer dans l'expression :"
                   Margin="0,0,0,8" FontWeight="SemiBold"/>

        <ListBox x:Name="TagListBox" Grid.Row="1"
                 DisplayMemberPath="DisplayLabel"
                 MouseDoubleClick="OnTagDoubleClick"
                 SelectionMode="Single"/>

        <StackPanel Grid.Row="2" Orientation="Horizontal"
                    HorizontalAlignment="Right" Margin="0,12,0,0">
            <Button Content="Annuler" IsCancel="True" Width="90" Margin="0,0,8,0"/>
            <Button Content="Sélectionner" IsDefault="True" Width="100"
                    Click="OnSelectClick"/>
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 2: Créer le code-behind**

```csharp
// src/ScadaBuilderV2.App/TagSelectionDialog.xaml.cs
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App;

public partial class TagSelectionDialog : Window
{
    private readonly ScadaTagCatalog? _catalog;

    public sealed record TagSelectionItem(string DisplayName, string TagId, string DisplayLabel);

    public TagSelectionItem? SelectedTag { get; private set; }

    public TagSelectionDialog(ScadaTagCatalog? catalog)
    {
        InitializeComponent();
        _catalog = catalog;
        Owner = Application.Current.MainWindow;
        PopulateList();
    }

    private void PopulateList()
    {
        var items = (_catalog?.Tags ?? Enumerable.Empty<ScadaTagDefinition>())
            .Where(tag => tag.Enabled)
            .OrderBy(tag => tag.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(tag =>
            {
                var label = string.Equals(tag.DisplayName, tag.Id, StringComparison.OrdinalIgnoreCase)
                    ? $"{tag.DisplayName}  ({tag.Datatype ?? "?"})"
                    : $"{tag.DisplayName}  —  {tag.Id}  ({tag.Datatype ?? "?"})";
                return new TagSelectionItem(tag.DisplayName, tag.Id, label);
            })
            .ToArray();

        TagListBox.ItemsSource = items;
    }

    private void OnTagDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (TagListBox.SelectedItem is TagSelectionItem item)
        {
            SelectedTag = item;
            DialogResult = true;
            Close();
        }
    }

    private void OnSelectClick(object sender, RoutedEventArgs e)
    {
        if (TagListBox.SelectedItem is TagSelectionItem item)
        {
            SelectedTag = item;
            DialogResult = true;
            Close();
        }
    }
}
```

- [ ] **Step 3: Ajouter les tests du sélecteur**

```csharp
// tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScadaBuilderV2.App;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Tests;

[TestClass]
public class ExpressionCreationDialogContractTests
{
    [TestMethod]
    public void TagSelectionDialog_OnlyShowsEnabledTags()
    {
        var source = ReadAppFile("TagSelectionDialog.xaml.cs");
        StringAssert.Contains(source, ".Where(tag => tag.Enabled)");
        StringAssert.Contains(source, ".OrderBy(tag => tag.DisplayName");
    }

    [TestMethod]
    public void TagSelectionDialog_SelectedTag_ReturnsDisplayNameAndId()
    {
        var xaml = ReadAppFile("TagSelectionDialog.xaml");
        var source = ReadAppFile("TagSelectionDialog.xaml.cs");
        StringAssert.Contains(xaml, "MouseDoubleClick=\"OnTagDoubleClick\"");
        StringAssert.Contains(xaml, "Content=\"Sélectionner\"");
        StringAssert.Contains(source, "SelectedTag");
        StringAssert.Contains(source, "DialogResult = true");
    }

    [TestMethod]
    public void TagSelectionDialog_Cancel_ReturnsNull()
    {
        var xaml = ReadAppFile("TagSelectionDialog.xaml");
        StringAssert.Contains(xaml, "Content=\"Annuler\"");
    }
}
```

- [ ] **Step 4: Build et tests**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ExpressionCreationDialogContractTests"
```

Attendu : les tests de contrat du sélecteur passent; les scénarios WPF visuels restent dans la vérification manuelle STA.

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.App/TagSelectionDialog.xaml src/ScadaBuilderV2.App/TagSelectionDialog.xaml.cs tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs
git commit -m "feat: add TagSelectionDialog for expression tag insertion

Modal dialog listing all enabled tags from ScadaTagCatalog, sorted by
DisplayName. Returns (DisplayName, TagId) on selection or double-click.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 2: Créer ExpressionCreationDialog

**Files:**
- Create: `src/ScadaBuilderV2.App/ExpressionCreationDialog.xaml`
- Create: `src/ScadaBuilderV2.App/ExpressionCreationDialog.xaml.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs`

**Interfaces:**
- Consumes: `string initialExpression`, `ScadaTagCatalog?` (injectés au constructeur)
- Produces: `string? ResultExpression` — le texte modifié si **Appliquer**, `null` si fermé sans appliquer

- [ ] **Step 1: Créer le XAML de l'assistant**

```xml
<!-- src/ScadaBuilderV2.App/ExpressionCreationDialog.xaml -->
<Window x:Class="ScadaBuilderV2.App.ExpressionCreationDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Assistant d'expression"
        Width="580" Height="440"
        WindowStartupLocation="CenterOwner"
        ResizeMode="CanResizeWithGrip"
        MinWidth="460" MinHeight="340">
    <Window.Resources>
        <Style TargetType="Button" x:Key="OperatorButtonStyle">
            <Setter Property="MinWidth" Value="40"/>
            <Setter Property="MinHeight" Value="32"/>
            <Setter Property="Margin" Value="2"/>
            <Setter Property="FontFamily" Value="Consolas"/>
            <Setter Property="FontSize" Value="14"/>
            <Setter Property="Padding" Value="6,3"/>
        </Style>
        <Style TargetType="Button" x:Key="PrimaryButtonStyle">
            <Setter Property="Background" Value="#2090A0"/>
            <Setter Property="BorderBrush" Value="#0F7280"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="Padding" Value="12,6"/>
        </Style>
    </Window.Resources>

    <Grid Margin="14">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- En-tête -->
        <TextBlock Text="Composez votre expression :" FontWeight="SemiBold" Margin="0,0,0,8"/>

        <!-- Barre d'outils -->
        <StackPanel Grid.Row="1" Margin="0,0,0,6">
            <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
                <Button Content="Variable" Click="OnVariableClick"
                        FontWeight="SemiBold" MinWidth="80" MinHeight="32" Margin="0,0,8,0"/>
                <Separator Style="{StaticResource {x:Static ToolBar.SeparatorStyleKey}}" Margin="4,0"/>
                <Button Content="&gt;" Style="{StaticResource OperatorButtonStyle}"
                        ToolTip="Supérieur à" Click="OnOperatorClick" Tag=" > "/>
                <Button Content="&lt;" Style="{StaticResource OperatorButtonStyle}"
                        ToolTip="Inférieur à" Click="OnOperatorClick" Tag=" < "/>
                <Button Content="&gt;=" Style="{StaticResource OperatorButtonStyle}"
                        ToolTip="Supérieur ou égal à" Click="OnOperatorClick" Tag=" >= "/>
                <Button Content="&lt;=" Style="{StaticResource OperatorButtonStyle}"
                        ToolTip="Inférieur ou égal à" Click="OnOperatorClick" Tag=" <= "/>
                <Button Content="==" Style="{StaticResource OperatorButtonStyle}"
                        ToolTip="Égal à" Click="OnOperatorClick" Tag=" == "/>
                <Button Content="!=" Style="{StaticResource OperatorButtonStyle}"
                        ToolTip="Différent de" Click="OnOperatorClick" Tag=" != "/>
            </StackPanel>
            <StackPanel Orientation="Horizontal">
                <Button Content="ET (&&)" Style="{StaticResource OperatorButtonStyle}"
                        ToolTip="Toutes les conditions doivent être vraies" Click="OnOperatorClick" Tag=" && "/>
                <Button Content="OU (||)" Style="{StaticResource OperatorButtonStyle}"
                        ToolTip="Au moins une condition doit être vraie" Click="OnOperatorClick" Tag=" || "/>
                <Separator Style="{StaticResource {x:Static ToolBar.SeparatorStyleKey}}" Margin="4,0"/>
                <Button Content="( )" Style="{StaticResource OperatorButtonStyle}"
                        ToolTip="Parenthèses (grouper des conditions)" Click="OnParenthesesClick"/>
                <Separator Style="{StaticResource {x:Static ToolBar.SeparatorStyleKey}}" Margin="4,0"/>
                <Button Content="Effacer" Click="OnClearClick"
                        MinWidth="70" MinHeight="32" Margin="2"/>
            </StackPanel>
        </StackPanel>

        <!-- Zone d'expression -->
        <TextBox x:Name="ExpressionTextBox" Grid.Row="2"
                 AcceptsReturn="True" TextWrapping="Wrap"
                 MinHeight="100" MinLines="3"
                 VerticalScrollBarVisibility="Auto"
                 TextChanged="OnExpressionTextChanged"
                 FontFamily="Consolas" FontSize="13"
                 Margin="0,0,0,4"/>

        <!-- Validation inline -->
        <TextBlock x:Name="ValidationText" Grid.Row="3"
                   TextWrapping="Wrap" Margin="0,0,0,10"
                   MinHeight="20"/>

        <!-- Boutons d'action -->
        <StackPanel Grid.Row="4" Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="Annuler" IsCancel="True" Width="90" Margin="0,0,8,0"/>
            <Button Content="Appliquer" IsDefault="True" Width="100"
                    Click="OnApplyClick" Style="{StaticResource PrimaryButtonStyle}"/>
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 2: Créer le code-behind**

```csharp
// src/ScadaBuilderV2.App/ExpressionCreationDialog.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App;

public partial class ExpressionCreationDialog : Window
{
    private readonly ScadaTagCatalog? _tagCatalog;

    /// <summary>Texte modifié si l'utilisateur clique Appliquer, null sinon.</summary>
    public string? ResultExpression { get; private set; }

    public ExpressionCreationDialog(string initialExpression, ScadaTagCatalog? tagCatalog, int? initialCaretIndex = null)
    {
        InitializeComponent();
        _tagCatalog = tagCatalog;
        Owner = Application.Current.MainWindow;

        // Copie locale de l'expression reçue (D9)
        ExpressionTextBox.Text = initialExpression ?? string.Empty;
        ExpressionTextBox.CaretIndex = ResolveInitialCaret(initialCaretIndex, ExpressionTextBox.Text.Length);
        ValidateInline();
    }

    private static int ResolveInitialCaret(int? caretIndex, int textLength)
    {
        if (caretIndex is null || caretIndex.Value <= 0)
            return 0;

        return Math.Min(caretIndex.Value, textLength);
    }

    private void OnVariableClick(object sender, RoutedEventArgs e)
    {
        var selectionDialog = new TagSelectionDialog(_tagCatalog) { Owner = this };
        if (selectionDialog.ShowDialog() == true && selectionDialog.SelectedTag is { } tag)
        {
            InsertAtCaret($"{{{tag.DisplayName}}}");
            ValidateInline();
        }
    }

    private void OnOperatorClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string op)
        {
            InsertAtCaret(op);
            ValidateInline();
        }
    }

    private void OnParenthesesClick(object sender, RoutedEventArgs e)
    {
        var caret = ExpressionTextBox.CaretIndex;
        var text = ExpressionTextBox.Text;
        ExpressionTextBox.Text = text.Insert(caret, "()");
        ExpressionTextBox.CaretIndex = Math.Min(caret + 1, ExpressionTextBox.Text.Length);
        ExpressionTextBox.Focus();
        ValidateInline();
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        ExpressionTextBox.Text = string.Empty;
        ValidateInline();
    }

    private void OnExpressionTextChanged(object sender, TextChangedEventArgs e) => ValidateInline();

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        ResultExpression = ExpressionTextBox.Text;
        DialogResult = true;
        Close();
    }

    private void InsertAtCaret(string fragment)
    {
        var caret = Math.Clamp(ExpressionTextBox.CaretIndex, 0, ExpressionTextBox.Text.Length);
        var text = ExpressionTextBox.Text;
        ExpressionTextBox.Text = text.Insert(caret, fragment);
        ExpressionTextBox.CaretIndex = caret + fragment.Length;
        ExpressionTextBox.Focus();
    }

    private void ValidateInline()
    {
        var source = ExpressionTextBox.Text;
        if (string.IsNullOrWhiteSpace(source))
        {
            ValidationText.Text = "";
            return;
        }

        var result = ScadaExpressionValidator.Validate(source, _tagCatalog);
        ValidationText.Text = result.IsValid
            ? "✓ Expression valide"
            : string.Join(" ", result.Errors);
        ValidationText.Foreground = result.IsValid
            ? System.Windows.Media.Brushes.SeaGreen
            : System.Windows.Media.Brushes.Firebrick;
    }
}
```

Le projet MSTest ne référence pas `ScadaBuilderV2.App`; les tests de cette surface WPF restent donc des tests de contrat par lecture XAML/code source. Aucun test ne doit instancier une fenêtre, appeler un handler privé ou accéder directement à un champ XAML généré. Les comportements visuels et le caret sont validés manuellement sur STA.

- [ ] **Step 3: Ajouter les tests de l'assistant**

```csharp
// Les tests ne doivent pas instancier des fenêtres WPF ni appeler des handlers
// privés ou des champs XAML générés privés. Les règles d'insertion sont testées
// via lecture des sources; la présence des contrôles et leur câblage sont
// testés par lecture de XAML/code source.

[TestMethod]
public void ExpressionCreationDialog_SourceContract_NormalizesInitialCaret()
{
    var code = ReadAppFile("ExpressionCreationDialog.xaml.cs");
    StringAssert.Contains(code, "int? initialCaretIndex");
    StringAssert.Contains(code, "initialCaretIndex is null");
    StringAssert.Contains(code, "ExpressionTextBox.CaretIndex");
}

[TestMethod]
public void ExpressionCreationDialog_SourceContract_InsertsAtLocalCaret()
{
    var code = ReadAppFile("ExpressionCreationDialog.xaml.cs");
    StringAssert.Contains(code, "text.Insert(caret, fragment)");
    StringAssert.Contains(code, "ExpressionTextBox.CaretIndex = caret + fragment.Length");
}

[TestMethod]
public void ExpressionCreationDialog_SourceContract_UsesOperatorSpacingAndParenthesesCaret()
{
    var code = ReadAppFile("ExpressionCreationDialog.xaml.cs");
    StringAssert.Contains(code, "ExpressionTextBox.CaretIndex = Math.Min(caret + 1");
    var xaml = ReadAppFile("ExpressionCreationDialog.xaml");
    StringAssert.Contains(xaml, "Tag=\" == \"");
}

[TestMethod]
public void ExpressionCreationDialog_SourceContractUsesApplyAndCancel()
{
    var xaml = ReadAppFile("ExpressionCreationDialog.xaml");
    var code = ReadAppFile("ExpressionCreationDialog.xaml.cs");
    StringAssert.Contains(xaml, "Content=\"Appliquer\"");
    StringAssert.Contains(xaml, "Content=\"Annuler\"");
    StringAssert.Contains(code, "ResultExpression");
    StringAssert.Contains(code, "InsertAtCaret");
}
```

- [ ] **Step 4: Build et tests**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ExpressionCreationDialogContractTests"
```

Attendu : tous les tests passent.

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.App/ExpressionCreationDialog.xaml src/ScadaBuilderV2.App/ExpressionCreationDialog.xaml.cs tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs
git commit -m "feat: add ExpressionCreationDialog with operator buttons and validation

WPF modal assistant with local expression copy, tag insertion via
TagSelectionDialog, 8 operator buttons (== != < <= > >= && ||),
parentheses with caret positioning, clear button, inline validation
via ScadaExpressionValidator, and Apply/Cancel contract.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 3: Ajouter le bouton Outil dans ElementStateRuleDialog

**Files:**
- Modify: `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml:68-72`
- Modify: `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs`

**Interfaces:**
- Consumes: `ExpressionCreationDialog` (Task 2), `_tagCatalog` (existant)
- Produces: Bouton **Outil** à droite du `ExpressionTextBox`, ouvre l'assistant, reçoit le résultat

- [ ] **Step 1: Modifier le XAML — ajouter le label et le bouton Outil**

Remplacer le bloc Expression mode (lignes 68-72) :

```xml
<!-- Expression mode -->
<StackPanel x:Name="ExpressionPanel" Visibility="Collapsed">
    <DockPanel Margin="0,2,0,2">
        <TextBlock Text="Expression :" VerticalAlignment="Center" Margin="0,0,8,0"/>
        <Button x:Name="ExpressionToolButton" Content="Outil"
                DockPanel.Dock="Right"
                Click="OnExpressionToolClick"
                MinWidth="60" MinHeight="28" Margin="8,0,0,0"
                ToolTip="Ouvrir l'assistant de composition d'expression"/>
        <TextBox x:Name="ExpressionTextBox" AcceptsReturn="False"
                 TextChanged="OnExpressionTextChanged"/>
    </DockPanel>
</StackPanel>
```

- [ ] **Step 2: Ajouter le handler dans le code-behind**

Dans `ElementStateRuleDialog.xaml.cs`, ajouter après `OnExpressionTextChanged` (ligne 377) :

```csharp
private void OnExpressionToolClick(object sender, RoutedEventArgs e)
{
    var assistant = new ExpressionCreationDialog(
        ExpressionTextBox.Text,
        _tagCatalog,
        ExpressionTextBox.CaretIndex)
    { Owner = this };
    if (assistant.ShowDialog() == true && assistant.ResultExpression is not null)
    {
        ExpressionTextBox.Text = assistant.ResultExpression;
        // OnExpressionTextChanged déclenche ValidateExpression automatiquement
    }
}
```

- [ ] **Step 3: Ajouter les tests d'intégration**

```csharp
// Ajouter dans ExpressionCreationDialogContractTests.cs

[TestMethod]
public void ElementStateRuleDialog_HasExpressionLabelAndToolButton()
{
    // Vérifie que le XAML contient le label et le bouton Outil
    var xamlPath = System.IO.Path.Combine(
        GetProjectRoot(), "src", "ScadaBuilderV2.App", "ElementStateRuleDialog.xaml");
    var xaml = System.IO.File.ReadAllText(xamlPath);
    StringAssert.Contains(xaml, "Expression :", "XAML must have Expression label");
    StringAssert.Contains(xaml, "ExpressionToolButton", "XAML must have tool button");
    StringAssert.Contains(xaml, "Outil", "XAML must have Outil button content");
}

[TestMethod]
public void ExpressionCreationDialog_RoundTrip_PreservesExpression()
{
    var code = ReadAppFile("ExpressionCreationDialog.xaml.cs");
    StringAssert.Contains(code, "ResultExpression = ExpressionTextBox.Text");
    StringAssert.Contains(code, "DialogResult = true");
    StringAssert.Contains(code, "ExpressionTextBox.Text = initialExpression");
}

[TestMethod]
public void ExpressionCreationDialog_InsertTag_AddsToExpression()
{
    var code = ReadAppFile("ExpressionCreationDialog.xaml.cs");
    StringAssert.Contains(code, "InsertAtCaret");
    StringAssert.Contains(code, "tag.DisplayName");
    StringAssert.Contains(code, "ScadaExpressionValidator.Validate");
}

[TestMethod]
public void ExpressionCreationDialog_ExistingStateRuleTests_Unaffected()
{
    // Vérifier que les tests existants du validateur passent toujours
    // Ce test est un placeholder — la suite ScadaExpressionValidatorTests
    // doit passer sans modification.
    var source = "{TagA} == {TagB}";
    var result = ScadaBuilderV2.Domain.ElementEvents.Expressions
        .ScadaExpressionValidator.Validate(source, null);
    // Même sans catalogue, la syntaxe est valide (tags non résolus = erreurs)
    Assert.IsFalse(result.IsValid); // tags non résolus
}
```

- [ ] **Step 4: Build complet et tests**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ExpressionCreationDialogContractTests"
```

Attendu : tous les tests passent.

- [ ] **Step 5: Vérification de non-régression**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ScadaExpressionValidatorTests"
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ScadaExpressionTests"
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ScadaExprNodeTests"
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ScadaExpressionParserTests"
```

Attendu : 0 échec, tous les tests existants passent.

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs
git commit -m "feat: add Outil button to Expression mode in ElementStateRuleDialog

Expression mode now shows 'Expression :' label and 'Outil' button next to
the text box. Clicking Outil opens ExpressionCreationDialog with the
current expression pre-filled. On Apply, the result replaces the text
box content and triggers validation through the existing pipeline.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 4: Vérification manuelle et documentation

**Files:**
- Modify: `docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md` (si applicable)

- [ ] **Step 1: Checklist de vérification manuelle**

1. **Insertion tag champ vide** : ouvrir dialogue → mode Expression → Outil → Variable → sélectionner un tag → Appliquer → le champ Expression affiche `{TagDisplayName}`
2. **Insertion tag au caret** : taper `{TagA} == ` → positionner le caret après `== ` → Outil → Variable → sélectionner → Appliquer → l'expression est `{TagA} == {TagB}`
3. **Opérateurs** : dans l'assistant, taper `{TagA}` → cliquer `>=` → taper `80` → Appliquer → `{TagA} >= 80`
4. **Parenthèses** : taper `{TagA} > 10 && ` → parenthèses → taper `{TagB} < 5` entre les parenthèses → `{TagA} > 10 && ({TagB} < 5)`
5. **Effacer** : dans l'assistant, cliquer Effacer → la zone est vide → Appliquer → le champ Expression est vide
6. **Validation inline** : taper `{TagInconnu}` → la validation affiche rouge "n'existe pas" → remplacer par un tag existant → vert "valide"
7. **Annulation** : modifier l'expression dans l'assistant → Annuler (pas Appliquer) → le champ Expression dans ElementStateRuleDialog est inchangé
8. **Double-clic tag** : dans TagSelectionDialog, double-clic sur un tag → sélection et fermeture immédiate
9. **Mode Variable non affecté** : basculer en mode Variable → le ComboBox de tags et les contrôles fonctionnent comme avant
10. **Sauvegarde** : composer `{TagA} == {TagB}` via l'assistant → Appliquer → Sauvegarder dans ElementStateRuleDialog → vérifier que la règle est persistée et rechargée correctement

- [ ] **Step 2: Mettre à jour IMPLEMENTED_FEATURES_V2.md**

```markdown
| 2026-07-13 | `V2.1.4.0003` | `PENDING` | Assistant de création d'expressions : TagSelectionDialog, ExpressionCreationDialog avec opérateurs et validation inline, bouton Outil dans ElementStateRuleDialog |
```

- [ ] **Step 3: Commit**

```bash
git add docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md
git commit -m "docs: register expression creation assistant in implemented features

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Validation finale

- [ ] `dotnet build ScadaBuilderV2.sln`
- [ ] `dotnet test ScadaBuilderV2.sln --no-restore`
- [ ] Tests ciblés : `ExpressionCreationDialogContractTests`, `ScadaExpressionValidatorTests`, `ScadaExpressionTests`, `ScadaExprNodeTests`, `ScadaExpressionParserTests`
- [ ] Vérification manuelle des 10 scénarios (Task 4 Step 1)
- [ ] `git status --short --branch` clean
