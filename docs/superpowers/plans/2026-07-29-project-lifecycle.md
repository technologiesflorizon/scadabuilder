# Cycle de vie moderne des projets — Plan d’implémentation

Date: 2026-07-29
Status: Draft implementation plan — pending execution approval
Document version: `V2.1.4.0068`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-29 | `V2.1.4.0068` | `PENDING` | Création du plan exécutable dérivé de la spécification approuvée `DEC-0049`. |

> Ce plan est dérivé de `docs/superpowers/specs/2026-07-29-project-lifecycle-design.md` (`DEC-0049`, D1–D16). Aucune décision produit n’y reste ouverte.

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remplacer le démarrage spécialisé sur `AMR_REF_SCADA_V2` par un cycle de vie projet complet : accueil sans projet, création transactionnelle dans un emplacement choisi, ouverture fail-closed, sauvegarde workspace, fermeture sûre, changement de projet et récents, tout en préservant les projets V2 existants et le contrat `.sb2`.

**Architecture:** Domain conserve uniquement le modèle persistant. Application porte les commandes `project.*`, les requêtes, le contexte actif, les transitions et le gate dirty. Infrastructure généralise la persistance vers une racine projet exacte, crée les projets par staging et persiste les récents. App/WPF porte les dialogues et projette une session préparée dans le workspace. `MainWindow` devient un host de session et ne déduit plus un projet depuis le dépôt du logiciel.

**Tech Stack:** C# 12, .NET 8, WPF/AvalonDock/WebView2, `System.Text.Json`, MSTest, PowerShell, fichiers projet V2 et export runtime `.sb2`

## Global Constraints

- Spec propriétaire: `docs/superpowers/specs/2026-07-29-project-lifecycle-design.md`.
- Décision: `DEC-0049`.
- Une fenêtre contient zéro ou une session projet active.
- Aucun projet n’est ouvert automatiquement au démarrage.
- Le dossier projet est choisi par l’utilisateur et indépendant du dépôt/installation.
- `project.json` reste le manifeste éditable; `.sb2` reste uniquement l’artefact runtime.
- Création et ouverture doivent préparer une cible valide avant de remplacer la session courante.
- `Enregistrer`, `Ne pas enregistrer`, `Annuler` est le seul contrat dirty pour ouvrir, créer, fermer et quitter.
- Une migration de modèle au chargement reste en mémoire jusqu’à la prochaine sauvegarde explicite.
- Les récents sont des paramètres utilisateur sous `%AppData%`, jamais des données de projet.
- `AMR_REF_SCADA_V2` doit rester ouvrable sur copie sans modifier les sources Wonderware.
- Preview, build et export continuent de consommer le même modèle V2.
- Aucun changement de schéma ou de comportement `.sb2` n’est autorisé.
- Aucun changement n’est autorisé dans TF100Web ou le projet Wonderware source.
- Les API publiques et les méthodes privées contractuelles reçoivent la documentation exigée avec `Decisions:`, `Contracts:` et `Tests:`.
- L’implémentation utilise un bump feature `V2.1.5.0000` seulement après validation de la capacité complète.

---

## Before You Start

- [ ] **Vérifier la branche et le worktree sans écraser les modifications existantes.**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git status --short --branch
git diff --stat
```

Attendu: le worktree actuel contient des modifications utilisateur sans rapport dans `projects/AMR_REF_SCADA_V2` et un test associé. Ne pas les restaurer, les stager ou les inclure. Avant le code, placer la nouvelle tranche sur une branche propre `codex/project-lifecycle` ou obtenir une instruction explicite sur la frontière de commit.

- [ ] **Committer séparément la spécification et le plan lorsque le worktree le permet.**

```bash
git add VERSION docs/README.md docs/00_governance/DECISION_REGISTER_V2.md docs/superpowers/specs/2026-07-29-project-lifecycle-design.md docs/superpowers/plans/2026-07-29-project-lifecycle.md
git commit -m "docs: approve modern project lifecycle design"
```

- [ ] **Capturer la baseline fraîche.**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
```

Attendu: consigner les résultats réels; toute nouvelle défaillance doit être investiguée. Ne pas utiliser un ancien décompte comme attendu.

- [ ] **Créer des répertoires temporaires dédiés aux essais.**

Tous les tests de création, migration, récupération et ouverture de projet utilisent `TestContext`/`Path.GetTempPath()` ou une copie contrôlée. Aucun test ne crée un projet sous le dépôt du logiciel, `Documents` réel ou `projects/AMR_REF_SCADA_V2`.

---

## Phase 1 — Contrats et chemins de projet

### Task 1: Enregistrer les contrats propriétaires de `DEC-0049`

**Files:**

- Modify: `docs/02_architecture/APPLICATION_FLOW_V2.md`
- Modify: `docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md`
- Modify: `docs/04_editor/COMMANDS_CONTRACT_V2.md`
- Modify: `docs/04_editor/STATE_MANAGEMENT_CONTRACT_V2.md`
- Modify: `docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md`
- Modify: `docs/06_ui_ux/UI_ARCHITECTURE_V2.md`
- Modify: `docs/06_ui_ux/UI_SPECIFICATION_V2.md`
- Modify: `docs/README.md`

**Interfaces:**

- Consumes: D1–D16 de la spécification.
- Produces: propriétaires documentaires alignés sur le contexte actif, les transitions, les commandes et l’accueil.

