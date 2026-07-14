# SCADA Builder V2 - Command Flow Diagram

Date: 2026-07-14
Status: Generated baseline with implemented page command flow
Document version: `V2.1.2.0003`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-14 | `V2.1.2.0003` | `PENDING` | Ajout du flux asynchrone partagé des commandes de page et des diagnostics. |
| 2026-06-16 | `V2.1.2.0002` | `PENDING` | Ajout du flux de groupement Element+ only et avertissement legacy. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du diagramme de flow commandes. |

```mermaid
sequenceDiagram
  participant UI as UI Surface
  participant Registry as Command Registry
  participant Context as Command Context
  participant Handler as Handler
  participant Coordinator as Page Coordinator
  participant Model as Model
  participant History as History
  participant Diagnostics as Diagnostics
  UI->>Registry: command id
  Registry->>Context: resolve state
  Context->>Handler: execute
  alt page.* command
    Handler->>Coordinator: typed PageCommandRequest
    Coordinator->>Model: produce PageWorkspaceMutation
    Coordinator->>History: project-scoped snapshot action
    Coordinator-->>Diagnostics: structured issues
    Coordinator-->>UI: result + selection/open routing
  else object.group with 2+ Element+ selected
    Handler->>Model: WithGroupedElements(groupId, name, selected ids)
    Handler->>History: SceneSnapshotChangedAction
  else legacy/source group attempt
    Handler-->>UI: warn convert to Element+ first
  else other command
    Handler->>Model: mutate or validate
  end
  Handler-->>UI: result
```
