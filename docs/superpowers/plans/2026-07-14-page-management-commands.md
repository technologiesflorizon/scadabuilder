# Gestion moderne des pages — Plan d’implémentation

Date: 2026-07-14
Status: Draft implementation plan — pending execution approval
Document version: `V2.1.4.0010`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-14 | `V2.1.4.0010` | `PENDING` | Création du plan exécutable dérivé de la spécification approuvée des commandes et du modèle moderne de pages. |

> Ce plan est dérivé de la spécification `docs/superpowers/specs/2026-07-14-page-commands-design.md` (`V2.1.4.0009`, D1–D38). Aucune décision produit n’y reste ouverte.

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Livrer une gestion moderne et cohérente des pages depuis le ruban `Pages`, le panneau `Projet > Pages` et le menu contextuel, avec identité interne immuable, undo/redo au niveau projet, persistance atomique, diagnostics structurés, pages natives et duplication Wonderware fidèle, sans modifier le contrat exporté `.sb2`.

**Architecture:** Le Domain porte l’identité et les invariants de page; Application porte les commandes asynchrones, l’analyse de dépendances, le coordinateur et l’historique du workspace; Infrastructure porte la migration et la sauvegarde atomique; Rendering résout `PageKey` vers `PageCode` et rend les pages natives; App/WPF adapte les mêmes commandes aux trois surfaces. `MainWindow` ne conserve que le câblage du shell et du WebView.

**Tech Stack:** C# 12, .NET 8, WPF/AvalonDock/WebView2, JSON `System.Text.Json`, MSTest, PowerShell, export ZIP `.sb2`

## Global Constraints

- Spec propriétaire: `docs/superpowers/specs/2026-07-14-page-commands-design.md` (D1–D38).
- Le ruban se nomme `Pages`; les commandes de cycle de vie stables sont `page.new`, `page.rename`, `page.duplicate` et `page.delete`.
- `PageKey` est un GUID interne immuable et invisible. `PageCode` est visible, modifiable et demeure l’identité humaine exportée.
- Les GUID ne doivent apparaître ni dans le manifeste `.sb2`, ni dans les dossiers de pages, ni dans les racines DOM, ni dans `TargetPageId`.
- Une nouvelle page `Default` utilise `IncludeInBuild = false` par défaut.
- Une duplication importée conserve automatiquement la projection et la provenance Wonderware, même avec un nouveau `PageCode`.
- Une page native doit être prévisualisable et exportable sans entrée correspondante dans `_referenceProject.Pages`.
- Les trois surfaces WPF appellent les mêmes `IApplicationCommand`; aucune règle métier ne doit être dupliquée dans les handlers.
- `IApplicationCommand.ExecuteAsync` est non réentrant, annulable et distinct de `ScadaCommandBinding` runtime.
- L’historique est porté au niveau workspace/projet; supprimer ou fermer une scène ne détruit pas la capacité d’annuler une action de page.
- La suppression est bloquée par les dépendances et ne répare jamais silencieusement les références.
- La sauvegarde d’un snapshot projet/scènes est transactionnelle avec rollback et récupération après interruption.
- Preview, build et export consomment le même modèle V2; aucun artefact d’éditeur ou diagnostic ne devient de la géométrie `.sb2`.
- Les droits d’accès complets, les dossiers de pages, le drag-and-drop et la bibliothèque complète de modèles restent hors périmètre.
- Les outils créatifs de tableau/grille restent hors périmètre de ce plan.
- Aucun changement n’est autorisé dans le projet Wonderware source ou dans le dépôt TF100Web pour cette tranche.
- Toute API publique ajoutée reçoit une documentation XML avec `Decisions:`, `Contracts:` et `Tests:` dans `<remarks>`.

---

## Before You Start

- [ ] Vérifier la branche et l’état de travail.

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git status --short --branch
```

Attendu avant code: branche dédiée `codex/page-commands-creative-tools-spec`; la spec, le présent plan, leur entrée d’index et `DEC-0038` sont les seuls changements de planification non validés. Les committer ensemble avant toute modification de code, puis confirmer un worktree propre.

- [ ] Créer le commit de référence spec + plan.

```bash
git add docs/README.md docs/00_governance/DECISION_REGISTER_V2.md docs/superpowers/specs/2026-07-14-page-commands-design.md docs/superpowers/plans/2026-07-14-page-management-commands.md
git commit -m "docs: approve page management specification and implementation plan"
```

- [ ] Capturer une baseline fraîche sans modifier les projets de référence.

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
```

Attendu: consigner le résultat réel. Toute défaillance préexistante est nommée dans le journal d’exécution; aucun nouveau test ne peut être déclaré conforme en se basant sur un ancien décompte.

- [ ] Copier un projet moderne existant dans un répertoire temporaire pour tous les essais de migration et de rollback. Ne pas exécuter une première migration directement sur `projects/AMR_REF_SCADA_V2`.

---

## Phase 1 — Contrats et identité de page

### Task 1: Aligner les contrats propriétaires avec `DEC-0038`

**Files:**

- Modify: `docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md`
- Modify: `docs/04_editor/COMMANDS_CONTRACT_V2.md`
- Modify: `docs/04_editor/STATE_MANAGEMENT_CONTRACT_V2.md`
- Modify: `docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md`
- Modify: `docs/README.md`

**Interfaces:**

- Consumes: décisions D1–D38 de la spec.
- Produces: contrats propriétaires alignés sur `DEC-0038` et routage documentaire vérifié.

- [ ] **Step 1: Vérifier le périmètre de `DEC-0038`.**

La décision enregistrée avec la spec et le plan doit rester l’autorité pour `PageKey`/`PageCode`, les références internes par clé, l’export humain inchangé, la provenance Wonderware, les commandes partagées, l’historique projet, la persistance atomique et les diagnostics structurés.

- [ ] **Step 2: Mettre à jour uniquement les contrats propriétaires.**

Ajouter les règles de modèle dans `PROJECT_MODEL_CONTRACT_V2.md`, le contrat asynchrone et les ids `page.*` dans `COMMANDS_CONTRACT_V2.md`, la portée projet de l’historique dans `STATE_MANAGEMENT_CONTRACT_V2.md`, puis les trois surfaces et l’onglet `Pages` dans `MENUS_AND_SURFACES_CONTRACT_V2.md`. Marquer le tout comme cible approuvée, pas comme fonctionnalité déjà implémentée.

