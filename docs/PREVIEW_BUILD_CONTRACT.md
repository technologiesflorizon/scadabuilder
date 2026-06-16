# SCADA Builder V2 - Preview Build Contract

Date: 2026-06-15
Status: Draft
Document version: `V2.1.1.0036`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0036` | `PENDING` | Generalisation du contrat de namespace CSS/DOM/runtime par page pour tous les selecteurs exportes TF100Web. |
| 2026-06-15 | `V2.1.1.0035` | `PENDING` | Ajout du contrat de CSS page-scopee pour les regles source `data-id` dans les compositions header/body/footer. |
| 2026-06-15 | `V2.1.1.0034` | `PENDING` | Clarification que la selection preview est polymorphe et que la suppression source durable passe par `RemovedSourceElementIds`, pas par masquage. |
| 2026-06-15 | `V2.1.1.0033` | `PENDING` | Limitation du garde-fou inline aux couches HTML legacy et verrouillage de la selection large des elements source `data-id`, incluant SVG. |
| 2026-06-15 | `V2.1.1.0032` | `PENDING` | Ajout du contrat de geometrie inline pour les source-projection legacy persistants. |
| 2026-06-15 | `V2.1.1.0031` | `PENDING` | Ajout du contrat de composition header/body/footer et du garde-fou HTML inline pour la geometrie critique des fragments FT100. |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Deprecation explicite de `index.html`, clarification preview/export source-of-truth et ajout du repere `win00009` correct / `win00008` divergent. |
| 2026-06-15 | `V2.1.1.0029` | `2b59efb` | Baseline initiale du depot SCADA Builder V2. |

## 1. Objective

This document defines the contract between the WebView2 preview and the generated build output.

The preview and build must render the same project model with the same runtime CSS, reset CSS, responsive rules and validation assumptions.

The goal is to prevent a screen from looking correct in the editor and different after export.

## 2. Scope

This contract covers:

1. WebView2 preview rendering.
2. Generated HTML/CSS/JavaScript build output.
3. Reset and normalization CSS.
4. CSS-first responsive generation.
5. Runtime asset resolution.
6. Validation before preview and build.
7. Diagnostics for preview/build differences.

This contract does not define:

1. FT100 communication protocol.
2. Scripting language syntax.
3. Legacy import parsing.
4. Project XAML schema details beyond render inputs.

## 3. Shared Render Inputs

Preview and build must consume the same normalized render package.

Render package inputs:

1. Project identity.
2. Scene identity.
3. Responsive mode.
4. Device preset.
5. Orientation.
6. Logical elements.
7. Variant layout data.
8. Styles and CSS custom properties.
9. Bindings, object event bindings, and action metadata.
10. Asset manifest.
11. Runtime options.

Rules:

1. The editor view model is not a render contract.
2. Preview must render from the same normalized package that build uses.
3. Scene background color is project scene state and must be available to the preview before the first WebView paint.
4. Live background updates may use WebView scripting, but scripting must update from the scene property rather than becoming the source of truth.
5. Element+ move/resize commit messages from the preview must include before and after bounds when they create undoable scene geometry changes.
6. Build must not infer missing layout values that preview did not use.
7. Any fallback applied during preview must be recorded in diagnostics.
8. Any fallback applied during build must be recorded in the build report.
9. Imported source ids removed from the active scene are render inputs. Preview and build/export must omit those source objects and must not copy assets referenced only by removed source objects.
10. Build/export output must present imported material as the source layer for new artifacts. Compatibility selectors may remain for previously generated `ft100-legacy-layer` fragments, but new FT100 exports must emit `ft100-source-layer`.
11. Page records provide structure, dimensions, required display dimensions, type, build inclusion, home-page participation, header/footer composition, and background; object event bindings provide runtime interaction triggers.
12. Pages must not own action trigger lists in preview, build, or Django manifests.
13. Active scene `CanvasSize` is render input and must be replayed during preview refresh/reload before editor overlay layers are rendered.
14. Edited imported-object bounds are active scene geometry. Preview refresh must replay those bounds into the source projection, and FT100 export must emit data-id based CSS so exported source-layer objects use the same geometry.
15. FT100 export manifests must keep page `Width` and `Height` as the authored `CanvasSize`, and must also report `RequiredDisplayWidth` and `RequiredDisplayHeight` as the minimum display area required by exported runtime geometry.
16. `LegacyStatic` imported-source projections must not be emitted into the Element+ runtime layer. They must remain in manifests as source inventory metadata, and their export-side rendering effect is limited to source-layer CSS needed to replay saved SCADA Builder geometry.
17. FT100 preview/export must not treat modernized legacy HTML as raw source by default. The V2 scene/project model is the target source of truth; temporary legacy source layers must be classified as raw, comparison, or sanitized before export.
18. `win00009` is the current known-good visual comparison page in SCADA Builder V2. `win00008` is a known regression candidate and must not be used to validate the contract until its source/position/tooling divergence is resolved.
19. Header and footer pages are render inputs of their own. They must be composed through manifest references as complete page fragments with their own page root, dimensions, CSS, images, and runtime metadata.
20. FT100 page HTML must carry critical inline geometry for the exported page root, persisted HTML source-layer objects, and Element+ objects so fragment injection preserves authored positions even when an intake pipeline temporarily handles HTML before CSS is attached. The page CSS remains authoritative for complete runtime styling and must be loaded.
21. Imported source elements with `data-id` are editable imported geometry even when they are not `.layer` nodes. Preview selection must include broad source `[data-id]` elements, movement must update native SVG geometry such as `x`, `y`, `width`, and `height` for rectangles, and export must not inject HTML `position`, `left`, or `top` declarations into SVG child tags.
22. Preview inventory is not the same as preview selection. Inventory sent to the C# materialization path must stay scoped to managed source projections, otherwise dense raw pages such as `win00008` can report hundreds of non-materialized `data-id` nodes.
23. Inventory deltas must not auto-hide or delete source nodes. A present imported source element remains selectable unless explicit scene state suppresses it through conversion or `RemovedSourceElementIds`.
24. Source/object deletion is durable only when the active scene records it. Preview may remove a source DOM node for immediate feedback, but reload, undo/redo, save, and export must derive visibility from scene state rather than from WebView CSS masking.
25. Exported CSS, generated DOM ids, and runtime action lookup must be namespaced by exported page root id. Unscoped generated selectors are forbidden, including `:root`, `html/body`, raw `[data-id="..."]`, raw `.ft100-*`, and raw Element+ ids such as `#Button1`, because TF100Web composes header, body, and footer pages in one document and page-local identities can repeat.
26. TF100Web must apply viewport scaling to the composed header/body/footer container as one unit. It must not independently scale or recenter header, body, and footer slots.

