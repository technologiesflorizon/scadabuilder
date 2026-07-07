# Element+ Mirror Context Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "Miroir" submenu (Verticale / Horizontale) to the Element+ right-click context menu that toggles a persisted flip state, renders correctly on canvas and in FT100 export, and shows a checkmark when active.

**Architecture:** Extends the existing single-selection context-menu pattern used by "Rotation" (`object.rotation`): two new booleans on `ScadaElementStyle`, a new `object.mirror` submenu built in `BuildContextMenuCommands`, two new dispatch cases in `ExecuteEditorCommandAsync` calling a new `ToggleModernElementMirror` mutator (mirrors `UpdateModernElementRotation`), a generic checkmark-rendering addition to the JS context-menu builder, transform composition in both JS render paths, and CSS composition in the FT100 exporter.

**Tech Stack:** C# / .NET 8 (WPF), inline JS embedded in `MainWindow.WebViewScript.cs` (WebView2 host), MSTest.

## Global Constraints

- Single selected Element+ object only — same guard as Rotation: `_selectedSceneObjectIds.Count == 1 && (_activeScene?.Elements.Any(e => e.Id == selected.Id) ?? false)` (`MainWindow.xaml.cs:3877`).
- French UI labels: "Miroir" (menu), "Verticale", "Horizontale" (children) — note the French adjectives are feminine agreeing with "Miroir" as a category label, matching existing sibling labels like "Rotation", "Redimensionner".
- No drag handle, no multi-selection, no normalization of combined flips into rotation — out of scope for this feature.
- Mutations must commit through `CommitModernElementProperties` so they participate in undo/redo, exactly like rotation.
- Preview/build/export must stay in parity (existing project guardrail) — the exporter's CSS output must match the canvas's JS transform.

---

## Existing Partial/Broken Code To Fix

`src/ScadaBuilderV2.App/MainWindow.xaml.cs` around lines 3896–3915 already has a **half-written, non-compiling** attempt at this feature:

```csharp
modernCommands.Add(new EditorCommandDescriptor(
    "object.rotation",
    "Rotation",
    "rotation",
    Children:
    [
        new EditorCommandDescriptor("object.rotation.0", "0°", "rotation"),
        new EditorCommandDescriptor("object.rotation.90", "90°", "rotation"),
        new EditorCommandDescriptor("object.rotation.180", "180°", "rotation"),
        new EditorCommandDescriptor("object.rotation.270", "270°", "rotation"),
        new EditorCommandDescriptor("object.rotation.custom", "Personnalisé...", "rotation"),
    ]),
    modernCommands.Add(new EditorCommandDescriptor(
        "object.Mirror", 
        "Miroir", 
        "mirror",
        Children: [
            new EditorCommandDescriptor("object.mirror.horizontal", "Horizontal", "mirror", IsChecked: currentStyle.FlipHorizontally ),
            new EditorCommandDescriptor("object.mirror.vertical", "Vertical", "mirror", IsChecked: currentStyle.FlipVertically ),    
        ]));
```

Bugs: the `object.rotation` descriptor's closing `));` is missing — it's replaced with `),` followed by a nested `modernCommands.Add(...)` call *as an argument expression*, which does not compile. `currentStyle` is never declared anywhere in this method. Task 2 below replaces this entire block with corrected code.

The domain model (`ScadaSceneModels.cs:196-209`) and `EditorCommandDescriptor.IsChecked` (`EditorCommandDescriptor.cs:19`) already have the fields this plan needs — confirmed present, no task required for those two.

---

### Task 1: Domain model test for flip fields

**Files:**
- Test: `tests/ScadaBuilderV2.Tests/ScadaSceneModelsTests.cs` (create if it does not exist; check first)

**Interfaces:**
- Consumes: `ScadaElementStyle` record (`src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs:196-209`), which already declares `bool FlipHorizontally = false` and `bool FlipVertically = false`.
- Produces: nothing new — this task only adds regression coverage for fields that already exist, so later tasks can rely on them without re-verifying.

- [ ] **Step 1: Check whether `ScadaSceneModelsTests.cs` exists**

Run: `Get-ChildItem "tests/ScadaBuilderV2.Tests/ScadaSceneModelsTests.cs"` (PowerShell) from the repo root. If it exists, read it first and add the test method below inside the existing class instead of creating a new file.

- [ ] **Step 2: Write the test**

