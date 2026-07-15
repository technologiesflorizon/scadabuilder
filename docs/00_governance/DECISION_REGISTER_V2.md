# SCADA Builder V2 - Decision Register

Date: 2026-07-15
Status: Active authoritative decision register
Document version: `V2.1.4.0024`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-15 | `V2.1.4.0024` | `3f6e6a5` | Ajout de `DEC-0040` pour la sous-surface Tableau, l'authoring avancé des cellules et le verrouillage persistant de position de tous les Element+. |
| 2026-07-14 | `V2.1.4.0016` | `10cfa72` | `DEC-0039` implementee avec modele Tableau, edition type tableur, export `.sb2`, tests et ruban Inserer hierarchique; smoke interactif isole restant. |
| 2026-07-14 | `V2.1.4.0015` | `95a57ac` | Ajout de `DEC-0039` pour l'Element+ Tableau moderne, l'edition type tableur, le ruban Inserer hierarchique et l'extraction des responsabilites hors `MainWindow`. |
| 2026-07-14 | `V2.1.4.0011` | `PENDING` | DEC-0038 passée de décision approuvée à tranche implémentée et couverte; la vérification UI manuelle et la migration du projet réel restent séparées. |
| 2026-07-14 | `V2.1.4.0010` | `c5d6f0e` | Ajout de DEC-0038 pour l’identité moderne des pages, les commandes partagées, l’historique projet, la persistance atomique et la compatibilité `.sb2`. |
| 2026-07-13 | `V2.1.4.0003` | `b954d46` | Ajout de DEC-0037 pour le contrat de style avancé Element+ model-backed et la conservation HTML/CSS TF100Web. |
| 2026-07-09 | `V2.1.4.0002` | `PENDING` | Ajout de DEC-0036 pour les references de tags d'expressions d'etat : libelle humain conserve, Id canonique obligatoire a l'export TF100Web. |
| 2026-06-19 | `V2.1.3.0001` | `620e914` | Ajustement de DEC-0032 pour la galerie Formes 32x32 sans libelles visibles. |
| 2026-06-19 | `V2.1.3.0000` | `b195fe0` | Ajout de DEC-0032 pour la galerie Formes du ruban Inserer et le placement ligne/fleche en deux points. |
| 2026-06-19 | `V2.1.2.0044` | `c50cbcf` | Mise a jour de DEC-0031 apres extraction de la palette laterale d'outils vers le catalogue semantique. |
| 2026-06-19 | `V2.1.2.0043` | `fde1b31` | Mise a jour de DEC-0031 apres retrait du fallback XAML statique du ruban superieur. |
| 2026-06-19 | `V2.1.2.0042` | `0825cfe` | Mise a jour de DEC-0031 apres activation des commandes de groupement depuis le ruban. |
| 2026-06-19 | `V2.1.2.0041` | `88a3e8b` | Mise a jour de DEC-0031 apres extraction du catalogue de ruban dans Application. |
| 2026-06-19 | `V2.1.2.0040` | `335adfb` | Mise a jour de DEC-0031 apres implementation du rendu de ruban depuis registre. |
| 2026-06-19 | `V2.1.2.0039` | `e5f8a82` | Ajout de DEC-0031 pour le ruban superieur groupe et le registre d'icones semantiques. |
| 2026-06-17 | `V2.1.2.0025` | `58567eb` | Mise a jour de DEC-0030 apres implementation TF100Web des masques `DisplayFormat` `#`. |
| 2026-06-17 | `V2.1.2.0024` | `PENDING` | Ajout de DEC-0030 pour la refonte de l'onglet Donnees Element+ et le format numerique actif. |
| 2026-06-17 | `V2.1.2.0022` | `PENDING` | Ajout de DEC-0029 pour l'intake TF100Web des events de binding `ValueBindings` depuis `.sb2`. |
| 2026-06-17 | `V2.1.2.0020` | `c2f0b6f` | Ajout de DEC-0028 pour l'export `.sb2` non bloquant et la validation CSS indentee. |
| 2026-06-17 | `V2.1.2.0019` | `bd6515e` | Ajout de DEC-0027 pour l'export `.sb2` FT100 et le gate anti-collision. |
| 2026-06-17 | `V2.1.2.0018` | `ad364a6` | Ajout de DEC-0026 pour le contrat d'intake fragment audite dans TF100Web. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Ajout de DEC-0025 pour les effets visuels runtime standards. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Ajout de DEC-0024 pour le bridge lifecycle runtime global. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Ajout de DEC-0023 pour les groupes de conditions runtime et politique degradee. |
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
Created in commit: `bd6515e`
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
Created in commit: `c2f0b6f`
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

