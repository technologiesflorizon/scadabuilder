# Studio Element+ Library Tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "Librairie" tab to Studio Element+'s side panel that lets a user browse the active library's saved components and Renommer/Copier/Supprimer them (Éditer visible but disabled, reserved for a future plan).

**Architecture:** Reuse the existing `LibraryRegistryStore`/`LibraryEntry` (from the multi-library configuration feature) to populate a library selector, and the existing `ElementPlusLibraryReader`/`ElementPlusLibraryItem` (already used by SCADA Builder V2's own "Librairie" panel) to list `.sep` components — no new reading infrastructure. A new pure helper computes unique `"_copieN"` names for the Copier action. All file mutations (rename/copy/delete) go through the existing `ElementStudioComponentPackageStore` read/write methods already used by the save and "Ajouter à la librairie" flows.

**Tech Stack:** C# / .NET 8, WPF (XAML), MSTest.

## Global Constraints

- **Éditer** is visible but disabled in the context menu, with tooltip "Édition disponible dans une prochaine version" — it is NOT implemented in this plan.
- **Renommer** updates only `Component.Name` (not `ComponentId`) and renames the underlying file to match the new safe filename; the old file is deleted only if the filename actually changed.
- **Copier** generates a name via `"{nom}_copie{N}"` (first available `N` starting at 1, checked against the names currently loaded in the list) and a new `ComponentId` derived from that name — never reuses the source `ComponentId`.
- **Supprimer** requires a Yes/No confirmation dialog before deleting the `.sep` file — this is real, irreversible data loss (unlike removing a library *entry* from configuration, which was already implemented without confirmation).
- File operations (read/write/delete) are wrapped in try/catch; failures show a `MessageBox` and never crash the app.
- No new library-reading logic — reuse `ElementPlusLibraryReader.ReadAsync(string libraryRoot)` exactly as SCADA Builder V2's own "Librairie" panel and Studio's own "Ajouter à la librairie" arrow menu already do.

---

## Before You Start

This plan continues directly on branch `master` in `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2` (per the user's established preference to work without a feature branch). There may still be a pre-existing unrelated uncommitted change to `projects/AMR_REF_SCADA_V2/scenes/win00008.scene.json` in the working tree — do not touch, stage, or commit that file in any task below.

---

### Task 1: `ElementStudioComponentCopyNaming` (pure helper)

**Files:**
- Create: `src/ScadaBuilderV2.Application/ElementStudio/ElementStudioComponentCopyNaming.cs`
- Test: `tests/ScadaBuilderV2.Tests/ElementStudioComponentCopyNamingTests.cs`

**Interfaces:**
- Consumes: nothing from other tasks (foundational).
- Produces: `namespace ScadaBuilderV2.Application.ElementStudio;` containing `public static class ElementStudioComponentCopyNaming` with `public static string GenerateCopyName(string baseName, IEnumerable<string> existingNames)` — Task 3's "Copier" handler calls this exact signature.

- [ ] **Step 1: Write the failing tests**

Create `tests/ScadaBuilderV2.Tests/ElementStudioComponentCopyNamingTests.cs`:

```csharp
using ScadaBuilderV2.Application.ElementStudio;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ElementStudioComponentCopyNamingTests
{
    [TestMethod]
    public void GenerateCopyNameReturnsFirstSuffixWhenNoCollision()
    {
        var result = ElementStudioComponentCopyNaming.GenerateCopyName("Vanne", Array.Empty<string>());

        Assert.AreEqual("Vanne_copie1", result);
    }

    [TestMethod]
    public void GenerateCopyNameSkipsExistingSuffixes()
    {
        var result = ElementStudioComponentCopyNaming.GenerateCopyName(
            "Vanne",
            new[] { "Vanne_copie1", "Vanne_copie2" });

        Assert.AreEqual("Vanne_copie3", result);
    }

    [TestMethod]
    public void GenerateCopyNameIgnoresUnrelatedNames()
    {
        var result = ElementStudioComponentCopyNaming.GenerateCopyName(
            "Vanne",
            new[] { "Pompe", "Vanne_copie1" });

        Assert.AreEqual("Vanne_copie2", result);
    }

    [TestMethod]
    public void GenerateCopyNameThrowsOnEmptyBaseName()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            ElementStudioComponentCopyNaming.GenerateCopyName("", Array.Empty<string>()));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ElementStudioComponentCopyNamingTests"`
Expected: build error / FAIL — `ElementStudioComponentCopyNaming` does not exist yet.

- [ ] **Step 3: Implement `ElementStudioComponentCopyNaming`**

Create `src/ScadaBuilderV2.Application/ElementStudio/ElementStudioComponentCopyNaming.cs`:

```csharp
namespace ScadaBuilderV2.Application.ElementStudio;

/// <summary>
/// Computes a unique "<name>_copieN" candidate for duplicating a Studio Element+ component.
/// </summary>
public static class ElementStudioComponentCopyNaming
{
    public static string GenerateCopyName(string baseName, IEnumerable<string> existingNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        ArgumentNullException.ThrowIfNull(existingNames);

        var existingSet = new HashSet<string>(existingNames, StringComparer.Ordinal);
        var suffixIndex = 1;
        string candidate;
        do
        {
            candidate = $"{baseName}_copie{suffixIndex}";
            suffixIndex++;
        } while (existingSet.Contains(candidate));

        return candidate;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ElementStudioComponentCopyNamingTests"`
Expected: all 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Application/ElementStudio/ElementStudioComponentCopyNaming.cs tests/ScadaBuilderV2.Tests/ElementStudioComponentCopyNamingTests.cs
git commit -m "feat: add copy-name generator for Studio Element+ library components"
```

---

### Task 2: "Librairie" tab — browse the active library

**Files:**
- Modify: `src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml:385-401` (add a new `TabItem` after "Composant", inside the same `TabControl`)
- Modify: `src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml.cs`
- Test: `tests/ScadaBuilderV2.Tests/StudioElementPlusLibraryTabContractTests.cs` (new)

**Interfaces:**
- Consumes: `LibraryEntry` (`Name`, `Path`, `IsDefault`) and `LibraryRegistryStore` (`ReadExternalEntriesAsync()`, `ReadDefaultNameAsync()`) — both already used by this file's existing `libraryRegistryStore` field and `OnAddToLibraryArrowClick` (`MainWindow.xaml.cs:100-117`). `ElementPlusLibraryReader.ReadAsync(string libraryRoot)` returning `Task<ElementPlusLibrarySnapshot>` (`ElementPlusLibrarySnapshot.Items` is `IReadOnlyList<ElementPlusLibraryItem>`), and `ElementPlusLibraryItem`'s `Name`/`FilePath` properties — same reader class already used by SCADA Builder V2's own "Librairie" panel (`src/ScadaBuilderV2.Infrastructure/ElementStudio/ElementPlusLibraryReader.cs`). Existing `ResolveDefaultSepDirectory()` (`MainWindow.xaml.cs:859`).
- Produces: a new private method `private async Task<IReadOnlyList<LibraryEntry>> BuildLibraryEntriesAsync()` (extracted from `OnAddToLibraryArrowClick`'s existing inline logic, used by both the arrow menu and this tab), `private async Task RefreshLibrarySelectorAsync()`, `private async Task RefreshLibraryItemsAsync()`, and the field `private readonly ObservableCollection<ElementPlusLibraryItem> libraryItems = [];` — Task 3's context-menu handlers call `RefreshLibraryItemsAsync()` after each mutation and read `StudioLibraryListBox.SelectedItem`/`libraryItems`.

Because this repo's test project cannot reference the WPF `ScadaBuilderV2.ElementStudio.App` project, verification for this task's WPF pieces follows this feature's established pattern (`ConfigurationWindowContractTests.cs`): read `.xaml`/`.xaml.cs` as text and assert on their contents.

- [ ] **Step 1: Write the failing tests**

Create `tests/ScadaBuilderV2.Tests/StudioElementPlusLibraryTabContractTests.cs`:

```csharp
namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class StudioElementPlusLibraryTabContractTests
{
    [TestMethod]
    public void MainWindowXamlExposesLibraryTabWithSelectorAndList()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml");

        StringAssert.Contains(xaml, "Header=\"Librairie\"");
        StringAssert.Contains(xaml, "x:Name=\"StudioLibrarySelectorComboBox\"");
        StringAssert.Contains(xaml, "x:Name=\"StudioLibraryListBox\"");
        StringAssert.Contains(xaml, "SelectionChanged=\"OnStudioLibrarySelectorSelectionChanged\"");
    }

    [TestMethod]
    public void MainWindowCodeLoadsLibraryItemsThroughSharedReader()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml.cs");

        StringAssert.Contains(code, "private async Task<IReadOnlyList<LibraryEntry>> BuildLibraryEntriesAsync()");
        StringAssert.Contains(code, "private async Task RefreshLibrarySelectorAsync()");
        StringAssert.Contains(code, "private async Task RefreshLibraryItemsAsync()");
        StringAssert.Contains(code, "elementPlusLibraryReader.ReadAsync(");
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate project file: {Path.Combine(parts)}");
        return "";
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~StudioElementPlusLibraryTabContractTests"`
Expected: both FAIL — none of the new names exist yet.

- [ ] **Step 3: Add the "Librairie" `TabItem` in `MainWindow.xaml`**

In `src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml`, insert this new `TabItem` immediately after the closing `</TabItem>` of `<TabItem Header="Composant">` (line 400), before the `</TabControl>` (line 401):

```xml
                    <TabItem Header="Librairie">
                        <DockPanel Margin="12">
                            <StackPanel DockPanel.Dock="Top" Margin="0,0,0,8">
                                <TextBlock Text="Librairie active"
                                           FontWeight="SemiBold"
                                           Foreground="{StaticResource InkBrush}"
                                           Margin="0,0,0,4"/>
                                <ComboBox x:Name="StudioLibrarySelectorComboBox"
                                          DisplayMemberPath="Name"
                                          SelectionChanged="OnStudioLibrarySelectorSelectionChanged"/>
                            </StackPanel>
                            <ListBox x:Name="StudioLibraryListBox"
                                     DisplayMemberPath="Name"
                                     ItemContainerStyle="{StaticResource ElementListBoxItemStyle}">
                                <ListBox.ContextMenu>
                                    <ContextMenu>
                                        <MenuItem Header="Editer"
                                                  IsEnabled="False"
                                                  ToolTip="Edition disponible dans une prochaine version"/>
                                        <MenuItem Header="Renommer" Click="OnRenameLibraryComponentClick"/>
                                        <MenuItem Header="Copier" Click="OnCopyLibraryComponentClick"/>
                                        <MenuItem Header="Supprimer" Click="OnDeleteLibraryComponentClick"/>
                                    </ContextMenu>
                                </ListBox.ContextMenu>
                            </ListBox>
                        </DockPanel>
                    </TabItem>
```

- [ ] **Step 4: Add the loading logic in `MainWindow.xaml.cs`**

Add this using alongside the existing usings at the top of the file (around line 14, after `using ScadaBuilderV2.Infrastructure.Libraries;`):

```csharp
using System.Collections.ObjectModel;
```

Add a new field next to `libraryRegistryStore` (around line 22):

```csharp
    private readonly ElementPlusLibraryReader elementPlusLibraryReader = new();
    private readonly ObservableCollection<ElementPlusLibraryItem> libraryItems = [];
```

In the constructor, after `Loaded += OnLoaded;` (line 46), add:

```csharp
        StudioLibraryListBox.ItemsSource = libraryItems;
```

so the constructor reads:

```csharp
        Loaded += OnLoaded;
        StudioLibraryListBox.ItemsSource = libraryItems;
    }
```

In `OnLoaded` (`MainWindow.xaml.cs:49`), add a call at the end of the method (after the existing `UpdateToolButtonStates();` line):

```csharp
        await RefreshLibrarySelectorAsync();
```

Replace `OnAddToLibraryArrowClick`'s existing body (`MainWindow.xaml.cs:100-117`), which currently reads:

```csharp
    private async void OnAddToLibraryArrowClick(object sender, RoutedEventArgs e)
    {
        var externalEntries = await libraryRegistryStore.ReadExternalEntriesAsync();
        var defaultName = await libraryRegistryStore.ReadDefaultNameAsync() ?? "Defaut";
        var defaultEntry = new LibraryEntry(defaultName, ResolveDefaultSepDirectory(), IsDefault: true);
        var entries = new[] { defaultEntry }.Concat(externalEntries).ToArray();

        var menu = new ContextMenu();
        foreach (var entry in entries)
        {
            var menuItem = new MenuItem { Header = entry.Name, Tag = entry };
            menuItem.Click += OnLibraryMenuItemClick;
            menu.Items.Add(menuItem);
        }

        menu.PlacementTarget = AddToLibraryArrowButton;
        menu.IsOpen = true;
    }
```

with this (extracting the entry-building logic into the new shared `BuildLibraryEntriesAsync()` so both the arrow menu and the new tab use one source of truth):

```csharp
    private async void OnAddToLibraryArrowClick(object sender, RoutedEventArgs e)
    {
        var entries = await BuildLibraryEntriesAsync();

        var menu = new ContextMenu();
        foreach (var entry in entries)
        {
            var menuItem = new MenuItem { Header = entry.Name, Tag = entry };
            menuItem.Click += OnLibraryMenuItemClick;
            menu.Items.Add(menuItem);
        }

        menu.PlacementTarget = AddToLibraryArrowButton;
        menu.IsOpen = true;
    }

    private async Task<IReadOnlyList<LibraryEntry>> BuildLibraryEntriesAsync()
    {
        var externalEntries = await libraryRegistryStore.ReadExternalEntriesAsync();
        var defaultName = await libraryRegistryStore.ReadDefaultNameAsync() ?? "Defaut";
        var defaultEntry = new LibraryEntry(defaultName, ResolveDefaultSepDirectory(), IsDefault: true);
        return new[] { defaultEntry }.Concat(externalEntries).ToArray();
    }

    private async Task RefreshLibrarySelectorAsync()
    {
        var entries = await BuildLibraryEntriesAsync();
        var previousName = (StudioLibrarySelectorComboBox.SelectedItem as LibraryEntry)?.Name;
        StudioLibrarySelectorComboBox.ItemsSource = entries;
        var toSelect = entries.FirstOrDefault(entry => entry.Name == previousName) ?? entries[0];
        StudioLibrarySelectorComboBox.SelectedItem = toSelect;
    }

    private async void OnStudioLibrarySelectorSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await RefreshLibraryItemsAsync();
    }

    private async Task RefreshLibraryItemsAsync()
    {
        libraryItems.Clear();
        if (StudioLibrarySelectorComboBox.SelectedItem is not LibraryEntry selected)
        {
            return;
        }

        try
        {
            var snapshot = await elementPlusLibraryReader.ReadAsync(selected.Path);
            foreach (var item in snapshot.Items)
            {
                libraryItems.Add(item);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Librairie", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
```

- [ ] **Step 5: Build and run the tests to verify they pass**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: `Build succeeded.`

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~StudioElementPlusLibraryTabContractTests"`
Expected: both PASS.

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: same pass/fail profile as before this task — the 2 pre-existing unrelated failures (`ScadaBuilderLaunchesStudioFromProjectInDevelopmentToAvoidStaleBinaries`, `LegacyContextMenuExposesElementStudioCommand`) still present, nothing else new failing.

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/StudioElementPlusLibraryTabContractTests.cs
git commit -m "feat: add Librairie tab to Studio Element+ side panel"
```

---

### Task 3: Renommer / Copier / Supprimer context menu

**Files:**
- Create: `src/ScadaBuilderV2.ElementStudio.App/ComponentNameDialog.xaml`
- Create: `src/ScadaBuilderV2.ElementStudio.App/ComponentNameDialog.xaml.cs`
- Modify: `src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml.cs`
- Test: `tests/ScadaBuilderV2.Tests/StudioElementPlusLibraryTabContractTests.cs` (extend)

**Interfaces:**
- Consumes: `ElementStudioComponentCopyNaming.GenerateCopyName(string, IEnumerable<string>)` (Task 1). `RefreshLibraryItemsAsync()`, `libraryItems` (`ObservableCollection<ElementPlusLibraryItem>`), `StudioLibraryListBox` (Task 2). Existing `componentPackageStore` field and its methods `ReadFromPathAsync(string, CancellationToken)`, `WriteToPathAsync(ElementStudioComponentPackage, string, CancellationToken)`, `WriteToLibraryAsync(ElementStudioComponentPackage, string, CancellationToken)`, and the static `ElementStudioComponentPackageStore.GetDefaultComponentPath(string libraryRoot, ElementStudioComponentPackage package)` (`src/ScadaBuilderV2.Infrastructure/ElementStudio/ElementStudioComponentPackageStore.cs:13-77`). Existing private `ToComponentId(string value)` (`MainWindow.xaml.cs:964`).
- Produces: nothing consumed by a later task (final task in this plan).

- [ ] **Step 1: Write the failing tests**

Add to `tests/ScadaBuilderV2.Tests/StudioElementPlusLibraryTabContractTests.cs`:

```csharp
    [TestMethod]
    public void MainWindowXamlExposesContextMenuWithDisabledEditAndThreeActions()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml");

        StringAssert.Contains(xaml, "Header=\"Editer\"");
        StringAssert.Contains(xaml, "IsEnabled=\"False\"");
        StringAssert.Contains(xaml, "Click=\"OnRenameLibraryComponentClick\"");
        StringAssert.Contains(xaml, "Click=\"OnCopyLibraryComponentClick\"");
        StringAssert.Contains(xaml, "Click=\"OnDeleteLibraryComponentClick\"");
    }

    [TestMethod]
    public void MainWindowCodeConfirmsBeforeDeletingAndUsesCopyNaming()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml.cs");

        StringAssert.Contains(code, "MessageBoxButton.YesNo");
        StringAssert.Contains(code, "ElementStudioComponentCopyNaming.GenerateCopyName(");
        StringAssert.Contains(code, "ElementStudioComponentPackageStore.GetDefaultComponentPath(");
    }

    [TestMethod]
    public void ComponentNameDialogXamlExposesNameTextBox()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "ComponentNameDialog.xaml");

        StringAssert.Contains(xaml, "x:Name=\"NameTextBox\"");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~MainWindowXamlExposesContextMenuWithDisabledEditAndThreeActions|FullyQualifiedName~MainWindowCodeConfirmsBeforeDeletingAndUsesCopyNaming|FullyQualifiedName~ComponentNameDialogXamlExposesNameTextBox"`
