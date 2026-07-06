# Element+ Rotation Handle & Context Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a user rotate a single selected Element+ object either by dragging a repurposed top-right (NE) handle, or by picking a preset (0/90/180/270°) or a custom angle from the existing right-click context menu.

**Architecture:** The scene model already carries `Style.Rotation` end-to-end (persisted + rendered via CSS `transform: rotate()`); only UI + host plumbing is missing. The NE resize handle is repurposed for rotation. A new `updateSceneObjectRotation` message type and one new host method (`UpdateModernElementRotation`) reuse the existing generic `ModernElementChangedAction` / `CommitModernElementProperties` undo/redo path — no new history action class is needed. The context menu gets a "Rotation" submenu following the exact `object.order` submenu pattern already in `BuildContextMenuCommands`.

**Tech Stack:** C# / .NET 8 WPF host (`ScadaBuilderV2.App`), embedded JS editor frontend (`MainWindow.WebViewScript.cs`), MSTest (`tests/ScadaBuilderV2.Tests`), WebView2 message bridge.

## Global Constraints

- Editor artifacts (handles, badges, menu DOM) must never leak into export geometry — only the numeric `Style.Rotation` value is persisted/exported.
- Angle is always normalized/stored in `[0, 360)`, rounded to at most 1 decimal place, regardless of entry point (drag, preset, custom input).
- Feature scope is a single selected Element+ object only — no group/multi-selection rotation.
- Follow existing code conventions in the touched files: this codebase's `MainWindow.WebViewScript.cs`/`MainWindow.xaml.cs` tests assert directly on raw source text (see `WebViewContextMenuScriptTests.cs`) rather than executing the WPF host — new tests must follow that same convention.
- Before starting, the worktree must be clean (per `docs/AGENTS.md`); commit after each task.

---

### Task 1: Add `Rotation` field to the frontend↔host DTO

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs:243-261` (inside `LegacyViewerMessage`, alongside the existing `X`/`Y`/`Width`/`Height` fields)
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs` (new test method)

**Interfaces:**
- Produces: `LegacyViewerMessage.Rotation` (`double`, default `0`) — consumed by Task 2's `UpdateModernElementRotation`.

- [ ] **Step 1: Write the failing test**

