# Fenêtres rapides paramétrées - Plan d’implémentation

Date: 2026-08-10
Status: Draft implementation plan - phase 0 isolation gate pending
Document version: `V2.1.5.0020`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-08-10 | `V2.1.5.0020` | `PENDING` | Corrections des 5 lacunes de revue : matrice FR→Tasks, anti-injection `Literal`/`Expression`, `PresentationDefaults` explicites, rejet profondeur 3 et épinglage Node LTS. |
| 2026-08-10 | `V2.1.5.0019` | `PENDING` | Renforcement après revue : boucle d’itération du prototype, audit popup mesurable, rollback inter-phase, gate export structurel, handshakes cross-repository précoces, races, SLA, version WebView2, extraction hors `MainWindow`, versioning et canary TF100Web. |
| 2026-08-10 | `V2.1.5.0018` | `PENDING` | Création du plan dérivé de `DEC-0050`; la phase 0 de prototype DOM/CSS est un gate bloquant avant toute modification de production. |

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Livrer une première verticale Fenêtre rapide fondée sur `win00054`, de l’authoring SCADA Builder V2 au runtime TF100Web, avec une définition réutilisable, deux invocations aux mappings moteur indépendants, un cycle host-owned et aucune fuite DOM, CSS, état, souscription ou écriture entre contextes.

**Architecture:** `QuickWindowDefinition` et `PageDefinition` possèdent chacune un `VisualContent` commun par composition. Une invocation persistante référence une définition et porte ses liaisons typées; un gestionnaire host crée un `RuntimeInstanceId` temporaire dans une racine DOM scoppée. Le runtime partagé conserve les sémantiques portables; le preview Builder et TF100Web fournissent chacun l’adaptateur host, le cache de tags et le pont d’écriture. Aucun héritage page/fenêtre, aucun second poller et aucun chemin Fragment de substitution ne sont permis.

**Tech Stack:** C# 12, .NET 8, WPF/WebView2, JSON `System.Text.Json`, JavaScript ES modules, MSTest, Node 20 LTS (20.18.x, épinglée via `.nvmrc` + `package.json` `engines.node`, `node:test`), Django/Python et tests TF100Web. La version Node exacte est consignée dans les rapports Phase 0/7 et vérifiée au `Before You Start`.

## Global Constraints

- Spec propriétaire: `docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md` (`DEC-0050`, `FR-001` à `FR-029`, `FR-UI-01` à `FR-UI-22`).
- Conformance runtime: `DEC-0047`; chaque capacité `quick-window.*` ou `command.*-quick-window` commence `Blocked` et exige des preuves Builder, runtime partagé et TF100Web avant promotion.
- La phase 0 valide `FR-020` et `FR-026`. Aucune modification des projets `src/`, du modèle persistant, de l’authoring, du compilateur, du package ou du runtime de production n’est autorisée avant son succès documenté.
- Les identités sont exactement `QuickWindowDefinitionKey`, `InvocationKey` et `RuntimeInstanceId`; aucun `InstanceKey` additionnel n’est introduit (`FR-027`).
- La politique V1 est `SinglePerDefinition`; la même invocation est remise au premier plan, une autre invocation de la même définition ferme puis recrée le contexte.
- Le runtime, le cache de tags, le poller et le pont d’écriture restent uniques et partagés.
- Les profils 2.1/2.2 et les hosts 2.3 sans capacités requises refusent les Fenêtres rapides sans conversion en Fragment.
- `OpenPopup`, `TogglePopup` et `ClosePopup` sont retirés du modèle de commande moderne. `MountFragment`, les actions popup et `ScadaPopupOptions` restent des résidus legacy à auditer/décommissionner explicitement, jamais des sources de migration ou de preuve.
- Les données du banc d’essai, overlays, handles, diagnostics et autres états editor-only ne sont jamais persistés dans le modèle runtime ni exportés dans `.sb2`.
- La première tranche de production exclut l’imbrication, la conversion de Fragment, l’adaptateur `iframe`, la copie de liaisons, la personnalisation avancée du cadre et les placements autres que centrés; la phase 0 doit néanmoins prouver `Page -> A -> B` pour valider la stratégie d’isolation.
- Les APIs publiques ajoutées reçoivent des commentaires XML avec `Decisions: DEC-0050`, contrats et tests associés.
- Aucun code QuickWindow substantiel n’est ajouté à `MainWindow.xaml.cs`; ce fichier conserve uniquement les hooks de composition indispensables. Les handlers, view models et coordinations vivent dans des fichiers/classes dédiés.
- Les séquences concurrentes font partie du contrat: double ouverture, navigation pendant montage, fermeture pendant hydratation, ouverture pendant dispose et résultats asynchrones stale sont testés par générations monotones et cleanup idempotent.

---

## Before You Start

- [ ] Dans SCADA Builder V2, exécuter `git status --short --branch` et terminer ou isoler les changements existants. Ne commencer aucune tâche de production dans un worktree sale.
- [ ] Dans `F:\Projet\Git\TF100Web`, exécuter `git status --short --branch`; utiliser une branche `codex/quick-window-v1` dédiée avant toute modification autorisée.
- [ ] Capturer les commits de départ des deux dépôts dans le rapport de phase 0, puis le résultat frais de:

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore
node --version; Get-Content .nvmrc; Get-Content package.json | Select-String -Pattern "engines"
npm --prefix tests/runtime-js test

