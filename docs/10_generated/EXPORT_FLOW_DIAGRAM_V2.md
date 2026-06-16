# SCADA Builder V2 - Export Flow Diagram

Date: 2026-06-16
Status: Generated baseline
Document version: `V2.1.1.0039`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du diagramme de flow export. |

```mermaid
flowchart TD
  Project[V2 project] --> Scenes[Compiled scenes]
  Scenes --> Manifest[root manifest.json]
  Scenes --> Pages[page folders]
  Pages --> Html[page-id.html]
  Pages --> Css[css/page-id.css]
  Pages --> Images[images]
  Manifest --> Package[scada-builder-v2-ft100-package]
  Html --> Package
  Css --> Package
  Images --> Package
```
