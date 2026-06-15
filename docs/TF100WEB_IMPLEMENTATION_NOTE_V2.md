# SCADA Builder V2 - TF100Web Implementation Note

Date: 2026-06-15
Status: Draft implementation note
Document version: `V2.1.1.0030`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Deprecation explicite de `index.html`, mise a jour hygiene depot/tests et clarification du prochain travail TF100Web. |
| 2026-06-15 | `V2.1.1.0025` | `2b59efb` | Baseline initiale du depot SCADA Builder V2. |

## 1. Objective

This note records the current SCADA_BUILDER_V2 audit findings and prepares the next implementation tranche for TF100Web package intake.

The goal is to make TF100Web consume the SCADA Builder V2 FT100 package as a stable runtime artifact instead of relying on manual file placement and hardcoded Django mapping dictionaries.

## 2. Audit Summary

Current SCADA_BUILDER_V2 status:

1. The solution is organized as a .NET 8 layered application with `Domain`, `Application`, `Infrastructure`, `Rendering`, WPF editor app, separate Studio Element+ app, and MSTest regression suite.
2. `dotnet test ScadaBuilderV2.sln --no-restore` passes with 167 tests as of the 2026-06-15 baseline audit.
3. The FT100 exporter already emits a normalized project package folder named `scada-builder-v2-ft100-package`.
4. Project export recreates the package folder on each export, removing stale files before writing new output.
5. Per-page output includes `<page-id>/<page-id>.html`, `css/<page-id>.css`, `images/`, per-page `manifest.json`, and `README.txt`.
6. Root `manifest.json` indexes compiled pages and exposes home page, page type, header/footer references, required display dimensions, object events, and actions.
7. The domain model already contains `TagBinding`, object-owned event bindings, action definitions, page type, home page, build inclusion, and header/footer composition.
8. Build validation blocks missing home/default-page errors and invalid compiled header/footer references.
9. Editor export currently writes to a user-selected folder, not directly to a TF100Web staging target.

Current repository hygiene findings:

1. SCADA_BUILDER_V2 is now initialized as a local Git repository.
2. Root `.gitignore` excludes generated `bin`, `obj`, WebView2 profiles, test results, logs, artifacts, and generated export packages.
3. The baseline commit is `2b59efb`.
4. Generated export packages remain ignored in this repository; the committed runtime import copy belongs on the TF100Web side.
5. Documentation history entries use `PENDING` for uncommitted changes because a file cannot reliably contain the hash of the commit that creates its own content.

## 3. Evidence From Current Code

Observed implementation anchors:

1. `Ft100SceneExporter.ProjectPackageDirectoryName` is `scada-builder-v2-ft100-package`.
2. Single-page export writes `<scene-id>.html`, `css/<scene-id>.css`, `images/`, `manifest.json`, and `README.txt`.
3. Project export validates the project, computes compiled pages, recreates the package directory, exports each compiled page, then writes the root manifest.
4. Page manifest records `RelativePath`, `Width`, `Height`, `RequiredDisplayWidth`, `RequiredDisplayHeight`, `Background`, `Objects`, and object `Events`.
5. Runtime JavaScript can execute object-owned actions including `navigate`, `show`, `hide`, `toggleVisibility`, `setClass`, and `toggleClass`.
6. Element data already carries `TagBinding`, but the export manifest does not yet expose a normalized TF100Web binding contract.
7. The editor UI can edit `TagBinding` through the Element+ property panel and persists it through element changes.

Regression evidence:

1. `Ft100SceneExporterTests.ExportWritesDjangoManifestAndObjectOwnedClickNavigateAction` protects the Django-readable manifest and object-owned navigation action output.
2. `Ft100SceneExporterTests` protect required display dimensions and multi-page package export.
3. `ModernProjectStoreTests` protect persistence for actions, object events, page composition, and home page.
4. `WebViewContextMenuScriptTests` protect WebView bridge behavior, source/object command naming, active scene movement, and FT100 export parity for edited source geometry.

## 4. TF100Web Implementation Target

TF100Web should consume SCADA Builder V2 output as a package:

