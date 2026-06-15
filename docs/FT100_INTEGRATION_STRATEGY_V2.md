# SCADA Builder V2 - FT100 Integration Strategy

Date: 2026-05-29
Status: Draft
Version: `V2.1.1.0029`

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

1. Import FT100 mapping list.
2. Show available FT100 tags/mappings in the editor.
3. Bind FT100 values to object properties.
4. Generate runtime bindings.
5. Validate missing or incompatible tags.
6. Support action relationships.
7. Support advanced scripting and generated JavaScript.
8. Support full project export for FT100 deployment.

## 4. Data Contract Direction

The editor must distinguish:

1. Visual scene model.
2. Responsive layout variants.
3. Object style/properties.
4. FT100 mappings.
5. Actions.
6. Scripts.
7. Generated runtime output.

The FT100 mapping layer should not be mixed directly into UI widgets.

## 5. Design Principle

The SCADA project must remain understandable even before full FT100 integration exists.

The model should allow:

1. Placeholders for unresolved mappings.
2. Warnings for missing mappings.
3. Deferred binding resolution.
4. Import/reimport of FT100 mappings.
5. Future migration without rewriting scene layouts.

## 6. TF100Web Alignment Audit - 2026-06-11

TF100Web repository path:

```text
F:\Projet\Git\TF100Web
```

Current observed runtime intake:

1. TF100Web currently reads imported SCADA HTML from `F:\Projet\Git\TF100Web\import`.
2. The Django view has a hardcoded page entry for `win00008`.
3. The HTML file path is `import/win00008/index.html`.
4. The CSS URL is served from `static/asset/scada/win00008/css/win00008.css`.
5. Runtime image URLs are rewritten to `static/asset/scada/win00008/images/`.
6. Live numeric runtime behavior is added by TF100Web through `static/asset/js/station/visualisation_import.js`.
7. TF100Web injects mapping attributes from a hardcoded Python dictionary in `frontend/views.py`, not from the SCADA Builder export manifest.

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
5. TF100Web may support `index.html` and `ft100-legacy-layer` as migration compatibility, but new SCADA Builder V2 exports should use `<scene-id>.html` and `ft100-source-layer`.
6. The imported package should be copied or staged atomically so TF100Web never renders mixed HTML/CSS/image versions.
7. Binding resolution should move from hardcoded page dictionaries toward manifest-driven matching against `RegisterMapping`.
8. SCADA Builder V2 recreates one package folder per export so stale pages, CSS, images, and metadata are removed.
9. Deleted source objects are filtered from source HTML before image asset copying, including decommissioned legacy image nodes that were selected and deleted before they were materialized as scene objects.
10. `LegacyStatic` scene elements are source-projection state. FT100 export must use them to write `data-id` CSS for edited imported-object bounds and must keep them in `manifest.json` as source inventory metadata, but must not emit them as `ft100-element` DOM nodes.
11. FT100 export source resolution must prefer the reference page HTML source, such as `08_web_modernized/html_pages/<page>_updated.html`, before falling back to raw `03_web_legacy/html_pages`. The raw fallback is compatibility only and must not replace the source declared by the reference page when that file exists.

## 8. Alignment Plan

Phase 1 - Freeze and validate the file contract:

1. Add a TF100Web importer service that discovers `import/<scene-id>/manifest.json`.
2. Accept `<scene-id>.html` as the primary entry file and keep `index.html` as a compatibility fallback.
3. Resolve CSS and images from the same package version instead of requiring manual duplication into `static/asset/scada`.
4. Add validation errors for missing HTML, CSS, manifest, root id, duplicate object ids, and broken image references.
5. Add tests in TF100Web for package discovery, HTML extraction, asset URL rewriting, and compatibility fallback.

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
5. Reference-page HTML source resolution is preferred for export so pages such as `win00008` keep their modernized source-layer visual content.
6. TF100Web consumes the root manifest and performs runtime header/page/footer composition in the package loader.

Phase 5 - Production hardening:

1. Add package schema versioning.
2. Add package checksum or generated timestamp metadata.
3. Add compatibility tests for previously exported `ft100-legacy-layer` packages.
4. Document the operator deployment workflow for Windows development and FT100 unit deployment.

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
2. Decide whether SCADA Builder V2 should optionally emit a compatibility `index.html` during the transition.
3. Decide the exact binding schema fields required in `manifest.json`.
4. Decide how TF100Web should expose multiple imported SCADA scenes in station configuration.
5. Decide whether local SCADA Builder V2 export should know the default TF100Web path or use a user-configured deployment target.
6. Decide the exact USB filesystem layout, registry file format, and validation/checksum policy for Industrial deployments.
