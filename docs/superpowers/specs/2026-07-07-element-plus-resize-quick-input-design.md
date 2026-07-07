# Element+ Resize Quick-Input (Context Menu) — Design

Date: 2026-07-07
Branch: `master`

## Purpose

Let a user set an exact width/height for a selected Element+ object from the
right-click context menu, without opening the full Properties dialog: pick
"Redimensionner", two floating numeric inputs appear at the element's current
N and W handle screen positions, pre-filled with the current dimension; typing
a value and pressing Enter applies it immediately.

## Background

Resize already works via drag on the 8 handles (`n/s/e/w/nw/ne/sw/se`,
`MainWindow.WebViewScript.cs:199-218`, drag wiring `~1899-1941`, commit
`~2360-2430`), posting `updateSceneObjectGeometry` to the host
(`MainWindow.xaml.cs:~1335`, `UpdateModernElementGeometry`). Rotation already
has a quick-input pattern to copy: `ensureRotationInput()` /
`beginCustomRotationEntry(anchorX, anchorY)` (`MainWindow.WebViewScript.cs:
2221-2290`) create a single floating `<input>`, positioned near an anchor,
validated live, committed on Enter/blur, cancelled on Escape. This feature
follows the same pattern with two inputs instead of one, and reuses the
existing geometry commit path instead of adding a new one.

`Bounds(X, Y, Width, Height)` (`ScadaSceneModels.cs:183`) are always in
unrotated local pixel coordinates; visual rotation is a separate CSS
`transform: rotate(deg)` applied on top (`MainWindow.WebViewScript.cs:1777`).
This means the side handles keep their local `data-handle` identity
(`n`/`s`/`e`/`w`) but rotate together with the element visually — after a 90°
rotation, the handle currently touching the top of the screen may well be the
one tagged `data-handle="e"`. Because the handle DOM elements already reflect
this (they're children of the rotated wrapper), we don't need any trig: we can
read their actual screen rects.

## Scope

- Single selected Element+ object only (matches the Rotation feature's
  scope). Groups and multi-selection are out of scope for this iteration —
  the "Redimensionner" menu item is not shown when the selection is a group
  or has children.
- Two entry points already exist for exact geometry: drag handles, and the
  Properties dialog. This adds a third, faster one for the common case of
  "just change one or two dimensions."

## Design

### 1. Context menu entry

Add a single leaf command `object.resize` ("Redimensionner") next to the
existing `object.rotation` submenu in `BuildContextMenuCommands`
(`MainWindow.xaml.cs:~3896`), following the same `EditorCommandDescriptor`
construction. Unlike Rotation, this is not a submenu — there's no fixed
preset to offer, so the item goes straight to the quick-input UI. Visible
only when the context-menu target resolves to a single Element+ object with
no children (same guard as the Rotation submenu).

Clicking it posts a command back to JS (mirroring how
`object.rotation.custom` currently triggers `beginCustomRotationEntry`),
which calls a new `beginResizeEntry(id)`.

### 2. Determining which handle is visually N / visually W

`beginResizeEntry(id)`:

1. Looks up the element's wrapper and its 4 side-handle DOM elements
   (`data-handle="n"|"s"|"e"|"w"` — corner handles are not used for this
   feature).
2. Reads each handle's `getBoundingClientRect()` (already correct on-screen
   position, since rotation is a CSS transform on an ancestor).
3. `visualNorthHandle` = the handle with the smallest rect-center Y.
   `visualWestHandle` = the handle with the smallest rect-center X.
4. Each of `n`/`s` controls `Height`; each of `e`/`w` controls `Width`. So the
   two inputs end up editing whichever logical dimension the visually
   top/left handle actually owns right now — which is exactly the "N becomes
   E after rotation" case called out in the request: if rotation has moved
   the `e` handle to visual-north, the north input edits **Width**, not
   Height.
5. Tie-break (near-45° rotation, handle centers coincide within a pixel
   threshold): prefer `n` for the north input and `w` for the west input if
   both candidates tie, to keep behavior deterministic.

### 3. Floating inputs