If the file does not exist, create it with:

```csharp
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ScadaSceneModelsTests
{
    [TestMethod]
    public void ScadaElementStyleDefaultsToNotFlipped()
    {
        var style = ScadaElementStyle.DefaultText;

        Assert.IsFalse(style.FlipHorizontally);
        Assert.IsFalse(style.FlipVertically);
    }

    [TestMethod]
    public void ScadaElementStyleWithExpressionTogglesFlipIndependently()
    {
        var style = ScadaElementStyle.DefaultText;

        var flippedHorizontally = style with { FlipHorizontally = true };
        Assert.IsTrue(flippedHorizontally.FlipHorizontally);
        Assert.IsFalse(flippedHorizontally.FlipVertically);

        var flippedBoth = flippedHorizontally with { FlipVertically = true };
        Assert.IsTrue(flippedBoth.FlipHorizontally);
        Assert.IsTrue(flippedBoth.FlipVertically);
    }
}
```

If the file already exists, add both `[TestMethod]`s inside the existing `[TestClass]` body instead.

- [ ] **Step 3: Run tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaSceneModelsTests"`
Expected: PASS (fields already exist in the domain model, so this documents/locks in existing behavior — it should not fail).

- [ ] **Step 4: Commit**

```bash
git add tests/ScadaBuilderV2.Tests/ScadaSceneModelsTests.cs
git commit -m "test: lock in ScadaElementStyle flip field defaults"
```

---

### Task 2: Fix and complete the context menu descriptor (`object.mirror`)

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs:3896-3915`
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `EditorCommandDescriptor(Id, Label, Category, IsEnabled, DisabledReason, IconKey, IsChecked, Children)` (`src/ScadaBuilderV2.Application/Commands/EditorCommandDescriptor.cs:14-21`, `IsChecked` already present). `selected.Style` — the currently-selected `ScadaElement`'s style, already in scope in this method (the existing broken code incorrectly referenced a non-existent `currentStyle` variable; use `selected.Style` instead, matching how the rest of the method reads properties off `selected`).
- Produces: two new command ids that Task 3 must handle in the dispatch switch: `"object.mirror.horizontal"`, `"object.mirror.vertical"`.

- [ ] **Step 1: Write the failing test**

Add to `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs` (near `ContextMenuOffersRotationPresetsForSingleElementPlusSelection`, following the same `ReadMainWindowSource()` pattern):

```csharp
[TestMethod]
public void ContextMenuOffersMirrorTogglesForSingleElementPlusSelection()
{
    var source = ReadMainWindowSource();

    StringAssert.Contains(source, "\"object.mirror\"");
    StringAssert.Contains(source, "\"object.mirror.horizontal\"");
    StringAssert.Contains(source, "\"object.mirror.vertical\"");
    StringAssert.Contains(source, "case \"object.mirror.horizontal\":");
    StringAssert.Contains(source, "case \"object.mirror.vertical\":");

    // Scoped inside the same single-selection guard as object.rotation and object.order.
    var guardStart = source.IndexOf("if (_selectedSceneObjectIds.Count == 1 && (_activeScene?.Elements.Any(e => e.Id == selected.Id) ?? false))", StringComparison.Ordinal);
    Assert.IsTrue(guardStart >= 0, "Guard condition for single-element descriptor scope not found");
    var scopeEnd = source.IndexOf("return modernCommands;", guardStart, StringComparison.Ordinal);
    Assert.IsTrue(scopeEnd >= 0, "return modernCommands statement not found after guard");
    var guardedScope = source[guardStart..scopeEnd];

    StringAssert.Contains(guardedScope, "\"object.mirror\"",
        "object.mirror descriptor must be inside the same single-element guard as object.rotation");
}

