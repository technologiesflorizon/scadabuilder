# Condition Variable/Expression Tag Picker — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the free-text Condition field in `ElementStateRuleDialog` with a Variable (tag dropdown + operator) / Expression (free text) radio button choice.

**Architecture:** UI-only change in the WPF `ElementStateRuleDialog`. A `TagItem` record wraps tag Id + display label for the ComboBox. An `OperatorItem` record maps display text (`=`, `<>`) to parser tokens (`==`, `!=`). Boolean tags get "Si vrai"/"Si faux" radio buttons; numeric tags get an operator ComboBox + value TextBox. Expression mode keeps the existing free-text path. Both paths feed `ScadaExpressionValidator.Validate()` before save. No domain, application, or infrastructure changes.

**Tech Stack:** WPF / .NET 8, C# 12, ScadaBuilderV2.App

## Global Constraints

- Tous les labels UI en français (Variable, Expression, Si vrai, Si faux, Valeur, Condition valide.)
- Opérateurs affichés : `<>`, `>=`, `>`, `=`, `<`, `<=`
- Opérateurs dans l'expression générée : `!=`, `>=`, `>`, `==`, `<`, `<=`
- Booléen détecté via `bool`, `boolean`, `digital` (case-insensitive) — tout le reste = numérique
- `_tagCatalog` est déjà injecté dans le constructeur, déjà utilisé par `ValidateExpression()`
- Suivre le même pattern de ComboBox tag que `ElementEventDialog` (`ItemsSource` + `DisplayMemberPath`)
- Aucun changement dans Domain, Application, Infrastructure, ou les contrats
- `ScadaExpression`, `ScadaExpressionValidator`, `ScadaStateRule`, `ScadaEffectBlock` : inchangés

---

### Task 1: Add helper records and tag loading in code-behind

**Files:**
- Modify: `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs`

**Interfaces:**
- Produces: `TagItem(string TagId, string DisplayName)` record, `OperatorItem(string Display, string Expression)` record, `_operatorItems` field, `PopulateTagComboBox()` method, `BuildExpressionFromVariable()` method

- [ ] **Step 1: Add TagItem and OperatorItem records**

Add these two private sealed records inside the `ElementStateRuleDialog` class (at the bottom, before the closing brace):

```csharp
private sealed record TagItem(string TagId, string DisplayName);
private sealed record OperatorItem(string Display, string Expression)
{
    public override string ToString() => Display;
}
```

- [ ] **Step 2: Add operator items field and tag population method**

Add a static field for the operator items and a method to populate the tag ComboBox. Insert these after the existing field declarations (`_tagCatalog`, `_ruleId`):

```csharp
private static readonly OperatorItem[] _operatorItems =
[
    new OperatorItem("<>", "!="),
    new OperatorItem(">=", ">="),
    new OperatorItem(">", ">"),
    new OperatorItem("=", "=="),
    new OperatorItem("<", "<"),
    new OperatorItem("<=", "<=")
];
```

Add the tag populating method after the constructor:

```csharp
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
```

- [ ] **Step 3: Add helper methods for datatype detection and expression building**

```csharp
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
```

- [ ] **Step 4: Commit**

```bash
git add src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs
git commit -m "feat: add TagItem, OperatorItem, tag loading, and expression builder for variable mode"
```

---

### Task 2: Update XAML with Variable/Expression radio buttons and new controls

**Files:**
- Modify: `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml`

**Interfaces:**
- Consumes: `TagComboBox`, `OperatorComboBox`, `BoolTrueRadio`, `BoolFalseRadio`, `ValueTextBox` control names (set in Task 1)
- Produces: `ExpressionModeRadio`, `VariableModeRadio` x:Name, `VariablePanel`, `BoolPanel`, `NumericPanel`, `ExpressionPanel` x:Name

- [ ] **Step 1: Replace the Condition section (lines 29-32)**

Replace the current lines 29-32:
```xml
            <TextBlock Text="Condition" Foreground="{StaticResource MutedBrush}"/>
            <TextBox x:Name="ExpressionTextBox" Margin="0,2,0,2" AcceptsReturn="False"
                     TextChanged="OnExpressionTextChanged"/>
            <TextBlock x:Name="ExpressionValidationText" TextWrapping="Wrap" Margin="0,0,0,8"/>
```

With the following new markup:

```xml
            <TextBlock Text="Condition" Foreground="{StaticResource MutedBrush}"/>

            <StackPanel Orientation="Horizontal" Margin="0,2,0,4">
                <RadioButton x:Name="VariableModeRadio" Content="Variable"
                             GroupName="ConditionMode" Margin="0,0,12,0"
                             Checked="OnConditionModeChanged" Unchecked="OnConditionModeChanged"/>
                <RadioButton x:Name="ExpressionModeRadio" Content="Expression"
                             GroupName="ConditionMode"
                             Checked="OnConditionModeChanged" Unchecked="OnConditionModeChanged"/>
            </StackPanel>

            <!-- Variable mode -->
            <StackPanel x:Name="VariablePanel" Visibility="Collapsed">
                <TextBlock Text="Tag" Foreground="{StaticResource MutedBrush}"/>
                <ComboBox x:Name="TagComboBox" Margin="0,2,0,4"
                          DisplayMemberPath="DisplayName"
                          SelectionChanged="OnTagSelectionChanged"/>

                <!-- Boolean tag: Si vrai / Si faux -->
                <StackPanel x:Name="BoolPanel" Visibility="Collapsed" Margin="0,2,0,4">
                    <TextBlock Text="Valeur" Foreground="{StaticResource MutedBrush}"/>
                    <StackPanel Orientation="Horizontal">
                        <RadioButton x:Name="BoolTrueRadio" Content="Si vrai"
                                     GroupName="BoolValue" Margin="0,0,12,0"/>
                        <RadioButton x:Name="BoolFalseRadio" Content="Si faux"
                                     GroupName="BoolValue"/>
                    </StackPanel>
                </StackPanel>

                <!-- Numeric tag: operator + value -->
                <StackPanel x:Name="NumericPanel" Visibility="Collapsed" Margin="0,2,0,4">
                    <TextBlock Text="Operateur" Foreground="{StaticResource MutedBrush}"/>
                    <ComboBox x:Name="OperatorComboBox" Margin="0,2,0,4"/>
                    <TextBlock Text="Valeur" Foreground="{StaticResource MutedBrush}"/>
                    <TextBox x:Name="ValueTextBox" Margin="0,2,0,4"
                             TextChanged="OnVariableValueChanged"/>
                </StackPanel>
            </StackPanel>

            <!-- Expression mode -->
            <StackPanel x:Name="ExpressionPanel" Visibility="Collapsed">
                <TextBox x:Name="ExpressionTextBox" Margin="0,2,0,2" AcceptsReturn="False"
                         TextChanged="OnExpressionTextChanged"/>
            </StackPanel>

            <TextBlock x:Name="ExpressionValidationText" TextWrapping="Wrap" Margin="0,0,0,8"/>
```

- [ ] **Step 2: Commit**

```bash
git add src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml
git commit -m "feat: add Variable/Expression radio buttons and tag picker UI to state rule dialog"
```

---

### Task 3: Wire up mode switching, tag selection, and datatype detection in code-behind

**Files:**
- Modify: `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs`

**Interfaces:**
- Consumes: `TagItem`, `OperatorItem`, `PopulateTagComboBox()`, `BuildExpressionFromVariable()`, `IsBooleanDatatype()` from Task 1; new XAML controls from Task 2

- [ ] **Step 1: Update constructor to populate tag ComboBox and operator ComboBox, set default mode**

Modify the constructor. After `AnimationComboBox.ItemsSource = Enum.GetValues<ScadaAnimation>();` (line 20), add:

```csharp
PopulateTagComboBox();
OperatorComboBox.ItemsSource = _operatorItems;
OperatorComboBox.SelectedIndex = 3; // "=" par defaut
BoolTrueRadio.IsChecked = true;
VariableModeRadio.IsChecked = true; // Mode Variable par defaut
```

- [ ] **Step 2: Add mode switching handler**

```csharp
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
```

- [ ] **Step 3: Add tag selection changed handler**

```csharp
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
```

- [ ] **Step 4: Add variable-mode validation and value-changed handler**

