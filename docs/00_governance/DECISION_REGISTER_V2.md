# SCADA Builder V2 - Decision Register

Date: 2026-06-17
Status: Active authoritative decision register
Document version: `V2.1.2.0017`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Ajout de DEC-0022 pour les options runtime avancees des popup Fragment. |
| 2026-06-17 | `V2.1.2.0016` | `PENDING` | Ajout de DEC-0021 pour les actions runtime de bordure Element+. |
| 2026-06-17 | `V2.1.2.0015` | `PENDING` | Ajout de DEC-0020 pour `Fermer popup` et `Basculer popup`. |
| 2026-06-17 | `V2.1.2.0014` | `PENDING` | Ajout de DEC-0019 pour l'action runtime `Ouvrir popup`. |
| 2026-06-17 | `V2.1.2.0012` | `PENDING` | Ajout de DEC-0018 pour l'application runtime des valeurs de tags lues. |
| 2026-06-17 | `V2.1.2.0010` | `PENDING` | Ajout de DEC-0017 pour les actions objet conditionnelles basees sur tags importes. |
| 2026-06-17 | `V2.1.2.0009` | `PENDING` | Ajout de DEC-0016 pour les bindings Element+ `Lire valeur` et `Ecrire valeur`; DEC-0015 est supersedee. |
| 2026-06-17 | `V2.1.2.0008` | `PENDING` | Ajout de la decision d'import catalogue tags TF100Web et d'authoring `WriteTag`. |
| 2026-06-16 | `V2.1.2.0007` | `PENDING` | Ajout de la decision de curseur runtime pour les cibles cliquables FT100/TF100Web. |
| 2026-06-16 | `V2.1.2.0006` | `PENDING` | Ajout de la decision d'export runtime transparent pour les events portes par des groupes Element+. |
| 2026-06-16 | `V2.1.2.0005` | `PENDING` | Ajout de la decision hover automatique des boutons Element+. |
| 2026-06-16 | `V2.1.2.0004` | `PENDING` | Ajout de la decision de registre contractuel des evenements Element+ et fonctions runtime. |
| 2026-06-16 | `V2.1.2.0002` | `PENDING` | Ajout de la decision Element+ only pour le groupement dans la scene principale. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du registre decisionnel centralise avec decisions actives, historiques et regles de deprecation. |

## 1. Rules

1. Decisions are append-only records.
2. Superseded decisions remain present.
3. Deprecated or superseded decisions require datetime, commit, reason, and successor when applicable.
4. Owner documents apply decisions; they do not replace this register.
5. `PENDING` is allowed only before the delivery commit exists.

## 2. Decision Template

```text
DEC-0000
Title:
Status: Active | Deprecated | Superseded
Created:
Created in commit:
Deprecated:
Deprecated in commit:
Superseded by:
Owner document:
Context:
Decision:
Consequences:
Regression coverage:
```

## 3. Active Decisions

### DEC-0001 - Enterprise Documentation Architecture

Status: Active
Created: 2026-06-16 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/00_governance/DOCUMENTATION_STANDARD_V2.md`

Context:

The previous `/docs` tree mixed architecture, contracts, status, implementation history, roadmaps, and unresolved topics. This made team work risky because a developer could update the wrong source of truth.

Decision:

Documentation is modularized into governance, product, architecture, runtime contracts, editor contracts, Studio Element+, UI/UX, legacy migration, implementation status, archive, and generated documentation.

Consequences:

Active contracts must live in their owner document. Legacy top-level files remain as historical source material until migration is complete.

Regression coverage:

`tools/docs/verify-docs.ps1`

### DEC-0002 - Append-Only Decision History

Status: Active
Created: 2026-06-16 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/00_governance/DECISION_REGISTER_V2.md`

Context:

Removing old decisions hides why behavior changed and makes regressions harder to analyze.

Decision:

A modified decision is not erased. It is marked `Deprecated` or `Superseded`, with datetime, commit, reason, and successor when applicable.

Consequences:

The decision register may grow, but team members can audit historical reasoning.

Regression coverage:

`tools/docs/verify-docs.ps1`

### DEC-0003 - Current FT100/TF100Web Package Contract

Status: Active
Created: 2026-06-15 00:00 America/Toronto
Created in commit: `72350e3`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`

Context:

Older exports and integration notes used or referenced an `index.html` entry point.

Decision:

Current SCADA Builder V2 FT100/TF100Web exports use root `manifest.json` and page-local `<page-id>/<page-id>.html`. `index.html` is deprecated for current packages. TF100Web may keep isolated legacy fallback support only for old packages.

Consequences:

New runtime documentation must not present `index.html` as the active export target.

Regression coverage:

`tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0004 - Shared Preview Build Export Model

