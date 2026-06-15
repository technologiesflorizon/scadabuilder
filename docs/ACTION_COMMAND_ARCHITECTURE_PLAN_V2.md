# SCADA Builder V2 - Action/Command Architecture Plan

Date: 2026-06-15
Status: Approved direction to implement
Document version: `V2.1.1.0030`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0030` | `PENDING` | Normalisation du header documentaire et rattachement a l'arbre documentaire stable. |
| 2026-06-15 | `V2.1.1.0026` | `2b59efb` | Baseline initiale du depot SCADA Builder V2. |

## Revision Log

### V2.1.1.0026

Hardened source-object deletion persistence for decommissioned legacy content.
The unified scene-object delete path now persists selected source `data-id` values even when the source node was not materialized as a `LegacyStatic` scene object. `SceneObjectsDeletedAction` carries those source-only deletions through the same scene-scoped undo/redo stack, so undo clears `RemovedSourceElementIds`, redo restores them, and FT100 export omits the deleted source HTML and copied image assets. The reference `win00002` header scene now records removed source image ids `3`, `4`, and `13`; the FT100 package was regenerated so those images are absent from the current export. Regression coverage protects source-only delete undo/redo and removed-source image omission.

### V2.1.1.0017

Added click-and-drag movement for editable active-scene selections.
The WebView bridge now emits a neutral `moveSelectionBy` gesture for imported source objects and multi-selected scene objects. Pointer movement previews the selected elements, commits one scene snapshot on pointer release, preserves relative offsets, and replays edited imported-object bounds into the preview after refresh. FT100 export now writes data-id based CSS for edited imported-object bounds so exported output follows the active scene geometry. Regression coverage protects the source drag bridge, common history snapshot, and FT100 data-id positioning contract.

### V2.1.1.0016

Fixed context-menu clipping after manual workzone resize.
The WebView context menu and nested submenu placement now uses the WebView viewport bounds rather than the resized scene surface bounds. This keeps right-click actions fully expanded when a scene workzone is made smaller while still clamping the menu into the visible browser viewport. Regression coverage protects viewport-based menu bounds and submenu height behavior.

### V2.1.1.0015

Improved active-scene workzone resize feedback.
The bottom-right preview resize handle now streams preview dimensions back to the Page inspector during drag while still committing a single undoable `CanvasSize` change on pointer release. Page width and height fields now apply valid dimension changes automatically after a short debounce, and the Page tab is scrollable so the existing page-property apply button remains reachable. Regression coverage protects live preview dimension messages, on-change dimension application, and the shared canvas-size DOM update path.

### V2.1.1.0014

Added manual active-scene workzone resize from the WebView preview.
The preview now injects an editor-only bottom-right resize handle into the active scene surface. Dragging the handle resizes the scene surface live in the WebView and commits one undoable `CanvasSize` mutation to the active scene on pointer release. Regression coverage protects the handle, bridge message, dirty/history update, and canvas-size replay path.

### V2.1.1.0013

Fixed active-scene canvas-size replay in the WebView preview.
Scene page dimensions are now reapplied during active-scene preview refresh before modern Element+ overlays render, and the resize script updates the legacy page CSS variables plus the modern overlay layer so reopened scenes use the persisted `CanvasSize`. Regression coverage protects preview refresh ordering and the canvas-size script contract.

### V2.1.1.0012

Implemented the first page-manifest and object-action contract slice.
The domain model now includes page type, structured scene background, action definitions, and object-owned event bindings. Scene save enriches the project manifest with page type, dimensions, and background metadata for Django consumption. The page inspector exposes type, width, height, background color, and advanced background CSS fields. FT100 export now writes a Django-readable `manifest.json`, emits structured background CSS, and adds a minimal runtime bridge for object-owned event bindings such as `click -> navigate`. Regression coverage protects page manifest persistence, object event persistence, export CSS, and manifest output.

### V2.1.1.0011

Completed the active-version refactor slice for selection and export naming.
The main window now stores source-object and scene-object selection through one `ActiveSelectionState` owner while preserving the existing polymorphic delete path and scene-scoped undo/redo stack. The WebView bridge now exposes the neutral `window.scadaSceneEditor` API for new calls, with `window.scadaLegacyExtraction` retained only as a compatibility alias. FT100 export internals now use source HTML naming, new exports emit `ft100-source-layer`, and the exporter keeps legacy layer CSS selectors only as compatibility coverage. Regression tests were run after each tranche and updated to protect the neutral naming contracts.

### V2.1.1.0010

Neutralized the context-menu command surface.
The WebView and WPF context bridge now emit `source.*` commands for imported source object actions and `object.*` commands for scene object actions. Existing `legacy.*` and `element-plus.*` ids remain accepted as compatibility aliases inside the command dispatcher, but the generated user interaction surface now uses `source.convert-to-element-plus.*`, `source.mask`, `source.group-to-element-plus`, `source.open-in-element-studio`, `object.delete`, and `object.ungroup`. WebView message targets were likewise moved from `legacy`/`modern` to `source`/`object` for new emissions. Regression contracts were updated to protect the neutral command ids while keeping the unified scene-object deletion path.

### V2.1.1.0009

Neutralized the active scene inventory identity surface.
The selection inventory now uses `source:` keys for imported source objects and `object:` keys for scene objects instead of `legacy:` and `v2:` prefixes, and user-facing inventory labels now distinguish `Source` from `Object` without presenting the editor as two product versions. Selection list handling in the main window resolves those neutral source categories back to the same active scene delete and edit paths. Regression coverage was updated for source/object ordering, multi-object selection, hidden source suppression, and user-facing source selection summaries.

### V2.1.1.0008

Unified active-scene deletion across imported source objects and Element+ objects.
The main editor now routes source-object, Element+, mixed selection, and context-menu delete requests through one scene-object deletion path backed by `SceneObjectsDeletedAction`. Deleted source ids are persisted on the active scene as removed source ids, undo restores the object snapshots and clears those ids, and redo removes the same scene objects again. FT100 export now suppresses and removes deleted source elements before asset copying, so deleted source images do not reappear visually or as copied `images/` assets. Added regression coverage for generic scene-object delete undo/redo, removed-source persistence, and export omission of removed source images.

### V2.1.1.0007

Further reduced scene background loading flash by synchronizing the native WebView2 background.
The preview loader now keeps the WebView hidden while refreshing an already-loaded URL, and the WebView2 `DefaultBackgroundColor` is updated from the active scene background before navigation and during background color application. This covers the native WebView compositor paint that can occur before DOM/CSS background rules are observable. Regression coverage now checks the same-URL hidden refresh path and native WebView background assignment.

### V2.1.1.0006

Removed the visible black preview flash during scene background loading.
The WPF preview surface now paints the active scene background before WebView navigation starts, the WebView is kept hidden during new document navigation, and it is shown only after the legacy extraction script plus active scene refresh have applied. The document-created background script now installs CSS immediately and reapplies the scene background during ready-state, DOMContentLoaded, and load events so late legacy CSS does not briefly expose a fallback background. Added regression coverage for the no-flash preview load contract.

### V2.1.1.0005

Completed the remaining scene-mutation migration to the common per-scene undo/redo history.
Element+ insertion, Element+ library instantiation, legacy group-frame creation, Element+ ungroup, and legacy text override editing now record `SceneSnapshotChangedAction` entries in the active scene tab history. Automatic legacy inventory materialization remains non-undoable because it is load/runtime normalization rather than a user command. Added regression coverage for scene snapshot undo/redo and common-history wiring across the remaining mutation paths.

### V2.1.1.0004

Added common-history undo/redo coverage for SCADA Builder V2 Element+ deletion.
Modern Element+ deletion now records `ModernElementsDeletedAction` snapshots in the active scene tab history instead of being a one-way scene mutation. Delete undo restores top-level elements and grouped child elements to their original parent when the parent still exists, while redo removes the same Element+ ids through the scene model. The deletion path filters nested selections so deleting both a group and a child does not duplicate restored geometry. Added regression coverage for top-level delete undo/redo, grouped child restore, and common-history wiring from the modern delete commands.

### V2.1.1.0003

Added common-history undo/redo coverage for SCADA Builder V2 Element+ property edits.
The WPF property panel and the WebView Element+ editor now record before/after `ScadaElement` snapshots through `ModernElementChangedAction` in the active scene tab history. Consecutive edits on the same Element+ object merge into one undo step when each edit starts from the previous result, keeping text-field edits practical while preserving redo support. Added regression coverage for Element+ property undo/redo, merge behavior, no-op filtering, and common-history wiring from both property-editing paths.

### V2.1.1.0002

Added common-history undo/redo coverage for SCADA Builder V2 Element+ geometry edits.
Modern element drag, resize handles, and keyboard movement now post one committed before/after geometry snapshot to the WPF host, and the active scene tab records that mutation as a `ModernElementBoundsChangedAction` in the shared per-scene `EditorHistoryService`. Undo and redo restore Element+ bounds through the scene model instead of relying on WebView-only state. Added regression coverage for before/after geometry messages, common-history wiring, and direct bounds undo/redo behavior.

### V2.1.1.0001

Implemented scene-scoped common undo/redo history for SCADA Builder V2 editor mutations.
Each open scene tab now owns one in-memory `EditorHistoryService`; background color changes, legacy selection deletion, and legacy-to-Element+ conversion use that shared per-scene history instead of separate operation-specific stacks. Redo is wired for the main editor ribbon. The WebView preview now prepares the scene background color with a document-created script before navigation, so the persisted scene background is applied before the legacy preview fallback paint. Added regression coverage for history behavior, scene independence, redo stack clearing, background undo/redo, common-history wiring, and initial background preparation.

### V2.0.3.0029

Fixed FT100 export text fidelity for legacy French content.
The exporter now repairs common Wonderware/ArchestrA mojibake sequences before writing UTF-8 output, applies local legacy Text overrides into the exported legacy HTML, and keeps browser-openable relative export paths unchanged. Added regression coverage for French accents, degree symbols, and local legacy text override export.

### V2.0.3.0028

Added first FT100 folder export from SCADA Builder V2.
The File ribbon Build button originally opened a destination folder picker and exported the active scene as a browser-openable folder containing a now-deprecated `index.html`, `css/<scene>.css`, `images/`, and a short integration README. This entry is historical; current FT100/TF100Web package documentation deprecates `index.html` and uses manifest-driven `<page-id>.html` output.

### V2.0.3.0027

Removed Studio Element+ workzone name overlays.
Imported Element+ structure names such as `Element001` and `Element002` remain available in the Element list and metadata, but Studio Element+ no longer renders those names as visual boxes over imported geometry in the workzone.

### V2.0.3.0026

Fixed Element+ SVG visibility for library previews and scene instances.
SCADA Builder V2 now normalizes SVG viewBox metadata when legacy geometry was saved with absolute Wonderware coordinates, and custom Element+ scene rendering removes text-input padding so the SVG fills the selection bounds.

### V2.0.3.0025

Hardened the SCADA Builder V2 Element+ library UX.
Library tiles must render a real preview from `.sep` SVG/HTML markup instead of only initials, and double-clicking a tile must instantiate that Element+ component into the active scene without relying on WebView drag/drop.

### V2.0.3.0024

Added the SCADA Builder V2 library instantiation interaction.
The Element+ library is displayed as square icon tiles with file names, and `.sep` components can be dragged from the library into the active scene to create a selectable, movable Element+ instance.

### V2.0.3.0023

Hardened Studio Element+ `.sep` Save As directory resolution.
The Save As dialog must never infer its initial directory from a legacy source page path. It must resolve, in order, the explicit target library path, the launched V2 project import location, the V2 repository project library, then the controlled user-documents fallback.

### V2.0.3.0022

Adjusted the Studio Element+ launcher contract for development builds.
SCADA Builder V2 may launch a co-located Studio Element+ executable for installed/published deployments, but in source development it must use `dotnet run --project` instead of stale `bin/Debug` or `bin/Release` Studio executables.

### V2.0.3.0021

Added an explicit target library path to the `.ft1` Studio Element+ import contract.
Studio Element+ must use this target path before trying to infer a library from the legacy source page or import package location, because legacy pages can live under a different project tree.

### V2.0.3.0020

Added the SCADA Builder V2 Element+ library refresh contract:

1. Studio Element+ `.sep` output is expected in the active project library folder: `projects/<project-id>/library/elements/`.
2. SCADA Builder V2 must load all valid `.sep` component packages from that folder into the Librairie tab.
3. SCADA Builder V2 must watch that folder for `.sep` create/change/delete/rename events and refresh the visible library without requiring an application restart.
4. Invalid `.sep` files must not block the full library; they are reported as diagnostics while valid components remain visible.

## 1. Problem

The current editor behavior is too generic.

Examples:

1. The legacy context menu always proposes broad actions.
2. The menu does not know whether the selection is a legacy Text, a legacy shape, an Element+ Text, an input, the scene background, or a mixed selection.
3. Several UI surfaces call direct handlers instead of a shared command.
4. Conversion actions are not sufficiently object-oriented.
5. Adding a new object type risks adding more conditional UI code.

This must stop before more conversion types are added.

## 2. Core Decision

SCADA Builder V2 must use object-oriented editor commands.

Every meaningful action must be represented by a command object. UI surfaces may display and invoke commands, but they must not own command rules.

Command surfaces include:

1. Ribbon.
2. Top menus.
3. Left toolbar.
4. Right context panels.
5. Element list.
6. Canvas context menu.
7. Keyboard shortcuts.
8. Floating object editors.

All surfaces must ask the same command registry:

```text
Which commands are available for this SelectionContext?
```

## 3. Target Domains

### 3.1 Application Command Domain

Location:

```text
src/ScadaBuilderV2.Application/Commands
```

Responsibilities:

1. Define command metadata.
2. Define enablement rules.
3. Execute application intent.
4. Return command result and diagnostics.
5. Provide undo/redo metadata later.

### 3.2 Selection Context Domain

Location:

```text
src/ScadaBuilderV2.Application/Selection
```

Responsibilities:

1. Describe current selection in editor terms.
2. Normalize legacy and Element+ selections into one context model.
3. Identify primary object type.
4. Identify whether a command can run against single selection, multi-selection, background, or mixed selection.

### 3.3 Editor Bridge Domain

Location:

```text
src/ScadaBuilderV2.App/EditorBridge
```

Responsibilities:

1. Translate WPF/WebView events into application command requests.
2. Translate command descriptors into UI menu payloads.
3. Keep WebView JavaScript thin.
4. Keep `MainWindow.xaml.cs` from becoming the application layer.

### 3.4 Domain Model

Location:

```text
src/ScadaBuilderV2.Domain
```

Responsibilities:

1. Own official project/scene/Element+ models.
2. Keep legacy ids as trace metadata only.
3. Expose domain operations that commands can call.

## 4. Command Class Shape

Use an abstract base command for shared behavior.

```csharp
public abstract class EditorCommand
{
    public abstract string Id { get; }
    public abstract string Label { get; }
    public virtual string Category => "general";
    public virtual string? IconKey => null;
    public virtual string? DefaultShortcut => null;
    public virtual CommandUndoPolicy UndoPolicy => CommandUndoPolicy.NotUndoable;

