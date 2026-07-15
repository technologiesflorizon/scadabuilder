# SCADA Builder V2 - State Flow Diagram

Date: 2026-07-15
Status: Generated baseline with project workspace and Table editor state
Document version: `V2.1.4.0034`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-15 | `V2.1.4.0034` | `b75f1d7` | Ajout des transitions atomiques Objet/Cellules et de la visibilite effective des reperes A/1. |
| 2026-07-14 | `V2.1.1.0040` | `PENDING` | Ajout du dirty state, undo/redo projet, suppressions en attente et sauvegarde atomique des pages. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du diagramme de flow etat. |

```mermaid
stateDiagram-v2
  [*] --> NoProject
  NoProject --> ProjectOpen
  ProjectOpen --> SceneActive
  SceneActive --> SelectionActive
  SelectionActive --> SceneActive: clear selection
  SelectionActive --> TableObject: select or reselect Table
  TableObject --> TableCellsGuides: Cellules / double-click / Afficher A/1
  TableCellsGuides --> TableCellsHidden: Masquer A/1
  TableCellsHidden --> TableCellsGuides: Afficher A/1
  TableCellsGuides --> TableObject: Objet / Escape / select another Table
  TableCellsHidden --> TableObject: Objet / Escape / select another Table
  TableObject --> SelectionActive: select non-Table Element+
  SceneActive --> Dirty: mutation
  SceneActive --> ProjectDirty: page.* mutation
  ProjectDirty --> ProjectUndoAvailable: project history push
  ProjectUndoAvailable --> ProjectRedoAvailable: undo
  ProjectRedoAvailable --> ProjectUndoAvailable: redo
  ProjectDirty --> SavingAtomic: save project + scenes + deletions
  SavingAtomic --> ProjectOpen: commit succeeds
  SavingAtomic --> ProjectDirty: rollback/recovery on failure
  Dirty --> ProjectOpen: scene save
```

Le verrou de position est orthogonal a ces etats : il bloque les transitions de geometrie qui changeraient X/Y avant preview, sans forcer Objet/Cellules ni masquer les outils internes du Tableau.
