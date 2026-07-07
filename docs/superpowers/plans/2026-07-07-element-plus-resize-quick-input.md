# Element+ Resize Quick-Input (Context Menu) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "Redimensionner" right-click context-menu entry on a single Element+ object that opens two floating numeric inputs — positioned at the element's current on-screen N and W handles — pre-filled with the dimension each handle actually controls right now (accounting for rotation), and commits a new width/height on Enter.

**Architecture:** Pure additive change on top of the existing WebView2-hosted canvas (`MainWindow.WebViewScript.cs`) and its context-menu builder (`MainWindow.xaml.cs`). No new C# message type or handler: the feature reuses the existing `updateSceneObjectGeometry` commit path (`postModernGeometry` → `UpdateModernElementGeometry`) that resize-by-drag already uses. The only new C# surface is one new leaf `EditorCommandDescriptor` in `BuildContextMenuCommands`. All quick-input DOM, handle-selection logic, and resize math are new JS functions in the same embedded-script file, following the existing `beginCustomRotationEntry` pattern.

**Tech Stack:** .NET 8 WPF host (`ScadaBuilderV2.App`), WebView2-hosted vanilla JS/CSS/HTML canvas (no framework), MSTest for source-text contract tests (`WebViewContextMenuScriptTests`).

## Global Constraints

- Editor-only artifacts (selection overlays, handles, drag rectangles, and — per this feature — the two floating resize inputs and any handle-lookup DOM state) must never leak into export geometry or scene JSON (`CLAUDE.md` non-negotiable guardrail).
- Preview/build/export must stay in parity by consuming one project model — this plan does not touch persistence, so no risk introduced, but no shortcuts that write geometry outside `Bounds` are allowed.
- Public APIs require XML docs; this plan only adds `private` C#/JS members, so no XML docs are required, but do not make anything `public`/`internal` without adding them.
- Scope for this iteration: single selected Element+ object only, no children (no groups/containers). The menu item must not appear otherwise.
- Follow existing code style exactly: 4-space indentation in both the C# and the embedded JS, matching the surrounding code in each file.

---

## Reference: exact math and pattern being reused

Read this once before starting; every task below cites back to it.

- `Bounds(X, Y, Width, Height)` (`src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs:183`) is always in unrotated local pixel coordinates. Rotation is a separate CSS `transform: rotate(deg)` applied on the wrapper (`MainWindow.WebViewScript.cs:1777`), so the 4 side-handle DOM elements (`data-handle="n"|"s"|"e"|"w"`) rotate visually with the element but keep their local identity.
- The existing side-handle drag-resize math (`MainWindow.WebViewScript.cs:2372-2430`) already proves the two things this feature needs:
  1. Each side handle changes exactly one axis, with the opposite edge fixed in local coordinates (`w`→width w/ `x = startX + startWidth - newWidth`; `e`→width w/ `x` unchanged; `n`→height w/ `y = startY + startHeight - newHeight`; `s`→height w/ `y` unchanged).
  2. When the element is rotated, that alone isn't enough to keep the *fixed* edge visually still on screen — because the CSS rotation pivots around the element's center, and changing width/height while holding one local edge fixed still moves the center, which moves everything under rotation. Lines 2411-2430 fix this by re-anchoring: rotate the pre-resize anchor point and the naive post-resize anchor point around their respective centers, then shift `x`/`y` by the screen-space difference so the anchor point (the edge opposite the dragged handle) stays exactly fixed on screen.
- This plan copies that exact formula into a new, input-driven (not delta-driven) function `applyAxisResize` (Task 2), because the existing drag code is delta-based (`dx`/`dy` from pointer movement) and already covered by its own tests — duplicating ~20 lines into a new function is safer than reshaping tested, working drag code to share a helper.
- The custom-rotation-input pattern (`ensureRotationInput`/`beginCustomRotationEntry`, `MainWindow.WebViewScript.cs:2221-2290`) is the template for the floating `<input>` lifecycle (create-once, position, prefill, validate live, commit on Enter, cancel on Escape, commit-on-blur). This feature needs two inputs instead of one, with independent commits and only closing when *both* lose focus.
- `postModernGeometry(id, before, after)` (`MainWindow.WebViewScript.cs:1000-1013`) already builds and posts the exact `updateSceneObjectGeometry` message shape that `UpdateModernElementGeometry` (`MainWindow.xaml.cs:4873`) consumes, including undo history, dirty-marking, and UI refresh. No C# change is needed for committing a resize — only JS needs to call this existing function.

