# SCADA Builder V2 - Modernization Workflow

Date: 2026-06-16
Status: Active modernization workflow pointer
Document version: `V2.1.1.0039`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du nouveau document proprietaire du workflow de modernisation legacy. |

## 1. Workflow

```mermaid
flowchart TD
  Inspect[Inspect legacy source] --> Compare[Compare visual baseline]
  Compare --> Extract[Extract source candidates]
  Extract --> Model[Create or update V2 model]
  Model --> Preview[Preview parity]
  Preview --> Studio[Optional Studio Element+ modernization]
  Preview --> Export[FT100/TF100Web export]
  Studio --> Export
  Export --> Regression[Regression validation]
```

## 2. Migration Note

Detailed historical content is archived in `docs/09_archive/deprecated/LEGACY_MODERNIZATION_WORKFLOW_V2.md`.