[TestMethod]
public void MirrorMenuChildrenReflectCurrentFlipStateViaIsChecked()
{
    var source = ReadMainWindowSource();

    StringAssert.Contains(source, "IsChecked: selected.Style.FlipHorizontally");
    StringAssert.Contains(source, "IsChecked: selected.Style.FlipVertically");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ContextMenuOffersMirrorTogglesForSingleElementPlusSelection|FullyQualifiedName~MirrorMenuChildrenReflectCurrentFlipStateViaIsChecked"`
Expected: the whole project FAILS TO BUILD, because the existing broken block at `MainWindow.xaml.cs:3896-3915` does not compile. (This confirms the pre-existing bug; the next step fixes it.)

- [ ] **Step 3: Replace the broken block with corrected code**

Replace lines 3896–3915 of `src/ScadaBuilderV2.App/MainWindow.xaml.cs` (the `object.rotation` descriptor through the end of the broken `object.mirror` block) with:

```csharp
                modernCommands.Add(new EditorCommandDescriptor(
                    "object.rotation",
                    "Rotation",
                    "rotation",
                    Children:
                    [
                        new EditorCommandDescriptor("object.rotation.0", "0°", "rotation"),
                        new EditorCommandDescriptor("object.rotation.90", "90°", "rotation"),
                        new EditorCommandDescriptor("object.rotation.180", "180°", "rotation"),
                        new EditorCommandDescriptor("object.rotation.270", "270°", "rotation"),
                        new EditorCommandDescriptor("object.rotation.custom", "Personnalisé...", "rotation"),
                    ]));
                modernCommands.Add(new EditorCommandDescriptor(
                    "object.mirror",
                    "Miroir",
                    "mirror",
                    Children:
                    [
                        new EditorCommandDescriptor("object.mirror.horizontal", "Horizontale", "mirror",
                            IsChecked: selected.Style.FlipHorizontally),
                        new EditorCommandDescriptor("object.mirror.vertical", "Verticale", "mirror",
                            IsChecked: selected.Style.FlipVertically),
                    ]));
```

Note: labels are corrected from the broken draft's "Horizontal"/"Vertical" to the spec's "Horizontale"/"Verticale" (feminine agreement with "Miroir" as established in the approved design).

- [ ] **Step 4: Add placeholder dispatch cases so the build succeeds (real behavior comes in Task 3)**

In `ExecuteEditorCommandAsync`, immediately after the existing `case "object.rotation.270":` block (`MainWindow.xaml.cs:4070-4072`), add:

```csharp
            case "object.mirror.horizontal":
                ToggleModernElementMirror(message.Id, vertical: false);
                break;
            case "object.mirror.vertical":
                ToggleModernElementMirror(message.Id, vertical: true);
                break;
```

This references `ToggleModernElementMirror`, which does not exist yet — add a temporary stub directly below `UpdateModernElementRotation` (`MainWindow.xaml.cs:4954-4978`) so the build compiles:

```csharp
    private void ToggleModernElementMirror(string? id, bool vertical)
    {
    }
```

(Task 3 replaces this stub with the real implementation and its own test.)

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ContextMenuOffersMirrorTogglesForSingleElementPlusSelection|FullyQualifiedName~MirrorMenuChildrenReflectCurrentFlipStateViaIsChecked"`
Expected: PASS.

- [ ] **Step 6: Run the full rotation-menu regression test to confirm no collateral damage**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ContextMenuOffersRotationPresetsForSingleElementPlusSelection"`
Expected: PASS (unchanged rotation behavior).

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "fix: repair broken object.mirror context menu descriptor and wire stub dispatch"
```

---

### Task 3: Implement `ToggleModernElementMirror` mutation with undo/redo

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs` (replace the Task 2 stub, located directly below `UpdateModernElementRotation`, `MainWindow.xaml.cs:4954-4978`)
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `_activeScene.FindElementRecursive(string id)` returning `ScadaElement?`, `_selectedSceneObject?.Id`, `CommitModernElementProperties(ScadaElement current, ScadaElement updated)` (`MainWindow.xaml.cs:5640`) — same three members `UpdateModernElementRotation` already uses.
- Produces: `private void ToggleModernElementMirror(string? id, bool vertical)` — real implementation, replacing the Task 2 stub. Called by the two dispatch cases added in Task 2.

- [ ] **Step 1: Write the failing test**

Add to `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`:

```csharp
[TestMethod]
public void ToggleModernElementMirrorFlipsCorrectAxisAndCommitsThroughSharedPath()
{
    var source = ReadMainWindowSource();

    StringAssert.Contains(source, "private void ToggleModernElementMirror(string? id, bool vertical)");

    var methodStart = source.IndexOf("private void ToggleModernElementMirror(string? id, bool vertical)", StringComparison.Ordinal);
    Assert.IsTrue(methodStart >= 0);
    var methodEnd = source.IndexOf("\n    private ", methodStart + 1, StringComparison.Ordinal);
    Assert.IsTrue(methodEnd > methodStart, "Could not locate end of ToggleModernElementMirror method");
    var methodBody = source[methodStart..methodEnd];

    StringAssert.Contains(methodBody, "FindElementRecursive(targetId)");
    StringAssert.Contains(methodBody, "FlipVertical = !current.Style.FlipVertically");
    StringAssert.Contains(methodBody, "FlipHorizontal = !current.Style.FlipHorizontally");
    StringAssert.Contains(methodBody, "CommitModernElementProperties(current, updated);");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ToggleModernElementMirrorFlipsCorrectAxisAndCommitsThroughSharedPath"`
Expected: FAIL — the Task 2 stub method body is empty, so the `StringAssert.Contains` calls on `methodBody` fail.

- [ ] **Step 3: Replace the stub with the real implementation**

Replace the stub added in Task 2 with:

```csharp
    private void ToggleModernElementMirror(string? id, bool vertical)
    {
        var targetId = string.IsNullOrWhiteSpace(id)
            ? _selectedSceneObject?.Id
            : id;
        if (_activeScene is null || string.IsNullOrWhiteSpace(targetId))
        {
            return;
        }

        var current = _activeScene.FindElementRecursive(targetId);
        if (current is null)
        {
            return;
        }

        var updated = vertical
            ? current with { Style = current.Style with { FlipVertical = !current.Style.FlipVertically } }
            : current with { Style = current.Style with { FlipHorizontal = !current.Style.FlipHorizontally } };
        CommitModernElementProperties(current, updated);
    }
```

Note the deliberate naming distinction already established by the domain model: the stored fields are `FlipHorizontally`/`FlipVertically` (adverbs, matching `Rotation`'s style), but the `with` expression's named parameters must use the record's actual declared parameter names — confirm against `ScadaSceneModels.cs:196-209` before writing this step in the real codebase, since C# positional-record `with` syntax requires the exact declared parameter name. If the declared parameter names are `FlipHorizontally`/`FlipVertically` (not `FlipHorizontal`/`FlipVertical`), use those same names on both sides:

```csharp
        var updated = vertical
            ? current with { Style = current.Style with { FlipVertically = !current.Style.FlipVertically } }
            : current with { Style = current.Style with { FlipHorizontally = !current.Style.FlipHorizontally } };
```

Use this second form — it matches the field names actually declared in `ScadaSceneModels.cs:208-209`. Update the Step 1 test's `StringAssert.Contains` calls to match this exact form (`FlipVertically = !current.Style.FlipVertically`, `FlipHorizontally = !current.Style.FlipHorizontally`) before running Step 4.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ToggleModernElementMirrorFlipsCorrectAxisAndCommitsThroughSharedPath"`
Expected: PASS.

- [ ] **Step 5: Add an undo/redo regression test**

Check `tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs` for the existing rotation undo/redo test (search for `UpdateModernElementRotation` or `Rotation` in that file) and add an equivalent test for mirror, following its exact structure — construct a scene with one element, call `ToggleModernElementMirror` (or, if the existing rotation test drives it through the public command dispatch instead of calling the private method directly, follow that same entry point), assert `FlipHorizontally` toggles true then false across undo/redo via the same history-service API the rotation test uses.

- [ ] **Step 6: Run the new undo/redo test**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~EditorHistoryServiceTests"`
Expected: PASS, including the new mirror case alongside existing rotation cases.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs
git commit -m "feat: implement ToggleModernElementMirror with undo/redo support"
```

---

### Task 4: Render checkmark for checked context-menu items (JS)

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:757-771` (`createCommandButton`)
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `command.IsChecked` / `command.isChecked` (boolean, sent from C# `EditorCommandDescriptor.IsChecked` — JSON serialization casing depends on the existing message serializer already used for `IsEnabled`/`isEnabled`, hence checking both casings exactly like the existing `command.IsEnabled === false || command.isEnabled === false` check at line 762).
- Produces: a `checked` CSS class (or equivalent visual marker) on the rendered `<button>`, usable by any future checkable command, not mirror-specific.

- [ ] **Step 1: Write the failing test**

Add to `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`:

```csharp
[TestMethod]
public void ContextMenuButtonRendersCheckedStateFromIsCheckedFlag()
{
    var source = ReadMainWindowSource();

    var functionStart = source.IndexOf("function renderContextMenuCommands(commands)", StringComparison.Ordinal);
    Assert.IsTrue(functionStart >= 0);
    var createCommandNodeStart = source.IndexOf("const createCommandNode = command =>", functionStart, StringComparison.Ordinal);
    Assert.IsTrue(createCommandNodeStart > functionStart);
    var createCommandButtonBody = source[functionStart..createCommandNodeStart];

    StringAssert.Contains(createCommandButtonBody, "command.IsChecked === true || command.isChecked === true");
    StringAssert.Contains(createCommandButtonBody, "button.classList.add('checked')");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ContextMenuButtonRendersCheckedStateFromIsCheckedFlag"`
Expected: FAIL — no such code exists yet in `createCommandButton`.

- [ ] **Step 3: Implement the checkmark rendering**

In `MainWindow.WebViewScript.cs`, modify `createCommandButton` (lines 757-771) from:

```js
    const createCommandButton = command => {
      const button = document.createElement('button');
      button.type = 'button';
      button.dataset.commandId = command.Id;
      button.textContent = command.Label || command.Id;
      if (command.IsEnabled === false || command.isEnabled === false) {
        button.disabled = true;
        button.setAttribute('aria-disabled', 'true');
        const reason = command.DisabledReason || command.disabledReason || '';
        if (reason) {
          button.title = reason;
        }
      }
      return button;
    };
```

to:

```js
    const createCommandButton = command => {
      const button = document.createElement('button');
      button.type = 'button';
      button.dataset.commandId = command.Id;
      button.textContent = command.Label || command.Id;
      if (command.IsEnabled === false || command.isEnabled === false) {
        button.disabled = true;
        button.setAttribute('aria-disabled', 'true');
        const reason = command.DisabledReason || command.disabledReason || '';
        if (reason) {
          button.title = reason;
        }
      }
      if (command.IsChecked === true || command.isChecked === true) {
        button.classList.add('checked');
        button.setAttribute('aria-checked', 'true');
      }
      return button;
    };
```

- [ ] **Step 4: Add the `.checked` CSS rule**

Find the existing context-menu CSS block (search `MainWindow.WebViewScript.cs` for `.submenu-panel` or the style block containing menu button rules) and add a sibling rule, e.g.:

```css
.context-menu button.checked::before {
  content: '\2713 ';
}
```

Place it next to the other menu-button style rules already present in that same CSS block, matching the existing rule ordering/formatting.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ContextMenuButtonRendersCheckedStateFromIsCheckedFlag"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: render checkmark for checked context-menu commands"
```

---

### Task 5: Apply flip transform on canvas render and drag-preview paths (JS)

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:1864` (render), `:1944` (badge counter-rotation), `:2597` (drag-preview rotate), `:2601` (dragged badge counter-rotation)
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `style.FlipHorizontally`, `style.FlipVertically` (booleans arriving on the element's `Style` object sent from C#, same object `style.Rotation` already comes from).
- Produces: composed `transform` CSS strings on `wrapper.style.transform` that later tasks (Task 6, exporter) must produce equivalent output for.

- [ ] **Step 1: Write the failing test**

Add to `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`:

```csharp
[TestMethod]
public void WrapperTransformComposesRotationWithFlipScaleOnRenderAndDrag()
{
    var source = ReadMainWindowSource();

    // Render path
    StringAssert.Contains(
        source,
        "wrapper.style.transform = `rotate(${Number(style.Rotation ?? 0)}deg) scaleX(${style.FlipHorizontally ? -1 : 1}) scaleY(${style.FlipVertically ? -1 : 1})`;");

    // Drag-preview path
    StringAssert.Contains(
        source,
        "modernDrag.wrapper.style.transform = `rotate(${normalized}deg) scaleX(${modernDrag.flipHorizontally ? -1 : 1}) scaleY(${modernDrag.flipVertically ? -1 : 1})`;");
}

[TestMethod]
public void BadgeCounterRotatesAndCounterFlipsSoLabelTextStaysReadable()
{
    var source = ReadMainWindowSource();

    // Render path badge
    StringAssert.Contains(
        source,
        "badge.style.transform = `rotate(${-Number(style.Rotation ?? 0)}deg) scaleX(${style.FlipHorizontally ? -1 : 1}) scaleY(${style.FlipVertically ? -1 : 1})`;");

    // Drag-preview badge
    StringAssert.Contains(
        source,
        "draggedBadge.style.transform = `rotate(${-normalized}deg) scaleX(${modernDrag.flipHorizontally ? -1 : 1}) scaleY(${modernDrag.flipVertically ? -1 : 1})`;");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~WrapperTransformComposesRotationWithFlipScaleOnRenderAndDrag|FullyQualifiedName~BadgeCounterRotatesAndCounterFlipsSoLabelTextStaysReadable"`
Expected: FAIL — current code only has `rotate(...)`, no `scaleX`/`scaleY`.

- [ ] **Step 3: Implement the render-path transform (line 1864)**

Change:
```js
      wrapper.style.transform = `rotate(${Number(style.Rotation ?? 0)}deg)`;
```
to:
```js
      wrapper.style.transform = `rotate(${Number(style.Rotation ?? 0)}deg) scaleX(${style.FlipHorizontally ? -1 : 1}) scaleY(${style.FlipVertically ? -1 : 1})`;
```

- [ ] **Step 4: Implement the render-path badge transform (line 1944)**

Change:
```js
      badge.style.transform = `rotate(${-Number(style.Rotation ?? 0)}deg)`;
```
to:
```js
      badge.style.transform = `rotate(${-Number(style.Rotation ?? 0)}deg) scaleX(${style.FlipHorizontally ? -1 : 1}) scaleY(${style.FlipVertically ? -1 : 1})`;
```

The badge counter-rotates by negating the angle so its text stays upright regardless of the wrapper's rotation. It must also counter-apply the *same* (not negated) scale as the wrapper: a `scaleX(-1)` on the wrapper mirrors everything inside it, including the badge's own rotation transform's handedness, so re-applying the identical scale on the badge un-mirrors its text back to normal reading direction while the badge still tracks the wrapper's position.

- [ ] **Step 5: Locate where `modernDrag` is initialized and add flip fields to it**

Search `MainWindow.WebViewScript.cs` for where `modernDrag = {` (or equivalent object literal) is constructed on `pointerdown` for the rotate/resize/move gesture (near where `modernDrag.startRotation` is set, since that is read at line 2584). Add two fields there, reading from the wrapper's element data at drag-start time, e.g. alongside the existing `startRotation: ...` line:

```js
        flipHorizontally: Boolean(elementStyleForDrag?.FlipHorizontally),
        flipVertically: Boolean(elementStyleForDrag?.FlipVertically),
```

using whatever variable already holds the dragged element's style object in that pointerdown handler (match its existing name rather than inventing `elementStyleForDrag` if a suitable variable is already in scope there).

- [ ] **Step 6: Implement the drag-preview transform (line 2597)**

Change:
```js
        modernDrag.wrapper.style.transform = `rotate(${normalized}deg)`;
```
to:
```js
        modernDrag.wrapper.style.transform = `rotate(${normalized}deg) scaleX(${modernDrag.flipHorizontally ? -1 : 1}) scaleY(${modernDrag.flipVertically ? -1 : 1})`;
```

- [ ] **Step 7: Implement the dragged-badge transform (line 2601)**

Change:
```js
          draggedBadge.style.transform = `rotate(${-normalized}deg)`;
```
to:
```js
          draggedBadge.style.transform = `rotate(${-normalized}deg) scaleX(${modernDrag.flipHorizontally ? -1 : 1}) scaleY(${modernDrag.flipVertically ? -1 : 1})`;
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~WrapperTransformComposesRotationWithFlipScaleOnRenderAndDrag|FullyQualifiedName~BadgeCounterRotatesAndCounterFlipsSoLabelTextStaysReadable"`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: compose flip scale into wrapper and badge transforms on render and drag"
```

---

### Task 6: Compose flip into FT100 exporter CSS

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs:1183` (`BuildElementInlineStyle`)
- Test: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

**Interfaces:**
- Consumes: `ScadaElementStyle.FlipHorizontally`, `ScadaElementStyle.FlipVertically` (already-existing domain fields), `Format(double)` (existing private static helper in `Ft100SceneExporter.cs` used for `Format(style.Rotation)` at line 1183 — reuse it, do not add a new formatter, since scale factors are just the literal integers `1`/`-1`, not decimal-formatted values).
- Produces: extended `transform:` CSS string in the inline style emitted per element, which exported HTML pages consume directly (no downstream task depends on this beyond the test in this task).

- [ ] **Step 1: Write the failing test**

Add to `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`, following the existing style of the shape-export test near line 1481 (construct a `ScadaElement` with `Style = ScadaElementStyle.DefaultInput with { ... FlipHorizontally = true }`, run the same export pipeline that test already runs, and assert on the emitted HTML/CSS). If that existing test already drives a full export-and-read-output pipeline, mirror its setup; otherwise add a narrower unit test directly against `BuildElementInlineStyle` if it's accessible via `InternalsVisibleTo` (check the project file for `InternalsVisibleTo` covering the test project before assuming; if `BuildElementInlineStyle` is `private static`, use reflection consistent with any existing reflection-based tests in this file, or drive it through the public export entry point instead):

```csharp
[TestMethod]
public async Task ExportedElementStyleComposesFlipScaleWithRotationTransform()
{
    // Follow the exact scaffolding (temp directories, source HTML, exporter invocation)
    // already used by the test at Ft100SceneExporterTests.cs:~1460-1510 for shape export,
    // substituting Style = ScadaElementStyle.DefaultInput with
    // {
    //     Rotation = 17,
    //     FlipHorizontally = true,
    //     FlipVertically = false
    // }
    // on the element under test, then assert the exported page's inline style contains:
    //   transform:rotate(17deg) scaleX(-1) scaleY(1);
    // Read the exported HTML file the same way the existing shape-export test does
    // (locate its assertion section immediately following the arrangement code read above)
    // and adapt its file-read + StringAssert.Contains calls to this new expectation.
}
```

Replace the comment scaffolding above with real, compileable arrangement code copied from the existing shape-export test's setup and assertion sections before running this step — the comments describe what to copy, they are not a substitute for it.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ExportedElementStyleComposesFlipScaleWithRotationTransform"`
Expected: FAIL — current CSS output has only `transform:rotate(17deg);`, no `scaleX`/`scaleY`.

- [ ] **Step 3: Implement the CSS composition**

In `Ft100SceneExporter.cs`, change line 1183 from:
```csharp
        css.Append($"transform:rotate({Format(style.Rotation)}deg);");
```
to:
```csharp
        var scaleX = style.FlipHorizontally ? -1 : 1;
        var scaleY = style.FlipVertically ? -1 : 1;
        css.Append($"transform:rotate({Format(style.Rotation)}deg) scaleX({scaleX}) scaleY({scaleY});");
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ExportedElementStyleComposesFlipScaleWithRotationTransform"`
Expected: PASS.

- [ ] **Step 5: Run the full exporter test suite to confirm no collateral CSS-diff regressions**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~Ft100SceneExporterTests"`
Expected: PASS. If any pre-existing test asserts an exact `transform:rotate(...);` string without the new `scaleX`/`scaleY` suffix (i.e. it was written before this change and expects the old, shorter string), update that assertion's expected string to include ` scaleX(1) scaleY(1)` for elements that don't set flip (default false/false), since every element now emits both scale terms regardless of whether they're 1.

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "feat: compose flip scale into FT100 exported element transform CSS"
```

---

### Task 7: Full regression run and manual smoke test

**Files:** none (verification-only task)

**Interfaces:** none — this task consumes the complete feature from Tasks 1-6 and produces no new interface.

- [ ] **Step 1: Run the full test suite**

Run: `dotnet build ScadaBuilderV2.sln` then `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: build succeeds, all tests PASS (including the pre-existing rotation/order/resize context-menu tests, confirming Task 2's fix didn't regress sibling menu items).

- [ ] **Step 2: Manual smoke test in the running app**

Run: `dotnet run --project src/ScadaBuilderV2.App`
In the app: open `AMR_REF_SCADA_V2` (or any project), select a single Element+ object, right-click it, open "Miroir", click "Horizontale" — confirm the object visually mirrors left-right and the menu item shows a checkmark on next right-click. Click "Verticale" — confirm it also flips top-bottom, and both checkmarks now show. Press Ctrl+Z twice — confirm each flip undoes independently and the object returns to its original, unflipped state. Export the project to `.sb2` and open the exported HTML in a browser — confirm the exported element renders mirrored identically to the canvas.

- [ ] **Step 3: Report results**

If the manual smoke test surfaces any visual defect (e.g. resize/rotation handles positioned incorrectly on a flipped element, since handle positioning math in the JS was written assuming no flip), note it — it is out of scope for this plan to fix handle-position math for flipped elements, but it must be reported to the user as a known follow-up rather than silently accepted.
