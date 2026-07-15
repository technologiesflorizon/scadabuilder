# Outils d'authoring Tableau et verrouillage Element+ - Plan d'implementation

Date: 2026-07-15
Status: Reviewed implementation plan - pending execution approval
Document version: `V2.1.4.0025`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-15 | `V2.1.4.0025` | `0b1fbf4` | Integration de la revue du plan : extraction structurelle conforme a la spec, guards d'export conditionnels, tests d'architecture/resize, commandes explicites, retrait controle du dialogue, scenario `win00012` qualifie et staging documentaire ferme. |
| 2026-07-15 | `V2.1.4.0024` | `3f6e6a5` | Creation du plan executable derive de la specification approuvee et de `DEC-0040`. |

> **Pour les agents d'execution :** executer ce plan tache par tache. Ne pas deleguer a des sous-agents sans autorisation explicite de l'utilisateur. Les cases `- [ ]` constituent le suivi d'execution et chaque tache possede sa propre frontiere de commit.

**Goal:** Completer le Tableau Element+ afin de reconstruire efficacement une page equivalente a `win00012` sans bindings cellule par cellule, offrir une sous-surface Tableau sans dialogue de creation, puis rendre le verrouillage de position reel et persistant pour tous les Element+ avec une semantique coherente de groupe et de multiselection.

**Architecture:** Domain porte `IsLocked`, les fermetures de groupe et les operations pures de contenu, format, bordure, pistes et en-tetes. Application porte la session d'authoring, les requetes typees, commandes, guards, mutations et historique. App porte les view models WPF, dialogues et adaptateurs WebView. Rendering consomme le modele commun sans etat d'editeur. `MainWindow` reste l'hote de haut niveau et ne calcule aucune regle Tableau ou de verrouillage.

**Tech Stack:** C# 12, .NET 8, WPF/AvalonDock/WebView2, JavaScript embarque, `System.Text.Json`, MSTest, PowerShell, HTML/CSS et archives `.sb2`/`.sep`

## Global Constraints

- Specification proprietaire : `docs/superpowers/specs/2026-07-15-table-ui-authoring-and-element-lock-design.md`.
- Decision active : `DEC-0040`; `DEC-0039` demeure l'historique du noyau Tableau deja implemente.
- `insert.table` ouvre la sous-surface Tableau; seule `table.add` arme le placement sans dialogue.
- Le Tableau reste un seul Element+ model-backed; aucun Element+ enfant ou `ValueBindings` cellule par cellule.
- Capacite obligatoire : 1 a 64 rangees et colonnes; preset unique 6 x 8 avec premiere rangee d'en-tete.
- Le verrou protege seulement les changements effectifs de X/Y. Selection, resize simple sans translation, rotation, contenu, style, events et edition interne du Tableau restent disponibles.
- Un toggle de groupe agit recursivement. Un descendant verrouille bloque toute operation qui changerait sa position par mouvement ou resize de groupe.
- L'etat mixte apparait deverrouille dans les toggles et indetermine dans la case Propriete; un clic verrouille toute la fermeture.
- `object.lock` remplace `selection.toggle-lock` sans alias dans l'application principale. Le verrou propre a `ElementStudioEditorState` reste intact.
- Les bordures avancees sont persistees par segments physiques. Les presets sont des operations, jamais des enums serialises.
- Les mesures typographiques d'auto-fit proviennent de WebView2; Application valide et Domain applique les dimensions.
- Preview, build et export consomment le meme modele. Aucun verrou, header/gouttiere d'authoring, overlay, handle ou diagnostic n'entre dans la geometrie `.sb2`/`.sep`.
- Aucun calcul de cellule, bordure, fermeture de groupe, aggregation de lock ou choix de toggle dans `MainWindow`.
- Toute mutation persistante produit une seule action undo/redo et aucun dirty state en cas de refus.
- Toute API publique ajoutee recoit une documentation XML avec `Decisions: DEC-0040`, contrat proprietaire et tests dans `<remarks>`.
- Ne jamais modifier, restaurer, supprimer ou stager `projects/AMR_REF_SCADA_V2` pendant l'execution.

---

## Before You Start

- [ ] Verifier la branche et l'etat de travail.

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git status --short --branch
```

Attendu : branche issue de `codex/table-tool-insert-ribbon`. Le worktree courant contient des modifications utilisateur sous `projects/AMR_REF_SCADA_V2`. Avant toute modification de code, utiliser un worktree d'execution propre ou demander a l'utilisateur de sauvegarder/committer lui-meme ces changements. Ne pas les stasher, restaurer ou committer implicitement.

- [ ] Confirmer que les commits de specification/decision/plan sont presents et que la specification est `Approved`.

```powershell
git log -5 --oneline
rg -n "Status: Approved|DEC-0040" docs/superpowers/specs/2026-07-15-table-ui-authoring-and-element-lock-design.md docs/00_governance/DECISION_REGISTER_V2.md
```

- [ ] Capturer un baseline frais dans le worktree propre.

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore
```

Attendu : conserver le resultat comme baseline. Ne jamais figer dans le plan un ancien nombre global de tests; toute nouvelle regression par rapport a ce baseline doit etre corrigee avant commit.

- [ ] Auditer une derniere fois les consommateurs de l'ancien verrou avant sa suppression.

```powershell
rg -n "selection\.toggle-lock|ToggleSelectionLockCommand|IsSelectionLocked|SetSelectionLocked" src tests
```

Attendu : seuls les consommateurs documentes dans la spec sont presents. Si un consommateur produit inconnu apparait, suspendre la migration et demander une decision.

---

## Phase 1 - Verrouillage persistant Domain et Application