    public abstract bool CanExecute(EditorCommandContext context);

    public abstract Task<EditorCommandResult> ExecuteAsync(EditorCommandContext context);
}
```

Decision:

1. Commands are asynchronous from the start.
2. Existing synchronous commands are kept only as temporary adapters.
3. Any command that touches WebView, file IO, save/load, rendering, or project store must use `ExecuteAsync`.

Specialized bases may be introduced when they remove duplication:

```csharp
public abstract class SelectionCommand : EditorCommand
public abstract class LegacySelectionCommand : SelectionCommand
public abstract class ElementPlusCommand : SelectionCommand
public abstract class SceneCommand : EditorCommand
public abstract class ConversionCommand : LegacySelectionCommand
```

This allows common validation in a base class and override only where needed.

The current `IApplicationCommand` can remain temporarily, but the target runtime interface is `EditorCommand`.

## 5. Command Context

```csharp
public sealed class EditorCommandContext
{
    public required EditorSessionState Session { get; init; }
    public required SelectionContext Selection { get; init; }
    public required ISceneMutationService SceneMutations { get; init; }
    public required ILegacyViewerBridge LegacyViewer { get; init; }
    public required IModernSceneRenderer Renderer { get; init; }
}
```

The command receives services instead of reaching into `MainWindow`.

Rule:

```text
Commands do not know WPF controls.
WPF controls do not know command internals.
```

## 6. Selection Context Shape

```csharp
public sealed record SelectionContext(
    SelectionScope Scope,
    IReadOnlyList<SelectedObjectRef> Items,
    SelectedObjectRef? Primary);
