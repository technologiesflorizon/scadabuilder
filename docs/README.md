# SCADA Builder V2 - Documentation Index

Date: 2026-06-17
Status: Active enterprise documentation map
Document version: `V2.1.2.0018`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-17 | `V2.1.2.0018` | `ad364a6` | Ajout du contrat d'intake FT100 audite dans TF100Web et de la reference source locale. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Ajout des effets visuels runtime standards. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Ajout du bridge lifecycle runtime global. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Ajout des groupes de conditions runtime et politique degradee explicite. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Ajout des options runtime avancees pour popup Fragment. |
| 2026-06-17 | `V2.1.2.0016` | `PENDING` | Ajout des actions runtime de bordure Element+ ciblee. |
| 2026-06-17 | `V2.1.2.0015` | `PENDING` | Ajout des actions runtime `Fermer popup` et `Basculer popup`. |
| 2026-06-17 | `V2.1.2.0014` | `PENDING` | Ajout de l'action runtime `Ouvrir popup` pour fragments compiles. |
| 2026-06-17 | `V2.1.2.0013` | `PENDING` | Ajout des filtres et du resume de catalogue tags dans l'editeur. |
| 2026-06-17 | `V2.1.2.0012` | `PENDING` | Ajout de l'application runtime des valeurs de tags lues aux Element+ lies. |
| 2026-06-17 | `V2.1.2.0010` | `PENDING` | Ajout des actions objet conditionnelles `Afficher`, `Masquer` et `Basculer visibilite`. |
| 2026-06-17 | `V2.1.2.0009` | `PENDING` | Remplacement de l'authoring `WriteTag` par les bindings Element+ `Lire valeur` et `Ecrire valeur`. |
| 2026-06-17 | `V2.1.2.0008` | `PENDING` | Ajout du catalogue tags TF100Web importe au projet et de l'authoring `WriteTag` Element+. |
| 2026-06-16 | `V2.1.2.0007` | `PENDING` | Ajout du curseur runtime par defaut pour boutons et cibles cliquables FT100/TF100Web. |
| 2026-06-16 | `V2.1.2.0006` | `PENDING` | Correction de l'export FT100 des events `Clic -> Changer de page` portes par des groupes Element+. |
| 2026-06-16 | `V2.1.2.0005` | `PENDING` | Ajout des metadonnees hover automatique des boutons Element+, de la tab Bouton et du CSS hover FT100. |
| 2026-06-16 | `V2.1.2.0004` | `PENDING` | Ajout du registre Evenement Element+ et de la premiere tranche Clic -> Changer de page. |
| 2026-06-16 | `V2.1.2.0003` | `PENDING` | Correction du groupement Element+: preservation de l'ordre visuel, hierarchie Element et mouvement solidaire. |
| 2026-06-16 | `V2.1.2.0002` | `PENDING` | Ajout du contrat de groupement scene Element+ only et de l'avertissement conversion legacy. |
| 2026-06-16 | `V2.1.2.0001` | `PENDING` | Correction du raccourci clavier WebView: Backspace ne supprime plus un Element+ selectionne et les champs editables ne declenchent pas les raccourcis scene. |
| 2026-06-16 | `V2.1.2.0000` | `PENDING` | Bump feature pour la conversion dynamique Element+ des boutons legacy, le menu Propriete contextualise et le rendu/export du texte des boutons. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Refonte de l'architecture documentaire en modules, ajout du registre decisionnel, des regles AGENTS, des contrats separes, des diagrammes Mermaid et du workflow de verification documentaire. |
| 2026-06-15 | `V2.1.1.0038` | `841d05a` | Ajout de la roadmap `On click -> open popup` et hover border sur element/groupe. |
| 2026-06-15 | `V2.1.1.0037` | `90c108b` | Ajout de la roadmap de developpement: events, tags TF100Web, Studio Element+, proprietes CSS, effets visuels et scripts globaux. |
| 2026-06-15 | `V2.1.1.0036` | `63c2475` | Generalisation du contrat de namespace CSS/DOM par page pour interdire les collisions de selecteurs en composition TF100Web. |
| 2026-06-15 | `V2.1.1.0035` | `63c2475` | Clarification du scoping CSS par page pour eviter les collisions header/body/footer sur les `data-id`. |
| 2026-06-15 | `V2.1.1.0034` | `63c2475` | Documentation du contrat selection polymorphe et suppression globale source/objet sans masquage durable. |
| 2026-06-15 | `V2.1.1.0033` | `63c2475` | Clarification du contrat de selection source `data-id`, incluant SVG, et du garde-fou inline limite aux couches HTML legacy. |
| 2026-06-15 | `V2.1.1.0032` | `63c2475` | Extension du garde-fou de geometrie inline aux objets source legacy persistants. |
| 2026-06-15 | `V2.1.1.0031` | `63c2475` | Documentation du contrat de composition header/body/footer TF100Web et du garde-fou de geometrie inline FT100. |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Creation de l'arbre documentaire stable, des regles de header et des decisions de deprecation `index.html`. |

## 1. Role

This file is the required entry point for SCADA Builder V2 documentation.