### Task 1: Ajouter `IsLocked` et les operations pures de fermeture

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs`
- Create: `src/ScadaBuilderV2.Domain/Scenes/ScadaSceneElementLockOperations.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ScadaSceneModelsTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ScadaSceneGroupTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`

**Interfaces:**
- Consumes: `ScadaElement`, record `ScadaScene`, conventions `System.Text.Json` de `ModernProjectStore`.
- Produces/Changes: parametre final `bool IsLocked = false`, fermeture de selection et mutation recursive immuable.

- [ ] Ajouter `IsLocked` comme dernier parametre du record `ScadaElement`, sans `[JsonIgnore]`, et documenter la cle de scene PascalCase `IsLocked`.
- [ ] Implementer `ExpandSelectionClosure(scene, ids)`, `ResolveEffectiveLock(scene, id)` et `ApplyRecursive(scene, ids, isLocked)` dans la nouvelle classe statique, sans reference Application/WPF.
- [ ] Garantir l'ordre stable des ids touches, l'elimination des doublons, les groupes imbriques et l'absence de mutation partielle si un id est absent.
- [ ] Tester ancien JSON sans cle, emission explicite vrai/faux, round-trip, groupe imbrique, groupe mixte et preservation des enfants au group/ungroup.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ScadaSceneModelsTests|FullyQualifiedName~ScadaSceneGroupTests|FullyQualifiedName~ModernProjectStoreTests"
```

Attendu : ancien JSON charge comme deverrouille; les mutations Domain sont deterministes et la scene source reste inchangee.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs src/ScadaBuilderV2.Domain/Scenes/ScadaSceneElementLockOperations.cs tests/ScadaBuilderV2.Tests/ScadaSceneModelsTests.cs tests/ScadaBuilderV2.Tests/ScadaSceneGroupTests.cs tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs
git commit -m "feat: add persistent element position lock model"
```

### Task 2: Ajouter le coordinateur, le guard, la commande et l'historique de lock

**Files:**
- Create: `src/ScadaBuilderV2.Application/Selection/ElementLockSelectionState.cs`
- Create: `src/ScadaBuilderV2.Application/Selection/ElementLockCoordinator.cs`
- Create: `src/ScadaBuilderV2.Application/Selection/ElementTransformGuard.cs`
- Create: `src/ScadaBuilderV2.Application/Commands/ToggleElementLockCommand.cs`
- Create: `src/ScadaBuilderV2.Application/History/ElementLockChangedAction.cs`
- Modify: `src/ScadaBuilderV2.Application/Commands/ApplicationContext.cs`
- Delete: `src/ScadaBuilderV2.Application/Commands/ToggleSelectionLockCommand.cs`
- Create: `tests/ScadaBuilderV2.Tests/ElementLockCoordinatorTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/ElementTransformGuardTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ApplicationCommandTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/SelectionStateTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs`

**Interfaces:**
- Consumes: scene snapshot, ids selectionnes, operations Domain de la tache 1 et `CommandRegistry`.
- Produces/Changes: etat agrege, mutation avant/apres par id, `object.lock`, guard geometrique et action undo/redo atomique.

- [ ] Definir `ElementLockSelectionState` avec `HasSelection`, `AllLocked`, `IsMixed` et `TargetIds`.
- [ ] Faire produire au coordinateur une mutation contenant, pour chaque id touche, l'etat avant et apres; ne pas stocker une scene complete dans l'action d'historique.
- [ ] Etendre `ApplicationContext` avec le snapshot de scene et un callback d'application de mutation, selon le pattern des commandes Pages; ne pas faire muter le projet directement par la commande.
- [ ] Implementer `ToggleElementLockCommand` sous `object.lock`, avec `CanExecute` faux sans Element+ et resultat `WorkspaceDirty = true` seulement sur changement.
- [ ] Implementer `ElementTransformGuard.CanApply` a partir des bounds avant/apres et de la fermeture effective; un changement W/H ou rotation sans changement X/Y reste autorise.
- [ ] Supprimer `SelectionState.IsSelectionLocked`, `SetSelectionLocked`, le parametre `force` devenu inutile et `ToggleSelectionLockCommand`; ne pas modifier `ElementStudioEditorState`.
- [ ] Tester aucun/tous/mixte, groupes imbriques, deux clics sur un groupe mixte, undo/redo exact, refus sans historique, ainsi que l'acceptation explicite d'un resize W/H et d'une rotation d'objet simple verrouille lorsque X/Y restent inchanges.
- [ ] Apres suppression, reexecuter la recherche de dependances avant commit :

```powershell
rg -n "selection\.toggle-lock|ToggleSelectionLockCommand|IsSelectionLocked|SetSelectionLocked" src tests
```

Attendu : aucune reference residuelle dans l'application principale; les symboles propres a `ElementStudioEditorState` peuvent subsister. Si un consommateur produit inconnu apparait, suspendre la migration et demander une decision.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ElementLockCoordinatorTests|FullyQualifiedName~ElementTransformGuardTests|FullyQualifiedName~ApplicationCommandTests|FullyQualifiedName~SelectionStateTests|FullyQualifiedName~EditorHistoryServiceTests"
```