```csharp
private void OnVariableValueChanged(object sender, TextChangedEventArgs e) =>
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
```

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs
git commit -m "feat: wire up Variable/Expression mode switching, tag selection, and datatype detection"
```

---

### Task 4: Restore existing rule state on load (mode detection + pre-selection)

**Files:**
- Modify: `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs`

**Interfaces:**
- Consumes: `BuildExpressionFromVariable()`, `IsBooleanDatatype()`, all XAML controls from Tasks 1-3

- [ ] **Step 1: Add the restore method**

Add this method after the constructor:

```csharp
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
        SelectTagById(boolMatch.Groups[1].Value);
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
        SelectTagById(numericMatch.Groups[1].Value);
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
        SelectTagById(bareMatch.Groups[1].Value);
        return;
    }

    // Fallback: Expression mode
    ExpressionModeRadio.IsChecked = true;
    ExpressionTextBox.Text = source;
}

private void SelectTagById(string tagId)
{
    for (int i = 0; i < TagComboBox.Items.Count; i++)
    {
        if (TagComboBox.Items[i] is TagItem item &&
            string.Equals(item.TagId, tagId, StringComparison.Ordinal))
        {
            TagComboBox.SelectedIndex = i;
            return;
        }
    }
    // Tag non trouve dans le catalogue : fallback Expression
    ExpressionModeRadio.IsChecked = true;
    ExpressionTextBox.Text = $"{{{tagId}}}";
}
```

- [ ] **Step 2: Update constructor to call RestoreExpression instead of directly setting ExpressionTextBox**

Replace line 25 in the constructor:
```csharp
            ExpressionTextBox.Text = existingRule.Expression.Source;
```

With:
```csharp
            RestoreExpression(existingRule.Expression.Source);
```

- [ ] **Step 3: Update OnSaveClick to use BuildExpressionFromVariable in Variable mode**

Replace the expression-building part of `OnSaveClick` (lines 124-138). The current code reads `ExpressionTextBox.Text` directly. Change to:

```csharp
    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var source = VariableModeRadio.IsChecked == true
            ? BuildExpressionFromVariable()
            : ExpressionTextBox.Text;

        var validation = ScadaExpressionValidator.Validate(source, _tagCatalog);
        if (!validation.IsValid || string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            ExpressionValidationText.Text = string.IsNullOrWhiteSpace(NameTextBox.Text)
                ? "Le nom est requis."
                : string.Join(" ", validation.Errors);
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
```

- [ ] **Step 4: Build and verify compilation**

```bash
dotnet build src/ScadaBuilderV2.App/ScadaBuilderV2.App.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Run existing tests to check for regressions**

```bash
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~State" --no-restore
```

Expected: All existing state-related tests pass.

- [ ] **Step 6: Run the full test suite**

```bash
dotnet test ScadaBuilderV2.sln --no-restore
```

Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs
git commit -m "feat: restore existing expression mode on load, build expression from variable inputs on save"
```

---

### Task 5: Manual verification checklist

- [ ] **Step 1: Launch the app**

```bash
dotnet run --project src/ScadaBuilderV2.App
```

- [ ] **Step 2: Verify new rule creation (Variable mode)**

1. Open a project with an imported tag catalog
2. Double-click an Element+ to open Properties
3. Go to "Etat" tab
4. Click "+ Ajouter"
5. Verify: "Variable" radio is selected by default
6. Verify: Tag ComboBox shows all enabled tags sorted by name
7. Select a boolean tag → verify "Si vrai"/"Si faux" radios appear
8. Select a numeric tag → verify operator ComboBox + value TextBox appear
9. Enter a value, verify validation message shows "Condition valide."
10. Click "Enregistrer" → dialog closes, rule appears in list

- [ ] **Step 3: Verify new rule creation (Expression mode)**

1. Click "+ Ajouter" again
2. Select "Expression" radio
3. Verify: Expression TextBox appears (no tag dropdown)
4. Type `{SomeTag} > 50`
5. Verify validation runs in real-time
6. Click "Enregistrer"

- [ ] **Step 4: Verify editing an existing rule**

1. Select a rule in the list, click "Editer"
2. Verify it reopens in the correct mode (Variable with tag pre-selected, or Expression with text restored)
3. Modify and save — verify the rule updates in the list

- [ ] **Step 5: Verify a tag not in catalog falls back to Expression mode**

(If possible: edit a rule whose tag was removed from the catalog, or manually test the restore logic)

- [ ] **Step 6: Commit if any fixes were made**

```bash
git add -A
git commit -m "fix: manual verification adjustments for Variable/Expression condition picker"
```
