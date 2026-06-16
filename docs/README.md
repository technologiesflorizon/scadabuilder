# SCADA Builder V2 - Documentation Index

Date: 2026-06-15
Status: Active documentation map
Document version: `V2.1.1.0037`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
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
6. Header and footer pages are composed as complete exported page fragments, not copied as loose child HTML.
7. TF100Web must size header/body/footer slots from manifest page dimensions and apply viewport scale to the composed page container once.
8. Page HTML carries critical inline geometry for the page root, HTML source-layer objects, and Element+ objects as a deployment guardrail; SVG source shapes keep SVG geometry attributes and the page CSS remains the complete runtime stylesheet that must still be loaded from the same package version.
9. Selection is polymorphic: any present non-editor source or Element+ object is selectable, and commands resolve the selected runtime type after hit-testing.
10. Persistent deletion uses the global scene history and `RemovedSourceElementIds`. WebView masking or `display:none` is not a durable delete state, and inventory differences must not auto-hide source nodes.
11. Exported page HTML, CSS, and runtime JavaScript must use a page namespace rooted at the exported page id, such as `#ft100-win00003`. Generated selectors for source `data-id`, Element+ object ids, FT100 layer classes, and runtime action targets must be page-scoped or page-prefixed; unscoped `:root`, `html/body`, `[data-id="..."]`, `.ft100-*`, or `#Button1`-style selectors are invalid for current TF100Web composition. Legacy `data-id` values and scene element ids remain page-local metadata, not package-global selector identities.

## 2.1 Header/Footer Composition Contract

Approved policy:

1. The root package manifest owns page inventory and composition references.
2. A default page renders as ordered slots: referenced header page, body page, referenced footer page.
3. Each slot injects the complete exported page root, such as `#ft100-win00002`, and keeps its page-local CSS active.
4. Slot width and height come from the referenced page `Width` and `Height` values, with `data-scada-width` and `data-scada-height` as HTML-side diagnostics.
5. TF100Web must not flatten header/footer children into the body page or infer positions after extraction.
6. Manual deployment must keep HTML, CSS, images, and manifests from the same package export to avoid mixed-version rendering.
7. HTML source-layer elements with persisted SCADA Builder bounds must carry those bounds directly in their exported inline `style` attributes, after any original legacy style declarations.
8. All imported source elements with `data-id`, including footer SVG shapes and `win00008` non-`.layer` nodes, remain selectable/editable in SCADA Builder V2; the C# materialization inventory remains limited to managed source projections and must not hide present source nodes; export must not inject HTML absolute-position declarations into SVG child tags.

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
2. `FT100_INTEGRATION_STRATEGY_V2.md` - SCADA Builder V2 to TF100Web package contract, integration plan, and TF100Web tag catalog extraction/import roadmap.
3. `PROJECT_MODEL_XAML.md` - intended source project model and source-of-truth boundaries.
4. `VERSIONING_POLICY_V2.md` - product/document versioning policy.

Architecture and editor behavior:

1. `ARCHITECTURE_V2.md` - layered architecture boundaries.
2. `COMMANDS_AND_STATE.md` - command/state/undo contracts.
3. `ELEMENT_OBJECT_MODEL_V2.md` - SCADA element object model.
4. `RESPONSIVE_MODEL_V2.md` - responsive layout model.
5. `UI_DIRECTION_V2.md` and `UI_SPEC_V2.md` - UI direction, implementation spec, and shared CSS property roadmap.
6. `ICON_STRATEGY_V2.md` - icon sourcing, licensing, and command icon map.

Legacy and reference material:

1. `LEGACY_MODERNIZATION_WORKFLOW_V2.md` - legacy-to-V2 modernization workflow.
2. `REFERENCE_PROJECT_MODEL_NOTES.md` - notes on the legacy reference project structure.
3. `REFERENCE_PROJECT_V2.md` - AMR reference project pointer.

Implementation plans and status:

1. `ACTION_COMMAND_ARCHITECTURE_PLAN_V2.md` - command architecture implementation history.
2. `PAGE_MANIFEST_OBJECT_ACTIONS_PLAN_V2.md` - page manifest, object-owned action slices, tag-conditioned event roadmap, visual effects, and global script/page-event roadmap.
3. `STUDIO_ELEMENT_PLUS_PLAN_V2.md` - Studio Element+ plan, modernization roadmap, shared CSS properties, and component visual effects.
4. `STUDIO_ELEMENT_PLUS_SELECTION_DECISIONS_V2.md` - canonical Studio Element+ selection decisions.
5. `TF100WEB_IMPLEMENTATION_NOTE_V2.md` - TF100Web intake implementation notes.
6. `MULTI_AGENT_OPERATING_MODEL_V2.md` - multi-agent operating model.

Assets:

1. `wireframes/wireframe_Scada_Builder_V2.png` - UI wireframe image referenced by UI documents.

Near-term development roadmap ownership:

1. First event-authoring slice: `PAGE_MANIFEST_OBJECT_ACTIONS_PLAN_V2.md` owns `On click -> change page`.
2. TF100Web tag extraction and SCADA Builder V2 tag file import: `FT100_INTEGRATION_STRATEGY_V2.md`.
3. Tag boolean conditions (`If tag is true`, `If tag is false`, `If tag is degraded`): `PAGE_MANIFEST_OBJECT_ACTIONS_PLAN_V2.md`, dependent on the imported tag catalog.
4. Studio Element+ modernization of images, shapes, forms, and composed elements: `STUDIO_ELEMENT_PLUS_PLAN_V2.md`.
5. CSS property set improvement in SCADA Builder V2 and Studio Element+: `UI_SPEC_V2.md` and `STUDIO_ELEMENT_PLUS_PLAN_V2.md`.
6. Event visual effects (`blink`, `glow`, etc.) and global scripts that generate page lifecycle events: `PAGE_MANIFEST_OBJECT_ACTIONS_PLAN_V2.md`.

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