Attendu : `object.lock` est la seule commande main-editor et l'ancien verrou de selection n'existe plus.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.Application/Selection src/ScadaBuilderV2.Application/Commands/ApplicationContext.cs src/ScadaBuilderV2.Application/Commands/ToggleElementLockCommand.cs src/ScadaBuilderV2.Application/History/ElementLockChangedAction.cs tests/ScadaBuilderV2.Tests/ElementLockCoordinatorTests.cs tests/ScadaBuilderV2.Tests/ElementTransformGuardTests.cs tests/ScadaBuilderV2.Tests/ApplicationCommandTests.cs tests/ScadaBuilderV2.Tests/SelectionStateTests.cs tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs
git rm src/ScadaBuilderV2.Application/Commands/ToggleSelectionLockCommand.cs
git commit -m "feat: add element lock command and transform guard"
```

---

## Phase 2 - Surfaces et enforcement du verrou

### Task 3: Brancher les trois surfaces de lock sur un view model commun

**Files:**
- Create: `src/ScadaBuilderV2.App/ElementLock/ElementLockStateViewModel.cs`
- Create: `src/ScadaBuilderV2.App/MainWindow.ElementLockIntegration.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Modify: `src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs`
- Modify: `tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `ElementLockSelectionState`, `object.lock`, scene/selection actives.
- Produces/Changes: `ElementLockStateViewModel` unique, toggle Selection, case Propriete tri-state et indicateur superieur.

- [ ] Rendre `object.lock` executable/toggle dans le catalogue et exposer l'etat dynamique dans le view model de commande du ruban.
- [ ] Creer `ElementLockStateViewModel` avec `INotifyPropertyChanged`, `IsEnabled`, `IsToggleChecked`, `IsPropertyChecked` et `ToggleCommand`.
- [ ] Supprimer `MainWindow.IsSelectionLocked`; enregistrer `ToggleElementLockCommand` et fournir au contexte les callbacks de scene/historique.
- [ ] Deplacer le bouton superieur dans un conteneur docke a droite, immediatement a gauche de `SCADA Builder V2`, puis appliquer les bindings OneWay de la spec.
- [ ] Ajouter la case `Verrouillage` a l'onglet droit Propriete et lier le ruban Selection a la meme instance.
- [ ] Appeler uniquement `RefreshElementLockState()` depuis le chemin central `RefreshSelectionUi()` et apres une mutation; aucune surface ne doit recalculer l'etat.
- [ ] Tester position XAML, bindings, etat mixte, enablement sans selection et absence de propriete locale `IsSelectionLocked`.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~RibbonCommandCatalogTests|FullyQualifiedName~TableUiArchitectureTests|FullyQualifiedName~WebViewContextMenuScriptTests|FullyQualifiedName~ElementLockCoordinatorTests"
```

Attendu : les trois surfaces affichent et executent strictement le meme etat/commande.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.App/ElementLock src/ScadaBuilderV2.App/MainWindow.ElementLockIntegration.cs src/ScadaBuilderV2.App/MainWindow.xaml src/ScadaBuilderV2.App/MainWindow.xaml.cs src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: add shared element lock surfaces"
```

### Task 4: Appliquer le guard a tous les chemins de translation

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.TableIntegration.cs`
- Inspect: `src/ScadaBuilderV2.Rendering/PreviewDocument.cs`
- Inspect: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`
- Inspect: `src/ScadaBuilderV2.Application/ElementStudio/ElementStudioComponentPackageFactory.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ElementTransformGuardTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/StudioElementPlusContractTests.cs`

**Interfaces:**
- Consumes: `ElementTransformGuard`, payload de rendu et intentions geometriques WebView/WPF.
- Produces/Changes: defense visuelle `data-editor-locked` et defense Application avant historique.

- [ ] Projeter `data-editor-locked` uniquement dans le document d'edition et empecher le debut de drag d'une cible effectivement verrouillee.
- [ ] Faire passer `moveSelectionBy`, drag, fleches clavier, edition X/Y, `UpdateModernElementGeometry`, normalisation vers groupe et `UpdateModernGroupGeometryWithChildren` par un seul adaptateur de validation.
- [ ] Comparer les bounds avant/apres de tous les descendants touches; refuser un resize de groupe qui deplacerait un descendant verrouille.
- [ ] Sur refus, restaurer le feedback WebView si necessaire, publier un statut clair et ne pousser ni dirty state ni action.
- [ ] Preserver `IsLocked` dans copie, couper/coller et duplication de scene; normaliser un objet importe de `.sep` a deverrouille.
- [ ] Verifier que `.sb2` n'emet pas `data-editor-locked`/`IsLocked` et que `.sep` ne transporte pas le verrou de scene.
- [ ] Si l'inspection de `PreviewDocument.cs`, `Ft100SceneExporter.cs` ou `ElementStudioComponentPackageFactory.cs` revele qu'un verrou ou un artefact editor-only est emis, corriger le producteur concerne, ajouter une regression ciblee et inclure explicitement ce fichier au commit de la tache.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ElementTransformGuardTests|FullyQualifiedName~WebViewContextMenuScriptTests|FullyQualifiedName~Ft100SceneExporterTests|FullyQualifiedName~StudioElementPlusContractTests|FullyQualifiedName~SceneClipboardTests"
```

Attendu : aucun chemin connu ne peut modifier X/Y d'une cible effective verrouillee, y compris par groupe.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs src/ScadaBuilderV2.App/MainWindow.xaml.cs src/ScadaBuilderV2.App/MainWindow.TableIntegration.cs src/ScadaBuilderV2.Rendering/PreviewDocument.cs src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs src/ScadaBuilderV2.Application/ElementStudio/ElementStudioComponentPackageFactory.cs tests/ScadaBuilderV2.Tests/ElementTransformGuardTests.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs tests/ScadaBuilderV2.Tests/StudioElementPlusContractTests.cs tests/ScadaBuilderV2.Tests/SceneClipboardTests.cs
git commit -m "feat: enforce element position lock across transforms"
```

---

## Phase 3 - Operations Domain Tableau avancees

### Task 5: Extraire structure, contenu et format dans des operations ciblees

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableModels.cs`
- Create: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableStructureOperations.cs`
- Create: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableContentOperations.cs`
- Create: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableFormatOperations.cs`
- Modify: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableOperations.cs`
- Modify: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableStyleResolver.cs`
- Create: `tests/ScadaBuilderV2.Tests/TableContentOperationsTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ScadaTableOperationsTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ScadaTableModelTests.cs`

**Interfaces:**
- Consumes: contenus/format nullable de `DEC-0039` et matrice section 7 de la spec.
- Produces/Changes: operations structurelles extraites, conversion de type deterministe, `TextWrap`, `LineHeight`, reset par propriete/portee et facade compatible.

- [ ] Ajouter `TextWrap` et `LineHeight` nullable sans changer les defaults historiques.
- [ ] Extraire `Merge`, `Unmerge`, `InsertRow`, `InsertColumn`, `DeleteRow` et `DeleteColumn` dans `ScadaTableStructureOperations`; conserver les signatures publiques historiques de `ScadaTableOperations` comme facade de compatibilite deleguant a la nouvelle classe.
- [ ] Implementer exactement les six conversions de la matrice Texte/Input texte/Input numerique, avec parsing/format invariant et suppression des champs caches incompatibles.
- [ ] Implementer `ApplyFormat`, `ResetProperty` et `ResetScope` pour Tableau, header, alternance, rangees, colonnes, cellule et plage.
- [ ] Faire appeler directement les classes structure/contenu/format ciblees par le coordinateur dans les taches suivantes; aucun nouvel appelant ne doit ajouter de logique a la facade historique.
- [ ] Tester conversions valides/invalides, portee multiple avec valeurs distinctes, reset nullable et precedence effective.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableContentOperationsTests|FullyQualifiedName~ScadaTableOperationsTests|FullyQualifiedName~ScadaTableModelTests"
```

