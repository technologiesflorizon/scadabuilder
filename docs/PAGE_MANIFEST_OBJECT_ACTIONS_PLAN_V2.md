# SCADA Builder V2 - Page Manifest And Object Actions Plan

Date: 2026-06-15
Status: First implementation slice delivered
Document version: `V2.1.1.0030`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Normalisation du header documentaire et rattachement a l'arbre documentaire stable. |
| 2026-06-15 | `V2.1.1.0024` | `2b59efb` | Baseline initiale du depot SCADA Builder V2; premiere tranche page manifest/actions. |

## 1. Objective

SCADA Builder V2 must let an operator select a page from `Projet > Pages`, inspect and edit its page properties, persist those properties in the project manifest, and generate deterministic preview/build output that Django can consume.

The same plan establishes the first runtime interaction foundation:

1. Pages do not own actions.
2. Objects own event bindings.
3. Event bindings reference action definitions.
4. Django consumes the manifest to understand pages, fragments, objects, events, and runtime actions.

## 2. Core Decision

The manifest is a runtime contract, not only an editor cache.

Required separation:

1. Page records describe structure, routing identity, type, authored dimensions, required display dimensions, background, source path, and composition role.
2. Object records describe object identity, geometry, style, bindings, and event bindings.
3. Action definitions describe reusable runtime operations such as navigation or visibility changes.
4. Only object event bindings can trigger actions.
5. A page may be the target of an action, but it is never the owner of that action.

## 3. Page Types

The page type enum must use stable machine values:

```text
default
fragment
header
footer
```

User-facing labels:

1. `default` -> `Defaut`
2. `fragment` -> `Fragment`
3. `header` -> `Entete`
4. `footer` -> `Pied-de-page`

Semantics:

1. `default`: a normal navigable page.
2. `fragment`: reusable HTML sub-tree; not exported as a standalone navigable screen unless a build profile explicitly asks for diagnostics.
3. `header`: reusable top composition region.
4. `footer`: reusable bottom composition region.

## 4. Page Properties

When a page is selected in `Projet > Pages`, the `Propriete` tab must show page properties.

Required basic fields:

1. `Nom de l'element`: example `win00008`.
2. `Type`: `Defaut`, `Fragment`, `Entete`, `Pied-de-page`.
3. `Compiler cette page`.
4. `Page d'accueil`.
5. `Header`.
6. `Footer`.
7. `Dimension`.
8. `Largeur`.
9. `Hauteur`.
10. `Couleur d'arriere fond`.

Composition rules:

1. A project may define one explicit `HomePageId`.
2. If `HomePageId` is absent or invalid, the build/runtime fallback is the first compiled `default` page.
3. A home page must be a compiled `default` page.
4. A compiled page can reference one compiled `header` page and one compiled `footer` page.
5. A compiled page that references a missing, wrong-type, or non-compiled header/footer must fail pre-build validation.
6. `header` and `footer` pages must not reference their own header/footer composition.
7. Non-compiled pages are omitted from build output and may keep unresolved composition as non-blocking editor state.

Recommended advanced background fields:

1. `Image d'arriere fond`.
2. `background-size`.
3. `background-repeat`.
4. `background-position`.
5. `background-attachment`.
6. `background-origin`.
7. `background-clip`.
8. `background-blend-mode`.

Rules:

1. Width and height must be positive integer pixel values.
2. Background values must be validated before save and before build.
3. Unsupported CSS values must produce validation warnings and must not silently generate invalid output.
4. Page properties mutate project/scene model state through commands, not direct UI writes.

## 5. Manifest Shape

The short-term implementation may keep `project.json`, but its shape must be compatible with a later `project.xaml` or versioned manifest exporter.

Recommended JSON shape:

```json
{
  "Name": "AMR_REF_SCADA_V2",
  "ManifestVersion": "2.0",
  "CanvasSize": {
    "Width": 1280,
    "Height": 873
  },
  "Pages": [
    {
      "Id": "win00008",
      "Name": "win00008",
      "Type": "default",
      "RelativePath": "scenes/win00008.scene.json",
      "Width": 1280,
      "Height": 873,
      "Background": {
        "Color": "#ADADAD",
        "Image": null,
        "Size": "cover",
        "Repeat": "no-repeat",
        "Position": "center center",
        "Attachment": "scroll",
        "Origin": "padding-box",
        "Clip": "border-box",
        "BlendMode": "normal"
      }
    }
  ],
  "Actions": [
    {
      "Id": "action_nav_win00009",
      "Kind": "navigate",
      "TargetPageId": "win00009"
    }
  ]
}
```

Rules:

1. `Pages[]` indexes all source page records Django must understand.
2. `Actions[]` is a global or scene-scoped catalog of action definitions, not a page-owned command list.
3. Each action id must be stable.
4. Each `TargetPageId` must reference an existing page.
5. The manifest must be deterministic so Django can safely diff, cache, or regenerate output.

## 6. Scene Shape

Scene files remain the detailed source for objects and page-local runtime data.

Recommended scene shape:

```json
{
  "Id": "win00008",
  "Title": "win00008",
  "PageType": "default",
  "CanvasSize": {
    "Width": 1280,
    "Height": 873
  },
  "Background": {
    "Color": "#ADADAD",
    "Image": null,
    "Size": "cover",
    "Repeat": "no-repeat",
    "Position": "center center"
  },
  "Elements": [
    {
      "Id": "btn_next",
      "DisplayName": "Bouton page suivante",
      "Kind": "Button",
      "Bounds": {
        "X": 100,
        "Y": 100,
        "Width": 160,
        "Height": 40
      },
      "Events": [
        {
          "Trigger": "click",
          "ActionId": "action_nav_win00009"
        }
      ]
    }
  ]
}
```

Rules:

1. Objects own `Events`.
2. Pages do not own `Events`.
3. Scene background and page manifest background must be synchronized from the same domain model.
4. Legacy `BackgroundColor` must remain readable during migration, then normalize into `Background.Color`.

## 7. Object Event Model

Initial event binding shape:

```json
{
  "Trigger": "click",
  "ActionId": "action_nav_win00009"
}
```

Required fields:

1. `Trigger`: the runtime event name.
2. `ActionId`: stable reference to an action definition.

Optional future fields:

1. `Condition`.
2. `StopPropagation`.
3. `PreventDefault`.
4. `DebounceMs`.
5. `Parameters`.

Initial supported triggers:

1. `click`
2. `dblclick`
3. `change`
4. `mouseenter`
5. `mouseleave`

MVP recommendation:

1. Implement `click` first.
2. Validate and serialize the other trigger names only after the runtime can execute them.

## 8. Runtime Action Catalog

Initial supported action kinds:

1. `navigate`: change active page.
2. `show`: show an object.
3. `hide`: hide an object.
4. `toggleVisibility`: toggle object visibility.
5. `setClass`: set a runtime class/state on an object.
6. `toggleClass`: toggle a runtime class/state on an object.
7. `mountFragment`: mount or display a fragment.
8. `writeTag`: future FT100/SCADA write action placeholder.

Rules:

1. `navigate` targets a page id.
2. `show`, `hide`, `toggleVisibility`, `setClass`, and `toggleClass` target object ids.
3. `mountFragment` targets a fragment page id and a host object or named region.
4. `writeTag` must remain disabled or validation-warning-only until the FT100 write contract is approved.
5. Build must fail or warn according to severity when an action target cannot be resolved.

## 9. Django Contract

Django must be able to consume the manifest without interpreting editor-only state.

Django-readable fields:

1. Project identity.
2. Manifest schema version.
3. Page list.
4. Page type.
5. Page dimensions.
6. Page background.
7. Scene file path or build path.
8. Object ids needed for event binding.
9. Object event bindings.
10. Action definitions.
11. Asset references.
12. Validation report path.