Status: Active
Created: 2026-06-15 00:00 America/Toronto
Created in commit: `72350e3`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/03_runtime_contracts/PREVIEW_BUILD_EXPORT_CONTRACT_V2.md`

Context:

Preview, build, and export drift creates regressions where the editor displays behavior that exported runtime cannot reproduce.

Decision:

Preview, build, and export consume the same V2 project/scene model. Editor overlays, handles, diagnostics, drag rectangles, workzone state, and test UI are editor-only and must not become runtime/export geometry.

Consequences:

Contract-sensitive rendering changes require regression coverage.

Regression coverage:

`tests/ScadaBuilderV2.Tests/PreviewDocumentTests.cs`, `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0005 - Legacy Source Policy

Status: Active
Created: 2026-06-15 00:00 America/Toronto
Created in commit: `72350e3`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/07_legacy_migration/LEGACY_SOURCE_POLICY_V2.md`

Context:

`03_web_legacy`, `08_web_modernized`, inventory JSON, and saved V2 scene geometry can disagree.

Decision:

Legacy artifacts are evidence and migration inputs, not the final V2 model. `03_web_legacy/html_pages/*` is preferred for raw comparison. `08_web_modernized/*` is comparison/history material unless an explicit sanitized-source decision approves it for a specific case. `win00009` is known-good; `win00008` is a known divergence candidate.

Consequences:

Migration source selection must be explicit and traceable.

Regression coverage:

`tests/ScadaBuilderV2.Tests/ReferenceProjectModelTests.cs`, `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0006 - Polymorphic Selection And Durable Source Delete

Status: Active
Created: 2026-06-15 00:00 America/Toronto
Created in commit: `63c2475`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/SELECTION_CONTRACT_V2.md`

Context:

Source DOM nodes and Element+ scene objects are both editable selection targets, but inventory scope and WebView masking previously risked hiding selectable objects.

Decision:

Selection is polymorphic. Present non-editor source nodes and Element+ scene objects remain selectable. Commands resolve the selected target type after hit-testing. Durable source deletion uses scene state and `RemovedSourceElementIds`; CSS masking, `display:none`, and inventory omission are not durable delete mechanisms.

Consequences:

Selection, delete, undo/redo, save/reload, and export omission must share the same scene state.

Regression coverage:

`tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`, `tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs`, `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0007 - Page-Scoped Runtime Namespace

Status: Active
Created: 2026-06-15 00:00 America/Toronto
Created in commit: `63c2475`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`

Context:

TF100Web composes header, body, and footer in one DOM. Unscoped selectors collide across pages.

Decision:

Exported page HTML, CSS, DOM ids, and runtime action lookup are namespaced under the exported page root id, such as `#ft100-win00003`. Package-global `:root`, `html/body`, `[data-id="..."]`, `.ft100-*`, and `#Button1`-style selectors are invalid for current composition.

Consequences:

Every generated selector must be page-scoped or page-prefixed.

Regression coverage:

`tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0008 - Studio Element+ Canonical Selection Modifiers

Status: Active
Created: 2026-06-15 00:00 America/Toronto
Created in commit: `63c2475`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/05_studio_element_plus/STUDIO_ELEMENT_PLUS_SELECTION_CONTRACT_V2.md`

Context:

Studio Element+ requires stable multi-selection behavior for movement, grouping, properties, and `.sep` export.

Decision:

`Shift + clic` adds, `Alt + clic` removes, drag rectangle supports replace/add/remove, and selection overlays/handles/drag rectangles must not be exported into `.sep` component geometry.

Consequences:

Studio Element+ selection work must preserve these modifiers.

Regression coverage:

`tests/ScadaBuilderV2.Tests/ElementStudioEditorStateTests.cs`, `tests/ScadaBuilderV2.Tests/ElementStudioSourceRenderingTests.cs`, `tests/ScadaBuilderV2.Tests/StudioElementPlusContractTests.cs`

### DEC-0009 - Code Documentation And Generated Maps

Status: Active
Created: 2026-06-16 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/00_governance/DOC_SYNC_SKILL_SPEC_V2.md`

Context:

Team work requires a reliable map from code functions to contracts, decisions, tests, and diagrams.

Decision:

Public APIs require XML documentation. Contract-sensitive private methods require intent documentation. Generated documentation under `docs/10_generated` must be updated or verified by `scada-v2-doc-sync` and `tools/docs/verify-docs.ps1`.

Consequences:

Documentation verification may report existing code documentation gaps until remediation is complete.

Regression coverage:

`tools/docs/verify-docs.ps1`

### DEC-0010 - Scene Grouping Is Element+ Only

Status: Active
Created: 2026-06-16 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/COMMANDS_CONTRACT_V2.md`

Context:

Legacy source nodes are migration inputs, while Element+ scene objects are the editable model-backed objects that can participate in durable scene grouping, undo/redo, save/reload, preview, and export.

Decision:

The SCADA Builder scene `Grouper` command is Element+ only. Selecting two or more Element+ scene objects creates a real `ScadaElementKind.Group`. Attempting to group legacy source nodes warns the user to convert them to Element+ first. Mixed legacy/source plus Element+ selections are rejected.

Consequences:

The legacy-only frame grouping workflow is decommissioned. Context menus and panels must not create groups from source DOM nodes. Scene grouping must route through the V2 scene model and history.

Regression coverage:

`tests/ScadaBuilderV2.Tests/ScadaSceneGroupTests.cs`, `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

### DEC-0011 - Element+ Event Registry And Runtime Function Contracts

Status: Active
Created: 2026-06-16 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`

Context:

Element+ objects must support industrial HMI-style runtime events without each UI surface inventing its own trigger names, function names, or action payloads.

Decision:

Element+ events are authored through a central registry. UI labels are French, while persisted/runtime bindings keep stable browser triggers and action function contracts. One Element+ may hold multiple bindings, including several `Clic` bindings. The first implemented function is `ChangePage`, stored as `ScadaActionKind.Navigate` plus `TargetPageId`.

Consequences:

New event triggers, conditional execution, tag writes, popup opening, visual effects, and scripts must be added to the registry and owner contract before becoming active UI choices.

Regression coverage:

`tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`, `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`, `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`, `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0014 - Runtime Pointer Cursor For Clickable Targets

Status: Active
Created: 2026-06-16 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`

Context:

FT100Web operators need a default button cursor on hover and click for obvious runtime affordance. The behavior must not depend on per-page manual CSS or FT100Web-specific overrides when the SCADA Builder V2 package already knows which objects are buttons or runtime event targets.

Decision:

FT100 export generates page-scoped `cursor: pointer` CSS for Element+ buttons and elements carrying `data-scada-events`. The cursor rule applies to descendants and active click state so nested text/buttons inside group wrappers retain the button cursor during hover and click.

Consequences:

TF100Web receives the pointer affordance as part of the package contract. Future disabled-state cursor behavior can extend the button behavior contract without weakening the default clickable-target cursor.

Regression coverage:

`tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0013 - Runtime Group Event Wrapper Export

Status: Active
Created: 2026-06-16 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`

Context:

Element+ groups may own runtime events such as `Clic -> Changer de page`. Flattening every group during FT100 export removes the DOM node that should carry `data-scada-events`, leaving exported `Navigate` actions unreachable in TF100Web.

Decision:

Groups without runtime events may remain flattened. Groups with runtime events must export a transparent, page-scoped runtime wrapper that carries `data-scada-events`, preserves child geometry relative to the group, and introduces no editor overlays or visual decoration.

Consequences:

TF100Web can continue to consume `data-scada-events` plus the package `Actions` list without a separate group event contract. Required display size must account for the exported runtime group surface.

Regression coverage:

`tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0012 - Element+ Button Default Hover Behavior

Status: Active
Created: 2026-06-16 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md`

Context:

Industrial HMI buttons need immediate visual feedback in the deployed FT100Web runtime so operators can identify active click targets. SCADA Builder V2 authors the intent as model metadata, and the FT100 export may materialize that metadata as page-scoped runtime CSS.

Decision:

Every Element+ button has hover metadata by default unless the button is disabled or its hover behavior is explicitly disabled in the `Bouton` tab. The `Bouton` tab is shown between `Style` and `Evenement` for Element+ buttons and writes model-backed `ScadaButtonBehavior` state. SCADA Builder V2 preserves this metadata in the manifest and generates page-scoped FT100 CSS `:hover` rules when the metadata is enabled.

Consequences:

The editor preview must not apply the hover effect as an authoring-side behavior. The FT100 runtime package may include generated scoped hover CSS, while FT100Web can also consume the manifest metadata for richer runtime behavior.

Regression coverage:

`tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`, `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`, `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`, `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0015 - TF100Web Tag Catalog Import And WriteTag Authoring

Status: Superseded
Created: 2026-06-17 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: 2026-06-17 00:00 America/Toronto
Deprecated in commit: `PENDING`
Superseded by: DEC-0016
Owner document: `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`

Context:

TF100Web exports a `tf100web-scada-tags-v1` tag file that SCADA Builder V2 must consume so designers can bind Element+ events and future inputs to real industrial tags instead of raw free-form `TagBinding` placeholders.

Decision:

SCADA Builder V2 imports the TF100Web tag export into a project-level `ScadaTagCatalog`. The WPF editor stores an import snapshot under `imports/tags`, persists the catalog in `project.json`, and exposes writeable enabled tags to the Element+ event modal. `WriteTag` is now an authorable runtime action stored as `ScadaActionKind.WriteTag` with `TagId` and `Value`.

Consequences:

FT100/TF100Web manifests include the imported tag catalog and write-tag actions. The exported runtime script calls `window.tf100webScadaBuilder.writeTag` when available and emits `scada-builder-write-tag` as an integration event. Read/display bindings, conditions, degraded semantics, and richer TF100Web server write behavior remain future slices.

Regression coverage:

`tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`, `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`, `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0018 - Runtime Read Tag Value Application

Status: Active
Created: 2026-06-17 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`

Context:

`Lire valeur` bindings must do more than request values. TF100Web needs a page-level integration point to push live tag values into exported Element+ fields while reusing the same runtime tag value cache for conditions.

Decision:

Exported FT100/TF100Web pages build a read-binding index from `data-scada-read-tag`, expose `window.scadaBuilderSetTagValue(tagId, value, meta)`, and listen for `scada-builder-tag-value` browser events. When a value arrives, the runtime stores it in `window.scadaBuilderTagValues[tagId]`, applies it to every read-bound Element+ using an input/select/textarea value when present or text content otherwise, and emits `scada-builder-tag-value-applied` for integration diagnostics.

Consequences:

`Lire valeur` can now display runtime values in deployed pages. Conditions can evaluate freshly pushed values through the shared `window.scadaBuilderTagValues` cache. Runtime value application does not emit a `change` event, so read refreshes do not loop back into `Ecrire valeur`.

Regression coverage:

`tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0019 - Fragment Popup Runtime Action

Status: Active
Created: 2026-06-17 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`

Context:

Industrial HMI screens need click-triggered popups for detail panels and equipment faceplates. The existing V2 project model already distinguishes page type `Fragment`, and FT100/TF100Web export already compiles each included page under its own page namespace.

Decision:

`Ouvrir popup` is implemented as registry function `OpenPopup` persisted as `ScadaActionKind.MountFragment` with `TargetPageId`. The authoring UI only offers compiled `Fragment` pages for this function. Build/export validation rejects missing popup fragments, non-fragment targets, and fragments excluded from build. Exported runtime mounts the target fragment page in a centered iframe popup, provides a local close button and outside-click close behavior, and emits popup opened/closed diagnostic events.

Consequences:

The fragment keeps its exported page namespace and internal behaviors because it is loaded as its own compiled page. Close/toggle action functions are covered by DEC-0020. Advanced popup placement, named host regions, lifecycle state reset rules, and multi-instance management remain future revisions.

Regression coverage:

`tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`, `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`, `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0020 - Popup Close And Toggle Runtime Actions

Status: Active
Created: 2026-06-17 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`

Context:

After `Ouvrir popup`, HMI screens need model-backed actions to close a specific popup fragment and to toggle that fragment from a host page or from controls inside the loaded fragment.

Decision:

`Fermer popup` is persisted as `ScadaActionKind.ClosePopup`, and `Basculer popup` is persisted as `ScadaActionKind.TogglePopup`. Both use `TargetPageId` and the same compiled `Fragment` validation contract as `Ouvrir popup`. Exported runtime closes or toggles the page-local popup overlay when the action runs in the host page. When the action runs inside an iframe-loaded fragment, the runtime posts a `scada-builder-v2` popup request to the parent page so the parent can close or toggle the owning overlay.

Consequences:

The popup cycle now covers open, close, and toggle without adding popup instances or host-region model fields. Explicit placement, named host regions, lifecycle reset policy, multi-instance policy, and popup sizing presets remain future revisions.

Regression coverage:

`tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`, `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`, `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0022 - Advanced Fragment Popup Runtime Options

Status: Active
Created: 2026-06-17 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`

Context:

Fragment popup actions need deterministic HMI layout behavior beyond the centered default popup. Operators need faceplates to open in predictable positions, docked surfaces, or a model-backed host region while preserving backward compatibility for existing popup actions.

Decision:

Popup actions may carry optional `ScadaPopupOptions` with `Position`, `SizePreset`, `AllowMultiple`, `ResetOnOpen`, and `HostRegionId`. Existing popup actions without options keep the centered large single-instance behavior. `HostRegion` placement requires a valid Element+ target and is rejected by build/export validation when missing. Exported runtime applies placement, size preset, multi-instance behavior, and iframe reset policy from the action options.

Consequences:

Popup placement and sizing are now model-backed and exportable without introducing editor overlays or custom per-page scripts. Named host regions are represented by Element+ host ids in the current slice; richer named host registries and drag-authored placement editors can build on the same options model.

Regression coverage:

`tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`, `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`, `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0021 - Runtime Object Border Actions

Status: Active
Created: 2026-06-17 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`

Context:

HMI authors need simple hover and click feedback that can highlight a target Element+ object or group without turning editor overlays into runtime geometry. The behavior must be authored through the same Element+ event/action model as navigation, visibility, popup, and tag bindings.

Decision:

`Afficher bordure`, `Masquer bordure`, and `Basculer bordure` are authorable Element+ event functions targeting another Element+ object. They persist as `ScadaActionKind.SetClass`, `RemoveClass`, and `ToggleClass` with `TargetElementId` and the standard `scada-runtime-border-highlight` class. Exported runtime applies the class with page-scoped CSS using `classList.add`, `classList.remove`, or `classList.toggle`.

Consequences:

Hover-enter and hover-exit can now show and hide a runtime border on an Element+ target. This is a runtime visual action only; it does not create editor selection overlays, modify `.sep` geometry, or introduce custom per-action CSS authoring. Richer blink, glow, pulse, alarm, and degraded visual effects remain future actions.

Regression coverage:

`tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`, `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`, `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0017 - Conditional Object Visibility Actions

Status: Active
Created: 2026-06-17 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`

Context:

HMI operators need deterministic object visibility behavior such as showing, hiding, or toggling an Element+ only when an imported process tag satisfies a simple condition. These conditions must be persisted in the scene model and exported with the same runtime action contract as other Element+ events.

Decision:

`Afficher objet`, `Masquer objet`, and `Basculer visibilite` are authorable Element+ event functions. They target another Element+ object and may carry one optional `ScadaActionCondition` with an imported tag id, an operator, and an optional comparison value. Supported operators are `Vrai`, `Faux`, `=`, `<>`, `>`, `>=`, `<`, and `<=`. Boolean `Vrai/Faux` operators are valid only for boolean imported tags and are rejected by build/export validation when used on non-boolean tags.

Consequences:

FT100/TF100Web manifests include the condition on the action. Exported runtime evaluates conditions through `window.tf100webScadaBuilder.getTagValue(tagId)` when available, or `window.scadaBuilderTagValues[tagId]` as a simple integration dictionary. If a condition cannot be evaluated, the action does not run. Expression authoring, degraded state handling, and compound conditions remain future revisions.

Regression coverage:

`tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`, `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0016 - Element Value Bindings For Imported Tags

Status: Active
Created: 2026-06-17 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`

Context:

Operator-entered values are runtime data and must not be persisted as literal design-time action values. SCADA Builder V2 needs to bind Element+ inputs to imported TF100Web tags for read and write behavior while keeping event triggers separate from value synchronization.

Decision:

SCADA Builder V2 persists Element+ value bindings on element data as optional `ReadTagId` and `WriteTagId`. The Element+ event modal exposes `Lire valeur` and `Ecrire valeur` as authorable functions before the trigger selector. These functions require a target tag, do not use event triggers, and disable the trigger control in the UI. The target tag selector lists enabled imported tags as `Nom du tag | datatype | Nom de l'appareil`. `Ecrire valeur` is valid only for editable input Element+ objects and writeable tags; build/export validation rejects read-only elements, non-inputs, read-only tags, and missing tag references. `WriteTag` remains a legacy action kind for compatibility, but it is not the active authoring path.

Consequences:

FT100/TF100Web manifests include imported tags and per-element value binding metadata. Exported HTML emits `data-scada-read-tag` and `data-scada-write-tag`; runtime script emits read requests and writes the operator-entered input value through `window.tf100webScadaBuilder.writeTag` when available. Local SCADA Builder tag creation is intentionally deferred until project protocol import exists.

Regression coverage:

`tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`, `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`, `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