Attendu : aucune conversion ne conserve un champ incompatible invisible et les anciens modeles rendent comme avant.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.Domain/Scenes/Tables tests/ScadaBuilderV2.Tests/TableContentOperationsTests.cs tests/ScadaBuilderV2.Tests/ScadaTableOperationsTests.cs tests/ScadaBuilderV2.Tests/ScadaTableModelTests.cs
git commit -m "feat: add advanced table content and format operations"
```

### Task 6: Ajouter les bordures physiques par segment

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableModels.cs`
- Create: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableBorderOperations.cs`
- Create: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableBorderResolver.cs`
- Create: `tests/ScadaBuilderV2.Tests/TableBorderOperationsTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ScadaTableModelTests.cs`

**Interfaces:**
- Consumes: grille logique, fusions et fallback `GridColor/GridWidth/GridStyle`.
- Produces/Changes: `ScadaTableBorder`, orientation, overrides unitaires et resolution effective partageable.

- [ ] Ajouter les records/enums de la section 9 et `BorderOverrides` nullable en fin de `ScadaTableDefinition`.
- [ ] Valider bornes de `GridLine`, `Segment`, style, couleur et epaisseur; normaliser les doublons vers une valeur canonique.
- [ ] Implementer les huit presets comme expansion en segments, sans persister le nom du preset.
- [ ] Faire conserver tous les segments lors de fusion, masquer les internes au resolver, puis les reveler a la defusion.
- [ ] Tester contours heterogenes, fallback historique, plage multi-cellules, fusion/defusion et JSON ancien/nouveau.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableBorderOperationsTests|FullyQualifiedName~ScadaTableModelTests|FullyQualifiedName~ScadaTableOperationsTests"
```

Attendu : chaque segment physique a une seule valeur effective et aucune bordure visible n'est perdue par fusion.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableModels.cs src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableBorderOperations.cs src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableBorderResolver.cs tests/ScadaBuilderV2.Tests/TableBorderOperationsTests.cs tests/ScadaBuilderV2.Tests/ScadaTableModelTests.cs
git commit -m "feat: add physical table border segments"
```

### Task 7: Ajouter pistes precises, auto-fit valide et en-tetes multiples

**Files:**
- Create: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableTrackOperations.cs`
- Create: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableHeaderOperations.cs`
- Modify: `src/ScadaBuilderV2.Application/Tables/TableEditCoordinator.cs`
- Create: `tests/ScadaBuilderV2.Tests/TableTrackOperationsTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs`

**Interfaces:**
- Consumes: pistes, plage, total cible et mesures typographiques deja calculees.
- Produces/Changes: uniformisation, distribution, auto-fit et prefixe `IsHeader` valide.

- [ ] Implementer `SetSize`, `Equalize`, `Distribute` et `ApplyAutoFit` avec minimums, correction d'arrondi et une seule definition resultat.
- [ ] Definir le DTO Application de mesure par cellule avec row/column/spans/dimensions desirees; refuser NaN, infini, spans/index invalides et liste incomplete.
- [ ] Distribuer le deficit des cellules fusionnees proportionnellement entre pistes couvertes.
- [ ] Implementer `SetHeaderRowCount(0..Rows.Count)` et `SetHeaderRows` en garantissant un prefixe consecutif.
- [ ] Etendre `TableEditKind`/`TableEditRequest` et le coordinateur pour appeler les nouvelles operations sans logique UI.
- [ ] Tester minimums, ratios, arrondis, mesures injectees, fusions, header 0/1/multiple et action atomique.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableTrackOperationsTests|FullyQualifiedName~TableEditCoordinatorTests"
```

Attendu : toutes les dimensions finales sont finies, valides et reproductibles sans dependance aux polices du runner.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableTrackOperations.cs src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableHeaderOperations.cs src/ScadaBuilderV2.Application/Tables/TableEditCoordinator.cs tests/ScadaBuilderV2.Tests/TableTrackOperationsTests.cs tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs
git commit -m "feat: add table tracks auto fit and headers"
```

---

## Phase 4 - Session et sous-surface Tableau

### Task 8: Creer la session d'authoring et le catalogue de la sous-surface

**Files:**
- Create: `src/ScadaBuilderV2.Application/Tables/TableAuthoringSession.cs`
- Create: `src/ScadaBuilderV2.Application/Tables/TableRibbonStateProvider.cs`
- Modify: `src/ScadaBuilderV2.Application/Commands/InsertToolCatalog.cs`
- Modify: `src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs`
- Create: `tests/ScadaBuilderV2.Tests/TableAuthoringSessionTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs`

**Interfaces:**
- Consumes: famille `data`, selection Element+, configuration 1..64 et descriptors de commandes.
- Produces/Changes: `ContextualSurface`, `table.add`, snapshot de session et groupes/disabled reasons.

