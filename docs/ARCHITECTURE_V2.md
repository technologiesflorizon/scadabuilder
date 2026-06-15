# SCADA Builder V2 - Architecture

Date: 2026-06-15
Status: Draft
Document version: `V2.1.1.0030`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Normalisation du header documentaire et rattachement a l'arbre documentaire stable. |
| 2026-06-15 | `V2.0.0.0002` | `2b59efb` | Baseline initiale du depot SCADA Builder V2. |

## 1. Objective

SCADA Builder V2 must be a compartmentalized C#/.NET desktop editor, not a UI monolith.

The application must separate:

1. UI composition.
2. Application orchestration.
3. Domain model and rules.
4. Infrastructure adapters.
5. Preview/build generation.

Primary domain rule:

SCADA Builder V2 owns a modern official domain model. Legacy Wonderware/ArchestrA files and `AMR_REF_SCADA` are source material for import, extraction, modernization, and comparison only. They must not become the project model that preview and build depend on.

The UI may display state and collect user intent, but it must not own project rules, command behavior, serialization, import logic, build logic, or future undo/redo history.

## 2. Target Stack Direction

Primary direction:

1. C#/.NET desktop application.
2. MVVM-friendly UI shell.
3. WebView2 or equivalent embedded browser for HTML/CSS preview when needed.
4. Domain model serializable to project files, with `project.xaml` as the main project contract.
5. Modular services registered through dependency injection.

The exact UI framework can be finalized later, but the architecture must keep the same boundaries whether the shell is WPF, WinUI, Avalonia, or another .NET desktop stack.

## 3. Layer Rules

Dependency direction:

```text
UI -> Application -> Domain
UI -> Application -> Infrastructure abstractions
Infrastructure -> Application/Domain abstractions
```

Rules:

1. UI references Application contracts and view models.
2. Application references Domain.
3. Domain references no UI framework, no file system, no WebView, no database, no HTTP, and no platform-specific API.
4. Infrastructure implements ports defined by Application or Domain-facing contracts.
5. Infrastructure must not call UI directly.
6. Preview/build consumers must read the same project model used by the editor.
7. Cross-module communication goes through commands, queries, events, or explicit services.

Forbidden shortcuts:

1. Event handlers directly mutating project files.
2. Property panels directly editing serialized XML.
3. Canvas controls owning business rules for selection, grouping, responsive variants, or bindings.
4. Build/export code reading UI controls instead of the project model.
5. FT100 import logic coupled to a ribbon button or panel.

## 4. Solution Structure

Initial project boundaries:

```text
SCADA.Builder.App
  Desktop startup, dependency injection, composition root.

SCADA.Builder.UI
  Shell, ribbon, panels, workspace, dialogs, view models, UI adapters.

SCADA.Builder.Application
  Commands, queries, application services, project session, state coordination.

SCADA.Builder.Domain
  Project model, scenes, elements, selection concepts, layout variants, bindings, validation rules.

SCADA.Builder.Infrastructure
  File system, XAML serialization, image/assets IO, legacy import adapters, FT100 import adapters.

SCADA.Builder.Preview
  Preview document generation, WebView bridge contracts, preview runtime packaging.

SCADA.Builder.Build
  HTML/CSS/JS export pipeline, CSS reset, responsive CSS generation.

SCADA.Builder.Tests
  Domain tests, command tests, serialization tests, build/preview contract tests.
```

The names can be adjusted to match the final repository convention, but each responsibility must remain separated.

## 5. Domain Modules

The Domain layer owns stable business concepts:

1. Project identity and metadata.
2. Project settings and responsive strategy.
3. Device presets and safe areas.
4. Scenes and scene variants.
5. Elements, groups, styles, properties, and z-order.
6. Selection identity, object lock flags, and selection-lock intent.
7. Bindings, action definitions, and script references.
8. Legacy traceability references.
9. Validation rules and warning categories.

The official Domain layer must distinguish:

1. Modern V2 project and scene objects.
2. Legacy source traces attached to modern objects.
3. Legacy extraction candidates that have not yet become modern objects.
4. Legacy comparison sessions used only by viewer/QA workflows.

Legacy ids such as `legacy:795` are trace metadata, not user-facing object identity.

Domain objects must be testable without launching the desktop application.

## 6. Application Modules

The Application layer owns use cases:

1. Create project.
2. Open project.
3. Save project.
4. Open scene tab.
5. Close scene tab.
6. Select element.
7. Lock or unlock current selection.
8. Lock or unlock object editing.
9. Add, move, resize, group, duplicate, delete, or reorder elements.
10. Change selected element properties.
11. Change project responsive mode or device preset.
12. Import legacy content.
13. Import FT100 mappings.
14. Validate project.
15. Generate preview.
16. Build/export project.