```

```csharp
public enum SelectionScope
{
    None,
    SceneBackground,
    LegacyOnly,
    ElementPlusOnly,
    Mixed
}
```

```csharp
public sealed record SelectedObjectRef(
    string Id,
    ObjectSource Source,
    ObjectKind Kind,
    string DisplayName,
    SceneBounds? Bounds,
    string? Text,
    LegacySourceTrace? LegacySource);
```

Selection ids must be typed, not free strings.

```csharp
public sealed record SelectionKey(ObjectSource Source, string Id)
{
    public string Canonical => Source == ObjectSource.Legacy
        ? $"legacy:{Id}"
        : Source == ObjectSource.ElementPlus
            ? $"v2:{Id}"
            : $"scene:{Id}";
}
```

Rule:

```text
Only SelectionKey builds canonical ids.
No UI code manually prefixes ids with legacy: or v2:.
```

```csharp
public enum ObjectSource
{
    Legacy,
    ElementPlus,
    Scene
}
```

```csharp
public enum ObjectKind
{
    Unknown,
    Text,
    Shape,
    Image,
    InputText,
    InputNumeric,
    Group,
    Background
}
```

## 7. Command Registry

The registry must be the single source of truth.

```csharp
public sealed class EditorCommandRegistry
{
    public void Register(EditorCommand command);
    public EditorCommand? Find(string id);
    public IReadOnlyList<CommandDescriptor> GetAvailableCommands(EditorCommandContext context, CommandSurface surface);
}
```

Command execution must pass through one dispatcher:

```csharp
public sealed class EditorCommandDispatcher
{
    public Task<EditorCommandResult> ExecuteAsync(string commandId, EditorCommandArgs args);
}
```

The dispatcher is the future hook for:

1. Command logging.
2. Undo/redo.
3. Dirty state.
4. Re-render requests.
5. Diagnostics.
6. Keyboard shortcut execution.

Command descriptors are UI-safe:

```csharp
public sealed record CommandDescriptor(
    string Id,
    string Label,
    string Category,
    string? IconKey,
    string? Shortcut,
    bool IsEnabled,
    string? DisabledReason);
```

The WebView context menu receives descriptors, not hardcoded HTML actions.

## 8. Command Surfaces

```csharp
public enum CommandSurface
{
    Ribbon,
    TopMenu,
    ContextMenu,
    ElementList,
    PropertyPanel,
    FloatingEditor,
    Keyboard
}
```

The same command can decide whether it appears on a surface:

```csharp
public virtual bool SupportsSurface(CommandSurface surface) => true;
```

Example:

1. `scene.background.edit` appears on background context menu and property panel.
2. `legacy.text.convert-to-element-plus` appears only on context menu and Element panel when a legacy Text selection exists.
3. `element.delete` appears for Element+ selections, not legacy selections.
4. `legacy.hide` appears for legacy selections, not Element+ selections.

## 9. Initial Command Set To Implement

### Selection

1. `selection.clear`
2. `selection.lock`
3. `selection.unlock`

### Legacy

1. `legacy.hide`
2. `legacy.delete-from-view`
3. `legacy.restore-hidden`
4. `legacy.inspect-source`

### Conversion

1. `legacy.text.convert-to-element-plus`

Rules:

1. Available only when at least one selected object is legacy Text-like.
2. If mixed selection contains supported and unsupported items, convert supported items and report ignored items.
3. If no supported item exists, command must be disabled with a reason before the user clicks.

### Element+

1. `element-plus.delete`
2. `element-plus.duplicate`
3. `element-plus.open-properties`
4. `element-plus.edit-text`

### Scene

1. `scene.background.edit`
2. `scene.properties.open`

## 10. Context Menu Rules

The context menu must never be static.

Flow:

1. User right-clicks canvas.
2. WebView sends a context request to WPF:

```json
{
  "type": "contextMenuRequest",
  "x": 100,
  "y": 240,
  "targetId": "Text29",
  "targetSource": "Legacy"
}
```

3. WPF updates or confirms selection.
4. WPF builds `SelectionContext`.
5. Registry returns available commands for `CommandSurface.ContextMenu`.
6. WPF sends descriptors back to WebView.
7. WebView renders only those descriptors.
8. Click on an item sends `executeCommand` with `commandId`.
9. WPF executes the command through registry.

The WebView no longer owns action rules.

## 11. Why Not Keep Simple Handlers

Direct handlers are acceptable for prototypes, but they are now blocking scale.

Problems with direct handlers:

1. Rules are duplicated.
2. UI labels drift from behavior.
3. Context menus become generic.
4. Test coverage is difficult.
5. New object types require changes in many places.
6. Commands cannot be reused by keyboard/ribbon/menu.

The command model solves those issues.

## 12. Object-Oriented Conversion Design

Conversion commands should derive from a base:

```csharp
public abstract class LegacyToElementPlusConversionCommand : ConversionCommand
{
    protected abstract bool CanConvert(SelectedObjectRef selected);
    protected abstract ScadaElement Convert(SelectedObjectRef selected, ConversionContext context);
}
```

Text conversion:

```csharp
public sealed class ConvertLegacyTextToElementPlusCommand : LegacyToElementPlusConversionCommand
{
    public override string Id => "legacy.text.convert-to-element-plus";
    public override string Label => "Conversion Element+";

