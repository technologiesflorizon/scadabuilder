# Element+ Rotation Handle & Context Menu — Design

Date: 2026-07-06
Branch: `add-ui-feature`

## Purpose

Let a user rotate a selected Element+ object either by dragging a handle in the
top-right corner, or by picking a preset/custom angle from the right-click
context menu.

## Background

The scene model already carries rotation end-to-end: `ScadaElementStyle.Rotation`
(`src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs:207`) is persisted in
every element's `Style` block in scene JSON and is already applied visually via
`wrapper.style.transform = rotate(${Rotation}deg)` with `transform-origin:
center center` (`src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:1710-1711`).
No UI exists yet to change it. This feature adds that UI and the plumbing to
persist changes, following the same patterns already used for the existing
resize handles (N/S/E/W/corners).

## Scope

- Single selected Element+ object only. Groups and multi-selection are out of
  scope for this feature.
- Two entry points to set rotation:
  1. A drag handle at the top-right (NE) corner of the selection.
  2. A "Rotation" submenu in the existing right-click context menu.

## Design

### 1. Rotation handle (replaces the NE resize handle)

The existing NE corner handle (`data-handle="ne"`,
`MainWindow.WebViewScript.cs:1792-1797`) is repurposed for rotation instead of
corner-resize. It gets a distinct cursor (rotate icon) on hover.

Interaction, following the existing `pointerdown`/`pointermove`/`pointerup`
drag pattern (`MainWindow.WebViewScript.cs:~1799`, `~2106`, `~2236`):

- `pointerdown` on the NE handle sets `modernDrag.mode = 'rotate'` and captures
  the element's center (via `readWrapperGeometry`, line 1063) as the pivot.
- `pointermove` computes the angle from the pivot to the cursor
  (`atan2`), converts to degrees, and live-applies it to
  `wrapper.style.transform`.
  - If `Ctrl` is held, the angle snaps to the nearest multiple of 90°
    (0/90/180/270) — no intermediate snap increments.
  - A floating tooltip near the cursor shows the live angle, formatted to at
    most 1 decimal place (e.g. `87.1°`).
- `pointerup` normalizes the final angle to `[0, 360)` and sends it to the C#
  host.
- Consecutive drag updates within one gesture coalesce into a single undo
  entry (see History section).

**Trade-off accepted:** the NE corner no longer supports diagonal resize. Only
N/S/E/W and the remaining corners (NW/SW/SE) keep resize behavior.

### 2. Context menu "Rotation" submenu

Added to the existing custom context menu (`openContextMenu`,
`MainWindow.WebViewScript.cs:1963`), built from the existing
`EditorCommandDescriptor` pattern. New submenu with 5 entries:

- `0°`, `90°`, `180°`, `270°` — each sets the absolute rotation immediately and
  commits one undo entry per click (not merged).
- `Personnalisé...` — reveals an inline numeric input. Validates on
  Enter/blur:
  - Accepts a number with at most 1 decimal place (`87`, `87.1` valid; `87.13`
    is truncated to `87.1`).
  - Values outside `[0, 360)` are normalized (e.g. `-10` → `350`, `450` → `90`)
    rather than rejected.
  - Empty or non-numeric input on blur/Enter is a no-op — rotation is left
    unchanged.

The submenu is only shown when the current selection is a single Element+
object (matching the scope above).

### 3. Frontend → host DTO

`LegacyViewerMessage` (`MainWindow.NestedTypes.cs:175`) gains `Rotation` and
`BeforeRotation` (double) fields, mirroring the existing `Before*`/current
pattern used for `X/Y/Width/Height`. A new message type (e.g.
`updateSceneObjectRotation`) is posted from JS via
`window.chrome.webview.postMessage(...)` and handled in the existing switch in
`OnLegacyViewerMessageReceived` (`MainWindow.xaml.cs:1332-1369`).

### 4. Undo/redo

New `ModernElementRotationChangedAction` in
`src/ScadaBuilderV2.Application/History/`, modeled on
`ModernElementBoundsChangedAction.cs`:

- Implements `UndoAsync`/`RedoAsync` by writing `Rotation` back onto the
  target element via `scene.FindElementRecursive` +
  `WithReplacedElementRecursive`, then `context.MarkDirty()` /
  `RefreshPreviewAsync()`.
- Implements `CanMergeWith`/`MergeWith` so continuous handle-drag updates
  coalesce into one history entry per gesture. Context-menu preset clicks and
  custom-input commits each produce their own, non-merged entry.

### 5. Persistence

No scene JSON schema change needed — `Rotation` is already read/written on
every element's `Style` block.

## Edge cases

- **Angle normalization:** always stored/sent in `[0, 360)`, regardless of
  entry point, to avoid unbounded drift from repeated rotation in one
  direction.
- **Manual input validation:** at most 1 decimal digit; out-of-range values
  are normalized, not rejected; invalid/empty input on commit is a no-op.
- **Scope guard:** handle and submenu only active for a single selected
  Element+; not shown for groups or multi-selection.
- **Export leakage:** only the numeric `Rotation` value is persisted/exported;
  no handle DOM or menu state must leak into exported scene geometry.

## Testing

- Extend `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs` for the
  new Rotation submenu and custom-angle input validation (decimal truncation,
  range normalization, empty/no-op input).
- Unit tests for `ModernElementRotationChangedAction` covering undo/redo and
  drag-merge coalescing.
- Integration test verifying `Rotation`/`BeforeRotation` DTO fields round-trip
  correctly into `ScadaElementStyle`.
- Manual check: confirm existing NE-corner-resize tests/usages are updated to
  reflect the new rotate behavior, and that no resize regression exists on the
  remaining handles (N/S/E/W/NW/SW/SE).