- [ ] **Step 1: Documenter la cible, pas une implémentation inexistante.**

Ajouter une section `Approved target` ou équivalente dans chaque propriétaire. Les statuts d’implémentation restent inchangés jusqu’à la clôture.

- [ ] **Step 2: Mettre à jour les flux Mermaid.**

Représenter `Accueil/Surface -> project.* -> ProjectLifecycleCoordinator -> ProjectWorkspaceRepository/RecentProjectStore -> ProjectSessionController -> PageWorkspaceController/Host`.

- [ ] **Step 3: Valider.**

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
rg -n "DEC-0049|project\.new|project\.open|project\.close|ProjectRoot|projets récents" docs/02_architecture docs/03_runtime_contracts docs/04_editor docs/06_ui_ux docs/README.md
```

Expected: documentation valide; aucune affirmation `Implemented` pour cette tranche.

- [ ] **Step 4: Commit.**

```bash
git add docs/02_architecture/APPLICATION_FLOW_V2.md docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md docs/04_editor/COMMANDS_CONTRACT_V2.md docs/04_editor/STATE_MANAGEMENT_CONTRACT_V2.md docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md docs/06_ui_ux/UI_ARCHITECTURE_V2.md docs/06_ui_ux/UI_SPECIFICATION_V2.md docs/README.md
git commit -m "docs: register project lifecycle contracts"
```

---

### Task 2: Introduire une racine projet exacte et supprimer le locator AMR du store général

**Files:**

- Create: `src/ScadaBuilderV2.Application/Projects/ProjectWorkspaceLocation.cs`
- Create: `src/ScadaBuilderV2.Application/Projects/IProjectWorkspaceRepository.cs`
- Create: `src/ScadaBuilderV2.Application/Projects/ProjectLoadCandidate.cs`
- Create: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ProjectWorkspacePathPolicy.cs`
- Create: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ReferenceProjectCompatibilityLocator.cs`
- Modify: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ModernProjectStore.cs`
- Modify: `src/ScadaBuilderV2.Application/Pages/IPageWorkspaceReader.cs`
- Modify: `src/ScadaBuilderV2.Application/Pages/IPageWorkspaceStore.cs`
- Modify: `src/ScadaBuilderV2.App/Pages/PageWorkspaceController.cs`
- Modify: `src/ScadaBuilderV2.App/Pages/PageExportInputBuilder.cs`
- Modify: `src/ScadaBuilderV2.App/Pages/PageSourceProjectionResolver.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ModernProjectAtomicSnapshotTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/ProjectWorkspacePathPolicyTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/ReferenceProjectCompatibilityLocatorTests.cs`

**Interfaces:**

- Consumes: appels actuels fondés sur `repositoryRoot`.
- Produces: `ProjectWorkspaceLocation` portant `ProjectRoot`, `ProjectFilePath` et base de provenance optionnelle.

- [ ] **Step 1: Définir le contrat Application.**

`ProjectWorkspaceLocation` reçoit des chemins déjà canonisés par Infrastructure. Il expose la racine exacte contenant `project.json`; il ne reconstruit aucun chemin `SCADA_BUILDER_V2/projects/...`.

- [ ] **Step 2: Généraliser `ModernProjectStore`.**

Faire recevoir la racine projet exacte aux opérations de projet, scène, snapshot, tag import, preview et transaction. Remplacer `GetReferenceModernProjectRoot`, `GetScenePath(repositoryRoot, ...)` et `GetTagImportDirectory(repositoryRoot)` par des résolutions confinées sous `ProjectRoot`.

Ne conserver aucun fallback AMR dans `ModernProjectStore`. L’adaptateur `ReferenceProjectCompatibilityLocator` est le seul endroit autorisé à reconnaître l’ancienne disposition.

- [ ] **Step 3: Adapter les interfaces workspace.**

Remplacer le paramètre `repositoryRoot` par `ProjectWorkspaceLocation` ou `projectRoot` selon le niveau. `PageWorkspaceController.Initialize` devient explicitement réinitialisable et refuse une double initialisation sans transition.

- [ ] **Step 4: Adapter les projections importées.**

`PageSourceProjectionResolver` résout :

1. un chemin relatif sous `ProjectRoot`;
2. sinon, pour une compatibilité déclarée, sous `ImportedSourceBaseRoot`;
3. jamais hors de ces bases.

Un fichier manquant ou échappant aux bases retourne un diagnostic bloquant.

- [ ] **Step 5: Migrer les tests existants.**

Les tests créent directement une racine projet temporaire avec `project.json`, sans fabriquer une fausse arborescence `SCADA_BUILDER_V2/projects/AMR_REF_SCADA_V2`.

