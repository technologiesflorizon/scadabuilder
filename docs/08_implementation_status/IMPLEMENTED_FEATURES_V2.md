# SCADA Builder V2 - Implemented Features

Date: 2026-06-16
Status: Active implementation status
Document version: `V2.1.2.0002`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-16 | `V2.1.2.0002` | `PENDING` | Implementation du groupement de scene Element+ only et de l'avertissement conversion pour les selections legacy. |
| 2026-06-16 | `V2.1.2.0001` | `PENDING` | Correction du raccourci Backspace pour les Element+ selectionnes et protection des champs editables contre les raccourcis scene. |
| 2026-06-16 | `V2.1.2.0000` | `PENDING` | Implementation de la conversion Button plausible, du choix Propriete contextualise et du rendu/export du texte des boutons Element+. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du registre des fonctionnalites implementees. |

## 1. Current Verified Baseline

As of 2026-06-16, `dotnet test ScadaBuilderV2.sln --no-restore` passes with 181 tests.

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

## 3. Source Of Truth

This file summarizes implemented behavior. Owner contracts remain under `03_runtime_contracts`, `04_editor`, and `05_studio_element_plus`.