- [ ] **Step 3: Vérifier le routage de la spec et du plan dans l’index documentaire.**

`docs/README.md` doit continuer de les router dans la section planification/implémentation sans dupliquer leur contenu.

- [ ] **Step 4: Valider et committer.**

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
rg -n "DEC-0038|PageKey|PageCode|page\.new|onglet `Pages`" docs/00_governance docs/03_runtime_contracts docs/04_editor docs/README.md
```

Attendu: aucun nouvel échec documentaire par rapport à la baseline; les références `PENDING` sont admises jusqu’au commit.

```bash
git add docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md docs/04_editor/COMMANDS_CONTRACT_V2.md docs/04_editor/STATE_MANAGEMENT_CONTRACT_V2.md docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md docs/README.md
git commit -m "docs: register modern page lifecycle architecture"
```

---

### Task 2: Ajouter l’identité logique, le code humain et la provenance

**Files:**

- Create: `src/ScadaBuilderV2.Domain/Projects/PageIdentityModels.cs`
- Create: `src/ScadaBuilderV2.Domain/Projects/PageCodePolicy.cs`
- Modify: `src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs`
- Modify: `src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs`
- Modify: `src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBinding.cs`
- Create: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ModernProjectMigration.cs`
- Modify: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ModernProjectStore.cs`
- Create: `tests/ScadaBuilderV2.Tests/PageIdentityTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`

**Interfaces:**

- Consumes: anciens champs `Id`, `HomePageId`, `HeaderPageId`, `FooterPageId`, `TargetPageId`.
- Produces: `PageKey`, `PageCode`, `PageOrigin`, `ImportProvenance`, champs `*PageKey` canoniques et migration idempotente.

- [ ] **Step 1: Définir les types Domain.**

Ajouter `PageOrigin { Native, Imported }`, `ImportProvenance(SourceSystem, SourceProjectId, SourcePageId, SourcePath)` et `PageKeyFactory`. La fabrique produit un GUID non vide aléatoire pour une nouvelle page et un GUID déterministe stable pour une page migrée à partir du nom du projet et de l’ancien code.

- [ ] **Step 2: Implémenter `PageCodePolicy`.**

Appliquer `^[a-z][a-z0-9_-]{0,63}$`, l’unicité sans tenir compte de la casse, les noms de fichiers Windows réservés et une proposition déterministe pour les duplications (`<code>_copy`, `<code>_copy2`, etc.). Les contrôles de confinement de chemin restent dans Infrastructure.

- [ ] **Step 3: Étendre les modèles de façon rétrocompatible.**

Ajouter les champs optionnels en fin de contrat JSON pour ne pas casser les constructeurs existants. Pendant la transition, `Id` reste un alias de compatibilité persisté; tout nouveau code métier utilise `PageCode`. Ajouter `HomePageKey`, `HeaderPageKey`, `FooterPageKey` et `TargetPageKey`; les anciens champs `*PageId` restent lisibles comme fallback de migration seulement.

- [ ] **Step 4: Implémenter la migration au chargement.**

`ModernProjectMigration` doit:

1. attribuer un `PageKey` déterministe aux références sans clé;
2. copier l’ancien `Id` vers `PageCode` lorsqu’il manque;
3. résoudre les anciens ids de composition, accueil, actions et commandes vers des clés;
4. produire `Imported/Wonderware` lorsqu’une projection importée est connue;
5. produire `Native` autrement;
6. préserver toute métadonnée moderne existante;
7. retourner le même snapshot au second passage.

- [ ] **Step 5: Tester les invariants et la compatibilité.**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~PageIdentityTests|FullyQualifiedName~ModernProjectStoreTests"
```

Attendu: clés stables/non vides/uniques, codes validés, migration idempotente, anciens JSON lisibles et nouveaux champs préservés au reload.

- [ ] **Step 6: Commit.**

```bash
git add src/ScadaBuilderV2.Domain/Projects/PageIdentityModels.cs src/ScadaBuilderV2.Domain/Projects/PageCodePolicy.cs src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBinding.cs src/ScadaBuilderV2.Infrastructure/ModernProjects/ModernProjectMigration.cs src/ScadaBuilderV2.Infrastructure/ModernProjects/ModernProjectStore.cs tests/ScadaBuilderV2.Tests/PageIdentityTests.cs tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs
git commit -m "feat: add stable page identity and provenance migration"
```

---

### Task 3: Introduire l’adaptateur d’identité runtime sans changer `.sb2`

**Files:**

- Create: `src/ScadaBuilderV2.Rendering/PageRuntimeIdentityResolver.cs`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100PackageValidation.cs`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100ProjectExportResult.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`

**Interfaces:**

- Consumes: références internes `PageKey` et inventaire `ScadaProject.Scenes`.
- Produces: DTO runtime utilisant exclusivement `PageCode` dans les champs historiques `.sb2`.

- [ ] **Step 1: Créer `PageRuntimeIdentityResolver`.**

Le résolveur reçoit un projet cohérent, rejette les clés absentes/dupliquées et fournit `ResolveCode(PageKey)`. Il ne génère jamais de GUID de secours pendant un export.

- [ ] **Step 2: Adapter manifeste, chemins, namespaces et cibles.**

Avant sérialisation, résoudre `HomePageKey`, `HeaderPageKey`, `FooterPageKey` et `TargetPageKey` vers `PageCode`. Les noms JSON restent `HomePageId`, `HeaderPageId`, `FooterPageId` et `TargetPageId`. Les dossiers et racines DOM restent fondés sur `PageCode`.

- [ ] **Step 3: Ajouter une comparaison de contrat.**

Construire deux projets fonctionnellement identiques, l’un ancien par ids et l’autre migré par clés. Exporter les deux et comparer le manifeste et les identifiants HTML normalisés; aucun GUID ne doit être présent dans l’archive migrée.

