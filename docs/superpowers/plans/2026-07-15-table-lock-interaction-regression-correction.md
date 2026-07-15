# Correction des interactions Tableau et du verrou Element+ - Plan d'implementation

Date: 2026-07-15
Status: Approved - ready for execution
Document version: `V2.1.4.0033`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-15 | `V2.1.4.0033` | `e811253` | Approbation utilisateur recue; execution des tranches autorisee. |
| 2026-07-15 | `V2.1.4.0032` | `ff21e33` | Plan correctif autonome derive de la specification de regression Tableau/verrou. |

> **Pour les agents d'execution :** executer ce plan tache par tache. Ne pas deleguer a des sous-agents sans autorisation explicite. Chaque tache commence par un test en echec, se termine par les validations indiquees et possede sa propre frontiere de commit.

**Goal:** Supprimer tout mouvement visuel ou persistant d'un Element+ verrouille, rendre un Tableau neuf immediatement positionnable, restaurer l'acces fiable aux cellules et pistes, et aligner `Afficher/Masquer A/1` sur l'etat reel.

**Architecture:** Application reste proprietaire des transitions de session Tableau et du guard final. App projette le modele vers des DTO editor-only testables et synchronise un snapshot unique vers WebView2. JavaScript arbitre les gestes avant tout apercu, sans devenir proprietaire du verrou ou du modele. Rendering et les contrats `.sb2`/`.sep` restent inchanges.

**Tech Stack:** C# 12, .NET 8, WPF/WebView2, JavaScript embarque, `System.Text.Json`, MSTest, PowerShell

## Global Constraints

- Specification proprietaire : `docs/superpowers/specs/2026-07-15-table-lock-interaction-regression-correction-design.md`.
- Ne pas modifier la specification ou le plan implemente de `DEC-0040`.
- Ne pas commencer l'implementation avant approbation de la specification corrective et enregistrement d'une nouvelle decision.
- Le verrou protege X/Y uniquement; les capacites internes du Tableau restent disponibles.
- Le refus doit arriver dans le WebView avant `modernDrag` et rester revalide par `ElementTransformGuard`.
- Aucun etat de mode, guide, selection ou verrou d'editeur ne doit entrer dans `.sb2` ou `.sep`.
- `MainWindow` reste un hote de haut niveau; les DTO et calculs testables sont dans des classes dediees.
- Toute mutation acceptee conserve l'undo/redo existant; tout refus produit zero historique et zero dirty state.
- Toute API publique ajoutee recoit une documentation XML citant la nouvelle decision, la specification et les tests.
- Ne jamais modifier, restaurer, supprimer ou stager `projects/AMR_REF_SCADA_V2`.

---

## Before You Start

- [ ] Verifier que la specification corrective est `Approved` et que sa nouvelle decision est enregistree.

```powershell
rg -n "Status: Approved|DEC-" docs/superpowers/specs/2026-07-15-table-lock-interaction-regression-correction-design.md docs/00_governance/DECISION_REGISTER_V2.md
```

- [ ] Verifier branche et worktree.

```powershell
git branch --show-current
git status --short
```

Attendu : branche `codex/table-tool-implementation` ou branche corrective issue de celle-ci. Le worktree contient actuellement un prototype non committe dans les fichiers Tableau/verrou et leurs tests. Ne pas le perdre, le restaurer ou le committer en bloc. Avant execution, l'isoler dans un worktree/commit temporaire autorise ou le reconciler fichier par fichier avec les tests rouges de ce plan. Aucun fichier utilisateur ne doit etre stage.

