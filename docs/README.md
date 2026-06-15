# SCADA Builder V2 - Documentation Index

Date: 2026-06-15
Status: Active documentation map
Document version: `V2.1.1.0030`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Creation de l'arbre documentaire stable, des regles de header et des decisions de deprecation `index.html`. |

## 1. Role

This file is the required entry point for SCADA Builder V2 documentation.

Use it to decide where a change belongs before editing or creating any other document.

## 2. Current Runtime Contract

Current FT100/TF100Web exports use the normalized package contract:

```text
scada-builder-v2-ft100-package/
  manifest.json
  README.txt
  <page-id>/
    <page-id>.html
    css/
      <page-id>.css
    images/
    manifest.json
    README.txt
```

Rules:

1. `index.html` is deprecated for current SCADA Builder V2 FT100/TF100Web exports.
2. New documentation must not describe `index.html` as the active export target.
3. TF100Web may keep isolated legacy fallback support for old imported packages only.
4. Root `manifest.json` is the authoritative intake contract.
5. Preview, build, and export must converge on the same V2 project model and must not depend on editor-only overlays or tooling.

## 3. Legacy Source Contract

Legacy artifacts are evidence and migration inputs, not the final V2 project model.

Rules:

1. `03_web_legacy/html_pages/*` is the preferred unworked HTML rendering when raw visual comparison is required.
2. `08_web_modernized/*` is comparison/history material by default.
3. `08_web_modernized/*` must not be used as raw runtime source without an explicit sanitized-source decision.
4. Helper scripts, layout tools, test panels, diagnostics, and repaired/manual positions must be stripped or rejected before runtime/export use.
5. `win00009` is currently the reference page that displays correctly in SCADA Builder V2; `win00008` is a known regression candidate with source/position/tooling divergence.

## 4. Document Tree

Core contracts:

1. `PREVIEW_BUILD_CONTRACT.md` - preview/build/export parity and runtime output structure.
2. `FT100_INTEGRATION_STRATEGY_V2.md` - SCADA Builder V2 to TF100Web package contract and integration plan.
3. `PROJECT_MODEL_XAML.md` - intended source project model and source-of-truth boundaries.
4. `VERSIONING_POLICY_V2.md` - product/document versioning policy.

Architecture and editor behavior:

1. `ARCHITECTURE_V2.md` - layered architecture boundaries.
2. `COMMANDS_AND_STATE.md` - command/state/undo contracts.
3. `ELEMENT_OBJECT_MODEL_V2.md` - SCADA element object model.
4. `RESPONSIVE_MODEL_V2.md` - responsive layout model.
5. `UI_DIRECTION_V2.md` and `UI_SPEC_V2.md` - UI direction and implementation spec.
6. `ICON_STRATEGY_V2.md` - icon sourcing, licensing, and command icon map.

Legacy and reference material:

1. `LEGACY_MODERNIZATION_WORKFLOW_V2.md` - legacy-to-V2 modernization workflow.
2. `REFERENCE_PROJECT_MODEL_NOTES.md` - notes on the legacy reference project structure.
3. `REFERENCE_PROJECT_V2.md` - AMR reference project pointer.

Implementation plans and status:

1. `ACTION_COMMAND_ARCHITECTURE_PLAN_V2.md` - command architecture implementation history.
2. `PAGE_MANIFEST_OBJECT_ACTIONS_PLAN_V2.md` - page manifest and object-owned action slices.
3. `STUDIO_ELEMENT_PLUS_PLAN_V2.md` - Studio Element+ plan.
4. `STUDIO_ELEMENT_PLUS_SELECTION_DECISIONS_V2.md` - canonical Studio Element+ selection decisions.
5. `TF100WEB_IMPLEMENTATION_NOTE_V2.md` - TF100Web intake implementation notes.
6. `MULTI_AGENT_OPERATING_MODEL_V2.md` - multi-agent operating model.

Assets:

1. `wireframes/wireframe_Scada_Builder_V2.png` - UI wireframe image referenced by UI documents.

## 5. Mandatory Header Rule

Every Markdown document in `docs/` must include:

1. H1 title.
2. `Date`.
3. `Status`.
4. `Document version`.
5. `## Historique des changements`.
6. A change table with `Date`, `Version`, `Commit`, and `Changement`.

Use `PENDING` only for uncommitted documentation changes. Replace it with a commit reference in a follow-up documentation bookkeeping change or release note after the delivery commit exists.

## 6. Update Rules

1. Update this index when adding, removing, renaming, or changing ownership of a document.
2. Do not create a new planning document if an existing owner document can be updated.
3. Keep future plans separate from implemented behavior.
4. State implementation gaps explicitly instead of documenting intended behavior as if it exists.
5. Run a targeted documentation consistency check before closing documentation work.
