# SCADA Builder V2 - Export Flow Diagram

Date: 2026-06-17
Status: Generated baseline
Document version: `V2.1.2.0018`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-17 | `V2.1.2.0018` | `ad364a6` | Ajout du chemin d'intake fragment TF100Web audite. |
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
  Package --> TfManifest[TF100Web reads root manifest]
  TfManifest --> TfPages[Compiled Pages entries]
  TfPages --> TfFragment[Extract div id ft100-page-id]
  TfPages --> TfCss[Load page sibling CSS]
  TfFragment --> TfHost[TF100Web visualisation host runtime]
  TfCss --> TfHost
  Html -. exported script outside extracted root not executed .-> TfGap[Runtime parity gap]
```