```text
TF100Web/
  import/
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

The root `manifest.json` is the authoritative intake contract. TF100Web should not infer page inventory by scanning arbitrary HTML files when a manifest is present.

## 5. Proposed TF100Web Intake Slices

Slice 1 - Package discovery and validation:

1. Add a Django-side package discovery service for `import/scada-builder-v2-ft100-package/manifest.json`.
2. Parse the root manifest into a typed intake model.
3. Validate manifest version, home page id, page ids, relative paths, page-local manifests, CSS files, and image references.
4. Accept `<page-id>.html` as the primary entry file.
5. Reject current SCADA Builder V2 packages that provide only `index.html`; any legacy fallback must be isolated outside the current package contract.
6. Return structured validation errors before rendering.

Slice 2 - Asset serving and HTML intake:

1. Serve package assets from the accepted package version or copy accepted packages atomically into the existing static tree.
2. Rework HTML/CSS/image URL rewriting so all paths resolve from the same accepted package.
3. Prevent mixed-version rendering where HTML comes from one copy and CSS/images from another.
4. Add tests for package discovery, page resolution, CSS resolution, image rewriting, and fallback behavior.

Slice 3 - Manifest-driven runtime mapping:

1. Extend SCADA Builder V2 export to emit normalized binding entries for visible runtime objects with `TagBinding`.
2. Add TF100Web binding resolution from manifest entries to `RegisterMapping`.
3. Match by explicit mapping id first, then approved secondary keys such as node id or controlled alias.
4. Report unresolved bindings as configuration warnings, not silent runtime failures.
5. Keep current hardcoded Python dictionaries as transition fallback only.

Slice 4 - Multi-page runtime composition:

1. Let TF100Web route to all compiled default pages listed in the root manifest.
2. Start at `HomePageId`, falling back to the first compiled default page only when no home page is configured.
3. Compose referenced header/footer pages at runtime or during intake according to a single approved policy.
4. Preserve object-owned navigation actions from the manifest/runtime script.
5. Add tests for missing target pages, invalid header/footer references, and navigation route generation.

Slice 5 - Deployment hardening:

1. Add a SCADA Builder V2 export target preset for the local TF100Web path.
2. Export to a staging directory, validate package shape, then atomically replace the TF100Web import package.
3. Produce a deploy report listing pages, assets, unresolved bindings, warnings, and package version.
4. Add package checksum or generated timestamp metadata.
5. Keep USB provisioning as the industrial deployment target once the local folder workflow is stable.

## 6. SCADA Builder V2 Changes Needed

Minimum next changes in SCADA Builder V2:

1. Add binding entries to the FT100 manifest for elements with `Data.TagBinding`.
2. Include binding metadata in root and per-page manifests.
3. Decide whether `TagBinding` is a raw operator-entered string or a structured mapping reference.
4. Add tests covering binding manifest output for text, numeric, input, and source-converted objects.
5. Add optional export target configuration for TF100Web, but keep the current generic folder export.
6. Add a root `.gitignore` to exclude `bin/`, `obj/`, WebView2 profiles, test results, and generated export folders.

## 7. Open Decisions

1. Should TF100Web serve assets directly from `import/scada-builder-v2-ft100-package`, or copy validated packages into `static/asset/scada`?
2. What exact binding schema should replace raw `TagBinding` in the manifest?
3. Which TF100Web object should own binding resolution: page view, importer service, station service, or a dedicated SCADA package service?
4. Should header/footer composition happen at TF100Web intake time or at request render time?
5. What is the approved atomic replacement strategy on Windows and on the target FT100 environment?
6. What sanitized-source policy should block pages like `win00008` from exporting mismatched modernized/source/inventory geometry while allowing known-good pages such as `win00009` to continue rendering?

## 8. Recommended Next Step

Implement Slice 1 in TF100Web first.

This is the narrowest useful integration step because it gives TF100Web a stable package reader and validation gate without changing SCADA Builder V2 runtime behavior. Once TF100Web can reliably read the root manifest and render a page from `<page-id>/<page-id>.html` without `index.html`, the binding schema can be added with regression tests on both sides.
