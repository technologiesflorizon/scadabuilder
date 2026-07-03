# Studio Element+ Library Preview Tiles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the plain-text list in Studio Element+'s "Librairie" tab with icon/preview tiles, matching SCADA Builder V2's own "Librairie" panel exactly.

**Architecture:** Port the existing `HtmlPreviewControl` (a small, self-contained WPF `UserControl` with no `ScadaBuilderV2.App`-specific dependencies, currently living only in that project) into `ScadaBuilderV2.ElementStudio.App`, then replace `StudioLibraryListBox`'s plain `DisplayMemberPath` rendering with the exact same `WrapPanel` + sized `ItemContainerStyle` + `DataTemplate` (preview tile with fallback letter badge and filename label) that SCADA Builder V2's `ElementLibraryListBox` already uses. No model changes — `ElementPlusLibraryItem` (`PreviewMarkup`, `IconText`, `FileName`, `DetailText`) already has every field the tile needs.

**Tech Stack:** C# / .NET 8, WPF (XAML), MSTest.

## Global Constraints

- Visual match with SCADA Builder V2's "Librairie" panel: 92×112 tiles in a `WrapPanel`, a 62px-tall preview border rendering `PreviewMarkup` via `HtmlPreviewControl`, a centered `IconText` letter badge shown only when `PreviewMarkup` is null, and a `FileName` label below — same colors/sizes as the reference implementation (`src/ScadaBuilderV2.App/MainWindow.xaml:1096-1158`).
- `HtmlPreviewControl` is ported verbatim (same rendering logic) into `ScadaBuilderV2.ElementStudio.App` — do not alter its behavior, only its namespace.
- The existing `ContextMenu` (Éditer disabled/Renommer/Copier/Supprimer) on `StudioLibraryListBox` must be preserved unchanged — this is a rendering-only change.

---

## Before You Start