- [ ] **Step 6: Tester.**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ProjectWorkspacePathPolicyTests|FullyQualifiedName~ReferenceProjectCompatibilityLocatorTests|FullyQualifiedName~ModernProjectStoreTests|FullyQualifiedName~ModernProjectAtomicSnapshotTests"
rg -n "GetReferenceModernProjectRoot|AMR_REF_SCADA_V2" src/ScadaBuilderV2.Infrastructure/ModernProjects src/ScadaBuilderV2.Application/Pages
```

Expected: tests verts; aucune spécialisation AMR dans le store ou les interfaces Application.

- [ ] **Step 7: Commit.**

```bash
git add src/ScadaBuilderV2.Application/Projects src/ScadaBuilderV2.Application/Pages src/ScadaBuilderV2.Infrastructure/ModernProjects src/ScadaBuilderV2.App/Pages tests/ScadaBuilderV2.Tests
git commit -m "refactor: make project storage root explicit"
```

---

## Phase 2 — Création et ouverture

### Task 3: Implémenter la création transactionnelle d’un projet

**Files:**

- Create: `src/ScadaBuilderV2.Application/Projects/CreateProjectRequest.cs`
- Create: `src/ScadaBuilderV2.Application/Projects/ProjectCreationPolicy.cs`
- Create: `src/ScadaBuilderV2.Application/Projects/ProjectCreationResult.cs`
- Create: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ProjectWorkspaceRepository.cs`
- Create: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ProjectCreationJournal.cs`
- Create: `tests/ScadaBuilderV2.Tests/ProjectCreationPolicyTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/ProjectCreationIntegrationTests.cs`

**Interfaces:**

- Consumes: nom, dossier parent, dossier projet, page initiale, canevas et responsive.
- Produces: projet complet validé et `ProjectLoadCandidate`, ou résultat bloqué sans artefact partiel.

- [ ] **Step 1: Définir la requête et la politique pure.**

Valider nom visible, nom de dossier Windows, parent absolu, cible absente, code via `PageCodePolicy`, titre, dimensions et responsive. Produire le chemin final canonique et un résumé de validation exploitable par WPF.

- [ ] **Step 2: Construire le snapshot initial.**

Utiliser `ScadaProject.CreateDefault`, un `PageKey` neuf, une référence `Default` native `win00001`, `IncludeInBuild = true`, `HomePageKey` défini et une scène vide synchronisée. Le chemin de scène est `scenes/<PageKey:N>.scene.json`.

- [ ] **Step 3: Créer par staging frère.**

Créer `<parent>/.<nom>.create-<id>`, initialiser tous les dossiers D1, sauvegarder le snapshot avec les validations existantes, relire `project.json` et la scène, puis renommer le staging vers le dossier final. Vérifier que le staging et la cible ont le même volume avant de dépendre du renommage atomique.

- [ ] **Step 4: Nettoyer uniquement le staging vérifié.**

Sur erreur/annulation, supprimer seulement le répertoire de staging dont le chemin canonique est contenu dans le parent attendu et dont le nom correspond à l’identifiant de transaction. Ne jamais supprimer une cible préexistante.

- [ ] **Step 5: Tester les échecs.**

Couverture minimale : cible existante, nom réservé, parent inaccessible simulé, JSON/validation staging en échec, annulation, collision concurrente avant rename et succès relisible.

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ProjectCreationPolicyTests|FullyQualifiedName~ProjectCreationIntegrationTests"
```

Expected: projet complet ou aucun projet; première page accueil et incluse dans le build.

- [ ] **Step 6: Commit.**

```bash
git add src/ScadaBuilderV2.Application/Projects src/ScadaBuilderV2.Infrastructure/ModernProjects tests/ScadaBuilderV2.Tests/ProjectCreationPolicyTests.cs tests/ScadaBuilderV2.Tests/ProjectCreationIntegrationTests.cs
git commit -m "feat: create project workspaces transactionally"
```

---

### Task 4: Implémenter l’ouverture fail-closed et la compatibilité de version

**Files:**

- Create: `src/ScadaBuilderV2.Application/Projects/OpenProjectRequest.cs`
- Create: `src/ScadaBuilderV2.Application/Projects/ProjectCompatibilityResult.cs`
- Create: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ProjectOpenValidator.cs`
- Modify: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ProjectWorkspaceRepository.cs`
- Modify: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ModernProjectMigration.cs`
- Create: `tests/ScadaBuilderV2.Tests/ProjectOpenValidatorTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/ProjectOpenIntegrationTests.cs`

**Interfaces:**

- Consumes: chemin absolu d’un `project.json`.
- Produces: candidat complet en mémoire avec diagnostics et migration non persistée.

- [ ] **Step 1: Valider l’entrée.**

Exiger le nom `project.json`, canoniser son dossier parent comme `ProjectRoot`, refuser liens/chemins non résolus selon les capacités .NET disponibles, puis charger sans modifier le manifeste.

- [ ] **Step 2: Définir la politique de version.**

Accepter la génération V2 et les versions de modèle connues. Refuser explicitement une génération ou une version de schéma plus récente. Ne pas comparer la version marketing du projet comme si elle était automatiquement un schéma incompatible; documenter le signal exact utilisé.

- [ ] **Step 3: Récupérer les transactions et préparer le snapshot.**

Exécuter la récupération journalisée existante, migrer projet/scènes en mémoire, valider inventaire, chemins, identités, home page et projections. Le candidat contient le snapshot complet avant toute transition UI.

- [ ] **Step 4: Ne rien persister à l’ouverture.**

Vérifier par hash/horodatage que `project.json` et les scènes ne changent pas lors d’une ouverture/migration réussie. La future sauvegarde atomique persiste le modèle migré.

- [ ] **Step 5: Tester les refus.**

JSON vide/invalide, scène hors racine, collision de codes/paths, scène manquante non créable, version plus récente, projection importée introuvable et journal non récupérable doivent retourner des diagnostics sans candidat.

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ProjectOpenValidatorTests|FullyQualifiedName~ProjectOpenIntegrationTests|FullyQualifiedName~ModernProjectStoreTests"
```

