# SCADA Builder V2 - Decision Register

Date: 2026-06-16
Status: Active authoritative decision register
Document version: `V2.1.2.0002`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
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