- [ ] **Step 4: Tester et committer.**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~Ft100SceneExporterTests|FullyQualifiedName~ModernProjectStoreTests"
```

Attendu: mêmes champs `.sb2` humains qu’avant migration et aucun `PageKey` exporté.

```bash
git add src/ScadaBuilderV2.Rendering/PageRuntimeIdentityResolver.cs src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs src/ScadaBuilderV2.Rendering/Ft100PackageValidation.cs src/ScadaBuilderV2.Rendering/Ft100ProjectExportResult.cs tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs
git commit -m "refactor: resolve internal page keys at sb2 export boundary"
```

---

## Phase 2 — Commandes asynchrones et historique workspace

### Task 4: Faire de `IApplicationCommand` le contrat asynchrone canonique

**Files:**

- Modify: `src/ScadaBuilderV2.Application/Commands/IApplicationCommand.cs`
- Modify: `src/ScadaBuilderV2.Application/Commands/ApplicationContext.cs`
- Modify: `src/ScadaBuilderV2.Application/Commands/CommandRegistry.cs`
- Modify: `src/ScadaBuilderV2.Application/Commands/ToggleSelectionLockCommand.cs`
- Create: `src/ScadaBuilderV2.Application/Commands/CommandAuthorizationPolicy.cs`
- Create: `src/ScadaBuilderV2.Application/Commands/CommandExecutionGate.cs`
- Create: `tests/ScadaBuilderV2.Tests/ApplicationCommandTests.cs`

**Interfaces:**

- Consumes: contrat synchrone `Execute(ApplicationContext)`.
- Produces: `ExecuteAsync(ApplicationContext, CancellationToken)`, résultat structuré et exécution non réentrante.

- [ ] **Step 1: Étendre le résultat de commande.**

Introduire `CommandResultStatus { Succeeded, Cancelled, Blocked, Failed }`, les clés affectées, la page à sélectionner/ouvrir, le dirty state et `IReadOnlyList<ScadaBuildValidationIssue> Diagnostics`. Conserver les fabriques `Success`/`NoChange` comme façades temporaires.

- [ ] **Step 2: Étendre `ApplicationContext`.**

Ajouter `SelectedPageKey`, `ActiveEditorPageKey`, `HomePageKey`, l’état occupé et `ICommandAuthorizationPolicy`. Fournir `AllowAllCommandAuthorizationPolicy` par défaut sans implémenter les rôles utilisateurs.

- [ ] **Step 3: Migrer l’interface et le registre.**

`CommandRegistry.ExecuteAsync` effectue lookup, `CanExecute`, autorisation, gate non réentrant, propagation du `CancellationToken` et conversion contrôlée des exceptions en résultat `Failed`. Ne jamais utiliser `.Wait()` ou `.Result`.

- [ ] **Step 4: Migrer la commande existante et tester.**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ApplicationCommandTests|FullyQualifiedName~SelectionStateTests"
```

Attendu: commande synchrone adaptée par `Task.FromResult`, annulation distincte d’un échec, deuxième exécution bloquée et `ScadaCommandBinding` inchangé.

- [ ] **Step 5: Commit.**

```bash
git add src/ScadaBuilderV2.Application/Commands tests/ScadaBuilderV2.Tests/ApplicationCommandTests.cs
git commit -m "refactor: make editor application commands asynchronous"
```

---

### Task 5: Généraliser l’historique polymorphe au workspace projet

**Files:**

- Modify: `src/ScadaBuilderV2.Application/History/EditorHistoryService.cs`
- Modify: `src/ScadaBuilderV2.Application/History/ModernElementBoundsChangedAction.cs`
- Modify: `src/ScadaBuilderV2.Application/History/ModernElementChangedAction.cs`
- Modify: `src/ScadaBuilderV2.Application/History/SceneBackgroundChangedAction.cs`
- Modify: `src/ScadaBuilderV2.Application/History/SceneObjectsAddedAction.cs`
- Modify: `src/ScadaBuilderV2.Application/History/SceneObjectsDeletedAction.cs`
- Modify: `src/ScadaBuilderV2.Application/History/SceneSelectionMovedAction.cs`
- Modify: `src/ScadaBuilderV2.Application/History/SceneSnapshotChangedAction.cs`
- Create: `src/ScadaBuilderV2.Application/History/EditorHistoryTarget.cs`
- Create: `src/ScadaBuilderV2.Application/History/ProjectWorkspaceSnapshotAction.cs`
- Modify: `tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/ProjectWorkspaceHistoryTests.cs`

**Interfaces:**

- Consumes: actions existantes ciblées par `SceneId` et piles portées par chaque onglet.
- Produces: une pile workspace supportant les portées `Scene` et `Project` sans perdre les actions existantes.

- [ ] **Step 1: Introduire une cible polymorphe.**

`EditorHistoryTarget` contient `Scope` (`Scene` ou `Project`) et une `PageKey` optionnelle. Adapter les actions scène existantes vers cette cible sans changer leur comportement métier.

- [ ] **Step 2: Étendre le contexte d’historique.**

Le contexte doit obtenir/remplacer le projet, obtenir/remplacer une scène par `PageKey`, restaurer sélection/onglet actif, marquer le workspace dirty et rafraîchir la vue concernée. Les callbacks WPF restent injectés; Application ne référence pas WPF.

- [ ] **Step 3: Ajouter `ProjectWorkspaceSnapshotAction`.**

L’action capture projet, scènes touchées, onglets, sélection, dirty state et suppressions en attente avant/après. Elle ne fait aucune I/O pendant undo/redo.

- [ ] **Step 4: Tester fermeture/suppression d’onglet.**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~EditorHistoryServiceTests|FullyQualifiedName~ProjectWorkspaceHistoryTests"
```

Attendu: les actions scène existantes passent; une action projet reste annulable après fermeture de la scène et après une sauvegarde réussie.

- [ ] **Step 5: Commit.**

```bash
git add src/ScadaBuilderV2.Application/History tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs tests/ScadaBuilderV2.Tests/ProjectWorkspaceHistoryTests.cs
git commit -m "refactor: move editor history to project workspace scope"
```

---

## Phase 3 — Persistance et rendu autonome

### Task 6: Ajouter la sauvegarde atomique du snapshot projet/workspace

**Files:**

- Create: `src/ScadaBuilderV2.Application/Pages/IPageWorkspaceStore.cs`
- Create: `src/ScadaBuilderV2.Application/Pages/PageWorkspaceSnapshot.cs`
- Modify: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ModernProjectStore.cs`
- Create: `src/ScadaBuilderV2.Infrastructure/ModernProjects/WorkspaceSaveJournal.cs`
- Create: `tests/ScadaBuilderV2.Tests/ModernProjectAtomicSnapshotTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`

**Interfaces:**

- Consumes: projet, scènes cohérentes, chemins durables et suppressions en attente.
- Produces: `SaveWorkspaceSnapshotAsync(snapshot, CancellationToken)` avec rollback et récupération.

- [ ] **Step 1: Définir le snapshot applicatif.**