- [ ] **Step 6: Commit.**

```bash
git add src/ScadaBuilderV2.Application/Projects src/ScadaBuilderV2.Infrastructure/ModernProjects tests/ScadaBuilderV2.Tests/ProjectOpenValidatorTests.cs tests/ScadaBuilderV2.Tests/ProjectOpenIntegrationTests.cs
git commit -m "feat: validate and prepare existing projects for opening"
```

---

## Phase 3 — Récents et coordinateur de session

### Task 5: Ajouter le registre utilisateur des projets récents

**Files:**

- Create: `src/ScadaBuilderV2.Application/Projects/RecentProjectEntry.cs`
- Create: `src/ScadaBuilderV2.Application/Projects/IRecentProjectStore.cs`
- Create: `src/ScadaBuilderV2.Infrastructure/Shell/RecentProjectStore.cs`
- Create: `tests/ScadaBuilderV2.Tests/RecentProjectStoreTests.cs`

**Interfaces:**

- Consumes: projet créé/ouvert avec succès.
- Produces: `%AppData%\ScadaBuilderV2\recent-projects.json`, douze entrées maximum.

- [ ] **Step 1: Définir le modèle Application.**

Conserver `DisplayName`, chemin canonique du `project.json`, `LastOpenedUtc` et état de disponibilité calculé à la lecture. Ne pas sérialiser l’état calculé comme autorité.

- [ ] **Step 2: Implémenter le store tolérant.**

Lecture d’un fichier absent/corrompu retourne une liste vide avec diagnostic non bloquant. Écriture utilise un fichier temporaire puis remplacement. Dédupliquer sans tenir compte de la casse, trier décroissant, tronquer à douze.

- [ ] **Step 3: Implémenter ajout et retrait.**

Ajouter seulement après une création/ouverture/activation réussie. Retirer une entrée par chemin sans toucher au dossier projet. Conserver une entrée manquante et la marquer indisponible.

- [ ] **Step 4: Mémoriser le dernier dossier de création.**

Ajouter ce paramètre dans le même espace utilisateur, avec fallback `Environment.SpecialFolder.MyDocuments\SCADA Builder V2 Projects`.

- [ ] **Step 5: Tester.**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~RecentProjectStoreTests"
```

Expected: déduplication Windows, limite 12, tri, retrait non destructif, corruption tolérée et fallback Documents.

- [ ] **Step 6: Commit.**

```bash
git add src/ScadaBuilderV2.Application/Projects src/ScadaBuilderV2.Infrastructure/Shell/RecentProjectStore.cs tests/ScadaBuilderV2.Tests/RecentProjectStoreTests.cs
git commit -m "feat: persist recent project history"
```

---

### Task 6: Implémenter `ProjectLifecycleCoordinator` et les commandes `project.*`

**Files:**

- Create: `src/ScadaBuilderV2.Application/Projects/ProjectLifecycleState.cs`
- Create: `src/ScadaBuilderV2.Application/Projects/ProjectLifecycleCoordinator.cs`
- Create: `src/ScadaBuilderV2.Application/Projects/IProjectSessionHost.cs`
- Create: `src/ScadaBuilderV2.Application/Projects/IProjectClosePolicy.cs`
- Create: `src/ScadaBuilderV2.Application/Projects/ProjectCloseDecision.cs`
- Create: `src/ScadaBuilderV2.Application/Commands/Projects/NewProjectCommand.cs`
- Create: `src/ScadaBuilderV2.Application/Commands/Projects/OpenProjectCommand.cs`
- Create: `src/ScadaBuilderV2.Application/Commands/Projects/SaveProjectCommand.cs`
- Create: `src/ScadaBuilderV2.Application/Commands/Projects/CloseProjectCommand.cs`
- Create: `src/ScadaBuilderV2.Application/Commands/Projects/ReopenLastProjectCommand.cs`
- Create: `tests/ScadaBuilderV2.Tests/ProjectLifecycleCoordinatorTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/ProjectApplicationCommandTests.cs`

**Interfaces:**

- Consumes: candidat créé/ouvert, session active, dirty policy, repository et récents.
- Produces: machine d’état `Empty/Transitioning/Active` non réentrante.

- [ ] **Step 1: Définir les ports sans WPF.**

`IProjectSessionHost` expose la préparation des changements transitoires, l’activation d’un candidat, la restauration de l’ancienne session en cas d’échec et le teardown. `IProjectClosePolicy` retourne `Save`, `Discard` ou `Cancel`.

- [ ] **Step 2: Implémenter le gate de transition.**

Une commande simultanée est bloquée avec résultat structuré. Chaque transition accepte un `CancellationToken`. Aucun `.Wait()`, `.Result` ou handler `async void` n’entre dans Application.

- [ ] **Step 3: Implémenter la préparation avant remplacement.**

Pour ouvrir : préparer/valider le candidat avant le prompt dirty. Pour créer : valider la requête et préparer le staging avant le prompt; ne committer le dossier final qu’après autorisation de remplacer la session.

- [ ] **Step 4: Implémenter le dirty gate partagé.**

`Save` appelle la sauvegarde snapshot complète et n’avance que sur succès. `Discard` n’écrit rien. `Cancel` retourne à `Active` sans modification. La session précédente reste disponible jusqu’à l’activation réussie du candidat.

- [ ] **Step 5: Implémenter fermeture et sauvegarde.**

`project.save` sauvegarde le workspace complet, et non seulement la scène active. `project.close` retourne à `Empty`. La fermeture de fenêtre invoquera exactement la même transition avant de fermer le shell.

- [ ] **Step 6: Tester la matrice.**

Tester les transitions :

- `Empty -> Create -> Active`;
- `Empty -> Open -> Active`;
- `Active clean -> Open -> Active`;
- `Active dirty -> Save/Discard/Cancel`;
- candidat invalide;
- sauvegarde en échec;
- activation en échec avec ancienne session conservée;
- double exécution bloquée;
- `Active -> Close -> Empty`.

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ProjectLifecycleCoordinatorTests|FullyQualifiedName~ProjectApplicationCommandTests"
```