Set-Location "F:\Projet\Git\TF100Web"
python -m pytest frontend/tests_scada_package.py frontend/tests_scada_page_composition.py frontend/tests_scada_deploy.py
node --version; node --test frontend/tests_runtime_js/*.test.mjs
```

Expected: conserver le résultat frais comme baseline; toute nouvelle régression doit être expliquée et corrigée avant commit.

- [ ] Vérifier épinglage Node LTS: `node --version`, `.nvmrc` et `package.json` `engines.node` doivent correspondre à `Tech Stack` (`20.18.x`). Toute divergence bloque le démarrage.

- [ ] Confirmer que `win00054` et ses mappings de référence sont des données de test/acceptation sans autorisation d’écriture PLC réelle. Les smokes de production restent read-only jusqu’à une fenêtre industrielle explicitement autorisée.
- [ ] Respecter les gates cross-repo: aucun déploiement TF100Web, activation d’export Builder ou écriture PLC n’est autorisé implicitement par ce plan.

## Phase Completion, Rollback and Version Policy

- [ ] Avant chaque phase, enregistrer `git rev-parse HEAD`, la branche, les résultats de baseline et les commits de la phase précédente dans `artifacts/quick-window-rollout/checkpoints.json` (artefact local non versionné).
- [ ] Une phase n’est complète que si tous ses tests ciblés et la baseline pertinente réussissent, que le worktree est propre et que son dernier commit ne rend aucune surface partielle visible ou exportable.
- [ ] Tant que Phase 6 n’est pas franchie, le modèle et l’authoring restent inertes derrière les capacités `Blocked`; aucun projet existant n’est migré et aucun package QuickWindow productible ne peut être émis.
- [ ] Si une phase échoue, ne pas continuer avec un sous-ensemble. Corriger sur la branche de phase ou revenir par `git revert` aux commits cohérents de cette phase; ne pas utiliser `git reset --hard`. Conserver les phases antérieures uniquement si leurs APIs restent backward-compatible, invisibles et vertes. Sinon, les réverter dans l’ordre inverse.
- [ ] Une branche partielle n’est ni fusionnée, ni déployée, ni utilisée pour authorer des données durables. Le point de reprise est le dernier checkpoint vert des deux dépôts.
- [ ] Politique de version: les révisions de plan, prototypes, tests, contrats encore `Blocked` et corrections préparatoires utilisent un bump `iteration`. La première activation réellement livrable des capacités QuickWindow en Phase 6 utilise un bump `feature` (`V2.<production>.<feature+1>.0000`). Les corrections/acceptations ultérieures reprennent des bumps `iteration`. Aucun bump `production` sans approbation explicite d’un jalon preview/pilote/RC/production.

---

## Phase 0 - Prototype d’isolation DOM/CSS (gate bloquant)

> **Gate absolu:** les tâches des phases 1 à 7 sont interdites tant que Task 0.4 n’a pas conclu `PASS` dans les deux hosts. Les fichiers de cette phase restent sous `tools/prototypes`, `tests` ou `frontend/test_fixtures`; aucun fichier de production sous `src/`, `frontend/scada_*.py`, `templates/` ou `static/` ne doit être modifié.

### Task 0.1: Construire une fixture d’isolation host-agnostic

**Files:**
- Create: `tools/prototypes/quick-window-dom-css-isolation/README.md`
- Create: `tools/prototypes/quick-window-dom-css-isolation/index.html`
- Create: `tools/prototypes/quick-window-dom-css-isolation/prototype.css`
- Create: `tools/prototypes/quick-window-dom-css-isolation/prototype.js`
- Create: `tools/prototypes/quick-window-dom-css-isolation/assertions.js`
- Create: `tools/prototypes/quick-window-dom-css-isolation/evidence.schema.json`
- Create: `tests/runtime-js/quick-window-dom-css-isolation.test.mjs`

**Interfaces:**
- Consumes: invariants `FR-020`, `FR-026`, `FR-UI-01` à `FR-UI-10` et API instrumentée de cache/pont simulé.
- Produces: fixture statique déterministe `Page -> A -> B`, schéma versionné `PrototypeRevision`, assertions core machine-readable et extensions host namespacées.

- [ ] **Step 1: Définir les sentinelles de collision**

Créer une page, deux définitions A/B actives et des ids auteur volontairement identiques. Couvrir ids DOM, `for`, `aria-*`, `href`/`xlink:href`, `url(#...)`, classes, animations/keyframes, tableaux, inputs, cibles d’état et de commande. Les sélecteurs doivent être relatifs à la racine de l’instance et chaque racine doit exposer les trois identités canoniques.

- [ ] **Step 2: Instrumenter le cycle de vie**

Compter listeners, observers, timers, souscriptions, dépendances de cache, écritures et nombre de pollers. Exécuter au moins 100 cycles ouverture/fermeture, fermer A pendant que B est active, rejeter une hydratation stale et prouver que les compteurs reviennent exactement à la baseline.

Ajouter les races déterministes suivantes avec barrières contrôlées: double-clic produisant deux `open()` concurrents, navigation pendant `Mounting`, fermeture pendant `Hydrating`, ouverture d’une autre invocation pendant `Closing/Disposed`, snapshot ancien arrivant après le nouveau contexte et dispose appelé deux fois. Une seule génération peut devenir `Active`; les autres doivent finir `Disposed` sans mutation tardive.

- [ ] **Step 3: Couvrir focus et modalité**

Vérifier ordre modal, focus initial, tabulation confinée, `X`, `Escape`, fermeture enfant puis parent, fermeture parent entraînant le dispose enfant et retour du focus à l’élément propriétaire.

- [ ] **Step 4: Tester la fixture sans host produit**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
npm --prefix tests/runtime-js test
```

Expected: la suite existante reste conforme et la fixture produit un rapport déterministe sans collision ni compteur résiduel.

- [ ] **Step 5: Établir la baseline performance du prototype**

Sur la machine de référence, exécuter 30 ouvertures froides et 100 ouvertures chaudes après 10 warmups; consigner CPU, RAM, versions OS/WebView2/Node (Node 20.18.x épinglée via `.nvmrc`/`engines.node`) et p50/p95 `request -> Active`. Le SLA de sortie est: p95 chaud ≤ 500 ms, p95 froid ≤ 1 500 ms et aucune régression > 10 % entre la fixture gelée et son portage de production sur la même machine. Toute mesure hors seuil bloque le gate ou exige une décision explicitement documentée.

- [ ] **Step 6: Commit prototype commun**

```powershell
git add tools/prototypes/quick-window-dom-css-isolation tests/runtime-js/quick-window-dom-css-isolation.test.mjs
git commit -m "test: prototype quick window DOM isolation"
```

### Task 0.2: Exécuter la fixture dans un WebView2 isolé représentant le preview Builder

**Files:**
- Create: `tools/QuickWindowIsolationPrototype.App/QuickWindowIsolationPrototype.App.csproj`
- Create: `tools/QuickWindowIsolationPrototype.App/App.xaml`
- Create: `tools/QuickWindowIsolationPrototype.App/App.xaml.cs`
- Create: `tools/QuickWindowIsolationPrototype.App/MainWindow.xaml`
- Create: `tools/QuickWindowIsolationPrototype.App/MainWindow.xaml.cs`
- Create: `tests/ScadaBuilderV2.Tests/QuickWindowIsolationPrototypeContractTests.cs`
- Avoid: `src/ScadaBuilderV2.App/**`, `src/ScadaBuilderV2.Rendering/**`

**Interfaces:**
- Consumes: fixture de Task 0.1 et moteur WebView2 utilisé par le produit.
- Produces: `artifacts/quick-window-isolation/builder-webview2.json`, capture visuelle, version SDK/runtime WebView2 et exit code non nul en cas d’échec.

- [ ] **Step 1: Créer le harness hors production**

Le harness référence exactement `Microsoft.Web.WebView2` `1.0.3967.48`, la version épinglée dans les deux applications. Il charge la fixture locale, attend l’événement final, sérialise toutes les assertions et se ferme automatiquement. Il ne référence aucun état global du `MainWindow` produit et n’ajoute pas son projet à `ScadaBuilderV2.sln` avant la réussite du gate.

- [ ] **Step 2: Capturer et qualifier le runtime WebView2**

Consigner `CoreWebView2Environment.BrowserVersionString`, l’architecture et le mode Evergreen/Fixed. Le gate exige la même version majeure que la baseline de production ciblée. Si la production est Evergreen et sa version exacte ne peut être épinglée, exécuter au minimum sur la version cible recensée et sur la version installée la plus récente; une divergence fonctionnelle bloque le gate.

- [ ] **Step 3: Exécuter le scénario complet**

```powershell
dotnet run --project tools/QuickWindowIsolationPrototype.App --configuration Release -- --output artifacts/quick-window-isolation/builder-webview2.json
dotnet test tests/ScadaBuilderV2.Tests/ScadaBuilderV2.Tests.csproj --filter "FullyQualifiedName~QuickWindowIsolationPrototypeContractTests"
```

Expected: `PASS`; aucune fuite de style vers la page/chrome, aucune collision d’id ou de référence, un seul poller simulé et tous les compteurs à zéro après dispose.

- [ ] **Step 4: Commit harness Builder**

```powershell
git add tools/QuickWindowIsolationPrototype.App tests/ScadaBuilderV2.Tests/QuickWindowIsolationPrototypeContractTests.cs
git commit -m "test: validate quick window isolation in WebView2"
```

### Task 0.3: Exécuter la même fixture dans le host TF100Web ciblé

> **Authorization required before modifying `F:\Projet\Git\TF100Web`.** Cette tâche autorise uniquement des fixtures et tests de prototype; elle n’autorise ni code host de production ni déploiement.

**Files:**
- Create: `F:\Projet\Git\TF100Web\frontend\test_fixtures\quick_window_isolation\index.html`
- Create: `F:\Projet\Git\TF100Web\frontend\test_fixtures\quick_window_isolation\prototype.css`
- Create: `F:\Projet\Git\TF100Web\frontend\test_fixtures\quick_window_isolation\prototype.js`
- Create: `F:\Projet\Git\TF100Web\frontend\tests_runtime_js\quick-window-dom-css-isolation.test.mjs`
- Create: `F:\Projet\Git\TF100Web\frontend\tests_scada_quick_window_isolation_prototype.py`
- Avoid: `F:\Projet\Git\TF100Web\frontend\scada_package.py`, `frontend/scada_builder_composition.py`, `templates/**`, `static/**`

**Interfaces:**
- Consumes: une révision candidate de Task 0.1, composition de test TF100Web et cache/pont instrumentés.
- Produces: `artifacts/quick-window-isolation/tf100web.json` et preuve que le host ciblé respecte les mêmes assertions.

- [ ] **Step 1: Vendoriser une révision candidate**

Copier uniquement les artefacts de fixture nécessaires avec leur `PrototypeRevision`. Pendant l’exploration, le hash identifie la révision candidate mais ne la fige pas encore. Ne pas dupliquer une seconde implémentation sémantique du gestionnaire.

- [ ] **Step 2: Exécuter le scénario dans le contexte de composition TF100Web**

```powershell
Set-Location "F:\Projet\Git\TF100Web"
node --test frontend/tests_runtime_js/quick-window-dom-css-isolation.test.mjs
python -m pytest frontend/tests_scada_quick_window_isolation_prototype.py
```

Expected: `PASS` avec les mêmes sentinelles, 100 cycles sans croissance, un seul cache/poller/pont et aucun accès global hors racine.

- [ ] **Step 3: Commit prototype TF100Web**

```powershell
git add frontend/test_fixtures/quick_window_isolation frontend/tests_runtime_js/quick-window-dom-css-isolation.test.mjs frontend/tests_scada_quick_window_isolation_prototype.py
git commit -m "test: validate quick window isolation in TF100Web"
```

### Task 0.3b: Itérer puis geler la fixture et son schéma

**Files:**
- Modify as needed: `tools/prototypes/quick-window-dom-css-isolation/**`
- Modify as needed: `tests/runtime-js/quick-window-dom-css-isolation.test.mjs`
- Synchronize: `F:\Projet\Git\TF100Web\frontend\test_fixtures\quick_window_isolation/**`
- Synchronize: `F:\Projet\Git\TF100Web\frontend\tests_runtime_js\quick-window-dom-css-isolation.test.mjs`

**Interfaces:**
- Consumes: écarts observés par Tasks 0.2 et 0.3.
- Produces: une révision finale unique, hashée et repassée intégralement dans les deux hosts.

- [ ] Classer toute assertion nouvelle comme `core` si elle doit réussir dans les deux hosts, ou sous `hostExtensions.builderWebView2` / `hostExtensions.tf100Web` si elle décrit seulement l’enveloppe host. Une extension host ne peut affaiblir un invariant core.
- [ ] À chaque modification du schéma, de la fixture ou d’une assertion core: incrémenter `PrototypeRevision`, recalculer le manifest SHA-256, resynchroniser TF100Web et rejouer Tasks 0.1, 0.2 et 0.3 au complet. Aucun résultat d’une révision antérieure ne compte pour le gate.
- [ ] Geler le hash seulement lorsque les deux hosts réussissent la même révision candidate. Ajouter alors le test SHA-256 strict dans les deux dépôts et commit les artefacts synchronisés.

Expected: aucune dépendance circulaire implicite; la boucle est explicite, bornée par une révision gelée unique, et toutes les preuves du gate portent ce même hash.

### Task 0.4: Statuer sur le gate et figer les preuves

**Files:**
- Create: `docs/superpowers/reports/2026-08-10-quick-window-dom-css-isolation-prototype.md`
- Modify only on PASS: `docs/superpowers/plans/2026-08-10-parameterized-quick-window-management.md`
- Modify only on FAIL: `docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md`

**Interfaces:**
- Consumes: rapports JSON, captures, hashes, commits et résultats Builder/TF100Web.
- Produces: décision binaire `PASS` ou `FAIL`, traçable aux deux commits.

- [ ] **Step 1: Vérifier la matrice de sortie**

Exiger 100 % des assertions core de la révision gelée dans les deux hosts: ids/références isolés, styles isolés, états/tableaux/inputs/commandes exacts, focus/modalité exacts, cascade de fermeture, compteurs revenus à la baseline, un seul poller/cache/pont, races déterministes résolues, hydratation stale rejetée, SLA respecté et résultats fonctionnels équivalents.

- [ ] **Step 2: Appliquer le gate**

Si une assertion échoue après la boucle Task 0.3b, écrire `FAIL`, arrêter le plan et rouvrir `FR-020`; ne pas contourner par `ShadowRoot`, `iframe` généralisé ou réécriture runtime arbitraire sans nouvelle décision approuvée. Si toutes réussissent, écrire `PASS`, inscrire `PrototypeRevision`, hash gelé, versions WebView2, commits, métriques et commandes puis autoriser Phase 1.

- [ ] **Step 3: Valider et commit le rapport**

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
git add docs/superpowers/reports/2026-08-10-quick-window-dom-css-isolation-prototype.md docs/superpowers/plans/2026-08-10-parameterized-quick-window-management.md
git commit -m "docs: record quick window isolation gate"
```

Expected: le rapport porte l’en-tête documentaire requis, les commits des deux dépôts et une conclusion binaire. Phase 1 reste interdite tant que le commit `PASS` n’existe pas.

---

## Phase 1 - Contrats persistants et décommissionnement fail-closed

### Task 1.1: Ajouter le modèle de définition, contenu visuel et Interface locale

**Files:**
- Create: `src/ScadaBuilderV2.Domain/QuickWindows/QuickWindowModels.cs`
- Create: `src/ScadaBuilderV2.Domain/QuickWindows/QuickWindowValidation.cs`
- Modify: `src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs`
- Modify: `tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowDomainTests.cs`

**Interfaces:**
- Consumes: contenu visuel actuel de `ScadaScene`, types Element+, `CanvasSize`, styles et assets.
- Produces: `VisualContent`, `QuickWindowDefinition`, familles/membres typés, présentation centrée, `SinglePerDefinition` et collection projet ordonnée.

- [ ] Extraire la frontière `VisualContent` par composition sans réécrire le JSON des pages existantes; conserver un adaptateur de scène en mémoire.
- [ ] Représenter clés GUID stables, code validé, version d’interface, familles/type/accès, `Required=false`, constantes/variables privées et `PresentationDefaults` explicites: `Title` (défaut = `DisplayName`, surcharge optionnelle par invocation `FR-UI-03`), `Position` (`Center` par défaut `FR-UI-04`, seule valeur V1), `Backdrop` bool configurable `FR-017`, `Chrome` borné `FR-UI-11` (couleur barre de titre, bordure, ombre — `X`/géométrie/comportements/backdrop et garde-fous restent au thème/host), ainsi que `IsDraggable=true`/`IsResizable=false`/`IsViewportConstrained=true` (`FR-UI-05`/`FR-UI-06`). Toute autre propriété ou placement libre par invocation est hors contrat V1 et doit être rejetée. Tester que l’itération d’une définition sans `PresentationDefaults` conserve les défauts et que le host ajoute bien chrome hors `CanvasSize` (`FR-UI-02`).
- [ ] Tester égalité, invariants, renommage sans changement de clé, absence d’héritage page/fenêtre et absence d’un quatrième identifiant.
- [ ] Exécuter:

```powershell
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~QuickWindowDomainTests|FullyQualifiedName~OfficialSceneDomainTests"
```

Expected: anciens projets et scènes inchangés; modèle Fenêtre rapide valide et exhaustif.

- [ ] Commit: `feat: add quick window domain model`.

### Task 1.2: Ajouter invocations et liaisons typées

**Files:**
- Create: `src/ScadaBuilderV2.Domain/QuickWindows/QuickWindowInvocationModels.cs`
- Create: `src/ScadaBuilderV2.Domain/QuickWindows/QuickWindowBindingValidator.cs`
- Modify: `src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBinding.cs`
- Modify: `src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs`
- Create: `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowBindingTests.cs`

**Interfaces:**
- Consumes: définition/interface locale et catalogue de tags.
- Produces: `InvocationKey`, sources `Tag`, `Literal`, `Expression`, `ParentPort`, absence explicite, `OpenQuickWindow` et `CloseQuickWindow(Self)`.

- [ ] Ajouter `OpenQuickWindow` exigeant `QuickWindowInvocationKey` et `CloseQuickWindow` sans cible explicite; ne pas ajouter Toggle.
- [ ] Valider type, accès, écriture autorisée, version d’interface, required/optional et neutralité des ports non liés.
- [ ] Tester anti-injection `FR-010` invariant 10 pour les sources `Literal` et `Expression`: HTML/JS (`<script>`, `{{}}`, `${}`, `javascript:`), sélecteur CSS (`#`, `.`, `[data-`, `url(`), et chemin fichier (`../`, `C:\`, `//`, `\\`) doivent être rejetés par le validateur typé sans créer de souscription ni écriture; un littéral valide reste typé et échappé, une expression invalide reste diagnostic `Blocked` avec catégorie `injection-rejected`.
- [ ] Tester deux invocations créées indépendamment, diagnostics précis et interdiction de `CloseQuickWindow` hors définition.
- [ ] Commit: `feat: add typed quick window invocations`.

### Task 1.3: Retirer les anciens command kinds popup et isoler les résidus legacy

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBinding.cs`
- Inspect: `src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs`
- Modify: `src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs`
- Modify: `src/ScadaBuilderV2.Application/Pages/PageDependencyAnalyzer.cs`
- Modify: `src/ScadaBuilderV2.Domain/RuntimeContracts/ScadaRuntimeCapabilityCatalog.cs`
- Modify: `src/ScadaBuilderV2.Application/RuntimeContracts/ScadaRuntimeCapabilityAnalyzer.cs`
- Modify: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ModernProjectStore.cs`
- Modify: `src/ScadaBuilderV2.App/ElementCommandDialog.xaml.cs`
- Inspect: `src/ScadaBuilderV2.App/ElementEventDialog.xaml.cs`
- Modify: `tests/runtime-js/command-dispatcher.test.mjs`
- Modify: `tests/conformance/expected-runtime-capabilities.json`
- Inspect: `projects/**/*.json`
- Inspect/Modify current owner docs only: `docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md`, `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`, `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`, `docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md`, `docs/10_generated/RUNTIME_CAPABILITY_MATRIX_V2.md`
- Create: `tests/conformance/legacy-popup-residue-allowlist.json`
- Create: `docs/superpowers/reports/quick-window-legacy-popup-audit.md`
- Modify/Create tests: `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`, `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementCommandConfigTests.cs`, `tests/ScadaBuilderV2.Tests/RuntimeContracts/ScadaRuntimeCapabilityCatalogTests.cs`

**Interfaces:**
- Consumes: JSON ancien pouvant contenir actions/options ou command kinds popup.
- Produces: diagnostic projet/scène/élément/commande, échec sans écriture et modèle de commande actif sans anciens kinds/capabilities; les actions legacy restent isolées et sans migration.

- [ ] **Step 1: Produire un inventaire exhaustif avant retrait**

Scanner séparément code, tests, projets JSON, fixtures de conformance, runtime JS, documentation active, documentation archivée et TF100Web. Le rapport doit classer chaque match avec: dépôt, fichier/ligne, famille `modern-command` ou `legacy-action`, surface authoring/runtime/persistence/capability/doc, décision `remove`, `diagnostic-only`, `historical` ou `legacy-allowlisted`, test propriétaire et commit de résolution attendu.

```powershell
rg -n "OpenPopup|TogglePopup|ClosePopup|MountFragment|ScadaPopupOptions|popup\.options|command\.(open|close|toggle)-popup" src tests tools
Get-ChildItem projects -Recurse -File -Filter *.json | Select-String -Pattern 'OpenPopup|TogglePopup|ClosePopup|MountFragment|ScadaPopupOptions|popup.options'
rg -n "OpenPopup|TogglePopup|ClosePopup|MountFragment|ScadaPopupOptions|popup\.options|command\.(open|close|toggle)-popup" docs --glob '!09_archive/**'
rg -n "OpenPopup|TogglePopup|ClosePopup|MountFragment|ScadaPopupOptions|popup\.options|command\.(open|close|toggle)-popup" docs/09_archive
rg -n "OpenPopup|TogglePopup|ClosePopup|MountFragment|ScadaPopupOptions|popup\.options|command\.(open|close|toggle)-popup" "F:\Projet\Git\TF100Web\frontend"
```

- [ ] **Step 2: Retirer uniquement le contrat de commande moderne**

Retirer `ScadaCommandKind.OpenPopup`, `TogglePopup`, `ClosePopup`, leurs cases UI/runtime, probes et capacités. Charger tout JSON ancien dans une représentation diagnostique avant désérialisation enum stricte afin de nommer projet/scène/élément/commande, puis refuser sans sauvegarde ni migration.

- [ ] **Step 3: Borner mesurablement les résidus legacy**

`legacy-popup-residue-allowlist.json` doit énumérer chaque symbole/fichier legacy encore toléré (`ScadaActionKind.MountFragment`, actions `ClosePopup`/`TogglePopup`, `ScadaPopupOptions`) avec propriétaire, raison et test de non-migration. Un test échoue si un nouveau résidu apparaît, si un résidu allowlisté atteint `QuickWindowDefinition`/`InvocationKey`, ou si une capacité legacy sert de preuve QuickWindow. Aucun simple commentaire « à auditer » ne satisfait la tâche.

- [ ] **Step 4: Protéger les projets et fixtures**

Ajouter des copies fixtures contenant chaque ancien command kind et chaque action legacy. Comparer les bytes avant/après tentative d’ouverture/build. Les command kinds sont refusés avec diagnostic; les actions legacy suivent uniquement leur politique allowlistée et ne créent aucune Fenêtre rapide.

- [ ] **Step 5: Fermer l’audit**

L’audit est `PASS` seulement si: inventaire 100 % classé; zéro ancien command kind dans `src/`, UI active, runtime actif, capability catalog, fixture courante et JSON de projet authorable; toutes les occurrences legacy restantes sont dans l’allowlist; tests d’absence de migration verts; docs actives ne les décrivent plus comme contrat courant. Les archives peuvent conserver l’historique.

Expected: aucune surface active ni capacité de commande popup moderne; les éventuels résidus d’action legacy ne subsistent que dans leur chemin isolé, les fixtures de rejet ou l’historique documenté et ne sont jamais utilisés comme preuve Fenêtre rapide.

- [ ] Commit: `refactor: fail closed on retired popup contracts`.

### Task 1.4: Persister définitions et contenus sans migration destructive

**Files:**
- Modify: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ModernProjectStore.cs`
- Create: `src/ScadaBuilderV2.Infrastructure/ModernProjects/QuickWindowStore.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowStoreTests.cs`

