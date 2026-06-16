# SCADA Builder V2 - UI Specification

Date: 2026-06-15
Status: Draft implementation spec
Document version: `V2.1.1.0037`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0037` | `PENDING` | Ajout de la roadmap du jeu de proprietes CSS partage entre SCADA Builder V2 et Studio Element+. |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Normalisation du header documentaire et rattachement a l'arbre documentaire stable. |
| 2026-06-15 | `V2.0.0.0003` | `2b59efb` | Baseline initiale du depot SCADA Builder V2; mise a jour page properties et object actions. |

## 1. Objective

SCADA Builder V2 is a modern C# desktop editor for industrial SCADA screens.

The UI must provide:

1. A top ribbon for global and contextual commands.
2. Dockable, resizable and restorable panels.
3. A central tabbed workspace for opened scenes/pages.
4. A dynamic right panel driven by the active page, selection and tool context.
5. A global `lock selection` state distinct from object locking.
6. A left palette with `Outil` and `Projet` modes.
7. A visual style aligned with Florizon Technologies: clear, dense, professional and industrial.

The wireframe defines the base layout:

```text
Top:       Fichier / Edition / Ecran / Selection / Menu-Ribbon
Left:      Tool and project palette
Center:    Workspace
Right:     Page / Element / Librairie / Propriete context
Bottom:    Etat / Warning
```

## 2. Target Desktop Stack

Recommended implementation stack:

1. Language: C#.
2. Runtime: .NET 8 or newer LTS-compatible desktop runtime.
3. UI framework: WPF.
4. Pattern: MVVM with explicit application commands.
5. Preview host: WebView2 for HTML/CSS preview parity when browser rendering is required.
6. Docking: a dock layout service or docking library isolated behind an application interface.
7. Serialization: XAML project configuration for project-level metadata, with separate scene/layout files as needed.

WPF is the preferred first implementation target because it supports mature desktop workflows: ribbon, docking, commands, binding, templates, custom inspectors and high-density editor layouts.

The UI layer must not contain business logic. User actions go through commands and application services.

## 3. Shell Layout

The application shell contains five primary regions:

1. `RibbonRegion`
   - Top command region.
   - Hosts file commands, edit commands, screen commands, selection commands and contextual ribbon groups.

2. `LeftDockRegion`
   - Dockable panel.
   - Contains the `Outil` and `Projet` palette tabs.

3. `WorkspaceRegion`
   - Main editor region.
   - Contains tabbed scene/page documents.

4. `RightDockRegion`
   - Dockable context panel.
   - Shows page, element, property and library inspectors.

5. `BottomStatusRegion`
   - Dockable bottom bar.
   - Shows status, warnings, validation errors, save state and build state.

All major panels must be:

1. Resizable.
2. Collapsible.
3. Closable.
4. Restorable from `Edition -> Panneau`.
5. Persisted in the user layout profile.

## 4. Ribbon

The top region uses an AutoCAD-style ribbon for frequent visual commands.

Primary tabs:

1. `Fichier`
2. `Edition`
3. `Ecran`
4. `Selection`
5. Contextual tabs based on active tool or selected element type.

### 4.1 Fichier

Expected commands:

1. New project.
2. Open project.
3. Save.
4. Save all.
5. Import legacy project.
6. Import FT100 mappings.
7. Export / build.
8. Recent projects.

### 4.2 Edition

Expected commands:

1. Undo.
2. Redo.
3. Cut.
4. Copy.
5. Paste.
6. Duplicate.
7. Delete.
8. Panel restoration menu: `Edition -> Panneau -> <PanelName>`.

### 4.3 Ecran

Expected commands:

1. New scene/page.
2. Rename scene/page.
3. Duplicate scene/page.
4. Delete scene/page.
5. Preview device: desktop, tablet, mobile.
6. Orientation for tablet/mobile: portrait, landscape, rotate 90 degrees.
7. Responsive mode: fixed, scale-to-fit, adaptive layout.
8. Preset size selector.

### 4.4 Selection

Expected commands:

1. Select all.
2. Clear selection.
3. Group.
4. Ungroup.
5. Bring forward.
6. Send backward.
7. Align.
8. Distribute.
9. Lock object.
10. Unlock object.
11. Lock selection global toggle.

### 4.5 Outils Menu

The ribbon must expose an `Outils` command that opens the complete tool catalog.

This catalog includes:

1. Tools visible in the left `Outil` palette.
2. Less frequent tools.
3. Future configurable tools.
4. Tool metadata: icon, label, tooltip, shortcut and category.

## 5. Left Dock Panel

The left panel is a compact vertical dock panel with tabs.

Initial tabs:

1. `Outil`
2. `Projet`

The panel must support icon-first navigation with tooltips and enough text only where it improves scan speed.

### 5.1 Outil Tab

The `Outil` tab contains immediate editor actions:

1. Selection.
2. Move.
3. Text.
4. Image.
5. Group.
6. Lock object.
7. Zoom.
8. Future drawing or component tools.

Tool buttons must have:

1. Icon.
2. Tooltip.
3. Active state.
4. Disabled state.
5. Optional shortcut hint in the tooltip.

Selection behavior:

1. Only one primary tool is active at a time.
2. Tool changes must not clear selection unless the command explicitly requires it.
3. The active tool contributes context to the right panel.

### 5.2 Projet Tab

The `Projet` tab contains project structure and global settings:

1. Project name.
2. Root path.
3. Global SCADA resolution.
4. Responsive strategy.
5. Device presets.
6. Scene/page tree.
7. Page editor commands: create, open, rename, duplicate, delete.
8. Asset/library entry points.

Page rules:

1. Pages are managed from the `Projet` tab.
2. Each opened page appears as a tab in the central workspace.
3. The default resolution is project-wide.
4. Page-specific resolution is supported through page properties when a page must be resized.
5. Pages describe structure, type, dimensions, background, and composition role; pages do not own runtime action triggers.

## 6. Workspace

The workspace is the primary visual editor.

It contains opened scenes/pages as document tabs.

Required tab behavior:

1. Each open page has one tab.
2. The active tab determines the active page context.
3. Tabs have a close button.
4. Tabs can be reordered by drag.
5. Unsaved tabs show `*`.
6. Closing a modified tab prompts save, discard or cancel.
7. Switching tabs updates the right panel dynamically.
8. Switching tabs must not discard global locked selection.

### 6.1 Scene Surface

Each workspace document contains:

1. A canvas or design surface.
2. Zoom and pan.
3. Selection bounds.
4. Resize handles.
5. Alignment guides.
6. Optional grid and snap controls.
7. Device preview frame when a preset is active.

The scene surface must remain visually dominant. Panels and ribbons must support editing without consuming unnecessary canvas space.

### 6.2 Responsive Preview

The workspace must support:

1. Desktop preview.
2. Tablet preview.
3. Mobile preview.
4. Portrait/landscape for tablet and mobile.
5. Rotation by 90 degrees for tablet and mobile.
6. Fixed, scale-to-fit and adaptive layout modes.

Adaptive layout uses one logical scene with multiple layout variants:

1. Desktop variant.
2. Tablet variant.
3. Mobile variant.

Bindings, tags and actions stay attached to the logical scene, not duplicated per device variant.

## 7. Right Dynamic Panel

The right panel is contextual and must update from:

1. Active workspace page.
2. Current selection.
3. Active tool.
4. Active library context.

Initial tabs or sections:

1. `Page`
2. `Element`
3. `Propriete`
4. `Librairie`

The wireframe shows this panel as `Page/Element/Librairie/Propriete`; the implementation may use tabs, accordion sections or a hybrid, provided the context stays clear and compact.

### 7.1 Page Context

Shows page-level settings:

1. Page name.
2. Logical scene id.
3. Page type: `Defaut`, `Fragment`, `Entete`, `Pied-de-page`.
4. Canvas size.
5. Width.
6. Height.
7. Responsive mode.
8. Active device variant.
9. Background.
10. Background CSS fields.
11. Page-level CSS variables.
12. Build/preview status.
13. Legacy source traceability when available.

Page context rule:

1. Selecting a page in `Projet > Pages` must show its page properties in `Propriete`.
2. Page property edits must update the manifest and scene model through commands.
3. Page context must not expose page-owned action triggers.

### 7.2 Element Context

Shows selected element data:

1. Display name.
2. Element type.
3. Layer order.
4. Parent group.
5. Position and size.
6. Lock object state.
7. Visibility.
8. Legacy source metadata in an advanced traceability section.

Internal ids such as `legacy:795` must not be used as user-facing names.

### 7.3 Propriete Context

Shows editable properties based on element type.

Property families:

1. Position and layout.
2. Box model.
3. Background.
4. Typography.
5. Effects.
6. Visibility and interaction.
7. Layering.
8. Bindings.
9. Actions and scripts.

Action property rule:

1. Actions and events are shown for selected objects only.
2. Page selection may show page metadata and background CSS, but not object action editors.

Properties must be validated before applying to the model. Unsupported values should show a warning and should not silently generate invalid output.

CSS property roadmap:

1. The property inspector must expose a broader CSS-capable metadata set for SCADA Builder V2 and Studio Element+ instead of one-off hardcoded controls.
2. The shared set must cover position/layout, box model, background, fill/stroke, border radius, typography, opacity, transform, shadow/glow-ready tokens, filters, cursor/interaction, visibility, and runtime state/effect classes.
3. Every CSS-related value must have an editor type, validation rule, serialization rule, preview output rule, and export output rule.
4. Unsupported browser/runtime values must remain blocked or warning-only until preview and TF100 export can reproduce them deterministically.
5. Event visual effects such as blink, glow, pulse, alarm highlight, and degraded visual treatment must be represented as validated effect/state properties, not arbitrary untracked CSS text.
6. Page-scoped CSS namespace rules remain mandatory when these properties are exported to TF100Web.

### 7.4 Librairie Context

Shows reusable items:

1. Components.
2. Symbols.
3. Images.
4. Icons.
5. Imported legacy candidates.
6. Future FT100 mapping helpers.

Library items should support drag into the workspace.

## 8. Global Lock Selection

`Lock selection` is a global editor state.

It is different from `Lock object`.

Definitions:

1. `Lock selection`
   - Keeps the current selection active across compatible operations.
   - Allows property editing while preserving the selected target.
   - Can survive page changes when the selected model reference remains valid or when cross-page copy/paste is intended.

2. `Lock object`
   - Prevents moving, resizing or direct editing of an object.
   - Does not preserve selection across context changes by itself.

Required UI:

1. A global lock-selection icon in the right panel header or selection summary.
2. A ribbon command under `Selection`.
3. A clear visual distinction between selection lock and object lock.
4. Tooltip text that explicitly states the behavior.
5. Status feedback in the bottom bar when lock selection affects an operation.

Required behavior:

1. Lock selection does not make an object immutable.
2. Lock object does not pin selection.
3. If the selected object is unavailable in the new context, the right panel shows a recoverable stale-selection state.
4. Unlocking selection returns to normal active-page selection behavior.

## 9. Bottom Status and Warning Bar

The bottom bar shows operational feedback:

1. Current status.
2. Warnings.
3. Validation errors.
4. Save state.
5. Build state.
6. Preview runtime state.
7. Selected element summary.
8. Coordinates and canvas size when relevant.

The bar must be collapsible, closable and restorable from `Edition -> Panneau`.

Warnings must be actionable where possible, for example opening the relevant page, element or property inspector.

## 10. Florizon Visual Style

The desktop editor must use a dense adaptation of the Florizon public style.

Base palette:

1. Application background: `#f7fbf5`
2. Panel surface: `#ffffff`
3. Main text: `#0f2a30`
4. Secondary text: `#4e6a71`
5. Primary accent green: `#90c030`
6. Secondary accent turquoise: `#2090a0`
7. Soft accent: `#e0f2d0`
8. Borders: `rgba(15,42,48,0.08)` to `rgba(15,42,48,0.16)`
9. Shadows: `rgba(15,42,48,0.10)` to `rgba(15,42,48,0.18)`