Add this test to `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs` (place it near `ElementPropertiesExposeAdvancedShapeStyleFields`, matching that test's pattern of reading the source file as text):

```csharp
[TestMethod]
public void LegacyViewerMessageExposesRotationField()
{
    var nestedTypesPath = Path.Combine(GetAppProjectDirectory(), "MainWindow.NestedTypes.cs");
    var source = File.ReadAllText(nestedTypesPath);

    StringAssert.Contains(source, "public double Rotation { get; set; }");
}
```

If the test file does not already have a `GetAppProjectDirectory()` helper, check the existing tests in the same file (e.g. `ElementPropertiesExposeAdvancedShapeStyleFields`) for how `mainXaml`/`source` paths are resolved, and reuse that exact helper instead of introducing a new one.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=LegacyViewerMessageExposesRotationField"`
Expected: FAIL (assertion fails — string not found).

- [ ] **Step 3: Add the field**

In `src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs`, inside the `LegacyViewerMessage` class, add after the `Height` property (around line 253):

```csharp
        public double Rotation { get; set; }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=LegacyViewerMessageExposesRotationField"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: add Rotation field to LegacyViewerMessage DTO"
```

---

### Task 2: Add `UpdateModernElementRotation` host method and wire the message switch case

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs:1332-1344` (switch statement in `OnLegacyViewerMessageReceived`)
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs` (new private method, placed near `UpdateModernElementGeometry` at line 4670)
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `LegacyViewerMessage.Rotation` (Task 1), `ScadaElementStyle.Rotation` (`src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs:207`, already exists), `CommitModernElementProperties(ScadaElement current, ScadaElement updated)` (`MainWindow.xaml.cs:5378`, already exists).
- Produces: `UpdateModernElementRotation(string? id, double rotation)` and `NormalizeRotation(double degrees)` — consumed by Task 5's preset dispatch cases.

- [ ] **Step 1: Write the failing test**

Add to `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`:

```csharp
[TestMethod]
public void MainWindowHandlesRotationMessageAndNormalizesAngle()
{
    var mainWindowPath = Path.Combine(GetAppProjectDirectory(), "MainWindow.xaml.cs");
    var source = File.ReadAllText(mainWindowPath);

    StringAssert.Contains(source, "case \"updateSceneObjectRotation\":");
    StringAssert.Contains(source, "UpdateModernElementRotation(message.Id, message.Rotation)");
    StringAssert.Contains(source, "private void UpdateModernElementRotation(string? id, double rotation)");
    StringAssert.Contains(source, "private static double NormalizeRotation(double degrees)");
}
```

(Use whatever path-resolution helper Task 1's test used for `mainXaml`/`source` — reuse it, do not duplicate a new one.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=MainWindowHandlesRotationMessageAndNormalizesAngle"`
Expected: FAIL

- [ ] **Step 3: Add the switch case**

In `src/ScadaBuilderV2.App/MainWindow.xaml.cs`, inside `OnLegacyViewerMessageReceived`'s switch (right after the `updateSceneObjectGeometry`/`updateModernElementGeometry` case ending at line 1344, before `resizeSceneGroupWithChildren` at line 1345):

```csharp
                case "updateSceneObjectRotation":
                    UpdateModernElementRotation(message.Id, message.Rotation);
                    break;
```

- [ ] **Step 4: Add the method**

In `src/ScadaBuilderV2.App/MainWindow.xaml.cs`, immediately after `UpdateModernElementGeometry` (which ends at line 4733), add:

```csharp
    private void UpdateModernElementRotation(string? id, double rotation)
    {
        if (_activeScene is null || string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var current = _activeScene.FindElementRecursive(id);
        if (current is null)
        {
            return;
        }

        var normalized = NormalizeRotation(rotation);
        if (Math.Abs(current.Style.Rotation - normalized) < 0.05)
        {
            return;
        }

        var updated = current with { Style = current.Style with { Rotation = normalized } };
        CommitModernElementProperties(current, updated);
    }

    private static double NormalizeRotation(double degrees)
    {
        var normalized = degrees % 360;
        if (normalized < 0)
        {
            normalized += 360;
        }

        return Math.Round(normalized, 1);
    }
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=MainWindowHandlesRotationMessageAndNormalizesAngle"`
Expected: PASS

- [ ] **Step 6: Build the full solution to catch compile errors**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: Build succeeds with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: add UpdateModernElementRotation host handler for rotation messages"
```

---

### Task 3: Repurpose the NE handle for rotation (drag interaction)

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:203-204` (CSS cursor for `[data-handle="ne"]`)
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:1799-1862` (`pointerdown` handler — set `mode: 'rotate'` for the NE handle)
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:2128-2206` (`pointermove` — add rotate branch)
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:2254-2288` (`pointerup` — add rotate branch, post new message)
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:1063-1077` (add `getWrapperRotation` next to `readWrapperGeometry`/`setWrapperGeometry`)
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:969-982` (add `postModernRotation` next to `postModernGeometry`)
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `modernDrag` shared state object (declared `let modernDrag = null;` at line 282), `readWrapperGeometry(wrapper)` (line 1063).
- Produces: `getWrapperRotation(wrapper)` (returns current rotation in degrees, `0` if unset) and `postModernRotation(id, rotation)` — both consumed by Task 4 (badge) and reused as-is by Task 6 (custom input).

- [ ] **Step 1: Write the failing test**

Add to `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs` (reuse whatever helper resolves the WebViewScript source, matching the existing `source` variable pattern seen in `ElementPropertiesExposeAdvancedShapeStyleFields`):

```csharp
[TestMethod]
public void NeHandleIsRepurposedForRotationDrag()
{
    var source = GetWebViewScriptSource();

    StringAssert.Contains(source, "cursor: grab;");
    StringAssert.Contains(source, "mode: event.target?.dataset?.handle === 'ne' ? 'rotate' : (isResize ? 'resize' : 'move')");
    StringAssert.Contains(source, "function getWrapperRotation(wrapper)");
    StringAssert.Contains(source, "function postModernRotation(id, rotation)");
    StringAssert.Contains(source, "modernDrag.mode === 'rotate'");
}
```

(If the file has no existing `GetWebViewScriptSource()` helper, look at how `ElementPropertiesExposeAdvancedShapeStyleFields` obtains its `source` variable and copy that exact expression instead of inventing a new helper name.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=NeHandleIsRepurposedForRotationDrag"`
Expected: FAIL

- [ ] **Step 3: Update the CSS cursor for the NE handle**

In `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, replace line 204:

```css
    .scada-modern-handle[data-handle="ne"] { right: -6px; top: -6px; cursor: nesw-resize; }
```

with:

```css
    .scada-modern-handle[data-handle="ne"] { right: -6px; top: -6px; cursor: grab; }
```

- [ ] **Step 4: Add `getWrapperRotation` next to `readWrapperGeometry`**

In `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, after `setWrapperGeometry` (ends line 1077), add:

```js
  function getWrapperRotation(wrapper) {
    const match = /rotate\(([-\d.]+)deg\)/.exec(wrapper.style.transform || '');
    return match ? parseFloat(match[1]) || 0 : 0;
  }
```

- [ ] **Step 5: Add `postModernRotation` next to `postModernGeometry`**

In `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, after `postModernGeometry` (ends line 982), add:

```js
  function postModernRotation(id, rotation) {
    window.chrome?.webview?.postMessage({
      type: 'updateSceneObjectRotation',
      id,
      rotation
    });
  }
```

- [ ] **Step 6: Set `mode: 'rotate'` on NE-handle pointerdown**

In `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, in the `wrapper.addEventListener('pointerdown', ...)` handler, locate this line (1845 in the current file):

```js
          mode: isResize ? 'resize' : 'move',
```

Replace it with:

```js
          mode: event.target?.dataset?.handle === 'ne' ? 'rotate' : (isResize ? 'resize' : 'move'),
```

Then, right after the `modernDrag = { ... };` assignment block (after line 1860, before `sceneMoveWrapper.setPointerCapture?.(event.pointerId);` at line 1861), add the rotation pivot capture:

```js
        if (modernDrag.mode === 'rotate') {
          modernDrag.centerX = modernDrag.startX + modernDrag.startWidth / 2;
          modernDrag.centerY = modernDrag.startY + modernDrag.startHeight / 2;
          modernDrag.startRotation = getWrapperRotation(sceneMoveWrapper);
        }
```

- [ ] **Step 7: Add the rotate branch to the `pointermove` listener**

In `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, inside the `if (modernDrag) { ... }` block, change line 2138 from:

```js
      if (modernDrag.mode === 'move') {
```

to insert a new branch before it:

```js
      if (modernDrag.mode === 'rotate') {
        const wrapperRect = modernDrag.wrapper.getBoundingClientRect();
        const pivotClientX = wrapperRect.left + wrapperRect.width / 2;
        const pivotClientY = wrapperRect.top + wrapperRect.height / 2;
        const angleRad = Math.atan2(event.clientY - pivotClientY, event.clientX - pivotClientX);
        let angleDeg = angleRad * (180 / Math.PI) + 90;
        if (event.ctrlKey) {
          angleDeg = Math.round(angleDeg / 90) * 90;
        }
        let normalized = angleDeg % 360;
        if (normalized < 0) {
          normalized += 360;
        }
        normalized = Math.round(normalized * 10) / 10;
        modernDrag.wrapper.style.transformOrigin = 'center center';
        modernDrag.wrapper.style.transform = `rotate(${normalized}deg)`;
        modernDrag.currentRotation = normalized;
        updateRotationBadge(event.clientX, event.clientY, normalized);
      } else if (modernDrag.mode === 'move') {
```

(This turns the existing `if (modernDrag.mode === 'move')` into an `else if` — the `else { ... resize logic ... }` block that already follows stays unchanged.)

`updateRotationBadge` is implemented in Task 4 — for this task, add a temporary no-op stub right above the `pointermove` listener registration (search for `document.addEventListener('pointermove'` at line 2106) so the file still compiles/runs standalone:

```js
  function updateRotationBadge(clientX, clientY, angleDeg) {
    // Implemented in Task 4 (live angle badge).
  }
```

- [ ] **Step 8: Add the rotate branch to the `pointerup` listener**

In `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, inside the `if (modernDrag) { ... }` block of the `pointerup` listener, change line 2254 area from:

```js
    if (modernDrag) {
      const geometry = readWrapperGeometry(modernDrag.wrapper);
      if (modernDrag.mode === 'move' && (modernDrag.items || []).length > 1) {
```

to:

```js
    if (modernDrag) {
      if (modernDrag.mode === 'rotate') {
        postModernRotation(modernDrag.id, modernDrag.currentRotation ?? modernDrag.startRotation);
        hideRotationBadge();
        modernDrag = null;
        event.preventDefault();
        event.stopPropagation();
        return;
      }
      const geometry = readWrapperGeometry(modernDrag.wrapper);
      if (modernDrag.mode === 'move' && (modernDrag.items || []).length > 1) {
```

`hideRotationBadge` is implemented in Task 4 — add a temporary no-op stub next to the `updateRotationBadge` stub added in Step 7:

```js
  function hideRotationBadge() {
    // Implemented in Task 4 (live angle badge).
  }
```

- [ ] **Step 9: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=NeHandleIsRepurposedForRotationDrag"`
Expected: PASS

- [ ] **Step 10: Build the full solution**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: Build succeeds with 0 errors.

- [ ] **Step 11: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: repurpose NE handle to drag-rotate Element+ objects"
```

---

### Task 4: Live angle badge during rotation drag

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs` (CSS block, near line 210)
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs` (replace the two stub functions added in Task 3 Step 7/8)
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `updateRotationBadge(clientX, clientY, angleDeg)` / `hideRotationBadge()` stub signatures from Task 3.
- Produces: real implementations of both, plus the `#scada-rotation-badge` DOM element created lazily.

- [ ] **Step 1: Write the failing test**

```csharp
[TestMethod]
public void RotationDragShowsLiveAngleBadge()
{
    var source = GetWebViewScriptSource();

    StringAssert.Contains(source, "scada-rotation-badge");
    StringAssert.Contains(source, "function updateRotationBadge(clientX, clientY, angleDeg)");
    StringAssert.Contains(source, "function hideRotationBadge()");
    StringAssert.Contains(source, "toFixed(1)");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=RotationDragShowsLiveAngleBadge"`
Expected: FAIL

- [ ] **Step 3: Add the badge CSS**

In `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, after the `.scada-modern-handle` CSS rules (after line 210), add:

```css
    #scada-rotation-badge {
      position: fixed;
      display: none;
      padding: 2px 6px;
      border-radius: 4px;
      background: #0f2a30;
      color: #ffffff;
      font: 12px "Segoe UI", sans-serif;
      pointer-events: none;
      z-index: 9999;
      transform: translate(12px, -50%);
    }
```

- [ ] **Step 4: Replace the stub functions with real implementations**

In `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, replace the two stubs added in Task 3:

```js
  function updateRotationBadge(clientX, clientY, angleDeg) {
    // Implemented in Task 4 (live angle badge).
  }

  function hideRotationBadge() {
    // Implemented in Task 4 (live angle badge).
  }
```

with:

```js
  function ensureRotationBadge() {
    let badge = document.getElementById('scada-rotation-badge');
    if (!badge) {
      badge = document.createElement('div');
      badge.id = 'scada-rotation-badge';
      document.body.appendChild(badge);
    }
    return badge;
  }

  function updateRotationBadge(clientX, clientY, angleDeg) {
    const badge = ensureRotationBadge();
    badge.textContent = `${angleDeg.toFixed(1)}°`;
    badge.style.left = `${clientX}px`;
    badge.style.top = `${clientY}px`;
    badge.style.display = 'block';
  }

  function hideRotationBadge() {
    const badge = document.getElementById('scada-rotation-badge');
    if (badge) {
      badge.style.display = 'none';
    }
  }
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=RotationDragShowsLiveAngleBadge"`
Expected: PASS

- [ ] **Step 6: Build the full solution**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: Build succeeds with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: show live angle badge while dragging the rotation handle"
```

---

### Task 5: "Rotation" context menu submenu with 0/90/180/270 presets

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs:3722-3741` (`BuildContextMenuCommands`, `object`/`modern` branch — add descriptor after the existing `object.order` block)
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs:3863-3874` (`ExecuteEditorCommandAsync` switch — add preset cases after the `object.order.*` cases)
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `UpdateModernElementRotation(string? id, double rotation)` (Task 2), `EditorCommandDescriptor` (`src/ScadaBuilderV2.Application/Commands/EditorCommandDescriptor.cs`, already exists).
- Produces: command ids `object.rotation`, `object.rotation.0`, `object.rotation.90`, `object.rotation.180`, `object.rotation.270`, `object.rotation.custom` — the last is consumed by Task 6 (client-side interception, no server-side case needed for it).

- [ ] **Step 1: Write the failing test**

```csharp
[TestMethod]
public void ContextMenuOffersRotationPresetsForSingleElementPlusSelection()
{
    var source = GetMainWindowXamlCsSource();

    StringAssert.Contains(source, "\"object.rotation\"");
    StringAssert.Contains(source, "\"object.rotation.0\"");
    StringAssert.Contains(source, "\"object.rotation.90\"");
    StringAssert.Contains(source, "\"object.rotation.180\"");
    StringAssert.Contains(source, "\"object.rotation.270\"");
    StringAssert.Contains(source, "\"object.rotation.custom\"");
    StringAssert.Contains(source, "case \"object.rotation.0\":");
    StringAssert.Contains(source, "UpdateModernElementRotation(message.Id, 0)");
    StringAssert.Contains(source, "UpdateModernElementRotation(message.Id, 90)");
    StringAssert.Contains(source, "UpdateModernElementRotation(message.Id, 180)");
    StringAssert.Contains(source, "UpdateModernElementRotation(message.Id, 270)");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=ContextMenuOffersRotationPresetsForSingleElementPlusSelection"`
Expected: FAIL

- [ ] **Step 3: Add the "Rotation" descriptor in `BuildContextMenuCommands`**

In `src/ScadaBuilderV2.App/MainWindow.xaml.cs`, inside the `if (_selectedSceneObjectIds.Count == 1 && ...)` block that currently only adds `object.order` (lines 3722-3741), add the rotation descriptor right after the `modernCommands.Add(new EditorCommandDescriptor("object.order", ...));` call, still inside that same `if`:

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
```

Note this reuses the same `_selectedSceneObjectIds.Count == 1` guard already used for `object.order`, matching the spec's single-selection scope — no additional group check is needed since that guard already restricts to a single top-level element.

- [ ] **Step 4: Add preset dispatch cases in `ExecuteEditorCommandAsync`**

In `src/ScadaBuilderV2.App/MainWindow.xaml.cs`, inside the `ExecuteEditorCommandAsync` switch, right after the `object.order.send-to-back` case (ends line 3874, before `object.delete` at line 3875), add:

```csharp
            case "object.rotation.0":
                UpdateModernElementRotation(message.Id, 0);
                break;
            case "object.rotation.90":
                UpdateModernElementRotation(message.Id, 90);
                break;
            case "object.rotation.180":
                UpdateModernElementRotation(message.Id, 180);
                break;
            case "object.rotation.270":
                UpdateModernElementRotation(message.Id, 270);
                break;
```

Do **not** add a case for `object.rotation.custom` here — Task 6 intercepts that command client-side (in JS) before it is ever posted as an `executeCommand` message, so it never reaches this switch. The switch's existing `default: SetStatus($"Commande non supportee: {commandId}");` is intentionally left as the fallback in case that interception is ever bypassed.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=ContextMenuOffersRotationPresetsForSingleElementPlusSelection"`
Expected: PASS

- [ ] **Step 6: Build the full solution**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: Build succeeds with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: add Rotation preset submenu (0/90/180/270) to Element+ context menu"
```

---

### Task 6: "Personnalisé..." custom angle input in the context menu

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs` (CSS block, near the rotation badge CSS)
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:1881-1906` (`wrapper.addEventListener('contextmenu', ...)` — track the last object context target id)
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:2399-2415` (`menu.addEventListener('click', ...)` — intercept `object.rotation.custom`)
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `postModernRotation(id, rotation)` (Task 3), `NormalizeRotation` semantics mirrored client-side (1 decimal max, wrap to `[0, 360)`).
- Produces: `beginCustomRotationEntry(anchorX, anchorY)`, module-level `let lastObjectContextTargetId = null;`.

- [ ] **Step 1: Write the failing test**

```csharp
[TestMethod]
public void ContextMenuCustomRotationOpensValidatedInlineInput()
{
    var source = GetWebViewScriptSource();

    StringAssert.Contains(source, "let lastObjectContextTargetId = null;");
    StringAssert.Contains(source, "lastObjectContextTargetId = sceneContextId;");
    StringAssert.Contains(source, "function beginCustomRotationEntry(anchorX, anchorY)");
    StringAssert.Contains(source, "commandId === 'object.rotation.custom'");
    StringAssert.Contains(source, "/^\\d{1,3}(\\.\\d)?$/");
    StringAssert.Contains(source, "postModernRotation(lastObjectContextTargetId");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=ContextMenuCustomRotationOpensValidatedInlineInput"`
Expected: FAIL

- [ ] **Step 3: Add the custom-input CSS**

In `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, after the `#scada-rotation-badge` CSS block added in Task 4, add:

```css
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

- [ ] **Step 4: Declare `lastObjectContextTargetId` and set it on right-click**

In `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, near the existing `let modernDrag = null;` declaration (line 282), add:

```js
  let lastObjectContextTargetId = null;
```

In the `wrapper.addEventListener('contextmenu', ...)` handler (lines 1881-1906), right after `const sceneContextId = sceneContextWrapper?.dataset?.id || element.Id;` (line 1891), add:

```js
        lastObjectContextTargetId = sceneContextId;
```

- [ ] **Step 5: Add `beginCustomRotationEntry`**

In `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, next to `ensureRotationBadge` (added in Task 4), add:

```js
  function ensureRotationInput() {
    let input = document.getElementById('scada-rotation-input');
    if (!input) {
      input = document.createElement('input');
      input.id = 'scada-rotation-input';
      input.type = 'text';
      document.body.appendChild(input);
    }
    return input;
  }

  function beginCustomRotationEntry(anchorX, anchorY) {
    if (!lastObjectContextTargetId) return;
    const targetId = lastObjectContextTargetId;
    const input = ensureRotationInput();
    const targetWrapper = document.querySelector(`.scada-modern-element[data-id="${targetId}"]`);
    input.value = targetWrapper ? getWrapperRotation(targetWrapper).toFixed(1) : '0';
    input.style.left = `${anchorX}px`;
    input.style.top = `${anchorY}px`;
    input.style.display = 'block';

    const decimalPattern = /^\d{1,3}(\.\d)?$/;
    const onInput = () => {
      if (input.value !== '' && !decimalPattern.test(input.value)) {
        input.value = input.value.slice(0, -1);
      }
    };

    const commit = () => {
      const parsed = parseFloat(input.value);
      if (!Number.isNaN(parsed)) {
        let normalized = parsed % 360;
        if (normalized < 0) {
          normalized += 360;
        }
        normalized = Math.round(normalized * 10) / 10;
        postModernRotation(targetId, normalized);
      }
      cleanup();
    };

    const cancel = () => cleanup();

    const onKeyDown = event => {
      if (event.key === 'Enter') {
        event.preventDefault();
        commit();
      } else if (event.key === 'Escape') {
        event.preventDefault();
        cancel();
      }
    };

    function cleanup() {
      input.style.display = 'none';
      input.removeEventListener('input', onInput);
      input.removeEventListener('keydown', onKeyDown);
      input.removeEventListener('blur', commit);
    }

    input.addEventListener('input', onInput);
    input.addEventListener('keydown', onKeyDown);
    input.addEventListener('blur', commit);
    input.focus();
    input.select();
  }
```

- [ ] **Step 6: Intercept `object.rotation.custom` in the menu click handler**

In `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, in the `menu.addEventListener('click', ...)` handler, locate:

```js
    const commandId = event.target?.getAttribute?.('data-command-id');
    if (!commandId) return;
    event.preventDefault();
    event.stopPropagation();
    hideMenu();
    window.chrome?.webview?.postMessage({
      type: 'executeCommand',
      commandId,
```

Replace with:

```js
    const commandId = event.target?.getAttribute?.('data-command-id');
    if (!commandId) return;
    event.preventDefault();
    event.stopPropagation();
    if (commandId === 'object.rotation.custom') {
      const anchorRect = event.target.getBoundingClientRect();
      hideMenu();
      beginCustomRotationEntry(anchorRect.left, anchorRect.top);
      return;
    }
    hideMenu();
    window.chrome?.webview?.postMessage({
      type: 'executeCommand',
      commandId,
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=ContextMenuCustomRotationOpensValidatedInlineInput"`
Expected: PASS

- [ ] **Step 8: Build the full solution and run the complete test suite**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: Build succeeds with 0 errors.

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: All tests pass, including the 5 new tests added in Tasks 1-6 and all pre-existing `WebViewContextMenuScriptTests` (confirming no regression on the single-handler `contextmenu` invariant or the existing submenu-nesting tests).

- [ ] **Step 9: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: add custom angle input to the Rotation context menu"
```

---

### Task 7: Manual verification and regression coverage doc update

**Files:**
- Modify: `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md` (add an entry for the new rotation tests, following that file's existing format for other `WebViewContextMenuScriptTests` entries)

**Interfaces:**
- Consumes: nothing new — this task is documentation + manual verification only.

- [ ] **Step 1: Launch the app and manually verify the golden path**

Run: `dotnet run --project src/ScadaBuilderV2.App`

In the running app, open `AMR_REF_SCADA_V2` / `win00008`, select a single Element+ object, and verify:
1. The NE corner handle shows a `grab` cursor and, when dragged, rotates the element live with a `NN.N°` badge following the cursor.
2. Holding `Ctrl` while dragging snaps the angle to the nearest of 0/90/180/270°.
3. Releasing the drag persists the rotation (reselect the element or check the Properties dialog's Rotation field shows the new value).
4. Right-clicking the element shows a "Rotation" submenu with `0°/90°/180°/270°/Personnalisé...`; clicking a preset rotates immediately.
5. Clicking "Personnalisé...", typing `87.13`, and pressing Enter results in the element rotating to `87.1°` (not `87.13°`).
6. Typing `450` and committing normalizes to `90°`; typing `-10` normalizes to `350°`.
7. Ctrl+Z after each of the above undoes exactly one rotation step; Ctrl+Y redoes it.
8. The remaining resize handles (N/S/E/W/NW/SW/SE) still resize correctly — NE no longer resizes.

- [ ] **Step 2: Verify export does not leak editor state**

Export the project (or run the existing `Ft100SceneExporterTests` suite: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~Ft100SceneExporterTests"`) and confirm the exported package contains only the numeric rotation value applied as CSS, with no handle/badge/menu DOM.

- [ ] **Step 3: Update the regression coverage map**

Open `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md`, find the section listing `WebViewContextMenuScriptTests` coverage, and add a line for the new rotation tests following the existing format in that file (guardrail/feature name → test method names → this plan's date).

- [ ] **Step 4: Run the docs verification script**

Run: `powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1`
Expected: Passes with no errors.

- [ ] **Step 5: Commit**

```bash
git add docs/08_implementation_status/REGRESSION_COVERAGE_V2.md
git commit -m "docs: record rotation handle/context-menu regression coverage"
```