Inclure `ScadaProject`, dictionnaire de scènes par `PageKey`, suppressions en attente et version de snapshot. Le snapshot ne contient aucun contrôle WPF ni état WebView.

- [ ] **Step 2: Écrire puis valider dans un staging transactionnel.**

Sérialiser tous les nouveaux fichiers dans `.studio/transactions/<transaction-id>/`, vérifier JSON, collisions, confinement des chemins et correspondance projet/scènes avant de toucher les fichiers actifs.

- [ ] **Step 3: Implémenter commit, rollback et journal de récupération.**

Créer les sauvegardes nécessaires, remplacer les scènes, remplacer `project.json` comme point de commit, puis appliquer les suppressions. En cas d’erreur, restaurer les sauvegardes. Au chargement, détecter un journal incomplet et restaurer ou finaliser déterministement selon sa phase. Aucun fichier source Wonderware n’est déplacé ou supprimé.

- [ ] **Step 4: Conserver les façades existantes.**

`SaveSceneAsync` et `SaveProjectAsync` restent utilisables pendant la migration, mais délèguent au nouveau mécanisme. Retirer l’upsert implicite de métadonnées seulement après que tous les appelants de page utilisent un snapshot cohérent.

- [ ] **Step 5: Tester les points de défaillance.**

Tester échec pendant staging, remplacement d’une scène, remplacement du projet et suppression. Vérifier que le reload expose intégralement l’ancien ou le nouveau snapshot, jamais un mélange non récupérable.

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ModernProjectAtomicSnapshotTests|FullyQualifiedName~ModernProjectStoreTests"
```

- [ ] **Step 6: Commit.**

```bash
git add src/ScadaBuilderV2.Application/Pages/IPageWorkspaceStore.cs src/ScadaBuilderV2.Application/Pages/PageWorkspaceSnapshot.cs src/ScadaBuilderV2.Infrastructure/ModernProjects/ModernProjectStore.cs src/ScadaBuilderV2.Infrastructure/ModernProjects/WorkspaceSaveJournal.cs tests/ScadaBuilderV2.Tests/ModernProjectAtomicSnapshotTests.cs tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs
git commit -m "feat: save project workspace snapshots atomically"
```

---

### Task 7: Rendre les pages natives et les projections importées interchangeables

**Files:**

- Create: `src/ScadaBuilderV2.Rendering/NativePageDocumentFactory.cs`
- Create: `src/ScadaBuilderV2.Rendering/PageDocumentInput.cs`
- Modify: `src/ScadaBuilderV2.Rendering/PreviewDocument.cs`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100ProjectExportResult.cs`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`
- Create: `src/ScadaBuilderV2.App/Pages/PageSourceProjectionResolver.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Create: `tests/ScadaBuilderV2.Tests/NativePageDocumentTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/PreviewDocumentTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

**Interfaces:**

- Consumes: `ScadaSceneReference`, `ScadaScene`, provenance importée optionnelle.
- Produces: document importé ou natif selon le modèle, avec le même pipeline preview/export.

- [ ] **Step 1: Définir `PageDocumentInput`.**

L’entrée contient la référence moderne, la scène et une projection HTML importée optionnelle. La projection est résolue par `ImportProvenance.SourcePath`, jamais par égalité implicite entre `PageCode` et `_referenceProject.Pages`.

- [ ] **Step 2: Implémenter `NativePageDocumentFactory`.**

Générer la racine `ft100-<PageCode>`, une couche source vide, la couche Element+, le CSS namespacé, les dimensions/fond et les métadonnées runtime. Aucun overlay, handle, workzone, zoom, pan ou diagnostic n’est sérialisé.

- [ ] **Step 3: Adapter preview et export.**

Le preview ajoute uniquement son bridge d’édition; l’export ajoute uniquement les ressources runtime existantes. `Ft100ProjectPageExportInput` accepte une projection absente sans casser les constructeurs historiques.

- [ ] **Step 4: Ajouter les baselines natives et importées.**

Tester une page `Blank` native et `win00009` importée. Vérifier racine, couches, dimensions, fond, Element+, CSS namespacé et parité d’identité `.sb2`.

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~NativePageDocumentTests|FullyQualifiedName~PreviewDocumentTests|FullyQualifiedName~Ft100SceneExporterTests"
```

Attendu: page native prévisualisable/exportable sans source; projection Wonderware inchangée pour `win00009`.

- [ ] **Step 5: Commit.**

```bash
git add src/ScadaBuilderV2.Rendering src/ScadaBuilderV2.App/Pages/PageSourceProjectionResolver.cs src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/NativePageDocumentTests.cs tests/ScadaBuilderV2.Tests/PreviewDocumentTests.cs tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "feat: render native pages without imported html sources"
```

---

## Phase 4 — Règles et commandes de page

### Task 8: Ajouter l’analyse de dépendances et les diagnostics structurés

**Files:**

- Create: `src/ScadaBuilderV2.Application/Pages/PageDependencyAnalyzer.cs`
- Create: `src/ScadaBuilderV2.Application/Pages/PageDependencyModels.cs`
- Create: `src/ScadaBuilderV2.Application/Pages/IPageWorkspaceReader.cs`
- Modify: `src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100PackageValidation.cs`
- Create: `tests/ScadaBuilderV2.Tests/PageDependencyAnalyzerTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

**Interfaces:**

- Consumes: snapshot cohérent, scènes ouvertes/dirty prioritaires, scènes fermées chargées du store.
- Produces: dépendances structurées et diagnostics localisables.

- [ ] **Step 1: Enrichir `ScadaBuildValidationIssue`.**

Ajouter de façon rétrocompatible `PageKey`, `ElementId`, `CommandId`, `PropertyPath`, `TargetKey` et `SuggestedFix`. Conserver `PageId` comme code humain de présentation/export.

- [ ] **Step 2: Implémenter l’analyse récursive.**

Inspecter accueil, header/footer, `ScadaActionDefinition`, `ScadaCommandBinding`, groupes Element+ et onglets ouverts. Exécuter même pour les pages exclues du build lors d’une suppression.

- [ ] **Step 3: Compléter la validation de build.**

Valider `Navigate` vers une page `Default` compilée et les actions popup vers un `Fragment` compilé, même sans catalogue de tags. Retourner toutes les erreurs et tous les avertissements.

- [ ] **Step 4: Tester et committer.**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~PageDependencyAnalyzerTests|FullyQualifiedName~Ft100SceneExporterTests"
```