**Interfaces:**
- Consumes: `ScadaProject.QuickWindows`, contenus visuels et invocations portées par les commandes.
- Produces: layout déterministe sous `quick-windows/`, round-trip atomique et anciens projets inchangés.

- [ ] Définir fichiers et ordre JSON déterministes, validation de chemins et écriture atomique cohérente avec scènes/projet.
- [ ] Tester création, save/reopen, renommage, rollback en cas d’échec, absence de réécriture d’un projet sans Fenêtre rapide et stabilité des clés.
- [ ] Commit: `feat: persist quick window definitions`.

### Task 1.5: Figer un handshake de contrat Builder -> TF100Web avant l’UI

> **Authorization required before modifying `F:\Projet\Git\TF100Web`.** Ce checkpoint touche uniquement fixtures et tests de contrat; aucun host de production n’est activé.

**Files:**
- Create: `tests/conformance/quick-window-contract-handshake/manifest.json`
- Create: `tests/conformance/quick-window-contract-handshake/expectations.json`
- Create: `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowContractHandshakeTests.cs`
- Create: `F:\Projet\Git\TF100Web\frontend\test_fixtures\quick_window_contract_handshake\manifest.json`
- Create: `F:\Projet\Git\TF100Web\frontend\test_fixtures\quick_window_contract_handshake\expectations.json`
- Create: `F:\Projet\Git\TF100Web\frontend\tests_scada_quick_window_contract_handshake.py`