---

### Task 1: Context-menu "Redimensionner" command descriptor

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs:3896-3908` (inside `BuildContextMenuCommands`)
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs` (new test method, inserted before the `ReadMainWindowSource` helper at line 1660)

**Interfaces:**
- Consumes: `ScadaElement.Kind` (enum `ScadaElementKind`), `ScadaElement.ChildElements` (`IReadOnlyList<ScadaElement>`) — both already defined in `src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs`. `EditorCommandDescriptor(string Id, string Label, string Category, ...)` from `src/ScadaBuilderV2.Application/Commands/EditorCommandDescriptor.cs`.
- Produces: context-menu command id `"object.resize"`, label `"Redimensionner"`. Later tasks (JS) consume this exact id string.

- [ ] **Step 1: Write the failing test**

Insert into `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`, immediately before the line `    private static string ReadMainWindowSource()` (currently line 1660):

```csharp
    [TestMethod]
    public void ContextMenuOffersResizeForSingleElementPlusSelectionWithoutChildren()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "\"object.resize\"");
        StringAssert.Contains(source, "\"Redimensionner\"");

        // object.resize must not have a case in ExecuteEditorCommandAsync; it is handled
        // entirely client-side in JS (opens the quick-input tool), like object.rotation.custom.
        Assert.IsFalse(
            source.Contains("case \"object.resize\":", StringComparison.Ordinal),
            "object.resize must NOT have a case in ExecuteEditorCommandAsync; it is handled client-side in JS.");

        // Scoped inside the same single-selection guard as object.rotation, and gated on
        // the element having no children (so groups/containers don't get the resize entry).
        var guardStart = source.IndexOf("if (_selectedSceneObjectIds.Count == 1 && (_activeScene?.Elements.Any(e => e.Id == selected.Id) ?? false))", StringComparison.Ordinal);
        Assert.IsTrue(guardStart >= 0, "Guard condition for single-element descriptor scope not found");
        var scopeEnd = source.IndexOf("return modernCommands;", guardStart, StringComparison.Ordinal);
        Assert.IsTrue(scopeEnd >= 0, "return modernCommands statement not found after guard");
        var guardedScope = source[guardStart..scopeEnd];

        StringAssert.Contains(guardedScope, "\"object.resize\"",
            "object.resize descriptor must be inside the same single-element guard as object.rotation");
        StringAssert.Contains(guardedScope, "selected.Kind != ScadaElementKind.Group && selected.ChildElements.Count == 0",
            "object.resize must be gated on the selection having no children (not a group/container)");
    }

```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=ContextMenuOffersResizeForSingleElementPlusSelectionWithoutChildren"`
Expected: FAIL (assertion on `"object.resize"` not found).

- [ ] **Step 3: Add the command descriptor**

In `src/ScadaBuilderV2.App/MainWindow.xaml.cs`, find (inside `BuildContextMenuCommands`, currently lines 3896-3908):

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
            }
```

Replace with:

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

                if (selected.Kind != ScadaElementKind.Group && selected.ChildElements.Count == 0)
                {
                    modernCommands.Add(new EditorCommandDescriptor(
                        "object.resize",
                        "Redimensionner",
                        "resize"));
                }
            }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=ContextMenuOffersResizeForSingleElementPlusSelectionWithoutChildren"`
Expected: PASS

