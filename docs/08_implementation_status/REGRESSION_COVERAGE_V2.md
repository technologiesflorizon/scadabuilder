# SCADA Builder V2 - Regression Coverage

Date: 2026-06-19
Status: Active regression coverage map
Document version: `V2.1.2.0042`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-19 | `V2.1.2.0042` | `PENDING` | Ajout de la couverture du dispatch de ruban pour `object.group` et `object.ungroup`. |
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
243 passed, 0 failed, 0 skipped
```

## 2. Coverage Map

| Contract area | Primary tests |
| --- | --- |
| FT100/TF100Web export | `Ft100SceneExporterTests.cs` |
| FT100 `.sb2` archive and namespace validation | `Ft100PackageValidator`, `Ft100SceneExporterTests.ExportProjectArchiveWritesSb2RootAndScopesLegacyDomIds`, `Ft100SceneExporterTests.PackageValidatorAcceptsIndentedPageScopedCssIdSelectors` |
| TF100Web package intake audit | `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py` |
| TF100Web `.sb2` binding event intake | `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py` (`ValueBindings.ReadTagId` / `WriteTagId` -> host mapping attributes) |
| TF100Web `DisplayFormat` hash-mask runtime | `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`, `node --check static\asset\js\station\visualisation_import.js`, targeted Node validation of `formatValue(999, "##.#") -> "99.9"` |
| Project save/reload | `ModernProjectStoreTests.cs` |
| Scene/domain rules | `OfficialSceneDomainTests.cs`, `ScadaSceneGroupTests.cs` |
| Undo/redo/history | `EditorHistoryServiceTests.cs` |
| WebView bridge/context menu | `WebViewContextMenuScriptTests.cs` |
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
| Element+ HMI button presets and `ButtonKind` export | `OfficialSceneDomainTests.ButtonElementHasDefaultHoverUnlessExplicitlyDisabled`, `WebViewContextMenuScriptTests.InsertRibbonExposesStandardShapesAndButtons`, `WebViewContextMenuScriptTests.ModernButtonRendersTextAndUsesPropertyText`, `ModernProjectStoreTests.SaveAndReloadPreservesPageManifestBackgroundAndObjectEvents`, `Ft100SceneExporterTests.ExportWritesDjangoManifestAndObjectOwnedClickNavigateAction` |
| Element+ advanced style opacity and rotation | `OfficialSceneDomainTests.InputTextElementHasEditableStyleAndDataDefaults`, `WebViewContextMenuScriptTests.ElementPropertiesExposeAdvancedShapeStyleFields`, `ModernProjectStoreTests.SaveAndReloadPreservesPageManifestBackgroundAndObjectEvents`, `Ft100SceneExporterTests.ExportRendersStandardShapeElementAsScopedSvg` |
| Element+ machine and measurement HMI symbols with manual insertion | `OfficialSceneDomainTests.ShapeElementDefaultsAndFactoriesPreserveShapeKind`, `WebViewContextMenuScriptTests.InsertRibbonExposesStandardShapesAndButtons`, `WebViewContextMenuScriptTests.ModernShapePreviewUsesSvgShapeKind`, `Ft100SceneExporterTests.ExportRendersStandardShapeElementAsScopedSvg` |
| Element+ electrical and alarm HMI symbols with manual insertion | `OfficialSceneDomainTests.ShapeElementDefaultsAndFactoriesPreserveShapeKind`, `WebViewContextMenuScriptTests.InsertRibbonExposesStandardShapesAndButtons`, `WebViewContextMenuScriptTests.ModernShapePreviewUsesSvgShapeKind`, `Ft100SceneExporterTests.ExportRendersStandardShapeElementAsScopedSvg` |
| Element+ standard, HMI, and process shapes with manual insertion | `OfficialSceneDomainTests.ShapeElementDefaultsAndFactoriesPreserveShapeKind`, `ModernProjectStoreTests.SaveAndReloadPreservesPageManifestBackgroundAndObjectEvents`, `WebViewContextMenuScriptTests.InsertRibbonExposesStandardShapesAndButtons`, `WebViewContextMenuScriptTests.ModernShapePreviewUsesSvgShapeKind`, `Ft100SceneExporterTests.ExportRendersStandardShapeElementAsScopedSvg` |
| Element+ `Donnees` authoring and `DisplayFormat` masks | `ElementGroupTests.NumericDisplayFormatMaskControlsScalePrecisionAndInputStep`, `ElementGroupTests.NumericDisplayFormatMaskClampsToVisibleDigitBudget`, `WebViewContextMenuScriptTests.ElementDataTabDeprecatesLegacyTagDecimalsAndUnitFields` |
| Studio Element+ editor state | `ElementStudioEditorStateTests.cs` |
| Studio Element+ contract | `StudioElementPlusContractTests.cs` |
| Studio source rendering | `ElementStudioSourceRenderingTests.cs` |
| Legacy extraction | `LegacyElementDetectorTests.cs`, `LegacyAtomicElementDetectorTests.cs` |

## 3. Rule

When a contract-sensitive behavior changes, update this map or document why no test exists in `KNOWN_GAPS_V2.md`.