Forbidden in the Django runtime contract:

1. Selection state.
2. Resize handles.
3. Drag rectangles.
4. Editor overlays.
5. Workzone zoom or pan.
6. WPF-only UI state.
7. Undo/redo state.

## 10. UI Workflow

Required workflow:

1. User selects a page in `Projet > Pages`.
2. The page remains the active workspace document or is opened if needed.
3. The right panel switches to `Propriete`.
4. The inspector shows page properties.
5. User changes type, width, height, or background.
6. The change executes a domain/application command.
7. The active scene preview refreshes from the same model state.
8. The manifest is marked dirty.
9. Save writes both the page manifest metadata and scene detail consistently.

Initial page commands:

1. `page.setType`
2. `page.resize`
3. `page.setBackground`
4. `page.rename`

Initial object action commands:

1. `object.event.add`
2. `object.event.remove`
3. `object.event.setTrigger`
4. `object.event.setAction`
5. `object.action.createNavigate`

## 11. Preview And Build

Preview and build must consume the same normalized render package.

Render package additions:

1. Page type.
2. Page dimensions.
3. Structured background style.
4. Object event bindings.
5. Runtime action catalog.
6. Django manifest export metadata.

Rules:

1. Page dimensions generate scene surface width and height.
2. Background style generates deterministic CSS.
3. Object event bindings generate runtime event hookup data or generated JavaScript.
4. Editor overlays must not be included.
5. Fragment/header/footer exports must preserve their composition role for Django.

## 12. Validation

Required validation:

1. Page ids are unique.
2. Page names are non-empty.
3. Page type is supported.
4. Page width and height are positive.
5. Background CSS fields are valid for the supported subset.
6. Element ids are unique inside each scene.
7. Object events reference existing action ids.
8. Actions reference existing page ids, element ids, fragment ids, or tag placeholders.
9. No page owns an event binding.
10. No page owns an action trigger list.
11. Fragment pages are not used as normal navigation targets unless explicitly allowed.
12. Editor-only overlays, handles, drag rectangles, zoom, pan, and diagnostics are excluded from runtime/export manifests.

## 13. Implementation Phases

### Phase 1 - Contract Foundation

1. Add domain models for page type, structured background, action definition, and object event binding.
2. Keep backward-compatible loading for current `BackgroundColor`.
3. Extend manifest serialization.
4. Add validation tests for page records and object event references.

### Phase 2 - Page Property Inspector

1. Selecting a page in `Projet > Pages` populates `Propriete`.
2. Add type, width, height, color, and advanced background fields.
3. Route page edits through commands.
4. Refresh preview and dirty state after edits.

### Phase 3 - Resize And Background Output

1. Generate preview CSS from page dimensions and structured background.
2. Generate FT100/Django export CSS from the same render package.
3. Add tests proving resize and background survive save/reload/export.

### Phase 4 - Object Event MVP

1. Add object event list serialization.
2. Add `click -> navigate` creation support.
3. Generate runtime click handlers.
4. Add validation for missing action ids and missing target page ids.

### Phase 5 - Django Manifest Export

1. Generate a Django-readable manifest file.
2. Include page records, object event bindings, action definitions, assets, and validation metadata.
3. Exclude editor-only state.
4. Add regression tests against the manifest schema.

### Phase 6 - Expanded Object Actions

1. Add visibility actions.
2. Add class/state actions.
3. Add fragment mount/display actions.
4. Reserve FT100 write action shape without enabling unsafe writes.

## 14. Regression Requirements

Required tests:

1. Save/reload preserves page type.
2. Save/reload preserves page width and height.
3. Save/reload preserves structured background.
4. Export CSS uses page width and height.
5. Export CSS uses structured background fields.
6. Manifest contains page records but no page-owned action triggers.
7. Object event binding references an action id.
8. `click -> navigate` resolves to an existing page.
9. Missing target page creates a validation warning or error.
10. Editor overlays are absent from Django and FT100 export manifests.
11. Manifest page records keep authored `Width`/`Height` and report `RequiredDisplayWidth`/`RequiredDisplayHeight` from exported runtime geometry.