Use it to locate the owner document before editing a contract, plan, status note, or decision. The documentation is now organized by ownership:

1. Governance and decisions.
2. Product objectives.
3. Software architecture.
4. Runtime contracts.
5. Editor contracts.
6. Studio Element+ contracts.
7. UI/UX contracts.
8. Legacy migration policy.
9. Implementation status.
10. Generated code documentation and diagrams.

## 2. Required Reading

Before changing documentation or code that affects documented behavior:

1. Read `docs/AGENTS.md`.
2. Read `docs/00_governance/DECISION_REGISTER_V2.md`.
3. Read the owner document listed below for the touched area.
4. If Studio Element+ selection, hit-testing, movement, grouping, properties, `.sep` export, or regression tests are touched, read `docs/05_studio_element_plus/STUDIO_ELEMENT_PLUS_SELECTION_CONTRACT_V2.md`.

## 3. Active Documentation Tree

Governance:

1. `00_governance/DOCUMENTATION_STANDARD_V2.md` - mandatory document structure, ownership, Mermaid, code-doc, and verification rules.
2. `00_governance/DECISION_REGISTER_V2.md` - authoritative decision registry; decisions are never deleted when superseded.
3. `00_governance/VERSIONING_AND_CHANGELOG_POLICY_V2.md` - version and history policy.
4. `00_governance/TEAM_WORKFLOW_V2.md` - team workflow for code, docs, decisions, tests, and reviews.
5. `00_governance/DOC_SYNC_SKILL_SPEC_V2.md` - required behavior for the `scada-v2-doc-sync` skill and verification script.

Product and architecture:

1. `01_product/APPLICATION_OBJECTIVES_V2.md` - product objectives and non-negotiable application goals.
2. `02_architecture/GLOBAL_ARCHITECTURE_V2.md` - global software architecture and module boundaries.
3. `02_architecture/APPLICATION_FLOW_V2.md` - end-to-end flow from input to preview, Studio Element+, export, and tests.
4. `02_architecture/MODULE_BOUNDARIES_V2.md` - ownership matrix for Domain, Application, Infrastructure, Rendering, App, and Studio.
5. `02_architecture/DATA_MODEL_OVERVIEW_V2.md` - high-level project, scene, element, event, and package model.

Runtime contracts:

1. `03_runtime_contracts/PREVIEW_BUILD_EXPORT_CONTRACT_V2.md` - preview/build/export parity.
2. `03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md` - normalized package contract.
3. `03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md` - project and scene model contract.
4. `03_runtime_contracts/VERSIONING_CONTRACT_V2.md` - runtime/product version contract.

Editor contracts:

1. `04_editor/COMMANDS_CONTRACT_V2.md` - command registry, ids, enablement, dispatch, and ownership.
2. `04_editor/STATE_MANAGEMENT_CONTRACT_V2.md` - project, scene, selection, dirty state, and undo/redo ownership.
3. `04_editor/ACTIONS_EVENTS_CONTRACT_V2.md` - runtime actions, object events, tags, popup, hover, and scripts.
4. `04_editor/SELECTION_CONTRACT_V2.md` - global SCADA Builder V2 source/object selection contract.
5. `04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md` - ribbon, context menu, panels, and command surfaces.
6. `04_editor/PROPERTIES_PANEL_CONTRACT_V2.md` - property inspector ownership and validation.

Studio Element+:

1. `05_studio_element_plus/STUDIO_ELEMENT_PLUS_ARCHITECTURE_V2.md` - SCADA Builder to Studio flow.
2. `05_studio_element_plus/STUDIO_ELEMENT_PLUS_SELECTION_CONTRACT_V2.md` - canonical Studio selection contract.
3. `05_studio_element_plus/STUDIO_ELEMENT_PLUS_SEP_CONTRACT_V2.md` - `.sep` package and export boundary.

UI/UX:

1. `06_ui_ux/UI_ARCHITECTURE_V2.md` - UI shell, surfaces, and interaction ownership.
2. `06_ui_ux/UI_SPECIFICATION_V2.md` - active UI specification.
3. `06_ui_ux/ICON_STRATEGY_V2.md` - icon strategy and licensing.
4. `06_ui_ux/RESPONSIVE_MODEL_V2.md` - responsive model.

Legacy migration:

1. `07_legacy_migration/LEGACY_SOURCE_POLICY_V2.md` - legacy source policy, sanitized-source decision, and `win00008`/`win00009` baseline.
2. `07_legacy_migration/REFERENCE_PROJECT_NOTES_V2.md` - reference project notes.
3. `07_legacy_migration/MODERNIZATION_WORKFLOW_V2.md` - modernization workflow.

Implementation status:

1. `08_implementation_status/IMPLEMENTED_FEATURES_V2.md` - current implemented features.
2. `08_implementation_status/REGRESSION_COVERAGE_V2.md` - regression coverage map.
3. `08_implementation_status/KNOWN_GAPS_V2.md` - gaps that must not be documented as implemented behavior.

Generated documentation:

1. `10_generated/CODE_MAP_V2.md` - generated or verified code map.
2. `10_generated/MODULE_FUNCTION_INDEX_V2.md` - generated public function index and doc coverage.
3. `10_generated/COMMAND_FLOW_DIAGRAM_V2.md` - command flow diagram.
4. `10_generated/STATE_FLOW_DIAGRAM_V2.md` - state flow diagram.
5. `10_generated/EXPORT_FLOW_DIAGRAM_V2.md` - export flow diagram.
6. `10_generated/STUDIO_ELEMENT_PLUS_FLOW_DIAGRAM_V2.md` - Studio Element+ flow diagram.

## 4. Current Contract Guardrails

These guardrails are active decisions in `00_governance/DECISION_REGISTER_V2.md`:

1. Current FT100/TF100Web exports use root `manifest.json` plus `<page-id>/<page-id>.html`; `index.html` is deprecated for current packages.
2. Preview, build, and export consume the same V2 project model.
3. Editor overlays, layout tools, diagnostics, selection handles, drag rectangles, and test panels must not become runtime/export geometry.
4. `08_web_modernized` is comparison/history material by default and is not raw source of truth without an explicit sanitized-source decision.
5. `win00009` is the known-good comparison baseline; `win00008` is a known divergence/regression candidate.
6. Selection is polymorphic: present source nodes and Element+ scene objects remain selectable according to their contract.
7. Durable source deletion uses scene state and `RemovedSourceElementIds`, not WebView masking or inventory omission.
8. Exported CSS, DOM ids, and runtime action targets are page-namespaced for TF100Web composition.
9. Scene grouping is Element+ only; legacy/source nodes must be converted to Element+ before they can be grouped.
10. Imported TF100Web tags are project-level catalog data; Element+ value bindings use all enabled tags for `Lire valeur`, require writeable tags for `Ecrire valeur`, and export through the FT100/TF100Web manifest/runtime bridge. The editor `Catalogue Tags` panel exposes search, device, datatype, access, and state filters plus a filtered summary.
11. Element+ object visibility actions may be conditioned by imported tag values with deterministic operators; boolean `Vrai/Faux` conditions require boolean tags.
12. Runtime TF100Web can push tag values into read-bound Element+ objects through `window.scadaBuilderSetTagValue(tagId, value, meta)` or the `scada-builder-tag-value` browser event.
13. Popup actions `Ouvrir popup`, `Fermer popup`, and `Basculer popup` target compiled `Fragment` pages only; build/export validation rejects missing, non-fragment, excluded popup targets, and missing host regions for host-region popups.
14. Runtime border actions `Afficher bordure`, `Masquer bordure`, and `Basculer bordure` target Element+ objects through the standard page-scoped `scada-runtime-border-highlight` CSS class.
15. Runtime action conditions support optional compound groups with `All` or `Any` mode and explicit missing-tag policy.
16. Exported pages expose `window.scadaBuilderRuntime` and lifecycle events for page ready, action executed, and runtime errors.
17. Standard runtime visual effects include blink, glow, pulse, alarm highlight, and degraded treatment through page-scoped CSS classes.
18. Current TF100Web intake source is `F:\Projet\Git\TF100Web` on branch `implementation_scada_builder`; as audited at commit `7d57600`, TF100Web extracts only `<div id="ft100-<page-id>">`, loads sibling CSS/assets, composes header/body/footer fragments, and executes host-side navigation plus mapping refresh/write behavior.
19. SCADA Builder exporter-emitted page scripts are not executed by the current TF100Web fragment intake. Documentation must separate exporter behavior from TF100Web-executed behavior until parity is implemented.

## 5. Decommissioned Legacy Documents

The original top-level Markdown files have been decommissioned as active documentation and moved to:

```text
docs/09_archive/deprecated/
```

They are historical/source material only. They must not receive new active contracts.

Examples:

1. `docs/09_archive/deprecated/ARCHITECTURE_V2.md` -> active content lives in `02_architecture/*`.
2. `docs/09_archive/deprecated/COMMANDS_AND_STATE.md` -> active content lives in `04_editor/COMMANDS_CONTRACT_V2.md` and `04_editor/STATE_MANAGEMENT_CONTRACT_V2.md`.
3. `docs/09_archive/deprecated/PAGE_MANIFEST_OBJECT_ACTIONS_PLAN_V2.md` -> active content lives in `04_editor/ACTIONS_EVENTS_CONTRACT_V2.md` and `08_implementation_status/*`.
4. `docs/09_archive/deprecated/STUDIO_ELEMENT_PLUS_SELECTION_DECISIONS_V2.md` -> active content lives in `05_studio_element_plus/STUDIO_ELEMENT_PLUS_SELECTION_CONTRACT_V2.md`.

The decommission map is `docs/09_archive/DECOMMISSION_REPORT_V2.md`.

## 6. Validation Commands

Run documentation validation after documentation changes:

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
rg -n "index\.html|08_web_modernized|source_html|Open[ ]Decisions|Document version|Historique des changements|PENDING" docs
```

Run tests when documentation claims implemented behavior changed:

```powershell
dotnet test ScadaBuilderV2.sln --no-restore
```
