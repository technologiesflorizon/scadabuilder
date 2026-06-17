# SCADA Builder V2 - Implemented Features

Date: 2026-06-17
Status: Active implementation status
Document version: `V2.1.2.0008`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
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

As of 2026-06-17, `dotnet test ScadaBuilderV2.sln --no-restore` passes with 199 tests.

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
20. Element+ events can author `WriteTag` actions against enabled writeable imported tags with a literal value, and FT100 export emits the tag catalog plus the write-tag runtime hook.

## 3. Source Of Truth

This file summarizes implemented behavior. Owner contracts remain under `03_runtime_contracts`, `04_editor`, and `05_studio_element_plus`.
