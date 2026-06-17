# SCADA Builder V2 - Implemented Features

Date: 2026-06-17
Status: Active implementation status
Document version: `V2.1.2.0017`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
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

As of 2026-06-17, `dotnet test ScadaBuilderV2.sln --no-restore` passes with 225 tests.

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
20. Element+ value bindings can author `Lire valeur` and `Ecrire valeur` against enabled imported tags. The editor stores `ReadTagId` and `WriteTagId`, disables event triggers for those functions, shows tag labels as `Nom du tag | datatype | Nom de l'appareil`, validates write bindings during build/export, and FT100 export emits tag catalog metadata plus read/write value runtime hooks.
21. The editor exposes a project-level `Catalogue Tags` panel listing imported tags and records local tag creation as a future protocol-import revision. The panel can filter by text, device, datatype, access, and active state, and reports the visible subset against the full imported catalog.
22. Element+ events can author `Afficher objet`, `Masquer objet`, and `Basculer visibilite` against Element+ targets, with one optional imported-tag condition using `Vrai`, `Faux`, `=`, `<>`, `>`, `>=`, `<`, or `<=`. Build/export validation rejects invalid condition tags, missing comparison values, missing target objects, and boolean operators on non-boolean tags.
23. FT100/TF100Web runtime can apply pushed tag values to all `Lire valeur` Element+ bindings through `window.scadaBuilderSetTagValue` or `scada-builder-tag-value`, while updating the shared runtime tag cache used by conditions.
24. Element+ events can author `Ouvrir popup`, `Fermer popup`, and `Basculer popup` against compiled `Fragment` pages. Build/export validation rejects invalid popup targets, and FT100/TF100Web runtime opens, closes, or toggles the fragment in a centered iframe popup with close diagnostics and iframe-to-parent close/toggle requests.
25. Element+ events can author `Afficher bordure`, `Masquer bordure`, and `Basculer bordure` against Element+ targets. Build/export validation rejects missing targets, and FT100/TF100Web runtime adds, removes, or toggles the standard page-scoped border class.
26. Popup actions can persist `ScadaPopupOptions` for position, size preset, multi-instance behavior, iframe reset policy, and Element+ host-region placement. Build/export validation rejects missing host-region targets.
27. Runtime actions can persist compound condition groups using `All` or `Any` evaluation plus explicit `BlockAction` or `AllowAction` policy when a required tag value is unavailable at runtime.
28. Exported pages expose `window.scadaBuilderRuntime` and emit lifecycle events for page ready, action executed, and runtime errors.

## 3. Source Of Truth

This file summarizes implemented behavior. Owner contracts remain under `03_runtime_contracts`, `04_editor`, and `05_studio_element_plus`.