Expected: all 3 FAIL. The first two already partially pass on the `Header="Editer"`/`IsEnabled="False"` and `Click="OnRenameLibraryComponentClick"` etc. strings from Task 2's XAML (those are already present) but fail on the missing `MessageBoxButton.YesNo`/`GenerateCopyName`/`GetDefaultComponentPath` code strings and the missing handler methods; the third fails because `ComponentNameDialog.xaml` doesn't exist yet.

- [ ] **Step 3: Create `ComponentNameDialog`**

Create `src/ScadaBuilderV2.ElementStudio.App/ComponentNameDialog.xaml`:

```xml
<Window x:Class="ScadaBuilderV2.ElementStudio.App.ComponentNameDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Nom du composant"
        Width="360"
        Height="180"
        MinWidth="320"
        MinHeight="180"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize"
        ShowInTaskbar="False">
    <Window.Resources>
        <SolidColorBrush x:Key="InkBrush" Color="#0F2A30"/>
        <SolidColorBrush x:Key="BorderBrushSoft" Color="#260F2A30"/>
        <Style TargetType="Button">
            <Setter Property="MinHeight" Value="32"/>
            <Setter Property="Padding" Value="12,6"/>
            <Setter Property="Margin" Value="4"/>
            <Setter Property="BorderBrush" Value="{StaticResource BorderBrushSoft}"/>
        </Style>
        <Style x:Key="PrimaryButtonStyle" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
            <Setter Property="Background" Value="#2090A0"/>
            <Setter Property="BorderBrush" Value="#0F7280"/>
            <Setter Property="Foreground" Value="#FFFFFF"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
        </Style>
    </Window.Resources>
    <DockPanel Margin="16">
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12,0,0">
            <Button Content="OK" Style="{StaticResource PrimaryButtonStyle}" Click="OnOkClick" IsDefault="True"/>
            <Button Content="Annuler" Click="OnCancelClick" IsCancel="True"/>
        </StackPanel>
        <StackPanel>
            <TextBlock x:Name="PromptText" Text="Nom du composant" Foreground="{StaticResource InkBrush}" Margin="0,0,0,6"/>
            <TextBox x:Name="NameTextBox" MinHeight="30"/>
            <TextBlock x:Name="ErrorText" Foreground="Red" Margin="0,6,0,0" TextWrapping="Wrap" Visibility="Collapsed"/>
        </StackPanel>
    </DockPanel>
</Window>
```

