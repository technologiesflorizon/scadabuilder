# Scene Clipboard & Keyboard Shortcuts — Design

Date: 2026-07-06
Branch: `master`

## Purpose

Add Select All, Copy, Cut, and Paste for Element+ scene objects on the main
canvas, and unify them with the already-working Undo/Redo shortcuts under one
shortcut-resolution mechanism, in preparation for a future
(not-implemented-here) settings page that lets users customize key bindings.

## Background

- Selection is already multi-object capable: JS tracks `selectedModernIds`
  (a `Set`, `MainWindow.WebViewScript.cs:304`) with `selectModernElementInDom`,
  `toggleModernElementInSelection`, `clearModernSelection`; C# mirrors it in
  `_selectedSceneObjectIds` / `_activeSelection.SceneObjectIds`
  (`MainWindow.xaml.cs:63`).
- Delete already exists end-to-end: JS keydown detects `Delete`
  (`MainWindow.WebViewScript.cs:~2559`), posts `{type:'deleteSceneObject', id}`,
  handled at `MainWindow.xaml.cs:1354-1357` via `DeleteSelectedSceneObjectsAsync`,
  which builds a `SceneObjectsDeletedAction` (undoable, multi-object capable,
  `src/ScadaBuilderV2.Application/History/SceneObjectsDeletedAction.cs`).
- Undo/Redo already work, but through an ad hoc path: JS keydown checks
  `ctrlKey && key==='z'/'y'` directly (`MainWindow.WebViewScript.cs:~2541`),
  posts `{type:'undo'|'redo'}`, handled by a dedicated `case` in the message
  switch (`MainWindow.xaml.cs:1367-1372`).
- `ScadaElement` (`src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs:494`)
  is a plain, JSON-serializable record that already recurses into `Children`
  for groups — the natural clipboard payload shape.
- No duplicate/clone, no clipboard, and no generic "add objects" undo action
  exist yet on the main canvas. `ElementPlusLibraryDragFormat` (library drag)
  only carries a `.sep` file path, not an inline element payload, and isn't
  reusable here.
- The WPF `KeyBinding`s in `MainWindow.xaml:17-24` exist but the working path
  for canvas shortcuts today is the JS keydown handler, since the WebView2
  canvas holds focus. New shortcuts must go through JS the same way.

## Scope

- Six shortcuts: Select All (Ctrl+A), Copy (Ctrl+C), Cut (Ctrl+X), Paste
  (Ctrl+V), Undo (Ctrl+Z, unchanged behavior), Redo (Ctrl+Y, unchanged
  behavior).
- Clipboard is in-memory, internal to the running app instance (not the OS
  clipboard). It holds exactly one snapshot, overwritten by the next Copy or
  Cut.
- A shortcut-to-command lookup table lives in `ScadaBuilderV2.Application`,
  data-only, so it can later back a settings/customization UI. Building that
  UI is explicitly out of scope for this batch.
- Select All operates on top-level scene objects only; a group is one
  selectable unit, its children are not selected individually (consistent
  with how a plain click on a group already behaves).
- Full arbitrary key rebinding (JS capturing whatever key a user picks) is out
  of scope; JS keeps listening for these six specific combinations.

## Design

### 1. Shortcut resolution (`ScadaBuilderV2.Application.Commands`)

New file `ApplicationShortcut.cs`:

```csharp
public enum ShortcutKey { A, C, V, X, Y, Z }

[Flags]
public enum ShortcutModifiers { None = 0, Control = 1, Shift = 2, Alt = 4 }

public sealed record ApplicationShortcut(string CommandId, ShortcutKey Key, ShortcutModifiers Modifiers);
```

`ShortcutKey`/`ShortcutModifiers` are small app-owned enums, not
`System.Windows.Input.Key`, so `Application` stays WPF-free.

New file `ShortcutRegistry.cs`: holds the default bindings and resolves a
`(key, modifiers)` pair to a command id.

```csharp
public sealed class ShortcutRegistry
{
    // command ids: "selection.select-all", "clipboard.copy", "clipboard.cut",
    // "clipboard.paste", "history.undo", "history.redo"
    public string? Resolve(ShortcutKey key, ShortcutModifiers modifiers);
}
```

Seeded with the six defaults listed in Scope. `Resolve` is pure and unit
tested directly — no WPF, no WebView2. (`Rebind(...)` for a future settings
page is a natural later addition to this class; not built now.)

### 2. Clipboard state (`ScadaBuilderV2.Application`)

New file `Clipboard/SceneClipboard.cs`, sitting next to `Selection/`:

```csharp
public sealed class SceneClipboard
{
    public IReadOnlyList<ScadaElement>? Content { get; private set; }
    public bool HasContent => Content is { Count: > 0 };
    public void Copy(IReadOnlyList<ScadaElement> elements) => Content = elements;
}
```

Pure state, no cloning/re-ID logic here (that's paste-time behavior, see
below, since it depends on generating fresh ids at the moment of insertion).
Not affected by Undo/Redo — undoing a Cut restores the deleted objects to the
scene but does not touch clipboard content, matching normal clipboard
expectations.

### 3. New undo/redo action (`ScadaBuilderV2.Application.History`)

New file `SceneObjectsAddedAction.cs`, the mirror of
`SceneObjectsDeletedAction.cs`:

```csharp
public sealed record SceneObjectsAddedAction(
    string SceneId,
    IReadOnlyList<DeletedSceneObjectSnapshot> AddedObjects) : IEditorHistoryAction
{
    public string Label => "Coller objets";
    public bool CanMergeWith(IEditorHistoryAction next) => false;
    public Task UndoAsync(EditorHistoryContext context); // removes AddedObjects from the scene
    public Task RedoAsync(EditorHistoryContext context); // re-inserts them (same snapshot shape as Deleted)
}
```

Reuses the existing `DeletedSceneObjectSnapshot(Element, ParentElementId,
SiblingIndex)` record as its payload shape (same fields needed: what, where
under which parent, at which sibling index) rather than introducing a
near-duplicate type. `UndoAsync`/`RedoAsync` bodies are the same logic as
`SceneObjectsDeletedAction` with Undo/Redo swapped.

### 4. Message contract (JS ↔ C#)

JS keydown (near the existing Undo/Redo/Delete checks,
`MainWindow.WebViewScript.cs:~2541`) recognizes Ctrl+A/C/V/X/Z/Y and posts:

```js
window.chrome?.webview?.postMessage({ type: 'shortcut', key, ctrlKey: true, shiftKey: event.shiftKey, altKey: event.altKey });
```

C# adds one `case "shortcut"` to the existing message switch
(`MainWindow.xaml.cs:~1373`), maps `message.key` to `ShortcutKey`, calls
`_shortcutRegistry.Resolve(key, modifiers)`, and dispatches on the returned
command id to the handler methods described below. Undo/Redo's existing
`case "undo"` / `case "redo"` are removed in favor of this single path so
there is exactly one shortcut mechanism going forward; their handler methods
(`UndoLastSceneOperationAsync`/`RedoLastSceneOperationAsync`) are unchanged
and simply get called from the new dispatch instead.

### 5. Command behaviors (`ScadaBuilderV2.App`, `MainWindow.xaml.cs`)

These stay in the App layer because they need the WebView2 canvas and the
async scene/history APIs, the same way `DeleteSelectedSceneObjectsAsync` and
`UndoLastSceneOperationAsync` already do.

- **SelectAll**: reads the active scene's top-level `Elements`, selects all of
  their ids (reuses the existing selection update path,
  `SelectModernElement`/equivalent multi-id call), no history entry.
- **Copy**: reads the currently selected top-level elements from the active
  scene, calls `_sceneClipboard.Copy(elements)`. No scene mutation, no
  history entry.
- **Cut**: identical to the existing `DeleteSelectedSceneObjectsAsync` path
  (builds and pushes a `SceneObjectsDeletedAction`) plus one extra step:
  before removing them, snapshot the selected elements into
  `_sceneClipboard.Copy(...)`.
- **Paste**: if `_sceneClipboard.HasContent`, deep-clone each clipboard
  element (and recursively its `Children`) with brand-new ids to avoid
  collisions with anything already in the scene (including the originals, if
  still present), offset each pasted top-level element's position by
  `(+20, +20)` relative to its stored position, insert them into the active
  scene, push a `SceneObjectsAddedAction`, then select the newly pasted
  elements. Pasting the same clipboard content multiple times in a row always
  offsets from the clipboard's stored position (not cumulative from the
  previously pasted copy) — each paste is `original + (20, 20)`, not
  `original + N*(20, 20)`.

### 6. Undo/redo interaction summary

| Action | Undo/redo entry? | Clipboard affected? |
|---|---|---|
| Select All | No | No |
| Copy | No | Sets clipboard |
| Cut | Yes (`SceneObjectsDeletedAction`) | Sets clipboard |
| Paste | Yes (`SceneObjectsAddedAction`) | No (clipboard untouched, can paste again) |
| Undo a Cut | — | Unchanged (still holds the cut content) |

## Edge cases

- **Copy/Cut with nothing selected**: no-op (matches existing Delete's
  behavior when there's no selection).
- **Paste with empty clipboard**: no-op, no history entry.
- **Id collisions**: always regenerate ids for every pasted element and every
  descendant, recursively, since the clipboard may be pasted after the
  original was itself deleted, kept, or duplicated multiple times.
- **Cut then Undo then Paste**: works independently — Undo restores the cut
  elements to the scene (via `SceneObjectsDeletedAction.UndoAsync`), Paste
  separately inserts a fresh copy from the untouched clipboard. The scene can
  end up with both the restored originals and the pasted clones, which is
  expected (same as any standard editor).
- **Export leakage**: pasted elements are regular scene elements once
  inserted; no clipboard/selection state must leak into export geometry
  (existing guardrail, unaffected by this feature).

## Testing

- `Application` unit tests (new, plain xUnit/MSTest, no WPF):
  `ShortcutRegistry.Resolve` for all six defaults and an unknown combination;
  `SceneClipboard.Copy`/overwrite semantics; `SceneObjectsAddedAction`
  Undo/Redo round-trip (mirroring existing `SceneObjectsDeletedAction` test
  coverage).
- `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`: extend with
  source-text assertions (existing convention) for the new `shortcut` message
  post in the JS keydown handler and the new `case "shortcut"` dispatch.
- Manual check: Select All / Copy / Cut / Paste on a single element, on a
  multi-selection, and on a group; confirm Undo/Redo still work after the
  dispatch path change.
