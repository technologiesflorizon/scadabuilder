# SCADA Builder V2 - Commands and State

Date: 2026-06-15
Status: Draft
Document version: `V2.1.1.0030`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Normalisation du header documentaire et rattachement a l'arbre documentaire stable. |
| 2026-06-15 | `V2.1.1.0026` | `2b59efb` | Baseline initiale du depot SCADA Builder V2. |

Detailed implementation direction: `ACTION_COMMAND_ARCHITECTURE_PLAN_V2.md`.

## 1. Objective

SCADA Builder V2 must route user intent through explicit application commands and maintain a clear project state model.

This document defines the initial command registry direction, state boundaries, selection model, panel state, and future undo/redo path.

## 2. Principles

1. Every meaningful user action has a command id.
2. Ribbon, menus, toolbar buttons, shortcuts, canvas gestures, and context menus reuse the same command registry.
3. Command handlers live in the Application layer.
4. Domain state changes happen through command handlers or domain services called by command handlers.
5. UI panels do not mutate project state directly.
6. Command enablement is computed from application state, not duplicated in each panel.
7. Commands return results, diagnostics, and state-change notifications.
8. Undo/redo uses a scene-scoped common history for implemented editor mutations and remains extensible for future command coverage.
9. Preview loading must not expose transient WebView fallback paints; the WPF preview host, native WebView2 controller, and loaded document must use the active scene background before the WebView is revealed.
10. Preview refresh must replay active scene page dimensions from `CanvasSize` before rendering editor overlay layers, so saved page width and height remain visible after scene reload.

## 3. Command Registry

Each command definition should include:

1. Command id.
2. User label.
3. Short description or tooltip.
4. Category.
5. Optional icon key.
6. Optional default shortcut.
7. Required context.
8. Enablement rule.
9. Handler id or handler type.
10. Undo policy.

Example shape:

```text
CommandId: project.save
Label: Enregistrer
Category: Fichier
Shortcut: Ctrl+S
RequiredContext: ProjectLoaded
UndoPolicy: NotUndoable
```

The registry is the single source of truth for:

1. Ribbon commands.
2. Menus.
3. Tool buttons.
4. Context menus.
5. Keyboard shortcuts.
6. Panel restore commands.
7. Canvas gesture commands.

## 4. Command Categories

Initial categories:

1. `file`
2. `edit`
3. `screen`
4. `selection`
5. `project`
6. `scene`
7. `element`
8. `layout`
9. `panel`
10. `preview`
11. `build`
12. `import`
13. `validation`
14. `tools`

These categories can map to the top menu, ribbon groups, context menus, and test organization.

## 5. Initial Command Set

File and project:

1. `project.new`
2. `project.open`
3. `project.save`
4. `project.saveAs`
5. `project.close`
6. `project.validate`

Scene and workspace:

1. `scene.new`
2. `scene.open`
3. `scene.closeTab`
4. `scene.rename`
5. `scene.duplicate`
6. `scene.delete`
7. `scene.setActive`
8. `scene.reorderTab`

Tools:

1. `tool.select`
2. `tool.move`
3. `tool.text`
4. `tool.image`
5. `tool.group`
6. `tool.zoom`
7. `tool.lockObject`

Selection:

1. `selection.set`
2. `selection.clear`
3. `selection.add`
4. `selection.remove`
5. `selection.lock`
6. `selection.unlock`
7. `selection.selectAll`
8. `selection.focus`

Elements:

1. `element.add`
2. `element.delete`
3. `element.duplicate`
4. `element.move`
5. `element.resize`
6. `element.setProperty`
7. `element.setStyle`
8. `element.group`
9. `element.ungroup`
10. `element.bringForward`
11. `element.sendBackward`
12. `element.lock`
13. `element.unlock`

Panels:

1. `panel.show`
2. `panel.hide`
3. `panel.collapse`
4. `panel.expand`
5. `panel.resize`
6. `panel.restoreDefaults`

Preview/build/import:

1. `preview.refresh`
2. `preview.setDevicePreset`
3. `preview.setOrientation`
4. `build.export`
5. `import.legacyProject`
6. `import.ft100Mappings`

Undo/redo placeholders:

1. `edit.undo`
2. `edit.redo`

## 6. Application State

The application editing session should expose one coherent state object or store.

Core state:

1. Current project id and path.
2. Loaded project model.
3. Project dirty state.
4. Open scene tabs.
5. Active scene id.
6. Active responsive variant.
7. Current device preview preset.
8. Current selection.
9. Selection lock state.
10. Active tool.
11. Panel layout state.
12. Validation diagnostics.
13. Save status.
14. Preview status.
15. Build status.
16. Import status.

State must be observable by UI view models, but write access should remain controlled by Application services.

## 7. Project State

Persistent project state belongs to the project model and must serialize to project files.

Examples:

1. Project name.
2. Project version/schema version.
3. Root path.
4. Global canvas size.
5. Responsive mode.
6. Device presets.
7. Scene list.
8. Assets and libraries.
9. Build options.
10. FT100 mapping references.

Project state should not contain transient UI details such as hover state or an in-progress drag operation.

## 8. Workspace State

Workspace state coordinates editor navigation:

1. Open tabs.
2. Active tab.
3. Tab order.
4. Dirty marker per scene.
5. Closed/restorable panel state.
6. Current zoom per scene.
7. Current scroll position per scene.
8. Current preview device.

Decision:

1. Scene tabs are an application-session concept.
2. Scene definitions are persistent project data.
3. Tab order may be stored as user preference, but it should not redefine the project scene list unless the user explicitly reorders project scenes.

## 9. Selection State

Selection state must distinguish identity, lock selection, and object lock.

Selection fields:

1. Active scene id.
2. Selected element ids.
3. Primary selected element id.
4. Selection source: canvas, tree, property panel, command, import, script.
5. Selection lock enabled.
6. Selection lock scope.
7. Last valid selection for restoration.

Rules:

1. `selection.lock` preserves the current selection across operations that would normally clear it.
2. `selection.lock` does not make selected objects read-only.
3. `element.lock` prevents object modification.
4. A locked object can still be selected.
5. A locked selection can point to an unlocked object.
6. If a locked selection target is deleted, the application must clear the invalid selection and report a warning.
7. Selection labels shown to users must use display names, not internal legacy ids.
8. Selection inventory keys use neutral active-scene identity prefixes: `source:` for imported source objects and `object:` for scene objects.
9. User-facing selection labels must not describe the active model as separate Legacy/V2 versions; origin is trace metadata, not a separate editing mode.
10. Context-menu command ids use neutral active-scene categories: `source.*` for source-object operations and `object.*` for scene-object operations. Legacy command ids may remain as temporary dispatcher aliases only.
11. Runtime selection state is owned by one active selection state object. Source-object ids, scene-object ids, and the primary scene object are coordinated together so delete, undo/redo, property panels, and WebView selection refreshes do not split behavior by product generation.

## 10. Panel State

Each panel has state:

1. Panel id.
2. Visible.
3. Collapsed.
4. Dock position.
5. Size.
6. Active tab or section.
7. Last restored size.

Initial panels:

1. `panel.left`
2. `panel.right`
3. `panel.bottom`
4. `panel.ribbon`

Panel restore commands should be generated or registered from panel metadata so `Edition -> Panneau` stays consistent with the actual available panels.

## 11. Command Context

Command enablement should be based on context flags:

1. `ProjectLoaded`
2. `ProjectDirty`
3. `SceneOpen`
4. `SceneActive`
5. `SelectionExists`
6. `SingleElementSelected`
7. `MultipleElementsSelected`
8. `SelectedElementEditable`
9. `SelectionLocked`
10. `ObjectLocked`
11. `CanUndo`
12. `CanRedo`
13. `PreviewAvailable`
14. `BuildAvailable`
15. `ImportAvailable`

Handlers must validate context again when executed. UI enablement is helpful, but it is not a security or correctness boundary.

## 12. Command Result

Every command execution should return a result:

1. Success or failure.
2. User-facing message when needed.
3. Diagnostics.
4. Changed state areas.
5. Dirty flag impact.
6. Undo record id when applicable.

Example:

```text
CommandResult
  Success: true
  Changed: Selection, Properties, SceneDirty
  Message: ""
  UndoRecordId: "undo-00042"
```

## 13. Undo/Redo Direction

Undo/redo is command-centered and scene-scoped for implemented editor mutations.

Implemented baseline:

1. Each open scene tab owns one in-memory `EditorHistoryService`.
2. The active scene tab handles `Undo` and `Redo`; actions from other open scenes are not mixed into the active scene history.
3. Scene background changes, Element+ geometry edits, active-scene selection moves, Element+ property edits, Element+ insertion, Element+ library instantiation, scene-object deletion, legacy group-frame creation, Element+ ungroup, legacy text override editing, and legacy-to-Element+ conversion undo/redo use the common per-scene history.
4. Save persists the current scene state but does not persist undo/redo history.
5. Closing a scene tab or the application discards the associated history.
6. `selection.delete` deletes selected scene objects through one active-scene path, regardless of whether an object originated from imported source material or was inserted as an Element+ object.
7. Deleted imported source object ids are persisted as active scene state so preview, save/reload, and FT100 export converge on the same visible object set.
8. Source-object deletion persists selected source `data-id` values even when the source object has no materialized `LegacyStatic` snapshot in the scene, and those source-only deletions use the same scene-scoped undo/redo action as normal scene-object deletion.
9. The implementation keeps compatibility aliases for older command and WebView bridge names, but new state and command surfaces use source/object terminology.

Undo policies:

1. `NotUndoable`
2. `Undoable`
3. `UndoableBatch`
4. `HistoryBoundary`

Examples:

1. `project.save`: `NotUndoable`
2. `selection.set`: `NotUndoable` or lightweight navigation history
3. `element.move`: `UndoableBatch`
4. `element.setProperty`: `Undoable`
5. `import.legacyProject`: `UndoableBatch` or `HistoryBoundary`
6. `build.export`: `NotUndoable`

Drag/resize behavior:

1. Pointer move updates may be transient.
2. Commit on pointer release creates one undo record.
3. Cancel restores the starting state.
4. Element+ move/resize messages carry before/after bounds so undo does not infer history from the current WebView DOM.
5. Scene workzone resize from the preview bottom-right handle commits one `CanvasSize` snapshot to the active scene on pointer release.
6. Scene dimension field edits in the Page inspector apply valid `CanvasSize` changes automatically after a short debounce and use the same scene snapshot history path.
7. Context menus are editor chrome and must clamp to the visible WebView viewport, not to the resized scene workzone.
8. Click-and-drag on any selected editable element moves the active selection by a single `moveSelectionBy` gesture message. Imported source objects and scene objects keep relative offsets, commit one scene snapshot on pointer release, and replay edited bounds into preview and FT100 export.

Property editing behavior:

1. Typing may debounce transient updates.
2. Commit on focus loss, Enter, or validated field change creates an undo record.
3. Invalid values do not enter history.
4. Consecutive Element+ property edits on the same object may merge into one undo step when each edit starts from the previous committed element snapshot.

## 14. Events and Notifications

Application events should be explicit:

1. `ProjectOpened`
2. `ProjectSaved`
3. `ProjectDirtyChanged`
4. `SceneOpened`
5. `ActiveSceneChanged`
6. `SelectionChanged`
7. `SelectionLockChanged`
8. `ElementChanged`
9. `PanelStateChanged`
10. `ValidationDiagnosticsChanged`
11. `PreviewStatusChanged`
12. `BuildStatusChanged`

Events are for notification and refresh. They must not become a second hidden command system.

## 15. Validation and Diagnostics State

Diagnostics should be structured:

1. Diagnostic id.
2. Severity: info, warning, error.
3. Source: project, scene, element, binding, FT100, script, build, preview.
4. Target id.
5. User message.
6. Technical detail.
7. Suggested command id when actionable.

The bottom panel reads diagnostics from state and can trigger related commands, but it does not own validation logic.

## 16. Testing Requirements

Command/state tests:

1. No duplicate command ids.
2. Every visible command has a label and category.
3. Every command has an enablement rule.
4. Disabled commands cannot mutate state when executed directly.
5. Selection lock and object lock remain distinct.
6. Scene tab changes update active scene state consistently.
7. Panel restore commands reflect registered panels.
8. Dirty flags update after project mutations.
9. Undoable commands produce undo metadata when undo/redo is enabled.
10. Validation diagnostics can be raised and cleared without UI dependencies.

## 17. Open Decisions

1. Exact command id casing convention.
2. Whether selection changes enter undo history or only navigation history.
3. Exact persistence location for panel layout preferences.
4. Granularity of command results for preview/build refresh.
5. Batch undo model for imports and generated responsive layout changes.
