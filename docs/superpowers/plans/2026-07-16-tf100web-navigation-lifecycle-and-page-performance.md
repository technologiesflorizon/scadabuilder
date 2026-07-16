# Cycle de navigation TF100Web et performance des pages - Plan d'implementation

Date: 2026-07-16
Status: Superseded implementation plan - folded into general runtime conformance plan
Document version: `V2.1.4.0046`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-16 | `V2.1.4.0046` | `PENDING` | Plan remplace par `2026-07-16-scada-v2-tf100web-runtime-conformance.md`; les tasks navigation/performance y demeurent obligatoires. |
| 2026-07-16 | `V2.1.4.0045` | `PENDING` | Plan cross-repository pour le cycle latest-wins, l'hydratation obligatoire, la performance serveur et l'acceptation exhaustive des quatre pages. |

**Goal:** Eliminer definitivement les courses navigation/polling et garantir tous les comportements authorés de `win00003`, `win00004`, `win00008` et `win00012_modern_no_legacy` avec des temps de composition observables.

**Architecture:** TF100Web possede une generation de navigation latest-wins, une barriere d'hydratation rattachee au DOM courant et un cache serveur invalidable. Le runtime partage Builder, le manifest 2.2 et les voies uniques de polling/ecriture restent inchanges.

**Tech Stack:** JavaScript navigateur, Node test runner natif, Python/Django, SCADA Builder V2 `.sb2`, PowerShell.

## Global Constraints

- Specification: `docs/superpowers/specs/2026-07-16-tf100web-navigation-lifecycle-and-page-performance-design.md`.
- Decision: `DEC-0046`.
- Repositories: SCADA Builder V2 pour contrats/package de reference; `F:\Projet\Git\TF100Web` pour l'implementation runtime et serveur.
- Ne pas creer de second poller, dispatcher, bridge d'ecriture ou chemin Tableau.
- Ne pas vider le cache de valeurs PLC par principe; invalider le travail, les dependances et le DOM obsoletes.
- Traiter `YL_E12_HDEG4` absent par fallback qualite non bloquant; ne pas fabriquer de mapping.
- Les tests d'ecriture industrielle sont bloques sans fenetre et valeurs de test explicitement autorisees.
- Les changements projet utilisateur deja presents dans le worktree Builder ne sont pas stages.

## Phase 0 - Baseline et gates

### Task 1: Verrouiller les preuves avant correction

**Files:**
- Inspect: `F:\Projet\Git\TF100Web\static\asset\js\station\visualisation_import.js`
- Inspect: `F:\Projet\Git\TF100Web\frontend\scada_builder_composition.py`
- Inspect: `F:\Projet\Git\TF100Web\frontend\views.py`
- Inspect: `C:\Users\mathi\Downloads\AMR_REF_SCADA_V2.sb2`
- Inspect: `C:\Users\mathi\Downloads\tf100web-scada-tags (3).json`

**Interfaces:** Enregistre le comportement et les temps courants sans mutation serveur ni PLC.

- [ ] Consigner SHA/commit deploye, hash `scada-runtime.js`, manifest et generation du package.
- [ ] Mesurer froid/chaud les endpoints page et snapshot pour les quatre pages.
- [ ] Capturer la reproduction `08 -> 12 -> 08`, incluant DOM, overlays, valeurs, historique et requetes.
- [ ] Auditer l'inventaire exact des commandes, etats, bindings et compositions contre le `.sb2`.
- [ ] Confirmer que `YL_E12_HDEG4` absent produit le fallback qualite et un diagnostic sans bloquer les autres comportements.
- [ ] Definir avec l'exploitant la fenetre et les valeurs sans danger pour les tests d'ecriture PLC.

**Gate:** Aucune ecriture industrielle avant autorisation. La correction client/serveur et les tests locaux peuvent avancer sans ce gate.

## Phase 1 - Regressions comportementales TF100Web

### Task 2: Ajouter un harness JavaScript deterministe

