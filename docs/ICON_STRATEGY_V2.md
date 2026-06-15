# SCADA Builder V2 - Icon Strategy

Date: 2026-06-15
Status: Draft strategy
Document version: `V2.1.1.0030`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0030` | `PENDING` | Normalisation du header documentaire et rattachement a l'arbre documentaire stable. |
| 2026-06-15 | `V2.0.2.0026` | `2b59efb` | Baseline initiale du depot SCADA Builder V2; strategie d'icones et carte initiale des commandes. |

## 1. Objective

SCADA Builder V2 needs a modern, coherent icon system for a dense WPF desktop editor.

The icon system must support:

1. Top ribbon tabs: `Fichier`, `Edition`, `Ecran`, `Selection`, contextual command groups.
2. Left toolbar: compact icon-first tools under `Outil`.
3. Project navigation and page actions under `Projet`.
4. Right panel context: `Page`, `Element`, `Propriete`, `Librairie`.
5. Build, FT100 import, legacy import, warnings, lock states, and preview states.
6. Reuse through the command registry, where each command owns an `icon key`.

The icon system must not become scattered across individual controls. Ribbon buttons, left toolbar buttons, menus, context menus, command palettes, and property panels must all resolve icons from the same icon registry.

## 2. Design Direction

Recommended visual direction:

1. Outline-first icons.
2. Simple geometry, readable at 16 px and 20 px.
3. Stroke around 1.75 to 2 px at 24 px source size.
4. Rounded joins and caps where the library supports them.
5. No heavy filled pictograms for normal tools.
6. Filled or accented variants reserved for active state, warning, error, or selected toggle.
7. Use Florizon accent colors only for state and emphasis, not for every icon.
8. Keep normal icons in text color or secondary text color.
9. Distinguish `lock selection` from `lock object` with different shapes, not color alone.

WPF implementation direction:

1. Prefer vector icons as `DrawingImage`, `PathGeometry`, or reusable XAML resources.
2. Do not depend on web icon fonts for the desktop shell.
3. Do not load icons from a CDN.
4. Store icon resources under a dedicated UI resource namespace later, for example `Icons.xaml`.
5. Expose icons through stable keys such as `Icon.Project.Save`, not file names.
6. Keep icon color themeable through `DynamicResource` brushes.
7. Keep icon size controlled by the button style, not by each path.

## 3. Option A - Internal XAML Vector Icons

Description:

Create SCADA Builder V2 icons manually as WPF vector resources.

### Style

Strengths:

1. Fully aligned to the Florizon industrial desktop identity.
2. Can include SCADA-specific metaphors such as FT100 build, legacy import, HMI page, tag binding, alarm state, responsive variants, and industrial symbols.
3. Can intentionally separate `lock selection` and `lock object`.
4. Can avoid consumer-style metaphors that feel too generic for industrial tooling.

Weaknesses:

1. Hard to reach broad icon coverage quickly.
2. Consistency requires design discipline and review.
3. Easy to create slightly different strokes, view boxes, and spacing over time.
4. Higher maintenance cost when new commands are added.

### License To Verify

1. Internal original icons should be owned by Groupe AMR / Florizon Technologies according to the project IP policy.
2. Every icon must be original or derived from approved licensed material.
3. If an internal icon is inspired by an external library, it must be redrawn enough to avoid copying protected expression.
4. Any contractor-created icon work must have assignment or usage rights confirmed.

### WPF / XAML Compatibility

Compatibility is excellent if icons are authored directly as WPF vectors.

Preferred shapes:

1. `DrawingImage` for reusable image resources.
2. `GeometryDrawing` and `PathGeometry` for simple monochrome paths.
3. `Viewbox` only where dynamic scaling is needed.

Implementation cautions:

1. Avoid complex nested groups that make theming difficult.
2. Avoid hard-coded colors inside each icon.
3. Normalize all icons to one source box, preferably 24 x 24.
4. Test 16 px rendering for ribbon small buttons and toolbar left buttons.

### Relevance For Fichier Ribbon

Internal icons are relevant for:

1. `Nouveau projet` with SCADA/project identity.
2. `Build FT100` with a target/export metaphor.
3. `Import Legacy` with a migration/source-system metaphor.
4. Any command where a generic open-source icon is ambiguous.

Risk:

The Fichier ribbon needs many standard commands. Drawing all of them internally delays the UI shell for little product value.

### Relevance For Left Toolbar

Internal icons are relevant for:

1. SCADA-specific drawing tools.
2. Future components and industrial symbols.
3. Object lock vs selection lock distinction.

Risk:

Generic tools such as select, move, text, image, group, zoom, and lock are already well covered by established libraries.

## 4. Option B - Open Source Icon Library

Description:

Use an approved open-source icon library as the baseline, converting only selected SVG icons to WPF vector resources.

Recommended operational rule:

1. Vendor only the selected icons, not an entire web package.
2. Keep the upstream library name, version or commit, license file, and attribution notice in a third-party notice document.
3. Convert SVG to XAML through a repeatable process later.
4. Keep custom SCADA-specific icons in a small internal overlay set.

### Candidate Libraries

| Library | Style | License to verify | WPF / XAML compatibility | Fichier ribbon fit | Left toolbar fit |
| --- | --- | --- | --- | --- | --- |
| Lucide | Clean outline, rounded, modern, close to Feather style. Good for dense editor UI. | ISC, with some Feather-derived icons under MIT. Verify current upstream license and derived-icon notice before distribution. | Good. SVG outline paths convert cleanly to XAML `PathGeometry`. Stroke-based icons need consistent WPF stroke styling. | Strong for new/open/save/import/export/settings/wrench/search. Less specific for FT100 without custom overlay. | Very strong for select, move, type, image, group, lock, zoom, panels, align, distribute. |
| Fluent UI System Icons | Microsoft-style, broad, polished, available in regular/filled weights. Feels native in Windows desktop apps. | MIT. Verify upstream package and trademark guidance. | Good. SVG variants convert well. Multiple sizes/weights help WPF clarity but require selection discipline. | Strong for Windows-like ribbon commands and panel states. | Strong for toolbar and property panel; may feel more Office/Windows than custom industrial. |
| Material Symbols / Material Icons | Broad Google icon vocabulary, recognizable, multiple weights/fill/optical sizes. | Apache 2.0. Verify NOTICE obligations and whether using font, SVG, or generated assets changes packaging obligations. | Good if using SVG exports. Icon font path is less desirable for WPF command buttons. | Strong coverage, but visual language can feel Android/web rather than industrial desktop. | Good coverage; some icons are visually heavier or less crisp in dense WPF at 16 px. |
| Bootstrap Icons | Simple UI-oriented SVG icons, MIT, broad enough for common app commands. | MIT. Verify current upstream license. | Good. SVG can be converted or embedded. Some icons are 16 x 16 source, which can help small UI but may mismatch 24 x 24 sets. | Good for standard file/edit/page icons. Less refined for specialized editor states. | Good for basic tools; less extensive than Lucide/Fluent for design-editor semantics. |

### License Sources To Recheck

Initial license references checked on 2026-05-29:

1. Lucide: `https://raw.githubusercontent.com/lucide-icons/lucide/main/LICENSE`
2. Material Design Icons: `https://raw.githubusercontent.com/google/material-design-icons/master/LICENSE`
3. Fluent UI System Icons: `https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/LICENSE`
4. Bootstrap Icons: `https://raw.githubusercontent.com/twbs/icons/main/LICENSE`

