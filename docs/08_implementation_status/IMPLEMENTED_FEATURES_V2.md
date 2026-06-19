# SCADA Builder V2 - Implemented Features

Date: 2026-06-19
Status: Active implementation status
Document version: `V2.1.2.0041`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-19 | `V2.1.2.0041` | `88a3e8b` | Extraction du catalogue de ruban dans Application et ajout de tests de registre. |
| 2026-06-19 | `V2.1.2.0040` | `335adfb` | Implementation du registre de commandes actif pour le rendu du ruban superieur. |
| 2026-06-19 | `V2.1.2.0039` | `e5f8a82` | Implementation de la refonte du ruban superieur et du registre d'icones visible. |
| 2026-06-19 | `V2.1.2.0038` | `6f76dc8` | Cloture du bloc boutons HMI avec parite metadata preview/export. |
| 2026-06-19 | `V2.1.2.0037` | `2a540d6` | Implementation des evenements runtime pour boutons HMI standards. |
| 2026-06-19 | `V2.1.2.0036` | `8cc4d33` | Implementation du runtime disabled reel pour boutons Element+. |
| 2026-06-19 | `V2.1.2.0035` | `588d712` | Implementation du runtime on/off pour boutons Toggle Element+. |
| 2026-06-19 | `V2.1.2.0034` | `61eef34` | Implementation du style appui/actif pour les boutons HMI Element+. |
| 2026-06-19 | `V2.1.2.0033` | `89d7165` | Implementation des symboles HMI Element+ electriques et alarme. |
| 2026-06-18 | `V2.1.2.0032` | `d5ee1fd` | Implementation des proprietes avancees Element+ opacite et rotation. |
| 2026-06-18 | `V2.1.2.0031` | `f6a85ed` | Implementation des symboles HMI Element+ moteur, ventilateur, convoyeur et jauge. |
| 2026-06-18 | `V2.1.2.0030` | `cae57c9` | Implementation des presets de boutons HMI Element+ `Command`, `Toggle`, `Navigation`, `AlarmAcknowledge` et `EmergencyStop`. |
| 2026-06-18 | `V2.1.2.0029` | `b97ef16` | Implementation des primitives process HMI Element+ reservoir, tuyaux, vanne et pompe. |
| 2026-06-18 | `V2.1.2.0028` | `PENDING` | Implementation des primitives HMI Element+ voyant et barres de valeur. |
| 2026-06-18 | `V2.1.2.0027` | `PENDING` | Implementation des formes standards Element+ et insertion manuelle des boutons Element+ depuis le ruban. |
| 2026-06-17 | `V2.1.2.0026` | `876a6be` | Correction du transport manifest `Data.DisplayFormat` et alignement du formatage TF100Web sur les datatypes de mapping. |
| 2026-06-17 | `V2.1.2.0025` | `58567eb` | Ajout du support TF100Web des masques `DisplayFormat` `#` comme comportement implemente. |
| 2026-06-17 | `V2.1.2.0024` | `PENDING` | Refactor de l'onglet Donnees Element+ et activation de `Format affichage` comme source du masque numerique. |
| 2026-06-17 | `V2.1.2.0022` | `PENDING` | Harmonisation TF100Web pour consommer les events de binding `ValueBindings` depuis `.sb2`. |
| 2026-06-17 | `V2.1.2.0021` | `1040889` | Correction du handler `.sb2` pour afficher le statut et la progression des le clic. |
| 2026-06-17 | `V2.1.2.0020` | `c2f0b6f` | Correction du validateur CSS `.sb2` et ajout du feedback de progression non bloquant. |
| 2026-06-17 | `V2.1.2.0019` | `bd6515e` | Implementation de l'export `.sb2` FT100 et du validateur anti-collision/compatibilite TF100Web. |
| 2026-06-17 | `V2.1.2.0018` | `ad364a6` | Clarification que plusieurs runtimes sont exporteur-only tant que TF100Web n'execute pas les scripts de page exportes. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Implementation des effets visuels runtime standards. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Implementation du bridge lifecycle runtime global. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Implementation des groupes de conditions runtime et politique de tag manquant. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Implementation des options runtime avancees pour popup Fragment. |
| 2026-06-17 | `V2.1.2.0016` | `PENDING` | Implementation des actions runtime de bordure Element+. |
| 2026-06-17 | `V2.1.2.0015` | `PENDING` | Implementation des actions `Fermer popup` et `Basculer popup`. |
| 2026-06-17 | `V2.1.2.0014` | `PENDING` | Implementation de l'action `Ouvrir popup` vers fragments compiles. |
| 2026-06-17 | `V2.1.2.0013` | `PENDING` | Implementation des filtres et du resume de catalogue tags dans l'editeur. |
| 2026-06-17 | `V2.1.2.0012` | `PENDING` | Implementation de l'application runtime des valeurs de tags lues. |
| 2026-06-17 | `V2.1.2.0010` | `PENDING` | Implementation des actions objet conditionnelles sur tags importes. |
| 2026-06-17 | `V2.1.2.0009` | `PENDING` | Implementation des bindings Element+ `Lire valeur` et `Ecrire valeur` sur tags importes. |
| 2026-06-17 | `V2.1.2.0008` | `PENDING` | Implementation de l'import catalogue tags TF100Web et de l'authoring Element+ `WriteTag`. |
| 2026-06-16 | `V2.1.2.0007` | `PENDING` | Implementation du curseur runtime par defaut pour boutons et cibles cliquables FT100. |
| 2026-06-16 | `V2.1.2.0006` | `PENDING` | Implementation de l'export FT100 des events `Clic -> Changer de page` portes par des groupes Element+. |
| 2026-06-16 | `V2.1.2.0005` | `PENDING` | Implementation des metadonnees hover automatique des boutons Element+ et de la tab Bouton. |
| 2026-06-16 | `V2.1.2.0004` | `PENDING` | Implementation du registre evenements Element+ et de la modale Clic -> Changer de page. |
| 2026-06-16 | `V2.1.2.0003` | `PENDING` | Correction du groupement Element+: ordre visuel preserve, enfants affiches sous leur groupe et deplacement solidaire. |
| 2026-06-16 | `V2.1.2.0002` | `PENDING` | Implementation du groupement de scene Element+ only et de l'avertissement conversion pour les selections legacy. |
| 2026-06-16 | `V2.1.2.0001` | `PENDING` | Correction du raccourci Backspace pour les Element+ selectionnes et protection des champs editables contre les raccourcis scene. |
| 2026-06-16 | `V2.1.2.0000` | `PENDING` | Implementation de la conversion Button plausible, du choix Propriete contextualise et du rendu/export du texte des boutons Element+. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du registre des fonctionnalites implementees. |

