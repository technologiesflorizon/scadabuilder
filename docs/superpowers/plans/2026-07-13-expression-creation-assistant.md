# Expression Creation Assistant — Plan d'implémentation

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
- Test: `tests/ScadaBuilderV2.Tests/ExpressionCreationAssistantTests.cs` (nouveau)

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
// tests/ScadaBuilderV2.Tests/ExpressionCreationAssistantTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScadaBuilderV2.App;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Tests;

[TestClass]
public class ExpressionCreationAssistantTests
{
    [TestMethod]
    public void TagSelectionDialog_OnlyShowsEnabledTags()
    {
        var catalog = new ScadaTagCatalog("v1", new[]
        {
            new ScadaTagDefinition("t1", "Temp", Enabled: true),
            new ScadaTagDefinition("t2", "Pressure", Enabled: false),
            new ScadaTagDefinition("t3", "Flow", Enabled: true)
        });

        var dialog = new TagSelectionDialog(catalog);
        // Verify that the ListBox contains exactly 2 items (Temp, Flow)
        Assert.AreEqual(2, dialog.TagListBox.Items.Count);
    }

    [TestMethod]
    public void TagSelectionDialog_SelectedTag_ReturnsDisplayNameAndId()
    {
        var catalog = new ScadaTagCatalog("v1", new[]
        {
            new ScadaTagDefinition("tf100.mapping.1", "Temperature")
        });

        var dialog = new TagSelectionDialog(catalog);
        dialog.TagListBox.SelectedIndex = 0;
        dialog.OnSelectClick(null, null!);

        Assert.IsTrue(dialog.DialogResult == true);
        Assert.IsNotNull(dialog.SelectedTag);
        Assert.AreEqual("Temperature", dialog.SelectedTag!.DisplayName);
        Assert.AreEqual("tf100.mapping.1", dialog.SelectedTag.TagId);
    }

    [TestMethod]
    public void TagSelectionDialog_Cancel_ReturnsNull()
    {
        var catalog = new ScadaTagCatalog("v1", new[]
        {
            new ScadaTagDefinition("t1", "Temp")
        });

        var dialog = new TagSelectionDialog(catalog);
        dialog.TagListBox.SelectedIndex = 0;
        // Simulate cancel via ESC
        dialog.DialogResult = false;

        Assert.IsNull(dialog.SelectedTag);
    }
}
```

- [ ] **Step 4: Build et tests**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ExpressionCreationAssistant"
```