Create `src/ScadaBuilderV2.ElementStudio.App/ComponentNameDialog.xaml.cs`:

```csharp
using System.Windows;

namespace ScadaBuilderV2.ElementStudio.App;

public partial class ComponentNameDialog : Window
{
    public ComponentNameDialog(string prompt, string initialName)
    {
        InitializeComponent();
        PromptText.Text = prompt;
        NameTextBox.Text = initialName;
        NameTextBox.SelectAll();
        Loaded += (_, _) => NameTextBox.Focus();
    }

    public string EnteredName { get; private set; } = "";

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        var trimmed = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            ErrorText.Text = "Le nom ne peut pas etre vide.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        EnteredName = trimmed;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
```

- [ ] **Step 4: Add the three context-menu handlers to `MainWindow.xaml.cs`**

Add these three methods after `RefreshLibraryItemsAsync()` (added in Task 2):

```csharp
    private async void OnRenameLibraryComponentClick(object sender, RoutedEventArgs e)
    {
        if (StudioLibraryListBox.SelectedItem is not ElementPlusLibraryItem selected)
        {
            return;
        }

        var nameDialog = new ComponentNameDialog("Nouveau nom du composant", selected.Name) { Owner = this };
        if (nameDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var package = await componentPackageStore.ReadFromPathAsync(selected.FilePath);
            var renamedComponent = package.Component with { Name = nameDialog.EnteredName };
            var renamedPackage = package with { Component = renamedComponent };
            var libraryRoot = Path.GetDirectoryName(selected.FilePath) ?? "";
            var newPath = ElementStudioComponentPackageStore.GetDefaultComponentPath(libraryRoot, renamedPackage);

            await componentPackageStore.WriteToPathAsync(renamedPackage, newPath);
            if (!string.Equals(Path.GetFullPath(newPath), Path.GetFullPath(selected.FilePath), StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(selected.FilePath);
            }

            await RefreshLibraryItemsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Renommer le composant", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnCopyLibraryComponentClick(object sender, RoutedEventArgs e)
    {
        if (StudioLibraryListBox.SelectedItem is not ElementPlusLibraryItem selected)
        {
            return;
        }

        try
        {
            var package = await componentPackageStore.ReadFromPathAsync(selected.FilePath);
            var existingNames = libraryItems.Select(item => item.Name);
            var copyName = ElementStudioComponentCopyNaming.GenerateCopyName(selected.Name, existingNames);
            var copiedComponent = package.Component with
            {
                ComponentId = ToComponentId(copyName),
                Name = copyName
            };
            var copiedPackage = package with { Component = copiedComponent };
            var libraryRoot = Path.GetDirectoryName(selected.FilePath) ?? "";

            await componentPackageStore.WriteToLibraryAsync(copiedPackage, libraryRoot);
            await RefreshLibraryItemsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Copier le composant", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnDeleteLibraryComponentClick(object sender, RoutedEventArgs e)
    {
        if (StudioLibraryListBox.SelectedItem is not ElementPlusLibraryItem selected)
        {
            return;
        }

        var confirmed = MessageBox.Show(
            this,
            $"Supprimer definitivement le composant '{selected.Name}' ?",
            "Supprimer le composant",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmed != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            File.Delete(selected.FilePath);
            await RefreshLibraryItemsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Supprimer le composant", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
```

