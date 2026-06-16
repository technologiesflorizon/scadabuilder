# SCADA Builder V2 - Reference Project Model Notes

Date: 2026-06-15
Status: Draft reference notes
Document version: `V2.1.1.0030`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Normalisation du header documentaire et rattachement a l'arbre documentaire stable. |
| 2026-06-15 | `V2.0.1.0000` | `2b59efb` | Baseline initiale du depot SCADA Builder V2; notes de liaison AMR_REF_SCADA vers modele V2. |

## 1. Reference Project

Legacy sample reference:

```text
F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER\AMR_SCADA\AMR_REF_SCADA
```

Backup recorded by the V2 reference document:

```text
F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\references\backups\AMR_REF_SCADA_backup_20260529_114507
```

Governance:

1. The reference project is read-only legacy source material for V2 model work unless the orchestrator explicitly approves edits.
2. V2 experiments should use generated projects or copies.
3. The purpose of the reference is to validate Legacy Viewer, extraction candidates, modernization, comparison, preview/build parity, responsive strategy, and future FT100 mappings.
4. `project.json`, pages JSON, HTML modernise and inventory data are analysis inputs, not the official V2 domain contract.

## 2. Observed Structure

Observed top-level structure:

```text
AMR_REF_SCADA/
  project.json
  pages/
  assets/
  components/
  libraries/
  runtime/
  dist/
  tools/
  08_web_modernized/
```

Model interpretation:

1. `project.json` is the current project index.
2. `pages/*.json` are current scene/page descriptors.
3. `assets/` contains visual assets used by current pages and generated output.
4. `03_web_legacy/html_pages/` in the repository root contains unworked HTML renderings and should be preferred for raw extraction when available.
5. `08_web_modernized/` contains modernized HTML source, especially for `win00008`, and should be treated as comparison material or previous manual work.
6. `dist/` is generated web output and useful for comparison, not the source-of-truth model.
7. `runtime/` and `dist/runtime/` are current runtime references, not automatically the final V2 runtime.
8. `components/` and `libraries/` are currently empty in the inspected reference and remain future V2 extension points.

## 3. Current Project JSON Mapping

Current `project.json` fields map to V2 as follows:

| Current field | Example | V2 destination |
| --- | --- | --- |
| `name` | `AMR_REF_SCADA` | legacy source display name or suggested V2 project name |
| `version` | `0.1.0` | legacy/import metadata, not V2 product version |
| `target.platform` | `tf100-web` | `Build.Target`, pending FT100 naming decision |
| `target.basePath` | `/` | `Build.BasePath` or deployment option |
| `target.offline` | `true` | `Build.OfflineMode` |
| `theme` | `amr_default` | `WorkspaceDefaults.Theme` |
| `pages[]` | `pages/win00008.json` | legacy source documents, then extraction candidates |
| `assets.paths[]` | `assets`, `08_web_modernized/html_pages/assets` | `Assets.AssetRoot` |
| `imports.*` | updated web roots and source reference | `SourceReferences`, `MigrationTrace` |
| `build.minify` | `true` | `Build.Minify` |
| `build.sourcemaps` | `false` | `Build.SourceMaps` |

Important note:

`project.json.version` is not the SCADA Builder V2 document version. V2 uses `V2.production.feature.iteration`, currently advanced to `V2.0.0.0002` for this documentation pass.

## 4. Scene Inventory

The reference project indexes scenes such as:

```text
pages/win00002.json
pages/win00003.json
pages/win00004.json
pages/win00007.json
pages/win00008.json
...
pages/win00096.json
```

V2 rule:

Each `pages/winxxxxx.json` becomes one logical scene:

```text
pages/win00008.json -> scenes/win00008.scene.xaml
```

The scene id stays stable:

```xml
<SceneRef Id="win00008" Title="win00008" File="scenes/win00008.scene.xaml" />
```

Workspace behavior:

1. Opening `win00008` creates a central workspace tab.
2. Closing the tab does not remove the scene from the project.
3. Dirty state belongs to the editor session and is reflected by the workspace tab marker.
4. Scene deletion is a project command and must update `project.xaml`.

## 5. `win00008` as Regression Test Scene

`win00008` remains valuable as a stress/regression scene because it contains:

1. A fixed canvas of `1280 x 873`.
2. A black background.
3. A `legacy_embed` layer pointing to `../assets/html_pages/win00008_updated.html`.
4. Legacy inventory metadata.
5. Image elements for modernized pipe, tank, pump, and valve assets.
6. Text elements with process labels and placeholder values such as `###.0` and `####`.
7. Source ids such as `784`, `795`, and labels such as `TE-EXT`.

It must not be treated as a known-good visual baseline until the `win00008` modernized-source, inventory, V2 scene, and export geometry divergence is resolved. `win00009` is the current known-good SCADA Builder V2 display reference.

Raw source note:

The unworked HTML source for first-pass extraction is:

```text
F:\Groupe AMR\SCADA_AMR_GROUP\03_web_legacy\html_pages\win00008_a0cf691217f4.html
```

The modernized source:

```text
F:\Groupe AMR\SCADA_AMR_GROUP\08_web_modernized\html_pages\win00008_updated.html
```

is useful for comparison and historical context, but it has already been worked and should not be the default raw extraction source.

Initial V2 import approach:

1. Import the full scene as a `LegacyEmbed` to prove opening and preview.
2. Import the inventory as trace metadata.
3. Promote selected image and text inventory items into V2 logical elements.
4. Preserve source ids and names on each promoted element.
5. Add FT100 placeholder candidates for process labels and value placeholders.
6. Build desktop layout first, then add tablet and mobile variants.

## 6. Legacy Traceability

Trace fields required for migrated scenes:

```xml
<LegacyTrace
    SourceReferenceId="amr-ref-scada"
    SourceProjectFile="project.json"
    SourcePageFile="pages/win00008.json"
    RawSourceHtml="03_web_legacy/html_pages/win00008_a0cf691217f4.html"
    ComparisonHtml="08_web_modernized/html_pages/win00008_updated.html" />
```

Trace fields required for migrated elements:

```xml
<Element
    Id="el-te-ext-label"
    Kind="Text"
    DisplayName="TE-EXT label"
    SourceElementId="823"
    SourceElementName="Text30"
    SourceElementType="Text" />
```

Rules:

1. Legacy ids are trace metadata.
2. V2 element ids are stable and editor-owned.
3. User-facing names come from readable labels when possible.
4. Raw imported encoding issues must be recorded and corrected in V2 display text only after review.

## 7. Responsive Variants

Current reference pages are fixed layouts.

V2 responsive interpretation:

1. `Fixed` mode can reproduce current behavior with minimal change.
2. `ScaleToFit` can adapt current fixed scenes to different panels while preserving proportions.
3. `AdaptiveLayout` creates `Desktop`, `Tablet`, and `Mobile` variants for the same logical scene.

For `win00008`:

```text
scenes/win00008.scene.xaml
scenes/win00008/desktop.layout.xaml
scenes/win00008/tablet.layout.xaml
scenes/win00008/mobile.layout.xaml
```

Rules:

1. Do not create `win00008_desktop`, `win00008_tablet`, and `win00008_mobile` as separate scenes.
2. Bindings and FT100 placeholders remain attached to the logical scene.
3. Layout files may reposition or hide elements per device family.
4. CSS media queries generated by build should be the primary runtime adaptation mechanism.

## 8. Asset Mapping

Observed asset roots in `project.json`:

```text
assets
08_web_modernized/html_pages/assets
08_web_modernized/html_pages
```

V2 mapping:

```xml
<AssetRoot Id="project-assets" Path="assets" Role="ProjectOwned" />
<AssetRoot Id="legacy-html-assets" Path="references/legacy/08_web_modernized/html_pages/assets" Role="LegacyReference" ReadOnly="true" />
<AssetRoot Id="legacy-html-root" Path="references/legacy/08_web_modernized/html_pages" Role="LegacyReference" ReadOnly="true" />
```

Rules:

1. Editable assets should be copied into `assets/`.
2. Legacy-only assets remain under `references/legacy/`.
3. Scene asset references should use normalized project-relative paths.
4. Generated `dist/assets` files must not be imported as source unless a deliberate reverse-import command is used.

## 9. FT100 Placeholder Candidates

The inspected `win00008` inventory contains process-like labels and value placeholders that are good FT100 candidates.

Examples:

| Scene | Source hint | Suggested placeholder | Target |
| --- | --- | --- | --- |
| `win00008` | `TE-EXT` | `tag-te-ext` | outside temperature value text |
| `win00008` | `PT-16 (reelle)` | `tag-pt-16-real` | pressure value text |
| `win00008` | `Consigne` near pressure values | `tag-pressure-setpoint` | setpoint value text |
| `win00008` | `LSH-127` | `tag-lsh-127` | high-level status |
| `win00008` | `LSL-127` | `tag-lsl-127` | low-level status |
| `win00008` | `LSLL-127` | `tag-lsll-127` | very-low-level status |
| `win00008` | `YV-130` | `tag-yv-130` | valve status/action |
| `win00008` | `R-MC-1A` | `tag-r-mc-1a` | condenser fan/motor state |
| `win00008` | `R-MC-1B` | `tag-r-mc-1b` | condenser fan/motor state |

Placeholder rule:

These are candidates only. They must remain `Status="Unresolved"` until matched against an actual FT100 mapping import or orchestrator-approved naming table.

Example:

```xml
<TagPlaceholder
    Id="tag-te-ext"
    Name="TE-EXT"
    DataType="Number"
    Unit="degC"
    Status="Unresolved"
    SourceHint="win00008 legacy label TE-EXT" />
```

## 10. Script Mapping

Current reference pages may contain JavaScript in generated or modernized HTML.

V2 rule:

1. Existing JavaScript is migration evidence, not the preferred source model.
2. V2 scripts should be represented as validated `ScadaBasic` or equivalent model scripts.
3. Generated JavaScript belongs to build output.
4. Scripts should reference actions and bindings by id.
5. Any imported raw JavaScript must be quarantined as legacy trace until converted or explicitly approved.

Candidate script examples:

1. Change text color when a pressure value crosses a threshold.
2. Show or hide a detail group for a pump or condenser.
3. Navigate to another scene from a button.
4. Apply an alarm class based on an FT100 status placeholder.

## 11. Build and Runtime Relationship

Current reference contains:

```text
runtime/
dist/runtime/
dist/pages/
dist/data/pages/
dist/index.html  (legacy generated output; deprecated for current FT100/TF100Web package contract)
```

V2 interpretation:

1. `dist/` is a generated output baseline for comparison.
2. V2 build output should be generated under `exports/ft100-web/`.
3. Preview and build must share the same runtime CSS normalization rules.
4. The V2 source model should not depend on browser default styles.
5. The final target should confirm the naming difference between `tf100-web` in the current project and `FT100` in V2 docs.

## 12. Migration Sequence

Recommended first sequence:

1. Create a V2 project copy from `AMR_REF_SCADA`.
2. Generate `project.xaml` from `project.json`.
3. Generate `SceneRef` entries from `pages[]`.
4. Convert `pages/win00008.json` to `scenes/win00008.scene.xaml`.
5. Keep `legacy_embed` as a first-pass scene element.
6. Promote selected inventory items into V2 elements.
7. Create desktop layout placements from current `x`, `y`, `width`, and `height`.
8. Add unresolved FT100 placeholders for obvious process labels.
9. Preview `win00009` first as the known-good display reference, then use `win00008` as a regression/stress scene.
10. Add scale-to-fit behavior.
11. Add tablet and mobile layout variants only after desktop parity is acceptable.
12. Compare V2 preview against raw legacy, generated dist output, and modernized HTML references without treating modernized HTML as raw source.

## 13. Validation Notes

Minimum checks for the reference migration:

1. Every `project.json.pages[]` entry resolves to an existing file.
2. Every generated `SceneRef.File` resolves.
3. Each scene id is unique.
4. Each promoted element has a V2 id and trace source.
5. Asset paths are normalized and resolvable.
6. Legacy embeds are flagged as migration debt.
7. FT100 placeholders are visible as warnings, not errors.
8. Generated preview does not mutate the original `AMR_REF_SCADA`.

## 14. Open Questions

1. Confirm whether `tf100-web` and `FT100Web` are the same target or separate target names.
2. Confirm the first actual FT100 mapping source file format.
3. Decide whether the initial importer should generate XAML directly or first generate an intermediate domain model.
4. Decide how to handle text encoding issues detected in legacy HTML output.
5. Decide which `win00008` elements should be promoted first into reusable V2 components.
