# SCADA Builder V2 - State Flow Diagram

Date: 2026-07-14
Status: Generated baseline with project workspace state
Document version: `V2.1.1.0040`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-14 | `V2.1.1.0040` | `PENDING` | Ajout du dirty state, undo/redo projet, suppressions en attente et sauvegarde atomique des pages. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du diagramme de flow etat. |

```mermaid
stateDiagram-v2
  [*] --> NoProject
  NoProject --> ProjectOpen
  ProjectOpen --> SceneActive
  SceneActive --> SelectionActive
  SelectionActive --> SceneActive: clear selection
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