- [ ] **Step 7: Commit.**

```bash
git add src/ScadaBuilderV2.Application/Projects src/ScadaBuilderV2.Application/Commands/Projects tests/ScadaBuilderV2.Tests/ProjectLifecycleCoordinatorTests.cs tests/ScadaBuilderV2.Tests/ProjectApplicationCommandTests.cs
git commit -m "feat: coordinate project lifecycle transitions"
```

---

## Phase 4 — Session WPF et accueil

### Task 7: Extraire l’activation et le teardown projet hors de `MainWindow`

**Files:**

- Create: `src/ScadaBuilderV2.App/Projects/ProjectSessionController.cs`
- Create: `src/ScadaBuilderV2.App/Projects/ProjectSessionSnapshot.cs`
- Create: `src/ScadaBuilderV2.App/Projects/ProjectClosePolicy.cs`
- Modify: `src/ScadaBuilderV2.App/Pages/PageWorkspaceController.cs`
- Modify: `src/ScadaBuilderV2.App/Diagnostics/DiagnosticsPanelViewModel.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs`
- Create: `tests/ScadaBuilderV2.Tests/ProjectSessionControllerTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/PageWorkspaceExtractionContractTests.cs`

**Interfaces:**

- Consumes: `ProjectLoadCandidate`, host WPF, workspace pages et stores.
- Produces: activation/teardown atomiques du point de vue de la session UI.

- [ ] **Step 1: Définir le snapshot de session.**

Inclure emplacement, projet, workspace, références importées, état de librairie et informations nécessaires à une restauration. Ne pas capturer des contrôles WPF dans Application.

- [ ] **Step 2: Ajouter `ResetAsync` au workspace.**

Fermer les onglets sans reprompt après décision globale, vider `History`, scènes retenues, suppressions, dirty state et références de projet. `Initialize` après reset doit produire une session propre.

- [ ] **Step 3: Centraliser le teardown host.**

Le contrôleur doit :

1. sauvegarder l’état transitoire de l’onglet actif;
2. arrêter watcher/timer de librairie;
3. annuler export/import/preview en cours;
4. détacher ou neutraliser le contenu WebView propre à la page;
5. vider sélection, objets source, tags, librairie et diagnostics;
6. réinitialiser propriétés/page/ruban;
7. libérer `_referenceProject`, `_modernProject`, `_activeScene`, `_activeSceneTab` et l’ancien contexte de chemin.

- [ ] **Step 4: Centraliser l’activation.**

Initialiser le workspace avec la racine exacte, charger les panneaux depuis le projet, sélectionner la page d’accueil/première page, démarrer la librairie locale et afficher le projet. Pour un projet sans page, conserver un workspace actif sans onglet.

- [ ] **Step 5: Brancher le prompt dirty.**

Le prompt global mentionne le nom du projet et le nombre d’onglets/scènes modifiés. Il ne déclenche pas un second prompt page par page après la décision.

- [ ] **Step 6: Tester et vérifier l’extraction.**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ProjectSessionControllerTests|FullyQualifiedName~PageWorkspaceExtractionContractTests|FullyQualifiedName~ProjectWorkspaceHistoryTests"
rg -n "GetReferenceModernProjectRoot|EnsureReferenceModernProjectAsync|LoadReferenceProjectAsync" src/ScadaBuilderV2.App/MainWindow.xaml.cs
```

Expected: aucun bootstrap AMR direct dans `MainWindow`; teardown sans état résiduel.

- [ ] **Step 7: Commit.**

```bash
git add src/ScadaBuilderV2.App/Projects src/ScadaBuilderV2.App/Pages/PageWorkspaceController.cs src/ScadaBuilderV2.App/Diagnostics/DiagnosticsPanelViewModel.cs src/ScadaBuilderV2.App/MainWindow.xaml.cs src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs tests/ScadaBuilderV2.Tests
git commit -m "refactor: extract active project session from MainWindow"
```

---

### Task 8: Créer l’accueil et le dialogue de création

**Files:**

- Create: `src/ScadaBuilderV2.App/Projects/WelcomeProjectViewModel.cs`
- Create: `src/ScadaBuilderV2.App/Projects/RecentProjectItemViewModel.cs`
- Create: `src/ScadaBuilderV2.App/Projects/CreateProjectDialog.xaml`
- Create: `src/ScadaBuilderV2.App/Projects/CreateProjectDialog.xaml.cs`
- Create: `src/ScadaBuilderV2.App/Projects/CreateProjectViewModel.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Modify: `src/ScadaBuilderV2.App/Resources/Icons.xaml`
- Create: `tests/ScadaBuilderV2.Tests/ProjectWelcomeSurfaceContractTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/CreateProjectDialogContractTests.cs`

