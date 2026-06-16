# SCADA Builder V2 - Legacy Modernization Workflow

Date: 2026-06-15
Status: Draft direction
Document version: `V2.1.1.0030`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Clarification du role non-source de `08_web_modernized` et ajout du repere `win00009` correct / `win00008` divergent. |
| 2026-06-15 | `V2.0.2.0028` | `2b59efb` | Baseline initiale du depot SCADA Builder V2. |

## 1. Core Decision

SCADA Builder V2 owns its official domain model.

Legacy Wonderware/ArchestrA files, extracted HTML, generated JSON, and `AMR_REF_SCADA` are not the target model. They are source material used to inspect, compare, extract, and validate modernization work.

The V2 editor must separate three concepts:

1. Legacy source: read-only material imported or opened for inspection.
2. Extraction workspace: review area where useful legacy elements are selected, classified, grouped, cleaned, accepted, rejected, or deferred.
3. V2 scene: official modern SCADA model used by preview, editing, save, and build.

## 2. Product Goal

The original goal was not simply to display Wonderware pages.

The goal is to take Wonderware/ArchestrA pages, modernize them, and compile them into a modern web SCADA output suitable for FT100.

V2 must make this workflow explicit:

1. Open a legacy page.
2. Inspect what was extracted.
3. Decide what should become part of the modern scene.
4. Convert accepted candidates into V2 elements.
5. Improve visual quality using modern CSS and reusable components.
6. Compare the modern result with the legacy scene.
7. Build/export only from the V2 model.

## 2.1 Raw Legacy Source Priority

When multiple versions of the same `winXXXXX` page exist, V2 must prefer the least modified source available for extraction.

For `win00008`, the known source chain is:

```text
01_legacy_source/pages_win/win00008.win
01_legacy_source/pages_win/win00008.wvw
01_legacy_source/pages_win/win00008.wbk
01_legacy_source/pages_win/win00008.bmp
02_extract/xml/win00008_a0cf691217f4.xml
03_web_legacy/html_pages/win00008_a0cf691217f4.html
08_web_modernized/html_pages/win00008_updated.html
```

Extraction should favor:

1. `01_legacy_source` and `02_extract` when structural data is needed.
2. `03_web_legacy/html_pages/*` when an unworked HTML rendering is needed.
3. `08_web_modernized/*` only as comparison material or as a record of previous manual work.

The modernized HTML may contain edits, helper scripts, layout tools, repaired positions, or component substitutions. It must not be treated as the raw source.

## 2.2 Known Page Baselines

Current visual audit notes:

1. `win00009` displays correctly in SCADA Builder V2 and is the current known-good comparison page for normal rendering.
2. `win00008` does not display correctly and must be treated as a regression candidate.
3. `win00008` contains conflicting evidence between modernized HTML, inventory JSON, saved V2 scene geometry, and current FT100/TF100Web export output.
4. `win00008_updated.html` contains helper/test tooling such as the condenser test panel and layout-tool local storage logic; these are not runtime/export material.
5. Until the sanitized-source policy is implemented, `win00008` must not be used as evidence that the source resolution contract is correct.

## 3. Integrated Tools

### 3.1 Legacy Viewer

The Legacy Viewer displays the legacy scene as source material.

Initial modes:

1. Legacy only.
2. V2 only.
3. Side-by-side comparison.
4. Overlay comparison.

Rules:

1. The Legacy Viewer is read-only by default.
2. It must not become the source of truth.
3. It can expose coordinates, raw ids, raw names, screenshots, HTML fragments, and extracted metadata.
4. It must allow manual comparison against the active V2 scene.

### 3.2 Legacy Extraction Workspace

The extraction workspace is where legacy content becomes modernization candidates.

Auto-detect scope:

1. Detect atomic elements.
2. Read legacy ids, names, positions, dimensions, text, tag type, SVG type, and style data.
3. Create extraction candidates for individual detected elements.
4. Do not perform logical grouping automatically in the first implementation.