## 4. Output Targets

Preview target:

1. WebView2.
2. Local generated preview bundle.
3. Same reset CSS as build.
4. Same runtime CSS as build.
5. Same generated scene CSS as build.

Build target:

1. Static HTML.
2. Generated CSS.
3. Generated JavaScript runtime.
4. Asset folder.
5. Manifest/report files.

Deprecated generic build sketch:

```text
exports/
  <build-id>/
    <entry>.html
    assets/
    css/
      reset.css
      runtime.css
      scene.css
    js/
      runtime.js
      bindings.js
      actions.js
    manifest.json
    validation-report.json
```

Current FT100 scene export structure:

```text
exports/
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

The FT100 project export structure is the current implemented SCADA Builder V2 to TF100Web package shape. `scada-builder-v2-ft100-package` is recreated on each export and is the only folder intended to be moved or copied into TF100Web. The root `manifest.json` indexes all compiled pages. Each page folder remains browser-openable and keeps a page-local manifest for diagnostics and compatibility. `index.html` is deprecated for current SCADA Builder V2 FT100/TF100Web exports and must not be presented as an active package target.

The generated `manifest.json` is the Django-readable runtime contract. It must include page records, object inventory, object event bindings, action definitions, asset references, and validation metadata while excluding editor-only state. Source-projection-only `LegacyStatic` objects are retained as manifest inventory but are not emitted as runtime Element+ DOM.

Header/footer composition output rules:

1. The root manifest is the only source of page inventory and header/footer references.
2. A composed default page uses three ordered slots: header, body, and footer.
3. Slot dimensions come from each referenced page record `Width` and `Height`.
4. Each slot must preserve the complete exported page root and load that page's CSS from the same package version.
5. HTML-side `data-scada-page-type`, `data-scada-width`, and `data-scada-height` are diagnostics and fallback metadata; they must not replace the manifest.
6. HTML source-layer objects with saved SCADA Builder geometry must have their original inline source style preserved, then receive SCADA Builder `position`, `left`, `top`, `width`, and `height` declarations directly in the exported HTML.
7. SVG source child tags with `data-id` must not receive HTML absolute-position inline styles. Their authored SVG attributes and page CSS are the runtime geometry contract.

## 5. WebView2 Preview Parity

WebView2 is the editor preview host, not a separate renderer.

Rules:

1. WebView2 must load generated preview HTML from the render package.
2. Preview must not use WPF-only layout behavior to represent final SCADA geometry.
3. Preview overlays, handles and selection frames must be separate editor layers.
4. Editor overlays must not change the generated scene DOM geometry.
5. Preview must expose the active preset, orientation, scale factor and responsive mode.
6. Preview refresh must be deterministic for the same render package.
7. When previewing imported legacy HTML, scene dimensions must update the legacy page CSS variables and the editor overlay layer. This resizes the scene surface without scaling legacy source geometry unless a separate scale-content command is explicitly implemented.
8. Manual scene-surface resize handles are editor-only preview controls. They may commit `CanvasSize` changes to the active scene, but they must not become runtime or exported build DOM.
9. During manual preview resizing, live dimension feedback may update editor property fields, but undoable scene state must be committed only at the end of the drag gesture.

Allowed preview-only features:

1. Selection outline.
2. Resize handles.
3. Guides.
4. Grid overlay.
5. Safe area overlay.
6. Validation markers.
7. Diagnostics panel.

Preview-only features must:

1. Be injected outside the scene runtime root or in clearly marked editor overlay roots.
2. Never become part of exported build output.
3. Never be required for the scene to render correctly.

## 6. CSS-First Build Contract

Build output must express layout and responsive behavior primarily in CSS.

Generated CSS layers:

1. `reset.css`
   - Browser normalization.
2. `runtime.css`
   - Shared SCADA runtime primitives.
3. `scene.css`
   - Project and scene-specific generated CSS.
4. Optional component CSS
   - Only for reusable components with stable contracts.

Ordering:

```text
reset.css
runtime.css
component css
scene.css
```

Rules:

1. Later layers may override earlier layers only through intentional selectors.
2. Scene CSS owns element geometry.
3. Runtime CSS owns shared behavior and base classes.
4. Reset CSS owns browser defaults only.
5. CSS custom properties are preferred for theme, scale and shared dimensions.
6. Generated CSS must be deterministic from the render package.

JavaScript may not be used to fix normal CSS layout differences between preview and build.

## 7. Reset CSS

SCADA Builder V2 must ship a controlled reset CSS.

Required reset behavior:

1. Apply `box-sizing: border-box` globally.
2. Remove default margins from `html`, `body` and generated scene roots.
3. Set explicit base font family, font size and line height.
4. Set explicit text rendering assumptions where needed.
5. Normalize button and input inherited font behavior.
6. Neutralize browser default button styling for SCADA components.
7. Set explicit background and text colors on root elements.
8. Set explicit overflow behavior on `html`, `body` and scene wrappers.
9. Remove dependency on user-agent stylesheet spacing.
10. Keep focus styling accessible and deterministic.

Reset CSS must not:

1. Contain project-specific geometry.
2. Hide overflow globally in a way that masks validation issues.
3. Override scene-specific z-index or positioning.
4. Depend on browser-specific hacks unless documented.

Minimum reset baseline:

```css
*, *::before, *::after {
  box-sizing: border-box;
}