- [ ] **Step 5: Run the full context-menu test suite to check for regressions**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~WebViewContextMenuScriptTests"`
Expected: PASS (all tests, including the pre-existing rotation ones)

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "Add Redimensionner context-menu command for single Element+ selections"
```

---

### Task 2: Pure JS resize math and handle-selection helpers

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:1145-1149` (right after `clampNearAxis`, right before `getSceneMoveWrapper`)
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: nothing new (uses only DOM APIs already used elsewhere in the file: `wrapper.querySelector`, `element.getBoundingClientRect()`).
- Produces:
  - `applyAxisResize(startGeometry, handle, newValue, rotationDeg)` → `{x, y, width, height}`. `startGeometry` is `{x, y, width, height}` (same shape as `readWrapperGeometry`'s return). `handle` is one of `'n'|'s'|'e'|'w'`. Task 3 calls this at commit time.
  - `pickVisualHandle(wrapper, preferenceOrder, axis, exclude)` → one of `'n'|'s'|'e'|'w'` or `null`. `preferenceOrder` is an array of handle names in tie-break priority order; `axis` is `'x'` or `'y'`; `exclude` (optional) is a handle name to skip. Task 3 calls this twice (once per axis) to resolve which handle is visually north/west.

- [ ] **Step 1: Write the failing tests**

Insert into `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`, immediately before `private static string ReadMainWindowSource()`:

```csharp
    [TestMethod]
    public void ApplyAxisResizeUsesOppositeEdgeAnchoredFormulaPerHandleAndCorrectsForRotation()
    {
        var source = ReadMainWindowSource();

        var start = source.IndexOf("function applyAxisResize(startGeometry, handle, newValue, rotationDeg)", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "applyAxisResize not found");
        var end = source.IndexOf("function pickVisualHandle(wrapper, preferenceOrder, axis, exclude)", start, StringComparison.Ordinal);
        Assert.IsTrue(end >= 0, "pickVisualHandle not found after applyAxisResize");
        var body = source[start..end];

        StringAssert.Contains(body, "geometry.x = startGeometry.x + startGeometry.width - newValue;",
            "handle 'w' must keep the right edge fixed (matches the drag-resize clampNearAxis formula)");
        StringAssert.Contains(body, "geometry.y = startGeometry.y + startGeometry.height - newValue;",
            "handle 'n' must keep the bottom edge fixed (matches the drag-resize clampNearAxis formula)");

        // Rotation-anchor correction: must match the same fx/fy + rotate-around-center
        // technique already proven by the drag-resize path (MainWindow.WebViewScript.cs
        // pointermove handler), so the edge opposite the edited handle stays visually
        // fixed on screen even when the element is rotated.
        StringAssert.Contains(body, "const fx = handle === 'w' ? 1 : handle === 'e' ? 0 : 0.5;");
        StringAssert.Contains(body, "const fy = handle === 'n' ? 1 : handle === 's' ? 0 : 0.5;");
        StringAssert.Contains(body, "geometry.x += screenAnchorOld.x - screenAnchorNew.x;");
        StringAssert.Contains(body, "geometry.y += screenAnchorOld.y - screenAnchorNew.y;");
    }

    [TestMethod]
    public void PickVisualHandlePrefersEarlierPreferenceOnNearTieAndSkipsExcluded()
    {
        var source = ReadMainWindowSource();

        var start = source.IndexOf("function pickVisualHandle(wrapper, preferenceOrder, axis, exclude)", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "pickVisualHandle not found");
        var end = source.IndexOf("function getSceneMoveWrapper(wrapper)", start, StringComparison.Ordinal);
        Assert.IsTrue(end >= 0, "getSceneMoveWrapper not found after pickVisualHandle");
        var body = source[start..end];

        StringAssert.Contains(body, "if (handle === exclude) return;",
            "must be able to skip a specific handle (used when north and west would otherwise resolve to the same handle)");
        StringAssert.Contains(body, "if (center < bestValue - 0.01)",
            "tie-break must keep the earlier (preferred) handle on a near-exact tie, not the later one");
    }

```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=ApplyAxisResizeUsesOppositeEdgeAnchoredFormulaPerHandleAndCorrectsForRotation|Name=PickVisualHandlePrefersEarlierPreferenceOnNearTieAndSkipsExcluded"`
Expected: FAIL (functions not found)

- [ ] **Step 3: Add the two helper functions**

In `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, find:

```csharp
  function clampNearAxis(startPos, startSize, delta) {
    const clampedDelta = Math.max(-startPos, Math.min(delta, startSize - 8));
    return { pos: startPos + clampedDelta, size: startSize - clampedDelta };
  }

  function getSceneMoveWrapper(wrapper) {
```

Replace with:

```csharp
  function clampNearAxis(startPos, startSize, delta) {
    const clampedDelta = Math.max(-startPos, Math.min(delta, startSize - 8));
    return { pos: startPos + clampedDelta, size: startSize - clampedDelta };
  }

  function applyAxisResize(startGeometry, handle, newValue, rotationDeg) {
    const geometry = { ...startGeometry };
    if (handle === 'w') {
      geometry.width = newValue;
      geometry.x = startGeometry.x + startGeometry.width - newValue;
    } else if (handle === 'e') {
      geometry.width = newValue;
    } else if (handle === 'n') {
      geometry.height = newValue;
      geometry.y = startGeometry.y + startGeometry.height - newValue;
    } else if (handle === 's') {
      geometry.height = newValue;
    }

    if (rotationDeg) {
      const rotationRad = rotationDeg * Math.PI / 180;
      const cosR = Math.cos(rotationRad);
      const sinR = Math.sin(rotationRad);
      const fx = handle === 'w' ? 1 : handle === 'e' ? 0 : 0.5;
      const fy = handle === 'n' ? 1 : handle === 's' ? 0 : 0.5;
      const oldCenterX = startGeometry.x + startGeometry.width / 2;
      const oldCenterY = startGeometry.y + startGeometry.height / 2;
      const newCenterX = geometry.x + geometry.width / 2;
      const newCenterY = geometry.y + geometry.height / 2;
      const anchorOldX = startGeometry.x + fx * startGeometry.width;
      const anchorOldY = startGeometry.y + fy * startGeometry.height;
      const anchorNewX = geometry.x + fx * geometry.width;
      const anchorNewY = geometry.y + fy * geometry.height;
      const rotateAroundCenter = (px, py, cx, cy) => ({
        x: cx + (px - cx) * cosR - (py - cy) * sinR,
        y: cy + (px - cx) * sinR + (py - cy) * cosR
      });
      const screenAnchorOld = rotateAroundCenter(anchorOldX, anchorOldY, oldCenterX, oldCenterY);
      const screenAnchorNew = rotateAroundCenter(anchorNewX, anchorNewY, newCenterX, newCenterY);
      geometry.x += screenAnchorOld.x - screenAnchorNew.x;
      geometry.y += screenAnchorOld.y - screenAnchorNew.y;
    }

    return geometry;
  }

  function pickVisualHandle(wrapper, preferenceOrder, axis, exclude) {
    let best = null;
    let bestValue = Infinity;
    preferenceOrder.forEach(handle => {
      if (handle === exclude) return;
      const el = wrapper.querySelector(`:scope > .scada-modern-handle[data-handle="${handle}"]`);
      if (!el) return;
      const rect = el.getBoundingClientRect();
      const center = axis === 'y' ? rect.top + rect.height / 2 : rect.left + rect.width / 2;
      if (center < bestValue - 0.01) {
        bestValue = center;
        best = handle;
      }
    });
    return best;
  }

  function getSceneMoveWrapper(wrapper) {
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=ApplyAxisResizeUsesOppositeEdgeAnchoredFormulaPerHandleAndCorrectsForRotation|Name=PickVisualHandlePrefersEarlierPreferenceOnNearTieAndSkipsExcluded"`
Expected: PASS

- [ ] **Step 5: Run the full context-menu test suite to check for regressions**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~WebViewContextMenuScriptTests"`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "Add applyAxisResize and pickVisualHandle helpers for the resize quick-input"
```

---

### Task 3: Floating resize inputs, `beginResizeEntry`, and menu wiring

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:231-240` (CSS, right after `#scada-rotation-input`)
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:2290-2292` (new functions, right after `beginCustomRotationEntry`, before the `pointermove` listener)
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:2675-2681` (menu click handler, add `object.resize` branch)
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `applyAxisResize`, `pickVisualHandle` (Task 2), `readWrapperGeometry`, `setWrapperGeometry`, `getWrapperRotation`, `postModernGeometry`, `lastObjectContextTargetId` (all pre-existing in this file).
- Produces: `beginResizeEntry()` — called with no arguments from the context-menu click handler when `commandId === 'object.resize'`.

- [ ] **Step 1: Write the failing tests**

Insert into `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`, immediately before `private static string ReadMainWindowSource()`:

```csharp
    [TestMethod]
    public void ContextMenuResizeOpensQuickInputToolAtVisualHandles()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "function ensureResizeInputs()");
        StringAssert.Contains(source, "function beginResizeEntry()");
        StringAssert.Contains(source, "commandId === 'object.resize'");
        StringAssert.Contains(source, "beginResizeEntry();");
        StringAssert.Contains(source, "'scada-resize-input-north'");
        StringAssert.Contains(source, "'scada-resize-input-west'");

        // Both inputs must re-derive the current wrapper from the DOM at commit time
        // instead of holding a stale reference, since the host may have already
        // re-rendered (replacing wrapper/handle nodes) after an earlier commit on
        // the other input.
        var commitStart = source.IndexOf("const commit = input => {", StringComparison.Ordinal);
        Assert.IsTrue(commitStart >= 0, "commit() function not found");
        var commitEnd = source.IndexOf("const onKeyDown = event => {", commitStart, StringComparison.Ordinal);
        Assert.IsTrue(commitEnd >= 0, "onKeyDown definition not found after commit()");
        var commitBody = source[commitStart..commitEnd];
        StringAssert.Contains(commitBody, "document.querySelector(`.scada-modern-element[data-id=\"${CSS.escape(targetId)}\"]`)",
            "commit() must re-query the current wrapper by id rather than reuse a captured reference");
        StringAssert.Contains(commitBody, "postModernGeometry(targetId, before, after);");
        StringAssert.Contains(commitBody, "configureInput(north, north.dataset.handle);",
            "committing one input must refresh the other input's position too, since resizing can shift both handles on screen");
        StringAssert.Contains(commitBody, "configureInput(west, west.dataset.handle);");
    }

    [TestMethod]
    public void ResizeQuickInputEscapeClosesWithoutCommittingAndBlurRequiresBothInputsToLoseFocus()
    {
        var source = ReadMainWindowSource();

        var start = source.IndexOf("function beginResizeEntry()", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "beginResizeEntry not found");
        var end = source.IndexOf("document.addEventListener('pointermove', event => {", start, StringComparison.Ordinal);
        Assert.IsTrue(end >= 0, "pointermove listener not found after beginResizeEntry");
        var body = source[start..end];

        StringAssert.Contains(body, "document.activeElement !== north && document.activeElement !== west",
            "blur must only close the tool once focus has left both inputs");

        var escapeStart = body.IndexOf("} else if (event.key === 'Escape') {", StringComparison.Ordinal);
        Assert.IsTrue(escapeStart >= 0, "Escape branch not found in onKeyDown");
        var escapeEnd = body.IndexOf("}", escapeStart + "} else if (event.key === 'Escape') {".Length, StringComparison.Ordinal);
        Assert.IsTrue(escapeEnd >= 0, "End of Escape branch not found");
        var escapeBranch = body[escapeStart..escapeEnd];

        StringAssert.Contains(escapeBranch, "closeBoth();");
        Assert.IsFalse(escapeBranch.Contains("commit(", StringComparison.Ordinal),
            "Escape must not commit the pending typed value");
    }

```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=ContextMenuResizeOpensQuickInputToolAtVisualHandles|Name=ResizeQuickInputEscapeClosesWithoutCommittingAndBlurRequiresBothInputsToLoseFocus"`
Expected: FAIL

- [ ] **Step 3: Add the CSS for the two floating inputs**

In `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, find:

```csharp
    #scada-rotation-input {
      position: fixed;
      display: none;
      width: 64px;
      padding: 3px 6px;
      border: 1px solid #2090a0;
      border-radius: 4px;
      font: 12px "Segoe UI", sans-serif;
      z-index: 9999;
    }
```

Replace with:

```csharp
    #scada-rotation-input {
      position: fixed;
      display: none;
      width: 64px;
      padding: 3px 6px;
      border: 1px solid #2090a0;
      border-radius: 4px;
      font: 12px "Segoe UI", sans-serif;
      z-index: 9999;
    }
    .scada-resize-input {
      position: fixed;
      display: none;
      width: 64px;
      padding: 3px 6px;
      border: 1px solid #2090a0;
      border-radius: 4px;
      font: 12px "Segoe UI", sans-serif;
      z-index: 9999;
      transform: translate(-50%, -50%);
    }
