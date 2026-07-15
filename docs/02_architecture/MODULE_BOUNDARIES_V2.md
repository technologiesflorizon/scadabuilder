# SCADA Builder V2 - Module Boundaries

Date: 2026-06-16
Status: Active module boundary contract
Document version: `V2.1.4.0027`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-15 | `V2.1.4.0027` | `88e865a` | Frontière avancée complétée avec `TablePropertiesInspector` en Application et `TableRibbonViewModel`/`TablePropertiesViewModel` en App; `MainWindow` conserve seulement l'adaptation workspace/scene. |
| 2026-07-14 | `V2.1.4.0016` | `10cfa72` | Frontiere Tableau explicitee : regles Domain, coordination/clipboard/catalogue Application, rendu Rendering et adaptation WPF/WebView App hors `MainWindow`. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation de la matrice de responsabilites par module logiciel. |

## 1. Boundary Matrix

| Module | Owns | Must not own |
| --- | --- | --- |
| Domain | Durable model and pure rules | WPF controls, WebView DOM, file dialogs |
| Application | Commands, history, conversion, Studio workflows | Runtime HTML details, UI styling |
| Infrastructure | Persistence and adapters | Business decisions, UI commands |
| Rendering | Preview/export output | Editor selection state ownership |
| App | User surfaces and WebView bridge | Durable contracts without commands/domain |
| ElementStudio.App | Studio UI and source editing surface | SCADA project persistence |
| Tests | Regression contracts | Production behavior |

Pour l'Element+ Tableau, `MainWindow` ne possède que le branchement au workspace actif et le guard de transformation dépendant de la scène. `ScadaTableOperations`, `ScadaTableFormatOperations` et `ScadaTableStyleResolver` appartiennent au Domain; `TableEditCoordinator`, `TablePropertiesInspector`, `TableClipboard`, `TableContextMenuProvider` et `InsertToolCatalog` appartiennent à Application; le HTML/CSS exporté appartient à Rendering; `TableRibbonViewModel`, `TablePropertiesViewModel`, les dialogues, l'adaptateur JSON et le script WebView appartiennent aux composants dédiés de App.

## 2. Dependency Direction

```mermaid
flowchart TD
  App --> Application
  ElementStudioApp[ElementStudio.App] --> Application
  Application --> Domain
  Infrastructure --> Domain
  Rendering --> Domain
  App --> Infrastructure
  App --> Rendering
  Tests --> App
  Tests --> Application
  Tests --> Domain
  Tests --> Infrastructure
  Tests --> Rendering
```