    protected override bool CanConvert(SelectedObjectRef selected)
    {
        return selected.Source == ObjectSource.Legacy && selected.Kind == ObjectKind.Text;
    }
}
```

Later conversion types:

1. `ConvertLegacyShapeToElementPlusCommand`
2. `ConvertLegacyInputTextToElementPlusCommand`
3. `ConvertLegacyInputNumericToElementPlusCommand`
4. `ConvertLegacyImageToElementPlusCommand`
5. `ConvertLegacyGroupToElementPlusCommand`

## 13. Migration Plan

### Phase 1 - Foundation

1. Add `SelectionContext`, `SelectedObjectRef`, `ObjectSource`, `ObjectKind`, `SelectionScope`.
2. Add `EditorCommand`, `EditorCommandContext`, `EditorCommandResult`.
3. Add `EditorCommandRegistry`.
4. Add `EditorCommandDispatcher`.
5. Keep existing handlers operational while introducing the new path.
6. Make existing WPF handlers call the dispatcher instead of deleting them immediately.

This is a migration by adapters. The old UI entry points stay alive until each action has moved behind a command.

### Phase 2 - Context Menu

1. Replace static WebView context menu HTML with dynamic descriptors.
2. Add `contextMenuRequest` and `executeCommand` messages.
3. Route `Conversion Element+`, `Masquer`, `Supprimer de la vue`, `CSS fond`, and `Effacer selection` through registry.
4. Remove duplicated menu decisions from JavaScript.
5. Keep the menu rendered in WebView HTML for z-order reliability, but make WPF own the list of commands.

### Phase 3 - Element+ Text Conversion

1. Move conversion logic from `MainWindow.xaml.cs` into `ConvertLegacyTextToElementPlusCommand`.
2. Add conversion diagnostics.
3. Add disabled reasons when no selected object is convertible.
4. Ensure converted Element+ is selected and visible in the element list.
5. Ensure duplicate conversion of the same source is detected or reported clearly.

### Phase 4 - WPF Surfaces

1. Bind ribbon buttons to command descriptors.
2. Bind Element panel actions to registry.
3. Bind keyboard shortcuts to command ids.
4. Keep UI display labels from registry metadata.

### Phase 5 - Cleanup

1. Remove obsolete direct handlers from `MainWindow.xaml.cs`.
2. Move bridge logic into `EditorBridge`.
3. Add a command execution log for diagnostics.
4. Prepare undo/redo command history.

Cleanup begins only after tests prove that WPF buttons and WebView menu execute the same command ids.

## 14. Test Plan

### Unit Tests

1. Legacy Text selection returns `legacy.text.convert-to-element-plus`.
2. Legacy Shape selection does not return Text conversion.
3. Element+ Text selection returns Element+ commands, not legacy commands.
4. Background context returns scene background commands.
5. Mixed selection returns only commands valid for mixed selection.
6. Disabled command includes a reason.
7. Convert legacy Text creates Element+ Text with trace metadata.
8. Conversion does not use legacy id as primary Element+ id.
9. `SelectionKey` normalizes ids without double-prefixing `legacy:` or `v2:`.
10. Unknown command id returns a clear failure result.
11. Command registry rejects or reports duplicate command ids.

### Integration Tests

1. WebView context request maps to expected command descriptors.
2. `executeCommand` invokes the same command as WPF button.
3. Element list and scene selection remain synchronized after conversion.
4. Save/reload preserves converted Element+.
5. WPF button and WebView context menu execute the same command id.
6. Command result marks whether render, save state, or selection refresh is required.

### Smoke Tests

1. Open `AMR_REF_SCADA`.
2. Open `win00008`.
3. Select legacy Text.
4. Right-click and verify only relevant commands appear.
5. Run `Conversion Element+`.
6. Verify Element+ appears in scene and properties.

## 15. Multi-Agent Implementation Split

### Agent A - Command Architecture

Ownership:

```text
src/ScadaBuilderV2.Application/Commands
tests/ScadaBuilderV2.Tests/*Command*Tests.cs
```

Tasks:

1. Add base command classes.
2. Add command descriptor model.
3. Add registry filtering by surface/context.

### Agent B - Selection Context

Ownership:

```text
src/ScadaBuilderV2.Application/Selection
tests/ScadaBuilderV2.Tests/*Selection*Tests.cs
```

Tasks:

1. Add `SelectionContext`.
2. Add mapping from legacy inventory and Element+ scene objects.
3. Add object-kind detection helpers.

### Agent C - UI Bridge

Ownership:

```text
src/ScadaBuilderV2.App/EditorBridge
src/ScadaBuilderV2.App/MainWindow.xaml.cs
```

Tasks:

1. Add WebView context menu request/response protocol.
2. Render dynamic context menu descriptors.
3. Route `executeCommand`.

### Agent D - Conversion Command

Ownership:

```text
src/ScadaBuilderV2.Application/Commands/Conversion
src/ScadaBuilderV2.Domain/Scenes
tests/ScadaBuilderV2.Tests/*Conversion*Tests.cs
```

Tasks:

1. Move Text conversion into command object.
2. Add conversion diagnostics.
3. Add trace metadata tests.

### Orchestrator

Ownership:

```text
docs
solution integration
final regression
```

Tasks:

1. Keep architecture consistent.
2. Resolve integration conflicts.
3. Run full tests.
4. Keep versioning updated.

## 16. Acceptance Criteria

The refactor is acceptable only when:

1. The context menu no longer shows generic actions.
2. Every displayed action comes from the command registry.
3. `Conversion Element+` appears only when a selection can convert.
4. The same command can be invoked from context menu and WPF panel.
5. MainWindow no longer owns conversion business rules.
6. Tests prove command availability by selection type.
7. Adding a new conversion type requires a new command class, not edits across every UI surface.

## 17. Living Plan Rules

This plan is organic.

Rules:

1. If implementation reveals a contradiction, update this plan before continuing implementation.
2. If current UI behavior conflicts with this plan, the plan is the source of truth unless the user approves a plan change.
3. If the plan is too vague to implement safely, add a decision record instead of guessing silently.
4. If an action is unclear or redundant, disable or remove it from the context surface until its domain meaning is defined.
5. Document temporary compromises explicitly and attach a cleanup condition.
6. Every new command must declare its selection rules before it appears in any UI.

Guardrail:

1. When a contradiction is detected, implementation work must stop.
2. The contradiction must be written in this plan under `Active Contradictions And Decisions`.
3. The plan document version must be bumped.
4. The user must be informed of the contradiction and the proposed decision.
5. Implementation resumes only after the plan is clarified enough to remove the ambiguity.
6. No code path may be added as a workaround while the plan contradiction is unresolved.

This guardrail applies to orchestrator and sub-agent work.

## 18. Active Contradictions And Decisions

### 18.1 Static Context Menu vs Context-Aware Commands

Contradiction:

The implementation still shows static context actions for legacy selections:

1. `CSS fond`
2. `Conversion Element+`
3. `Jumeler et extraire`
4. `Masquer la selection`
5. `Supprimer de la vue`
6. `Effacer la selection`

This contradicts the command architecture.

Decision:

The context menu must be generated from `SelectionContext` and `EditorCommandRegistry`.

Immediate rule:

1. Legacy Text selection shows only legacy Text commands.
2. Scene background commands appear only when the background is the target.
3. Unsupported actions do not appear.

### 18.2 `CSS fond` On Legacy Element

Contradiction:

`CSS fond` is offered when right-clicking a legacy Text element.

Decision:

`scene.background.edit` is available only for `SelectionScope.SceneBackground`.

It must not appear for:

1. Legacy Text.
2. Legacy Shape.
3. Element+ Text.
4. Element+ inputs.
5. Mixed selection.

### 18.3 `Jumeler et extraire`

Contradiction:

`Jumeler et extraire` remains in the context menu even though the extraction workflow will be refactored.

Decision:

Remove `Jumeler et extraire` from the context menu until the extraction candidate workflow is redesigned.

The future command must be explicit, for example:

```text
legacy.candidate.create
legacy.candidate.group
legacy.candidate.promote-to-element-plus
```

### 18.4 `Masquer` vs `Supprimer De La Vue`

Contradiction:

`Masquer la selection` and `Supprimer de la vue` are currently redundant from the user perspective, and neither has a clear domain model.

Decision:

Use one non-destructive command first:

```text
legacy.mask
```

Domain meaning:

1. The legacy object remains in the legacy source.
2. The working view stores a mask state for the source object.
3. The object can be unmasked later.
4. Mask state belongs to the V2 work session or scene overlay, not to the raw legacy file.

Remove or disable `legacy.delete-from-view` until a distinct domain meaning is approved.

### 18.5 Mask State Ownership

Contradiction:

The current implementation stores hidden legacy ids only as UI session state. The user expects masked to behave like an object property that can be unmasked.

Decision:

Introduce explicit legacy overlay state in the V2 scene/workspace.

Target shape:

```csharp
public sealed record LegacyObjectOverlay(
    string SourceElementId,
    bool IsMasked,
    string? Note);
```

This makes mask/unmask persistent and inspectable without modifying legacy source files.

### 18.6 `Effacer La Selection`

Contradiction:

`Effacer la selection` is offered as an object context action and overlaps with object commands.

Decision:

`selection.clear` remains a valid general command, but it must be visually separated from object mutation commands.

Context menu rule:

1. It can appear in a low-priority `Selection` group.
2. It must never be confused with `delete`, `mask`, or `convert`.
3. It must execute through the shared command dispatcher.

### 18.7 Conversion Element+

Contradiction:

`Conversion Element+` is displayed for legacy selections before the registry proves that the selected object is convertible.

Decision:

`legacy.text.convert-to-element-plus` appears only when `SelectionContext` contains at least one convertible legacy Text object.

If a selected legacy Text cannot be converted, the menu must either hide the command or show it disabled with a reason.

### 18.8 Runtime Object Entity Requirement

Contradiction:

The current conversion path is still too close to a DTO/id workflow. It receives legacy ids and message snapshots, then decides what to do from generic UI state. This contradicts the object-oriented command direction.

Decision:

Detected legacy objects and created Element+ objects must exist as real runtime entity classes in the editor domain.

Minimum target model:

```csharp
public abstract class EditorObject
{
    public string RuntimeId { get; }
    public string DisplayName { get; }
    public EditorObjectKind Kind { get; }
}

public sealed class LegacyDetectedObject : EditorObject
{
    public LegacySourceTrace Source { get; }
    public LegacyObjectGeometry Geometry { get; }
    public LegacyObjectStyle Style { get; }
}

public abstract class ElementPlusObject : EditorObject
{
    public ElementPlusObjectType ElementType { get; }
}
```

Command and converter rules:

1. `SelectionContext` carries references or stable object handles to `EditorObject` instances, not only string ids.
2. Commands receive selected runtime objects through the shared command context.
3. Conversion receives the source object reference/snapshot plus explicit conversion options.
4. UI message DTOs remain transport objects only. They must not become the domain model.

### 18.9 Explicit Conversion Target

Contradiction:

Auto-detection alone blocks valid conversions. Example: `win00008` / `Text27` is a legacy Text object visually showing `####`, but it represents a numeric display. Converting it blindly to Element+ Text is wrong, and refusing conversion because detection is uncertain is also wrong.

Decision:

`Conversion Element+` must allow the user to choose the target Element+ type.

Auto-detection may suggest a default target, but must never be the only path.

Initial target choices:

1. `Element+ Texte`
2. `Element+ Affichage numerique`
3. `Element+ Champ d'entree texte`

Numeric rule:

There must not be two different numeric runtime object types for display and input. `Affichage numerique` and numeric entry are the same domain object with a `ReadOnly` option.

1. `ReadOnly = true`: display-only numeric value.
2. `ReadOnly = false`: editable numeric value.
3. UI labels may expose this as display vs input, but the runtime object class must remain the same.

Rule for `Text27`:

1. Source type remains `LegacyDetectedObject` with legacy kind `Text`.
2. Suggested target may be `Element+ Affichage numerique` because the text is placeholder-like numeric content.
3. The user can override the target before conversion.
4. The converter creates an undo snapshot containing the source object and the created Element+ object.

Conversion lifetime:

1. After conversion, the legacy object no longer exists in the active runtime scene.
2. Undo can restore the legacy object and remove the converted Element+ object while the application session remains open.
3. This undo cache is not a persistent legacy dependency.
4. A conversion trace is useful for audit, diagnostics, and undo, but it is not required in the exported/runtime FT100 model.

Required converter shape:

```csharp
public interface IElementPlusConverter
{
    bool CanConvert(EditorObject source, ElementPlusObjectType targetType);
    ConversionPreview Preview(EditorObject source, ElementPlusObjectType targetType);
    ElementPlusObject Convert(EditorObject source, ElementPlusObjectType targetType, ConversionOptions options);
}
```

## 19. Immediate Implementation Order After This Update

1. Add runtime `EditorObject` entities for detected legacy objects, Element+ objects, and scene background.
2. Add `SelectionContext` and typed object references/handles.
3. Add `EditorCommandDescriptor` and a registry filter for context menu commands.
4. Replace remaining static command assumptions with descriptor-driven command availability.
5. Refactor `Conversion Element+` so the command opens target selection before conversion.
6. Implement first conversion targets:
   - `Element+ Texte`
   - `Element+ Affichage numerique`
   - `Element+ Champ d'entree texte`
7. Implement `ReadOnly` on numeric Element+ objects instead of a separate numeric input object type.
8. Implement conversion undo cache for the current application session.
9. Implement only these first shared commands:
   - `legacy.convert-to-element-plus`
   - `legacy.mask`
   - `selection.clear`
   - `scene.background.edit`
10. Add a legacy overlay model for mask/unmask outside the conversion flow.
11. Add tests proving command availability, target selection, conversion behavior, and undo behavior by runtime object type.

## 20. Implementation Status

### V2.0.2.0017

Implemented first adapter slice:

1. Added `EditorCommandDescriptor`.
2. Replaced the hardcoded WebView context menu with descriptor-driven menu rendering.
3. Added context menu request flow from WebView to WPF.
4. Added shared command ids for:
   - `scene.background.edit`
   - `legacy.text.convert-to-element-plus`
   - `legacy.mask`
   - `selection.clear`
5. Removed `Jumeler et extraire` from the WebView context menu.
6. Removed `Supprimer de la vue` from the WebView context menu.
7. Kept existing WPF handlers as temporary adapters.

Still pending:

1. Full `SelectionContext` class.
2. Typed `SelectionKey`.
3. Full `EditorCommandRegistry` filtering implementation.
4. Persistent legacy overlay state for mask/unmask.
5. Unit tests for command availability by selection type.

### V2.0.2.0018

Fixed context menu selection regression:

1. Right-click no longer opens the legacy context menu from `pointerdown`.
2. The menu is opened only from the native `contextmenu` event.
3. Duplicate window-level `contextmenu` registration was removed.
4. A guard prevents duplicate handling of the same `contextmenu` event.
5. Added regression tests for the WebView context menu script contract.

### V2.0.2.0019

Updated the command/conversion plan after the `Text27` analysis:

1. Added the requirement that detected legacy objects and created Element+ objects are real runtime entities.
2. Clarified that conversion must receive the selected object reference/snapshot, not only a legacy id.
3. Added explicit conversion target selection so auto-detection suggests but does not block conversion.
4. Documented `Text27` as a legacy Text source that should be convertible to `Element+ Affichage numerique`.
5. Updated the immediate implementation order around runtime object entities and target-aware conversion.

### V2.0.2.0020

Updated the conversion plan from user decisions:

1. Numeric display and numeric input are one runtime object type.
2. The only behavioral difference is the `ReadOnly` option.
3. `Conversion Element+` target selection is approved.
4. After conversion, the legacy object is removed from the active runtime scene.
5. Undo must restore the legacy object from a session cache until the application closes.
6. Conversion trace is required for undo/diagnostics, but not for exported FT100 runtime output.

### V2.0.2.0021

Implemented the first target-aware Conversion Element+ slice:

1. Added runtime editor object classes for detected legacy objects and Element+ runtime objects.
2. Added `ElementPlusLegacyConverter`.
3. Added explicit conversion targets:
   - `Texte`
   - `Affichage numerique`
   - `Champ d'entree texte`
   - `Champ numerique editable`
4. Right-click `Conversion Element+` now exposes target-specific commands.
5. `Affichage numerique` converts to the same numeric runtime kind as numeric input with `ReadOnly = true`.
6. Conversion hides/removes the legacy object from the active scene view and records a session undo snapshot.
7. The `Edition > Undo` command can restore the converted legacy object during the current application session.
8. Added regression tests for numeric conversion, explicit conversion targets, and conversion undo wiring.

### V2.0.2.0022

Corrected the right-click context menu UI for Conversion Element+:

1. `Conversion Element+` is now a parent context menu action.
2. Conversion targets are shown in a second-level submenu.
3. `EditorCommandDescriptor` supports child commands.
4. The WebView context menu renderer supports nested submenu commands.
5. Added regression coverage for the nested submenu contract.

### V2.0.2.0023

Implemented the first Element object model slice:

1. Added the official Element object model document.
2. Added the `Element` base class with generic id, name, bounds, style, legacy source, `HtmlCode`, `CssCode`, and `JsCode`.
3. Added `NumericInput : Element`.
4. Numeric display and numeric input are represented by the same `NumericInput` class with `IsReadOnly`.
5. `ElementPlusLegacyConverter` now instantiates `NumericInput` for numeric conversion targets before adapting to the temporary `ScadaElement` pipeline.
6. Read-only numeric objects render as display text in the WebView instead of an empty `input type=number` when the legacy value is a placeholder like `####`.
7. Added regression tests for concrete `NumericInput` instantiation, code generation, adapter output, and read-only numeric rendering.

### V2.0.2.0024

Corrected the Conversion Element+ submenu hover behavior:

1. Removed the positive gap between the parent context-menu item and the submenu.
2. Added a transparent pointer bridge so the submenu stays open while moving the pointer from the parent to the submenu.
3. Added regression coverage to prevent reintroducing a hover gap.

### V2.0.2.0025

Fixed the Text22 legacy-to-numeric conversion inventory bug:

1. Converted legacy ids are now removed immediately from the C# runtime legacy inventory.
2. Legacy inventory refresh ignores ids already marked hidden/converted.
3. Legacy selection refresh ignores hidden/converted ids.
4. The Element list filters hidden legacy objects, so `Text22 [Legacy]` disappears immediately after conversion.
5. Undo restores the legacy object into the runtime inventory before refreshing the UI.
6. Added a regression test for `win00008` `Text22` id `784` conversion inventory behavior.

### V2.0.2.0026

Cleaned the Element+ legacy converter static-analysis warnings:

1. `ElementPlusLegacyConverter` is now a stateless static converter.
2. `CanConvert`, `Convert`, and `ConvertToElement` are static.
3. Removed the unused converter instance from `MainWindow`.
4. Removed a temporary debug `Console.WriteLine` from numeric conversion.
5. Updated tests to call the static converter API.

### V2.0.2.0027

Persisted and reload-validated the `win00008` Text22 conversion:

1. `Text22` source id `784` is now saved in the V2 scene as `Element+ Text22`.
2. The converted object is an `InputNumeric` Element+ with `IsReadOnly = true`, matching `Affichage numerique`.
3. The scene keeps the legacy source trace only as metadata for diagnostics and reload masking.
4. Reopening the scene derives hidden legacy ids from converted Element+ traces, so `Text22 [Legacy]` does not return to the element list.
5. Added regression coverage for save, reload, and inventory filtering of the converted Text22 source.

### V2.0.2.0028

Clarified and enforced conversion lifetime:

1. The full legacy object snapshot exists only in the in-memory undo stack.
2. The undo stack is cleared when the application closes.
3. Saving a converted Element+ removes legacy text overrides for the converted source id.
4. Loading a scene normalizes out legacy overrides that match already converted Element+ source ids.
5. Computed projection properties are no longer serialized in scene JSON.
6. Added regression coverage so converted Text22 id `784` does not persist as a legacy override after save/reload.

### V2.0.2.0029

Fixed manual Conversion Element+ execution from the WebView context submenu:

1. Context-menu pointer interactions no longer enter the global scene selection handler.
2. Clicking a conversion target no longer starts a zero-size drag selection that clears the selected legacy object.
3. Context-menu command clicks now stop propagation and send the current selected legacy item snapshot to WPF.
4. WPF rehydrates the legacy selection from the command message before executing the selected command.
5. Added regression tests for submenu command clicks preserving selection before conversion.

### V2.0.2.0030

Implemented the first ElementGroup domain slice:

1. Added `ShapeElement` as a modern shape/polygon-capable Element+ type.
2. Added `ElementGroup` as an Element+ parent with a child element list.
3. Added domain operations for grouping, ungrouping, moving a group, and moving a child relative to its parent.
4. Groups can contain simple elements, nested groups, or a mix of both.
5. Direct children use coordinates relative to their parent group.
6. Ungrouping preserves the visual absolute position of direct children.
7. Added guardrails against self-grouping, duplicate grouping, and selecting both a group and its descendant.
8. Added regression tests for relative coordinates, parent movement, child movement, ungrouping, nested groups, and invalid group inputs.

### V2.0.2.0031

Branched the first ElementGroup UI slice:

1. Added Element panel buttons for `Grouper` and `Degrouper`.
2. Added a legacy context command `Grouper Element+` for multi-selection.
3. Legacy group creation converts selected legacy items into an Element+ group with modern shape children.
4. Group children are stored under the group and use coordinates relative to their parent.
5. Legacy source ids inside group children are hidden/removed from the active working inventory.
6. The Element list now includes nested Element+ children.
7. The WebView renderer supports recursive group rendering.
8. Group selection shows a blue group outline; child selection shows a yellow child outline.
9. Modern context menu exposes `Degrouper` for selected groups.
10. Added regression coverage for recursive scene operations and group UI command wiring.

### V2.0.2.0032

Fixed selection regressions introduced by the first group UI slice:

1. Element+ selection now supports multiple selected modern objects.
2. The WPF selection state stores selected Element+ ids separately from the primary properties object.
3. The WebView modern selection uses a `Set` instead of a single selected id.
4. Ctrl/Shift click on Element+ toggles selection without clearing the previous selection.
5. The Element list can highlight multiple selected Element+ objects.
6. Right-click on an existing legacy multi-selection preserves the selection, even if the DOM target is ambiguous.
7. Added regression coverage for multi Element+ selection and legacy right-click selection preservation.

### V2.0.2.0033

Corrected grouped shape visibility:

1. Element+ groups remain transparent containers.
2. Shape children created from legacy geometry no longer inherit a fully transparent fill.
3. Transparent legacy shape backgrounds now receive a visible editor fallback fill.
4. Transparent legacy shape borders now receive a visible editor fallback stroke.
5. The WebView renderer keeps the same visible fallback if a shape reaches the UI with transparent CSS.
6. Added regression coverage so grouped shape children do not become visually transparent.

### V2.0.2.0034

Corrected the grouping contract after review:

1. Grouping legacy elements is not a conversion operation.
2. Legacy grouped elements must keep their original rendering 100% unchanged.
3. Grouping now creates only an Element+ frame/selection object.
4. Grouping no longer hides the legacy source elements from the WebView.
5. Grouping no longer removes legacy source elements from the Element list.
6. Grouping no longer creates replacement `Shape` children that repaint the original legacy geometry.
7. Degrouping an empty legacy frame group removes the frame.
8. Added regression coverage so legacy grouping cannot call the conversion/hide path.

### V2.0.2.0035

Documented the Studio Element+ direction:

1. Studio Element+ is a real second application, installable optionally.
2. Polygon/line/composed legacy graphics are not direct linear conversions.
3. SCADA Builder V2 will offer `Ouvrir dans Studio Element+` for eligible legacy selections.
4. The selected legacy material is exported to a temporary package under `.studio/imports`.
5. The product-facing package/component extension is `.ft1`.
6. The initial `.ft1` content may remain JSON while the model stabilizes.
7. Studio Element+ owns composed component creation; SCADA Builder V2 owns scene placement.

### V2.0.3.0000

Introduced the first Studio Element+ implementation slice:

1. Added the `.ft1` import package model and metadata.
2. Added a package factory that computes package bounds and relative item bounds.
3. Added a JSON `.ft1` writer under `.studio/imports`.
4. Added `Ouvrir dans Studio Element+` to the legacy context menu.
5. SCADA Builder V2 writes a `.ft1` package from the selected legacy items.
6. SCADA Builder V2 attempts to launch the separate Studio Element+ executable with the package path.
7. Added the new WPF application project `ScadaBuilderV2.ElementStudio.App`.
8. Studio Element+ opens with or without a package and renders an initial isolated workspace.
9. Added regression coverage for the `.ft1` writer and context command wiring.

### V2.0.3.0001

Added the first faithful legacy-source rendering slice for Studio Element+:

1. Legacy selection messages now capture source `outerHTML`.
2. Legacy selection messages now capture raw computed-style metadata.
3. `LegacyElementListItem` carries legacy markup and raw metadata into the `.ft1` pipeline.
4. `.ft1` import items now include `LegacyMarkup`.
5. Studio Element+ uses WebView2 for the legacy source layer.
6. Studio Element+ renders the imported legacy markup before any Element+ conversion.
7. WPF rectangle previews remain only as a light diagnostic overlay.
8. Added regression coverage for legacy markup capture and Studio WebView source rendering.

### V2.0.3.0002

Hardened Studio Element+ launch from SCADA Builder V2:

1. The launcher now searches from the repository root as well as the application output folder.
2. The launcher supports both Debug and Release Studio executable locations.
3. If no Studio executable exists yet, the launcher falls back to `dotnet run --project`.
4. The launcher uses shell execution for WPF process startup so the Studio window opens normally.
5. Status messaging now reports package creation separately from launch availability.
6. Added regression coverage for the launcher fallback path.

### V2.0.3.0003

Corrected Studio Element+ launch reliability and removed selection overhead:

1. SCADA Builder V2 now captures heavy legacy markup only when `Ouvrir dans Studio Element+` is executed.
2. Normal inventory and selection messages stay lightweight and no longer clone every selected legacy element with full computed CSS.
3. The Studio launcher now verifies whether the executable exits immediately.
4. If the executable exits immediately, the launcher falls back to `dotnet run --project`.
5. Added regression coverage for deferred Studio markup capture and launcher health checking.

### V2.0.3.0004

Added launch diagnostics for Studio Element+:

1. SCADA Builder V2 now writes a durable `.studio/logs/element-studio-launch.log` entry for every Studio launch attempt.
2. The launcher waits for a real WPF main window handle instead of treating process creation alone as success.
3. If no Studio window is detected, the status message reports that explicitly.
4. Regression coverage now checks launch logging and window-handle health checks.

### V2.0.3.0005

Connected the status diagnostic button:

1. The status-bar `!` button is now wired to `OnStatusDiagnosticsClick`.
2. The diagnostics dialog shows the current status, current version, and recent Studio Element+ launch log lines.
3. The Studio launch log path is resolved through a shared helper used by both logging and diagnostics.
4. Added regression coverage so the status diagnostic button cannot regress into a decorative-only control.

### V2.0.3.0006

Corrected Studio Element+ launch success detection:

1. Launch success now requires a visible Studio Element+ WPF window, not only a live process.
2. The executable launch path uses normal shell startup and brings the Studio window to the foreground when found.
3. The `dotnet run` fallback no longer reports success when it remains alive without a visible WPF window.
4. Failed fallback processes are cleaned up to avoid accumulating invisible `dotnet` processes.
5. Launch status now records the visible window process id when the Studio window is detected.

### V2.0.3.0007

Fixed Studio Element+ startup crash:

1. Studio Element+ property-panel text boxes bound to read-only view-model properties now use explicit `Mode=OneWay`.
2. This prevents WPF from applying the default `TextBox.Text` `TwoWay` binding to properties without setters.
3. Added regression coverage for read-only property bindings.

### V2.0.3.0008

Fixed Studio Element+ legacy geometry rendering:

1. SVG legacy markup such as `<polygon>` is now rendered inside a real SVG layer instead of invalid HTML `div` wrappers.
2. The Studio source layer uses the package bounds as the SVG `viewBox` so absolute legacy coordinates remain visible.
3. Non-SVG legacy markup still uses the positioned HTML fallback layer.
4. Added regression coverage for SVG source-layer rendering.

### V2.0.3.0009

Documented SVG/image-backed Element+ component direction:

1. Element+ is allowed to represent reusable graphic components, not only simple UI controls.
2. Legacy piping and composed industrial graphics may become one SVG-backed Element+ object.
3. SVG primitives remain internal to the component while the scene treats the result as one reusable object.
4. Future events may target the component first, then named internal parts later.
5. Studio Element+ owns cleanup, coordinate normalization, packaging, and library publication.

### V2.0.3.0010

Recorded Element+ component direction decisions and adjusted Studio layout:

1. First-slice events target the whole Element+ component, not internal SVG parts.
2. Image-backed Element+ components must embed image data in `.ft1` for portability.
3. Legacy geometry must remain editable in Studio after conversion so variants can be created later.
4. Legacy source names may be preserved as trace metadata while Studio creates cleaner internal part names.
5. Studio Element+ now places drawing tools on the left and moves `Sources` / `Structure` into the right context panel.

### V2.0.3.0011

Implemented the first Studio Element+ authoring slice:

1. Studio workzone dimensions now derive from package bounds.
2. Permanent WPF rectangle overlays were removed so they are not confused with imported source geometry.
3. Studio WebView source items can be selected and synchronized with the right-side source/properties panel.
4. Legacy selection artifacts are stripped from exported/imported markup.
5. The `Creer SVG` command creates an in-memory SVG Element+ component draft and records diagnostics.

### V2.0.3.0012

Updated the Studio Element+ implementation plan:

1. `.sep` is now the explicit Studio Element Plus editable working format.
2. `.sep` must package all Element+ source content required to reopen and edit the component without external files.
3. The Studio workzone, zoom, pan, selection rectangles, and editor overlays must not be exported as Element+ geometry.
4. The `Selection`, `Ligne`, `Polyline`, `Rectangle`, `Polygone`, and `Image` tools must become functional authoring tools.
5. Tool-created primitives must become real component model parts.
6. Studio must evolve into a real component scene that can temporarily hold multiple imports while authoring one component.

### V2.0.3.0013

Recorded `.sep` scope decisions:

1. `.sep` is the editable Studio source of truth.
2. `.ft1` is reserved for runtime, library, or export packaging if a separate deployment format is required later.
3. One `.sep` file contains exactly one Element+ component.
4. Legacy imports are source material only and are not kept as permanent non-destructive legacy layers in the final `.sep`.
5. Image data must be embedded in `.sep` for portability.

### V2.0.3.0014

Updated Studio Element+ element selection behavior:

1. The right context tab is now `Element` instead of `Sources`.
2. Imported legacy items are presented with Element+ names such as `Element001`.
3. The original legacy source name remains visible in properties for traceability.
4. Multi-selection in the Element list now synchronizes to the workzone WebView highlight.
5. Added regression coverage for the Element tab, Element+ import naming, and list-to-workzone multi-selection.

### V2.0.3.0015

Clarified Studio Element+ format boundaries while remaining in planning mode:

1. `.ft1` is the SCADA Builder V2 -> Studio Element+ transfer/export package.
2. `.sep` is the shared library component format for Studio Element+ and SCADA Builder V2.
3. `.sep` remains one component per file.
4. `.ft1` is not the reusable library component format.

### V2.0.3.0017

Fixed Studio Element+ workzone selection behavior:

1. The SVG source layer can receive clicks because transparent HTML/label/marquee layers no longer intercept pointer events.
2. The `Selection` tool now exposes a dashed drag-selection marquee inside the workzone.
3. Drag selection supports replace, add with Ctrl/Shift, and subtract with Alt.
4. Workzone zoom now uses scaled dimensions so scrollable workzone size grows with zoom.
5. Added regression coverage for drag marquee, SVG hit testing, and scaled workzone bindings.

### V2.0.3.0018

Refined Studio Element+ workzone ergonomics:

1. The selection marquee now uses a more standard, lighter dashed rectangle style.
2. The `Vue` ribbon exposes `Zone -`, `Zone +`, and `Fit zone` controls for manual workzone resize.
3. Workzone width and height are editable editor state and remain excluded from Element+ export payloads.
4. Added regression coverage for resize controls and marquee styling.

### V2.0.3.0019

Implemented Studio Element+ `.sep` save wiring:

1. `Enregistrer` and `Save as .sep` are now connected to real save handlers.
2. Studio Element+ creates a one-component `.sep` package from the current imported Element list.
3. Saved `.sep` files preserve SVG markup, Element+ names, part metadata, and legacy source traces.
4. The workzone, zoom, and editor overlays remain excluded from the saved component payload.
5. Added regression coverage so the save buttons cannot regress into decorative-only commands.
