# AvalonDock Side Panels Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fixed `Grid`/`GridSplitter` side-panel layout in `SCADA_BUILDER_V2.App`'s `MainWindow` with AvalonDock anchorable panes (draggable, floatable, closable/reopenable, persisted across sessions), without changing the central WebView2 canvas area's behavior.

**Architecture:** Wrap the existing three-column shell in an AvalonDock `DockingManager`. The 3 left tabs (`Outil`, `Projet`, `Catalogue Tags`) and 4 right tabs (`Page`, `Element`, `Propriete`, `Librairie`) each become an independent `LayoutAnchorable` in a left/right `LayoutAnchorablePane`; existing tab *content* moves unchanged into each anchorable. The untouched center content (the `DockPanel` containing `SceneTabs` and `PreviewSurfaceBorder`/`PreviewWebView`) is wrapped in a single non-closable `LayoutDocument` inside AvalonDock's required `LayoutDocumentPane` — this satisfies AvalonDock's structural requirement of a document host while keeping the center region visually and behaviorally identical to today (single area, no new tabs — multi-tab canvas is a separate plan). Layout state (pane positions/floating/visibility) persists to `%AppData%\ScadaBuilderV2\dock-layout.xml` via a new `DockLayoutStore` (path-parameterized like the existing `LibraryRegistryStore`), loaded on window `Loaded` and saved during the existing `OnMainWindowClosing` confirmed-close path.

**Tech Stack:** .NET 8, WPF (`net8.0-windows`), AvalonDock (NuGet `Xceed.Wpf.AvalonDock` 5.2.26322.8434 — the plain `AvalonDock` NuGet package id is a stale/unmaintained line capped at 2.0.2000; `Xceed.Wpf.AvalonDock` is the actively maintained package providing the `Xceed.Wpf.AvalonDock.*` namespaces used throughout this plan), MSTest (existing `tests\ScadaBuilderV2.Tests`, `net8.0`, no WPF reference).

## Global Constraints

- Target framework for any new/modified project stays `net8.0-windows` (App) or `net8.0` (Domain/Application/Infrastructure/Rendering/Tests) — do not change any `TargetFramework`.
- `tests\ScadaBuilderV2.Tests` does not and must not reference `ScadaBuilderV2.App` or any WPF/AvalonDock assembly — new automated tests for this feature must target a plain C# class in `ScadaBuilderV2.Infrastructure`.
- No existing MSTest test may be broken; `dotnet test ScadaBuilderV2.sln --no-restore` must pass the same 302 tests that pass today (plus any new ones this plan adds) after every task. 4 pre-existing failures unrelated to this work (`ScadaBuilderLaunchesStudioFromProjectInDevelopmentToAvoidStaleBinaries`, `StudioFinalPolishContractKeepsSelectionStructureAndDecisionTrace`, `LegacyContextMenuExposesElementStudioCommand`, `ReadOnlyNumericElementsRenderDisplayFormatWhenValueIsMissing`) are a known baseline condition — do not fix them as part of this plan, but never let their count grow.
- Panel *content* (existing XAML inside each tab: `ElementLibraryListBox`, `TagCatalogDataGrid`, the nested `General`/`Style`/`Bouton`/`Evenement`/`Donnees` property sub-tabs, etc.) moves verbatim — no redesign of what is inside a panel.
- The center canvas region (`SceneTabs`, `PreviewSurfaceBorder`, `PreviewWebView`, `PreviewPlaceholder`) is moved as a block into a single `LayoutDocument` with no internal changes to its XAML or the C# that manipulates it (`PreviewSurfaceBorder`, `PreviewWebView`, `SceneTabs` references in `MainWindow.xaml.cs` keep their exact current names and behavior).
- Persisted settings files in this codebase live under `%AppData%\ScadaBuilderV2\<file>` (see `LibraryRegistryStore.GetDefaultSettingsPath()`), using `System.Text.Json`-free hand-rolled read/write with `Directory.CreateDirectory` before write and swallowed `IOException`/format exceptions on read returning a default. The new `dock-layout.xml` follows the same root folder convention (raw XML text, not JSON).
- Public C# members require XML doc comments per `docs/AGENTS.md` (existing convention already enforced across `Application`/`Infrastructure`).

---

### Task 1: Add AvalonDock package reference and verify the solution still builds

**Files:**
- Modify: `src/ScadaBuilderV2.App/ScadaBuilderV2.App.csproj:10-12`

**Interfaces:**
- Produces: `Xceed.Wpf.AvalonDock` assembly available to `ScadaBuilderV2.App` (types `Xceed.Wpf.AvalonDock.DockingManager`, `Xceed.Wpf.AvalonDock.Layout.LayoutAnchorable`, `Xceed.Wpf.AvalonDock.Layout.LayoutDocument`, `Xceed.Wpf.AvalonDock.Layout.Serialization.XmlLayoutSerializer` — used by later tasks).