These references are planning evidence only. Legal approval still needs a formal third-party dependency review before shipping.

### Relevance For Fichier Ribbon

Open-source libraries are the best default for standard commands:

1. New project.
2. Open project.
3. Save.
4. Save all.
5. Recent projects.
6. Import.
7. Export/build.
8. Settings/options.

The likely gap is `Build FT100`, which should use either a custom internal icon or a composed icon based on an approved library icon plus internal badge.

### Relevance For Left Toolbar

Open-source libraries are strongly suited to the left toolbar because most tools are standard editor metaphors:

1. Pointer selection.
2. Move.
3. Text.
4. Image.
5. Group.
6. Lock object.
7. Zoom.
8. Future align/layout tools.

Use tooltips for all icon-only controls and active state styling for the selected tool.

## 5. Recommendation

Use a hybrid strategy:

1. Select one open-source outline library as the baseline.
2. Prefer Lucide or Fluent UI System Icons for the first UI shell.
3. Create a small internal SCADA overlay set only for commands that are not clear in the baseline library.
4. Store every icon behind command-owned icon keys.
5. Convert selected SVGs to WPF XAML resources and theme them with shared brushes.
6. Maintain a third-party notice file before distribution.

Preferred baseline:

1. `Lucide` if the priority is clean, tool-like, lightweight editor icons.
2. `Fluent UI System Icons` if the priority is native Windows desktop familiarity.

Current recommendation for SCADA Builder V2:

Use Lucide as the first baseline candidate, then add internal icons for `Build FT100`, `Import Legacy`, and any industrial object/component commands that Lucide does not express clearly.

Reason:

1. Lucide's outline style matches the desired modern dense editor direction.
2. It covers most common ribbon and toolbar commands.
3. It is easy to convert from SVG to WPF vectors.
4. It avoids the more platform-branded feel of Fluent and Material.
5. It leaves room for Florizon-specific SCADA icons.

Fallback:

If legal or product direction favors Microsoft-native desktop conventions, use Fluent UI System Icons as the baseline instead.

## 6. Icon Resource Rules

Icon key rules:

1. Use stable semantic keys.
2. Do not expose library names in command ids.
3. Keep the library source as metadata, not as the public app contract.

Examples:

```text
Icon.Project.New
Icon.Project.Open
Icon.Project.Save
Icon.Edit.Undo
Icon.Tool.Select
Icon.Selection.Lock
Icon.Object.Lock
Icon.Build.FT100
Icon.Import.Legacy
```

Sizing:

1. Left toolbar: 20 px visual target inside 32 px button.
2. Ribbon small: 16 px or 20 px.
3. Ribbon large: 24 px or 32 px.
4. Right panel tabs: 16 px or 18 px.
5. Status/warning bar: 16 px.

States:

1. Normal: secondary text brush.
2. Hover: main text brush or accent turquoise.
3. Active tool: accent soft background plus main text icon.
4. Disabled: low-opacity secondary text.
5. Warning: dedicated amber.
6. Error: dedicated red.
7. Build success: success green, separate from normal brand accent.

Accessibility:

1. Icon-only buttons require tooltips.
2. Critical commands require text labels in ribbon large mode where space allows.
3. State must not rely on color alone.
4. Active, disabled, warning, and error states need shape, tooltip, text, or status support.

## 7. First Icon Map

This list proposes semantic icon keys and candidate visual metaphors. Library names are examples only and must be verified against the selected baseline.