**Files:**
- Modify: `F:\Projet\Git\TF100Web\static\asset\js\station\visualisation_import.js`
- Create: `F:\Projet\Git\TF100Web\frontend\tests_runtime_js\navigation-lifecycle.test.mjs`
- Create: `F:\Projet\Git\TF100Web\frontend\tests_runtime_js\tag-cache-hydration.test.mjs`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`

**Interfaces:** Le code de production expose des primitives testables sans installer un second runtime ni une dependance navigateur externe.

- [ ] Extraire ou isoler les primitives generation, annulation, acceptation et barriere d'hydratation de facon testable avec `node:test`.
- [ ] Ecrire un test rouge pour `poll(true)` pendant `pollInFlight`.
- [ ] Ecrire un test rouge ou deux chargements finissent dans l'ordre inverse.
- [ ] Ecrire les regressions histoire/URL, overlay proprietaire, erreur courante et reponse obsolete.
- [ ] Ecrire le cas cache identique + nouveau DOM qui doit notifier le runtime.
- [ ] Ecrire l'idempotence des handlers apres plusieurs navigations.
- [ ] Conserver les assertions de contrat existantes et les faire pointer vers le comportement, pas seulement des chaines source.

**Validation:**

```powershell
node --test frontend/tests_runtime_js/*.test.mjs
python manage.py test frontend.tests_scada_package.ScadaRuntimeInitContractTests
```

**Commit:** `test: cover scada navigation and hydration races`

## Phase 2 - Cycle latest-navigation-wins

### Task 3: Implementer la propriete de generation du corps

**Files:**
- Modify: `F:\Projet\Git\TF100Web\static\asset\js\station\visualisation_import.js`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_runtime_js\navigation-lifecycle.test.mjs`

**Interfaces:** `ScadaHost.loadPage()` accepte une intention de navigation et produit une page seulement si sa generation est encore courante.

- [ ] Ajouter le compteur de generation et l'`AbortController` de page.
- [ ] Invalider la requete, les callbacks et les references DOM de l'ancienne generation.
- [ ] Verifier la generation avant chaque mutation header/body/footer, dimensions, popup, loading et erreur.
- [ ] Conserver l'ancien DOM jusqu'a reception de la derniere composition valide.
- [ ] Deplacer le commit d'historique apres l'acceptation de page; traiter `popstate` sans creer une nouvelle entree.
- [ ] Coalescer les demandes identiques et empecher le double branchement des handlers.
- [ ] Donner aux popups un controleur distinct et les fermer/invalider lors d'un changement de corps accepte.

### Task 4: Rendre l'hydratation obligatoire et awaitable

**Files:**
- Modify: `F:\Projet\Git\TF100Web\static\asset\js\station\visualisation_import.js`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_runtime_js\tag-cache-hydration.test.mjs`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`

**Interfaces:** La generation acceptee attend une hydratation de son DOM; un poll en vol ne peut pas transformer la demande forcee en no-op.

- [ ] Remplacer le retour silencieux `pollInFlight` par une promesse partagee et une demande forcee pending/coalescee.
- [ ] Rattacher snapshot et notification a la generation courante.
- [ ] Recalculer les mappings du DOM apres le rendu complet.
- [ ] Appliquer les ValueBindings et notifier `TagBridge` meme pour des valeurs de cache identiques.
- [ ] Reprendre le poll periodique seulement apres l'hydratation initiale.
- [ ] Preserver une seule instance de cache, poller, `TagBridge` et `CommandDispatcher`.

**Validation:**

```powershell
node --test frontend/tests_runtime_js/*.test.mjs
python manage.py test frontend.tests_scada_package
python manage.py check
```

**Commit:** `fix: make scada navigation latest-wins and hydration mandatory`

## Phase 3 - Composition serveur performante

### Task 5: Instrumenter et supprimer les scans multiplicatifs

**Files:**
- Modify: `F:\Projet\Git\TF100Web\frontend\scada_builder_composition.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\views.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_page_composition.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`

**Interfaces:** L'endpoint produit le meme JSON/HTML, avec cout d'injection lineaire ou indexe et mesures de phase.

- [ ] Ajouter timings composition, resolution et injection dans logs structures ou `Server-Timing`.
- [ ] Ajouter une regression qui compte/interdit un scan de fragment par binding.
- [ ] Construire un index des cibles DOM ou injecter les attributs en un passage.
- [ ] Preserver namespace, ordre, echappement, mappings lecture/ecriture distincts, datatype, format et permissions.
- [ ] Comparer byte/DOM-equivalence sur les fixtures 2.1 et 2.2.

**Commit:** `perf: make scada binding injection single-pass`

### Task 6: Ajouter un cache structurel explicitement invalidable

**Files:**
- Modify: `F:\Projet\Git\TF100Web\frontend\scada_builder_composition.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\views.py`
- Modify: `F:\Projet\Git\TF100Web\core\management\commands\deploy_scada_builder.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_deploy.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_page_composition.py`

**Interfaces:** Cache seulement les donnees stables; cle et invalidation couvrent deploiement et revision catalogue.

- [ ] Definir une signature de deploiement atomique et une revision de mappings.
- [ ] Mettre en cache manifest, fragments extraits et metadata structurelle par signature/page.
- [ ] Garder la resolution des droits/mappings fraiche ou inclure sa revision dans la cle.
- [ ] Invalider apres deploiement, remplacement de package et changement pertinent du catalogue.
- [ ] Ajouter tests stale-cache, deploiement concurrent et retour a froid.
- [ ] Evaluer compression/ETag seulement si les mesures restantes le justifient.

**Validation:**

```powershell
python manage.py test frontend.tests_scada_page_composition frontend.tests_scada_package frontend.tests_scada_deploy
python manage.py check
```

**Commit:** `perf: cache deployed scada composition safely`

## Phase 4 - Contrats exhaustifs des quatre pages

### Task 7: Ajouter l'inventaire package comme gate automatise

**Files:**
- Modify: `tests/ScadaBuilderV2.Tests/Win00012DefrostToggleConfigurationTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/ReferenceScadaPageBehaviorContractTests.cs`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`
- Test fixture: package minimal ou export controle de `AMR_REF_SCADA_V2`

**Interfaces:** Le test compare l'inventaire authore a l'intake rendu sans coupler TF100Web a un chemin local utilisateur.

- [ ] Verifier les 8 destinations de `win00003`.
- [ ] Verifier la composition `win00004 = win00002 + win00004 + win00003` et l'absence de bindings locaux fantomes.
- [ ] Verifier les 8 etats, 2 lectures variables et 1 input lecture/ecriture de `win00008`.
- [ ] Verifier les 56 Toggles, 56 etats, 56 heures et 70 consignes de `win00012_modern_no_legacy`.
- [ ] Verifier que toutes les references de tags se resolvent dans l'export officiel.
- [ ] Verifier explicitement le fallback qualite et le diagnostic lorsque `YL_E12_HDEG4` est absent.
- [ ] Verifier que le petit input temperature exterieure reste declare non lie, sans mapping implicite.

**Validation:**

```powershell
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ReferenceScadaPageBehaviorContractTests|FullyQualifiedName~Win00012DefrostToggleConfigurationTests|FullyQualifiedName~Ft100SceneExporterTests"
python manage.py test frontend.tests_scada_package
```

**Commit Builder:** `test: lock reference scada page behavior contracts`

### Task 8: Valider le comportement mapping absent

**Files:**
- Input: nouvel export officiel `tf100web-scada-tags-v1`
- Modify only if authoritative: `projects/AMR_REF_SCADA_V2/project.json`, snapshot sous `projects/AMR_REF_SCADA_V2/imports/tags/`, scene `win00012_modern_no_legacy`
- Generate: nouvel `.sb2` de livraison

**Interfaces:** Le mapping vient de TF100Web/apres correction automate, puis est importe par le workflow catalogue normal.

- [ ] Conserver l'absence officielle comme fixture de qualite degradee.
- [ ] Verifier fallback visuel, diagnostic et absence d'ecriture pour cette seule commande.
- [ ] Verifier que les 55 autres Toggles et 126 bindings continuent de fonctionner.
- [ ] Si un futur export officiel ajoute le tag, l'importer par le workflow normal sans migration speciale.

**Gate:** Aucun; le mapping absent est un cas de qualite attendu du contrat general.

## Phase 5 - Documentation, deploiement et acceptance

### Task 9: Synchroniser les preuves apres implementation

**Files:**
- Modify: `F:\Projet\Git\TF100Web\docs\SCADA_BUILDER_SB2_RUNTIME.md`
- Modify: `VERSION`
- Modify: `docs/README.md`
- Modify: `docs/00_governance/DECISION_REGISTER_V2.md`
- Modify: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`
- Modify: `docs/08_implementation_status/KNOWN_GAPS_V2.md`
- Modify: cette specification et ce plan
- Modify if coverage changes: `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md`

- [ ] Remplacer les statuts pending uniquement apres tests et commits reels.
- [ ] Inscrire hashes Builder/TF100Web, resultats, timings froid/chaud et diagnostic du mapping absent.
- [ ] Ne retirer le gap navigation qu'apres smoke aller-retour reussi.
- [ ] Executer verification docs et `git diff --check` dans les deux repositories.

### Task 10: Deployer et executer la matrice de production

**Deployment order:**

1. Deployer les commits TF100Web valides.
2. Executer migration uniquement si le code en ajoute une; sinon aucune migration.
3. Executer `collectstatic`, invalider les caches de composition et redemarrer `tf100web.service`.
4. Verifier les hashes JS et la generation de package servis.
5. Deployer un nouveau `.sb2` seulement lorsque son contenu projet change; le mapping absent seul ne force pas un export.
6. Executer les smokes lecture/navigation, puis les ecritures dans la fenetre autorisee.

**Production acceptance:**

- [ ] `win00003`: 8/8 destinations et retour/avance.
- [ ] `win00004`: composition complete et footer 8/8 apres aller-retour.
- [ ] `win00008`: 8/8 etats, 2/2 lectures variables, 1/1 input lecture/ecriture apres chargement frais et retour de `win00012`.
- [ ] `win00012_modern_no_legacy`: 55 Toggles avec mapping et le Toggle absent en fallback qualite; 56/56 heures et 70/70 consignes avec readback autorise.
- [ ] Sequences rapides et ordre inverse: seule la derniere page gagne.
- [ ] Aucun handler duplique, aucune reponse stale, aucune zone blanche/fallback residuel.
- [ ] Temps serveur conformes ou ecart documente par phase.

**Rollback:** Revenir aux commits TF100Web precedents, relancer `collectstatic` et le service, invalider les caches; remettre l'ancien `.sb2` seulement si un nouvel export avait ete deploye. Ne jamais « rollback » une valeur PLC par script generique.

## Final Validation Checklist

- [ ] Les tests JavaScript testent la concurrence reelle, pas uniquement des chaines source.
- [ ] Les suites Django composition/package/deploiement passent.
- [ ] Les tests Builder verrouillent l'inventaire des quatre pages.
- [ ] La course `pollInFlight` ne peut plus annuler une hydratation forcee.
- [ ] Aucune ancienne navigation ne peut muter DOM, historique ou loading.
- [ ] Le chemin serveur n'est plus O(fragment x bindings).
- [ ] Les caches sont invalides par deploiement et revision catalogue.
- [ ] `YL_E12_HDEG4` absent produit le fallback qualite non bloquant attendu.
- [ ] Tous les comportements de la matrice passent dans l'environnement autorise.