**Interfaces:**

- Consumes: politiques Application, récents et commandes `project.*`.
- Produces: accueil productif et assistant WPF sans logique de fichier.

- [ ] **Step 1: Ajouter l’état d’accueil central.**

Afficher l’accueil lorsque le coordinateur est `Empty`; masquer l’espace d’édition ou afficher son placeholder. L’accueil présente Nouveau, Ouvrir, Rouvrir le dernier et douze récents maximum.

- [ ] **Step 2: Ajouter le dialogue de création.**

Le dialogue comporte nom, dossier projet, parent, Parcourir, chemin final, page code/titre, dimensions et responsive. Les erreurs de validation sont inline; `Créer` reste désactivé tant que la requête est invalide.

- [ ] **Step 3: Appliquer les valeurs par défaut.**

Première utilisation : `Documents\SCADA Builder V2 Projects`. Utilisations suivantes : dernier parent valide. Valeurs initiales : `win00001`, `Page principale`, preset desktop et responsive courant.

- [ ] **Step 4: Ajouter le picker d’ouverture.**

Utiliser `OpenFileDialog` filtré sur `project.json`. Le picker fournit seulement une intention; le repository Application/Infrastructure valide le fichier.

- [ ] **Step 5: Ajouter les actions récentes.**

Double-clic ou bouton Ouvrir utilise `project.open`. `Rouvrir` utilise le premier récent disponible. Retirer demande seulement une confirmation de retrait de liste et ne mentionne jamais une suppression de fichiers.

- [ ] **Step 6: Respecter le polish industriel.**

Utiliser les styles, icônes sémantiques, espacements et états disabled du produit. Aucun placeholder technique, chemin tronqué non découvrable ou bouton actif sans commande.

- [ ] **Step 7: Tester.**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ProjectWelcomeSurfaceContractTests|FullyQualifiedName~CreateProjectDialogContractTests|FullyQualifiedName~ProjectCreationPolicyTests"
```

- [ ] **Step 8: Commit.**

```bash
git add src/ScadaBuilderV2.App/Projects src/ScadaBuilderV2.App/MainWindow.xaml src/ScadaBuilderV2.App/MainWindow.xaml.cs src/ScadaBuilderV2.App/Resources/Icons.xaml tests/ScadaBuilderV2.Tests
git commit -m "feat: add project welcome and creation surfaces"
```

---

### Task 9: Brancher le ruban, la sauvegarde, la fermeture de fenêtre et tous les chemins projet-locaux

**Files:**

- Modify: `src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs`
- Modify: `src/ScadaBuilderV2.Application/Commands/ApplicationContext.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Modify: `src/ScadaBuilderV2.App/Pages/PageExportInputBuilder.cs`
- Modify: `src/ScadaBuilderV2.App/Pages/PageSourceProjectionResolver.cs`
- Modify: `src/ScadaBuilderV2.App/Diagnostics/DiagnosticsPanelViewModel.cs`
- Modify: `tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/ProjectCommandSurfaceContractTests.cs`

**Interfaces:**

- Consumes: session active et commandes `project.*`.
- Produces: enablement cohérent et aucune dérivation projet depuis le dépôt logiciel.

- [ ] **Step 1: Activer les commandes.**

Activer `project.new` et `project.open` sans projet. Ajouter `project.close`. `project.save` et `project.close` requièrent une session active. Ajouter `project.reopen-last` seulement à l’accueil ou le rendre disabled avec raison selon la métadonnée de surface retenue.

- [ ] **Step 2: Faire de `project.save` une sauvegarde workspace.**

Remplacer le libellé/tooltip « scène active » et router vers `SaveProjectCommand`. Garder le bouton page/scène local uniquement s’il possède une responsabilité distincte documentée; autrement unifier.

- [ ] **Step 3: Unifier la fermeture de fenêtre.**

`OnMainWindowClosing` attend la transition `project.close` partagée. Sur `Cancel` ou erreur de sauvegarde, annuler la fermeture. Sur succès, sauvegarder le layout puis fermer sans reprompt.

- [ ] **Step 4: Migrer tous les chemins locaux.**

Passer par `ActiveProject.Location.ProjectRoot` pour :

- scènes et snapshot;
- `.studio/preview`;
- `library/elements`;
- `imports/tags`;
- `exports`;
- export dossier et `.sb2`;
- lancement Studio Element+;
- résolution de provenance.

`ResolveRepositoryRoot` peut subsister uniquement pour lire `VERSION` ou des ressources de développement clairement nommées; il ne choisit plus le projet.

- [ ] **Step 5: Vérifier par recherche.**

```powershell
rg -n "_repositoryRoot|GetReferenceModernProjectRoot|AMR_REF_SCADA_V2|LoadReferenceProjectAsync" src/ScadaBuilderV2.App src/ScadaBuilderV2.Infrastructure src/ScadaBuilderV2.Application
```

Expected: chaque résultat restant appartient à l’adaptateur de compatibilité ou à une ressource produit explicitement justifiée.

