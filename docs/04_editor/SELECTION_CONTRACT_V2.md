# SCADA Builder V2 - Selection Contract

Date: 2026-07-15
Status: Active editor selection contract
Document version: `V2.1.4.0034`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-15 | `V2.1.4.0034` | `b75f1d7` | Verrou projete dans le payload editor-only avant hit-testing; mode Tableau Objet/Cellules deterministe et gestes internes disponibles sans translation. |
| 2026-07-15 | `V2.1.4.0030` | `5d762bb` | Verrou applique avant tout preview de deplacement et priorite des gestes internes Tableau en mode Cellules. |
| 2026-07-15 | `V2.1.4.0026` | `0874416` | Separation du verrou de position et de la selection; ajout des portees cellule/plage/rangee/colonne propres au Tableau. |
| 2026-06-16 | `V2.1.2.0003` | `PENDING` | Clarification que le deplacement normal d'un enfant de groupe Element+ cible son groupe parent. |
| 2026-06-16 | `V2.1.2.0002` | `PENDING` | Clarification que le groupement de scene consomme uniquement la selection Element+ moderne. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du contrat actif de selection SCADA Builder V2. |

## 1. Contract

SCADA Builder V2 selection is polymorphic.

The editor can select:

1. Imported source DOM nodes with `data-id`.
2. Managed source projections.
3. SVG source shapes with `data-id`.
4. Modern Element+ scene objects.
5. Groups according to scene object rules.

## 2. Rules

1. Selection scope cannot be narrowed to materialization inventory.
2. Present source nodes must remain selectable unless hidden, locked, or editor-only by contract.
3. Commands resolve selected target type after hit-testing.
4. Durable source deletion uses `RemovedSourceElementIds`.
5. CSS masking, `display:none`, WebView-only flags, and inventory omission are not durable delete state.
6. Undo/redo and export omission must use the same scene state.
7. A source/legacy selection can be converted, moved, hidden, deleted, or opened in Studio Element+ according to command contracts, but it cannot be grouped directly in the scene.
8. A scene group can be created only from two or more selected Element+ scene object ids.
9. In normal scene movement, a selected child inside an Element+ group is normalized to the containing group so grouped objects move together.
10. `IsLocked` n'empeche jamais de selectionner un Element+; il bloque seulement les mutations qui changeraient effectivement X ou Y.
11. En mode Tableau Cellules, la selection interne est une plage distincte de la selection de scene. Les headers de rangee/colonne et le coin produisent une portee explicite sans devenir des Element+.
12. Un Element+ verrouille, un groupe contenant un descendant verrouille ou une multiselection contenant un verrou ne produit aucun preview de deplacement; la geometrie reste immobile des le pointer down.
13. En mode Tableau Cellules, le hit-testing interne intercepte clic, double-clic, clic droit et resize de piste avant les gestes de selection/deplacement de l'Element+ conteneur.

## 3. Related Decision

1. `DEC-0006` - Polymorphic Selection And Durable Source Delete.
2. `DEC-0010` - Scene Grouping Is Element+ Only.
3. `DEC-0040` - Advanced Table Authoring And Persistent Element Position Lock.
4. `DEC-0041` - Deterministic Table Interaction And Immediate Element Position Lock.

## 4. Related Tests

1. `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`
2. `tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs`
3. `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
4. `tests/ScadaBuilderV2.Tests/ScadaSceneGroupTests.cs`
5. `tests/ScadaBuilderV2.Tests/ModernElementRenderPayloadFactoryTests.cs`
6. `tests/ScadaBuilderV2.Tests/TableEditorWebViewStateTests.cs`