Attendu: aucune dépendance n’est omise dans une scène fermée ou dirty; les diagnostics contiennent une localisation structurée.

```bash
git add src/ScadaBuilderV2.Application/Pages src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs src/ScadaBuilderV2.Rendering/Ft100PackageValidation.cs tests/ScadaBuilderV2.Tests/PageDependencyAnalyzerTests.cs tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "feat: analyze page dependencies and emit structured diagnostics"
```

---

### Task 9: Implémenter le coordinateur et les commandes `page.*`

**Files:**

- Create: `src/ScadaBuilderV2.Application/Pages/PageCommandCoordinator.cs`
- Create: `src/ScadaBuilderV2.Application/Pages/PageCommandRequests.cs`
- Create: `src/ScadaBuilderV2.Application/Pages/PageTemplate.cs`
- Create: `src/ScadaBuilderV2.Application/Pages/PageWorkspaceMutation.cs`
- Create: `src/ScadaBuilderV2.Application/Commands/Pages/NewPageCommand.cs`
- Create: `src/ScadaBuilderV2.Application/Commands/Pages/RenamePageCommand.cs`
- Create: `src/ScadaBuilderV2.Application/Commands/Pages/DuplicatePageCommand.cs`
- Create: `src/ScadaBuilderV2.Application/Commands/Pages/DeletePageCommand.cs`
- Create: `src/ScadaBuilderV2.Application/Commands/Pages/PagePropertyCommands.cs`
- Create: `tests/ScadaBuilderV2.Tests/PageCommandCoordinatorTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/PageApplicationCommandTests.cs`

**Interfaces:**

- Consumes: contexte de commande, workspace store/reader, analyseur, historique et modèle `Blank`.
- Produces: mutations projet/scènes sans dépendance WPF ni écriture directe du DOM.

- [ ] **Step 1: Implémenter `Blank` et `page.new`.**

Créer une page `Default` avec nouveau `PageKey`, chemin `scenes/<PageKey:N>.scene.json`, `IncludeInBuild = false`, titre/code validés et composition héritée seulement d’une page active `Default`. Sélectionner et demander l’ouverture de la nouvelle page dans le résultat.

- [ ] **Step 2: Implémenter renommage et changement de code.**

`page.rename` modifie `Title`; `page.change-code` modifie `PageCode` sans changer `PageKey` ni `RelativePath`. Synchroniser la scène de compatibilité et produire les impacts exportés dans le résultat.

- [ ] **Step 3: Implémenter la duplication complète.**

Copier scène, éléments, actions, événements, bindings, fond et métadonnées; créer `PageKey`/`PageCode`; réécrire les auto-références vers le duplicata; régénérer les ids d’actions et les bindings concernés. Une page importée conserve exactement sa projection/provenance Wonderware; une page native reste native.

- [ ] **Step 4: Implémenter suppression et propriétés.**

`page.delete` exécute la préanalyse et retourne `Blocked` avec toutes les dépendances si nécessaire. En succès, retirer du snapshot, enregistrer une suppression en attente, fermer l’onglet via la mutation workspace et pousser une action historique projet. Implémenter aussi `page.open`, `page.properties`, `page.set-build-inclusion`, `page.set-home`, `page.set-type`, `page.set-composition` et `page.validate` sur le même coordinateur.

- [ ] **Step 5: Tester tous les invariants.**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~PageCommandCoordinatorTests|FullyQualifiedName~PageApplicationCommandTests|FullyQualifiedName~ProjectWorkspaceHistoryTests"
```

Attendu: nouveau non compilé, duplication Wonderware fidèle, références internes réécrites, suppression bloquée, aucune mutation sur annulation/échec et undo/redo projet complet.

- [ ] **Step 6: Commit.**

```bash
git add src/ScadaBuilderV2.Application/Pages src/ScadaBuilderV2.Application/Commands/Pages tests/ScadaBuilderV2.Tests/PageCommandCoordinatorTests.cs tests/ScadaBuilderV2.Tests/PageApplicationCommandTests.cs
git commit -m "feat: implement shared page lifecycle commands"
```

---

## Phase 5 — Workspace WPF et surfaces utilisateur

### Task 10: Extraire le workspace de pages hors de `MainWindow`

**Files:**

- Create: `src/ScadaBuilderV2.App/Pages/PageWorkspaceController.cs`
- Create: `src/ScadaBuilderV2.App/Pages/PageWorkspaceEntry.cs`
- Create: `src/ScadaBuilderV2.App/Pages/IPageWorkspaceHost.cs`
- Create: `src/ScadaBuilderV2.App/Workspace/SceneWorkspaceTab.cs`
- Create: `src/ScadaBuilderV2.App/Pages/PageExportInputBuilder.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs`
- Modify: `tests/ScadaBuilderV2.Tests/SceneWorkspaceTabContractTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/PageWorkspaceExtractionContractTests.cs`

**Interfaces:**

- Consumes: projet moderne, provenance optionnelle, commandes, historique, store et callbacks du host WebView.
- Produces: sélection, ouverture/fermeture, onglets, dirty state, sauvegarde et entrées export pilotés hors de `MainWindow`.

- [ ] **Step 1: Remplacer la dépendance `ReferenceScadaPage`.**

`SceneWorkspaceTab` reçoit `PageWorkspaceEntry` fondé sur `ScadaSceneReference`; la provenance importée est optionnelle. Retirer `ReferencePage` comme identité de l’onglet.

- [ ] **Step 2: Déplacer le cycle de vie du workspace.**

Migrer vers `PageWorkspaceController`: inventaire, sélection, ouverture/activation, fermeture, dirty prompt, snapshot, historique, page voisine après suppression et récupération du document preview. Les callbacks visuels/WebView sont exposés par `IPageWorkspaceHost`.

- [ ] **Step 3: Déplacer la préparation de l’export.**

`PageExportInputBuilder` parcourt `ScadaProject.Scenes`, choisit la scène dirty ou sauvegardée et résout la projection par provenance. Il accepte une page native; aucune recherche directe par `_referenceProject.Pages` ne demeure dans `MainWindow`.

- [ ] **Step 4: Réduire `MainWindow` à l’adaptation.**

Les handlers peuvent relayer un événement, mais ne doivent plus construire de `ScadaSceneReference`, modifier `HomePageId`, appeler `SaveSceneAsync`, analyser une suppression ou résoudre une projection Wonderware.

- [ ] **Step 5: Tester l’extraction.**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~SceneWorkspaceTabContractTests|FullyQualifiedName~PageWorkspaceExtractionContractTests"
rg -n "new ScadaSceneReference|HomePageId\s*=|SaveSceneAsync|ReferenceScadaPage|_referenceProject\.Pages\.ToDictionary" src/ScadaBuilderV2.App/MainWindow.xaml.cs
```