**Interfaces:**
- Consumes: records Domain/persistance de Phase 1, identités et schéma 2.3 approuvé.
- Produces: première preuve cross-repository des noms, types, ordres, diagnostics et rejets avant authoring WPF ou runtime host.

- [ ] Générer la fixture depuis les records Builder, jamais à la main dans les deux dépôts; vendoriser par SHA-256 dans TF100Web.
- [ ] Faire parser/valider la fixture par TF100Web, y compris deux invocations, binding absent explicite, required invalide, ancien command kind et absence d’un quatrième identifiant.
- [ ] Muter indépendamment clés, types, ordre, `InvocationKey`, capability et manifest profile; les deux dépôts doivent accepter/rejeter les mêmes cas avec la même catégorie diagnostique.
- [ ] Bloquer Phase 2 si le handshake diverge. À chaque changement ultérieur du schéma, régénérer et repasser ce checkpoint avant le commit concerné.

```powershell
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~QuickWindowContractHandshakeTests"
Set-Location "F:\Projet\Git\TF100Web"
python -m pytest frontend/tests_scada_quick_window_contract_handshake.py
```

Expected: fixture et expectations portent le même SHA dans les deux dépôts; aucune interprétation divergente n’est reportée à la Phase 7.

- [ ] Commit Builder: `test: freeze quick window contract handshake`.
- [ ] Commit TF100Web: `test: consume quick window contract handshake`.

---

## Phase 2 - Orchestration Application, dépendances et historique

### Task 2.1: Créer les services de définition et d’invocation

**Files:**
- Create: `src/ScadaBuilderV2.Application/QuickWindows/QuickWindowDefinitionService.cs`
- Create: `src/ScadaBuilderV2.Application/QuickWindows/QuickWindowInvocationService.cs`
- Create: `src/ScadaBuilderV2.Application/QuickWindows/QuickWindowDependencyAnalyzer.cs`
- Create: `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowApplicationTests.cs`

**Interfaces:**
- Consumes: snapshots projet/scènes, validateur domaine et catalogue.
- Produces: create/update/delete/navigation-to-usage, diagnostics référentiels et blocage de suppression d’une définition référencée.

- [ ] Implémenter création vide, édition de métadonnées/interface, liste d’usages et suppression fail-closed.
- [ ] Interdire cycles et profondeur > 2 dans l’analyse générale, tout en gardant l’imbrication hors surface de production de la première tranche; couvrir `A->B->A` (cycle direct) et `Page->A->B->C` (profondeur 3) comme rejets déterministes dans `QuickWindowDependencyAnalyzer` avec diagnostic `cycle/depth-exceeded` (même si `Page->A->B` reste vert).
- [ ] Tester définitions manquantes, versions incompatibles, ports supprimés et diagnostics stables.
- [ ] Commit: `feat: orchestrate quick window definitions`.

### Task 2.2: Rendre les mutations atomiques et undoables

**Files:**
- Create: `src/ScadaBuilderV2.Application/History/QuickWindowWorkspaceSnapshotAction.cs`
- Modify: `src/ScadaBuilderV2.Application/History/EditorHistoryService.cs`
- Modify: `src/ScadaBuilderV2.Application/History/ProjectWorkspaceSnapshotAction.cs`
- Create: `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowHistoryTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs`

**Interfaces:**
- Consumes: mutation définition/invocation/élément appelant.
- Produces: une unité d’historique restaurant exactement élément, commande, invocation et liaisons.

- [ ] Couvrir suppression de l’appelant, changement de définition/liaison et restauration de sélection/dirty state.
- [ ] Tester undo/redo répété et indépendance de deux invocations créées séparément.
- [ ] Commit: `feat: add quick window undo history`.