This plan continues directly on branch `master` in `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2` (per the user's established preference to work without a feature branch). There may still be a pre-existing unrelated uncommitted change to `projects/AMR_REF_SCADA_V2/scenes/win00008.scene.json` in the working tree — do not touch, stage, or commit that file in any task below.

---

### Task 1: Icon/preview tiles for the Librairie tab

**Files:**
- Create: `src/ScadaBuilderV2.ElementStudio.App/HtmlPreviewControl.cs`
- Modify: `src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml:1-8` (add `xmlns:local`)
- Modify: `src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml:412-426` (replace `StudioLibraryListBox`'s rendering)
- Test: `tests/ScadaBuilderV2.Tests/StudioElementPlusLibraryTabContractTests.cs` (extend)

**Interfaces:**
- Consumes: `ElementPlusLibraryItem`'s existing properties `PreviewMarkup` (`string?`), `IconText` (`string`), `FileName` (`string`), `DetailText` (`string`) — all already defined in `src/ScadaBuilderV2.Application/ElementStudio/ElementPlusLibraryModels.cs:5-30`, unchanged by this task. The existing `AccentBlueBrush` resource already declared in this file's `Window.Resources` (`MainWindow.xaml:19`).
- Produces: `ScadaBuilderV2.ElementStudio.App.HtmlPreviewControl` (a `UserControl` with a `Markup` dependency property), referenced from XAML as `local:HtmlPreviewControl`. Nothing else in this plan consumes it further (final task).

Because this repo's test project cannot reference the WPF `ScadaBuilderV2.ElementStudio.App` project, verification follows this feature's established pattern: read `.xaml`/`.cs` as text and assert on their contents.

- [ ] **Step 1: Write the failing tests**

Add to `tests/ScadaBuilderV2.Tests/StudioElementPlusLibraryTabContractTests.cs`:

```csharp
    [TestMethod]
    public void MainWindowXamlRendersLibraryItemsAsPreviewTiles()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml");

        StringAssert.Contains(xaml, "xmlns:local=\"clr-namespace:ScadaBuilderV2.ElementStudio.App\"");
        StringAssert.Contains(xaml, "<WrapPanel/>");
        StringAssert.Contains(xaml, "local:HtmlPreviewControl");
        StringAssert.Contains(xaml, "Markup=\"{Binding PreviewMarkup}\"");
        StringAssert.Contains(xaml, "Text=\"{Binding IconText}\"");
        StringAssert.Contains(xaml, "Text=\"{Binding FileName}\"");
        StringAssert.Contains(xaml, "ToolTip=\"{Binding DetailText}\"");
    }

    [TestMethod]
    public void HtmlPreviewControlExistsInElementStudioApp()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "HtmlPreviewControl.cs");

        StringAssert.Contains(code, "namespace ScadaBuilderV2.ElementStudio.App;");
        StringAssert.Contains(code, "public sealed class HtmlPreviewControl : UserControl");
        StringAssert.Contains(code, "public static readonly DependencyProperty MarkupProperty");
        StringAssert.Contains(code, "browser.NavigateToString(");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~MainWindowXamlRendersLibraryItemsAsPreviewTiles|FullyQualifiedName~HtmlPreviewControlExistsInElementStudioApp"`
Expected: both FAIL — `HtmlPreviewControl.cs` doesn't exist yet, and none of the new XAML strings exist yet.

- [ ] **Step 3: Port `HtmlPreviewControl` into `ScadaBuilderV2.ElementStudio.App`**

Create `src/ScadaBuilderV2.ElementStudio.App/HtmlPreviewControl.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;

namespace ScadaBuilderV2.ElementStudio.App;

public sealed class HtmlPreviewControl : UserControl
{
    private readonly WebBrowser browser = new();

    public static readonly DependencyProperty MarkupProperty =
        DependencyProperty.Register(
            nameof(Markup),
            typeof(string),
            typeof(HtmlPreviewControl),
            new PropertyMetadata(null, OnMarkupChanged));

    public string? Markup
    {
        get => (string?)GetValue(MarkupProperty);
        set => SetValue(MarkupProperty, value);
    }

    public HtmlPreviewControl()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsHitTestVisible = false;
        Content = browser;
        Loaded += (_, _) => RenderMarkup();
    }

    private static void OnMarkupChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((HtmlPreviewControl)dependencyObject).RenderMarkup();
    }

    private void RenderMarkup()
    {
        if (!IsLoaded)
        {
            return;
        }

        var markup = Markup;
        if (string.IsNullOrWhiteSpace(markup))
        {
            browser.NavigateToString("<!doctype html><html><body></body></html>");
            return;
        }

        browser.NavigateToString($$"""
<!doctype html>
<html>
<head>
  <meta http-equiv="X-UA-Compatible" content="IE=edge">
  <style>
    html, body {
      width: 100%;
      height: 100%;
      margin: 0;
      overflow: hidden;
      background: transparent;
    }
    body {
      display: flex;
      align-items: center;
      justify-content: center;
    }
    svg {
      display: block;
      width: 100%;
      height: 100%;
      max-width: 100%;
      max-height: 100%;
    }
    body > * {
      max-width: 100%;
      max-height: 100%;
      box-sizing: border-box;
    }
  </style>
</head>
<body>
{{markup}}
</body>
</html>
""");
    }
}
```

- [ ] **Step 4: Declare the `local` XAML namespace**

In `src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml`, change:

```xml
<Window x:Class="ScadaBuilderV2.ElementStudio.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:wv2="clr-namespace:Microsoft.Web.WebView2.Wpf;assembly=Microsoft.Web.WebView2.Wpf"
```

to:

```xml
<Window x:Class="ScadaBuilderV2.ElementStudio.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:ScadaBuilderV2.ElementStudio.App"
        xmlns:wv2="clr-namespace:Microsoft.Web.WebView2.Wpf;assembly=Microsoft.Web.WebView2.Wpf"
```

- [ ] **Step 5: Replace `StudioLibraryListBox`'s rendering with preview tiles**

In `src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml`, change:

```xml
                            <ListBox x:Name="StudioLibraryListBox"
                                     DisplayMemberPath="Name"
                                     ItemContainerStyle="{StaticResource ElementListBoxItemStyle}">
                                <ListBox.ContextMenu>
                                    <ContextMenu>
                                        <MenuItem Header="Editer"
                                                  IsEnabled="False"
                                                  ToolTip="Edition disponible dans une prochaine version"
                                                  ToolTipService.ShowOnDisabled="True"/>
                                        <MenuItem Header="Renommer" Click="OnRenameLibraryComponentClick"/>
                                        <MenuItem Header="Copier" Click="OnCopyLibraryComponentClick"/>
                                        <MenuItem Header="Supprimer" Click="OnDeleteLibraryComponentClick"/>
                                    </ContextMenu>
                                </ListBox.ContextMenu>
                            </ListBox>
```

to:

```xml
                            <ListBox x:Name="StudioLibraryListBox"
                                     BorderThickness="0"
                                     Background="Transparent"
                                     ScrollViewer.HorizontalScrollBarVisibility="Disabled">
                                <ListBox.ItemsPanel>
                                    <ItemsPanelTemplate>
                                        <WrapPanel/>
                                    </ItemsPanelTemplate>
                                </ListBox.ItemsPanel>
                                <ListBox.ItemContainerStyle>
                                    <Style TargetType="ListBoxItem" BasedOn="{StaticResource ElementListBoxItemStyle}">
                                        <Setter Property="Width" Value="92"/>
                                        <Setter Property="Height" Value="112"/>
                                        <Setter Property="Padding" Value="3"/>
                                        <Setter Property="Margin" Value="3"/>
                                        <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
                                    </Style>
                                </ListBox.ItemContainerStyle>
                                <ListBox.ItemTemplate>
                                    <DataTemplate>
                                        <StackPanel ToolTip="{Binding DetailText}">
                                            <Border Height="62"
                                                    CornerRadius="4"
                                                    Background="#F2F8EF"
                                                    BorderBrush="#335E7A82"
                                                    BorderThickness="1"
                                                    Margin="0,0,0,5">
                                                <Grid>
                                                    <local:HtmlPreviewControl Markup="{Binding PreviewMarkup}"
                                                                              IsHitTestVisible="False"/>
                                                    <TextBlock Text="{Binding IconText}"
                                                               HorizontalAlignment="Center"
                                                               VerticalAlignment="Center"
                                                               FontWeight="Bold"
                                                               FontSize="18"
                                                               Foreground="{StaticResource AccentBlueBrush}">
                                                        <TextBlock.Style>
                                                            <Style TargetType="TextBlock">
                                                                <Setter Property="Visibility" Value="Collapsed"/>
                                                                <Style.Triggers>
                                                                    <DataTrigger Binding="{Binding PreviewMarkup}" Value="{x:Null}">
                                                                        <Setter Property="Visibility" Value="Visible"/>
                                                                    </DataTrigger>
                                                                </Style.Triggers>
                                                            </Style>
                                                        </TextBlock.Style>
                                                    </TextBlock>
                                                </Grid>
                                            </Border>
                                            <TextBlock Text="{Binding FileName}"
                                                       FontSize="11"
                                                       TextAlignment="Center"
                                                       TextWrapping="Wrap"
                                                       MaxHeight="38"
                                                       TextTrimming="CharacterEllipsis"/>
                                        </StackPanel>
                                    </DataTemplate>
                                </ListBox.ItemTemplate>
                                <ListBox.ContextMenu>
                                    <ContextMenu>
                                        <MenuItem Header="Editer"
                                                  IsEnabled="False"
                                                  ToolTip="Edition disponible dans une prochaine version"
                                                  ToolTipService.ShowOnDisabled="True"/>
                                        <MenuItem Header="Renommer" Click="OnRenameLibraryComponentClick"/>
                                        <MenuItem Header="Copier" Click="OnCopyLibraryComponentClick"/>
                                        <MenuItem Header="Supprimer" Click="OnDeleteLibraryComponentClick"/>
                                    </ContextMenu>
                                </ListBox.ContextMenu>
                            </ListBox>
```

- [ ] **Step 6: Build and run the tests to verify they pass**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: `Build succeeded.`

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~MainWindowXamlRendersLibraryItemsAsPreviewTiles|FullyQualifiedName~HtmlPreviewControlExistsInElementStudioApp"`
Expected: both PASS.

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: same pass/fail profile as before this task — the 2 pre-existing unrelated failures (`ScadaBuilderLaunchesStudioFromProjectInDevelopmentToAvoidStaleBinaries`, `LegacyContextMenuExposesElementStudioCommand`) still present, nothing else new failing.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.ElementStudio.App/HtmlPreviewControl.cs src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml tests/ScadaBuilderV2.Tests/StudioElementPlusLibraryTabContractTests.cs
git commit -m "feat: render Studio Element+ Librairie tab as preview tiles"
```

---

## Self-Review Notes

- **Spec coverage:** the approved inline design (mirror SCADA Builder V2's exact tile visual, port `HtmlPreviewControl`, preserve the context menu) is fully covered by this single task.
- **Placeholder scan:** none — every step has literal code and exact commands.
- **Type consistency:** `ElementPlusLibraryItem.PreviewMarkup`/`IconText`/`FileName`/`DetailText` are used exactly as already defined in `ElementPlusLibraryModels.cs` — no new model fields introduced. `HtmlPreviewControl`'s `Markup` dependency property matches its existing, unmodified implementation in `ScadaBuilderV2.App`.