Industrial state colors must be distinct from brand green:

1. Error: dedicated red.
2. Warning: dedicated amber.
3. Info: dedicated blue or turquoise.
4. Success: green may be used only when the meaning is state success, not just brand emphasis.

Style rules:

1. Panels are compact and functional, not marketing cards.
2. Cards are used only for repeated items, modals or framed tools.
3. Editor controls prioritize scan speed.
4. Icon buttons are preferred for frequent commands.
5. Text labels are used where industrial clarity is more important than compactness.
6. Font direction: `Space Grotesk` for high-level headings when available.
7. Font direction: `Source Sans 3` or system equivalent for UI text.
8. Layout density must fit desktop editing workflows.

## 11. C# UI Architecture

Recommended shell view model structure:

```text
MainShellViewModel
  RibbonViewModel
  DockLayoutViewModel
  WorkspaceViewModel
    OpenDocuments[]
    ActiveDocument
  LeftPaletteViewModel
    ToolPaletteViewModel
    ProjectPaletteViewModel
  RightContextViewModel
    PageContextViewModel
    ElementContextViewModel
    PropertyInspectorViewModel
    LibraryContextViewModel
  StatusBarViewModel
  SelectionStateViewModel
```

Core services:

1. `ICommandDispatcher`
2. `IProjectService`
3. `ISceneDocumentService`
4. `ISelectionService`
5. `IToolService`
6. `IDockLayoutService`
7. `IPropertyInspectorService`
8. `IResponsivePreviewService`
9. `IWarningService`
10. `IBuildPreviewService`

