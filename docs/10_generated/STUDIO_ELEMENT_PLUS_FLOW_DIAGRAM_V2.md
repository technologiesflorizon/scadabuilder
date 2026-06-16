# SCADA Builder V2 - Studio Element+ Flow Diagram

Date: 2026-06-16
Status: Generated baseline
Document version: `V2.1.1.0039`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du diagramme de flow Studio Element+. |

```mermaid
flowchart TD
  SourceSelection[Source selection] --> Ft1[.ft1 transfer]
  Ft1 --> Studio[Studio Element+]
  Studio --> Edit[Edit component source]
  Edit --> Sep[.sep package]
  Sep --> Library[Component library]
  Library --> Scene[SCADA Builder scene placement]
```
