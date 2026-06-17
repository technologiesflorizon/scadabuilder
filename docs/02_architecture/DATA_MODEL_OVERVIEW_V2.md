# SCADA Builder V2 - Data Model Overview

Date: 2026-06-17
Status: Active data model overview
Document version: `V2.1.2.0010`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-17 | `V2.1.2.0010` | `PENDING` | Ajout de la relation conditionnelle entre action runtime et tag importe. |
| 2026-06-17 | `V2.1.2.0009` | `PENDING` | Ajout des relations `ReadTagId` et `WriteTagId` sur les donnees Element+. |
| 2026-06-17 | `V2.1.2.0008` | `PENDING` | Ajout du catalogue tags TF100Web au modele projet et aux relations runtime. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation de la vue d'ensemble du modele projet, scene, elements, actions, Studio et export. |

## 1. Model Families

1. Project model: project identity, scene inventory, home page, build inclusion, page composition, and imported TF100Web tag catalog.
2. Scene model: canvas, page type, background, elements, actions, removed source ids, and composition references.
3. Element model: source projections, modern Element+ objects, groups, bindings, events, and geometry.
4. Studio model: `.ft1` transfer package and `.sep` reusable component package.
5. Runtime package model: root manifest, page manifests, page HTML, CSS, images, and runtime action metadata.

## 2. Relationship Diagram

```mermaid
classDiagram
  class ScadaProject {
    Id
    ManifestVersion
    HomePageId
    Scenes
    TagCatalog
  }
  class ScadaTagCatalog {
    Schema
    Tags
  }
  class ScadaTagDefinition {
    Id
    DisplayName
    Device
    AddressUri
    Writeable
  }
  class ScadaScene {
    Id
    PageType
    CanvasSize
    Elements
    Actions
    RemovedSourceElementIds
  }
  class ScadaElement {
    Id
    Kind
    Bounds
    ReadTagId
    WriteTagId
    Events
  }
  class ScadaActionDefinition {
    Id
    Kind
    Target
    Condition
  }
  class ScadaActionCondition {
    TagId
    Operator
    CompareValue
  }
  class SepComponent {
    ComponentId
    SourceTrace
    Geometry
  }
  ScadaProject "1" --> "*" ScadaScene
  ScadaProject "1" --> "0..1" ScadaTagCatalog
  ScadaTagCatalog "1" --> "*" ScadaTagDefinition
  ScadaScene "1" --> "*" ScadaElement
  ScadaScene "1" --> "*" ScadaActionDefinition
  ScadaElement "1" --> "*" ScadaActionDefinition : references
  ScadaActionDefinition "0..1" --> "0..1" ScadaActionCondition
  ScadaActionCondition "*" --> "1" ScadaTagDefinition : TagId
  ScadaElement "*" --> "0..1" ScadaTagDefinition : ReadTagId
  ScadaElement "*" --> "0..1" ScadaTagDefinition : WriteTagId
  ScadaElement "*" --> "0..1" SepComponent : published from
```