### DEC-0025 - Standard Runtime Visual Effects

Status: Active
Created: 2026-06-17 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`

Context:

HMI screens need standard visual effects for operator attention and degraded/alarm states without exporting editor overlays or hand-authored page CSS. These effects must be model-backed actions and must remain scoped to the exported page root.

Decision:

SCADA Builder V2 implements standard visual effect functions for blink, glow, pulse, alarm highlight, and degraded treatment. Each effect has start, stop, and toggle functions that persist as `SetClass`, `RemoveClass`, or `ToggleClass` with a fixed page-scoped runtime CSS class. FT100 export emits the standard effect CSS and keyframes under the page namespace.

Consequences:

Visual effects can be authored through the same Element+ event/action model as popup, visibility, and border actions. Custom effect styling and a visual effect style editor remain future revisions.

Regression coverage:

`tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`, `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`, `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0026 - Audited TF100Web Fragment Intake Contract

Status: Active
Created: 2026-06-17 00:00 America/Toronto
Created in commit: `ad364a6`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`

Context:

TF100Web was pulled from `origin` in `F:\Projet\Git\TF100Web` on branch `implementation_scada_builder` to commit `7d57600`. The current SCADA Builder V2 documentation described the full exporter-emitted runtime script as if TF100Web consumed it directly, but the active TF100Web code extracts only the page root fragment and runs a host-side runtime in `static/asset/js/station/visualisation_import.js`.

Decision:

SCADA Builder V2 documentation must record the audited TF100Web intake contract separately from the exporter contract. TF100Web currently requires `scada-builder-v2-ft100-package/manifest.json`, validates compiled page entries, extracts `<div id="ft100-<page-id>">`, loads sibling page CSS and rewritten assets, composes header/body/footer fragments, and executes host-side navigation plus mapping refresh/write behavior. Scripts emitted outside the extracted root in the SCADA Builder page HTML are not executed by this intake path.

Consequences:

Exporter features such as lifecycle bridge, popup runtime, condition evaluation, read/write tag page hooks, border/effect actions, and non-navigation actions remain implemented exporter behavior but are a TF100Web parity gap until TF100Web executes the exported page script or implements equivalent host-side handlers. Future SCADA Builder export changes must be tested against the audited TF100Web intake contract or documented as exporter-only.

Regression coverage:

`tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`, `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`

### DEC-0027 - FT100 .sb2 Archive Export And Collision Gate

Status: Active
Created: 2026-06-17 00:00 America/Toronto
Created in commit: `bd6515e`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`

Context:

FT100 imports SCADA Builder V2 packages through `.sb2` uploads. The audited TF100Web intake treats `.sb2` as a ZIP archive, extracts `scada-builder-v2-ft100-package`, validates the root manifest and page fragments, then composes header/body/footer roots in a single DOM. A valid transfer format must therefore protect both archive shape and page-local identity.

Decision:

SCADA Builder V2 exports `.sb2` archives by generating the existing normalized FT100 package in a staging directory, rewriting legacy source ids into the page namespace, validating that package against the TF100Web intake contract, and zipping the staging root so the archive top-level entry is `scada-builder-v2-ft100-package/`. The validation gate blocks missing manifests, unsafe paths, missing page roots, invalid header/footer references, duplicate DOM ids, unscoped DOM ids, and global CSS selectors that can collide during TF100Web composition.

Consequences:

The folder export remains available for diagnostics, while `.sb2` is the preferred FT100 transfer artifact. SCADA Builder V2 may refuse to create `.sb2` archives for pages that still contain raw global ids or unscoped CSS, even if the folder export can be generated. Runtime parity gaps documented in DEC-0026 remain separate from archive/import compatibility.

Regression coverage:

`Ft100PackageValidator`, `Ft100SceneExporter.ExportProjectArchiveAsync`, and targeted coverage in `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`.

### DEC-0028 - Nonblocking FT100 .sb2 Export Feedback

Status: Active
Created: 2026-06-17 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`

Context:

FT100 `.sb2` export can copy many source assets, write page folders, validate the package, and compress the staging directory. Running that archive generation on the WPF UI thread makes SCADA Builder V2 appear frozen. The `.sb2` validator also rejected otherwise valid page-scoped CSS when a generated or author-provided selector line was indented before `#ft100-<page-id>`.

