# SCADA Builder V2 - Command Flow Diagram

Date: 2026-06-16
Status: Generated baseline
Document version: `V2.1.2.0002`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-16 | `V2.1.2.0002` | `PENDING` | Ajout du flux de groupement Element+ only et avertissement legacy. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du diagramme de flow commandes. |

```mermaid
sequenceDiagram
  participant UI as UI Surface
  participant Registry as Command Registry
  participant Context as Command Context
  participant Handler as Handler
  participant Model as Model
  participant History as History
  UI->>Registry: command id
  Registry->>Context: resolve state
  Context->>Handler: execute
  alt object.group with 2+ Element+ selected
    Handler->>Model: WithGroupedElements(groupId, name, selected ids)
    Handler->>History: SceneSnapshotChangedAction
  else legacy/source group attempt
    Handler-->>UI: warn convert to Element+ first
  else other command
    Handler->>Model: mutate or validate
  end
  Handler-->>UI: result
```
