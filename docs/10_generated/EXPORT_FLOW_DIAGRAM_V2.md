# SCADA Builder V2 - Export Flow Diagram

Date: 2026-07-16
Status: Generated baseline
Document version: `V2.1.4.0043`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-16 | `V2.1.4.0043` | `8489dbd` | Ajout du deploiement du runtime package partage et de l'initialisation Etat/Commande sur les fragments TF100Web. |
| 2026-07-15 | `V2.1.4.0027` | `88e865a` | Ajout du chemin Tableau model-backed vers HTML sémantique partagé preview/`.sb2`. |
| 2026-06-17 | `V2.1.2.0019` | `bd6515e` | Ajout de l'archive `.sb2` apres validation de staging. |
| 2026-06-17 | `V2.1.2.0018` | `ad364a6` | Ajout du chemin d'intake fragment TF100Web audite. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du diagramme de flow export. |

```mermaid
flowchart TD
  Project[V2 project] --> Scenes[Compiled scenes]
  Scenes --> Tables[Table model: tracks, cells, headers, border segments]
  Tables --> TableHtml[Semantic tr/th/td plus physical CSS borders]
  Scenes --> Manifest[root manifest.json]
  Scenes --> Pages[page folders]
  Pages --> Html[page-id.html]
  Pages --> Css[css/page-id.css]
  Pages --> Images[images]
  Exporter --> Runtime[scada-runtime.hash.js]
  Manifest --> Package[scada-builder-v2-ft100-package]
  Html --> Package
  TableHtml --> Html
  Css --> Package
  Images --> Package
  Package --> Validate[SCADA Builder validates TF100Web intake and namespace]
  Validate --> Sb2[.sb2 zip archive]
  Package --> TfManifest[TF100Web reads root manifest]
  Sb2 --> TfManifest
  TfManifest --> TfPages[Compiled Pages entries]
  TfPages --> TfFragment[Extract div id ft100-page-id]
  TfPages --> TfCss[Load page sibling CSS]
  TfFragment --> TfHost[TF100Web visualisation host runtime]
  TfCss --> TfHost
  Runtime --> TfRuntime[static scada js scada-runtime.js]
  TfRuntime --> TfHost
  TfHost --> Init[Initialize composed fragments]
  Init --> StateCommand[Shared StateEngine and CommandDispatcher]
  Html -. inline scripts outside extracted root not executed .-> TfGap[Legacy action parity gap]
```