Decision:

SCADA Builder V2 shows an indeterminate progress bar in the bottom status bar while `.sb2` export is active and runs archive generation off the WPF UI thread. The validation gate normalizes leading whitespace before checking CSS id selectors, accepting `#ft100-<page-id>` and `#ft100-<page-id>__*` id tokens while still rejecting package-global ids.

Consequences:

Operators receive visible feedback during long exports and the shell remains responsive while package generation runs. The `.sb2` gate remains strict against unscoped CSS but no longer fails valid indented page-scoped selectors.

Regression coverage:

`tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0029 - TF100Web Host Intake For SCADA Builder Binding Events

Status: Active
Created: 2026-06-17 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`

Context:

SCADA Builder V2 exports `Lire valeur` and `Ecrire valeur` as binding events on Element+ data (`ReadTagId` and `WriteTagId`) and as manifest `ValueBindings`. TF100Web imports `.sb2` through the UI, extracts only page root fragments, and runs a host-side runtime that refreshes and writes values through `data-scada-role`, `data-scada-mapping-id`, and related attributes.

Decision:

TF100Web host intake must consume SCADA Builder V2 `ValueBindings` directly from the `.sb2` manifest. `tf100.mapping.<id>` resolves to the TF100Web `RegisterMapping` id. TF100Web injects host-runtime mapping attributes onto the page-scoped Element+ DOM id (`ft100-<page-id>__<element-id>`) while also retaining compatibility with legacy unscoped ids. When read and write bindings target different mappings, the read mapping remains `data-scada-mapping-id` and the write mapping is carried by `data-scada-write-mapping-id`.

Consequences:

`win00007 / Element+ Text20 / tf100.mapping.180` is a valid production acceptance candidate for `.sb2` binding-event intake. Hardcoded `win00008` bindings are no longer the intended acceptance path for current SCADA Builder V2 packages. Page scripts outside the extracted fragment remain a separate parity topic for popup, visual, lifecycle, and conditional action events.

Regression coverage:

`F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`

### DEC-0030 - Element+ Data Tab Active Numeric Display Contract

Status: Active
Created: 2026-06-17 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md`

Context:

The Element+ `Donnees` tab exposed overlapping numeric controls: `Format affichage`, `Decimales`, `Unite`, and legacy `Mapping / Tag`. Current SCADA Builder V2 binding authoring uses `Lire valeur` and `Ecrire valeur`, not raw `TagBinding`, and TF100Web receives the display instruction through exported `DisplayFormat`.

Decision:

`Format affichage` is the active numeric display contract for Element+ authoring and export. Hash masks such as `##.#` define visible digit budget and decimal placement; for example, value `999` with `##.#` displays as `99.9`, and the maximum visible value for that mask is `99.9`. `Min` and `Max` remain active only for non-read-only numeric inputs and represent operator-entry clamp constraints. `Mapping / Tag`, `Decimales`, and `Unite` are legacy model fields retained for save/reload compatibility but removed from active authoring.

Consequences:

SCADA Builder V2 exports a single display signal to TF100Web through `DisplayFormat`. TF100Web commit `3c795c2` interprets hash masks such as `##.#` in its `.sb2` host runtime by applying decimal placement, visible digit budget, and display clamping; `fixed:n` remains a compatibility display mode.

Regression coverage:

`tests/ScadaBuilderV2.Tests/ElementGroupTests.cs`, `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`, `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`

### DEC-0031 - Grouped Top Ribbon And Semantic Icon Registry