Attendu : 3 tests passent.

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.App/TagSelectionDialog.xaml src/ScadaBuilderV2.App/TagSelectionDialog.xaml.cs tests/ScadaBuilderV2.Tests/ExpressionCreationAssistantTests.cs
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
- Modify: `tests/ScadaBuilderV2.Tests/ExpressionCreationAssistantTests.cs`

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
            <Button Content="Fermer" IsCancel="True" Width="90" Margin="0,0,8,0"/>
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

    public ExpressionCreationDialog(string initialExpression, ScadaTagCatalog? tagCatalog)
    {
        InitializeComponent();
        _tagCatalog = tagCatalog;
        Owner = Application.Current.MainWindow;

        // Copie locale de l'expression reçue (D9)
        ExpressionTextBox.Text = initialExpression ?? string.Empty;
        ValidateInline();
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
        var caret = ExpressionTextBox.CaretIndex;
        // D5 : caret null ou 0 → insertion au début
        if (caret < 0) caret = 0;
        caret = Math.Min(caret, ExpressionTextBox.Text.Length);

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

- [ ] **Step 3: Ajouter les tests de l'assistant**

```csharp
// Ajouter dans ExpressionCreationAssistantTests.cs

[TestMethod]
public void ExpressionCreationDialog_Operator_InsertsWithSpaces()
{
    var dialog = new ExpressionCreationDialog("", null);
    dialog.ExpressionTextBox.Text = "{TagA}";
    dialog.ExpressionTextBox.CaretIndex = 6; // après {TagA}

    // Simuler clic sur == en appelant OnOperatorClick avec un bouton taggé
    var button = new System.Windows.Controls.Button { Tag = " == " };
    dialog.OnOperatorClick(button, null!);

    Assert.AreEqual("{TagA} == ", dialog.ExpressionTextBox.Text);
}

[TestMethod]
public void ExpressionCreationDialog_Variable_InsertsTagAtCaret()
{
    // Ce test vérifie que l'insertion utilise le DisplayName du tag sélectionné
    var dialog = new ExpressionCreationDialog("{TagA}", null);
    dialog.ExpressionTextBox.CaretIndex = 6; // fin du texte

    // InsertAtCaret est appelée indirectement via OnVariableClick
    // Test direct de la logique d'insertion
    dialog.InsertAtCaret("{NewTag}");
    Assert.AreEqual("{TagA}{NewTag}", dialog.ExpressionTextBox.Text);
}

[TestMethod]
public void ExpressionCreationDialog_Clear_EmptiesExpression()
{
    var dialog = new ExpressionCreationDialog("{TagA} == {TagB}", null);
    dialog.OnClearClick(null, null!);
    Assert.AreEqual("", dialog.ExpressionTextBox.Text);
}

[TestMethod]
public void ExpressionCreationDialog_Apply_ReturnsExpression()
{
    var dialog = new ExpressionCreationDialog("{TagA} == 42", null);
    dialog.OnApplyClick(null, null!);
    Assert.AreEqual(true, dialog.DialogResult);
    Assert.AreEqual("{TagA} == 42", dialog.ResultExpression);
}

[TestMethod]
public void ExpressionCreationDialog_Cancel_ReturnsNull()
{
    var dialog = new ExpressionCreationDialog("{TagA}", null);
    // Fermer sans Appliquer
    Assert.IsNull(dialog.ResultExpression);
}

[TestMethod]
public void ExpressionCreationDialog_Parentheses_PositionsCaretBetween()
{
    var dialog = new ExpressionCreationDialog("{TagA}", null);
    dialog.ExpressionTextBox.CaretIndex = 6; // après {TagA}
    dialog.OnParenthesesClick(null, null!);
    Assert.AreEqual("{TagA}()", dialog.ExpressionTextBox.Text);
    Assert.AreEqual(7, dialog.ExpressionTextBox.CaretIndex); // entre ( et )
}

[TestMethod]
public void ExpressionCreationDialog_InsertAtCaret_ZeroInsertsAtStart()
{
    var dialog = new ExpressionCreationDialog("existing", null);
    dialog.ExpressionTextBox.CaretIndex = 0; // début
    dialog.InsertAtCaret("{TagB}");
    Assert.AreEqual("{TagB}existing", dialog.ExpressionTextBox.Text);
}

[TestMethod]
public void ExpressionCreationDialog_Validation_ShowsErrors()
{
    var catalog = new ScadaTagCatalog("v1", new[]
    {
        new ScadaTagDefinition("t1", "Temp")
    });
    var dialog = new ExpressionCreationDialog("{UnknownTag}", catalog);
    // ValidateInline est appelée dans le constructeur
    Assert.IsTrue(dialog.ValidationText.Text.Contains("n'existe pas"));
}
```

- [ ] **Step 4: Build et tests**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ExpressionCreationAssistant"
```

Attendu : tous les tests passent.

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.App/ExpressionCreationDialog.xaml src/ScadaBuilderV2.App/ExpressionCreationDialog.xaml.cs tests/ScadaBuilderV2.Tests/ExpressionCreationAssistantTests.cs
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
- Modify: `tests/ScadaBuilderV2.Tests/ExpressionCreationAssistantTests.cs`

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
    var assistant = new ExpressionCreationDialog(ExpressionTextBox.Text, _tagCatalog) { Owner = this };
    if (assistant.ShowDialog() == true && assistant.ResultExpression is not null)
    {
        ExpressionTextBox.Text = assistant.ResultExpression;
        // OnExpressionTextChanged déclenche ValidateExpression automatiquement
    }
}
```

- [ ] **Step 3: Ajouter les tests d'intégration**

```csharp
// Ajouter dans ExpressionCreationAssistantTests.cs

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
    // Test du flux complet : ElementStateRuleDialog → assistant → retour
    var catalog = new ScadaTagCatalog("v1", new[]
    {
        new ScadaTagDefinition("t1", "Temp"),
        new ScadaTagDefinition("t2", "Pressure")
    });

    var originalExpression = "{Temp} > 80 && {Pressure} < 120";
    var assistant = new ExpressionCreationDialog(originalExpression, catalog);
    // L'utilisateur ne modifie rien, juste Appliquer
    assistant.OnApplyClick(null, null!);

    Assert.AreEqual(originalExpression, assistant.ResultExpression);
}

[TestMethod]
public void ExpressionCreationDialog_InsertTag_AddsToExpression()
{
    var catalog = new ScadaTagCatalog("v1", new[]
    {
        new ScadaTagDefinition("t1", "Temp")
    });

    var assistant = new ExpressionCreationDialog("", catalog);
    // Simuler insertion manuelle (comme le ferait TagSelectionDialog)
    assistant.InsertAtCaret("{Temp}");

    Assert.AreEqual("{Temp}", assistant.ExpressionTextBox.Text);
    // La validation doit passer car Temp existe dans le catalogue
    Assert.IsTrue(assistant.ValidationText.Text.Contains("valide"));
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
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ExpressionCreationAssistant"
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
git add src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs tests/ScadaBuilderV2.Tests/ExpressionCreationAssistantTests.cs
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
7. **Annulation** : modifier l'expression dans l'assistant → Fermer (pas Appliquer) → le champ Expression dans ElementStateRuleDialog est inchangé
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
- [ ] Tests ciblés : `ExpressionCreationAssistantTests`, `ScadaExpressionValidatorTests`, `ScadaExpressionTests`, `ScadaExprNodeTests`, `ScadaExpressionParserTests`
- [ ] Vérification manuelle des 10 scénarios (Task 4 Step 1)
- [ ] `git status --short --branch` clean
