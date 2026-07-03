# Studio Element+ Tools Ribbon Button Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a working "Studio E+" button to the Tools ribbon tab's Configuration group, removing the equivalent entry from the side tool palette where it doesn't semantically belong.

**Architecture:** `RibbonCommandCatalog` (Application layer) is the single source of truth for both the top ribbon tabs and the side tool palette; the WPF shell (`MainWindow.xaml.cs`) already dispatches `tool.element-studio` to the existing Element Studio launch pipeline regardless of which command surface raised it. This plan only moves the command's catalog entry and adds its missing icon resource — no launch logic changes.

**Tech Stack:** C# / .NET 8, WPF (XAML resource dictionaries), MSTest.

## Global Constraints

- Do not change `OpenElementStudioFromToolPaletteAsync`, `TryLaunchElementStudioAsync`, or any Element Studio launch/log logic — spec marks this out of scope.
- Do not duplicate `tool.element-studio` in both the side palette and the Tools ribbon tab — spec requires exactly one location (Tools tab).
- Icon resource must follow the existing `Icons.xaml` style: a `DrawingImage` containing a `DrawingGroup` of `GeometryDrawing` elements using the shared `Icon.OutlinePen`, geometry drawn on the same ~24x24 grid as sibling icons (see `Icon.Tool.Settings`, `Icon.Tool.Zoom`).
- Every ribbon command's `IconKey` must start with `"Icon."` (enforced by `DefaultCatalogRequiresSemanticIconKeys`) — already satisfied by `Icon.Tool.ElementStudio`, do not rename it.
- Command ids must stay unique across the whole catalog (enforced by `DefaultCatalogUsesStableUniqueCommandIds`) — reuse the existing id `tool.element-studio`, do not introduce a second id.

---

## Before You Start

The repository is currently on branch `master` with one pre-existing, unrelated uncommitted change:

```
modified:   projects/AMR_REF_SCADA_V2/scenes/win00008.scene.json
```

Do not touch or commit that file — it is not part of this work. Confirm with the user whether to work directly on `master` or create a feature branch before starting Task 1; do not assume.

---

### Task 1: Add the `Icon.Tool.ElementStudio` icon resource

**Files:**
- Modify: `src/ScadaBuilderV2.App/Resources/Icons.xaml:310` (insert new `DrawingImage` immediately after the closing `</DrawingImage>` of `Icon.Tool.Settings`, before `Icon.Field.Numeric`)

**Interfaces:**
- Consumes: shared brush `{StaticResource Icon.OutlinePen}` (already defined earlier in `Icons.xaml`, used by every sibling icon).
- Produces: resource key `Icon.Tool.ElementStudio`, a `DrawingImage`, resolvable via `TryFindResource("Icon.Tool.ElementStudio")` — this is the exact key `RibbonCommandCatalog` already references at `src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs:143`.

There is no dedicated unit test for individual icon glyphs in this codebase; correctness is verified by the XAML resource dictionary compiling successfully (`dotnet build` fails on malformed XAML/BAML).

- [ ] **Step 1: Read the current icon block to confirm the exact insertion point**

Read `src/ScadaBuilderV2.App/Resources/Icons.xaml` around line 303-312 and confirm it still reads:

```xml
    <DrawingImage x:Key="Icon.Tool.Settings">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M4,7 L20,7 M4,12 L20,12 M4,17 L20,17"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M8,5 L8,9 M15,10 L15,14 M11,15 L11,19"/>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>

    <DrawingImage x:Key="Icon.Field.Numeric">
```

If the surrounding content differs (file was edited since this plan was written), locate `x:Key="Icon.Tool.Settings"` and `x:Key="Icon.Field.Numeric"` directly and insert between their two `<DrawingImage>` blocks instead.

- [ ] **Step 2: Insert the new icon block**

Insert this block between the closing `</DrawingImage>` of `Icon.Tool.Settings` and the opening `<DrawingImage x:Key="Icon.Field.Numeric">`:

```xml

    <DrawingImage x:Key="Icon.Tool.ElementStudio">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M5,9 L5,19 L19,19 L19,9 L14,9 C14,6.5 10,6.5 10,9 L5,9 Z"/>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>
```

This draws a module/block outline (a square with a rounded tab bump on its top edge, evoking a reusable puzzle-piece component), consistent with the line-only, `Icon.OutlinePen`-based style of every sibling `Icon.Tool.*` entry.

- [ ] **Step 3: Build to verify the XAML is well-formed**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: `Build succeeded.` with no XAML/BAML compilation errors.

- [ ] **Step 4: Commit**

```bash
git add src/ScadaBuilderV2.App/Resources/Icons.xaml
git commit -m "feat: add Icon.Tool.ElementStudio icon resource"
```

---

### Task 2: Update ribbon catalog tests to expect the moved command (failing first)

**Files:**
- Modify: `tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs:126-149` (the `ToolPaletteUsesSemanticCommandCatalog` test)
- Modify: `tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs` (add a new test method for the Tools tab)

**Interfaces:**
- Consumes: `RibbonCommandCatalog.CreateToolPalette()` returning `IReadOnlyList<RibbonCommandDefinition>`; `RibbonCommandCatalog.CreateDefault()` returning `IReadOnlyDictionary<string, IReadOnlyList<RibbonGroupDefinition>>`; `RibbonGroupDefinition(string Label, IReadOnlyList<RibbonCommandDefinition> Commands)`; `RibbonCommandDefinition(string Id, string Label, string IconKey, string ToolTip, bool IsEnabled, string? DisabledReason = null)` — all defined in `src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs`.
- Produces: the two updated/added test methods that Task 3's implementation must satisfy.

- [ ] **Step 1: Update `ToolPaletteUsesSemanticCommandCatalog` to drop `tool.element-studio` from the expected palette ids**

This test's expected list at lines 133-143 currently reads:

```csharp
        CollectionAssert.AreEqual(
            new[]
            {
                "tool.select",
                "tool.move",
                "tool.text",
                "tool.image",
                "tool.group",
                "tool.zoom"
            },
            ids);
```

It already omits `tool.element-studio` — leave this assertion as-is (it already encodes the target end state; it currently fails only because `CreateToolPalette()` still returns the extra id). No edit needed for this specific block.

- [ ] **Step 2: Add a new test verifying the Tools tab hosts the command**

Add this test method to `RibbonCommandCatalogTests`, after `ToolPaletteUsesSemanticCommandCatalog`:

```csharp
    [TestMethod]
    public void ToolsTabExposesElementStudioCommand()
    {
        var tabs = RibbonCommandCatalog.CreateDefault();
        var toolsCommands = tabs["Tools"].SelectMany(group => group.Commands).ToArray();
        var studioCommand = toolsCommands.SingleOrDefault(command => command.Id == "tool.element-studio");

        Assert.IsNotNull(studioCommand, "Tools tab should expose the tool.element-studio command.");
        Assert.IsTrue(studioCommand!.IsEnabled);
        Assert.IsNull(studioCommand.DisabledReason);
        Assert.AreEqual("Icon.Tool.ElementStudio", studioCommand.IconKey);

        var paletteIds = RibbonCommandCatalog.CreateToolPalette().Select(command => command.Id).ToArray();
        CollectionAssert.DoesNotContain(paletteIds, "tool.element-studio");
    }
```