- [ ] Capturer un baseline frais avant modification.

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore
```

Attendu : consigner le resultat reel. Ne pas reutiliser un ancien nombre global de tests.

- [ ] Confirmer les causes sur le commit de depart.

```powershell
rg -n "ModernElementRenderPayload|ToRenderPayload|editorLocked|CompletePlacement|ShowEditorGuides|SetTableInteractionModeInWebViewAsync" src tests
```

Attendu : le payload ne transporte pas encore effectivement `IsLocked`, la creation entre en mode Cellules, et le mode/guides sont synchronises separement. Si le code de depart a deja diverge, adapter les tests sans changer les comportements approuves.

---

## Phase 1 - Projection editor-only fiable

### Task 1: Extraire et tester le payload Element+ avec `IsLocked`

**Files:**
- Create: `src/ScadaBuilderV2.App/EditorBridge/ModernElementRenderPayload.cs`
- Create: `src/ScadaBuilderV2.App/EditorBridge/ModernElementRenderPayloadFactory.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Create: `tests/ScadaBuilderV2.Tests/ModernElementRenderPayloadFactoryTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `ScadaElement`, ids selectionnes, index de rendu.
- Produces: payload editor-only recursif avec `IsLocked`, sans dependance WPF.

- [ ] Ecrire d'abord des tests qui construisent un element verrouille, un element deverrouille et un groupe avec enfant verrouille; serialiser le payload reel et verifier chaque valeur `IsLocked`.
- [ ] Creer le DTO dedie et `ModernElementRenderPayloadFactory.Create(ScadaElement, IReadOnlySet<string>, int)` en deplacant la projection actuellement privee dans `MainWindow`.
- [ ] Projeter `IsLocked` recursivement pour racines et descendants, sans calculer un verrou effectif different du modele.
- [ ] Remplacer `ToRenderPayload` par un appel de factory et retirer le type correspondant de `MainWindow.NestedTypes.cs`.
- [ ] Garder `data-editor-locked` editor-only; ajouter une regression qui prouve que le script le lit depuis le payload reel, pas seulement qu'une chaine JavaScript existe.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ModernElementRenderPayloadFactoryTests|Name=LockedElementMovementIsRejectedBeforeVisualDragStarts"
```

Attendu : le JSON contient `IsLocked:true` pour chaque cible verrouillee et `false` pour les autres.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.App/EditorBridge src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/ModernElementRenderPayloadFactoryTests.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "fix: project element lock state into editor payload"
```

---

## Phase 2 - Refus des gestes interdits avant apercu

### Task 2: Bloquer move et resize traductif dans le WebView

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`
- Modify: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ElementTransformGuardTests.cs`

**Interfaces:**
- Consumes: `data-editor-locked`, type de cible, handle de resize et fermeture DOM de groupe.
- Produces: decision locale de demarrer ou non `modernDrag`; geometrie preview a X/Y invariants.

- [ ] Ajouter des tests rouges d'ordre qui exigent le refus avant creation de `modernDrag` et avant pointer capture pour move, resize nord/ouest et resize de groupe avec descendant verrouille.
- [ ] Centraliser dans le script les predicates `blocksPositionMove`, `isPositionChangingResize` et `containsLockedDescendant`.
- [ ] Refuser tout move si une cible effective est verrouillee.
- [ ] Pour un objet simple verrouille, permettre seulement les handles est, sud et sud-est; refuser les handles qui exigent un changement de X/Y.
- [ ] Refuser avant apercu un resize de groupe si le groupe ou un descendant verrouille changerait de position.
- [ ] Stocker `positionLocked` dans l'etat de drag autorise et forcer X/Y aux valeurs initiales pendant chaque preview de resize comme defense complementaire.
- [ ] Verifier que la rotation conserve les X/Y du modele et que le guard Application demeure appele au commit.
- [ ] Etendre `ElementTransformGuardTests` uniquement pour les cas contractuels manquants : W/H a X/Y fixes accepte, X/Y modifie refuse, groupe avec descendant deplace refuse.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~WebViewContextMenuScriptTests|FullyQualifiedName~ElementTransformGuardTests"
```

Attendu : aucun geste interdit ne produit d'apercu; le guard final refuse toujours un message forge.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs tests/ScadaBuilderV2.Tests/ElementTransformGuardTests.cs
git commit -m "fix: prevent locked transform previews"
```

---

## Phase 3 - Etat Tableau deterministe

### Task 3: Corriger creation, reselection et reperes A/1

**Files:**
- Modify: `src/ScadaBuilderV2.Application/Tables/TableAuthoringSession.cs`
- Modify: `src/ScadaBuilderV2.Application/Tables/TableRibbonStateProvider.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableAuthoringSessionTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs`

**Interfaces:**
- Consumes: transitions de creation/selection et preference A/1.
- Produces: mode Objet/Cellules et `EditorGuidesVisible` deterministes.

- [ ] Ecrire les tests rouges de la matrice : placement -> Objet; deselection -> Objet; reselection -> Objet; passage explicite Cellules; A/1 depuis Objet -> Cellules visible; masquer en Cellules; retour Objet -> invisible.
- [ ] Modifier `CompletePlacement` pour terminer en mode Objet.
- [ ] Faire de `SelectTable` une transition explicite : nouvelle selection ou reselection apres absence -> Objet; une simple actualisation du meme id ne doit pas casser un mode Cellules actif.
- [ ] Ajouter `EditorGuidesVisible => Mode == Cells && ShowEditorGuides`.
- [ ] Faire entrer `ToggleEditorGuides` en mode Cellules avec guides actifs lorsqu'elle est invoquee depuis Objet; en mode Cellules, basculer seulement la preference.
- [ ] Faire utiliser `EditorGuidesVisible` par `TableRibbonStateProvider.Build` pour libelle, pressed state et aide.
- [ ] Verifier qu'aucune mutation de lock n'appelle `SetMode` ou ne derive le mode depuis `IsLocked`.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableAuthoringSessionTests|FullyQualifiedName~RibbonCommandCatalogTests"
```