Application services coordinate commands and state. They may call repositories, importers, validators, preview services, and build services through interfaces.

## 7. UI Modules

The UI layer is split into replaceable panels and surfaces:

1. Shell.
2. Top ribbon and menu area.
3. Left panel: tools and project navigation.
4. Central workspace: scene tabs and canvas host.
5. Right panel: page, element, property, and library context.
6. Bottom panel: status, warnings, errors, save state, and build state.
7. Dialogs: project creation, device presets, import, export, validation details.
8. Legacy Viewer: read-only legacy scene display.
9. Legacy Extraction Workspace: candidate review and conversion.
10. Compare View: side-by-side or overlay comparison between legacy and V2 scene.

Each panel reads from view models and sends user intent through the command registry. Panels must not duplicate command enablement rules locally.

## 8. Command Flow

Default command flow:

```text
User action
  -> UI command binding
  -> Command registry
  -> Application command handler
  -> Domain operation
  -> Project state update
  -> Domain/application events
  -> View model refresh
  -> UI render
```

The command handler is the only place where a user action becomes a project mutation.

Benefits:

1. One command definition can feed ribbon, menu, toolbar, shortcuts, and context menu.
2. Enable/disable rules are centralized.
3. Future undo/redo can wrap command execution.
4. Tests can execute commands without UI automation.
5. Panels remain independent.

## 9. State Ownership

The Application layer owns the active editing session:

1. Loaded project.
2. Open scene tabs.
3. Active scene.
4. Current selection.
5. Selection lock state.
6. Active tool.
7. Active panel layout.
8. Dirty flags.
9. Validation warnings.
10. Preview/build status.

The Domain layer owns persistent project state.

The UI layer owns only transient display state:

1. Focused control.
2. Scroll position.
3. Expanded visual sections.
4. Splitter drag in progress.
5. Hover state.

Persistent user layout preferences may be saved by Infrastructure, but they must not be mixed into the SCADA project model unless they affect the generated project.

## 10. Panels and Extensibility

Panels are independent modules registered with metadata:

1. Panel id.
2. Display name.
3. Default dock position.
4. Can resize.
5. Can collapse.
6. Can close.
7. Restore command id.
8. Required application services.

Panel visibility and docking state are coordinated by the Application layer so `Edition -> Panneau -> <Nom>` can restore closed panels consistently.

## 11. Preview and Build Boundary

Preview and build must share:

1. Project model input.
2. Style model input.
3. Responsive mode rules.
4. CSS reset/normalization rules.
5. Script/action validation.
6. Binding model.

Preview may add editor-only overlays for selection, handles, guides, and warnings. These overlays must be generated separately from the project runtime output.

Build output must not depend on UI control state.

## 12. Infrastructure Boundary

Infrastructure owns adapters:

1. Project file read/write.
2. Scene file read/write.
3. Asset copy and path resolution.
4. Legacy project import.
5. FT100 mapping import.
6. Logs and backups.
7. User settings storage.
8. External process or browser integration if needed.

Infrastructure must expose results as domain/application objects, diagnostics, or import reports. It must not return UI controls or require a panel to complete parsing.

Legacy adapters may read extracted Wonderware/ArchestrA material, but they must return source documents, extraction candidates, diagnostics, or trace metadata. They must not return final V2 scene elements without an explicit conversion step.

## 13. Future Undo/Redo Boundary

Undo/redo is not required to be complete in the first implementation, but the architecture must reserve the path.

Requirements now:

1. Project mutations pass through explicit commands.
2. Commands identify their target and input parameters.
3. Command handlers report state changes.
4. Domain operations avoid hidden global mutation.
5. Batch commands can group multiple low-level changes.
6. Generated changes can be tagged as editor, import, build, or migration operations.

Future implementation can add:

1. Undoable command records.
2. Before/after snapshots for small state changes.
3. Domain delta objects for large scene changes.
4. Transaction groups for drag, resize, paste, import, and responsive layout operations.

## 14. Testing Strategy

Minimum architecture tests:

1. Domain model tests without UI.
2. Command handler tests without desktop shell.
3. Command registry tests for duplicate ids and missing labels.
4. Selection state tests, including locked selection and object lock distinction.
5. Project state tests for open tabs, dirty flags, and active scene.
6. Serialization round-trip tests for `project.xaml`.
7. Preview/build contract tests for shared CSS normalization and responsive mode rules.

UI automation should be added for critical workflows, but it must not be the only way to verify application behavior.

## 15. Open Decisions

1. Final desktop UI framework.
2. Exact project/solution naming convention.
3. Whether preview generation lives in a separate assembly from build generation or shares a lower-level rendering core.
4. Exact format for scene files and layout variant files.
5. Granularity of future undo/redo deltas.
