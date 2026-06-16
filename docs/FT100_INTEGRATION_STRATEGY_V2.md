# SCADA Builder V2 - FT100 Integration Strategy

Date: 2026-06-15
Status: Draft
Document version: `V2.1.1.0037`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0037` | `90c108b` | Ajout de la roadmap d'extraction des tags cote TF100Web et d'import du fichier de tags dans SCADA Builder V2. |
| 2026-06-15 | `V2.1.1.0036` | `63c2475` | Contrat general de namespace CSS/DOM/runtime par page pour eviter toute collision de selecteurs TF100Web. |
| 2026-06-15 | `V2.1.1.0035` | `63c2475` | Clarification que les CSS `data-id` exportees sont scopees au root de page pour proteger la composition TF100Web. |
| 2026-06-15 | `V2.1.1.0034` | `63c2475` | Clarification que l'omission export source depend de l'etat scene `RemovedSourceElementIds`, pas du masquage WebView. |
| 2026-06-15 | `V2.1.1.0033` | `63c2475` | Clarification que la geometrie inline FT100 vise les couches HTML legacy et ne doit pas muter les formes SVG source. |
| 2026-06-15 | `V2.1.1.0032` | `63c2475` | Extension de la geometrie inline FT100 aux objets source legacy avec positions persistantes. |
| 2026-06-15 | `V2.1.1.0031` | `63c2475` | Decision de composition header/body/footer par manifeste et ajout de la geometrie inline critique dans les exports FT100. |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Deprecation explicite de `index.html`, clarification source legacy/modernized et ajout du risque `win00008` vs `win00009`. |
| 2026-06-15 | `V2.1.1.0029` | `2b59efb` | Baseline initiale du depot SCADA Builder V2. |

## 1. Context

The final output of SCADA Builder is intended to run on the FT100.

SCADA Builder V2 will not manage every FT100-to-SCADA relationship at first. Integration must be progressive, controlled, and documented.

## 2. V2.00 Scope

V2.00 focuses on:

1. Modern industrial editor foundation.
2. Project creation and configuration.
3. Scene editing.
4. Responsive layout modes.
5. Preview/build parity.
6. Initial model structure for future FT100 relationships.

V2.00 does not need to fully automate every FT100 mapping, action, or runtime binding.

## 3. Progressive Integration

FT100 integration should be introduced by capability line:

1. Extract a TF100Web tag catalog from the runtime/mapping side.
2. Import the TF100Web tag file into the SCADA Builder V2 project.
3. Import FT100 mapping list.
4. Show available FT100 tags/mappings in the editor.
5. Bind FT100 values to object properties.
6. Generate runtime bindings.
7. Validate missing or incompatible tags.
8. Support tag-conditioned events.
9. Support action relationships.
10. Support advanced scripting and generated JavaScript.
11. Support full project export for FT100 deployment.

## 4. Data Contract Direction

The editor must distinguish:

1. Visual scene model.
2. Responsive layout variants.
3. Object style/properties.
4. FT100 mappings.
5. TF100Web tag catalog.
6. Actions.
7. Scripts.
8. Generated runtime output.

The FT100 mapping layer should not be mixed directly into UI widgets.

## 5. Design Principle

The SCADA project must remain understandable even before full FT100 integration exists.

The model should allow:

1. Placeholders for unresolved mappings.
2. Warnings for missing mappings.
3. Deferred binding resolution.
4. Import/reimport of FT100 mappings.
5. Future migration without rewriting scene layouts.

## 6. TF100Web Alignment Audit

TF100Web repository path:

```text
F:\Projet\Git\TF100Web
```

Historical intake risk observed on 2026-06-11:

1. TF100Web previously supported imported SCADA HTML from `F:\Projet\Git\TF100Web\import`.
2. Older Django-side wiring contained hardcoded page entries for `win00008`.
3. Older imported page contracts used a now-deprecated page-local index file.
4. Older CSS/image serving could split HTML, CSS, and images between `import/` and `static/asset/scada/`.
5. Older runtime numeric behavior used Django-side/manual mapping dictionaries.

Current contract correction on 2026-06-15:

1. `index.html` is deprecated and must not be emitted by new SCADA Builder V2 FT100/TF100Web exports.
2. TF100Web intake for current SCADA Builder V2 packages must start from `import/scada-builder-v2-ft100-package/manifest.json`.
3. Page HTML is addressed by the manifest `RelativePath`, normally `<page-id>/<page-id>.html`.
4. Legacy `index.html` fallback, if retained in TF100Web, is an isolated compatibility path for old packages only.
5. Hardcoded mapping dictionaries remain transition material and must not define the SCADA Builder V2 package contract.

Current SCADA Builder V2 export behavior:

1. `Ft100SceneExporter` exports to `<export-root>/<scene-id>/`.
2. The generated HTML file is `<scene-id>.html`, not `index.html`.
3. New exports emit `ft100-source-layer`; older copied content may still contain `ft100-legacy-layer`.
4. The export writes `css/<scene-id>.css`, `images/`, `manifest.json`, and `README.txt`.
5. Regression coverage protects relative CSS/image paths, source-layer naming, removed-source omission, source-only deleted image omission, edited source bounds, French text repair, required display dimensions, and manifest output.

Integration risks:

1. TF100Web and SCADA Builder V2 disagree on the HTML entry file name.
2. TF100Web splits the SCADA package between `import/` for HTML and `static/asset/scada/` for CSS/images.
3. TF100Web does not yet consume `manifest.json`.
4. Runtime bindings are duplicated manually in `frontend/views.py`.
5. There is no package validation gate before TF100Web renders an imported scene.
6. Multi-page export and navigation are only partially represented in the current intake.
7. Generated SCADA files appear to be copied manually, which allows HTML, CSS, images, and binding metadata to drift.

## 7. Target Contract

The stable SCADA Builder V2 to TF100Web contract is a single normalized folder package:

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

Contract rules:

1. SCADA Builder V2 owns package generation.
2. TF100Web owns package intake, validation, static serving, and runtime telemetry binding resolution.
3. `manifest.json` is the authoritative Django-readable contract for page identity, page dimensions, required display dimensions, build inclusion, home page identity, header/footer composition, object identity, event bindings, actions, and binding placeholders.
4. TF100Web must not depend on editor-only state, selection overlays, handles, drag rectangles, diagnostics, or Studio Element+ workzone geometry.
5. `index.html` is deprecated for current SCADA Builder V2 FT100/TF100Web exports. New exports must use `<page-id>.html` and `ft100-source-layer`; TF100Web legacy fallback must not be documented as the current contract.
6. The imported package should be copied or staged atomically so TF100Web never renders mixed HTML/CSS/image versions.
7. Binding resolution should move from hardcoded page dictionaries toward manifest-driven matching against `RegisterMapping`.
8. SCADA Builder V2 recreates one package folder per export so stale pages, CSS, images, and metadata are removed.
9. Deleted source objects are filtered from source HTML before image asset copying, including decommissioned legacy image nodes that were selected and deleted before they were materialized as scene objects.
10. `LegacyStatic` scene elements are source-projection state. FT100 export must use them to write `data-id` CSS for edited imported-object bounds and must keep them in `manifest.json` as source inventory metadata, but must not emit them as `ft100-element` DOM nodes.
11. FT100 export source resolution must not blindly treat `08_web_modernized` as raw source. The V2 scene/project model is the target source of truth. During migration, raw visual comparison should prefer `03_web_legacy/html_pages/*`; `08_web_modernized/*` may be used only as comparison/history material or as an explicitly approved sanitized source with helper scripts, layout tools, test panels, and editor-only artifacts removed.
12. `win00009` currently displays correctly in SCADA Builder V2 and should be used as a comparison baseline for expected source-layer rendering. `win00008` is a known regression candidate because its modernized source, inventory metadata, and exported package positions diverge.
13. Header and footer are compiled pages. TF100Web must compose them from root manifest references as complete page roots in header/body/footer slots, not by copying loose child nodes into another page.
14. Header/body/footer slot dimensions come from each page record `Width` and `Height`; the exported HTML root also exposes `data-scada-width` and `data-scada-height` for diagnostics and fallback validation.
15. Any viewport scaling must be applied once to the composed page container. Independent scaling or recentering of header, body, and footer pages is outside the approved contract.
16. SCADA Builder V2 emits critical inline geometry on the page root, HTML source-layer objects with saved bounds, and modern Element+ objects to protect fragment composition during deployment, while `css/<page-id>.css` remains the full runtime stylesheet and must be loaded from the same package version.
17. SVG source shapes with `data-id` remain selectable and movable in the editor, but FT100 export must preserve their native SVG geometry model and must not inject HTML absolute-position inline styles into SVG child tags.
18. Source omission during export is driven by scene state: `RemovedSourceElementIds` and explicit conversion suppression. WebView CSS state, inventory deltas, and temporary masking are not export inputs.
19. CSS, generated DOM ids, and runtime action lookup are page-namespaced under the exported root id. TF100Web composes header, body, and footer in one document, so generated selectors must not rely on package-global `data-id`, Element+ ids, FT100 layer classes, `:root`, or `html/body`; those identities are page-local and may repeat across composed pages.

## 8. Alignment Plan

Phase 1 - Freeze and validate the file contract:

1. Add a TF100Web importer service that discovers `import/<scene-id>/manifest.json`.
2. Accept `<scene-id>.html` as the current entry file and reject `index.html` for current SCADA Builder V2 package validation.
3. Resolve CSS and images from the same package version instead of requiring manual duplication into `static/asset/scada`.
4. Add validation errors for missing HTML, CSS, manifest, root id, duplicate object ids, and broken image references.
5. Add tests in TF100Web for package discovery, HTML extraction, asset URL rewriting, and rejection of current packages that only provide `index.html`.

Phase 2 - Make telemetry binding manifest-driven:

1. Extend SCADA Builder V2 export metadata with binding placeholders where the scene has runtime numeric/text objects.
2. Add stable object ids and optional source traces into `manifest.json`.
3. Replace the hardcoded `SCADA_IMPORTED_PAGES["win00008"]["elements"]` dictionary with a manifest intake model.
4. Match bindings by explicit mapping id first, then OPC UA node id, then approved keyword/device/protocol matching.
5. Surface unresolved bindings as operator-visible configuration warnings in TF100Web.

Phase 3 - Add one-click deployment from SCADA Builder V2:

1. Add an export target preset for the local TF100Web repository path.
2. Write to a staging folder, validate package shape, then atomically replace `TF100Web/import/<scene-id>`.
3. Produce a deploy report listing copied files, unresolved bindings, warnings, and package version.
4. Keep generic folder export available for non-local deployments.

Phase 4 - Multi-page and runtime action parity:

1. Let TF100Web discover all compiled pages listed in root `manifest.json`.
2. Start the runtime at `HomePageId`, falling back to the first compiled `default` page if no explicit home is present.
3. Compose a compiled page with its referenced compiled `header` and `footer` pages when present.
4. Preserve SCADA Builder V2 object-owned action bindings such as `click -> navigate`.
5. Ensure navigation resolves through TF100Web routes or package-relative page paths consistently.
6. Add regression coverage for page navigation, missing target pages, header/footer composition, and stale package rollback.

SCADA Builder V2 implementation status:

1. Multi-page package export is implemented for compiled pages.
2. Root `manifest.json` is emitted inside `scada-builder-v2-ft100-package`.
3. Per-page folders remain emitted as `<page-id>/<page-id>.html`, `css/`, `images/`, and page-local `manifest.json`.
4. Legacy static imported geometry is no longer exported as empty Element+ runtime DOM nodes, while `manifest.json` still preserves source inventory metadata for those objects.
5. Current SCADA Builder V2 still has an implementation gap around migration source selection: documentation now requires V2 model/sanitized-source governance, while the existing resolver may still select `legacy.source_html` when present.
6. TF100Web consumes the root manifest and performs runtime header/page/footer composition in the package loader.
7. FT100 page HTML now includes page type/dimension data attributes and inline geometry for the exported root, HTML source-layer legacy elements, and modern Element+ elements; SVG source shapes remain governed by SVG attributes plus page CSS. Regression coverage protects the fragment-composition guardrail.

Phase 5 - Production hardening:

1. Add package schema versioning.
2. Add package checksum or generated timestamp metadata.
3. Add compatibility tests for previously exported `ft100-legacy-layer` packages.
4. Document the operator deployment workflow for Windows development and FT100 unit deployment.

Phase 6 - TF100Web tag catalog extraction and import:

1. Add a TF100Web-side extraction mechanism that writes the available runtime tags into a versioned file.
2. Use a deterministic JSON tag catalog as the first interchange format.
3. Recommended file name:

```text
tf100web-tags.json
```

4. The file must be importable into a SCADA Builder V2 project without requiring manual copy/paste of tag names.
5. SCADA Builder V2 stores the imported catalog as project metadata and treats it as the source for tag pickers, tag binding validation, and tag-conditioned events.
6. Events must reference stable tag ids from the imported catalog, not display labels or DOM ids.
7. Reimport must detect removed, renamed, stale, and type-changed tags without silently rewriting existing event bindings.
8. The import workflow must produce a validation report before preview/export.

Minimum tag catalog fields:

1. Schema version.
2. Extracted timestamp.
3. TF100Web source identity or station identity.
4. Stable tag id.
5. Display name.
6. Runtime path or mapping key.
7. Data type.
8. Unit, when available.
9. Read/write capability.
10. Quality/degraded metadata.
11. Optional source trace to the TF100Web mapping model.
12. Optional grouping/category information for editor browsing.

Tag catalog rules:

1. Boolean event conditions initially support `If tag is true`, `If tag is false`, and `If tag is degraded`.
2. Numeric/string tag events remain future work until boolean conditions and degraded semantics are stable.
3. `Degraded` must be represented explicitly by the catalog/runtime quality contract; SCADA Builder V2 must not infer it from color, CSS class, or text formatting.
4. A tag catalog import does not grant write capability. Tag writes remain disabled until the FT100 write contract is approved.
5. The catalog is a project input, not an editor UI cache; it must survive save/reload and participate in validation.

## 9. Near-Term Industrial USB Deployment Direction

This is a future industrial deployment direction, not an implemented runtime behavior yet.

In Industrial mode, the target deployment source should become a provisioned USB key instead of a manually copied folder:

1. A TF100/industrial provisioning tool formats a USB key into an approved deployment layout.
2. The provisioning tool writes a mapping table or mapping registry seed onto the USB key.
3. SCADA Builder V2 reads that mapping registry as the source for variable binding, action binding, and event relationship configuration.
4. SCADA Builder V2 validates the project against the USB mapping registry before compilation.
5. SCADA Builder V2 pushes the compiled `scada-builder-v2-ft100-package` onto the same USB key as an atomic deployment artifact.
6. TF100Web, when `TF100_INDUSTRIAL_DEPLOYMENT` is active, uses the USB-provided package and mapping registry as its SCADA Builder V2 runtime source.
7. Manual mapping dictionaries in TF100Web remain a transition mechanism only and must be replaced by the registry-driven workflow.

USB deployment hardening requirements:

1. The USB layout must include package version, schema version, generated timestamp, and source project identity.
2. The mapping registry must be validated before SCADA Builder V2 accepts it as a binding source.
3. TF100Web must reject incomplete packages, stale mixed package content, and mappings that do not match the compiled package.
4. The deployment process should support checksums or signatures before being considered production-ready.
5. The Windows development workflow may still support direct folder export, but the Industrial operator workflow should converge on USB provisioning.

## 10. Open Decisions

1. Decide whether TF100Web should serve package assets directly from `import/` or copy accepted packages into `static/asset/scada/`.
2. Decide the exact binding schema fields required in `manifest.json`.
3. Decide how TF100Web should expose multiple imported SCADA scenes in station configuration.
4. Decide whether local SCADA Builder V2 export should know the default TF100Web path or use a user-configured deployment target.
5. Decide the exact USB filesystem layout, registry file format, and validation/checksum policy for Industrial deployments.
6. Decide the approved sanitized-source policy for pages where `03_web_legacy`, `08_web_modernized`, inventory JSON, and saved V2 scene geometry disagree.
7. Decide whether `tf100web-tags.json` is generated from TF100Web runtime discovery, static mapping configuration, or both.
8. Decide the exact stale-tag policy: warning-only, blocking validation, or explicit user remap.
9. Decide the canonical degraded-state sources and precedence when communication quality, timestamp age, and runtime diagnostics disagree.