```

- [ ] **Step 4: Add `ensureResizeInputs` and `beginResizeEntry`**

In the same file, find (end of `beginCustomRotationEntry`, right before the `pointermove` listener):

```csharp
    input.addEventListener('input', onInput);
    input.addEventListener('keydown', onKeyDown);
    input.addEventListener('blur', commit);
    input.focus();
    input.select();
  }

  document.addEventListener('pointermove', event => {
```

Replace with:

```csharp
    input.addEventListener('input', onInput);
    input.addEventListener('keydown', onKeyDown);
    input.addEventListener('blur', commit);
    input.focus();
    input.select();
  }

  function ensureResizeInputs() {
    return ['scada-resize-input-north', 'scada-resize-input-west'].map(id => {
      let input = document.getElementById(id);
      if (!input) {
        input = document.createElement('input');
        input.id = id;
        input.type = 'text';
        input.className = 'scada-resize-input';
        document.body.appendChild(input);
      }
      return input;
    });
  }

  function beginResizeEntry() {
    if (!lastObjectContextTargetId) return;
    const targetId = lastObjectContextTargetId;
    const wrapper = document.querySelector(`.scada-modern-element[data-id="${CSS.escape(targetId)}"]`);
    if (!wrapper) return;

    const northHandleName = pickVisualHandle(wrapper, ['n', 'w', 's', 'e'], 'y');
    let westHandleName = pickVisualHandle(wrapper, ['w', 'n', 'e', 's'], 'x', northHandleName);
    if (!westHandleName) {
      westHandleName = pickVisualHandle(wrapper, ['w', 'n', 'e', 's'], 'x');
    }
    if (!northHandleName || !westHandleName) return;

    const [north, west] = ensureResizeInputs();
    const liveTypingPattern = /^\d{0,5}(\.\d?)?$/;

    const configureInput = (input, handleName) => {
      const currentWrapper = document.querySelector(`.scada-modern-element[data-id="${CSS.escape(targetId)}"]`);
      if (!currentWrapper) return;
      const handleEl = currentWrapper.querySelector(`:scope > .scada-modern-handle[data-handle="${handleName}"]`);
      if (!handleEl) return;
      const rect = handleEl.getBoundingClientRect();
      const geometry = readWrapperGeometry(currentWrapper);
      const currentValue = (handleName === 'n' || handleName === 's') ? geometry.height : geometry.width;
      input.value = Math.round(currentValue).toString();
      input.style.left = `${rect.left + rect.width / 2}px`;
      input.style.top = `${rect.top + rect.height / 2}px`;
      input.style.display = 'block';
      input.dataset.handle = handleName;
    };

    const onInput = event => {
      const input = event.target;
      if (input.value !== '' && !liveTypingPattern.test(input.value)) {
        input.value = input.value.slice(0, -1);
      }
    };

    const closeBoth = () => {
      [north, west].forEach(input => {
        input.removeEventListener('input', onInput);
        input.removeEventListener('keydown', onKeyDown);
        input.removeEventListener('blur', onBlur);
        input.style.display = 'none';
        delete input.dataset.handle;
      });
    };

    const onBlur = () => {
      window.setTimeout(() => {
        if (document.activeElement !== north && document.activeElement !== west) {
          closeBoth();
        }
      }, 0);
    };

    const commit = input => {
      const handleName = input.dataset.handle;
      const parsed = parseFloat(input.value);
      const currentWrapper = document.querySelector(`.scada-modern-element[data-id="${CSS.escape(targetId)}"]`);
      if (!currentWrapper || Number.isNaN(parsed) || parsed <= 0) {
        if (currentWrapper) configureInput(input, handleName);
        return;
      }
      const before = readWrapperGeometry(currentWrapper);
      const rotationDeg = getWrapperRotation(currentWrapper);
      const clampedValue = Math.max(8, Math.round(parsed));
      const after = applyAxisResize(before, handleName, clampedValue, rotationDeg);
      setWrapperGeometry(currentWrapper, after);
      postModernGeometry(targetId, before, after);
      configureInput(north, north.dataset.handle);
      configureInput(west, west.dataset.handle);
    };

    const onKeyDown = event => {
      if (event.key === 'Enter') {
        event.preventDefault();
        commit(event.target);
        event.target.focus();
        event.target.select();
      } else if (event.key === 'Escape') {
        event.preventDefault();
        closeBoth();
      }
    };

    configureInput(north, northHandleName);
    configureInput(west, westHandleName);

    [north, west].forEach(input => {
      input.addEventListener('input', onInput);
      input.addEventListener('keydown', onKeyDown);
      input.addEventListener('blur', onBlur);
    });

    north.focus();
    north.select();
  }

  document.addEventListener('pointermove', event => {
```

- [ ] **Step 5: Wire the `object.resize` command in the menu click handler**

In the same file, find:

```csharp
    if (commandId === 'object.rotation.custom') {
      const anchorRect = event.target.getBoundingClientRect();
      hideMenu();
      beginCustomRotationEntry(anchorRect.left, anchorRect.top);
      return;
    }
    hideMenu();
```

Replace with:

```csharp
    if (commandId === 'object.rotation.custom') {
      const anchorRect = event.target.getBoundingClientRect();
      hideMenu();
      beginCustomRotationEntry(anchorRect.left, anchorRect.top);
      return;
    }
    if (commandId === 'object.resize') {
      hideMenu();
      beginResizeEntry();
      return;
    }
    hideMenu();
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=ContextMenuResizeOpensQuickInputToolAtVisualHandles|Name=ResizeQuickInputEscapeClosesWithoutCommittingAndBlurRequiresBothInputsToLoseFocus"`
Expected: PASS

- [ ] **Step 7: Run the full context-menu test suite, then the full solution build and test suite**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~WebViewContextMenuScriptTests"`
Expected: PASS (all tests, including Tasks 1-2 and pre-existing rotation/resize-drag tests)

Run: `dotnet build ScadaBuilderV2.sln`
Expected: Build succeeds with no new warnings/errors.

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: PASS (full suite, no regressions elsewhere)

- [ ] **Step 8: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "Add resize quick-input floating tool wired to the Redimensionner command"
```

---

### Task 4: Manual verification

**Files:** none (manual, exploratory)

**Interfaces:**
- Consumes: the running app (`ScadaBuilderV2.App`), the `AMR_REF_SCADA_V2` reference project.
- Produces: confirmation the feature works end-to-end; no code changes expected unless a defect is found (if so, fix it in the relevant Task above and re-run its tests before continuing).

- [ ] **Step 1: Launch the app**

Run: `dotnet run --project src/ScadaBuilderV2.App`

- [ ] **Step 2: Baseline check (no rotation)**

Open the `AMR_REF_SCADA_V2` project, select any single Element+ object with no children, right-click it, choose "Redimensionner". Confirm:
- Two inputs appear, one at the top-middle handle (N) showing the current height, one at the left-middle handle (W) showing the current width.
- Typing a new value in the N input and pressing Enter changes only the height, keeping the bottom edge in place.
- Typing a new value in the W input and pressing Enter changes only the width, keeping the right edge in place.
- The other input stays open and its displayed value/position stays correct after the first commits.

- [ ] **Step 3: 90°/180°/270° rotation check**

Rotate the same element to 90° (via the existing Rotation context-menu preset), reopen "Redimensionner". Confirm the two inputs now sit at the visually-top and visually-left handles on screen, and that editing them changes the dimension that's visually correct (e.g. at 90°, the input that visually looks like it's editing "width" on screen should, after rotation, be bound to whichever logical dimension the visual left handle actually is). Repeat for 180° and 270°.

- [ ] **Step 4: Arbitrary-angle rotation check**

Rotate the element to an arbitrary angle (e.g. 40°) via "Personnalisé...", reopen "Redimensionner". Confirm:
- The two inputs are positioned at the current visually-topmost and visually-leftmost handles.
- Committing a value in either input keeps the *opposite* edge visually fixed on screen (it should not appear to jump or drift), matching how drag-resize already behaves at that same rotation.

- [ ] **Step 5: Escape / blur check**

Open "Redimensionner", type a value in one input without pressing Enter, then press Escape. Confirm no change was applied. Reopen, type a value without pressing Enter, then click elsewhere on the canvas. Confirm the tool closes and no change was applied.

- [ ] **Step 6: Scope guard check**

Select a group (multiple elements grouped) and right-click it. Confirm "Redimensionner" does not appear in the menu.

- [ ] **Step 7: Undo check**

After committing a resize via the quick-input, press Ctrl+Z. Confirm the element returns to its pre-resize size (the commit reuses the existing undo-tracked geometry path).
