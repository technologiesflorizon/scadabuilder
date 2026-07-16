# SCADA Builder V2 - Regression Coverage

Date: 2026-07-16
Status: Active regression coverage map
Document version: `V2.1.4.0058`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-16 | `V2.1.4.0058` | TF100Web `9e85844` | 17 tests composition/deploiement/performance verts; single-pass, revisions, generation et rollback couverts. |
| 2026-07-16 | `V2.1.4.0057` | TF100Web `c304af3` | 23 tests JS et 20 contrats Django verts; bindings read/write, edit, formats/datatype et mapping absent couverts. |
| 2026-07-16 | `V2.1.4.0056` | TF100Web `1fc3ac4` | 16 tests JS host/lifecycle/hydration et 19 tests contrat Django verts; ordre inverse, timeout, reprise et forced poll couverts. |
| 2026-07-16 | `V2.1.4.0055` | TF100Web `cab2733` | 7 tests JS HostAdapter et 16 tests contrat runtime Django verts : intents, origine, doublons, stale scope, URL/ecriture/permissions. |
| 2026-07-16 | `V2.1.4.0054` | TF100Web `7d60c63` | 11 tests negotiation/fixture cibles verts; check Django local vert; suite module expose des echecs historiques hors tranche. |
| 2026-07-16 | `V2.1.4.0053` | `PENDING` | 53 tests runtime JS et 98 tests cibles verts; suite 679/684 avec cinq echecs historiques; fixture `fb06431e...08404`. |
| 2026-07-16 | `V2.1.4.0052` | `PENDING` | 47 tests runtime JS et 113 tests .NET cibles verts; suite 678/683, cinq echecs historiques; fixture `4381347c...40a6`. |
| 2026-07-16 | `V2.1.4.0051` | `PENDING` | 35 tests runtime JS et 113 tests .NET cibles verts; suite complete 678/683, cinq echecs historiques; hash fixture `6976e192...15ef`. |
| 2026-07-16 | `V2.1.4.0050` | `PENDING` | 3 tests de conformance et 94 tests cibles verts; package byte-identique et SHA-256 canonique verifies; suite complete 678/683, cinq echecs historiques. |
| 2026-07-16 | `V2.1.4.0049` | `PENDING` | 84 tests FT100 exporter/package verts : 2.3, requirements, hash, tamper, blocked et profils compatibles; suite complete 675/680, cinq echecs historiques. |
| 2026-07-16 | `V2.1.4.0048` | `PENDING` | Matrice de 162 capabilities generee depuis le code; `verify-docs` rejette staleness et support sans preuves trilaterales. |
| 2026-07-16 | `V2.1.4.0047` | `PENDING` | 7 regressions `RuntimeContracts` couvrent catalogue, exhaustivite enum/effet/AST, gaps bloques, analyse et deduplication. |
| 2026-07-16 | `V2.1.4.0046` | `PENDING` | Plan `DEC-0047` : matrice generee, tests d'exhaustivite, fixture `.sb2` partagee et preuves end-to-end requises pour chaque capability. |
| 2026-07-16 | `V2.1.4.0044` | `de37a35`, TF100Web `9d5d400` | Couverture `DEC-0045` : restauration fallback, overlay sous contenu, collecte mappings resolus, snapshot force et binding numerique commun idempotent. |
| 2026-07-16 | `V2.1.4.0043` | `8489dbd` | Ajout de la couverture `DEC-0044` pour les 56 toggles, le texte semantique exporte, les effets true/false et la collecte TF100Web des mappings de commande. |
| 2026-07-16 | `V2.1.4.0042` | `9fd2a30` | Regression `page.properties` : ouverture, activation et selection de la page cible sans mutation, dirty state ni historique; suite complete 659/664 avec cinq echecs historiques inchanges. |
| 2026-07-16 | `V2.1.4.0041` | `6afe427` | Couverture `DEC-0043` pour A1, provenance de selection, commande/dialogue unique, fallback Lire/Ecrire, double-clic, round-trip/export et smoke isole; suite complete 658/663 avec cinq echecs historiques inchanges. |
| 2026-07-15 | `V2.1.4.0039` | `PENDING` | Couverture `DEC-0042` Domain/Application/WPF/rendu/export/validation et intake TF100Web; suite SCADA 645/650 avec cinq echecs historiques inchanges. |
| 2026-07-15 | `V2.1.4.0035` | `740796e` | Regression du hit-testing cellule Tableau : guides A/1 externes, drag primaire explicite, annulation pointeur, normalisation des plages et rendu commun des scopes rangee/colonne. |
| 2026-07-15 | `V2.1.4.0034` | `b75f1d7` | Couverture `DEC-0041` du payload reel, etat Tableau atomique, refus avant preview, resize verrouille, guide A/1 et absence d'artefact export; smoke WPF/WebView2 isole reussi. |
| 2026-07-15 | `V2.1.4.0031` | `e127190` | Regression XAML du ruban secondaire : hauteur anti-clipping, scrollbar masquee et chevrons de pagination; 14 tests ruban reussis, suite complete inchangee a 614 reussites et 5 echecs historiques. |
| 2026-07-15 | `V2.1.4.0030` | `5d762bb` | Couverture verrou avant preview, gestes Tableau, reperes A/1, toggle fusion et format; suite complete observee a 614 reussites et 5 echecs historiques non lies. |
| 2026-07-15 | `V2.1.4.0029` | `bbca8fa` | Regression XAML du ruban secondaire compact : dimensions, disposition horizontale, troncature et groupes sur deux rangees. |
| 2026-07-15 | `V2.1.4.0028` | `c873744` | Régressions ciblées pour l'entrée Tableau sans `ShowDialog`, la visibilité des contrôles de verrou et l'action contextuelle Verrouiller/Déverrouiller. |
| 2026-07-15 | `V2.1.4.0027` | `32a3ef6` | Ajout des suites dédiées inspecteur, bridge, performance 64 x 64 et intégration `win00012`; suite complète observée à 608 réussites et 5 échecs historiques non liés. |
| 2026-07-15 | `V2.1.4.0026` | `0874416` | Couverture `DEC-0040` : lock/persistance/groupes/guard, session Tableau, conversions, formats, bordures, pistes/en-tetes, architecture WebView 64x64 et export sans artefact editeur. |
| 2026-07-14 | `V2.1.4.0018` | `858473c` | Couverture du layout type des dialogues Tableau et de l'affichage de leurs controles WPF concrets. |
| 2026-07-14 | `V2.1.4.0017` | `a94016a` | Ajout de la regression garantissant un niveau 1 Inserer compact, independant du style 58 px des commandes de niveau 2. |
| 2026-07-14 | `V2.1.4.0016` | `10cfa72` | Couverture Tableau : modele/limites/precedence, operations, coordinator, clipboard, menu, architecture hors MainWindow, persistance scene, rendu HTML et archive `.sb2`; couverture des huit familles Inserer. |
| 2026-07-14 | `V2.1.4.0008` | `PENDING` | Ajout de la couverture du clic droit sur une page lorsque la cible est un contenu inline WPF `Run`. |
| 2026-07-14 | `V2.1.4.0007` | `PENDING` | Couverture des dimensions et de la marge interne de l'icone Nouvelle page. |
| 2026-07-14 | `V2.1.4.0006` | `PENDING` | Couverture du libelle Recherche, des filtres initiaux Default/Tous et de l'icone partagee Nouvelle page. |
| 2026-07-14 | `V2.1.4.0005` | `PENDING` | Ajout de la regression interdisant les liaisons `Run.Text` TwoWay implicites vers les proprietes Pages et Diagnostics en lecture seule. |
| 2026-07-14 | `V2.1.4.0004` | `PENDING` | Ajout de la couverture identité, commandes, historique, sauvegarde atomique, pages natives, surfaces Pages/Diagnostics et cycle `.sb2` complet. |
| 2026-07-13 | `V2.1.4.0003` | `b954d46` | Couverture des nouveaux champs de style, export CSS, preview WebView, icônes sémantiques et preuve d’intake TF100Web. |
| 2026-07-06 | `V2.1.3.0002` | `4dfe7fe` | Ajout de la couverture de la poignée de rotation Element+ et des presets/angle personnalisé du menu contextuel (7 tests WebViewContextMenuScriptTests). |
| 2026-06-19 | `V2.1.3.0001` | `620e914` | Ajout de la couverture icon-only 32x32 pour la galerie Formes. |
| 2026-06-19 | `V2.1.3.0000` | `b195fe0` | Ajout de la couverture galerie Formes, formes standard completes, et placement Ligne/Fleche en deux points. |
| 2026-06-19 | `V2.1.2.0044` | `c50cbcf` | Ajout de la couverture de la palette laterale d'outils issue du catalogue semantique. |
| 2026-06-19 | `V2.1.2.0043` | `fde1b31` | Ajout de la couverture interdisant le retour des anciens rubans XAML statiques. |
| 2026-06-19 | `V2.1.2.0042` | `0825cfe` | Ajout de la couverture du dispatch de ruban pour `object.group` et `object.ungroup`. |
| 2026-06-19 | `V2.1.2.0038` | `6f76dc8` | Ajout de la couverture de parite metadata preview/export pour boutons Element+. |
| 2026-06-19 | `V2.1.2.0037` | `2a540d6` | Ajout de la couverture des evenements runtime pour boutons HMI standards. |
| 2026-06-19 | `V2.1.2.0036` | `8cc4d33` | Ajout de la couverture du runtime disabled reel pour boutons Element+. |
| 2026-06-19 | `V2.1.2.0035` | `588d712` | Ajout de la couverture du runtime on/off pour boutons Toggle Element+. |
| 2026-06-19 | `V2.1.2.0034` | `61eef34` | Ajout de la couverture du style appui/actif pour les boutons Element+. |
| 2026-06-19 | `V2.1.2.0033` | `89d7165` | Ajout de la couverture des symboles HMI Element+ electriques et alarme. |
| 2026-06-18 | `V2.1.2.0032` | `d5ee1fd` | Ajout de la couverture des proprietes avancees Element+ opacite et rotation. |
| 2026-06-18 | `V2.1.2.0031` | `f6a85ed` | Ajout de la couverture des symboles HMI Element+ moteur, ventilateur, convoyeur et jauge. |
| 2026-06-18 | `V2.1.2.0030` | `cae57c9` | Ajout de la couverture des presets de boutons HMI Element+ et du champ exporte `ButtonKind`. |
| 2026-06-18 | `V2.1.2.0029` | `b97ef16` | Ajout de la couverture des primitives process HMI Element+ reservoir, tuyaux, vanne et pompe. |
| 2026-06-18 | `V2.1.2.0028` | `PENDING` | Ajout de la couverture des primitives HMI Element+ voyant et barres de valeur. |
| 2026-06-18 | `V2.1.2.0027` | `PENDING` | Ajout de la couverture des formes standards Element+ et de l'insertion manuelle des boutons Element+. |
| 2026-06-17 | `V2.1.2.0025` | `58567eb` | Ajout de la validation TF100Web du formatage runtime `DisplayFormat` hash mask. |
| 2026-06-17 | `V2.1.2.0024` | `PENDING` | Ajout de la couverture du refactor Donnees Element+ et du masque numerique `DisplayFormat`. |
| 2026-06-17 | `V2.1.2.0022` | `PENDING` | Ajout de la couverture TF100Web des events de binding `ValueBindings` issus du `.sb2`. |
| 2026-06-17 | `V2.1.2.0020` | `c2f0b6f` | Ajout de la couverture du validateur CSS `.sb2` avec selecteurs page-scopes indentes. |
| 2026-06-17 | `V2.1.2.0019` | `bd6515e` | Ajout du validateur `.sb2` FT100 a la carte de couverture et test archive cible. |
| 2026-06-17 | `V2.1.2.0018` | `ad364a6` | Ajout de la reference aux tests d'intake TF100Web audites. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Ajout de la couverture domaine, persistance et export pour effets visuels runtime. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Ajout de la couverture export pour le bridge lifecycle runtime global. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Ajout de la couverture domaine, validation, persistance et export pour conditions composees. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Ajout de la couverture domaine, validation, persistance et export pour options popup avancees. |
| 2026-06-17 | `V2.1.2.0016` | `PENDING` | Ajout de la couverture domaine, validation, persistance et export pour les actions bordure Element+. |
| 2026-06-17 | `V2.1.2.0015` | `PENDING` | Ajout de la couverture domaine, persistance et export pour `Fermer popup` et `Basculer popup`. |
| 2026-06-17 | `V2.1.2.0014` | `PENDING` | Ajout de la couverture domaine, persistance, validation et export pour `Ouvrir popup`. |
| 2026-06-17 | `V2.1.2.0013` | `PENDING` | Ajout de la couverture de contrat pour le panneau `Catalogue Tags` filtre. |
| 2026-06-17 | `V2.1.2.0012` | `PENDING` | Ajout de la couverture export runtime pour l'application des valeurs `Lire valeur`. |
| 2026-06-17 | `V2.1.2.0010` | `PENDING` | Ajout de la couverture actions objet conditionnelles, validation build et export runtime. |
| 2026-06-17 | `V2.1.2.0009` | `PENDING` | Ajout de la couverture bindings `Lire valeur` et `Ecrire valeur`, validation build et export runtime. |
| 2026-06-17 | `V2.1.2.0008` | `PENDING` | Ajout de la couverture import tags TF100Web, persistance catalogue et export `WriteTag`. |
| 2026-06-16 | `V2.1.2.0007` | `PENDING` | Ajout de la couverture du curseur runtime par defaut des cibles cliquables FT100. |
| 2026-06-16 | `V2.1.2.0006` | `PENDING` | Ajout de la couverture des wrappers runtime transparents pour events de groupe Element+. |
| 2026-06-16 | `V2.1.2.0005` | `PENDING` | Ajout de la couverture metadonnees hover automatique, CSS FT100 et disabled des boutons Element+. |
| 2026-06-16 | `V2.1.2.0004` | `PENDING` | Ajout de la couverture du registre evenements Element+ et du bouton Evenement de l'editeur double-clic. |
| 2026-06-16 | `V2.1.2.0003` | `PENDING` | Ajout de la couverture pour ordre visuel, inventaire hierarchique et mouvement solidaire des groupes Element+. |
| 2026-06-16 | `V2.1.2.0002` | `PENDING` | Ajout de la couverture regression pour le groupement de scene Element+ only. |
| 2026-06-16 | `V2.1.2.0001` | `PENDING` | Ajout de la couverture regression du raccourci Backspace non destructif et du garde-fou clavier pour champs editables. |
| 2026-06-16 | `V2.1.2.0000` | `PENDING` | Ajout de la couverture regression pour conversion Button, Propriete contextuelle et rendu/export du texte des boutons. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation de la carte de couverture regression. |