Grouping belongs to V2:

1. Convert/detect elements into V2 objects.
2. Let the user select multiple V2 objects.
3. Let the user group them manually.
4. Let the user promote a group into the V2 library.
5. Keep all legacy ids as trace metadata.

Candidate states:

1. Candidate.
2. Accepted.
3. Rejected.
4. Converted.
5. Deferred.

Each candidate keeps traceability:

1. Source system.
2. Source document or page.
3. Source element id.
4. Source element name.
5. Source bounds.
6. Extraction notes.

Accepted candidates are converted into V2 elements. Rejected or deferred candidates remain available for audit but do not enter the official scene.

### 3.3 V2 Scene Editor

The V2 Scene Editor edits the official domain.

Rules:

1. V2 scene elements use V2 ids and user-facing display names.
2. Legacy ids such as `legacy:795` remain trace metadata only.
3. Modern styles, bindings, actions, scripts, and responsive layout variants belong to the V2 scene.
4. Preview and build read from V2 scenes, not from legacy HTML.

### 3.4 Element+ Conversion

`Element+` is the user-facing name for official V2 elements created from legacy material.

Initial conversion scope:

1. Convert legacy Text-like elements into Element+ Text objects.
2. Preserve visible text, bounds, font family, font size, foreground, background, and source trace when available.
3. Assign a V2-owned id instead of reusing legacy ids as primary identifiers.
4. Hide converted legacy elements in the working view to avoid visual duplicates.
5. Keep the legacy source file unchanged; conversion affects the V2 scene model only.

Reload rule:

1. A converted Element+ keeps its legacy source id as trace metadata.
2. On scene reload, V2 derives hidden legacy ids from those traces.
3. This prevents converted legacy objects from reappearing in the working inventory after close/reopen.

Undo lifetime:

1. The complete legacy object snapshot is kept only in the current application session undo cache.
2. If the application closes without undo, that snapshot is discarded.
3. The saved V2 scene keeps the Element+ object and removes editable legacy overrides tied to the converted source id.

## 4. Domain Boundary

Official V2 domain:

1. `ScadaProject`
2. `ScadaScene`
3. `ScadaElement`
4. `SceneBounds`
5. `LegacySourceTrace`
6. Responsive variants.
7. Bindings, actions, scripts, styles, and validation rules.

Legacy support domain:

1. `LegacySourceDocument`
2. `LegacyExtractionCandidate`
3. `LegacyComparisonSession`
4. Import reports and diagnostics.

Legacy support objects feed the V2 domain, but they do not replace it.

## 5. AMR_REF_SCADA Role

`AMR_REF_SCADA` is a reference legacy sample and a regression corpus.

It is not considered a good project model and must not define the V2 architecture.

Its value is:

1. Representative legacy material.
2. Known pages to test viewer/extraction/compare workflows.
3. A way to validate that V2 can progressively decouple from legacy output.
4. A source for concrete modernization decisions.

## 6. Build Rule

The FT100 build must compile from the V2 project model only.

Legacy artifacts may be included as references, diagnostics, or comparison aids, but generated FT100 output must not depend on legacy page HTML as the runtime source of truth.

If a temporary source layer is required during migration, the source must be explicitly classified and sanitized. Modernized HTML with helper scripts, layout tools, test panels, or manually repaired geometry is not acceptable as raw runtime source.

## 7. First Implementation Milestones

1. Keep current AMR legacy preview as temporary Legacy Viewer input.
2. Add V2 scene model and tests.
3. Add extraction candidate model and tests.
4. Point `win00008` raw extraction to `03_web_legacy/html_pages/win00008_a0cf691217f4.html`.
5. Detect atomic elements from one legacy page without grouping.
6. Convert detected candidates into V2 elements.
7. Group V2 elements manually inside the V2 editor.
8. Promote selected V2 groups into the library.
9. Show Legacy Viewer and V2 scene side-by-side.
10. Generate preview from V2 element model.
11. Keep traceability back to the legacy candidate.