Attendu : les transitions sont pures, reproductibles et independantes du verrou.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.Application/Tables/TableAuthoringSession.cs src/ScadaBuilderV2.Application/Tables/TableRibbonStateProvider.cs tests/ScadaBuilderV2.Tests/TableAuthoringSessionTests.cs tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs
git commit -m "fix: make table interaction state deterministic"
```

### Task 4: Synchroniser mode et guides atomiquement dans WebView2

**Files:**
- Create: `src/ScadaBuilderV2.App/TableEditor/TableEditorWebViewState.cs`
- Create: `src/ScadaBuilderV2.App/TableEditor/TableEditorWebViewStateFactory.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableWebViewScript.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.TableIntegration.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Create: `tests/ScadaBuilderV2.Tests/TableEditorWebViewStateTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: snapshot `TableAuthoringSession`.
- Produces: `TableEditorWebViewState` et une seule operation DOM `setEditorState(mode, guides)`.

- [ ] Tester les quatre couples Objet/Cellules x preference A/1 et la serialisation JavaScript echappee.
- [ ] Creer le record et la factory stateless; ne pas exposer le modele mutable.
- [ ] Ajouter `window.scadaModernTable.setEditorState(mode, guides)` qui applique mode et visibilite effective dans le meme tour JavaScript.
- [ ] Remplacer les envois separes par `SyncTableEditorStateInWebViewAsync` dans `MainWindow.TableIntegration.cs`.
- [ ] Appeler la synchronisation apres creation, selection/deselection, commande Objet/Cellules, commande A/1, fermeture de surface et rerender apres lock.
- [ ] Garantir que le lock ne force jamais Objet ou Cellules.
- [ ] Verifier les priorites de pointeur : en Cells, cellule/en-tete/separateur consomme l'evenement avant le drag Element+; en Object, le corps du Tableau appartient au wrapper.
- [ ] Verifier double-clic et Escape, y compris avec `data-editor-locked=true`.
- [ ] Maintenir `MainWindow` au niveau delegation seulement; le test d'architecture doit refuser le retour de calculs inline de mode/guides.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableEditorWebViewStateTests|FullyQualifiedName~TableUiArchitectureTests|FullyQualifiedName~WebViewContextMenuScriptTests|FullyQualifiedName~TableAuthoringSessionTests"
```

Attendu : un Tableau verrouille en mode Cellules accepte selection et resize de pistes sans demarrer de drag Element+.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.App/TableEditor/TableEditorWebViewState.cs src/ScadaBuilderV2.App/TableEditor/TableEditorWebViewStateFactory.cs src/ScadaBuilderV2.App/TableEditor/TableWebViewScript.cs src/ScadaBuilderV2.App/MainWindow.TableIntegration.cs src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/TableEditorWebViewStateTests.cs tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "fix: synchronize table mode and editor guides"
```

---

## Phase 4 - Compatibilite et cloture

### Task 5: Valider export, workflow interactif et documentation

**Files:**
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/StudioElementPlusContractTests.cs`
- Modify: `docs/00_governance/DECISION_REGISTER_V2.md`
- Modify: `docs/04_editor/SELECTION_CONTRACT_V2.md`
- Modify: `docs/04_editor/STATE_MANAGEMENT_CONTRACT_V2.md`
- Modify: `docs/06_ui_ux/UI_SPECIFICATION_V2.md`
- Modify: `docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md`
- Modify: `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md`
- Modify: `docs/08_implementation_status/KNOWN_GAPS_V2.md` only if a gap reel remains
- Modify: `docs/README.md`
- Modify: `docs/superpowers/specs/2026-07-15-table-lock-interaction-regression-correction-design.md`
- Modify: this plan
- Modify: `VERSION`