### Task 2.3: Étendre le validateur build/export fail-closed

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs`
- Create: `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowBuildValidationTests.cs`

**Interfaces:**
- Consumes: projet, scènes, définitions, invocations, catalogue et profil manifest.
- Produces: diagnostics de la section 11 de la spec, sans empêcher la sauvegarde intermédiaire authoring.

- [ ] Couvrir required absent, mapping/type/accès, définition/contenu/version, cycles/profondeur (incluant `Page->A->B->C` profondeur 3 doit être rejeté en build/export avec diagnostic `cycle/depth-exceeded` même si `A->B` reste vert, et `A->B->A` cycle), profil 2.1/2.2, présentation interdite et capacité non supportée; rejouer l’injection `Literal`/`Expression` invalide côté build pour prouver rejet fail-closed sans souscription ni écriture.
- [ ] Séparer warnings d’authoring des erreurs build/export; ne jamais fabriquer de liaison par défaut.
- [ ] Commit: `feat: validate quick window builds`.

---

## Phase 3 - Authoring WPF et preview Builder

### Task 3.1: Ajouter le groupe projet Fenêtres rapides et le contexte d’éditeur

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml`
- Modify minimally (wiring only): `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Create: `src/ScadaBuilderV2.App/MainWindow.QuickWindows.cs`
- Create: `src/ScadaBuilderV2.App/QuickWindows/QuickWindowEditorContext.cs`
- Create: `src/ScadaBuilderV2.App/QuickWindows/QuickWindowWorkspaceController.cs`
- Create: `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowShellContractTests.cs`

**Interfaces:**
- Consumes: services Application et éditeur visuel existant.
- Produces: groupe distinct, création vide, onglet/badge/titre, sélection et politique de commandes masquées.

- [ ] Ajouter `Fenêtres rapides` sans les mélanger aux pages ou Element+; réutiliser le canvas via le contexte `VisualContent`.
- [ ] Masquer navigation, route, header/footer, import et autres commandes page-only; afficher le contexte actif sans ambiguïté.
- [ ] Garder dans `MainWindow.xaml.cs` seulement l’initialisation du controller et les délégations d’événements d’une ligne. Toute logique de sélection, création, refresh, enablement et navigation QuickWindow appartient au controller ou à `MainWindow.QuickWindows.cs`.
- [ ] Tester le contrat XAML/commandes et le basculement page/fenêtre. Ajouter un test d’architecture qui échoue si de nouvelles méthodes QuickWindow substantielles ou des types métier sont ajoutés à `MainWindow.xaml.cs`.
- [ ] Commit: `feat: add quick window authoring shell`.

### Task 3.2: Construire l’éditeur Interface locale

**Files:**
- Create: `src/ScadaBuilderV2.App/QuickWindows/QuickWindowInterfacePanel.xaml`
- Create: `src/ScadaBuilderV2.App/QuickWindows/QuickWindowInterfacePanel.xaml.cs`
- Create: `src/ScadaBuilderV2.App/QuickWindows/QuickWindowInterfaceMemberDialog.xaml`
- Create: `src/ScadaBuilderV2.App/QuickWindows/QuickWindowInterfaceMemberDialog.xaml.cs`
- Create: `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceAuthoringTests.cs`

**Interfaces:**
- Consumes: membres typés et analyse d’usages.
- Produces: tableau groupé public/privé, filtres, édition inline/avancée, compteurs et navigation vers usages.

- [ ] Remplacer `Catalogue Tags` par `Interface locale` uniquement dans ce contexte; restreindre les sélecteurs état/commande/binding/expression aux membres locaux.
- [ ] Afficher optional non lié gris et required non lié rouge; confirmer toute suppression référencée.
- [ ] Commit: `feat: author quick window local interfaces`.

### Task 3.3: Ajouter Open/Close et l’onglet conditionnel Liaisons

**Files:**
- Modify: `src/ScadaBuilderV2.App/ElementPropertiesDialog.xaml`
- Modify: `src/ScadaBuilderV2.App/ElementPropertiesDialog.xaml.cs`
- Create: `src/ScadaBuilderV2.App/QuickWindows/QuickWindowBindingsEditor.xaml`
- Create: `src/ScadaBuilderV2.App/QuickWindows/QuickWindowBindingsEditor.xaml.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.QuickWindows.cs`
- Create: `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowBindingAuthoringTests.cs`

**Interfaces:**
- Consumes: définitions, interface publique, catalogue et historique.
- Produces: choix cible, `InvocationKey`, grille typée Tag/Littéral/Expression/Port parent et diagnostic final.

- [ ] N’afficher `Liaisons` que pour `OpenQuickWindow`; ne proposer ni Toggle ni cible page.
- [ ] Dans un contenu Fenêtre rapide, proposer `CloseQuickWindow(Self)` sans cible libre.
- [ ] Vérifier persistance, undo/redo, suppression appelant et absence d’état partagé.
- [ ] Commit: `feat: author quick window bindings`.

### Task 3.4: Ajouter le preview et le banc d’essai editor-only

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/PreviewDocument.cs`
- Modify: `src/ScadaBuilderV2.Rendering/NativePageDocumentFactory.cs`
- Create: `src/ScadaBuilderV2.Rendering/QuickWindows/QuickWindowPreviewDocumentFactory.cs`
- Create: `src/ScadaBuilderV2.App/QuickWindows/QuickWindowTestBench.xaml`
- Create: `src/ScadaBuilderV2.App/QuickWindows/QuickWindowTestBench.xaml.cs`
- Create: `src/ScadaBuilderV2.App/QuickWindows/BuilderQuickWindowHostAdapter.cs`
- Create: `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPreviewTests.cs`

**Interfaces:**
- Consumes: définition compilée, mappings/valeurs temporaires et gestionnaire partagé issu du prototype validé.
- Produces: preview avec cadre minimal, backdrop, `X`, `Escape`, `Self`, remplacement M101/M102 et diagnostics.

- [ ] Adapter le prototype validé au code de production sans copier une seconde sémantique; connecter le runtime/cache/pont existants.
- [ ] Vérifier même invocation => focus, autre invocation => close/dispose/recreate, aucune fuite de mapping et aucune exportation des données de test.
- [ ] Commit: `feat: preview quick window instances`.

---

## Phase 4 - Package 2.3, runtime partagé et conformance encore bloquée

### Task 4.1: Ajouter les capacités Blocked et l’analyse exhaustive

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/RuntimeContracts/ScadaRuntimeCapabilityCatalog.cs`
- Modify: `src/ScadaBuilderV2.Application/RuntimeContracts/ScadaRuntimeCapabilityAnalyzer.cs`
- Modify: `tests/ScadaBuilderV2.Tests/RuntimeContracts/ScadaRuntimeCapabilityCatalogTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/RuntimeContracts/ScadaRuntimeCapabilityAnalyzerTests.cs`
- Modify: `tests/conformance/expected-runtime-capabilities.json`

**Interfaces:**
- Consumes: variantes persistantes de définition/invocation/commande.
- Produces: capacités granulaires exactes de §10.2, toutes `Blocked`, sans identifiant parapluie.

- [ ] Ajouter les capacités incluses dans la verticale seulement; conserver nesting/parent-port/legacy-adapter bloqués et non requis si hors tranche.
- [ ] Ajouter tests de réflexion/exhaustivité et mutation indépendante.
- [ ] Commit: `feat: register blocked quick window capabilities`.

### Task 4.2: Compiler définitions et invocations déterministes

**Files:**
- Create: `src/ScadaBuilderV2.Rendering/QuickWindows/Ft100QuickWindowManifestModels.cs`
- Create: `src/ScadaBuilderV2.Rendering/QuickWindows/QuickWindowCompiler.cs`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100PackageValidation.cs`
- Create: `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowExporterTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100PackageValidatorTests.cs`

**Interfaces:**
- Consumes: modèle validé et namespaces prouvés en phase 0.
- Produces: `QuickWindows[]`, `QuickWindowInvocations[]`, contenu/CSS namespacés et référence de commande par `InvocationKey`.

- [ ] Ordonner ordinalement définitions, membres et liaisons; écrire sous un chemin stable dérivé de `QuickWindowDefinitionKey`.
- [ ] Namespace ids, références internes, CSS et cibles sans réécriture à l’ouverture; exclure données de test et editor-only.
- [ ] **Implémenter un gate structurel sans bypass produit:** `Ft100SceneExporter.ExportProjectAsync` et `ExportProjectArchiveAsync` doivent appeler `ScadaRuntimeCapabilityAnalyzer.Analyze` puis `EnsureRuntimeCapabilitiesExportable` avant de créer le staging directory ou d’écrire le moindre fichier. Tant qu’une capacité QuickWindow est `Blocked`, l’appel public lève une erreur déterministe et laisse zéro artefact.
- [ ] Tester `QuickWindowCompiler` directement comme classe `internal` via `InternalsVisibleTo` pour produire la fixture non livrable. Ne créer aucun paramètre `allowBlocked`, variable d’environnement, profil caché, option CLI ou branche conditionnelle permettant de contourner le gate depuis le produit.
- [ ] Ajouter un test d’ordre d’appel et un test filesystem prouvant qu’un refactoring ne peut compiler/archiver avant la validation; muter chaque capacité QuickWindow à `Blocked` et vérifier le même rejet.
- [ ] Tester package byte-déterministe, rejets 2.1/2.2 et absence de Fragment de substitution.
- [ ] Commit: `feat: compile quick window package contracts`.

### Task 4.3: Étendre le runtime partagé et la fixture de conformance

**Files:**
- Create: `src/ScadaBuilderV2.Rendering/Runtime/quick-window-runtime.js`
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/scada-runtime.js`
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/command-dispatcher.js`
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/tag-bridge.js`
- Create: `tests/runtime-js/quick-window-runtime.test.mjs`
- Modify: `tests/ScadaBuilderV2.Tests/RuntimeContracts/ScadaV2RuntimeConformanceProjectFactory.cs`
- Modify: `tests/ScadaBuilderV2.Tests/RuntimeContracts/RuntimeConformancePackageTests.cs`
- Modify: `tests/conformance/expected-runtime-capabilities.json`

**Interfaces:**
- Consumes: registres manifest, `InvocationKey`, contexte résolu par host.
- Produces: sémantique portable open/close, résolution locale, indisponibilité de ports et hooks de cycle host-owned.

- [ ] Ne pas créer l’overlay/chrome dans le runtime partagé; déléguer open/close/focus/montage au host adapter versionné.
- [ ] Implémenter ports optionnels/required validés, états/commandes/expressions avec scope instance et cleanup idempotent; rejeter `Literal`/`Expression` d’injection (`<script>`,`../`,`#...`) avant souscription et prouver qu’aucune écriture n’est émise.
- [ ] Ajouter probes exacts, deux invocations M101/M102, stale hydration, cross-write rejection, profondeur 3 / cycle rejetés, injection rejetée et hash partagé; garder les statuts `Blocked`.
- [ ] Commit: `feat: add shared quick window runtime`.