Two reusable `<input>` elements are lazily created (mirroring
`ensureRotationInput()`): `#scada-resize-input-north`,
`#scada-resize-input-west`, same fixed-position floating style. Each is
positioned at its handle's current screen rect center and pre-filled with the
current logical dimension (`Bounds.Width` or `Bounds.Height`, rounded to
whole pixels), text pre-selected on focus.

Behavior per input, independent of the other:

- **Enter**: parse the value as a positive number. If valid, commit that one
  dimension immediately (see §4) and re-focus/keep both inputs open,
  refreshing their displayed value and position (geometry may have shifted
  the other handle's screen position too, e.g. editing Height moves the N
  edge, which also moves the W handle's screen Y — reposition both inputs
  after any commit). If invalid or empty, no-op, value reverts to the
  current dimension.
- **Escape**: closes both inputs without applying any pending (uncommitted)
  edit in that input. Already-committed edits (from a prior Enter) stand.
- **Blur on both inputs** (focus leaves both, e.g. click elsewhere on
  canvas): closes the tool. No implicit commit of whatever text is currently
  typed but not yet confirmed with Enter.

Only positive, non-zero values are accepted (matches existing resize-drag
minimum-size clamping, if any exists in the drag path — otherwise a simple
`> 0` check is sufficient for v1).

### 4. Applying the resize

Each side handle only ever changes one axis, with the opposite edge fixed —
this already matches how drag-resize on side handles behaves, so the same
per-handle formula applies:

| handle | changes | fixed edge | formula |
|--------|---------|------------|---------|
| `n`    | Height  | bottom     | `newY = oldY + oldHeight - newHeight; height = newHeight` |
| `s`    | Height  | top        | `y` unchanged; `height = newHeight` |
| `w`    | Width   | right      | `newX = oldX + oldWidth - newWidth; width = newWidth` |
| `e`    | Width   | left       | `x` unchanged; `width = newWidth` |

This is computed entirely in unrotated local `Bounds` space, independent of
the element's current rotation — rotation only decided *which* handle (and
therefore which formula row) is bound to the north/west input in §2, not how
the formula itself works. If the two inputs happen to resolve to adjacent
handles (the normal case, e.g. north→`e`, west→`s`), each commits
independently with no interaction between them.

Each committed edit posts the existing `updateSceneObjectGeometry` message
(`id, x, y, width, height, beforeX, beforeY, beforeWidth, beforeHeight`) —
same message shape the drag-resize path already sends — so no new C# handler
is needed. `UpdateModernElementGeometry` already exists
(`MainWindow.xaml.cs`, dispatched `~1335`) and already handles history/undo,
`MarkActiveSceneDirty()`, and UI refresh.

### 5. Persistence / export

No schema change: `Bounds` is already read/written for every element. Per
the repo's non-negotiable guardrail, the two floating inputs and any
handle-lookup DOM state are editor-only and must never be serialized into
scene JSON or exported `.sb2` geometry — they're transient DOM, same as the
rotation input.

## Edge cases

- **Rotation exactly 45°/135°/etc. (tie between two handles for
  north/west):** resolved deterministically per the §2 tie-break (prefer
  `n`/`w`).
- **North and west inputs resolve to the same axis** (shouldn't normally
  happen for a rectangle, since `n`/`s` and `e`/`w` are on perpendicular
  axes, but guard anyway): if the extremal-Y and extremal-X handle are
  somehow the same handle, still show two inputs — one for its own axis, and
  fall back to the next-closest handle on the other axis for the second
  input.
- **Group / multi-selection:** menu item not shown (§1 guard); no
  `resizeSceneGroupWithChildren` handling in this iteration.
- **Element resized to near-zero or negative via typed value:** rejected
  (`> 0` check), input reverts, no commit.
- **Editor-artifact leakage:** inputs and lookup state must never appear in
  exported geometry (guardrail from `CLAUDE.md`).

## Testing

- Extend `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`
  (existing convention: assert on the embedded JS/C# source text) for: the
  new `object.resize` command descriptor and its single-element/no-children
  visibility guard, the visual-handle-to-axis resolution logic, and the
  per-handle resize formulas.
- Manual check: rotate an element to 90°/180°/270° and to an arbitrary angle
  (e.g. 40°), open "Redimensionner", confirm the north/west inputs edit the
  dimension that's visually correct on screen, and that committing one
  doesn't corrupt the other's pending display.