| Area | Command / concept | Icon key | Preferred metaphor | Candidate source |
| --- | --- | --- | --- | --- |
| Fichier | Nouveau projet | `Icon.Project.New` | file plus / folder plus | Lucide `file-plus-2` or `folder-plus` |
| Fichier | Ouvrir projet | `Icon.Project.Open` | folder open | Lucide `folder-open` |
| Fichier | Enregistrer | `Icon.Project.Save` | save | Lucide `save` |
| Fichier | Enregistrer tout | `Icon.Project.SaveAll` | stacked save / save plus | Lucide `save-all` if available, otherwise internal composition |
| Fichier | Projets recents | `Icon.Project.Recent` | clock over folder | Lucide `history` plus folder composition |
| Fichier | Import legacy | `Icon.Import.Legacy` | import arrow into archive/window | Internal overlay recommended |
| Fichier | Import FT100 mappings | `Icon.Import.FT100Mappings` | table/list import with tag badge | Internal overlay recommended |
| Fichier | Build FT100 | `Icon.Build.FT100` | package/export to target | Internal icon recommended |
| Edition | Annuler | `Icon.Edit.Undo` | curved arrow left | Lucide `undo-2` |
| Edition | Retablir | `Icon.Edit.Redo` | curved arrow right | Lucide `redo-2` |
| Edition | Couper | `Icon.Edit.Cut` | scissors | Lucide `scissors` |
| Edition | Copier | `Icon.Edit.Copy` | two rectangles | Lucide `copy` |
| Edition | Coller | `Icon.Edit.Paste` | clipboard | Lucide `clipboard-paste` |
| Edition | Dupliquer | `Icon.Edit.Duplicate` | copy plus | Lucide `copy-plus` or internal composition |
| Edition | Supprimer | `Icon.Edit.Delete` | trash | Lucide `trash-2` |
| Edition | Panneaux | `Icon.Panel.Restore` | layout panels | Lucide `panel-left`, `panel-right`, or `layout-panel-top` |
| Ecran | Nouvelle page/scene | `Icon.Scene.New` | page plus | Lucide `file-plus-2` |
| Ecran | Renommer page | `Icon.Scene.Rename` | text cursor / edit | Lucide `pencil-line` |
| Ecran | Dupliquer page | `Icon.Scene.Duplicate` | stacked pages | Lucide `copy` |
| Ecran | Supprimer page | `Icon.Scene.Delete` | page x / trash | Lucide `file-x-2` or `trash-2` |
| Ecran | Preview desktop | `Icon.Preview.Desktop` | monitor | Lucide `monitor` |
| Ecran | Preview tablet | `Icon.Preview.Tablet` | tablet | Lucide `tablet` |
| Ecran | Preview mobile | `Icon.Preview.Mobile` | smartphone | Lucide `smartphone` |
| Ecran | Rotation | `Icon.Preview.Rotate` | rotate cw | Lucide `rotate-cw` |
| Ecran | Mode responsive | `Icon.Responsive.Mode` | screens / scaling corners | Lucide `panels-top-left` or internal composition |
| Selection | Tout selectionner | `Icon.Selection.SelectAll` | dashed square / select all | Lucide `scan` or internal |
| Selection | Effacer selection | `Icon.Selection.Clear` | selection x | Internal composition |
| Selection | Grouper | `Icon.Selection.Group` | grouped squares | Lucide `group` |
| Selection | Degrouper | `Icon.Selection.Ungroup` | ungrouped squares | Lucide `ungroup` |
| Selection | Avancer | `Icon.Selection.BringForward` | layer up | Lucide `bring-to-front` |
| Selection | Reculer | `Icon.Selection.SendBackward` | layer down | Lucide `send-to-back` |
| Selection | Aligner | `Icon.Selection.Align` | align lines | Lucide `align-center-horizontal` / variants |
| Selection | Distribuer | `Icon.Selection.Distribute` | distributed bars | Lucide `between-horizontal-start` or internal |
| Selection | Verrouiller objet | `Icon.Object.Lock` | lock on square/object | Internal composition based on lock |
| Selection | Verrouiller selection | `Icon.Selection.Lock` | lock on cursor/selection frame | Internal composition required |
| Outils | Catalogue outils | `Icon.Tools.Catalog` | wrench / toolbox | Lucide `wrench` or `tool-case` equivalent |
| Outils | Selection | `Icon.Tool.Select` | pointer | Lucide `mouse-pointer-2` |
| Outils | Deplacement | `Icon.Tool.Move` | four arrows | Lucide `move` |
| Outils | Texte | `Icon.Tool.Text` | type | Lucide `type` |
| Outils | Image | `Icon.Tool.Image` | image | Lucide `image` |
| Outils | Groupe | `Icon.Tool.Group` | grouped boxes | Lucide `group` |
| Outils | Lock objet | `Icon.Tool.LockObject` | lock object | Internal composition |
| Outils | Zoom | `Icon.Tool.Zoom` | magnifier | Lucide `zoom-in` / `zoom-out` |
| Projet | Projet | `Icon.Project.Root` | folder tree | Lucide `folder-tree` |
| Projet | Parametres projet | `Icon.Project.Settings` | sliders or settings | Lucide `sliders-horizontal` |
| Projet | Resolution | `Icon.Project.CanvasSize` | ruler / rectangle | Lucide `ruler` |
| Projet | Strategy responsive | `Icon.Project.Responsive` | multiple devices | Internal composition |
| Projet | Assets | `Icon.Project.Assets` | image stack | Lucide `images` |
| Page | Page context | `Icon.Context.Page` | document/page | Lucide `file` |
| Page | Arriere-plan | `Icon.Page.Background` | paint bucket / image | Lucide `paint-bucket` or `image` |
| Page | Variables CSS | `Icon.Page.CssVariables` | braces / code | Lucide `braces` |
| Page | Build status | `Icon.Page.BuildStatus` | activity / check circle | Lucide `activity` or state-specific icons |
| Element | Element context | `Icon.Context.Element` | square with handles | Internal or Lucide `square-dashed-mouse-pointer` if available |
| Element | Position / taille | `Icon.Element.Geometry` | move/resize corners | Lucide `move` plus corners |
| Element | Ordre layer | `Icon.Element.Layer` | layers | Lucide `layers` |
| Element | Visibilite | `Icon.Element.Visibility` | eye | Lucide `eye` / `eye-off` |
| Element | Lock objet | `Icon.Element.Lock` | lock on object | Internal composition |
| Propriete | Proprietes | `Icon.Context.Properties` | sliders | Lucide `sliders-horizontal` |
| Propriete | Couleur | `Icon.Property.Color` | palette | Lucide `palette` |
| Propriete | Typographie | `Icon.Property.Typography` | type | Lucide `type` |
| Propriete | Effets | `Icon.Property.Effects` | sparkles | Lucide `sparkles` |
| Propriete | Binding | `Icon.Property.Binding` | link | Lucide `link-2` |
| Propriete | Script/action | `Icon.Property.Script` | code | Lucide `code-2` |
| Librairie | Librairie | `Icon.Context.Library` | blocks / library | Lucide `blocks` |
| Librairie | Composants | `Icon.Library.Components` | component blocks | Lucide `component` |
| Librairie | Symboles | `Icon.Library.Symbols` | shapes | Lucide `shapes` |
| Librairie | Images | `Icon.Library.Images` | images | Lucide `images` |
| Librairie | Icones | `Icon.Library.Icons` | badge/icon grid | Lucide `icons` if available, otherwise grid |
| Build FT100 | Export build | `Icon.Build.FT100` | FT100 target with arrow/package | Internal icon required |
| Build FT100 | Validation build | `Icon.Build.Validate` | shield check / list check | Lucide `shield-check` or `list-checks` |
| Build FT100 | Rapport build | `Icon.Build.Report` | document chart | Lucide `file-chart-column` or internal |
| Import Legacy | Import projet legacy | `Icon.Import.Legacy` | legacy window into project | Internal icon required |
| Import Legacy | Inventaire legacy | `Icon.Import.LegacyInventory` | list tree / archive | Lucide `list-tree` plus badge |
| Import Legacy | Moderniser element | `Icon.Import.Modernize` | refresh / wand | Lucide `wand-sparkles` or internal |

