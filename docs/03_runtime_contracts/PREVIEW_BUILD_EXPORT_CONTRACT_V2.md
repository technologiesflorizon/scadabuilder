# SCADA Builder V2 - Preview Build Export Contract

Date: 2026-06-19
Status: Active runtime contract
Document version: `V2.1.2.0034`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-19 | `V2.1.2.0034` | `61eef34` | Ajout du contrat export CSS appui/actif pour les boutons Element+. |
| 2026-06-17 | `V2.1.2.0019` | `bd6515e` | Ajout de l'export archive `.sb2` FT100 avec validation de compatibilite avant packaging. |
| 2026-06-16 | `V2.1.2.0007` | `PENDING` | Clarification du curseur FT100 runtime par defaut sur boutons et cibles avec events. |
| 2026-06-16 | `V2.1.2.0006` | `PENDING` | Clarification de la parite export des events runtime portes par des groupes Element+. |
| 2026-06-16 | `V2.1.2.0005` | `PENDING` | Ajout des metadonnees preview/export du hover automatique des boutons Element+. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du contrat actif preview/build/export separe des notes historiques. |

## 1. Contract

Preview, build, and export must consume the same V2 project and scene model.

Editor-only artifacts are never runtime geometry:

1. Selection overlays.
2. Handles.
3. Drag rectangles.
4. Diagnostics.
5. Layout tools.
6. Test panels.
7. Studio workzone state.

Element+ button hover behavior is FT100Web runtime metadata, not an editor overlay and not SCADA Builder V2 preview styling. Preview must preserve `ScadaButtonBehavior` without applying hover locally. FT100 export must preserve `ScadaButtonBehavior` in the manifest and may generate page-scoped CSS `:hover` rules from enabled hover metadata.

Element+ button pressed/active behavior is also FT100Web runtime metadata. Preview must preserve `ScadaButtonBehavior.Pressed` without simulating press state locally. FT100 export must preserve the metadata in the manifest and may generate page-scoped CSS `:active` plus active toggle-state rules from enabled pressed metadata.

Element+ group runtime events are model behavior, not editor overlay geometry. FT100 export must preserve a group event by emitting a transparent runtime wrapper for hit-testing and `data-scada-events`, while groups without runtime events may remain flattened.

Runtime click affordance is export-owned styling. FT100 export must generate page-scoped `cursor: pointer` CSS for Element+ buttons and elements carrying `data-scada-events`, including descendants and active click state.

FT100 `.sb2` export is a packaging layer over the same V2 project export model. It must first generate the normalized `scada-builder-v2-ft100-package` folder in a staging directory, validate TF100Web intake compatibility and page namespace rules, then create a ZIP archive with `.sb2` extension. The `.sb2` archive must not change scene geometry, runtime markup, CSS scoping, or page composition semantics compared with the validated folder package.

## 2. Flow

```mermaid
flowchart TD
  Model[V2 project and scene model] --> Preview[Preview renderer]
  Model --> Build[Build/export renderer]
  Preview --> Diff[Preview/build difference detection]
  Build --> Package[Runtime package]
  Package --> Validate[TF100Web intake and namespace validation]
  Validate --> Archive[.sb2 archive]
  Package --> Tests[Regression tests]
  Diff --> Tests
```

## 3. Related Decisions

1. `DEC-0004` - Shared Preview Build Export Model.
2. `DEC-0007` - Page-Scoped Runtime Namespace.
3. `DEC-0012` - Element+ Button Default Hover Behavior.
4. `DEC-0013` - Runtime Group Event Wrapper Export.
5. `DEC-0014` - Runtime Pointer Cursor For Clickable Targets.

## 4. Related Tests

1. `tests/ScadaBuilderV2.Tests/PreviewDocumentTests.cs`
2. `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
3. `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`