Status: Active
Created: 2026-06-19 00:00 America/Toronto
Created in commit: `e5f8a82`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/06_ui_ux/UI_SPECIFICATION_V2.md`, `docs/06_ui_ux/ICON_STRATEGY_V2.md`, `docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md`

Context:

The WPF shell top ribbon previously exposed flat `StackPanel` command rows, mixed French and English labels, reused generic icons for unrelated commands, and represented many insert commands with temporary text glyphs. This made the command surface hard to scan and weakened the icon strategy.

Decision:

The top ribbon is organized by active tab plus grouped task families. Visible command buttons must use semantic icon keys from the central WPF icon registry. Commands that are shown but not implemented stay disabled with a reason tooltip instead of appearing executable.

Consequences:

The shell now renders the active top ribbon only through `RibbonCommandSurface`, bound to command metadata containing stable ids, labels, tooltips or disabled reasons, icon keys, grouped order, and executable state. The canonical default command catalog lives in `ScadaBuilderV2.Application.Commands.RibbonCommandCatalog`; WPF adapts that metadata for resource lookup and dispatch. The left-side tool palette also consumes semantic command metadata from the same catalog so `Icon.Tool.*` references are not duplicated as static button rows in `MainWindow.xaml`. The `Selection` ribbon exposes executable `object.group` and `object.ungroup` commands by routing to the same Element+ scene grouping workflows used by context menus. Future command additions must update the registry first and preserve grouped/overflow behavior. Legacy static XAML button rows have been removed and must not be reintroduced as a parallel command source.

Regression coverage:

`tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs`, `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`, `dotnet build ScadaBuilderV2.sln --no-restore`

### DEC-0032 - Insert Shape Gallery And Two-Point Shape Authoring

Status: Active
Created: 2026-06-19 00:00 America/Toronto
Created in commit: `b195fe0`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md`, `docs/05_studio_element_plus/STUDIO_ELEMENT_PLUS_SEP_CONTRACT_V2.md`, `docs/06_ui_ux/UI_SPECIFICATION_V2.md`, `docs/06_ui_ux/ICON_STRATEGY_V2.md`

Context:

The Insert ribbon shape commands must be visually distinct, must show the active insertion command, and must author real Element+ geometry. The previous standard shape surface exposed only rectangle, ellipse, line, and arrow; line and arrow creation consumed one click and produced generic horizontal geometry instead of a user-selected start/end point.

Decision:

The Insert ribbon `Formes` group is a large shape gallery capped at four columns, with 32x32 semantic shape icons, no visible shape-name labels inside the gallery buttons, tooltip labels for discoverability, and active-command visual state. The standard shape set includes rectangle, ellipse, circle, triangle, star, line, and arrow. Line and arrow insertion uses a two-point editor workflow: first click captures the start point, pointer movement shows an editor-only SVG preview, second click persists the final Element+ shape with `ScadaElementData.ShapeStartX`, `ShapeStartY`, `ShapeEndX`, and `ShapeEndY`. Escape cancels placement and clears the active ribbon command.

Consequences:

Preview and FT100 export render circle, triangle, star, line, and arrow as Element+-owned SVG content using the same persisted model data. Editor-only placement previews, selection overlays, handles, and drag rectangles remain outside exported `.sep` and FT100 runtime geometry. Future shape commands must enter the semantic command catalog before WPF visual templates or dispatch code are changed.

Regression coverage:

`tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs`, `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`, `tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`, `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0024 - Global Runtime Lifecycle Bridge

Status: Active
Created: 2026-06-17 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`

Context:

TF100Web integration and future customer scripts need stable lifecycle hooks without editing exported page internals or relying on ad hoc DOM polling.

Decision:

Every exported page exposes `window.scadaBuilderRuntime` with page id, root id, actions, and a `dispatch` helper. The runtime emits `scada-builder-page-ready` after event bindings are registered, `scada-builder-action-executed` after a runtime action successfully applies, and `scada-builder-runtime-error` when action execution throws.

Consequences:

TF100Web and customer scripts can subscribe to lifecycle diagnostics consistently across pages. The bridge does not execute arbitrary imported script code; script loading and script authoring remain a future controlled extension.

Regression coverage:

`tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

### DEC-0023 - Compound Runtime Conditions And Missing Tag Policy

Status: Active
Created: 2026-06-17 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`

Context:

Single tag conditions are insufficient for common HMI behavior such as showing an object when one of several states is active or only when several process constraints are simultaneously true. Runtime behavior must also be deterministic when a required tag value is unavailable.

Decision:

Runtime actions may carry an optional `ScadaActionConditionGroup` in addition to the legacy single `Condition`. The group contains one or more `ScadaActionCondition` entries, a `Mode` of `All` or `Any`, and a `MissingTagPolicy` of `BlockAction` or `AllowAction`. Build/export validation applies the same tag, datatype, boolean, and comparison-value checks to every condition in the group. Exported runtime evaluates the single condition and the group before applying the action.

Consequences:

Compound conditions and explicit missing-tag degraded behavior are now model-backed and exportable. Expression authoring and formula parsing remain out of scope; compound behavior is intentionally deterministic and limited to registered tag conditions.

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

### DEC-0033 - Interactive Icon Modernization Loop Replaces Autonomous AI Pipeline