The selection service owns global selection state. The right panel observes selection and active document changes; it must not own the canonical selection.

## 12. Command Model

Every user action that changes project, scene, selection, layout, properties or build state must use an explicit command.

Command requirements:

1. Command id.
2. Display label.
3. Icon key.
4. Can-execute state.
5. Execute handler in application layer.
6. Undo/redo support when the command mutates the scene or project model.
7. Validation result.

Examples:

1. `Project.New`
2. `Project.Open`
3. `Project.Save`
4. `Scene.Open`
5. `Scene.Duplicate`
6. `Selection.LockGlobal`
7. `Object.Lock`
8. `Object.Group`
9. `Property.SetValue`
10. `Preview.SetDevicePreset`

## 13. Property Inspector Rules

The property inspector must be generated from metadata, not hardcoded independently for every element type.

Property metadata includes:

1. Property key.
2. User label.
3. Category.
4. Editor type.
5. Allowed element types.
6. Validation rules.
7. Default value.
8. Advanced/basic visibility.
9. Serialization behavior.

Editor types:

1. Text input.
2. Numeric input.
3. Unit input.
4. Color picker.
5. Toggle.
6. Enum dropdown.
7. Asset picker.
8. Binding picker.
9. Script editor trigger.

CSS-related values must serialize into the project/scene model and produce deterministic preview/build output.

