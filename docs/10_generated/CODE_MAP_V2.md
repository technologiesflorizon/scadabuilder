# SCADA Builder V2 - Code Map

Date: 2026-07-15
Status: Generated baseline; DEC-0040 and DEC-0050 Phase 2 ownership verified
Document version: `V2.1.5.0022`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-08-13 | `V2.1.5.0022` | `PENDING` | Ajout des propriétaires QuickWindow Phase 2 : Application, historique workspace et validation build Domain. |
| 2026-07-15 | `V2.1.4.0027` | `88e865a` | Carte Tableau complétée avec inspecteur Application, view models App, tests bridge/performance/intégration et rendu sémantique. |
| 2026-07-15 | `V2.1.4.0026` | `0874416` | Ajout des proprietaires Tableau avance et verrouillage persistant Element+. |
| 2026-07-14 | `V2.1.2.0020` | `PENDING` | Ajout des propriétaires identité, commandes, workspace, diagnostics, migration, persistance atomique et projection `.sb2` des pages. |
| 2026-06-17 | `V2.1.2.0019` | `bd6515e` | Ajout de la responsabilite export `.sb2` et validation FT100 au module Rendering. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation de la carte code initiale pour verification documentaire. |

## 1. Modules

| Module | Responsibility |
| --- | --- |
| `src/ScadaBuilderV2.Domain` | Durable project, scene, element, versioning, and pure domain rules |
| `src/ScadaBuilderV2.Application` | Async command registry, `PageCommandCoordinator`, dependency analysis, project/scene history, conversion, selection models, Studio factories |
| `src/ScadaBuilderV2.Infrastructure` | Page identity migration, atomic workspace persistence, legacy readers, package IO, adapters |
| `src/ScadaBuilderV2.Rendering` | Native/imported preview/export rendering, `PageKey` to `PageCode` projection, FT100 package generation, `.sb2` packaging and validation |
| `src/ScadaBuilderV2.App` | WPF shell/WebView bridge, `PageWorkspaceController`, Pages surfaces, page properties adapter, Diagnostics surfaces |
| `src/ScadaBuilderV2.ElementStudio.App` | Studio Element+ WPF application |
| `tests/ScadaBuilderV2.Tests` | Regression coverage |

## 2. Page Management Owners

| Area | Owner |
| --- | --- |
| Durable identity/provenance | `Domain/Projects/ProjectModels.cs` |
| Lifecycle/property rules | `Application/Pages/PageCommandCoordinator.cs` |
| Shared command adapters | `Application/Commands/Pages/` |
| Dependency diagnostics | `Application/Pages/PageDependencyAnalyzer.cs` |
| Atomic project/scenes/deletions | `Infrastructure/ModernProjects/ModernProjectStore.cs` |
| Runtime identity compatibility | `Rendering/PageRuntimeIdentityResolver.cs` |
| Open tabs and WPF workspace | `App/Pages/PageWorkspaceController.cs` |
| Ribbon/project/context UI | `App/Pages/`, `App/MainWindow.xaml` |
| Blocking and retained diagnostics | `App/Diagnostics/` |

## 3. DEC-0040 Owners

| Area | Owner |
| --- | --- |
| Table persistent model and pure operations | `Domain/Scenes/Tables/` |
| Element position-lock closure | `Domain/Scenes/ScadaSceneElementLockOperations.cs` |
| Table session, ribbon state, format inspection and typed edits | `Application/Tables/` (`TablePropertiesInspector`, `TableEditCoordinator`) |
| Lock aggregation, command, guard and history | `Application/Selection/`, `Application/Commands/ToggleElementLockCommand.cs`, `Application/History/ElementLockChangedAction.cs` |
| WPF Table surfaces and typed WebView bridge | `App/TableEditor/` (`TableRibbonViewModel`, `TablePropertiesViewModel`, adapter/script), `App/MainWindow.TableIntegration.cs` |
| Shared Table runtime rendering | `Rendering/ModernTableHtmlRenderer.cs` |

## 4. DEC-0050 Phase 2 Owners

| Area | Owner |
| --- | --- |
| Definition/invocation orchestration | `Application/QuickWindows/QuickWindowDefinitionService.cs`, `QuickWindowInvocationService.cs` |
| Usage, reference, cycle and depth analysis | `Application/QuickWindows/QuickWindowDependencyAnalyzer.cs` |
| Atomic workspace undo/redo | `Application/History/QuickWindowWorkspaceSnapshotAction.cs`, `ProjectWorkspaceSnapshotAction.cs`, `EditorHistoryService.cs` |
| Build/export fail-closed validation | `Domain/Projects/ProjectModels.cs` (`ScadaProjectBuildValidator`) |
| Regression evidence | `Tests/QuickWindows/QuickWindowApplicationTests.cs`, `QuickWindowHistoryTests.cs`, `QuickWindowBuildValidationTests.cs` |

## 5. Verification

Run:

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
```