### Task 4.4: Exécuter le package compilé dans TF100Web avant tout host complet

> **Authorization required before modifying `F:\Projet\Git\TF100Web`.** Ce checkpoint est test-only et précède le développement complet du host.

**Files:**
- Generate: `tests/conformance/quick-window-runtime-handshake.sb2`
- Generate: `tests/conformance/quick-window-runtime-handshake.sha256`
- Create: `F:\Projet\Git\TF100Web\frontend\test_fixtures\quick-window-runtime-handshake.sb2`
- Create: `F:\Projet\Git\TF100Web\frontend\test_fixtures\quick-window-runtime-handshake.sha256`
- Create: `F:\Projet\Git\TF100Web\frontend\tests_runtime_js\quick-window-runtime-handshake.test.mjs`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_quick_window_contract_handshake.py`

**Interfaces:**
- Consumes: sortie directe `QuickWindowCompiler`, runtime partagé et parser TF100Web.
- Produces: preuve réelle Builder -> package -> intake/runtime TF100Web avant l’adaptateur host et avant toute promotion.

- [ ] Générer le package uniquement par le harness test interne protégé de Task 4.2; le hash doit être identique dans les deux dépôts.
- [ ] Faire ingérer le package par TF100Web, monter une racine host simulée et exécuter ouverture M101, remplacement M102, close Self, required absent, stale snapshot et cross-write rejection.
- [ ] Rejouer ce checkpoint à chaque changement de manifest, compiler, runtime ou parser. Toute divergence bloque Phase 5; elle n’est pas reportée à la verticale `win00054`.

Expected: le premier round-trip package réel est vert avant le code host complet; toutes les capacités restent `Blocked` et aucun export produit n’est possible.

- [ ] Commit Builder: `test: generate quick window runtime handshake`.
- [ ] Commit TF100Web: `test: execute builder quick window handshake`.

---

## Phase 5 - Host TF100Web et déploiement capable

> **Authorization required before modifying or deploying `F:\Projet\Git\TF100Web`.** Les commits de code peuvent être préparés localement; le déploiement exige une autorisation distincte et une fenêtre appropriée.

### Task 5.1: Valider et ingérer les registres 2.3

**Files:**
- Modify: `F:\Projet\Git\TF100Web\frontend\scada_package.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\scada_projects.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_deploy.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\test_fixtures\expected-runtime-capabilities.json`

**Interfaces:**
- Consumes: manifest 2.3, capacités, hash et registres déterministes.
- Produces: validation fail-closed avant activation, stockage/déploiement atomique et refus des profils incompatibles.

- [ ] Refuser capacités/version/hash inconnus, liens/path traversal, doublons de clés, références absentes et profils 2.1/2.2.
- [ ] Vendoriser fixture/hash exacts et tester rollback conservant le package actif.
- [ ] Commit: `feat: validate quick window packages`.

### Task 5.2: Implémenter l’adaptateur host et le gestionnaire SinglePerDefinition

**Files:**
- Modify: `F:\Projet\Git\TF100Web\templates\frontend\scada_builder.html`
- Modify: `F:\Projet\Git\TF100Web\static\asset\css\templates\frontend\scada_builder.css`
- Create: `F:\Projet\Git\TF100Web\static\asset\js\quick-window-host.js`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_runtime_js\host-adapter.test.mjs`
- Create: `F:\Projet\Git\TF100Web\frontend\tests_runtime_js\quick-window-host.test.mjs`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_page_composition.py`

**Interfaces:**
- Consumes: runtime partagé, contenu compilé, cache/hydratation/navigation existants.
- Produces: cadre host-owned, backdrop, focus, viewport, instance unique, dispose et invalidation navigation/déploiement/session.

- [ ] Porter exactement la stratégie validée en phase 0; les queries restent root-scoped et le cache/poller/pont existants restent uniques.
- [ ] Implémenter `X`, `Escape`, `Self`, même invocation => front, invocation différente => close/recreate, scrolling interne et fermeture sur navigation.
- [ ] Tester 100 cycles, fuite nulle, hydratation générationnelle, permissions d’écriture et absence de cross-mapping.
- [ ] Rejouer les races de Phase 0 contre le vrai host: double-clic/open concurrent, navigation pendant montage, close pendant hydration, open pendant dispose, invalidation de session/déploiement et callbacks tardifs. Vérifier une génération active maximum, dispose idempotent et aucun changement DOM/historique après invalidation.
- [ ] Commit: `feat: host quick windows in TF100Web`.

### Task 5.3: Exécuter la conformance cross-runtime et déployer TF100Web

**Files:**
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_runtime_conformance.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_runtime_js\runtime-conformance-harness.mjs`
- Create: `F:\Projet\Git\TF100Web\deploy\developpement\quick_window_canary_runbook.md`
- Update generated fixture/hash files in both repos only through their generator.

**Interfaces:**
- Consumes: même `.sb2`, index d’attentes et runtime SHA-256.
- Produces: preuve exécutable de chaque capacité incluse, canary isolé, rollback éprouvé et version déployée vérifiée.

- [ ] Exécuter suites Builder/Node/TF100Web, mutation d’une capacité à la fois, preview/host equivalence et package déterministe.
- [ ] **Canary/staging obligatoire:** déployer d’abord le package dans une instance TF100Web non industrielle utilisant un `STATIC_ROOT` distinct. Réutiliser `deploy_package_to_static(package_dir, canary_static_root)` et une configuration de station de test; ne pas remplacer `STATIC_ROOT/scada` actif. Vérifier commit, génération, registre de capacités et SHA effectivement servis.
- [ ] Exécuter sur le canary la conformance complète, les races, 100 cycles, les SLA p95 et un soak d’au moins 24 h sans erreur QuickWindow, croissance mémoire ni impact sur les pages 2.1/2.2/2.3 existantes.
- [ ] Éprouver le rollback avant production: conserver l’archive `.sb2`, le SHA et la génération known-good; redéployer ce package dans le canary, vérifier retour des pages/runtime/hash et documenter le temps de restauration. Le rollback production est un redéploiement atomique du package known-good, jamais une édition manuelle de `STATIC_ROOT`.
- [ ] Après autorisation distincte, déployer TF100Web en production. Effectuer un smoke read-only, surveiller erreurs et métriques, puis conserver la possibilité de redéployer immédiatement le package known-good.
- [ ] Ne promouvoir aucune capacité et ne passer à Phase 6 qu’après canary/soak/rollback verts et preuve du déploiement production capable. Si production échoue, redéployer known-good et garder toutes les capacités Builder `Blocked`.
- [ ] Commit: `test: prove quick window host conformance`.

---

## Phase 6 - Promotion des capacités et activation contrôlée de l’export Builder

### Task 6.1: Promouvoir uniquement les capacités prouvées

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/RuntimeContracts/ScadaRuntimeCapabilityCatalog.cs`
- Modify: `tests/conformance/expected-runtime-capabilities.json`
- Modify: `docs/10_generated/RUNTIME_CAPABILITY_MATRIX_V2.md` via générateur
- Modify at promotion boundary: `VERSION` and owner-document version metadata
- Modify matching TF100Web expected capability fixture.

**Interfaces:**
- Consumes: commits/preuves Builder, runtime, TF100Web et SHA déployé.
- Produces: `Supported` seulement pour les capacités de la verticale prouvées aux trois couches.

- [ ] Laisser `quick-window.binding.parent-port`, `quick-window.nesting.depth-2` et `quick-window.legacy-fragment-adapter` bloqués/hors export tant qu’ils ne sont pas livrés.
- [ ] Exécuter `tools/RuntimeCapabilityMatrixGenerator` et le gate stale.
- [ ] Puisque cette promotion introduit la première capacité QuickWindow livrable, calculer un bump `feature` depuis la valeur courante de `VERSION` avec `python C:\Users\mathi\.codex\skills\scada-builder-v2-versioning\scripts\bump_scada_v2_version.py <current> feature`, remettre l’itération à `0000` et synchroniser seulement les documents propriétaires touchés. Ne pas effectuer ce bump si une capacité, le canary, le déploiement ou l’export reste bloqué.
- [ ] Commit: `feat: promote proven quick window capabilities`.

### Task 6.2: Activer l’export strict 2.3 Fenêtre rapide

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100PackageValidation.cs`
- Modify: `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowExporterTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/RuntimeContracts/RuntimeConformancePackageTests.cs`

**Interfaces:**
- Consumes: capacités Supported et profil de déploiement capable.
- Produces: export Builder activé uniquement pour le contrat prouvé.

- [ ] Retirer le gate temporaire uniquement pour les variantes Supported; maintenir le rejet de toute variante Blocked ou host incompatible.
- [ ] Tester export/rejet, hash, déterminisme, aucune donnée test/editor-only et aucune géométrie de chrome host dans le contenu.
- [ ] Commit: `feat: enable strict quick window export`.

---

## Phase 7 - Verticale win00054, acceptation et documentation

### Task 7.1: Construire la référence moteur à deux invocations