Shared CSS property metadata requirements:

1. SCADA Builder V2 and Studio Element+ must consume the same property metadata catalog where possible.
2. Each metadata entry defines allowed element/component kinds, unit support, default value, validation severity, and export behavior.
3. Runtime effect properties must be modeled explicitly enough for events and tag conditions to target them by id.
4. Free-form CSS can exist only as an advanced escape hatch after validation and export scoping rules are defined.
5. The first implementation should prefer a curated property matrix over a full arbitrary CSS editor.

## 14. Preview and Build Parity

Preview and build must consume the same scene model.

Rules:

1. The editor preview and generated output use the same CSS normalization layer.
2. HTML element defaults must be controlled.
3. `box-sizing: border-box` is global.
4. Fonts, colors, overflow and basic spacing are explicit.
5. WebView2 preview must not depend on untracked local browser defaults.
6. The build pipeline must be able to reproduce the preview layout.

The UI must expose validation differences before export.

## 15. Project Creation UI

New project flow:

1. Choose root path.
2. Enter project name.
3. Choose base canvas size.
4. Choose responsive mode.
5. Choose authoring mode: desktop first, tablet first or mobile first.
6. Confirm initial device presets.
7. Create folder structure.
8. Create `project.xaml`.
9. Open first scene in workspace.

Initial project structure:

```text
MyScadaProject/
  project.xaml
  scenes/
    desktop/
    tablet/
    mobile/
  assets/
    images/
    icons/
    symbols/
  libraries/
  exports/
  backups/
  logs/
```

## 16. Accessibility and Usability

Minimum requirements:

1. Keyboard access for ribbon commands and panel focus.
2. Tooltips for icon-only commands.
3. Visible focus state.
4. Clear disabled states.
5. Sufficient contrast for editor text and controls.
6. No hidden critical state based only on color.
7. Status and warning messages written in user-facing language.

## 17. Initial Implementation Priority

Recommended first implementation sequence:

1. Main shell with five regions.
2. Ribbon tabs and command placeholders.
3. Dockable left/right/bottom panels.
4. Workspace document tabs.
5. Project palette with scene list.
6. Tool palette with active tool state.
7. Selection service with global lock selection.
8. Right panel context switching.
9. Status/warning bar.
10. Florizon theme resources.
11. Basic scene surface and preview device selector.
12. Property metadata foundation.

## 18. Acceptance Criteria

The UI shell is acceptable when:

1. The app opens to the editor shell, not a marketing or landing screen.
2. The ribbon shows `Fichier`, `Edition`, `Ecran`, `Selection` and command groups.
3. The left panel switches between `Outil` and `Projet`.
4. The center workspace supports multiple opened page tabs.
5. The right panel changes content when the active page, selection or tool changes.
6. Global lock selection is visible and behaviorally distinct from object lock.
7. Closed panels can be restored from `Edition -> Panneau`.
8. The bottom bar shows status and warnings.
9. The visual style follows the Florizon palette while staying dense enough for desktop editing.
10. UI actions route through commands and services rather than embedding business logic in views.

## 19. Open Decisions

1. Choose the exact docking library or internal docking implementation.
2. Confirm whether the ribbon is implemented with a third-party control, WPF custom controls or a native-compatible ribbon package.
3. Define final icon set.
4. Define exact visual treatment for global lock selection vs object lock.
5. Define the minimum property set for the first element types: text, image, group and imported legacy element.
6. Define the first shared CSS property matrix for SCADA Builder V2 and Studio Element+.
7. Define which visual effects are CSS-only and which require generated runtime JavaScript.