Attendu: le `rg` ne trouve aucune mutation/résolution de page interdite dans `MainWindow.xaml.cs`; les tests d’onglets passent.

- [ ] **Step 6: Commit.**

```bash
git add src/ScadaBuilderV2.App/Pages src/ScadaBuilderV2.App/Workspace src/ScadaBuilderV2.App/MainWindow.xaml.cs src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs tests/ScadaBuilderV2.Tests/SceneWorkspaceTabContractTests.cs tests/ScadaBuilderV2.Tests/PageWorkspaceExtractionContractTests.cs
git commit -m "refactor: extract page workspace from MainWindow"
```

---

### Task 11: Ajouter l’onglet `Pages`, les actions rapides et le menu contextuel

**Files:**

- Create: `src/ScadaBuilderV2.App/Pages/PagesPanelViewModel.cs`
- Create: `src/ScadaBuilderV2.App/Pages/PageListItemViewModel.cs`
- Create: `src/ScadaBuilderV2.App/Pages/PageCommandController.cs`
- Create: `src/ScadaBuilderV2.App/Pages/PageEditorDialog.xaml`
- Create: `src/ScadaBuilderV2.App/Pages/PageEditorDialog.xaml.cs`
- Modify: `src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Modify: `src/ScadaBuilderV2.App/Resources/Icons.xaml`
- Modify: `docs/06_ui_ux/ICON_STRATEGY_V2.md`
- Modify: `tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/PageManagementSurfaceContractTests.cs`

**Interfaces:**

- Consumes: registre `IApplicationCommand`, `PagesPanelViewModel`, diagnostics et workspace controller.
- Produces: trois surfaces cohérentes utilisant les mêmes ids et états.

- [ ] **Step 1: Ajouter l’onglet du ruban.**

Insérer `Pages` entre `Edition` et `Écran`. Ajouter le groupe `Gestion` (`Nouveau`, `Renommer`, `Dupliquer`, `Supprimer`) et le groupe `Inspection` (`Propriétés`, `Valider`) avec clés d’icônes sémantiques `Icon.Page.*`.

- [ ] **Step 2: Construire le modèle de panneau.**

Exposer titre, `PageCode`, type, inclusion, accueil, statut diagnostic, sélection, recherche texte et filtres type/build. Le filtre ne modifie ni ordre durable ni sélection encore visible.

- [ ] **Step 3: Refaire `Projet > Pages`.**

Lier la liste moderne, ajouter le bouton `+`, les actions rapides Renommer/Dupliquer/Supprimer, badges et tooltips accessibles. `PageKey` ne doit apparaître dans aucun texte, binding de présentation ou tooltip.

- [ ] **Step 4: Ajouter le menu contextuel et le clavier.**

Le clic droit sélectionne sans ouvrir. Le menu contient Ouvrir, Renommer, Dupliquer, Accueil, Inclure/exclure, Propriétés et Supprimer. Entrée/F2/Ctrl+D/Suppr sont actifs seulement lorsque le focus est dans le panneau Pages.

- [ ] **Step 5: Ajouter le dialogue partagé.**

Le mode Nouvelle page expose code, titre, type, build décoché, composition et modèle `Blank`. Les modes Renommer/Dupliquer réutilisent le même style et les validations applicatives, sans exposer `PageKey`.

- [ ] **Step 6: Tester les surfaces.**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~RibbonCommandCatalogTests|FullyQualifiedName~PageManagementSurfaceContractTests"
```

Attendu: mêmes `CommandId` sur ruban/lignes/contexte, onglet `Pages` présent, aucun GUID affiché, états désactivés accompagnés d’une raison.

- [ ] **Step 7: Commit.**

```bash
git add src/ScadaBuilderV2.App/Pages src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs src/ScadaBuilderV2.App/MainWindow.xaml src/ScadaBuilderV2.App/MainWindow.xaml.cs src/ScadaBuilderV2.App/Resources/Icons.xaml docs/06_ui_ux/ICON_STRATEGY_V2.md tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs tests/ScadaBuilderV2.Tests/PageManagementSurfaceContractTests.cs
git commit -m "feat: expose page commands across ribbon and project panel"
```

---

### Task 12: Ajouter la boîte d’erreur moderne et le panneau inférieur Diagnostics

**Files:**

- Create: `src/ScadaBuilderV2.App/Diagnostics/DiagnosticsPanelViewModel.cs`
- Create: `src/ScadaBuilderV2.App/Diagnostics/DiagnosticIssueViewModel.cs`
- Create: `src/ScadaBuilderV2.App/Diagnostics/CommandErrorDialog.xaml`
- Create: `src/ScadaBuilderV2.App/Diagnostics/CommandErrorDialog.xaml.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Modify: `src/ScadaBuilderV2.App/Pages/PageCommandController.cs`
- Create: `tests/ScadaBuilderV2.Tests/DiagnosticsSurfaceContractTests.cs`

**Interfaces:**

- Consumes: collection structurée commune retournée par commandes, validation, compilation et export.
- Produces: dialogue bloquant moderne, panneau AvalonDock inférieur et navigation par `PageKey`.

- [ ] **Step 1: Créer le modèle de diagnostics réutilisable.**

Exposer collections filtrées `Erreurs`, `Avertissements`, `Informations`, compteurs, horodatage/source et commande de navigation. Conserver le dernier résultat jusqu’à nouvelle validation ou fermeture du projet.

- [ ] **Step 2: Ajouter le panneau inférieur.**

Créer un `LayoutAnchorable` `Diagnostics` ancré en bas, masqué lorsqu’inutilisé mais accessible depuis le bouton de statut. Afficher code, message, page humaine, objet/commande et correction possible.

- [ ] **Step 3: Remplacer les erreurs page par le dialogue moderne.**

Le dialogue affiche résumé, compteurs, premières causes, `Fermer` et `Afficher les erreurs`. Cette dernière action ouvre le panneau sur `Erreurs`. Une annulation utilisateur ne déclenche pas le dialogue.

- [ ] **Step 4: Brancher la navigation.**

Le double-clic résout `PageKey`, ouvre la page puis sélectionne l’élément ou la propriété si la localisation existe. Le GUID n’est jamais présenté.

- [ ] **Step 5: Tester et committer.**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~DiagnosticsSurfaceContractTests|FullyQualifiedName~PageApplicationCommandTests|FullyQualifiedName~Ft100SceneExporterTests"
```