**Files:**
- Inspect: `projects/AMR_REF_SCADA_V2/scenes/win00054.scene.json`
- Inspect: `projects/AMR_REF_SCADA_V2/.studio/imports/studio_win00054_20260804_145141.ft1`
- Inspect: `projects/AMR_REF_SCADA_V2/project.json` (`TagCatalog`)
- Create: `tests/conformance/industrial/win00054-quick-window-mapping-audit.json`
- Modify: `projects/AMR_REF_SCADA_V2/project.json`
- Create: `projects/AMR_REF_SCADA_V2/quick-windows/<generated-definition-key>.quick-window.json`
- Modify the selected caller scene under: `projects/AMR_REF_SCADA_V2/scenes/`
- Create: `tests/ScadaBuilderV2.Tests/QuickWindows/Win00054QuickWindowIntegrationTests.cs`

**Interfaces:**
- Consumes: inventaire visuel/fonctionnel `win00054` comme référence de conception et mappings existants confirmés séparément dans le `TagCatalog` du projet.
- Produces: une définition quatre boutons/quatre états/close, interface minimale et deux invocations moteur indépendantes.

- [ ] **Auditer avant de créer:** lire la scène/import `win00054` uniquement pour dresser l’inventaire opérateur des quatre boutons/quatre états et lire `project.json.TagCatalog` pour sélectionner deux jeux de mappings. Pour chaque mapping, consigner id, nom, datatype, accès, enabled, source de confirmation et rôle M101/M102 dans `win00054-quick-window-mapping-audit.json`. Si deux jeux complets et compatibles ne sont pas présents, bloquer la tâche; ne rien inventer et ne pas modifier le catalogue silencieusement.
- [ ] Créer la `QuickWindowDefinition` sur un canvas vide et redessiner explicitement son contenu Element+ selon l’inventaire. « Canvas vide » signifie aucune conversion/copie de géométrie, logique, événement ou mapping depuis la scène/Fragment; cela n’interdit pas de référencer ensuite des tags déjà validés du catalogue projet.
- [ ] Lier lecture booléenne, commande écriture, `MotorName` et `Precision` aux ids approuvés par l’audit; les deux invocations sont nouvelles et indépendantes même si leurs sources existent déjà dans le catalogue.
- [ ] Tester save/reopen, required/optional, focus/recreate, cross-read/write, undo/redo et preview/export.
- [ ] Commit: `test: add win00054 quick window vertical`.

### Task 7.2: Exécuter l’acceptation complète sans écriture PLC non autorisée

**Files:**
- Modify: `tests/conformance/industrial/amr-ref-industrial-acceptance.json`
- Modify matching TF100Web fixture/evidence files through the canonical sync flow.

**Interfaces:**
- Consumes: package final et TF100Web déployé.
- Produces: preuve de parité Builder/TF100Web et statut clair des smokes industriels.

- [ ] Exécuter full suites, fixture conformance, races concurrentes, 100 cycles, navigation/reopen et read-only smoke.
- [ ] Mesurer 30 ouvertures froides et 100 chaudes après warmup sur la même machine et même Node LTS (20.18.x) que Phase 0. Exiger p95 chaud ≤ 500 ms, p95 froid ≤ 1 500 ms et régression ≤ 10 % contre la baseline gelée; vérifier aussi absence de croissance mémoire après les 100 cycles.
- [ ] N’exécuter une écriture/readback PLC qu’après autorisation explicite; sinon consigner le gate restant sans présenter la livraison comme validée en production.
- [ ] Commit: `test: validate win00054 quick window vertical`.

### Task 7.3: Synchroniser contrats, décisions et couverture

**Files:**
- Modify: `docs/00_governance/DECISION_REGISTER_V2.md`
- Modify: `docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md`
- Modify: `docs/03_runtime_contracts/PREVIEW_BUILD_EXPORT_CONTRACT_V2.md`
- Modify: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`
- Modify: `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`
- Modify: `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md`
- Modify: `docs/08_implementation_status/KNOWN_GAPS_V2.md`
- Modify: `docs/README.md`
- Modify: `VERSION`

**Interfaces:**
- Consumes: code/tests/commits et statut réel du déploiement.
- Produces: documentation propriétaire synchronisée, diagrammes à jour et version itérative finale.

- [ ] Documenter uniquement les capacités réellement Supported; garder nesting/legacy adapter/copie/présentation avancée comme gaps ou tranches futures.
- [ ] Mettre à jour diagrammes modèle, build/export et cycle host; remplacer les `PENDING` appropriés par des commits existants sans auto-référence impossible.
- [ ] Appliquer la politique de version du plan: si Phase 6 a réellement activé la première capacité QuickWindow livrable, le bump feature a déjà été effectué à cette frontière; Task 7.3 calcule seulement le prochain bump iteration avec `python C:\Users\mathi\.codex\skills\scada-builder-v2-versioning\scripts\bump_scada_v2_version.py <current> iteration`. Si Phase 6 n’a pas été franchie, rester en bumps iteration et ne jamais annoncer la feature comme livrée. Synchroniser ensuite tous les documents touchés.
- [ ] Exécuter:

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
rg -n "index\.html|08_web_modernized|source_html" docs --glob '!09_archive/**'
rg -n "Open[ ]Decisions|Open Technical Questions" docs --glob '!09_archive/**'
rg -n "OpenPopup|TogglePopup|ClosePopup|MountFragment|ScadaPopupOptions|command\.(open|close|toggle)-popup|popup\.options" docs/03_runtime_contracts docs/04_editor docs/08_implementation_status docs/10_generated
rg -n "PENDING" docs/README.md docs/00_governance docs/03_runtime_contracts docs/04_editor docs/08_implementation_status docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md docs/superpowers/plans/2026-08-10-parameterized-quick-window-management.md
dotnet test ScadaBuilderV2.sln --no-restore
```

Expected: `verify-docs` valide séparément les métadonnées `Document version`/`Historique des changements`; chaque commande `rg` possède un signal unique. Les matches legacy actifs sont expliqués/allowlistés, les `PENDING` sont examinés comme dette de commit et la documentation reste cohérente avec les gates réels.

- [ ] Commit: `docs: record quick window v1 implementation`.

---

## Annexe A — Matrice de couverture FR → Tasks (preuve de complétude)

Chaque invariant approuvé de la spec `docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md:FR-001..029 + FR-UI-01..22` est tracé vers au moins une tâche testée. Une case vide = gap bloquant à combler avant `PASS` Phase 0 ou promotion.

| FR | Intitulé court | Tasks porteuses (production) | Type de preuve |
| --- | --- | --- | --- |
| `FR-001` | Entité distincte page | `1.1` modèle + `3.1` shell | Domain equality + XAML contract |
| `FR-002` | Workflow clic → X | `3.4` preview + `5.2` host + `7.1/7.2` verticale | Focus/modal + smoke read-only |
| `FR-003` | Open = commande `OnClick` | `1.2` invocations + `3.3` Liaisons | Domain `OpenQuickWindow` + dialog test |
| `FR-004` | Interface locale remplace Catalogue | `3.2` InterfacePanel | WPF panel + sélecteurs restreints |
| `FR-005` | 5 familles typées | `1.1` + `3.2` | Validation familles/type/accès |
| `FR-006` | Constante vs paramètre `MotorName` | `1.1`/`1.2` | Constante non liée, param public lié |
| `FR-007` | Onglet Liaisons conditionnel | `3.3` BindingsEditor | Grille `Tag/Littéral/Expression/ParentPort` |
| `FR-008` | Définition vs invocation | `1.1`/`1.2` | `DefinitionKey` vs `InvocationKey` |
| `FR-009` | Port sans liaison = absence explicite | `1.2` + `2.3` | `null/absent` sans souscription |
| `FR-010` | Indisponible neutre + anti-injection | `1.2` + `2.3` + `3.2` + `4.3` + `5.2` | Gris/rouge + valeur `—`, rejet `<script>/../` |
| `FR-011` | Nesting `A->B` | `0.1-0.3` (`Page->A->B`) + `5.2` enfant centrée `FR-UI-09` | Prototype 100 cycles + host |
| `FR-012` | Cycles interdits, profondeur ≤2 | `2.1` analyzer + `2.3`/`4.2` validation `Page->A->B->C` rejet | `cycle/depth-exceeded` fail-closed |
| `FR-013` | `SinglePerDefinition` | `0.1` races + `3.4` + `5.2` | Même invocation front / autre recreate |
| `FR-014` | Delete caller atomique + undo | `2.2` history + `3.3` | `QuickWindowWorkspaceSnapshotAction` |
| `FR-015` | Compile 1× définition + N descriptors | `4.2` compiler + `4.3` runtime | `QuickWindowCompiler` déterministe |
| `FR-016` | Runtime/cache/poller/pont uniques | `0.1` un seul poller + `4.3` + `5.2` | Compteurs → baseline |
| `FR-017` | Backdrop configurable | `1.1` `PresentationDefaults.Backdrop` + `3.4`/`5.2` | Host backdrop bool |
| `FR-018` | `Required=false` par défaut, rouge/bloquant | `1.1` + `1.2` + `2.3` + `3.2` | Rouge + build bloqué |
| `FR-019` | `VisualContent` par composition, pas héritage | `1.1` | Adaptateur scène, pas `PageDefinition:QuickWindowDefinition` |
| `FR-020` | Racine DOM scoppée + namespace stable | Phase 0 gate `0.1-0.4` | Sentinelles ids/CSS isolés |
| `FR-021` | Manifest 2.3 `Blocked` → `Supported` + 2.1/2.2 refus | `1.5` handshake + `4.1/4.2` gate + `5.1` intake + `6.1/6.2` promotion | SHA + capabilities triées |
| `FR-022` | Pas de preset/contexte équipement, copie autonome | `1.2` descriptor explicite + validation absence preset | Test absence modèle `BindingPreset` |
| `FR-023` | Pas de Toggle, `Close Self` only | `1.2` + `3.3` + `5.2` | `CloseQuickWindow(Self)` only |
| `FR-024` | `ScadaActionDefinition` phased-out | `1.3` audit allowlist | `legacy-popup-residue-allowlist.json` |
| `FR-025` | Retrait `Open/ Toggle/ClosePopup` kinds | `1.3` | Diagnostic sans sauvegarde/migration |
| `FR-026` | Plan après spec, Phase 0 bloquante | Phase 0 gate `0.4 PASS` avant Phase 1 | Rapport `PASS/FAIL` |
| `FR-027` | 3 identités, pas de 4e | `1.1`/`1.2` + `4.2` + Global Constraints | Absence `InstanceKey` |
| `FR-028` | Verticale `win00054` 2 invocations | `7.1`/`7.2` | M101/M102 cross-leak tests |
| `FR-029` | `DEC-0050` supersède `DEC-0019/020/022` | `1.3` + `7.3` docs | Registre + archives |