Status: Active
Created: 2026-07-05 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/07_legacy_migration/MODERNIZATION_WORKFLOW_V2.md`

Context:

The `sep-ai-modernizer` autonomous pipeline (separate repository, Django
worker + GPU-hosted model, fully implemented) was evaluated against real
`.sep` components. Two failures: `Ventilateur2.sep` (AI-regenerated) reads
as a generic consumer "flat icon" (pastel gradients) rather than an
industrial SCADA symbol, and independently, the modernized piping icons
integrated into `win00008_updated.html` kept correct bounding boxes but no
longer touched those boxes' edges at the same relative positions as the
legacy originals, breaking the visual connection between neighboring pieces
(pipe to valve, pipe to tank).

Decision:

Icon artwork modernization is an interactive, human-in-the-loop process
(Claude Code session per `.sep`), not an autonomous service. Every
candidate icon must be authored as native inline SVG primitives (never a
raster or SVG-as-image), follow
`docs/07_legacy_migration/SCADA_2026_ICON_STYLE_GUIDE_V2.md`, and pass the
`tools/icon_modernization` junction-point check (2 pixel tolerance) before
human visual review. `sep-ai-modernizer` is not part of the active
workflow.

Consequences:

New `.sep` icons are produced and reviewed one at a time inside Claude Code
sessions rather than through a queued service; the icon and style-guide
reference table in
`docs/07_legacy_migration/SCADA_2026_ICON_STYLE_GUIDE_V2.md` section 4
grows only as icons are actually approved. Reviving a service-based
approach later requires the style guide and junction-point contract to
already be validated manually across multiple icons.

Regression coverage:

`tools/icon_modernization/tests` (junction point extraction and comparison),
manual visual review recorded in
`docs/07_legacy_migration/SCADA_2026_ICON_STYLE_GUIDE_V2.md` section 4.

### DEC-0034 - Component Provenance Field (Legacy Vs AI-Modernized)

Status: Active
Created: 2026-07-05 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/05_studio_element_plus/STUDIO_ELEMENT_PLUS_SEP_CONTRACT_V2.md`

Context:

Two `.sep` components (`Condenseur.sep`, `Triangle.sep`, `VentilateurPale.sep`)
were modernized and, in one case, replaced in place with no structured way
to tell from the file itself whether its current artwork is the untouched
legacy import or a redraw produced by the interactive modernization loop.
The only prior safety net was a `.sep.bak` sibling file (DEC-0033 workflow
step 1a) and free-text `SourceTrace.Notes`, neither of which is a queryable
property a reader or the editor UI can check.

Decision:

`ElementStudioComponent` gains an optional field, `Provenance`
(`ElementStudioComponentProvenance?`: `Legacy` or `AiModernized`), set by
`ElementStudioComponentPackageFactory.Create`/`CreateSvg` and persisted like
any other component field (PascalCase JSON, `JsonStringEnumConverter`). It
is additive and optional: `.sep` files written before this field existed
deserialize unchanged with `Provenance == null` ("not recorded", not
implicitly "Legacy"). The element library UI in both `ScadaBuilderV2.App`
and `ScadaBuilderV2.ElementStudio.App` shows a small "IA" badge on library
tiles whose `Provenance` is `AiModernized`. This field does not replace the
`.sep.bak` safety-copy step or `SourceTrace` - it is a structured,
queryable complement to both.

Consequences:

Every `.sep` produced or touched by the interactive modernization loop must
set `Provenance: AiModernized` as its last authoring step. Existing
modernized components (`Ventilateur.sep`, `Condenseur.sep`, `Triangle.sep`,
`VentilateurPale.sep`) were retroactively tagged; their `.sep.bak` backups
were tagged `Legacy` for the same reason.

Regression coverage:

`tests/ScadaBuilderV2.Tests/StudioElementPlusContractTests.cs`
(`ProvenanceRoundTripsThroughSepWriteAndRead`,
`ScadaBuilderLibraryReaderLoadsSepComponentsFromProjectLibrary`).

### DEC-0035 - Re-Edit Existing Element+ Library Components From The Scene