## 1. Current Verified Baseline

As of 2026-06-19, `dotnet test ScadaBuilderV2.sln --no-restore` passes with 243 tests.

## 2. Implemented Areas

1. Layered .NET 8 solution with Domain, Application, Infrastructure, Rendering, WPF editor, Studio Element+ app, and MSTest regression suite.
2. FT100/TF100Web export package with root manifest and page-local folders.
3. Page-scoped CSS/DOM namespace for composed runtime pages.
4. Header/footer composition references in project/page manifest flow.
5. Polymorphic selection of source and Element+ objects.
6. Durable source deletion through `RemovedSourceElementIds`.
7. Studio Element+ selection, movement, grouping, visibility, lock, resizing, geometry editing, and `.sep` save path baseline.
8. Object-owned click navigation action baseline in model and export manifest.
9. Dynamic Element+ conversion targets for legacy buttons, including `Button1` on `win00003`.
10. Context-menu `Propriete` command enabled for converted Element+ objects and visibly disabled with warning for non-converted source objects.
11. Button Element+ text rendering in editor preview and FT100 export.
12. WebView keyboard shortcuts protect editable controls and keep `Backspace` non-destructive for selected Element+ objects.
13. Scene-level grouping of selected Element+ objects with legacy/source grouping decommissioned behind a conversion warning.
14. Element+ group regressions preserve sibling render order, expose group children in the Element tab hierarchy, and normalize child movement to the containing group.
15. Element+ event registry with French trigger labels and the first authoring modal for `Clic -> Changer de page`, persisted as scene actions plus Element+ event bindings.
16. Element+ buttons have default hover metadata, a `Bouton` properties tab for hover/disabled configuration, save/reload persistence, FT100 manifest export for FT100Web consumption, and scoped FT100 CSS hover generation when enabled.
17. FT100 export preserves `Clic -> Changer de page` events carried by Element+ groups through transparent runtime wrappers with page-scoped `data-scada-events`.
18. FT100 export emits default page-scoped pointer cursor CSS for Element+ buttons and any exported target carrying `data-scada-events`, including descendants and active click state.
19. TF100Web tag exports using schema `tf100web-scada-tags-v1` can be imported into the project catalog, persisted through save/reload, and snapshotted under `imports/tags`.
20. Element+ value bindings can author the binding events `Lire valeur` and `Ecrire valeur` against enabled imported tags. The editor stores `ReadTagId` and `WriteTagId`, disables user-trigger selection for those functions, shows tag labels as `Nom du tag | datatype | Nom de l'appareil`, validates write bindings during build/export, and FT100 export emits tag catalog metadata plus read/write value runtime hooks.
21. The editor exposes a project-level `Catalogue Tags` panel listing imported tags and records local tag creation as a future protocol-import revision. The panel can filter by text, device, datatype, access, and active state, and reports the visible subset against the full imported catalog.
22. Element+ events can author `Afficher objet`, `Masquer objet`, and `Basculer visibilite` against Element+ targets, with one optional imported-tag condition using `Vrai`, `Faux`, `=`, `<>`, `>`, `>=`, `<`, or `<=`. Build/export validation rejects invalid condition tags, missing comparison values, missing target objects, and boolean operators on non-boolean tags.
23. FT100/TF100Web runtime can apply pushed tag values to all `Lire valeur` Element+ bindings through `window.scadaBuilderSetTagValue` or `scada-builder-tag-value`, while updating the shared runtime tag cache used by conditions.
24. Element+ events can author `Ouvrir popup`, `Fermer popup`, and `Basculer popup` against compiled `Fragment` pages. Build/export validation rejects invalid popup targets, and FT100/TF100Web runtime opens, closes, or toggles the fragment in a centered iframe popup with close diagnostics and iframe-to-parent close/toggle requests.
25. Element+ events can author `Afficher bordure`, `Masquer bordure`, and `Basculer bordure` against Element+ targets. Build/export validation rejects missing targets, and FT100/TF100Web runtime adds, removes, or toggles the standard page-scoped border class.
26. Popup actions can persist `ScadaPopupOptions` for position, size preset, multi-instance behavior, iframe reset policy, and Element+ host-region placement. Build/export validation rejects missing host-region targets.
27. Runtime actions can persist compound condition groups using `All` or `Any` evaluation plus explicit `BlockAction` or `AllowAction` policy when a required tag value is unavailable at runtime.
28. Exported pages expose `window.scadaBuilderRuntime` and emit lifecycle events for page ready, action executed, and runtime errors.
29. Standard runtime visual effects can start, stop, or toggle blink, glow, pulse, alarm highlight, and degraded treatment classes on Element+ targets.
30. TF100Web intake parity is not identical to exporter coverage. In `F:\Projet\Git\TF100Web` commit `3c795c2`, TF100Web extracts root fragments and executes host-side navigation plus `.sb2` `ValueBindings` mapping refresh/write logic; it does not execute SCADA Builder page scripts emitted outside the root fragment.
31. FT100 `.sb2` archive export generates the normalized package in staging, rewrites legacy source ids under the page namespace to prevent DOM collisions, validates TF100Web intake compatibility and page namespace rules, then writes a `.sb2` ZIP archive with `scada-builder-v2-ft100-package/` as the archive root.
32. FT100 `.sb2` export accepts indented page-scoped CSS id selectors during validation, reports destination/preparation/source/compression phases in the status bar, shows an indeterminate bottom-right status progress bar while running, guards against concurrent `.sb2` clicks, and performs archive generation off the WPF UI thread.
33. TF100Web `.sb2` intake resolves SCADA Builder V2 `ValueBindings.ReadTagId` and `ValueBindings.WriteTagId` shaped as `tf100.mapping.<id>` into host runtime mapping attributes. Page-scoped Element+ DOM ids are matched from manifest model ids, and separate read/write mappings are supported through `data-scada-write-mapping-id`.
34. The Element+ `Donnees` tab now treats `Format affichage` as the active numeric display signal. Legacy `Mapping / Tag`, `Decimales`, and `Unite` controls are removed from visible authoring, while legacy model fields remain preserved for compatibility. `Min` and `Max` are enabled only for non-read-only numeric inputs.
35. FT100 `.sb2` manifests export `InputNumeric` display metadata under `Objects[].Data`, including `DisplayFormat`, `IsReadOnly`, `Min`, and `Max`, so TF100Web does not have to infer numeric masks from page text for newly compiled packages.
36. TF100Web interprets SCADA Builder V2 `DisplayFormat` hash masks made of `#` plus an optional decimal point. It aligns runtime formatting with TF100Web `RegisterMapping.DataType`: `FLOAT32` and `FLOAT64` round directly, explicit integer datatypes scale by the mask decimal count, and unknown datatypes use direct rounding. For example, `39.599998474121094` with `###.#` and `FLOAT32` displays as `39.6`.
37. The insert ribbon can create standard Element+ shapes and Element+ buttons directly in the scene. Standard shapes persist `ShapeKind` for rectangle, rounded rectangle, ellipse, line, and arrow; the WebView preview and FT100 export render them as Element+-owned SVG content with style-backed fill, stroke, border width, and dashed/dotted treatment.
38. The insert ribbon can create HMI Element+ shapes for `IndicatorLamp`, `HorizontalBar`, and `VerticalBar`. Lamp and bar shapes persist through `ShapeKind`; bar shapes use `Data.Value` as a clamped 0-100 percentage in preview and FT100 export.
39. The insert ribbon can create process HMI Element+ shapes for `Tank`, `PipeHorizontal`, `PipeVertical`, `Valve`, and `Pump`. These primitives persist through `ShapeKind`; tank shapes use `Data.Value` as a clamped 0-100 percentage in preview and FT100 export.
40. The insert ribbon can create HMI Element+ button presets for `Command`, `Toggle`, `Navigation`, `AlarmAcknowledge`, and `EmergencyStop`. Button presets persist through `ButtonKind`, provide initial size/text/style, and export `ButtonKind` in the FT100 manifest plus `data-scada-button-kind` in generated HTML.
41. The insert ribbon can create machine and measurement HMI Element+ symbols for `Motor`, `Fan`, `Conveyor`, and `Gauge`. These primitives persist through `ShapeKind`; gauge symbols use `Data.Value` as a clamped 0-100 percentage in preview and FT100 export.
42. The `Style` tab can author model-backed `Opacity` and `Rotation` for Element+ objects. Preview and FT100 export apply `opacity` and `rotate(...deg)`, save/reload preserves them, and `AdvancedCss` remains a later override point.
43. The insert ribbon can create electrical and alarm HMI Element+ symbols for `Switch`, `Breaker`, `Transformer`, and `AlarmBeacon`. These primitives persist through `ShapeKind` and render as Element+-owned SVG content in preview and FT100 export.
44. Element+ buttons can author model-backed pressed/active styling in the `Bouton` tab. FT100 export preserves `ButtonBehavior.Pressed` in manifests and emits page-scoped `:active` plus active toggle-state CSS when enabled.
45. Element+ Toggle buttons export their on/off runtime state on the page-scoped Element+ wrapper. The exported page runtime toggles `data-scada-toggle-state` between `off` and `on` on click, drives active toggle styling, and emits `scada-builder-toggle-state-changed`.
46. Element+ disabled buttons now export native disabled button state, wrapper disabled metadata, not-allowed cursor treatment, suppressed hover/pressed/toggle behavior, and a runtime action guard for object-owned events.
47. Element+ standard buttons now emit runtime activation diagnostics. Enabled `Command`, `Navigation`, `AlarmAcknowledge`, `EmergencyStop`, and `Toggle` wrappers dispatch `scada-builder-button-activated` plus a kind-specific event for TF100Web or host runtime integration.
48. The HMI button block is closed with preview/export metadata parity: SCADA Builder V2 preview wrappers now expose the same button kind, behavior, disabled, and Toggle-state metadata expected by FT100 export while preserving editor-only non-interactive preview behavior.
49. The WPF shell top ribbon now marks the active tab, groups visible commands by task family, exposes horizontal overflow for long command families, standardizes visible labels in French, disables placeholder commands with explanatory tooltips, and replaces insert-ribbon text glyphs with semantic icon keys from `Icons.xaml`.
50. The WPF shell top ribbon now renders the active tab from a command registry containing stable command ids, labels, tooltips or disabled reasons, icon keys, executable state, and grouped command metadata. Implemented commands dispatch through the registry to existing shell workflows; future commands remain registered but disabled.

## 3. Source Of Truth

This file summarizes implemented behavior. Owner contracts remain under `03_runtime_contracts`, `04_editor`, and `05_studio_element_plus`.