- [ ] **Step 3: Run both tests to confirm they fail for the expected reason**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "Name=ToolPaletteUsesSemanticCommandCatalog|Name=ToolsTabExposesElementStudioCommand"`
Expected: both `FAIL`. `ToolPaletteUsesSemanticCommandCatalog` fails on the `CollectionAssert.AreEqual` (actual list still contains `tool.element-studio`). `ToolsTabExposesElementStudioCommand` fails on `Assert.IsNotNull` (command not found in the Tools tab yet).

- [ ] **Step 4: Commit**

```bash
git add tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs
git commit -m "test: expect tool.element-studio on the Tools ribbon tab, not the side palette"
```

---

### Task 3: Move `tool.element-studio` from the side palette to the Tools ribbon tab

**Files:**
- Modify: `src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs:77-87` (Tools tab definition inside `CreateDefault()`)
- Modify: `src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs:133-145` (`CreateToolPalette()`)

**Interfaces:**
- Consumes: `Enabled(string id, string label, string iconKey, string toolTip)` and `Disabled(string id, string label, string iconKey, string disabledReason)` private helpers already defined at the bottom of `RibbonCommandCatalog.cs`; `Group(string label, params RibbonCommandDefinition[] commands)` helper.
- Produces: `RibbonCommandCatalog.CreateDefault()["Tools"]` now includes an enabled `tool.element-studio` entry in the "Configuration" group; `RibbonCommandCatalog.CreateToolPalette()` no longer includes it. `MainWindow.xaml.cs`'s `ExecuteRibbonCommand` (case `"tool.element-studio"` at line 5846) and `InitializeToolPaletteCommands()`/`SetActiveRibbon()` (which render whichever list the catalog returns) require no changes — they already iterate whatever `RibbonCommandCatalog` returns.

- [ ] **Step 1: Move the command definition**

In `src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs`, change the `"Tools"` tab's `Group("Configuration", ...)` from:

```csharp
                Group("Configuration",
                    Disabled("tool.settings", "Configurer", "Icon.Tool.Settings", "Configurer les outils a venir"))
```

to:

```csharp
                Group("Configuration",
                    Disabled("tool.settings", "Configurer", "Icon.Tool.Settings", "Configurer les outils a venir"),
                    Enabled("tool.element-studio", "Studio E+", "Icon.Tool.ElementStudio", "Ouvrir Studio Element+ (editeur de composants Element+)"))
```

Then remove the same entry from `CreateToolPalette()`, changing:

```csharp
            Disabled("tool.zoom", "Zoom", "Icon.Tool.Zoom", "Zoom outille a venir"),
            Enabled("tool.element-studio", "Studio E+", "Icon.Tool.ElementStudio", "Ouvrir Studio Element+ (editeur de composants Element+)")
        ];
```

to:

```csharp
            Disabled("tool.zoom", "Zoom", "Icon.Tool.Zoom", "Zoom outille a venir")
        ];
```

- [ ] **Step 2: Run the two tests from Task 2 to confirm they now pass**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "Name=ToolPaletteUsesSemanticCommandCatalog|Name=ToolsTabExposesElementStudioCommand"`
Expected: both `PASS`.

- [ ] **Step 3: Run the full test suite to check for regressions**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: all tests pass, including `DefaultCatalogUsesStableUniqueCommandIds`, `DefaultCatalogRequiresSemanticIconKeys`, `DisabledCommandsExposeReason`, and `SelectionGroupCommandsAreExecutableFromRibbonCatalog`.

- [ ] **Step 4: Commit**

```bash
git add src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs
git commit -m "feat: move Studio Element+ launch command to the Tools ribbon tab"
```

---

## Self-Review Notes

- **Spec coverage:** icon resource (Task 1), catalog move + test updates (Tasks 2-3), no launch-logic changes (explicitly called out as a constraint, verified by leaving `MainWindow.xaml.cs` untouched), no duplication between palette and ribbon (asserted in `ToolsTabExposesElementStudioCommand`'s palette check and unchanged `ToolPaletteUsesSemanticCommandCatalog` expectation) are all covered.
- **Placeholder scan:** none found — every step has literal code and exact commands.
- **Type consistency:** `RibbonCommandDefinition`, `RibbonGroupDefinition`, `Enabled`/`Disabled`/`Group` helper signatures are used identically to their existing definitions in `RibbonCommandCatalog.cs`; no new types introduced.