Status: Active
Created: 2026-07-06 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md`

Context:

Once a legacy element is converted to Element+, right-clicking it exposed no way back into Studio Element+ to edit its source `.sep` component; only never-converted legacy elements could open Studio Element+, and only to create a brand-new component. Users who convert to Element+ (required for grouping/resizing) lost their only path to editing that component's appearance. Separately, `.sep` components created from a library import always kept the placeholder name "Nouveau composant" because nothing seeded `ElementStudioWorkspaceViewModel.ComponentName` from the captured source element's name.

Decision:

A converted Element+ object created from a library component (`ScadaElementData.TagBinding` holds the source `.sep` filename) exposes `object.open-in-element-studio` in its context menu. Activating it reads the `.sep` via `ElementStudioComponentPackageStore`, maps it back into an `ElementStudioImportPackage` via `ElementStudioComponentToImportPackageMapper.ToEditablePackage` (flattening `Group` parts into their children), writes it through the existing `.ft1` import pipeline with `TargetLibraryPath` set to the original `.sep`'s directory, and launches Studio Element+ against it — so Save re-targets the same library folder. Separately, `ElementStudioWorkspaceViewModel` now seeds `ComponentName` via `ElementStudioComponentNaming.ResolveDefaultComponentName`, defaulting to the first imported source element's name instead of the placeholder.

Consequences:

Editing a re-opened component does not automatically overwrite the exact original `.sep` file (the Save dialog defaults to the same folder and a filename derived from `ComponentName`, but the user must confirm the save); nested groups deeper than one level are flattened on re-edit and must be re-grouped manually in Studio Element+.

Regression coverage:

`tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`, `tests/ScadaBuilderV2.Tests/ElementStudioComponentToImportPackageMapperTests.cs`, `tests/ScadaBuilderV2.Tests/ElementStudioComponentNamingTests.cs`

### DEC-0036 - Canonical Tag Ids In State Expression Runtime ASTs

Status: Active
Created: 2026-07-09 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/superpowers/specs/2026-07-09-expression-tag-id-reference.md`

Context:

Element+ state expressions were authored with human display labels such as
`PE_16` or legacy TF100Web names. The TF100Web runtime bridge resolves values
through canonical mapping identifiers in the `tf100.mapping.<id>` format.
Exporting display labels as runtime `tagName` values caused valid state rules
to degrade to `qualityFallback` even when the project tag catalog contained the
correct mapping.

Decision:

`ScadaExprTagRef` separates `TagName` for UI display/re-editing from optional
`TagId` for canonical identity. State-expression export must normalize resolved
references so the runtime AST contains `tagName = tf100.mapping.<id>` in both
HTML state config attributes and FT100 manifests. Unresolved references remain
unchanged and emit export warnings so TF100Web degrades deterministically.
Ambiguous references block export.

Consequences:

The editor can keep showing human labels while exported runtime payloads target
stable TF100Web mappings. Existing scenes without `TagId` are normalized at
export when the project catalog resolves the label uniquely. Export warnings
are surfaced on scene and project export results and deduplicated across HTML
and manifest generation.

Regression coverage:

`tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExprNodeTests.cs`,
`tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionTests.cs`,
`tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionValidatorTests.cs`,
`tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementCommandConfigTests.cs`,
`tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`,
`tests/runtime-js/expression-evaluator.test.mjs`,
`tests/runtime-js/state-engine.test.mjs`
### DEC-0037 - Model-Backed Element+ Advanced Style Contract

Status: Active
Created: 2026-07-13 00:00 America/Toronto
Created in commit: `b954d46`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md`

Context:

The Element+ Style tab could not author common typography, complete CSS border styles, foreground text color, or border radius without raw `AdvancedCss`. The modal and docked property surfaces also lacked a shared structured contract, creating preview/export drift risk.

Decision:

Element+ styles persist model-backed typography fields, `Foreground`, the nine CSS border styles, and a four-corner pixel `BorderRadius` record with backward-compatible defaults. Both WPF property surfaces use the same controls and mutation path. WebView preview and FT100 export emit the same structured CSS mapping, with `AdvancedCss` applied afterward except for export invariants. The current TF100Web composition path treats the resulting HTML/CSS as opaque and requires no semantic style parser.

Consequences:

Older projects remain readable without migration. The WPF inspector provides a local temporary preview and semantic `Icon.Property.*` resources. Integration coverage must verify that TF100Web deployment/composition preserves the generated HTML/CSS; changes to TF100Web runtime parsing require a separate contract decision.

Regression coverage:

`tests/ScadaBuilderV2.Tests/ScadaSceneModelsTests.cs`, `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`, `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`, and `F:\Projet\Git\TF100Web\frontend\tests_scada_deploy.py`.

### DEC-0038 - Modern Page Identity And Lifecycle Commands

Status: Active
Created: 2026-07-14 00:00 America/Toronto
Created in commit: `c5d6f0e`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/superpowers/specs/2026-07-14-page-commands-design.md`