- [ ] **Step 6: Tester.**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~RibbonCommandCatalogTests|FullyQualifiedName~ProjectCommandSurfaceContractTests|FullyQualifiedName~ProjectApplicationCommandTests|FullyQualifiedName~PageWorkspaceExtractionContractTests"
```

- [ ] **Step 7: Commit.**

```bash
git add src/ScadaBuilderV2.Application/Commands src/ScadaBuilderV2.App src/ScadaBuilderV2.Infrastructure tests/ScadaBuilderV2.Tests
git commit -m "feat: wire project lifecycle through the WPF shell"
```

---

## Phase 5 — Intégration et compatibilité

### Task 10: Ajouter les scénarios d’intégration end-to-end

**Files:**

- Create: `tests/ScadaBuilderV2.Tests/ProjectLifecycleIntegrationTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/NativePageDocumentTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ReferenceScadaProjectReaderTests.cs`

**Interfaces:**

- Consumes: cycle complet sur projets temporaires.
- Produces: preuve de persistance, isolation et compatibilité.

- [ ] **Step 1: Scénario nouveau projet.**

`Empty -> Create -> première page active -> modifier -> Save -> Close -> Open -> export .sb2`. Vérifier structure, home page, build, scène, preview, librairie, tags et export sous la racine choisie.

- [ ] **Step 2: Scénarios dirty.**

Depuis un projet modifié, tenter l’ouverture d’un autre projet avec :

- `Cancel`: premier projet intact et actif;
- `Discard`: aucun fichier du premier projet modifié;
- `Save`: snapshot relisible avant activation du second;
- échec de sauvegarde: premier projet actif.

- [ ] **Step 3: Scénarios de cible invalide.**

Ouvrir un JSON invalide, une version plus récente et une scène échappant à la racine. Vérifier que la session active ne change pas et qu’aucun récent n’est ajouté.

- [ ] **Step 4: Scénario récent.**

Créer deux projets, fermer, relire le store, rouvrir le dernier, retirer l’autre, puis vérifier que le dossier retiré existe toujours.

- [ ] **Step 5: Compatibilité AMR sur copie.**

Copier uniquement les données requises dans un environnement temporaire reproduisant la base legacy, ouvrir le `project.json`, charger `win00009`, sauvegarder dans la copie et exporter. Vérifier que les sources Wonderware originales ne changent pas.

- [ ] **Step 6: Parité `.sb2`.**

Comparer l’export d’une fixture avant/après généralisation des chemins. Le manifeste, les pages, racines DOM, capacités runtime et validations doivent rester sémantiquement identiques.

- [ ] **Step 7: Exécuter tests ciblés puis suite complète.**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ProjectLifecycleIntegrationTests|FullyQualifiedName~ProjectCreationIntegrationTests|FullyQualifiedName~ProjectOpenIntegrationTests|FullyQualifiedName~RecentProjectStoreTests|FullyQualifiedName~Ft100SceneExporterTests"
dotnet test ScadaBuilderV2.sln --no-restore
```

Expected: ciblés verts; suite complète identique ou meilleure que la baseline.

- [ ] **Step 8: Commit.**

```bash
git add tests/ScadaBuilderV2.Tests
git commit -m "test: cover project lifecycle end to end"
```

---

### Task 11: Exécuter le smoke WPF isolé

**Files:**

- Inspect: `src/ScadaBuilderV2.App/**`
- Create temporarily: projets sous un dossier de smoke hors dépôt
- Avoid modifying: `projects/AMR_REF_SCADA_V2/**`
- Avoid modifying: `SCADA_BUILDER/AMR_SCADA/AMR_REF_SCADA/**`

**Interfaces:**

- Consumes: build Release et dossiers de test.
- Produces: preuve visuelle et comportementale du parcours utilisateur.

- [ ] **Step 1: Démarrer sans projet.**

Vérifier accueil, absence de projet ouvert, ruban, disabled reasons et absence de chargement AMR.

- [ ] **Step 2: Créer un projet.**

Vérifier Parcourir, validation inline, chemin final, première page, statut, panneau Projet, preview et arborescence disque.

- [ ] **Step 3: Tester sauvegarde et fermeture.**

Modifier une scène puis couvrir `Annuler`, `Ne pas enregistrer` et `Enregistrer`. Vérifier le retour à l’accueil et l’absence d’état ancien.

- [ ] **Step 4: Tester ouverture et récents.**

Ouvrir deux projets, utiliser `Rouvrir le dernier`, retirer une entrée et confirmer que les fichiers persistent.

- [ ] **Step 5: Tester les erreurs.**

Sélectionner un `project.json` invalide; vérifier diagnostic exploitable, session courante intacte et absence de crash.

- [ ] **Step 6: Tester la copie AMR.**

Ouvrir la copie autorisée, afficher `win00009`, vérifier la librairie et produire un `.sb2` sans modifier la source.

- [ ] **Step 7: Consigner les résultats.**

Ajouter au journal d’exécution du présent plan les cas réussis, les chemins temporaires et toute exception. Ne jamais déclarer le smoke réussi sans exécution interactive réelle.

---

## Phase 6 — Documentation, version et clôture

### Task 12: Synchroniser les contrats, la couverture et la version

**Files:**