## 8. Implementation Plan

Phase 1 - Decision:

1. Choose baseline library: Lucide or Fluent UI System Icons.
2. Confirm legal approval path.
3. Confirm whether third-party notices are enough for distribution.
4. Confirm if icons can be vendored into the desktop app source.

Phase 2 - Registry:

1. Add icon keys to the command registry spec.
2. Define a WPF icon provider interface later in the UI layer.
3. Map command ids to icon keys once.
4. Use the same keys in ribbon, toolbar, menu, and panels.

Phase 3 - Asset conversion:

1. Select only icons used by visible commands.
2. Record upstream library, version or commit, and source filename.
3. Convert SVG to normalized WPF vector resources.
4. Review every icon at 16, 20, 24, and 32 px.
5. Add internal overlay icons for SCADA-specific gaps.

Phase 4 - Governance:

1. Add a third-party notice document before shipping.
2. Require license review when adding a new icon library.
3. Require UI review when adding custom icons.
4. Keep icons synchronized with the command registry.

## 9. Open Questions

1. Should the baseline library be Lucide or Fluent UI System Icons?
2. Is SCADA Builder V2 distributed externally, internally only, or bundled with FT100 deliverables?
3. Where should third-party notices appear: installer, about dialog, documentation folder, or all three?
4. Should `Build FT100` use the FT100 name directly in the icon or only in the label?
5. Is `FT100` the final target name, or should the current `tf100-web` naming from the reference project remain visible?
6. Should the app support both regular and filled icon variants for active states?
7. What is the minimum icon size in the ribbon small mode?
8. Do we need high-contrast theme variants in the first implementation?
9. Should project-owned icon assets be exposed to users in the `Librairie` panel separately from application UI icons?
10. Should legacy/import icons intentionally use a different visual treatment to mark migration debt?

## 10. License Risks

1. Open-source licenses must be reviewed before distribution, even when permissive.
2. MIT and ISC generally require preserving copyright and permission notices in copies or substantial portions.
3. Apache 2.0 may require retaining notices and marking modified files, depending on how assets are redistributed.
4. Icon conversion from SVG to XAML may count as a modified or derivative asset; record source and modification metadata.
5. Do not mix icons from multiple libraries without tracking each icon's source and license.
6. Avoid icon fonts unless their font license and embedding rights are explicitly approved.
7. Avoid using product trademarks or branded marks inside icons unless the project has rights to do so.
8. Some libraries include derived icons from earlier projects; preserve both upstream notices where required.
9. If icons are shipped inside an installer or executable resource, notices still need to be available to recipients.
10. Legal approval should decide whether a generated `THIRD_PARTY_NOTICES.md` is required for V2 before the first customer-facing build.

## 11. Risks

1. Too many custom icons will slow the UI shell and create inconsistency.
2. Too many libraries will make the UI feel patched together.
3. Library icons may not cover industrial SCADA-specific commands clearly.
4. WPF SVG conversion can introduce rendering differences if strokes, view boxes, or transforms are not normalized.
5. Dense ribbon usage can make icons ambiguous without labels or tooltips.
6. `lock selection` and `lock object` are high-risk for user confusion and need visibly different icons.
7. Build/import icons can imply destructive operations if arrow direction and labels are unclear.
8. If application UI icons and project library icons share the same visual treatment, users may confuse app commands with draggable SCADA assets.