html,
body {
  margin: 0;
  min-width: 100%;
  min-height: 100%;
}

body {
  font-family: var(--scada-font-family);
  font-size: var(--scada-font-size);
  line-height: var(--scada-line-height);
  color: var(--scada-text-color);
  background: var(--scada-page-background);
}

button,
input,
select,
textarea {
  font: inherit;
}
```

## 8. Runtime CSS

Runtime CSS defines stable primitives used by preview and build.

Required primitives:

1. Scene viewport wrapper.
2. Scene surface.
3. Element base class.
4. Group base class.
5. Text base class.
6. Image base class.
7. Interactive element base class.
8. Hidden state.
9. Disabled state.
10. Alarm/state classes.

Rules:

1. Runtime CSS class names must be stable.
2. Runtime CSS cannot include editor-only styles.
3. Runtime CSS cannot assume one responsive mode.
4. Runtime CSS must support fixed, scale-to-fit and adaptive layout.
5. Runtime CSS must be loaded in WebView2 and build output without divergence.

## 9. Scene CSS

Scene CSS is generated from the render package.

Responsibilities:

1. Scene dimensions.
2. Element geometry.
3. Variant overrides.
4. Media queries.
5. Orientation queries.
6. Safe area variables.
7. Project theme variables.
8. Component instance styling.

Rules:

1. Element selectors must be deterministic.
2. Shared styles are emitted once.
3. Variant-specific styles are emitted in scoped blocks.
4. Generated CSS must preserve stable element identity.
5. CSS output must be reproducible for the same input.
6. Invalid values must fail validation before CSS emission.
7. Current FT100/TF100Web scene CSS must be emitted through the page namespace helper. Page dimensions must be concrete page-scoped declarations, not package-global CSS variables.
8. Scene element ids may be exported as `data-scada-element-id` metadata, but runtime DOM ids must be page-prefixed so two composed pages can both contain `Button1` without duplicate DOM ids or cross-page CSS/action targeting.

## 10. Asset Resolution

Preview and build must resolve assets through the same manifest model.

Asset manifest fields:

1. Logical asset ID.
2. Source path.
3. Build path.
4. MIME/type hint.
5. Hash or version token.
6. Missing asset policy.

Rules:

1. Preview can load from source paths or staged preview paths.
2. Build must copy or package assets into the export folder.
3. The DOM must reference build-stable asset paths.
4. Missing critical assets are build errors.
5. Missing non-critical decorative assets are warnings.
6. Asset paths must be normalized before preview/build.

## 11. Validation Gates

Validation must run before preview and before build.

Preview gate:

1. Validate render package shape.
2. Validate active scene exists.
3. Validate selected preset exists.
4. Validate CSS values.
5. Validate asset references.
6. Show warnings and errors in the editor.

Build gate:

1. Run all preview gate checks.
2. Validate all configured presets required by the project.
3. Validate all scenes included in export.
4. Validate unresolved bindings.
5. Validate missing critical assets.
6. Validate generated CSS for conflicts.
7. Produce a validation report.
8. Validate that every compiled page references only compiled header/footer pages of the correct type.

Build must fail on:

1. Invalid render package.
2. Invalid scene dimensions.
3. Invalid CSS value.
4. Duplicate element ID.
5. Missing required adaptive variant.
6. Unresolved binding on visible runtime element.
7. Missing critical asset.
8. Preview/build CSS layer mismatch.
9. A compiled page referencing a missing, wrong-type, or non-compiled header/footer page.

## 12. Preview/Build Difference Detection

The system must make divergence visible.

Required diagnostics:

1. Render package hash.
2. Reset CSS hash.
3. Runtime CSS hash.
4. Scene CSS hash.
5. Active preset.
6. Orientation.
7. Responsive mode.
8. Scale factor when relevant.
9. Browser/WebView2 user agent.
10. Build timestamp or preview timestamp.

Rules:

1. Preview and build reports must include render package identity.
2. A build generated from the same render package must produce the same scene CSS.
3. A CSS hash mismatch between preview and build is an error unless caused by an approved build-time minification step.
4. Minification must preserve a source map or equivalent trace when diagnostics are enabled.

## 13. Generated JavaScript Boundaries

JavaScript runtime responsibilities:

1. Data binding updates.
2. Object-triggered operator actions.
3. Navigation.
4. Generated scripts.
5. Runtime state classes.

JavaScript runtime must not:

1. Recompute responsive breakpoints.
2. Rewrite element geometry for normal responsive layout.
3. Apply hidden viewport-specific fixes outside the render package.
4. Depend on editor-only APIs.
5. Mask validation errors by mutating the DOM after load.
6. Infer page-owned actions that are not present as object event bindings.

## 14. Acceptance Criteria

A preview/build implementation satisfies this contract when:

1. The same render package can produce preview and build output.
2. WebView2 preview loads the same reset, runtime and scene CSS layers as build.
3. Fixed, scale-to-fit and adaptive layout render through CSS-first rules.
4. Desktop, tablet and mobile presets produce deterministic preview output.
5. Rotation updates preset, orientation and safe area behavior.
6. Validation reports blocking errors before build export.
7. Generated build output can run without editor-only services.
8. Diagnostics can prove which render package and CSS files were used.
9. Header/footer fragment composition preserves authored coordinates for HTML source-layer, SVG source-shape, and modern Element+ objects when rendered from the exported page root and page CSS.

## 15. Open Decisions

1. Confirm the exact WebView2 runtime version baseline.
2. Decide whether build output should include minified CSS in the first implementation.
3. Decide where validation reports are stored for preview-only sessions.
4. Confirm whether source maps are required for generated CSS in MVP.
5. Decide the approved sanitized-source policy for pages where raw legacy HTML, modernized HTML, inventory JSON, and saved V2 scene geometry disagree.