Context:

SCADA Builder V2 exposes page properties and tabs, but page lifecycle operations are not available consistently from the ribbon, Project panel, and contextual menus. Page identity is also coupled to the human/export id, imported Wonderware inventory still acts as an implicit workspace identity, page history is scoped to open scene tabs, and project/scene persistence is not committed as one coherent snapshot.

Decision:

Every modern page has an immutable internal GUID `PageKey`, a visible mutable `PageCode`, a visible title, and optional import provenance. Internal home/composition/action/command references use `PageKey`; the `.sb2` adapter resolves keys to `PageCode` and preserves all existing human manifest fields, page folders, DOM roots, and runtime `TargetPageId` values. The shared asynchronous `page.*` application commands own creation, rename, duplication, deletion, properties, dependency validation, project-scoped history, and atomic workspace persistence. WPF surfaces only adapt these commands. Native pages do not require imported HTML, while duplication of an imported Wonderware page preserves its projection and provenance automatically.

Consequences:

Existing projects require an idempotent migration from ids to keys while old id fields remain readable during transition. A new `Default` page is excluded from build by default. Deletion is blocked until dependencies are resolved manually. Error dialogs and the bottom Diagnostics panel consume the same structured issue collection. `MainWindow` must no longer own page mutations, persistence rules, dependency analysis, or imported-source resolution. Roles/permissions, page folders, drag-and-drop organization, full template libraries, and creative table tools remain separate slices.

Implementation status:

Implemented in commits `40c77a3` through `3493055`. Automated lifecycle and `.sb2` compatibility validation are complete. Manual UI verification on an isolated project copy and migration of `projects/AMR_REF_SCADA_V2` remain explicit gates.

Regression coverage:

`tests/ScadaBuilderV2.Tests/PageIdentityTests.cs`, `tests/ScadaBuilderV2.Tests/PageCommandCoordinatorTests.cs`, `tests/ScadaBuilderV2.Tests/ProjectWorkspaceHistoryTests.cs`, `tests/ScadaBuilderV2.Tests/ModernProjectAtomicSnapshotTests.cs`, `tests/ScadaBuilderV2.Tests/NativePageDocumentTests.cs`, `tests/ScadaBuilderV2.Tests/PageManagementSurfaceContractTests.cs`, `tests/ScadaBuilderV2.Tests/DiagnosticsSurfaceContractTests.cs`, `tests/ScadaBuilderV2.Tests/PageLifecycleIntegrationTests.cs`, and `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`.

### DEC-0039 - Modern Table Element And Hierarchical Insert Ribbon

Status: Active
Created: 2026-07-14 00:00 America/Toronto
Created in commit: `95a57ac`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/superpowers/specs/2026-07-14-modern-table-and-insert-ribbon-design.md`

Context:

The `win00012` reference scene reconstructs tabular information with hundreds of independent legacy text, rectangle, line, and button objects. The current Insert ribbon is flat, grows for every tool, and routes many insertion ids through `MainWindow`. SCADA Builder V2 has no model-backed table capable of track resizing, merged cells, contextual row/column commands, coherent style inheritance, persistence, or direct `.sb2` export.

Decision:

SCADA Builder V2 introduces one Element+ `Table` object backed by a nullable, backward-compatible domain definition. It supports 1 to 64 rows and columns, manual and proportional track resize, rectangular selection, merge/unmerge, native text and numeric inputs without per-cell `ValueBindings`, table/column/row/cell formatting, internal and TSV clipboard exchange, and contextual insert/delete/clear/format/size commands. Effective formatting is resolved property by property in the order `Cell > explicit Row > automatic Row Band > Column > Table > system default`, where `null` means inherit.

The right `Propriete` panel, a dedicated `TablePropertiesDialog`, `CellFormatDialog`, dimension dialog, WebView table editor, and contextual menu route typed requests through shared Application commands and Domain operations. Editor gutters, selections, handles, and previews are never exported. The live WebView canvas and FT100 renderer consume the same table model; `.sb2` keeps its existing page-root, folder, namespace, and manifest contract.

The Insert ribbon becomes hierarchical: level 1 selects a semantic family and level 2 exposes its tools. Implemented tools keep stable ids; future modern SCADA tools may remain visible only with explicit disabled reasons. Catalog, generic insertion descriptors, table behavior, validation, clipboard rules, and detailed coordination are extracted from `MainWindow`; only high-level shell/workspace adaptation may remain there.

Consequences:

Existing projects remain readable with `Table = null` and are not converted automatically. `win00012` remains evidence and a visual/capacity reference, not an automatic migration target. Table inputs are standard HTML controls whose runtime values are local and not persisted after page reload. Implementation must add Domain, Application, Rendering, WebView, WPF, persistence, history, architecture, and `.sb2` regression coverage before the feature is documented as implemented.

Regression coverage:

Implemented in `tests/ScadaBuilderV2.Tests/ScadaTableModelTests.cs`, `ScadaTableOperationsTests.cs`, `TableEditCoordinatorTests.cs`, `TableClipboardTests.cs`, `RibbonCommandCatalogTests.cs`, `TableUiArchitectureTests.cs`, `ModernProjectStoreTests.cs`, `WebViewContextMenuScriptTests.cs`, and `Ft100SceneExporterTests.cs`.

### DEC-0040 - Advanced Table Authoring And Persistent Element Position Lock

Status: Active
Created: 2026-07-15 00:00 America/Toronto
Created in commit: `3f6e6a5`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/superpowers/specs/2026-07-15-table-ui-authoring-and-element-lock-design.md`

