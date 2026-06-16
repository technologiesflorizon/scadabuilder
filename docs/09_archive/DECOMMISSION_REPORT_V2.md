# SCADA Builder V2 - Documentation Decommission Report

Date: 2026-06-16
Status: Active archive and migration trace
Document version: `V2.1.1.0039`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du rapport de decommission des anciens fichiers racine apres migration vers la nouvelle architecture documentaire. |

## 1. Rule

The files listed here are decommissioned as active documentation. Their original content is preserved under `docs/09_archive/deprecated/` for audit and historical traceability.

Active documentation now lives in the owner documents listed in the migration table.

## 2. Migration Table

| Legacy file | Disposition | Active owner documents |
| --- | --- | --- |
| `ACTION_COMMAND_ARCHITECTURE_PLAN_V2.md` | Archived as implementation history | `04_editor/COMMANDS_CONTRACT_V2.md`, `04_editor/STATE_MANAGEMENT_CONTRACT_V2.md`, `08_implementation_status/IMPLEMENTED_FEATURES_V2.md`, `08_implementation_status/REGRESSION_COVERAGE_V2.md` |
| `ARCHITECTURE_V2.md` | Archived as architecture source | `02_architecture/GLOBAL_ARCHITECTURE_V2.md`, `02_architecture/MODULE_BOUNDARIES_V2.md`, `02_architecture/APPLICATION_FLOW_V2.md` |
| `COMMANDS_AND_STATE.md` | Archived as command/state source | `04_editor/COMMANDS_CONTRACT_V2.md`, `04_editor/STATE_MANAGEMENT_CONTRACT_V2.md`, `04_editor/SELECTION_CONTRACT_V2.md` |
| `ELEMENT_OBJECT_MODEL_V2.md` | Archived as model source | `02_architecture/DATA_MODEL_OVERVIEW_V2.md`, `03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md`, `05_studio_element_plus/STUDIO_ELEMENT_PLUS_SEP_CONTRACT_V2.md` |
| `FT100_INTEGRATION_STRATEGY_V2.md` | Archived as runtime integration source | `03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`, `07_legacy_migration/LEGACY_SOURCE_POLICY_V2.md`, `08_implementation_status/KNOWN_GAPS_V2.md` |
| `ICON_STRATEGY_V2.md` | Archived as icon analysis source | `06_ui_ux/ICON_STRATEGY_V2.md` |
| `LEGACY_MODERNIZATION_WORKFLOW_V2.md` | Archived as modernization source | `07_legacy_migration/MODERNIZATION_WORKFLOW_V2.md`, `07_legacy_migration/LEGACY_SOURCE_POLICY_V2.md` |
| `MULTI_AGENT_OPERATING_MODEL_V2.md` | Archived as operating-model source | `00_governance/TEAM_WORKFLOW_V2.md`, `docs/AGENTS.md` |
| `PAGE_MANIFEST_OBJECT_ACTIONS_PLAN_V2.md` | Archived as actions/events source | `04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`, `08_implementation_status/IMPLEMENTED_FEATURES_V2.md`, `08_implementation_status/KNOWN_GAPS_V2.md` |
| `PREVIEW_BUILD_CONTRACT.md` | Archived as runtime source | `03_runtime_contracts/PREVIEW_BUILD_EXPORT_CONTRACT_V2.md`, `03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md` |
| `PROJECT_MODEL_XAML.md` | Archived as project model source | `03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md`, `02_architecture/DATA_MODEL_OVERVIEW_V2.md` |
| `REFERENCE_PROJECT_MODEL_NOTES.md` | Archived as reference source | `07_legacy_migration/REFERENCE_PROJECT_NOTES_V2.md`, `07_legacy_migration/LEGACY_SOURCE_POLICY_V2.md` |
| `REFERENCE_PROJECT_V2.md` | Archived as reference pointer | `07_legacy_migration/REFERENCE_PROJECT_NOTES_V2.md` |
| `RESPONSIVE_MODEL_V2.md` | Archived as responsive source | `06_ui_ux/RESPONSIVE_MODEL_V2.md` |
| `STUDIO_ELEMENT_PLUS_PLAN_V2.md` | Archived as Studio plan source | `05_studio_element_plus/STUDIO_ELEMENT_PLUS_ARCHITECTURE_V2.md`, `05_studio_element_plus/STUDIO_ELEMENT_PLUS_SEP_CONTRACT_V2.md`, `08_implementation_status/IMPLEMENTED_FEATURES_V2.md` |
| `STUDIO_ELEMENT_PLUS_SELECTION_DECISIONS_V2.md` | Archived as Studio selection source | `05_studio_element_plus/STUDIO_ELEMENT_PLUS_SELECTION_CONTRACT_V2.md`, `00_governance/DECISION_REGISTER_V2.md` |
| `TF100WEB_IMPLEMENTATION_NOTE_V2.md` | Archived as implementation note source | `03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`, `08_implementation_status/IMPLEMENTED_FEATURES_V2.md`, `08_implementation_status/KNOWN_GAPS_V2.md` |
| `UI_DIRECTION_V2.md` | Archived as UI direction source | `06_ui_ux/UI_ARCHITECTURE_V2.md`, `06_ui_ux/UI_SPECIFICATION_V2.md` |
| `UI_SPEC_V2.md` | Archived as UI spec source | `06_ui_ux/UI_SPECIFICATION_V2.md`, `04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md`, `04_editor/PROPERTIES_PANEL_CONTRACT_V2.md` |
| `VERSIONING_POLICY_V2.md` | Archived as versioning source | `00_governance/VERSIONING_AND_CHANGELOG_POLICY_V2.md`, `03_runtime_contracts/VERSIONING_CONTRACT_V2.md` |

## 3. Validation Rule

No active document may point to a decommissioned root file as the current source of truth. If archived material is referenced, the reference must use `docs/09_archive/deprecated/<file>.md` and must be described as historical.

## 4. Line Coverage

The following archived files were counted after decommission. This table is the audit trace that every legacy root file was included in the migration pass.

| Archived file | Lines preserved |
| --- | ---: |
| `ACTION_COMMAND_ARCHITECTURE_PLAN_V2.md` | 1492 |
| `ARCHITECTURE_V2.md` | 309 |
| `COMMANDS_AND_STATE.md` | 440 |
| `ELEMENT_OBJECT_MODEL_V2.md` | 250 |
| `FT100_INTEGRATION_STRATEGY_V2.md` | 286 |
| `ICON_STRATEGY_V2.md` | 413 |
| `LEGACY_MODERNIZATION_WORKFLOW_V2.md` | 223 |
| `MULTI_AGENT_OPERATING_MODEL_V2.md` | 291 |
| `PAGE_MANIFEST_OBJECT_ACTIONS_PLAN_V2.md` | 621 |
| `PREVIEW_BUILD_CONTRACT.md` | 454 |
| `PROJECT_MODEL_XAML.md` | 543 |
| `REFERENCE_PROJECT_MODEL_NOTES.md` | 351 |
| `REFERENCE_PROJECT_V2.md` | 69 |
| `RESPONSIVE_MODEL_V2.md` | 376 |
| `STUDIO_ELEMENT_PLUS_PLAN_V2.md` | 416 |
| `STUDIO_ELEMENT_PLUS_SELECTION_DECISIONS_V2.md` | 160 |
| `TF100WEB_IMPLEMENTATION_NOTE_V2.md` | 161 |
| `UI_DIRECTION_V2.md` | 609 |
| `UI_SPEC_V2.md` | 662 |
| `VERSIONING_POLICY_V2.md` | 118 |