- [ ] Ajouter `InsertPlacementMode.ContextualSurface` et migrer uniquement `insert.table` de `DialogThenPoint` vers ce mode; conserver le libelle `Tableau`.
- [ ] Implementer les transitions `OpenSurface`, `CloseSurface`, `ConfigureCreation`, `BeginPlacement`, modes Objet/Cellules, plage et portee.
- [ ] Definir les groupes Creation, Mode, Selection, Contenu, Structure, Format, Dimensions et En-tetes avec ids stables; enregistrer explicitement `table.mode.object` et `table.mode.cells` comme toggles mutuellement exclusifs.
- [ ] Maintenir `table.add` actif sans selection; desactiver les commandes contextuelles avec raisons precises.
- [ ] Tester toutes les transitions, Escape, retour Donnees, selection/non-selection Tableau, config 6 x 8 et remplacement d'un placement actif.
- [ ] Ajouter un test d'architecture par reflexion qui echoue si `TableAuthoringSession` contient un champ/propriete/parametre/retour `ScadaElement` mutable ou une reference a `TableEditCoordinator`; la session ne transporte que des ids et snapshots immuables.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableAuthoringSessionTests|FullyQualifiedName~RibbonCommandCatalogTests"
```

Attendu : aucune ouverture de surface ne cree un tableau ou n'affiche un dialogue.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.Application/Tables/TableAuthoringSession.cs src/ScadaBuilderV2.Application/Tables/TableRibbonStateProvider.cs src/ScadaBuilderV2.Application/Commands/InsertToolCatalog.cs src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs tests/ScadaBuilderV2.Tests/TableAuthoringSessionTests.cs tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs
git commit -m "feat: add contextual table authoring session"
```

### Task 9: Rendre la sous-surface WPF et retirer le dialogue de creation

**Files:**
- Create: `src/ScadaBuilderV2.App/TableEditor/TableRibbonViewModel.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableEditorController.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableDialogs.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.TableIntegration.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: session et `TableRibbonStateProvider` de la tache 8.
- Produces/Changes: groupes niveau 2 dans `RibbonCommandSurface`, creation `table.add` Point et retour Donnees.

- [ ] Faire traiter `insert.table` comme activation de sous-surface dans `ExecuteRibbonCommand`, sans appeler `BeginInsertToolPlacement`.
- [ ] Rendre le groupe Creation et ses controles Rangées/Colonnes/Premiere rangee d'en-tete dans le ruban secondaire existant; garder `InsertFamilySurface` compact visible.
- [ ] Faire armer le placement uniquement par `table.add`; utiliser la configuration de session lors du clic canvas puis selectionner le nouvel objet et entrer en mode Cellules.
- [ ] Avant le retrait, inventorier les consommateurs avec `rg -n "TableCreationDialog|TableCreationOptions|RequestCreationOptions" src tests`; apres migration de `table.add`, supprimer `RequestCreationOptions`, `TableCreationDialog` et `TableCreationOptions`, puis reexecuter le grep et attendre zero reference. Conserver `TablePropertiesDialog`, `CellFormatDialog` et les dialogues de dimensions d'un tableau existant.
- [ ] Ne pas voler un autre menu superieur lors d'une selection Tableau; ouvrir automatiquement la sous-surface seulement si `Inserer > Donnees` est deja actif.
- [ ] Limiter `MainWindow.TableIntegration.cs` aux methodes de delegation nommees par la spec.
- [ ] Tester absence de dialogue, bouton toujours disponible, placement/Escape, retour Donnees et frontiere MainWindow.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableUiArchitectureTests|FullyQualifiedName~WebViewContextMenuScriptTests|FullyQualifiedName~TableAuthoringSessionTests|FullyQualifiedName~RibbonCommandCatalogTests"
```

Attendu : `Donnees > Tableau` affiche les outils; `Ajouter un tableau` est le seul declencheur de creation.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.App/TableEditor/TableRibbonViewModel.cs src/ScadaBuilderV2.App/TableEditor/TableEditorController.cs src/ScadaBuilderV2.App/TableEditor/TableDialogs.cs src/ScadaBuilderV2.App/MainWindow.TableIntegration.cs src/ScadaBuilderV2.App/MainWindow.xaml src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: add table ribbon surface without creation dialog"
```

---

## Phase 5 - Bridge WebView, modes et performance

### Task 10: Ajouter l'adaptateur type et les gestes exclusifs Objet/Cellules

**Files:**
- Create: `src/ScadaBuilderV2.App/TableEditor/TableWebViewMessages.cs`
- Create: `src/ScadaBuilderV2.App/TableEditor/TableWebViewMessageAdapter.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableWebViewScript.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableEditorController.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Create: `tests/ScadaBuilderV2.Tests/TableWebViewMessageAdapterTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs`

**Interfaces:**
- Consumes: JSON `tableSelection`, `tableCellEdit`, `tableTrackResize`, session et table active.
- Produces/Changes: DTO valides, headers cliquables, plage et capture de gestes en mode Cellules.

- [ ] Implementer les schemas exacts de la spec avec parsing central, normalisation de plage et diagnostics sans mutation.
- [ ] Migrer les cases Tableau du switch general vers `ForwardTableWebViewMessage` puis `TableEditorController.HandleBridgeRequest`.
- [ ] Rendre headers de colonnes/rangees, coin tout-selectionner, cellule/plage et separateurs comme overlays editor-only.
- [ ] En mode Cellules, arreter la propagation avant le drag Element+; en mode Objet, ne pas intercepter cellules/separateurs.
- [ ] Implementer double-clic pour entrer en mode Cellules, Shift pour etendre, clic droit sans perdre la selection et Escape pour revenir au mode Objet.
- [ ] Coalescer un resize de piste en feedback live puis un seul `tableTrackResize` au pointer-up.
- [ ] Utiliser la delegation d'evenements au conteneur Tableau et supprimer les nouveaux listeners permanents par cellule.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableWebViewMessageAdapterTests|FullyQualifiedName~WebViewContextMenuScriptTests|FullyQualifiedName~TableUiArchitectureTests"
```

Attendu : une cellule est selectionnable/redimensionnable sans deplacer le tableau et le JSON brut ne traverse plus MainWindow.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.App/TableEditor/TableWebViewMessages.cs src/ScadaBuilderV2.App/TableEditor/TableWebViewMessageAdapter.cs src/ScadaBuilderV2.App/TableEditor/TableWebViewScript.cs src/ScadaBuilderV2.App/TableEditor/TableEditorController.cs src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/TableWebViewMessageAdapterTests.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs
git commit -m "feat: add typed table webview interaction modes"
```