- Modify: `VERSION`
- Modify: `docs/README.md`
- Modify: `docs/00_governance/DECISION_REGISTER_V2.md`
- Modify: `docs/02_architecture/APPLICATION_FLOW_V2.md`
- Modify: `docs/02_architecture/DATA_MODEL_OVERVIEW_V2.md`
- Modify: `docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md`
- Modify: `docs/04_editor/COMMANDS_CONTRACT_V2.md`
- Modify: `docs/04_editor/STATE_MANAGEMENT_CONTRACT_V2.md`
- Modify: `docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md`
- Modify: `docs/06_ui_ux/UI_ARCHITECTURE_V2.md`
- Modify: `docs/06_ui_ux/UI_SPECIFICATION_V2.md`
- Modify: `docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md`
- Modify: `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md`
- Modify: `docs/08_implementation_status/KNOWN_GAPS_V2.md`
- Modify: `docs/10_generated/CODE_MAP_V2.md`
- Modify: `docs/10_generated/COMMAND_FLOW_DIAGRAM_V2.md`
- Modify: `docs/10_generated/STATE_FLOW_DIAGRAM_V2.md`
- Modify: `docs/superpowers/specs/2026-07-29-project-lifecycle-design.md`
- Modify: `docs/superpowers/plans/2026-07-29-project-lifecycle.md`

**Interfaces:**

- Consumes: code, tests et smoke réellement validés.
- Produces: statut traçable et version de capacité majeure.

- [ ] **Step 1: Mettre à jour uniquement les faits implémentés.**

Passer `DEC-0049`, la spec et le plan à `Implemented` seulement si le smoke et les tests requis sont terminés. Sinon conserver un statut partiel précis et enregistrer les gaps.

- [ ] **Step 2: Mettre à jour les cartes et diagrammes.**

Représenter le nouveau cycle de vie, les propriétaires et les tests. Régénérer les fichiers générés avec les outils existants si applicable.

- [ ] **Step 3: Appliquer le bump feature.**

À partir de la version réelle au moment de la clôture :

```powershell
$currentVersion = (Get-Content -Raw VERSION).Trim()
python "C:\Users\mathi\.codex\skills\scada-builder-v2-versioning\scripts\bump_scada_v2_version.py" $currentVersion feature
```

Expected pour la ligne actuelle `V2.1.4.x`: `V2.1.5.0000`. Appliquer cette version à `VERSION` et aux nouvelles lignes d’historique de clôture.

- [ ] **Step 4: Valider documentation et code.**

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
rg -n "GetReferenceModernProjectRoot|LoadReferenceProjectAsync|project\.new|project\.open|project\.close|recent-projects|DEC-0049" src docs tests
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore
git status --short --branch
```

Expected: documentation valide, tests conformes, spécialisations restantes justifiées, aucune modification utilisateur étrangère incluse.

- [ ] **Step 5: Commit de clôture.**

```bash
git add VERSION docs src tests
git commit -m "feat: deliver modern project lifecycle"
```

Le staging doit être vérifié avec `git diff --cached --name-only` afin d’exclure les modifications préexistantes de `projects/AMR_REF_SCADA_V2`.

---

## Validation Checklist

- [ ] L’application démarre sur l’accueil sans ouvrir AMR ou le dernier projet.
- [ ] `project.new`, `project.open`, `project.save`, `project.close` et `project.reopen-last` passent par Application.
- [ ] Un dialogue permet de choisir parent, nom, dossier, page initiale, dimensions et responsive.
- [ ] Le premier parent proposé est sous Documents et le dernier parent valide est mémorisé.
- [ ] La création produit un projet complet ou aucun artefact.
- [ ] La première page est native, `Default`, incluse dans le build et définie comme accueil.
- [ ] Une ouverture invalide ne ferme ni ne modifie le projet courant.
- [ ] Une version future est refusée avec diagnostic.
- [ ] L’ouverture ne persiste pas automatiquement une migration.
- [ ] Une fenêtre possède au plus une session active.
- [ ] Les transitions concurrentes sont bloquées.
- [ ] Save/Discard/Cancel est partagé par nouveau, ouvrir, fermer et quitter.
- [ ] `project.save` persiste le snapshot workspace complet.
- [ ] `project.close` revient à un shell vide sans fermer l’application.
- [ ] Aucun onglet, historique, watcher, sélection, diagnostic, tag, librairie ou aperçu de l’ancien projet ne subsiste.
- [ ] Les récents sont dédupliqués, triés, limités à douze et jamais ouverts automatiquement.
- [ ] Retirer un récent ne supprime aucun fichier.
- [ ] `Rouvrir le dernier projet` choisit la première entrée disponible.
- [ ] Toutes les données projet-locales utilisent la racine exacte du projet actif.
- [ ] Le store général ne contient aucune spécialisation `AMR_REF_SCADA_V2`.
- [ ] La copie de `AMR_REF_SCADA_V2` s’ouvre avec ses provenances legacy confinées.
- [ ] Aucun fichier Wonderware source n’est modifié.
- [ ] `.sb2` reste un artefact runtime inchangé, sans rétro-ingénierie projet.
- [ ] Preview/build/export restent en parité.
- [ ] Les tests ciblés et la suite complète respectent la baseline fraîche.
- [ ] Le smoke WPF isolé couvre création, ouverture, fermeture, récents et erreurs.
- [ ] Les documents touchés ont leurs en-têtes requis et les `PENDING` sont explicitement tracés.
- [ ] Le bump feature est appliqué uniquement après validation complète.
- [ ] Les modifications préexistantes du projet de référence ne sont ni altérées ni incluses dans les commits de cette tranche.