- [ ] **Step 1: Add the package reference**

Edit `src/ScadaBuilderV2.App/ScadaBuilderV2.App.csproj`, inside the existing `ItemGroup` that currently contains only the WebView2 reference (lines 10-12):

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.3967.48" />
    <PackageReference Include="Xceed.Wpf.AvalonDock" Version="5.2.26322.8434" />
  </ItemGroup>
```

- [ ] **Step 2: Restore and build**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: Build succeeds (`Build succeeded.`), NuGet restores `Xceed.Wpf.AvalonDock` 5.2.26322.8434 (and its dependents) with no version conflicts.

- [ ] **Step 3: Run the full test suite to confirm no regression**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 306 tests total, the same 302 pass and the same 4 pre-existing failures remain (`ScadaBuilderLaunchesStudioFromProjectInDevelopmentToAvoidStaleBinaries`, `StudioFinalPolishContractKeepsSelectionStructureAndDecisionTrace`, `LegacyContextMenuExposesElementStudioCommand`, `ReadOnlyNumericElementsRenderDisplayFormatWhenValueIsMissing`) — adding an unused package reference must not change test behavior.

- [ ] **Step 4: Commit**

```bash
git add src/ScadaBuilderV2.App/ScadaBuilderV2.App.csproj
git commit -m "build: add AvalonDock package reference to App project"
```

---

### Task 2: `DockLayoutStore` — testable persistence for the dock layout XML

**Files:**
- Create: `src/ScadaBuilderV2.Infrastructure/Shell/DockLayoutStore.cs`
- Test: `tests/ScadaBuilderV2.Tests/DockLayoutStoreTests.cs`

**Interfaces:**
- Produces:
  - `namespace ScadaBuilderV2.Infrastructure.Shell`
  - `public sealed class DockLayoutStore`
    - `public string GetDefaultLayoutPath()` — returns `%AppData%\ScadaBuilderV2\dock-layout.xml` (via `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)`).
    - `public async Task<string?> ReadLayoutXmlAsync(string path)` — returns file contents as `string`, or `null` if the file does not exist or cannot be read/parsed as text (swallows `IOException`, `UnauthorizedAccessException`).
    - `public async Task WriteLayoutXmlAsync(string path, string layoutXml)` — creates the containing directory if missing, then writes `layoutXml` to `path` (overwrite). Propagates unexpected exceptions (caller decides how to handle write failures on close).
  - This class has **no dependency on AvalonDock or WPF** — it only reads/writes a raw XML string. `MainWindow.xaml.cs` (Task 5) is responsible for producing/consuming that string via AvalonDock's `XmlLayoutSerializer` against a `StringWriter`/`StringReader`.

- [ ] **Step 1: Write the failing tests**

Create `tests/ScadaBuilderV2.Tests/DockLayoutStoreTests.cs`:

```csharp
using ScadaBuilderV2.Infrastructure.Shell;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class DockLayoutStoreTests
{
    private string _tempDirectory = "";
    private string _tempPath = "";

    [TestInitialize]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"scada-builder-v2-dock-layout-test-{Guid.NewGuid():N}");
        _tempPath = Path.Combine(_tempDirectory, "dock-layout.xml");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadLayoutXmlAsyncReturnsNullWhenFileMissing()
    {
        var store = new DockLayoutStore();

        var result = await store.ReadLayoutXmlAsync(_tempPath);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task WriteThenReadRoundTripsLayoutXml()
    {
        var store = new DockLayoutStore();
        const string xml = "<LayoutRoot><RootPanel/></LayoutRoot>";

        await store.WriteLayoutXmlAsync(_tempPath, xml);
        var result = await store.ReadLayoutXmlAsync(_tempPath);

        Assert.AreEqual(xml, result);
    }

    [TestMethod]
    public async Task WriteLayoutXmlAsyncCreatesMissingDirectory()
    {
        var store = new DockLayoutStore();
        Assert.IsFalse(Directory.Exists(_tempDirectory));

        await store.WriteLayoutXmlAsync(_tempPath, "<LayoutRoot/>");

        Assert.IsTrue(File.Exists(_tempPath));
    }

    [TestMethod]
    public void GetDefaultLayoutPathEndsWithScadaBuilderV2DockLayoutXml()
    {
        var store = new DockLayoutStore();

        var path = store.GetDefaultLayoutPath();

        Assert.IsTrue(path.EndsWith(Path.Combine("ScadaBuilderV2", "dock-layout.xml"), StringComparison.Ordinal));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ScadaBuilderV2.Tests --filter "FullyQualifiedName~DockLayoutStoreTests"`
Expected: Compile error (`DockLayoutStore` does not exist) — confirms the test references a not-yet-created type.

- [ ] **Step 3: Implement `DockLayoutStore`**

Create `src/ScadaBuilderV2.Infrastructure/Shell/DockLayoutStore.cs`:

```csharp
namespace ScadaBuilderV2.Infrastructure.Shell;

/// <summary>
/// Reads and writes the AvalonDock layout XML used to persist the SCADA Builder V2
/// shell's side-panel arrangement across sessions.
/// </summary>
public sealed class DockLayoutStore
{
    /// <summary>
    /// Returns the default per-user path for the dock layout file:
    /// <c>%AppData%\ScadaBuilderV2\dock-layout.xml</c>.
    /// </summary>
    public string GetDefaultLayoutPath()
    {
        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataRoot, "ScadaBuilderV2", "dock-layout.xml");
    }

    /// <summary>
    /// Reads the layout XML at <paramref name="path"/>. Returns <c>null</c> if the file
    /// does not exist or cannot be read.
    /// </summary>
    public async Task<string?> ReadLayoutXmlAsync(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return await File.ReadAllTextAsync(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes <paramref name="layoutXml"/> to <paramref name="path"/>, creating the
    /// containing directory if it does not exist.
    /// </summary>
    public async Task WriteLayoutXmlAsync(string path, string layoutXml)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, layoutXml);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ScadaBuilderV2.Tests --filter "FullyQualifiedName~DockLayoutStoreTests"`
Expected: 4 tests pass.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 310 tests total, 306 pass (302 existing + 4 new), the same 4 pre-existing failures remain.

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.Infrastructure/Shell/DockLayoutStore.cs tests/ScadaBuilderV2.Tests/DockLayoutStoreTests.cs
git commit -m "feat: add DockLayoutStore for persisting AvalonDock layout XML"
```

---

### Task 3: Replace the fixed Grid shell with an AvalonDock `DockingManager`

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml:349-1186`

**Interfaces:**
- Consumes: `AvalonDock` types from Task 1 (`Xceed.Wpf.AvalonDock.DockingManager`, `.Layout.LayoutRoot`, `.Layout.LayoutPanel`, `.Layout.LayoutAnchorablePane`, `.Layout.LayoutAnchorable`, `.Layout.LayoutDocumentPane`, `.Layout.LayoutDocument`).
- Produces (named elements later tasks/code rely on):
  - `x:Name="MainDockingManager"` (the root `DockingManager`) — consumed by Task 5 (`XmlLayoutSerializer`).
  - `x:Name="ToolAnchorable"`, `x:Name="ProjectAnchorable"`, `x:Name="TagCatalogAnchorable"` (left pane) — consumed by Task 4 (Fenêtres menu, close→hide behavior).
  - `x:Name="PageAnchorable"`, `x:Name="ElementAnchorable"`, `x:Name="PropertiesAnchorable"`, `x:Name="LibraryAnchorable"` (right pane) — consumed by Task 4 (Fenêtres menu, close→hide behavior) and existing code at `MainWindow.xaml.cs:214,3779,3843` (rewired in Task 4).
  - `PreviewSurfaceBorder`, `PreviewWebView`, `PreviewPlaceholder`, `SceneTabs` keep their existing names, moved unchanged into the new `LayoutDocument`.

- [ ] **Step 1: Add the AvalonDock XML namespace to the root `Window` element**

`MainWindow.xaml` currently declares its root `<Window ...>` element namespaces near the top of the file. Add the AvalonDock namespace alongside the existing ones (find the root `<Window` opening tag and add this attribute on its own line, after the existing `xmlns:x=` declaration):

```xml
        xmlns:avalonDock="https://github.com/Dirkster99/AvalonDock"
```

- [ ] **Step 2: Replace the outer 5-column Grid (lines 349-1186) with a `DockingManager`**

Replace the entire block from the `<Grid>` opening at line 349 through its matching `</Grid>` at line 1186 (this removes the `Grid.ColumnDefinitions`, the two `GridSplitter` elements at lines 475-478 and 530-533, the left `Border`/`TabControl` at lines 358-473, the center `Grid` at lines 480-528, and the right `Border`/`TabControl` at lines 535-1185) with:

```xml
        <avalonDock:DockingManager x:Name="MainDockingManager">
            <avalonDock:DockingManager.Theme>
                <avalonDock:AeroTheme />
            </avalonDock:DockingManager.Theme>
            <avalonDock:LayoutRoot>
                <avalonDock:LayoutPanel Orientation="Horizontal">
                    <avalonDock:LayoutAnchorablePane DockWidth="220">
                        <avalonDock:LayoutAnchorable x:Name="ToolAnchorable" Title="Outil" ContentId="Tool" CanClose="True">
                            <!-- MOVE HERE, UNCHANGED: the content that was inside
                                 <TabItem Header="Outil"> at MainWindow.xaml:363-376 -->
                        </avalonDock:LayoutAnchorable>
                        <avalonDock:LayoutAnchorable x:Name="ProjectAnchorable" Title="Projet" ContentId="Project" CanClose="True">
                            <!-- MOVE HERE, UNCHANGED: the content that was inside
                                 <TabItem Header="Projet"> at MainWindow.xaml:377-400 -->
                        </avalonDock:LayoutAnchorable>
                        <avalonDock:LayoutAnchorable x:Name="TagCatalogAnchorable" Title="Catalogue Tags" ContentId="TagCatalog" CanClose="True">
                            <!-- MOVE HERE, UNCHANGED: the content that was inside
                                 <TabItem Header="Catalogue Tags"> at MainWindow.xaml:401-470,
                                 including the TagCatalogDataGrid element -->
                        </avalonDock:LayoutAnchorable>
                    </avalonDock:LayoutAnchorablePane>

                    <avalonDock:LayoutDocumentPane>
                        <avalonDock:LayoutDocument Title="Canvas" ContentId="Canvas" CanClose="False" CanFloat="False">
                            <!-- MOVE HERE, UNCHANGED: the entire center Grid content that was at
                                 MainWindow.xaml:480-528, i.e. the DockPanel containing
                                 x:Name="SceneTabs" and the x:Name="PreviewSurfaceBorder" Border
                                 (which itself contains x:Name="PreviewWebView" and
                                 x:Name="PreviewPlaceholder"). No changes to this subtree's XAML. -->
                        </avalonDock:LayoutDocument>
                    </avalonDock:LayoutDocumentPane>

                    <avalonDock:LayoutAnchorablePane DockWidth="320">
                        <avalonDock:LayoutAnchorable x:Name="PageAnchorable" Title="Page" ContentId="Page" CanClose="True">
                            <!-- MOVE HERE, UNCHANGED: the content that was inside
                                 <TabItem x:Name="PageContextTab" Header="Page"> at
                                 MainWindow.xaml:555-827 -->
                        </avalonDock:LayoutAnchorable>
                        <avalonDock:LayoutAnchorable x:Name="ElementAnchorable" Title="Element" ContentId="Element" CanClose="True">
                            <!-- MOVE HERE, UNCHANGED: the content that was inside
                                 <TabItem Header="Element"> at MainWindow.xaml:828-867 -->
                        </avalonDock:LayoutAnchorable>
                        <avalonDock:LayoutAnchorable x:Name="PropertiesAnchorable" Title="Propriete" ContentId="Properties" CanClose="True">
                            <!-- MOVE HERE, UNCHANGED: the content that was inside
                                 <TabItem x:Name="PropertiesContextTab" Header="Propriete"> at
                                 MainWindow.xaml:868-1078, including the nested General/Style/
                                 Bouton/Evenement/Donnees TabControl and TagCatalogDataGrid-adjacent
                                 controls -->
                        </avalonDock:LayoutAnchorable>
                        <avalonDock:LayoutAnchorable x:Name="LibraryAnchorable" Title="Librairie" ContentId="Library" CanClose="True">
                            <!-- MOVE HERE, UNCHANGED: the content that was inside
                                 <TabItem Header="Librairie"> at MainWindow.xaml:1079-1182,
                                 including the ElementLibraryListBox element -->
                        </avalonDock:LayoutAnchorable>
                    </avalonDock:LayoutAnchorablePane>
                </avalonDock:LayoutPanel>
            </avalonDock:LayoutRoot>
        </avalonDock:DockingManager>
```

When performing this edit, cut each `TabItem`'s child content (everything between the `TabItem`'s opening tag with its `Header="..."` and its matching `</TabItem>`) from its original location and paste it as the direct child of the corresponding `LayoutAnchorable` above, replacing the `<!-- MOVE HERE ... -->` comment. Do not alter any attribute, binding, or `x:Name` inside the moved content. The `TabItem` wrapper elements themselves (and the two now-empty `TabControl`/`Border` shells and `GridSplitter`s) are deleted, not moved.

- [ ] **Step 3: Build to catch structural XAML errors**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: Build succeeds. If it fails with "The name '...' does not exist in the current context" for a control referenced in `MainWindow.xaml.cs` (e.g. `TagCatalogDataGrid`, `ElementLibraryListBox`, `SceneTabs`, `PreviewWebView`, `PreviewSurfaceBorder`, `PreviewPlaceholder`, `ButtonContextTab`), it means that control's `x:Name` was accidentally dropped during the move — re-check the moved block against the original for that name and restore it exactly.

- [ ] **Step 4: Manually launch and visually confirm the shell renders**

Run: `dotnet run --project src/ScadaBuilderV2.App`
Expected: The window opens with a left docked pane showing 3 tabs (Outil/Projet/Catalogue Tags), a center canvas area unchanged from before, and a right docked pane showing 4 tabs (Page/Element/Propriete/Librairie). Dragging a tab's title bar out of its pane detaches it into a floating window; dragging it back re-docks it. Close the app without saving layout changes (persistence is Task 5).

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 310 tests total, 306 still pass — this task only restructures WPF shell XAML, which is not covered by the (App-project-excluding) test suite, so the count must not change from Task 2's end state.

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml
git commit -m "feat: replace fixed Grid side panels with AvalonDock DockingManager"
```

---

### Task 4: Rewire tab-activation code, close→hide behavior, and the "Fenêtres" reopen menu

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs:214`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs:3779`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs:3843`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs:118-149` (constructor — add anchorable-closing wiring)
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml` (add a "Fenêtres" top-level menu)

**Interfaces:**
- Consumes: `ToolAnchorable`, `ProjectAnchorable`, `TagCatalogAnchorable`, `PageAnchorable`, `ElementAnchorable`, `PropertiesAnchorable`, `LibraryAnchorable` (all `Xceed.Wpf.AvalonDock.Layout.LayoutAnchorable`, produced by Task 3).
- Produces: `private void OnAnchorableClosing(object? sender, System.ComponentModel.CancelEventArgs e)` — wired to every anchorable's `Closing` event; consumed nowhere else (self-contained behavior), but its existence is required by Task 6's manual verification checklist ("panel closed accidentally stays reachable").

- [ ] **Step 1: Replace the two `RightContextTabs.SelectedItem` call sites**

At `MainWindow.xaml.cs:214` and `MainWindow.xaml.cs:3779`, replace:

```csharp
RightContextTabs.SelectedItem = PageContextTab;
```

with:

```csharp
PageAnchorable.IsActive = true;
```

At `MainWindow.xaml.cs:3843`, replace:

```csharp
RightContextTabs.SelectedItem = PropertiesContextTab;
```

with:

```csharp
PropertiesAnchorable.IsActive = true;
```

- [ ] **Step 2: Build to confirm no remaining references to removed names**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: Build succeeds. If it fails referencing `RightContextTabs`, `PageContextTab`, or `PropertiesContextTab`, search `MainWindow.xaml.cs` for any remaining usage beyond the three sites above and apply the same `XxxAnchorable.IsActive = true;` substitution.

- [ ] **Step 3: Add close→hide behavior for all 7 anchorables**

In `MainWindow.xaml.cs`, inside the constructor (after the existing wiring block at lines 138-139, i.e. right after `Closing += OnMainWindowClosing;`), add:

```csharp
ToolAnchorable.Closing += OnAnchorableClosing;
ProjectAnchorable.Closing += OnAnchorableClosing;
TagCatalogAnchorable.Closing += OnAnchorableClosing;
PageAnchorable.Closing += OnAnchorableClosing;
ElementAnchorable.Closing += OnAnchorableClosing;
PropertiesAnchorable.Closing += OnAnchorableClosing;
LibraryAnchorable.Closing += OnAnchorableClosing;
```

Then add the handler as a new private method (place it near `OnMainWindowClosing` at line 494 for locality):

```csharp
/// <summary>
/// Prevents AvalonDock's default "close" behavior (which detaches the anchorable from
/// the layout permanently) from firing when the user clicks a side panel's close button.
/// Instead the panel is hidden and remains reachable from the "Fenêtres" menu.
/// </summary>
private void OnAnchorableClosing(object? sender, System.ComponentModel.CancelEventArgs e)
{
    if (sender is not Xceed.Wpf.AvalonDock.Layout.LayoutAnchorable anchorable)
    {
        return;
    }

    e.Cancel = true;
    anchorable.Hide();
}
```

- [ ] **Step 4: Add the "Fenêtres" menu**

In `MainWindow.xaml`, locate the existing top menu/ribbon bar (the `Border DockPanel.Dock="Top"` region at lines 239-311 described in exploration) and add a `Menu` with one `MenuItem` per anchorable, each with a `Click` handler that shows the corresponding anchorable. Add this XAML fragment as a sibling near the existing top command surface (exact placement within the top `DockPanel`/`Grid` is at the implementer's discretion as long as it is visible in the top bar):

```xml
<Menu>
    <MenuItem Header="Fenetres">
        <MenuItem Header="Outil" Click="OnShowToolAnchorableClick" />
        <MenuItem Header="Projet" Click="OnShowProjectAnchorableClick" />
        <MenuItem Header="Catalogue Tags" Click="OnShowTagCatalogAnchorableClick" />
        <MenuItem Header="Page" Click="OnShowPageAnchorableClick" />
        <MenuItem Header="Element" Click="OnShowElementAnchorableClick" />
        <MenuItem Header="Propriete" Click="OnShowPropertiesAnchorableClick" />
        <MenuItem Header="Librairie" Click="OnShowLibraryAnchorableClick" />
    </MenuItem>
</Menu>
```

In `MainWindow.xaml.cs`, add the 7 click handlers near `OnAnchorableClosing`:

```csharp
private void OnShowToolAnchorableClick(object sender, RoutedEventArgs e) => ToolAnchorable.Show();
private void OnShowProjectAnchorableClick(object sender, RoutedEventArgs e) => ProjectAnchorable.Show();
private void OnShowTagCatalogAnchorableClick(object sender, RoutedEventArgs e) => TagCatalogAnchorable.Show();
private void OnShowPageAnchorableClick(object sender, RoutedEventArgs e) => PageAnchorable.Show();
private void OnShowElementAnchorableClick(object sender, RoutedEventArgs e) => ElementAnchorable.Show();
private void OnShowPropertiesAnchorableClick(object sender, RoutedEventArgs e) => PropertiesAnchorable.Show();
private void OnShowLibraryAnchorableClick(object sender, RoutedEventArgs e) => LibraryAnchorable.Show();
```

- [ ] **Step 5: Build and manually verify close→hide→reopen**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: Build succeeds.

Run: `dotnet run --project src/ScadaBuilderV2.App`
Expected: Clicking the close ("X") button on the `Catalogue Tags` panel hides it (it disappears from its pane, the pane still shows the remaining tabs). Opening the `Fenetres` menu and clicking `Catalogue Tags` brings the panel back in its original location. Repeat for at least one other anchorable (e.g. `Propriete`) to confirm the pattern generalizes.

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 310 tests total, 306 pass (no App-layer tests exist for this behavior per the Global Constraints — manual verification is the coverage for this task).

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml src/ScadaBuilderV2.App/MainWindow.xaml.cs
git commit -m "feat: rewire tab activation to AvalonDock anchorables, add close-to-hide and Fenetres reopen menu"
```

---

### Task 5: Persist and restore the dock layout across sessions

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs:47` (field declarations area, alongside `_libraryRegistryStore`)
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs:118-149` (constructor)
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs:494-515` (`OnMainWindowClosing`)
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml` (the "Fenetres" menu added in Task 4, Step 4 — add a "Reinitialiser la disposition" entry)

**Interfaces:**
- Consumes: `ScadaBuilderV2.Infrastructure.Shell.DockLayoutStore` (Task 2), `MainDockingManager` (Task 3).
- Produces: `private async Task LoadDockLayoutAsync()`, `private async Task SaveDockLayoutAsync()`, `private void CaptureDefaultLayout()`, `private void OnResetLayoutClick(object sender, RoutedEventArgs e)` — all private to `MainWindow`, not consumed elsewhere.

- [ ] **Step 1: Add the store field and a field to hold the XAML-default layout snapshot**

Near `private readonly LibraryRegistryStore _libraryRegistryStore = new();` (line 47), add:

```csharp
private readonly ScadaBuilderV2.Infrastructure.Shell.DockLayoutStore _dockLayoutStore = new();
private string? _defaultLayoutXml;
```

- [ ] **Step 2: Capture the XAML-default layout before loading any saved layout, then wire `Loaded`**

In the constructor, immediately after `InitializeComponent();` (line 120), add:

```csharp
CaptureDefaultLayout();
```

After the existing `Closing += OnMainWindowClosing;` line (139), add:

```csharp
Loaded += async (_, _) => await LoadDockLayoutAsync();
```

Add the capture method near `OnMainWindowClosing`:

```csharp
/// <summary>
/// Snapshots the AvalonDock layout as defined in XAML, before any saved layout is
/// restored, so "Reinitialiser la disposition" has a default to return to.
/// </summary>
private void CaptureDefaultLayout()
{
    var serializer = new Xceed.Wpf.AvalonDock.Layout.Serialization.XmlLayoutSerializer(MainDockingManager);
    using var writer = new System.IO.StringWriter();
    serializer.Serialize(writer);
    _defaultLayoutXml = writer.ToString();
}
```

- [ ] **Step 3: Implement `LoadDockLayoutAsync`**

Add this method near `OnMainWindowClosing`:

```csharp
/// <summary>
/// Restores the previously saved AvalonDock layout, if any. Falls back silently to the
/// XAML-defined default layout when no saved layout exists or it fails to parse.
/// </summary>
private async Task LoadDockLayoutAsync()
{
    var path = _dockLayoutStore.GetDefaultLayoutPath();
    var layoutXml = await _dockLayoutStore.ReadLayoutXmlAsync(path);
    if (string.IsNullOrEmpty(layoutXml))
    {
        return;
    }

    try
    {
        var serializer = new Xceed.Wpf.AvalonDock.Layout.Serialization.XmlLayoutSerializer(MainDockingManager);
        using var reader = new System.IO.StringReader(layoutXml);
        serializer.Deserialize(reader);
    }
    catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException or NullReferenceException)
    {
        System.Diagnostics.Debug.WriteLine($"Failed to restore dock layout, keeping default: {ex.Message}");
    }
}
```

- [ ] **Step 4: Implement `SaveDockLayoutAsync` and call it from the confirmed-close path**

Add this method next to `LoadDockLayoutAsync`:

```csharp
/// <summary>
/// Serializes the current AvalonDock layout and persists it so it can be restored on
/// the next launch. Failures are logged and swallowed so a layout-save problem never
/// blocks the window from closing.
/// </summary>
private async Task SaveDockLayoutAsync()
{
    try
    {
        var serializer = new Xceed.Wpf.AvalonDock.Layout.Serialization.XmlLayoutSerializer(MainDockingManager);
        using var writer = new System.IO.StringWriter();
        serializer.Serialize(writer);
        await _dockLayoutStore.WriteLayoutXmlAsync(_dockLayoutStore.GetDefaultLayoutPath(), writer.ToString());
    }
    catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException or IOException or UnauthorizedAccessException)
    {
        System.Diagnostics.Debug.WriteLine($"Failed to save dock layout: {ex.Message}");
    }
}
```

In `OnMainWindowClosing` (lines 494-515), find the line that sets `_isClosingConfirmed = true;` (immediately before the re-entrant `Close();` call) and call the save immediately before it:

```csharp
await SaveDockLayoutAsync();
_isClosingConfirmed = true;
Close();
```

- [ ] **Step 5: Add the "Reinitialiser la disposition" command to the "Fenetres" menu**

In `MainWindow.xaml`, inside the `Fenetres` `MenuItem` added in Task 4 Step 4, add a separator and a new item after the 7 panel entries:

```xml
<MenuItem Header="Fenetres">
    <MenuItem Header="Outil" Click="OnShowToolAnchorableClick" />
    <MenuItem Header="Projet" Click="OnShowProjectAnchorableClick" />
    <MenuItem Header="Catalogue Tags" Click="OnShowTagCatalogAnchorableClick" />
    <MenuItem Header="Page" Click="OnShowPageAnchorableClick" />
    <MenuItem Header="Element" Click="OnShowElementAnchorableClick" />
    <MenuItem Header="Propriete" Click="OnShowPropertiesAnchorableClick" />
    <MenuItem Header="Librairie" Click="OnShowLibraryAnchorableClick" />
    <Separator />
    <MenuItem Header="Reinitialiser la disposition" Click="OnResetLayoutClick" />
</MenuItem>
```

Add the handler near `OnAnchorableClosing`:

```csharp
/// <summary>
/// Restores the AvalonDock layout captured from XAML at startup, discarding any
/// manual rearrangement made during the current or previous sessions.
/// </summary>
private void OnResetLayoutClick(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrEmpty(_defaultLayoutXml))
    {
        return;
    }

    var serializer = new Xceed.Wpf.AvalonDock.Layout.Serialization.XmlLayoutSerializer(MainDockingManager);
    using var reader = new System.IO.StringReader(_defaultLayoutXml);
    serializer.Deserialize(reader);
}
```

- [ ] **Step 6: Build**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: Build succeeds.

- [ ] **Step 7: Manually verify persistence round-trip**

Run: `dotnet run --project src/ScadaBuilderV2.App`. Drag the `Propriete` panel out into a floating window, resize the left pane wider, then close the app normally (confirming any dirty-tab prompts if they appear). Re-run `dotnet run --project src/ScadaBuilderV2.App`.
Expected: The `Propriete` panel reopens as a floating window in the same position, and the left pane keeps its resized width. Confirm the file exists: check `%AppData%\ScadaBuilderV2\dock-layout.xml` was created/updated (its `LastWriteTime` should match the moment the app closed).

- [ ] **Step 8: Manually verify corrupt-file fallback**

With the app closed, open `%AppData%\ScadaBuilderV2\dock-layout.xml` in a text editor and replace its contents with `not valid xml`. Run `dotnet run --project src/ScadaBuilderV2.App`.
Expected: The app starts normally with the default layout (no crash, no error dialog) — `LoadDockLayoutAsync`'s catch block absorbs the parse failure.

- [ ] **Step 9: Manually verify "Reinitialiser la disposition"**

Run: `dotnet run --project src/ScadaBuilderV2.App`. Drag the `Propriete` panel to a floating window and resize the left pane. Click `Fenetres > Reinitialiser la disposition`.
Expected: All panels return immediately to their original docked positions and sizes as defined in XAML, without needing to restart the app.

- [ ] **Step 10: Run the full test suite**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 310 tests total, 306 pass (same 4 pre-existing failures).

- [ ] **Step 11: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml src/ScadaBuilderV2.App/MainWindow.xaml.cs
git commit -m "feat: persist, restore, and reset AvalonDock layout via DockLayoutStore"
```

---

### Task 6: Full manual verification pass and known-gaps documentation update

**Files:**
- Modify: `docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md` (append a new dated changelog row + implemented-area bullet)
- Modify: `docs/06_ui_ux/UI_ARCHITECTURE_V2.md` (update "Shell Surfaces" section to describe the docking model)

**Interfaces:**
- None (documentation + verification only).

- [ ] **Step 1: Run the full manual verification checklist**

With `dotnet run --project src/ScadaBuilderV2.App`, confirm all of the following in one session:

1. Drag every one of the 7 side panels (`Outil`, `Projet`, `Catalogue Tags`, `Page`, `Element`, `Propriete`, `Librairie`) out to a floating window, then re-dock each one back to its original pane.
2. Close every one of the 7 panels via their "X" button, then reopen each one via the `Fenetres` menu.
3. Resize the left and right anchorable panes; confirm the center canvas area continues to fill the remaining space exactly as it did before this change (no clipping, no layout shift in `PreviewWebView`).
4. Open a legacy page and select an Element+ object; confirm the `Propriete` panel still auto-activates (`PropertiesAnchorable.IsActive = true` from Task 4 Step 1) exactly as `RightContextTabs.SelectedItem = PropertiesContextTab` did before.
5. Restart the app twice in a row; confirm the dock layout from the end of the previous session is restored both times.
6. With one panel floated in a second monitor position (if available) or simply floated on the primary monitor, restart the app and confirm the floating position is restored.

- [ ] **Step 2: Update `IMPLEMENTED_FEATURES_V2.md`**

Add a new row to the changelog table (top of the table, following the existing newest-first ordering) and a new numbered bullet under "Implemented Areas":

```markdown
| 2026-07-05 | `V2.1.4.0000` | `PENDING` | Implementation du docking AvalonDock pour les panneaux lateraux avec persistance de disposition. |
```

```markdown
53. The WPF shell's left and right side panels (`Outil`, `Projet`, `Catalogue Tags`, `Page`, `Element`, `Propriete`, `Librairie`) are AvalonDock anchorable panes: draggable, floatable, closable/reopenable via a `Fenetres` menu, and persisted across sessions to `%AppData%\ScadaBuilderV2\dock-layout.xml`. The central canvas area (`SceneTabs`, `PreviewWebView`) is unchanged and remains a single non-closable document region.
```

- [ ] **Step 3: Update `UI_ARCHITECTURE_V2.md`**

In the "Shell Surfaces" section, replace:

```markdown
2. Left tool/project panel.
```
```markdown
4. Right property/context panel.
```

with:

```markdown
2. Left tool/project panel (AvalonDock anchorable panes: `Outil`, `Projet`, `Catalogue Tags`; draggable, floatable, closable/reopenable, layout persisted per user).
```
```markdown
4. Right property/context panel (AvalonDock anchorable panes: `Page`, `Element`, `Propriete`, `Librairie`; same docking behavior as the left panel).
```

Add a changelog row at the top of that document's history table:

```markdown
| 2026-07-05 | `V2.1.4.0000` | `PENDING` | Description du modele de docking AvalonDock pour les panneaux lateraux. |
```

- [ ] **Step 4: Run documentation validation**

Run: `powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1`
Expected: Passes with no errors.

- [ ] **Step 5: Run the full test suite one final time**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 310 tests total, 306 pass (same 4 pre-existing failures).

- [ ] **Step 6: Commit**

```bash
git add docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md docs/06_ui_ux/UI_ARCHITECTURE_V2.md
git commit -m "docs: record AvalonDock side-panel docking as implemented"
```