### Task 11: Mesurer l'auto-fit dans WebView2 et proteger 64 x 64

**Files:**
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableWebViewScript.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableWebViewMessages.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableWebViewMessageAdapter.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableEditorController.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableWebViewMessageAdapterTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableTrackOperationsTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/TableWebViewPerformanceContractTests.cs`

**Interfaces:**
- Consumes: DOM style effectif et commande `Ajuster au contenu`.
- Produces/Changes: `tableAutoFitMeasured` par cellule, rendu batche et preuve de strategie performance.

- [ ] Mesurer valeur initiale/placeholder, padding et bordures apres application des styles; regrouper toutes les lectures avant les ecritures.
- [ ] Emettre `fitColumns`, `fitRows`, selection et cellules avec row/column/spans/desiredWidth/desiredHeight.
- [ ] Valider le DTO avant de construire une requete Application; aucune mesure DOM ne devient directement une mutation.
- [ ] Construire 4096 cellules via `DocumentFragment` et un seul `replaceChildren`; conserver un overlay borne pour selection/resize.
- [ ] Ajouter des tests de contrat qui interdisent un listener permanent par cellule et exigent le batching/document fragment.
- [ ] Executer les tests automatises puis le gate Release interactif sur une copie isolee : rendu initial <= 500 ms et feedback p95 <= 50 ms. Conserver pour la cloture la date, OS, CPU, RAM, commit/build Release, nombre d'echantillons, valeurs brutes ou resume statistique, rendu initial, p95 selection, p95 resize et conclusion conforme/non conforme.

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableWebViewMessageAdapterTests|FullyQualifiedName~TableTrackOperationsTests|FullyQualifiedName~TableWebViewPerformanceContractTests"
dotnet build ScadaBuilderV2.sln -c Release
```

Attendu : tests deterministes verts. Si le gate interactif echoue, ne pas declarer la tache terminee; optimiser ou ouvrir explicitement une tranche de virtualisation.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.App/TableEditor tests/ScadaBuilderV2.Tests/TableWebViewMessageAdapterTests.cs tests/ScadaBuilderV2.Tests/TableTrackOperationsTests.cs tests/ScadaBuilderV2.Tests/TableWebViewPerformanceContractTests.cs
git commit -m "perf: add table auto fit and 64x64 rendering guard"
```

---

## Phase 6 - Inspecteur, dialogues et commandes Tableau

### Task 12: Ajouter l'inspecteur complet de contenu et format par portee

**Files:**
- Create: `src/ScadaBuilderV2.App/TableEditor/TablePropertiesViewModel.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableDialogs.cs` (contient le `CellFormatDialog` existant a adapter, pas a recreer)
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml`
- Modify: `src/ScadaBuilderV2.App/MainWindow.TableIntegration.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs`

**Interfaces:**
- Consumes: session, valeurs locales/effectives, resolver de style et coordinateur stateless.
- Produces/Changes: editeur de type, scope `Appliquer a`, badges Herite/Personnalise/Mixte et reset nullable.

- [ ] Implementer un view model unique pour panneau droit, `TablePropertiesDialog` et `CellFormatDialog`.
- [ ] Exposer Texte/Input texte/Input numerique et tous leurs champs selon la matrice; ne pas ecraser les valeurs distinctes d'une multiselection sans modification explicite.
- [ ] Exposer les portees directes et les portees calculees Header/Alternance via `Appliquer a`, sans falsifier la selection physique.
- [ ] Afficher valeur effective et source `Herite de`, `Personnalise` ou `Mixte`; `ResetProperty` et `ResetScope` ecrivent `null` par une action unique.
- [ ] Ajouter police, taille, gras, italique, alignements, padding, couleurs, wrap et line-height en reutilisant le color picker existant.
- [ ] Faire transiter toutes les mutations par `TableEditRequest`; aucun dialogue ne construit directement une definition complete.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableUiArchitectureTests|FullyQualifiedName~TableEditCoordinatorTests|FullyQualifiedName~TableContentOperationsTests"
```

Attendu : panneau et dialogues affichent/appliquent exactement les memes valeurs et diagnostics.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.App/TableEditor/TablePropertiesViewModel.cs src/ScadaBuilderV2.App/TableEditor/TableDialogs.cs src/ScadaBuilderV2.App/MainWindow.xaml src/ScadaBuilderV2.App/MainWindow.TableIntegration.cs tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs
git commit -m "feat: add scoped table content and format inspector"
```

### Task 13: Ajouter bordures, dimensions, en-tetes et menu contextuel complet

**Files:**
- Modify: `src/ScadaBuilderV2.Application/Tables/TableContextMenuProvider.cs`
- Modify: `src/ScadaBuilderV2.Application/Tables/TableEditCoordinator.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TablePropertiesViewModel.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableEditorController.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableDialogs.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml`
- Modify: `tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableBorderOperationsTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableTrackOperationsTests.cs`

**Interfaces:**
- Consumes: bordures/pistes/en-tetes Domain, plage/session et clipboard existant.
- Produces/Changes: presets de bordure, dimensions numeriques, auto-fit, header count et commandes de contexte.