## 15. Open Decisions

1. Confirm whether the short-term Django manifest is `project.json` directly or a generated `manifest.json` derived from project/scene files.
2. Confirm whether actions are global project catalog entries or scene-local catalog entries with project-level export aggregation.
3. Confirm whether fragments can be direct navigation targets for diagnostics only.
4. Confirm the first supported Django routing convention for `navigate`.
5. Confirm the first full multi-page package layout once TF100Web consumes project-level manifests instead of one scene package at a time.

## 16. Implemented Slice V2.1.1.0012

The first implementation slice covers:

1. Domain models for page type, structured page background, action definitions, and object-owned event bindings.
2. Backward-compatible scene persistence where legacy `BackgroundColor` remains readable and new `Background` metadata is written when page properties are applied.
3. Project manifest enrichment on scene save: page type, page dimensions, and structured background are written to `project.json` scene references.
4. Page property UI fields for type, width, height, background color, and advanced background CSS fields.
5. FT100 export `manifest.json` for Django integration, including page records, object event bindings, action definitions, and no page-owned action trigger list.
6. Runtime export support for object `click` event bindings that invoke `navigate`, visibility, and class actions.
7. Regression coverage for page manifest persistence, object event persistence, structured background export CSS, Django manifest output, and object-owned `click -> navigate`.

## 17. Implemented Slice V2.1.1.0020

The second implementation slice covers:

1. Project-level `HomePageId` with fallback to the first compiled `default` page.
2. Scene/page-level `IncludeInBuild`, `HeaderPageId`, and `FooterPageId` metadata.
3. Page inspector controls for compile inclusion, home page selection, and header/footer composition.
4. Enforcement that only one page is the explicit project home.
5. Pre-build validation for compiled pages that reference missing, wrong-type, or non-compiled header/footer pages.
6. FT100 manifest version `2.1` output with home, compile, header, and footer fields.
7. Regression coverage in `ModernProjectStoreTests` and `Ft100SceneExporterTests`, plus a full solution build.

## 18. Implemented Slice V2.1.1.0021

The third implementation slice covers:

1. Multi-page FT100 project export for all pages marked `IncludeInBuild`.
2. Root-level `manifest.json` containing all compiled page records, `HomePageId`, header/footer composition metadata, and aggregated action definitions.
3. Per-page export folders remain browser-openable and keep their page-local `manifest.json`.
4. The `Exporter dossier FT100` command now exports the compiled project page set instead of only the active page.
5. Open scene tabs are used as export inputs so unsaved active workspace state participates in the export command.
6. Non-compiled pages are omitted from the generated package.
7. Regression coverage verifies compiled multi-page export, aggregate manifest paths, home/header/footer metadata, and omitted non-compiled pages.

## 19. Implemented Slice V2.1.1.0022

The fourth implementation slice covers:

1. A normalized single FT100 package folder named `scada-builder-v2-ft100-package`.
2. The selected export folder is treated as the package parent unless the selected folder is already the package folder.
3. Each export recreates that package folder, removing stale files from the previous export.
4. The package contains root `manifest.json`, root `README.txt`, and all compiled page folders.
5. The export status now reports the package folder that should be moved or copied into TF100Web.
6. Regression coverage verifies stale package content is removed and output is written under the normalized package folder.

## 20. Implemented Slice V2.1.1.0024

The fifth implementation slice covers:

1. FT100 page manifests now report `RequiredDisplayWidth` and `RequiredDisplayHeight`.
2. Required display dimensions are calculated from authored `CanvasSize` plus exported Element+ and materialized source-object geometry, including children inside groups.
3. Authored page `Width` and `Height` remain unchanged so existing FT100/Django consumers keep their page-size contract.
4. Regression coverage verifies required display dimensions when exported geometry extends beyond the authored page.
