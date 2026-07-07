# Element+ Mirror (Miroir) Context Menu — Design

Date: 2026-07-07
Branch: `feature/element-plus-resize-quick-input`

## Purpose

Let a user mirror a selected Element+ object horizontally or vertically from
the right-click context menu: `Miroir → Verticale` / `Miroir → Horizontale`.

## Background

The scene model already carries a transform-style property end-to-end:
`ScadaElementStyle.Rotation` (`src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs:207`)
is persisted per-element in scene JSON and applied visually via
`wrapper.style.transform = rotate(${Rotation}deg)`
(`src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:~1864`, drag-preview
variant `~2597`). The context menu already has a "Rotation" submenu built in
`BuildContextMenuCommands` (`src/ScadaBuilderV2.App/MainWindow.xaml.cs:3896-3907`)
and dispatched in `ExecuteEditorCommandAsync` (same file, ~4053-4063) via
`UpdateModernElementRotation` (~4946-4970). Mirror follows the same shape:
new `Style` fields, a new context-menu submenu, a new dispatch case, a new
mutation method.

There is no existing flip/mirror/scale logic anywhere in the codebase — this
is net-new, not a refactor of existing behavior.

## Scope

- Single selected Element+ object only, guarded the same way as Rotation
  (`_selectedSceneObjectIds.Count == 1`, `MainWindow.xaml.cs:3877`). Groups and
  multi-selection are out of scope for this feature.
- Two independent toggles: `FlipHorizontal` and `FlipVertical`. Both can be
  active at once (equivalent to a 180° rotation, but stored/toggled
  independently — no attempt to normalize combinations away).
- No drag handle or keyboard shortcut for mirror in this iteration — context
  menu only.

## Design

### 1. Data model

Add two booleans to `ScadaElementStyle`
(`src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs:196-207`), alongside
`Rotation`:

```csharp
bool FlipHorizontal = false,
bool FlipVertical = false,
```

Naming convention: "Horizontal" flips left-right (mirrors across a vertical
axis); "Vertical" flips top-bottom (mirrors across a horizontal axis). This
matches common image-editor terminology and the French labels below map
directly (`Horizontale` → `FlipHorizontal`, `Verticale` → `FlipVertical`).

Persisted automatically wherever `ScadaElementStyle` already round-trips
(scene JSON serialization, undo/redo snapshots) — no separate persistence
work needed since it's a plain record field with a default.

### 2. Context menu

`EditorCommandDescriptor` (`src/ScadaBuilderV2.Application/Commands/EditorCommandDescriptor.cs`)
gains an `IsChecked` flag so the menu can render a checkmark for active
toggles:

```csharp
public sealed record EditorCommandDescriptor(
    string Id,
    string Label,
    string Category,
    bool IsEnabled = true,
    string? DisabledReason = null,
    string? IconKey = null,
    bool IsChecked = false,
    IReadOnlyList<EditorCommandDescriptor>? Children = null);
```

In `BuildContextMenuCommands` (`MainWindow.xaml.cs:3828-3968`), immediately
after the `object.rotation` submenu, add:

```csharp
modernCommands.Add(new EditorCommandDescriptor(
    "object.mirror", "Miroir", "mirror",
    Children:
    [
        new EditorCommandDescriptor("object.mirror.vertical", "Verticale", "mirror",
            IsChecked: currentStyle?.FlipVertical ?? false),
        new EditorCommandDescriptor("object.mirror.horizontal", "Horizontale", "mirror",
            IsChecked: currentStyle?.FlipHorizontal ?? false),
    ]));
```

placed under the same single-selection guard as Rotation.

### 3. Menu rendering (JS)

`renderContextMenuCommands` (`src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:755`)
must render a checkmark indicator when `command.isChecked` is true, on the
menu item itself (e.g. a `✓` prefix or a checked CSS class on the row) —
matching how a native app renders a checkable menu item. This is a small
generic addition to the existing item-rendering code, usable by any future
checkable command, not mirror-specific.

### 4. Dispatch and mutation (C#)

In `ExecuteEditorCommandAsync` (`MainWindow.xaml.cs`, switch starting ~3970),
add:

```csharp
case "object.mirror.vertical":
    ToggleModernElementMirror(message.Id, vertical: true);
    break;
case "object.mirror.horizontal":
    ToggleModernElementMirror(message.Id, vertical: false);
    break;
```

New method `ToggleModernElementMirror(string elementId, bool vertical)`,
modeled on `UpdateModernElementRotation` (~4946-4970): looks up the current
element, flips the relevant boolean —

```csharp
var current = ...; // existing lookup pattern
var updatedStyle = vertical
    ? current.Style with { FlipVertical = !current.Style.FlipVertical }
    : current.Style with { FlipHorizontal = !current.Style.FlipHorizontal };
```

— and commits through the existing `CommitModernElementProperties` path so
the change participates in undo/redo exactly like rotation changes do.

### 5. Canvas rendering (JS)

Everywhere the wrapper transform string is built from `Rotation` — render
path (`MainWindow.WebViewScript.cs:~1864`) and drag-preview path (`~2597`) —
extend it to compose scale:

```js
const scaleX = flipHorizontal ? -1 : 1;
const scaleY = flipVertical ? -1 : 1;
wrapper.style.transform = `rotate(${rotation}deg) scaleX(${scaleX}) scaleY(${scaleY})`;
```

`transform-origin: center center` (already set for rotation) applies
unchanged — scaling around the center is the correct mirror pivot.

### 6. Export (FT100 / `.sb2`)

`Ft100SceneExporter.cs` currently emits CSS incorporating `Style.Rotation`.
Extend the same CSS transform composition to include `scaleX`/`scaleY` from
`FlipHorizontal`/`FlipVertical`, so exported pages render identically to the
editor canvas. This keeps preview/build/export in parity per the project's
non-negotiable guardrail.

## Error handling

No new error states. `ToggleModernElementMirror` follows the exact
not-found/no-op guards already present in `UpdateModernElementRotation` (if
the element id can't be resolved, no-op — consistent with existing rotation
behavior).

## Testing

- Domain: default `ScadaElementStyle` has `FlipHorizontal = false`,
  `FlipVertical = false`; `with` expressions toggle correctly and round-trip
  through scene JSON serialization.
- `WebViewContextMenuScriptTests`: `object.mirror` submenu is present under
  single selection, absent/disabled otherwise (matching Rotation's guard);
  `IsChecked` reflects current style state; checkmark renders in the JS menu
  script tests.
- `EditorHistoryServiceTests`: toggling mirror produces one undo entry per
  toggle, and undo/redo restores the correct `FlipHorizontal`/`FlipVertical`
  value.
- `Ft100SceneExporterTests`: exported CSS transform includes the expected
  `scaleX`/`scaleY` when a flip flag is set, and omits them (or emits `1`)
  when both are false, to avoid needless diff noise in unflipped elements.

## Out of scope / explicitly not doing

- No drag handle for mirroring (rotation has one; mirror does not, per this
  design).
- No multi-selection mirror.
- No normalization of `FlipHorizontal + FlipVertical` into an equivalent
  180° rotation — the two independent booleans are stored and rendered as-is.