- [ ] Ajouter aux ruban/panneau/dialogue les huit presets, style/couleur/epaisseur et application par cote/contour/interieur.
- [ ] Exposer largeur/hauteur de pistes, uniformiser, distribuer, auto-fit et X/Y/W/H; soumettre X/Y au guard de lock et les dimensions internes au coordinateur Tableau.
- [ ] Exposer nombre de rangees d'en-tete, marquer/demarquer et fusion de titres dans les limites du prefixe consecutif.
- [ ] Etendre le menu clic droit pour cellule/plage/header : copier, coller, inserer/supprimer piste, clear, format, taille, merge/unmerge et header.
- [ ] Conserver la selection au clic droit et afficher une raison de desactivation issue d'Application.
- [ ] Conserver `tests/ScadaBuilderV2.Tests/TableClipboardTests.cs` sans modification et verifier explicitement que cette regression `DEC-0039` reste verte.
- [ ] Tester chaque scope, preset, menu, dialogue numerique, undo/redo et refus sans mutation.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableUiArchitectureTests|FullyQualifiedName~TableEditCoordinatorTests|FullyQualifiedName~TableBorderOperationsTests|FullyQualifiedName~TableTrackOperationsTests|FullyQualifiedName~TableClipboardTests"
```

Attendu : tous les outils indispensables de la spec sont atteignables depuis au moins une surface coherente.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.Application/Tables/TableContextMenuProvider.cs src/ScadaBuilderV2.Application/Tables/TableEditCoordinator.cs src/ScadaBuilderV2.App/TableEditor src/ScadaBuilderV2.App/MainWindow.xaml tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs tests/ScadaBuilderV2.Tests/TableBorderOperationsTests.cs tests/ScadaBuilderV2.Tests/TableTrackOperationsTests.cs
git commit -m "feat: add complete table border dimension and header tools"
```

---

## Phase 7 - Rendering, integration et cloture

### Task 14: Synchroniser Rendering, round-trip et scenario `win00012`

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/ModernTableHtmlRenderer.cs`
- Modify: `src/ScadaBuilderV2.Rendering/PreviewDocument.cs`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/AdvancedTableAuthoringIntegrationTests.cs`

**Interfaces:**
- Consumes: format effectif, border resolver, headers, contenu et scene lock.
- Produces/Changes: HTML/CSS page-scope identique preview/export et preuve end-to-end.

- [ ] Rendre wrap, line-height et chaque segment de bordure avec CSS page-scope, sans fusionner arbitrairement des segments heterogenes.
- [ ] Rendre toutes les rangees `IsHeader` en `<th>` avec spans valides; conserver inputs natifs et absence de binding.
- [ ] Verifier round-trip d'une table avec contenus mixtes, scopes, bordures, headers, dimensions et `IsLocked`.
- [ ] Construire en test une grille 16 x 10 representative des capacites necessaires a `win00012` : contenus Texte/Input texte/Input numerique melanges, au moins deux rangees d'en-tete, un titre de section fusionne, largeurs/hauteurs non uniformes, bandes alternees et bordures exterieures/interieures/par cellule heterogenes. Verifier creation -> edition -> save/reload -> preview -> `.sb2`; ce scenario structurel n'est ni une conversion automatique ni une reproduction des quelque 593 objets legacy de la page complete.
- [ ] Inspecter le HTML exporte pour exclure `IsLocked`, `data-editor-locked`, headers/gouttieres d'authoring, overlays et handles.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~AdvancedTableAuthoringIntegrationTests|FullyQualifiedName~Ft100SceneExporterTests|FullyQualifiedName~ModernProjectStoreTests|FullyQualifiedName~ScadaTable"
```

Attendu : le scenario structurel equivalent a `win00012` survit au round-trip et s'exporte via le contrat `.sb2` actuel.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.Rendering tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs tests/ScadaBuilderV2.Tests/AdvancedTableAuthoringIntegrationTests.cs
git commit -m "feat: render advanced tables through sb2 pipeline"
```

### Task 15: Executer la validation complete et synchroniser la documentation

**Files:**
- Modify: `docs/00_governance/DECISION_REGISTER_V2.md`
- Modify: `docs/02_architecture/MODULE_BOUNDARIES_V2.md`
- Modify: `docs/03_runtime_contracts/PREVIEW_BUILD_EXPORT_CONTRACT_V2.md`
- Modify: `docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md`
- Modify: `docs/04_editor/COMMANDS_CONTRACT_V2.md`
- Modify: `docs/04_editor/STATE_MANAGEMENT_CONTRACT_V2.md`
- Modify: `docs/04_editor/SELECTION_CONTRACT_V2.md`
- Modify: `docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md`
- Modify: `docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md`
- Modify: `docs/05_studio_element_plus/STUDIO_ELEMENT_PLUS_SEP_CONTRACT_V2.md`
- Modify: `docs/06_ui_ux/UI_ARCHITECTURE_V2.md`
- Modify: `docs/06_ui_ux/UI_SPECIFICATION_V2.md`
- Modify: `docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md`
- Modify: `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md`
- Modify: `docs/08_implementation_status/KNOWN_GAPS_V2.md` only for gaps reels restants
- Modify: `docs/10_generated/CODE_MAP_V2.md`
- Modify: `docs/10_generated/MODULE_FUNCTION_INDEX_V2.md`
- Modify: `docs/10_generated/COMMAND_FLOW_DIAGRAM_V2.md`
- Modify: `docs/10_generated/STATE_FLOW_DIAGRAM_V2.md`
- Modify: `docs/10_generated/EXPORT_FLOW_DIAGRAM_V2.md`
- Modify: `docs/10_generated/STUDIO_ELEMENT_PLUS_FLOW_DIAGRAM_V2.md` only if the generated `.sep` flow changes
- Modify: `docs/README.md`
- Modify: `docs/superpowers/specs/2026-07-15-table-ui-authoring-and-element-lock-design.md`
- Modify: this plan
- Modify: `VERSION` selon la decision de version au moment de la cloture

**Interfaces:**
- Consumes: commits, tests et preuves interactives des taches 1 a 14.
- Produces/Changes: statut implemente, couverture, diagrammes, gaps reels et version tracable.