Attendu: dialogue et panneau consomment la même collection; les erreurs bloquent, les avertissements non; le panneau n’est jamais exporté.

```bash
git add src/ScadaBuilderV2.App/Diagnostics src/ScadaBuilderV2.App/MainWindow.xaml src/ScadaBuilderV2.App/MainWindow.xaml.cs src/ScadaBuilderV2.App/Pages/PageCommandController.cs tests/ScadaBuilderV2.Tests/DiagnosticsSurfaceContractTests.cs
git commit -m "feat: add shared page and build diagnostics surfaces"
```

---

### Task 13: Router toutes les propriétés de page par le coordinateur

**Files:**

- Create: `src/ScadaBuilderV2.App/Pages/PagePropertiesViewModel.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Modify: `src/ScadaBuilderV2.App/Pages/PageWorkspaceController.cs`
- Modify: `src/ScadaBuilderV2.App/Pages/PageCommandController.cs`
- Modify: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ModernProjectStore.cs`
- Modify: `tests/ScadaBuilderV2.Tests/PageWorkspaceExtractionContractTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/PageApplicationCommandTests.cs`

**Interfaces:**

- Consumes: contrôles Page existants et commandes `page.change-code`, `page.set-*`.
- Produces: une seule autorité de mutation pour titre/code/type/build/accueil/composition/canevas/fond.

- [ ] **Step 1: Lier le panneau Page à un view model.**

Afficher `PageCode` comme propriété humaine modifiable, garder `PageKey` absent, et conserver les contrôles de type/build/accueil/composition/canevas/fond.

- [ ] **Step 2: Remplacer les mutations code-behind.**

Supprimer de `MainWindow` la logique de `OnIncludeInBuildClick`, `SetHomePageId`, `EnsureHomePageStillValid`, composition, création de référence et sauvegarde implicite. Les événements invoquent les commandes et appliquent uniquement le résultat au view model.

- [ ] **Step 3: Synchroniser référence, scène, onglets et historique.**

Toute modification met à jour le snapshot workspace, pousse une action au bon scope, marque le projet dirty et rafraîchit les surfaces. Une sauvegarde ultérieure ne doit pas écraser le titre/code moderne. Une fois tous les appelants migrés, retirer l’upsert implicite de `SaveSceneAsync`; la référence projet autoritaire provient exclusivement du snapshot.

- [ ] **Step 4: Tester et committer.**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~PageWorkspaceExtractionContractTests|FullyQualifiedName~PageApplicationCommandTests|FullyQualifiedName~ModernProjectStoreTests"
rg -n "OnIncludeInBuildClick|SetHomePageId|EnsureHomePageStillValid|UpdateModernProjectFromActiveScene" src/ScadaBuilderV2.App/MainWindow.xaml.cs
```

Attendu: aucun propriétaire de mutation page ne demeure dans `MainWindow`; sauvegarde/reload préservent toutes les métadonnées modernes.

```bash
git add src/ScadaBuilderV2.App/Pages/PagePropertiesViewModel.cs src/ScadaBuilderV2.App/Pages/PageWorkspaceController.cs src/ScadaBuilderV2.App/Pages/PageCommandController.cs src/ScadaBuilderV2.App/MainWindow.xaml src/ScadaBuilderV2.App/MainWindow.xaml.cs src/ScadaBuilderV2.Infrastructure/ModernProjects/ModernProjectStore.cs tests/ScadaBuilderV2.Tests/PageWorkspaceExtractionContractTests.cs tests/ScadaBuilderV2.Tests/PageApplicationCommandTests.cs tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs
git commit -m "refactor: centralize page property mutations outside MainWindow"
```

---

## Phase 6 — Intégration, migration contrôlée et documentation

### Task 14: Valider le parcours complet sans toucher au projet réel

**Files:**

- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs` seulement si le contrat clavier/WebView l’exige
- Create: `tests/ScadaBuilderV2.Tests/PageLifecycleIntegrationTests.cs`

**Interfaces:**

- Consumes: toutes les tranches précédentes sur une copie temporaire de projet.
- Produces: preuve automatisée et manuelle du cycle complet.

- [ ] **Step 1: Ajouter le scénario d’intégration.**

Charger un projet ancien, migrer, créer une page `Blank`, renommer, changer le code, dupliquer une page Wonderware, tenter une suppression bloquée, corriger la dépendance, supprimer, sauvegarder, undo, sauvegarder, reload et exporter `.sb2`.

- [ ] **Step 2: Vérifier les invariants finaux.**

Le scénario doit prouver:

1. stabilité des `PageKey`;
2. absence de GUID dans `.sb2`;
3. nouvelle page non compilée;
4. projection Wonderware conservée sur duplication;
5. rollback et récupération;
6. historique disponible après fermeture/suppression d’onglet;
7. diagnostics complets;
8. absence d’artefacts éditeur dans preview/export.