## 1. Current Test Baseline

```text
dotnet test ScadaBuilderV2.sln --no-restore
659 passed, 5 failed, 0 skipped
```

## 2. Coverage Map

| Contract area | Primary tests |
| --- | --- |
| Runtime capability completeness (`DEC-0047`, partial) | `RuntimeContracts/ScadaRuntimeCapabilityCatalogTests.cs` and `ScadaRuntimeCapabilityAnalyzerTests.cs` cover typed inventory, artifacts, fixture ids, three-layer evidence requirements and model analysis. `RuntimeConformancePackageTests.cs` proves exact 118-capability factory coverage, byte-identical package regeneration, canonical SHA `fb06431eafbdb39f8e75aaa5216a8e6517e36a8f04bde6f6b1945aea90b08404`, archive/manifest/DOM/CSS/runtime integrity, sanitization and an exhaustive 162-entry expectation index. Runtime JS suites add table-driven AST/state/effect/command/action semantics, transitions, async and re-init coverage. `tools/docs/generate-runtime-capability-matrix.ps1` plus `verify-docs` enforce code/matrix parity. TF100Web execution of the committed fixture remains pending. |
| Shared command and input semantics (`DEC-0047`, partial) | Builder `tests/runtime-js/command-dispatcher.test.mjs` covers all five triggers, seven kinds, Toggle/SetFixed/SetFromInput and real Momentary phases, confirmation ordering, disabled/missing values, canonical intents, HostAdapter precedence, async rejection and duplicate suppression. TF100Web `frontend/tests_runtime_js/host-adapter.test.mjs` covers canonical service mapping, invalid input, duplicate delivery, origin, stale declared page and protected writes. End-to-end Momentary/readback promotion remains pending. |
| Shared object-action semantics (`DEC-0047`, partial) | Builder `tests/runtime-js/action-dispatcher.test.mjs` covers all nine kinds, every condition operator, All/Any, both missing policies, binding order, prevent/stop propagation, disabled sources, disposal and duplicate ids across composed page roots. `Ft100SceneExporterTests.cs` locks canonical registries/bindings and scope. TF100Web `cab2733` removes the parallel message switch and routes action-owned host intents into one adapter; fixture execution proof remains pending. |
| Latest-wins navigation and hydration (`DEC-0046`) | TF100Web `frontend/tests_runtime_js/navigation-lifecycle.test.mjs` covers supersession, inverse completion, timeout ownership, stale settle, offline/retry and generation-gated mutation symbols. `tag-cache-hydration.test.mjs` covers forced-during-in-flight follow-up, force coalescing, unchanged-value notification, dependency recollection and stale snapshot rejection. `ScadaRuntimeInitContractTests` locks the deployed integration; 16 JS and 19 Django contract tests are green. |
| Generic numeric bindings (`DEC-0047`) | TF100Web `frontend/tests_runtime_js/binding-runtime.test.mjs` table-tests read-only/write-only/read-write/denied/unbound policies, fixed/hash formatting across FLOAT32/FLOAT64 and eight integer datatypes, valid/invalid/denied/rejected/offline commits, pending duplicate suppression and Enter/Escape behavior. Source integration locks one Element+/Table path, all composed slots, poll focus/pending protection and missing-mapping quality fallback. Combined JS runtime count is 23; 20 focused Django contracts are green. |
| Linear composition and atomic cache lifecycle (`DEC-0047`) | TF100Web `frontend/tests_scada_performance.py` parameterizes 1/3 pages and 64/256/1,024 bindings per fragment, proves one tag pass and constant catalog resolver calls, and verifies generation-keyed structural invalidation. `DeployScadaBuilderManifestTests` covers generation rotation, timing metadata and failed-swap rollback. Seventeen focused composition/deployment/performance tests are green. |
| TF100Web manifest 2.3 negotiation (`DEC-0047`, partial) | `frontend.tests_scada_deploy.DeployScadaBuilderManifestTests` and targeted package tests cover 2.1/2.2 compatibility, valid 2.3, missing/unknown versions, unsupported capability ids, contract version, runtime tampering, missing contract, pre-replacement preservation and exact vendored fixture SHA. Eleven focused tests and the local Django check pass under SQLite verification settings. |
| FT100/TF100Web export | `Ft100SceneExporterTests.cs`: manifest 2.3 default, sorted/deduplicated requirements, packaged runtime SHA-256, pre-staging blocked-capability rejection, and explicit 2.1/2.2 profiles. |
| FT100 `.sb2` archive and namespace validation | `Ft100PackageValidator`, `Ft100PackageValidatorTests`: unknown/duplicate/unsorted/blocked capabilities, runtime contract version, missing/invalid/mismatched SHA-256, tampering and runtime filename; plus archive and page-scope regressions in `Ft100SceneExporterTests`. |
| TF100Web package intake audit | `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py` |
| TF100Web `.sb2` binding event intake | `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py` (`ValueBindings.ReadTagId` / `WriteTagId` -> host mapping attributes) |
| TF100Web `DisplayFormat` hash-mask runtime | `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`, `node --check static\asset\js\station\visualisation_import.js`, targeted Node validation of `formatValue(999, "##.#") -> "99.9"` |
| Project save/reload | `ModernProjectStoreTests.cs` |
| Modern page identity and Wonderware migration | `PageIdentityTests.cs`, `ModernProjectStoreTests.cs` |
| Page command coordinator and shared application commands | `PageCommandCoordinatorTests.cs` (`ShowPropertiesOpensAndActivatesSelectedPageWithoutDirtyingWorkspace`), `PageApplicationCommandTests.cs` |
| Project-scoped page undo/redo | `ProjectWorkspaceHistoryTests.cs`, `PageLifecycleIntegrationTests.cs` |
| Atomic project/scenes/deletions persistence | `ModernProjectAtomicSnapshotTests.cs`, `ModernProjectStoreTests.cs` |
| Native page preview/export and `.sb2` identity projection | `NativePageDocumentTests.cs`, `Ft100SceneExporterTests.cs`, `PageLifecycleIntegrationTests.cs` |
| Pages ribbon/project/context surfaces | `RibbonCommandCatalogTests.cs`, `PageManagementSurfaceContractTests.cs` (search label, initial filters, shared `Icon.Page.New` and inline-content right-click traversal) |
| Shared error dialog and Diagnostics panel | `DiagnosticsSurfaceContractTests.cs`, including explicit `OneWay` bindings for read-only WPF `Run.Text` targets |
| Scene/domain rules | `OfficialSceneDomainTests.cs`, `ScadaSceneGroupTests.cs` |
| Undo/redo/history | `EditorHistoryServiceTests.cs` |
| WebView bridge/context menu | `WebViewContextMenuScriptTests.cs` |
| Top ribbon dynamic command surface | `RibbonCommandCatalogTests.MainRibbonUsesOnlyDynamicCommandSurface`, `RibbonCommandCatalogTests.DefaultCatalogDefinesExpectedTopRibbonTabs`, `RibbonCommandCatalogTests.DefaultCatalogRequiresSemanticIconKeys`, `RibbonCommandCatalogTests.InsertFamilyRibbonKeepsFirstLevelCompact`, `RibbonCommandCatalogTests.SecondLevelRibbonUsesCompactTwoRowCommands` |
| Left tool palette semantic command surface | `RibbonCommandCatalogTests.ToolPaletteUsesSemanticCommandCatalog`, `RibbonCommandCatalogTests.DefaultCatalogRequiresSemanticIconKeys`, `RibbonCommandCatalogTests.DisabledCommandsExposeReason` |
| Element inventory hierarchy | `LegacyElementSelectionModelTests.cs` |
| Element+ legacy conversion | `ElementPlusLegacyConverterTests.cs` |
| Element+ events/actions | `OfficialSceneDomainTests.cs`, `WebViewContextMenuScriptTests.cs`, `ModernProjectStoreTests.cs`, `Ft100SceneExporterTests.cs` |
| Element+ conditional object visibility actions | `OfficialSceneDomainTests.cs`, `ModernProjectStoreTests.cs`, `Ft100SceneExporterTests.cs` |
| Element+ compound condition groups | `OfficialSceneDomainTests.cs`, `ModernProjectStoreTests.cs`, `Ft100SceneExporterTests.cs` |
| FT100 runtime lifecycle bridge | `Ft100SceneExporterTests.cs` |
| Element+ runtime border actions | `OfficialSceneDomainTests.cs`, `ModernProjectStoreTests.cs`, `Ft100SceneExporterTests.cs` |
| Element+ runtime visual effects | `OfficialSceneDomainTests.cs`, `ModernProjectStoreTests.cs`, `Ft100SceneExporterTests.cs` |
| Element+ popup fragment actions | `OfficialSceneDomainTests.cs`, `ModernProjectStoreTests.cs`, `Ft100SceneExporterTests.cs` |
| Element+ advanced popup options | `OfficialSceneDomainTests.cs`, `ModernProjectStoreTests.cs`, `Ft100SceneExporterTests.cs` |
| TF100Web tag catalog import and Element+ value bindings | `ModernProjectStoreTests.cs`, `OfficialSceneDomainTests.cs`, `Ft100SceneExporterTests.cs` |
| Tag catalog editor panel filters | `StudioElementPlusContractTests.cs` |
| FT100 read tag value application runtime | `Ft100SceneExporterTests.cs` |
| Element+ group click navigation export | `Ft100SceneExporterTests.cs` |
| FT100 clickable target pointer cursor | `Ft100SceneExporterTests.cs` |
| Element+ button hover metadata and FT100 CSS | `OfficialSceneDomainTests.cs`, `WebViewContextMenuScriptTests.cs`, `ModernProjectStoreTests.cs`, `Ft100SceneExporterTests.cs` |
| Element+ button pressed/active metadata and FT100 CSS | `OfficialSceneDomainTests.ButtonElementHasDefaultHoverUnlessExplicitlyDisabled`, `WebViewContextMenuScriptTests.ElementPropertiesExposeAdvancedButtonPressedFields`, `ModernProjectStoreTests.SaveAndReloadPreservesPageManifestBackgroundAndObjectEvents`, `Ft100SceneExporterTests.ExportWritesDjangoManifestAndObjectOwnedClickNavigateAction` |
| Element+ Toggle button on/off runtime state | `Ft100SceneExporterTests.ExportWritesToggleButtonRuntimeStateOnWrapper` |
| Element+ disabled button runtime state | `Ft100SceneExporterTests.ExportWritesDisabledButtonRuntimeStateAndOmitsHoverCss` |
| Element+ standard button activation runtime events | `Ft100SceneExporterTests.ExportWritesStandardButtonActivationRuntimeEvents` |
| Element+ button preview/export metadata parity | `WebViewContextMenuScriptTests.ModernButtonRendersTextAndUsesPropertyText`, `Ft100SceneExporterTests.ExportWritesToggleButtonRuntimeStateOnWrapper`, `Ft100SceneExporterTests.ExportWritesDisabledButtonRuntimeStateAndOmitsHoverCss`, `Ft100SceneExporterTests.ExportWritesStandardButtonActivationRuntimeEvents` |
| Stateful defrost Toggle buttons (`DEC-0044`) | `Win00012DefrostToggleConfigurationTests.ReferenceScene_ConfiguresAllDefrostTogglesFromTheirConfirmedCommandBit`, `Ft100SceneExporterTests.ExportAsync_WrapsButtonLabelInDataScadaTextSpan`, `tests/runtime-js/state-engine.test.mjs`, `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py::ScadaRuntimeInitContractTests` |
| Reversible state visuals and shared numeric mappings (`DEC-0045`) | `tests/runtime-js/effect-applier.test.mjs`, `tests/runtime-js/state-engine.test.mjs`, `RuntimeJsModulesTests`, `Ft100SceneExporterTests`, `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py::ScadaRuntimeInitContractTests` |
| Element+ HMI button presets and `ButtonKind` export | `OfficialSceneDomainTests.ButtonElementHasDefaultHoverUnlessExplicitlyDisabled`, `WebViewContextMenuScriptTests.InsertRibbonExposesStandardShapesAndButtons`, `WebViewContextMenuScriptTests.ModernButtonRendersTextAndUsesPropertyText`, `ModernProjectStoreTests.SaveAndReloadPreservesPageManifestBackgroundAndObjectEvents`, `Ft100SceneExporterTests.ExportWritesDjangoManifestAndObjectOwnedClickNavigateAction` |
| Element+ advanced style opacity and rotation | `OfficialSceneDomainTests.InputTextElementHasEditableStyleAndDataDefaults`, `WebViewContextMenuScriptTests.ElementPropertiesExposeAdvancedShapeStyleFields`, `ModernProjectStoreTests.SaveAndReloadPreservesPageManifestBackgroundAndObjectEvents`, `Ft100SceneExporterTests.ExportRendersStandardShapeElementAsScopedSvg` |
| Element+ rotation handle and context menu presets | `WebViewContextMenuScriptTests.LegacyViewerMessageExposesRotationField`, `WebViewContextMenuScriptTests.MainWindowHandlesRotationMessageAndNormalizesAngle`, `WebViewContextMenuScriptTests.NeHandleIsRepurposedForRotationDrag`, `WebViewContextMenuScriptTests.RotationDragShowsLiveAngleBadge`, `WebViewContextMenuScriptTests.ContextMenuOffersRotationPresetsForSingleElementPlusSelection`, `WebViewContextMenuScriptTests.ContextMenuCustomRotationOpensValidatedInlineInput`, `WebViewContextMenuScriptTests.CustomRotationCleanupDetachesBlurListenerBeforeHidingInput` |
| Element+ machine and measurement HMI symbols with manual insertion | `OfficialSceneDomainTests.ShapeElementDefaultsAndFactoriesPreserveShapeKind`, `WebViewContextMenuScriptTests.InsertRibbonExposesStandardShapesAndButtons`, `WebViewContextMenuScriptTests.ModernShapePreviewUsesSvgShapeKind`, `Ft100SceneExporterTests.ExportRendersStandardShapeElementAsScopedSvg` |
| Element+ electrical and alarm HMI symbols with manual insertion | `OfficialSceneDomainTests.ShapeElementDefaultsAndFactoriesPreserveShapeKind`, `WebViewContextMenuScriptTests.InsertRibbonExposesStandardShapesAndButtons`, `WebViewContextMenuScriptTests.ModernShapePreviewUsesSvgShapeKind`, `Ft100SceneExporterTests.ExportRendersStandardShapeElementAsScopedSvg` |
| Element+ standard, HMI, and process shapes with manual insertion | `OfficialSceneDomainTests.ShapeElementDefaultsAndFactoriesPreserveShapeKind`, `ModernProjectStoreTests.SaveAndReloadPreservesPageManifestBackgroundAndObjectEvents`, `WebViewContextMenuScriptTests.InsertRibbonExposesStandardShapesAndButtons`, `WebViewContextMenuScriptTests.ModernShapePreviewUsesSvgShapeKind`, `Ft100SceneExporterTests.ExportRendersStandardShapeElementAsScopedSvg` |
| Insert Formes gallery and two-point line/arrow authoring | `RibbonCommandCatalogTests.MainRibbonUsesClippingSafeOverflowHeight`, `RibbonCommandCatalogTests.DefaultCatalogUsesStableUniqueCommandIds`, `WebViewContextMenuScriptTests.InsertRibbonExposesStandardShapesAndButtons`, `WebViewContextMenuScriptTests.LineAndArrowPlacementUseTwoPointMode`, `OfficialSceneDomainTests.ShapeElementDefaultsAndFactoriesPreserveShapeKind`, `Ft100SceneExporterTests.ExportRendersStandardShapeElementAsScopedSvg` |
| Modern table dialog fields | `TableUiArchitectureTests.TableDialogLayoutKeepsConcreteWpfControlsVisible` |
| Table contextual entry and shared Element+ lock surfaces | `TableUiArchitectureTests`, `TableAuthoringSessionTests.RibbonTogglesEditorGuidesAndMergeActionFromSelectionState`, `TableEditCoordinatorTests.ToggleMergeUsesCurrentSelectionState`, `WebViewContextMenuScriptTests.ContextMenuOffersStatefulElementLockAction`, `WebViewContextMenuScriptTests.LockedElementMovementIsRejectedBeforeVisualDragStarts`, `WebViewContextMenuScriptTests.TableCellModeOwnsPointerInputBeforeElementDrag`, `ElementLockCoordinatorTests`, `ApplicationCommandTests`, `RibbonCommandCatalogTests` |
| Advanced table format inspection and reset | `TablePropertiesInspectorTests`, `TableEditCoordinatorTests` |
| Typed table WebView bridge diagnostics | `TableWebViewMessageAdapterTests` |
| 64 x 64 batching and Release measurements | `TableWebViewPerformanceContractTests` |
| Representative `win00012` 16 x 10 round-trip/preview/`.sb2` | `AdvancedTableAuthoringIntegrationTests` |
| Element+ `Donnees` authoring and `DisplayFormat` masks | `ElementGroupTests.NumericDisplayFormatMaskControlsScalePrecisionAndInputStep`, `ElementGroupTests.NumericDisplayFormatMaskClampsToVisibleDigitBudget`, `WebViewContextMenuScriptTests.ElementDataTabDeprecatesLegacyTagDecimalsAndUnitFields` |
| Table cell numeric binding domain and edit safety | `TableCellBindingOperationsTests`, `TableContentOperationsTests`, `ScadaTableOperationsTests`, `TableClipboardTests`, `TableEditCoordinatorTests`, `OfficialSceneDomainTests` |
| Table cell numeric authoring surfaces | `TableNumericInputPropertiesViewModelTests`, `TableNumericInputPropertiesDialogTests`, `TableUiArchitectureTests`, `TablePropertiesViewModelTests` |
| Table cell numeric preview/export/validation | `ModernTableHtmlRendererTests`, `ModernProjectStoreTests`, `Ft100SceneExporterTests`, `Ft100PackageValidatorTests` |
| Reliable single-surface numeric cell authoring (`DEC-0043`) | `TableCellAddressTests`, `TableNumericBindingAuthoringPolicyTests`, `TablePropertiesInspectorTests`, `TableEditCoordinatorTests`, `TableUiArchitectureTests`, `TableWebViewMessageAdapterTests`, `ModernProjectStoreTests`, `Ft100SceneExporterTests`; isolated `win00012_modern_no_legacy` WPF/WebView2 smoke |
| TF100Web manifest 2.1/2.2 and cell runtime intake | `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`, `frontend/tests_scada_table_bindings.py`, `frontend/tests_scada_runtime.py`, `frontend/tests_deploy_scada_builder.py` |
| Studio Element+ editor state | `ElementStudioEditorStateTests.cs` |
| Studio Element+ contract | `StudioElementPlusContractTests.cs` |
| Studio Element+ re-edit from scene | `WebViewContextMenuScriptTests.cs`, `ElementStudioComponentToImportPackageMapperTests.cs`, `ElementStudioComponentNamingTests.cs` |
| Studio source rendering | `ElementStudioSourceRenderingTests.cs` |
| Legacy extraction | `LegacyElementDetectorTests.cs`, `LegacyAtomicElementDetectorTests.cs` |

## 3. Rule

When a contract-sensitive behavior changes, update this map or document why no test exists in `KNOWN_GAPS_V2.md`.