| `FR-UI-01` | Chrome host + X | `0.1` + `3.4` + `5.2` | Barre titre hors canvas |
| `FR-UI-02` | `CanvasSize` = contenu seul | `1.1` `PresentationDefaults` + `5.2` | Host ajoute chrome hors dimensions |
| `FR-UI-03` | Titre `DisplayName` + surcharge invocation | `1.1` + `5.2` | `Title` défaut + override |
| `FR-UI-04` | Position `Center` défaut définition | `1.1` + `5.2` | Seule valeur V1 |
| `FR-UI-05` | Déplaçable, non redimensionnable | `0.1` + `1.1` + `5.2` | `IsDraggable/IsResizable` |
| `FR-UI-06` | Confinée viewport + scroll interne | `0.1` + `5.2` | Viewport + `overflow` |
| `FR-UI-07` | Backdrop bloque, click ne ferme pas, opacité thème | `1.1` + `3.4` + `5.2` | Thème global |
| `FR-UI-08` | `Escape` = `X` sécurisé | `0.1` + `3.4` + `5.2` | Même chemin `close` |
| `FR-UI-09` | Enfant centrée, partage backdrop | `0.1` + `5.2` (depth 2) | Pas de 2e backdrop |
| `FR-UI-10` | Même invocation front / autre recreate | `0.1` races + `3.4` + `5.2` | `SinglePerDefinition` |
| `FR-UI-11` | Chrome personnalisation bornée | `1.1` `Chrome` (couleur/barre/bordure/ombre) | Thème verrouille `X`/comportements |
| `FR-UI-12` | Groupe projet `Fenêtres rapides` | `3.1` | `MainWindow.xaml` group distinct |
| `FR-UI-13` | Nouvelle = canvas vide | `3.1` | Pas de conversion Fragment V1 |
| `FR-UI-14` | Réutilise éditeur + badge + commandes masquées | `3.1` | Policy `Page-only` masquées |
| `FR-UI-15` | Tableau `Interface publique/privée` + filtres | `3.2` | Panel groupé |
| `FR-UI-16` | Édition inline + dialogue avancé | `3.2` | `QuickWindowInterfaceMemberDialog` |
| `FR-UI-17` | Compteurs usages + nav | `3.2` | Navigation vers usages |
| `FR-UI-18` | Grille Liaisons (nom/famille/type/source/valeur/statut) | `3.3` | `QuickWindowBindingsEditor` |
| `FR-UI-19` | Source `Tag/Littéral/Expression/ParentPort` typée | `1.2` + `2.3` + `3.3` | Sélecteur contextuel validé |
| `FR-UI-20` | Optional gris `Non lié` / Required rouge + preview indisponible | `3.2` + `3.4` | Statut + valeur `—` |
| `FR-UI-21` | Banc d’essai editor-only non exporté | `3.4` | `QuickWindowTestBench` + test absence `.sb2` |
| `FR-UI-22` | Suppression référencée bloquée + nav | `2.1` + `3.2` | Dialogue usages + confirmation |

> Gap check: si une FR n’a pas de ligne verte dans cette annexe à la fin d’une phase, la phase est incomplète. Le `verify-docs` de `Task 7.3` doit rejouer `rg` sur `FR-0` pour détecter toute FR orpheline.

---

## Validation Checklist

- [ ] La phase 0 porte un `PASS` documenté dans WebView2 et TF100Web avant le premier commit de production.
- [ ] Toutes les preuves Phase 0 portent la même `PrototypeRevision` et le même hash gelé après la boucle d’itération; versions SDK/runtime WebView2 consignées.
- [ ] Aucun `InstanceKey`, héritage page/fenêtre, second poller/cache/dispatcher ou repli Fragment n’existe.
- [ ] Définition, Interface locale, présentation et deux invocations survivent à save/reopen avec clés stables.
- [ ] Optional non lié est indisponible sans fausse valeur; required non lié bloque build/export.
- [ ] Même invocation => front sans recréation; autre invocation même définition => close/dispose/recreate.
- [ ] `X`, `Escape` et `CloseQuickWindow(Self)` empruntent le même chemin sécurisé.
- [ ] Aucune lecture, qualité, souscription ou écriture M101 ne fuit vers M102, et inversement.
- [ ] Suppression/undo/redo de l’appelant restaure exactement commande et liaisons.
- [ ] Manifest 2.3 et RuntimeContract 1.0 déclarent registres, capacités et SHA exacts dans un ordre déterministe.
- [ ] Profils 2.1/2.2 et hosts incapables refusent sans mutation ni Fragment de substitution.
- [ ] Les anciens command kinds popup ne subsistent que dans fixtures de rejet ou historique; les éventuels résidus d’action/options legacy sont isolés, non authorables et sans migration vers Fenêtre rapide.
- [ ] L’audit popup inventorie 100 % des matches code/tests/projects/docs/TF100Web; chaque résidu legacy est allowlisté avec propriétaire et test.
- [ ] Aucune donnée de banc d’essai, overlay, chrome host ou état editor-only ne fuit dans `.sb2`.
- [ ] L’export public échoue avant toute écriture lorsque les capacités sont `Blocked`; aucun bypass `allowBlocked`/env/CLI n’existe.
- [ ] Les handshakes contrat et package Builder -> TF100Web réussissent avant l’authoring final et avant le host complet.
- [ ] Les races double-open/navigation/mount/close/hydrate/dispose ne laissent qu’une génération active et aucun callback stale.
- [ ] `MainWindow.xaml.cs` ne contient que le wiring minimal QuickWindow; la logique réside dans les fichiers/controllers dédiés.
- [ ] L’audit mapping `win00054` distingue clairement référence visuelle et catalogue de tags; aucun mapping n’est inventé ou copié implicitement.
- [ ] Les SLA p95 chaud/froid et la non-régression ≤ 10 % sont respectés sur la machine de référence.
- [ ] TF100Web capable est déployé et vérifié avant promotion des capacités et activation export Builder.
- [ ] Canary TF100Web isolé, soak 24 h et redéploiement du package known-good réussissent avant production.
- [ ] Chaque phase possède un checkpoint vert et une frontière de revert; aucune branche partielle n’est fusionnée/déployée.
- [ ] Bumps `iteration` avant activation, bump `feature` exactement à la promotion livrable de Phase 6, aucun bump `production` implicite.
- [ ] `Literal`/`Expression` contenant HTML/JS/sélecteur/chemin est rejetée en domaine, build et runtime partagé sans souscription ni écriture (FR-010 inv.10).
- [ ] `Page->A->B->C` profondeur 3 et cycle `A->B->A` sont rejetés en `QuickWindowDependencyAnalyzer` et en build/export `Blocked` avec diagnostic `depth-exceeded`/`cycle`; `Page->A->B` reste vert.
- [ ] Node LTS épinglée (`20.18.x` via `.nvmrc` + `package.json` `engines.node` + `node --version`) est identique dans les rapports Phase 0 et Phase 7; aucune divergence de version n’est tolérée.
- [ ] `PresentationDefaults` contient `Title`/`Center`/`Backdrop`/`Chrome` borné/`IsDraggable=true`/`IsResizable=false`/`IsViewportConstrained=true` et aucune autre propriété V1; chrome host reste hors `CanvasSize` (FR-UI-02/11).
- [ ] Annexe A mapping `FR-001..029` + `FR-UI-01..22` est 100% verte et `rg FR-0` ne révèle aucune FR orpheline.
- [ ] Full suites Builder, runtime JS, package/conformance TF100Web et vérification docs réussissent par rapport aux baselines fraîches.
- [ ] Toute écriture PLC réelle reste explicitement autorisée et traçable; sinon le gate industriel reste ouvert.