- [ ] **Step 3: Exécuter build et tests.**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~PageLifecycleIntegrationTests|FullyQualifiedName~PageIdentityTests|FullyQualifiedName~PageCommandCoordinatorTests|FullyQualifiedName~ModernProjectAtomicSnapshotTests|FullyQualifiedName~NativePageDocumentTests|FullyQualifiedName~Ft100SceneExporterTests"
dotnet test ScadaBuilderV2.sln --no-restore
```

Attendu: tests ciblés verts; suite complète identique ou meilleure que la baseline, sans nouvelle défaillance.

- [ ] **Step 4: Vérification manuelle sur copie temporaire.**

Tester ruban, bouton `+`, actions rapides, clic droit, raccourcis, recherche/filtres, dialogue d’erreur, panneau Diagnostics, double-clic diagnostic, preview native, duplication Wonderware et export `.sb2`.

- [ ] **Step 5: Commit.**

```bash
git add tests/ScadaBuilderV2.Tests
git commit -m "test: cover modern page lifecycle end to end"
```

---

### Task 15: Mettre à jour la documentation, les cartes et la version

**Files:**

- Modify: `docs/00_governance/DECISION_REGISTER_V2.md`
- Modify: `docs/02_architecture/DATA_MODEL_OVERVIEW_V2.md`
- Modify: `docs/02_architecture/APPLICATION_FLOW_V2.md`
- Modify: `docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md`
- Modify: `docs/03_runtime_contracts/PREVIEW_BUILD_EXPORT_CONTRACT_V2.md`
- Modify: `docs/04_editor/COMMANDS_CONTRACT_V2.md`
- Modify: `docs/04_editor/STATE_MANAGEMENT_CONTRACT_V2.md`
- Modify: `docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md`
- Modify: `docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md`
- Modify: `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md`
- Modify: `docs/08_implementation_status/KNOWN_GAPS_V2.md`
- Modify: `docs/10_generated/CODE_MAP_V2.md`
- Modify: `docs/10_generated/COMMAND_FLOW_DIAGRAM_V2.md`
- Modify: `docs/10_generated/STATE_FLOW_DIAGRAM_V2.md`
- Modify: `docs/superpowers/plans/2026-07-14-page-management-commands.md`
- Modify: `docs/README.md`
- Modify: `VERSION`

**Interfaces:**

- Consumes: code et tests réellement validés.
- Produces: documentation actuelle, traçable, sans déclarer les hors-périmètre comme implémentés.

- [ ] **Step 1: Mettre à jour les propriétaires et diagrammes.**

Documenter le flux Surface → registre → coordinateur → modèle/historique/store → diagnostics, la migration, la page native et l’adaptateur d’export. Ajouter les tests exacts aux contrats et à la couverture de régression.

- [ ] **Step 2: Clore le plan selon les résultats réels.**

Cocher uniquement les étapes exécutées. Passer le statut à `Implemented` seulement si build, tests automatisés et vérification manuelle requise sont réellement terminés; sinon nommer précisément ce qui reste.

- [ ] **Step 3: Calculer une hausse d’itération depuis la valeur réelle.**

```powershell
$current = (Get-Content -Raw VERSION).Trim()
python "C:\Users\mathi\.codex\skills\scada-builder-v2-versioning\scripts\bump_scada_v2_version.py" $current iteration
```

Appliquer le résultat à `VERSION` et aux lignes d’historique touchées. Ne pas forcer une hausse feature/production sans nouvelle décision utilisateur.

- [ ] **Step 4: Valider la documentation et le dépôt.**

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
rg -n "PageKey|PageCode|page\.new|page\.delete|Wonderware|Diagnostics|MainWindow" docs/00_governance docs/02_architecture docs/03_runtime_contracts docs/04_editor docs/08_implementation_status
git status --short --branch
```

Attendu: aucun nouveau problème documentaire; les écarts préexistants restent explicitement séparés; les fichiers du plan correspondent au code réellement livré.

- [ ] **Step 5: Commit de clôture.**

```bash
git add VERSION docs
git commit -m "docs: record implemented modern page management"
```

---

## Phase 7 — Projet de référence réel (Authorization Gate)

> **Authorization required before opening or migrating `projects/AMR_REF_SCADA_V2` avec le nouveau modèle.** Les tests et la copie temporaire doivent être conformes avant ce point.

### Task 16: Exécuter la migration réelle et vérifier le projet AMR

**Files:**

- Inspect/backup: `projects/AMR_REF_SCADA_V2/project.json`
- Inspect/backup: `projects/AMR_REF_SCADA_V2/scenes/*.scene.json`
- Avoid modifying: `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER\AMR_SCADA\AMR_REF_SCADA\**`

**Interfaces:**

- Consumes: projet réel après autorisation explicite.
- Produces: projet moderne migré et vérifié, jamais la source Wonderware.

- [ ] **Step 1: Créer une sauvegarde datée hors des chemins actifs.**

- [ ] **Step 2: Ouvrir, migrer et enregistrer une fois.**

Vérifier que chaque page reçoit une clé stable, un code humain identique à l’ancien id et une provenance Wonderware résoluble.

- [ ] **Step 3: Redémarrer et vérifier l’idempotence.**

Aucune clé, provenance, composition, inclusion, page d’accueil, scène ou ordre ne doit changer au deuxième chargement.

- [ ] **Step 4: Exporter un `.sb2` de contrôle.**

Comparer manifeste, dossiers, racines DOM et cibles de navigation avec le contrat antérieur. Aucun GUID ne doit apparaître.

- [ ] **Step 5: Committer uniquement si les fichiers projet sont versionnés et si leur migration faisait partie de l’autorisation.**

---

## Validation Checklist

- [ ] Les quatre commandes de cycle de vie existent sous leurs ids stables et passent par le registre asynchrone.
- [ ] Ruban, actions rapides et menu contextuel partagent ids, enablement, résultats et diagnostics.
- [ ] L’onglet visible se nomme `Pages`.
- [ ] Une page `Default` nouvellement créée est exclue du build par défaut.
- [ ] `PageKey` est immuable, non vide, unique et absent de toutes les surfaces utilisateur et de `.sb2`.
- [ ] `PageCode` est visible, modifiable, validé et utilisé par le contrat `.sb2` inchangé.
- [ ] Une duplication Wonderware conserve automatiquement sa projection et sa provenance.
- [ ] Une page native fonctionne sans entrée dans `_referenceProject.Pages`.
- [ ] Les auto-références dupliquées sont réécrites et les ids d’actions/bindings concernés sont régénérés.
- [ ] Une suppression référencée est bloquée avec la collection complète des dépendances.
- [ ] Undo/redo d’une action projet reste fonctionnel après fermeture ou suppression d’onglet et après sauvegarde.
- [ ] Une défaillance de sauvegarde restaure un snapshot cohérent; un journal interrompu est récupérable.
- [ ] Recherche et filtres n’altèrent ni ordre durable ni sélection valide.
- [ ] Le dialogue moderne et le panneau Diagnostics utilisent la même collection structurée.
- [ ] Les erreurs bloquent l’export; les avertissements restent non bloquants.
- [ ] `MainWindow` ne possède plus les mutations, dépendances, persistance ou résolution de source des pages.
- [ ] Preview/build/export restent en parité et aucun artefact éditeur n’est exporté.
- [ ] Les tests ciblés et la suite complète respectent la baseline fraîche.
- [ ] `tools/docs/verify-docs.ps1` n’introduit aucun nouvel échec.
- [ ] La version a reçu une hausse d’itération calculée depuis la valeur réelle.
- [ ] Le worktree est propre après les commits de clôture.