- [ ] Executer build, tests cibles puis suite complete :

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~Table|FullyQualifiedName~ElementLock|FullyQualifiedName~ElementTransformGuard|FullyQualifiedName~ScadaSceneGroup|FullyQualifiedName~RibbonCommandCatalog|FullyQualifiedName~WebViewContextMenuScript|FullyQualifiedName~Ft100SceneExporter|FullyQualifiedName~ModernProjectStore"
dotnet test ScadaBuilderV2.sln --no-restore
```

Attendu : tests cibles verts; suite complete egale ou meilleure que le baseline frais. Toute nouvelle regression bloque la cloture.

- [ ] Effectuer sur une copie isolee le smoke WPF : ouvrir sous-surface Tableau, creer 6 x 8 et 16 x 10, selectionner cellules/headers, resize pistes, formats/bordures, merge/unmerge, auto-fit, lock simple/mixte/groupe, reload et inspection `.sb2`/`.sep`.
- [ ] Consigner dans `IMPLEMENTED_FEATURES_V2.md` ou `REGRESSION_COVERAGE_V2.md` la preuve 64 x 64 collectee a la tache 11 : date, machine, OS, CPU, RAM, commit/build Release, echantillonnage, temps de rendu initial, p95 selection, p95 resize et conclusion. Ne jamais ouvrir/sauvegarder le projet utilisateur pendant ce smoke.
- [ ] Mettre `DEC-0040`, la spec et le plan au statut implemente seulement si les preuves requises existent; sinon documenter exactement le gap.
- [ ] Synchroniser contrats proprietaires, diagrammes Mermaid, cartes generees, XML docs et regression coverage.
- [ ] Appliquer un bump d'iteration via `scada-builder-v2-versioning` a partir de la version presente au moment de la cloture; ne pas reutiliser aveuglement `V2.1.4.0024`.
- [ ] Executer :

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
rg -n "index\.html|08_web_modernized|source_html|Open[ ]Decisions|Document version|Historique des changements|PENDING" docs
git diff --check
git status --short
```

Attendu : aucun nouvel ecart documentaire attribuable a la tranche, aucun fichier utilisateur stage et tous les gaps historiques separes du statut de cette implementation.

- [ ] Commit :

```powershell
git add VERSION
git add docs/00_governance/DECISION_REGISTER_V2.md docs/02_architecture/MODULE_BOUNDARIES_V2.md
git add docs/03_runtime_contracts/PREVIEW_BUILD_EXPORT_CONTRACT_V2.md docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md
git add docs/04_editor/COMMANDS_CONTRACT_V2.md docs/04_editor/STATE_MANAGEMENT_CONTRACT_V2.md docs/04_editor/SELECTION_CONTRACT_V2.md docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md
git add docs/05_studio_element_plus/STUDIO_ELEMENT_PLUS_SEP_CONTRACT_V2.md docs/06_ui_ux/UI_ARCHITECTURE_V2.md docs/06_ui_ux/UI_SPECIFICATION_V2.md
git add docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md docs/08_implementation_status/REGRESSION_COVERAGE_V2.md docs/08_implementation_status/KNOWN_GAPS_V2.md
git add docs/10_generated/CODE_MAP_V2.md docs/10_generated/MODULE_FUNCTION_INDEX_V2.md docs/10_generated/COMMAND_FLOW_DIAGRAM_V2.md docs/10_generated/STATE_FLOW_DIAGRAM_V2.md docs/10_generated/EXPORT_FLOW_DIAGRAM_V2.md docs/10_generated/STUDIO_ELEMENT_PLUS_FLOW_DIAGRAM_V2.md
git add docs/README.md docs/superpowers/specs/2026-07-15-table-ui-authoring-and-element-lock-design.md docs/superpowers/plans/2026-07-15-table-ui-authoring-and-element-lock.md
git diff --cached --name-only
git commit -m "docs: record advanced table and element lock implementation"
```

Attendu avant commit : la liste stagee correspond uniquement aux fichiers effectivement modifies par la tranche; aucun `git add docs` global et aucun fichier sous `projects/AMR_REF_SCADA_V2`.

---

## Validation Checklist

- [ ] Specification approuvee et `DEC-0040` respectes sans redefinir l'architecture dans le plan.
- [ ] `insert.table` ouvre la sous-surface et `table.add` cree sans dialogue.
- [ ] Modes Objet/Cellules exclusifs; selection et resize internes ne deplacent pas le Tableau.
- [ ] Types Texte/Input texte/Input numerique et matrice de conversion couverts.
- [ ] Tableau, headers, alternance, rangee, colonne, cellule et plage sont des portees atteignables.
- [ ] Valeurs effectives, Herite/Personnalise/Mixte et reset nullable sont coherents entre panneau/dialogues.
- [ ] Bordures exterieures/interieures/par cote et fusions heterogenes round-trip/export.
- [ ] Dimensions numeriques, uniformisation, distribution et auto-fit WebView valides.
- [ ] Plusieurs rangees d'en-tete consecutives rendent des `<th>` valides.
- [ ] `IsLocked` survit scene save/reload, clipboard, groupe et undo/redo.
- [ ] `object.lock` est la seule commande main-editor; ancien verrou de selection retire sans toucher au Studio.
- [ ] Etat simple/tous/mixte identique dans ruban Selection, Propriete et Lock superieur.
- [ ] Drag, clavier, X/Y et mouvements/resizes de groupe ne peuvent deplacer une cible verrouillee.
- [ ] Selection, W/H simple, rotation, contenu/style/events et edition interne d'un Tableau verrouille restent possibles.
- [ ] Preview et `.sb2` partagent formats/bordures sans artefact editor-only; `.sep` ne transporte pas le verrou de scene.
- [ ] Grille 64 x 64 respecte les budgets approuves ou la tranche reste explicitement incomplete.
- [ ] Scenario 16 x 10 equivalent a `win00012` sans `ValueBindings` valide end-to-end.
- [ ] `MainWindow` ne contient que les points de delegation autorises.
- [ ] Aucun fichier sous `projects/AMR_REF_SCADA_V2` n'est modifie, restaure, supprime ou stage par l'implementation.
- [ ] Build, tests cibles, suite complete, docs verifier et smoke isole sont consignes.