- [ ] **Step 5: Build and run the tests to verify they pass**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: `Build succeeded.`

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~StudioElementPlusLibraryTabContractTests|FullyQualifiedName~ElementStudioComponentCopyNamingTests"`
Expected: all PASS.

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: same pass/fail profile as before this task — the 2 pre-existing unrelated failures still present, nothing else new failing.

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.ElementStudio.App/ComponentNameDialog.xaml src/ScadaBuilderV2.ElementStudio.App/ComponentNameDialog.xaml.cs src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/StudioElementPlusLibraryTabContractTests.cs
git commit -m "feat: add Renommer/Copier/Supprimer to Studio Element+ Librairie tab"
```

---

## Self-Review Notes

- **Spec coverage:** new "Librairie" `TabItem` next to Element/Structure/Proprietes/Composant (Task 2); reuse of `LibraryRegistryStore`/`ElementPlusLibraryReader` with no new reading infrastructure (Task 2); context menu with Éditer disabled + tooltip, Renommer, Copier (`_copieN` naming, new `ComponentId`), Supprimer with Yes/No confirmation (Task 3); file-operation error handling via try/catch + `MessageBox` (Tasks 2-3) — all covered. The "Éditer" and "Enregistrer sous variant" capabilities are explicitly out of scope per the spec and are not implemented here.
- **Placeholder scan:** none — every step has literal code and exact commands.
- **Type consistency:** `LibraryEntry`, `LibraryRegistryStore`, `ElementPlusLibraryReader`, `ElementPlusLibraryItem`, `ElementPlusLibrarySnapshot`, `ElementStudioComponentPackageStore`, `ElementStudioComponentPackage`/`ElementStudioComponent` are all used with their exact existing signatures from the current codebase (verified by reading the live source, not assumed). `ElementStudioComponentCopyNaming.GenerateCopyName` defined in Task 1 is called identically in Task 3. `BuildLibraryEntriesAsync()`/`RefreshLibrarySelectorAsync()`/`RefreshLibraryItemsAsync()`/`libraryItems` introduced in Task 2 are consumed unchanged in Task 3.
- **Known out-of-scope risk (not a task gap, flagging for future awareness):** Renommer's `WriteToPathAsync(renamedPackage, newPath)` will silently overwrite a *different* existing component if the new name happens to collide with another component's filename in the same library. The spec does not request collision detection for Renommer (only Task 1's `_copieN` generation avoids collisions, and only for Copier), so this plan does not add it — noted here rather than silently expanding scope.