Context:

The `DEC-0039` table core persists and exports modern tables, but its current insertion flow opens a creation dialog and its editor does not yet expose the complete cell types, formatting scopes, heterogeneous borders, precise dimensions, headers, or reliable object-versus-cell interaction required to reconstruct `win00012` efficiently. The visible scene lock controls currently protect only transient selection state and do not prevent Element+ movement.

Decision:

`insert.table` opens a secondary Table authoring surface inside the existing Insert ribbon. The always-available `table.add` command configures and arms point placement without a modal creation dialog. Object and Cells modes own mutually exclusive gestures. Table authoring adds explicit direct and calculated scopes, deterministic cell-content conversion, inheritable full formatting, physical border-segment overrides, precise track operations, WebView-measured auto-fit, multiple consecutive header rows, typed bridge messages, and a validated 64 x 64 performance gate.

Every scene `ScadaElement` receives persistent `IsLocked` position metadata. `object.lock` becomes the sole main-editor lock command and replaces `selection.toggle-lock` without an alias. Group toggles recurse through descendants; a locked descendant blocks a group translation. Mixed selection appears unlocked in toggle surfaces and indeterminate in the Properties checkbox; activating it locks the complete selection closure. The Selection ribbon, right Properties panel, and top `Lock` indicator share one application-derived state. WebView feedback and an Application transform guard both reject effective X/Y changes while selection, resize without translation, rotation, content, style, events, and internal Table editing remain available.

Domain owns persistent values and pure operations, Application owns sessions, commands, guards, mutations and history, App owns WPF view models and WebView adapters, and Rendering consumes only the project model. `MainWindow` remains a high-level host. Scene locks, cell overlays, headers used only for authoring, handles and diagnostics never become `.sb2` or `.sep` runtime geometry. Per-cell `ValueBindings`, formulas, CSV/Excel import, automatic `win00012` conversion and tables larger than 64 x 64 remain out of scope.

Consequences:

Existing scene JSON without `IsLocked` remains readable as unlocked. Copy, cut/paste, duplicate, group/ungroup and undo/redo must preserve the approved lock semantics. The current `DialogThenPoint`, disabled `object.lock`, `ToggleSelectionLockCommand`, `SelectionState.IsSelectionLocked`, local `MainWindow.IsSelectionLocked` binding and direct Table bridge logic are explicit migration targets. The feature cannot be documented as implemented until persistence, history, WPF/WebView interaction, preview/export parity, performance, architecture boundaries and isolated interactive verification are covered.

Regression coverage:

Planned in `tests/ScadaBuilderV2.Tests/ScadaTableModelTests.cs`, `TableContentOperationsTests.cs`, `TableBorderOperationsTests.cs`, `TableTrackOperationsTests.cs`, `TableAuthoringSessionTests.cs`, `TableEditCoordinatorTests.cs`, `TableWebViewMessageAdapterTests.cs`, `TableUiArchitectureTests.cs`, `ElementLockCoordinatorTests.cs`, `ElementTransformGuardTests.cs`, `ScadaSceneGroupTests.cs`, `ModernProjectStoreTests.cs`, `ApplicationCommandTests.cs`, `WebViewContextMenuScriptTests.cs`, `Ft100SceneExporterTests.cs`, and `StudioElementPlusContractTests.cs`.