**Interfaces:**
- Consumes: implementation et preuves automatisees/interactives.
- Produces: decision active, contrats synchronises et statut exact.

- [ ] Renforcer les regressions d'export : absence de `data-editor-locked`, DTO editor-only, A/1, handles et overlays dans `.sb2` et `.sep`.
- [ ] Executer build et tests cibles :

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ModernElementRenderPayloadFactory|FullyQualifiedName~TableAuthoringSession|FullyQualifiedName~TableEditorWebViewState|FullyQualifiedName~TableUiArchitecture|FullyQualifiedName~WebViewContextMenuScript|FullyQualifiedName~ElementTransformGuard|FullyQualifiedName~Ft100SceneExporter|FullyQualifiedName~StudioElementPlusContract"
```

- [ ] Executer la suite complete et comparer au baseline frais :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore
```

- [ ] Executer le smoke interactif exact de la section 7.2 de la specification sur une copie isolee. Consigner resultat, date, branche/commit et toute divergence.
- [ ] Inspecter une archive `.sb2` et un `.sep` produits par le smoke.
- [ ] Enregistrer la nouvelle decision; ne pas modifier `DEC-0040`, mais indiquer que la nouvelle decision remplace ses seuls comportements d'interaction explicitement listes.
- [ ] Synchroniser les contrats proprietaires, couverture, statut et gaps. Marquer spec/plan `Implemented` seulement apres le smoke reussi.
- [ ] Appliquer le bump requis depuis la version presente au moment de la cloture; ne pas reutiliser aveuglement `V2.1.4.0032`.
- [ ] Executer :

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
git diff --check
git status --short
```

Attendu : aucune nouvelle erreur documentaire, aucun artefact editor-only exporte et aucun fichier utilisateur stage.

- [ ] Commit :

```powershell
git add VERSION docs/00_governance/DECISION_REGISTER_V2.md docs/04_editor/SELECTION_CONTRACT_V2.md docs/04_editor/STATE_MANAGEMENT_CONTRACT_V2.md docs/06_ui_ux/UI_SPECIFICATION_V2.md docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md docs/08_implementation_status/REGRESSION_COVERAGE_V2.md docs/08_implementation_status/KNOWN_GAPS_V2.md docs/README.md docs/superpowers/specs/2026-07-15-table-lock-interaction-regression-correction-design.md docs/superpowers/plans/2026-07-15-table-lock-interaction-regression-correction.md tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs tests/ScadaBuilderV2.Tests/StudioElementPlusContractTests.cs
git diff --cached --name-only
git commit -m "docs: record table lock interaction corrections"
```

---

## Validation Checklist

- [ ] Specification corrective approuvee et nouvelle decision enregistree sans modifier `DEC-0040`.
- [ ] Le payload editor-only transporte `IsLocked` recursivement et un test serialise le DTO reel.
- [ ] Aucun drag verrouille ne demarre ou ne deplace visuellement le wrapper.
- [ ] Les handles de resize verrouille respectent X/Y invariants; les transformations de groupe interdites n'ont pas d'apercu.
- [ ] `ElementTransformGuard` reste la defense finale contre les messages forges ou chemins secondaires.
- [ ] Un Tableau neuf termine en mode Objet et se deplace s'il est deverrouille.
- [ ] Deselection/reselection, Objet/Cellules, double-clic et Escape sont deterministes.
- [ ] Le verrou ne change jamais le mode Tableau.
- [ ] Cellules, headers et separateurs restent utilisables sur un Tableau verrouille en mode Cellules.
- [ ] `Afficher/Masquer A/1` reflete `EditorGuidesVisible` et agit immediatement.
- [ ] Mode et guides sont synchronises dans une seule operation WebView.
- [ ] Aucun calcul de politique n'est ajoute a `MainWindow`.
- [ ] `.sb2`, `.sep` et TF100Web restent inchanges.
- [ ] Build, tests cibles, suite complete, docs verifier et smoke interactif sont consignes.
- [ ] Aucun fichier sous `projects/AMR_REF_SCADA_V2` n'est modifie ou stage.
