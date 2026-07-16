# Conformite runtime generale SCADA Builder V2 vers TF100Web - Plan d'implementation

Date: 2026-07-16
Status: Active implementation plan - execution in progress
Document version: `V2.1.4.0054`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-16 | `V2.1.4.0054` | TF100Web `7d60c63` | Task 8 completee : negotiation 2.3, registry host, hash runtime et fixture exacte avant remplacement. |
| 2026-07-16 | `V2.1.4.0053` | `PENDING` | Task 7 completee : 9 actions objet, conditions, ordre/propagation, page scope et lifecycle partage. |
| 2026-07-16 | `V2.1.4.0052` | `PENDING` | Task 6 completee : CommandConfig canonique, Momentary press/release, intents 1.0, async et cleanup. |
| 2026-07-16 | `V2.1.4.0051` | `PENDING` | Task 5 completee : semantiques Etat/Expression/Effet table-driven, reversibles et deterministes. |
| 2026-07-16 | `V2.1.4.0050` | `PENDING` | Task 4 completee : fixture `.sb2` deterministe, index exhaustif, sanitization et hash canonique. |
| 2026-07-16 | `V2.1.4.0049` | `PENDING` | Task 3 completee cote Builder : manifest 2.3 strict, SHA-256 runtime, validation et profils de compatibilite explicites. |
| 2026-07-16 | `V2.1.4.0048` | `PENDING` | Task 2 completee : matrice generee, verification stale, evidence typee et statut strict fail-closed. |
| 2026-07-16 | `V2.1.4.0047` | `PENDING` | Task 1 completee : registre type, analyseur pur, statuts de gaps et 7 tests d'exhaustivite/analyse verts. |
| 2026-07-16 | `V2.1.4.0046` | `PENDING` | Plan general remplacant l'execution page-centrique par un registre, une negotiation 2.3 et une suite de conformance partagee. |

> **For agentic workers:** Execute this plan task-by-task. Preserve one coherent commit per task and do not start a later phase while an earlier contract gate is red.

**Goal:** Garantir toutes les capacites runtime actuellement authorables/exportables de SCADA Builder V2, sans execution silencieusement manquante et sans dupliquer les semantiques entre Builder, runtime package et TF100Web.

**Architecture:** Un registre Builder derive les capacites requises et les publie dans le manifest 2.3. Le runtime package partage execute toutes les semantiques portables; TF100Web fournit des services host et refuse un package demandant une capacite absente. Une suite `.sb2` de conformance partagee verrouille chaque variante du modele.

**Tech Stack:** .NET 8/C#, JavaScript, Node `node:test`, Python/Django, WPF/WebView2, `.sb2` ZIP, PowerShell.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-16-scada-v2-tf100web-runtime-conformance-design.md` (`DEC-0047`).
- Navigation invariant: `DEC-0046`.
- Repositories: SCADA Builder V2 et `F:\Projet\Git\TF100Web`.
- Un proprietaire semantique par capacite; TF100Web ne recode pas les expressions, effets ou actions deja possedes par le runtime partage.
- Une capacite non implementee est bloquee a l'export/deploiement, jamais ignoree.
- Manifest 2.3 TF100Web d'abord, export Builder ensuite.
- Les packages 2.1/2.2 conservent un profil de compatibilite teste.
- `YL_E12_HDEG4` est un mapping projet absent non bloquant; le fallback qualite attendu doit etre teste.
- Aucune ecriture PLC sans fenetre industrielle autorisee.
- Preserver les modifications utilisateur non reliees dans chaque worktree.

---

## Before You Start

- [ ] Capturer `git status --short --branch`, HEAD et remote des deux repositories.
- [ ] Capturer les baselines Builder, runtime JS, Django et les cinq echecs historiques sans figer leurs comptes dans le plan.
- [ ] Archiver SHA-256 du `.sb2`, du runtime servi et du catalogue officiel utilises pour le smoke.
- [ ] Auditer toutes les valeurs d'enum persistantes et tous les champs exportes; aucun inventaire manuel n'est considere complet sans test de reflection.
- [ ] Confirmer l'ordre de livraison TF100Web -> Builder 2.3 -> nouveau `.sb2`.

---

## Phase 1 - Inventaire executable des capacites

### Task 1: Creer le registre type et le test d'exhaustivite

**Files:**
- Create: `src/ScadaBuilderV2.Domain/RuntimeContracts/ScadaRuntimeCapability.cs`
- Create: `src/ScadaBuilderV2.Domain/RuntimeContracts/ScadaRuntimeCapabilityCatalog.cs`
- Create: `src/ScadaBuilderV2.Application/RuntimeContracts/ScadaRuntimeCapabilityAnalyzer.cs`
- Create: `tests/ScadaBuilderV2.Tests/RuntimeContracts/ScadaRuntimeCapabilityCatalogTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/RuntimeContracts/ScadaRuntimeCapabilityAnalyzerTests.cs`

**Interfaces:**
- Consumes: pages, bindings, `ScadaActionKind`, `ScadaCommandKind`, `ScadaCommandTrigger`, `ScadaWriteMode`, AST et `ScadaEffectBlock`.
- Produces: ensemble stable, trie et deduplique des capacites requises.

- [x] Definir les identifiants stables par famille page/binding/state/expression/effect/command/action/host.
- [x] Mapper chaque variante persistante vers exactement une capacite.
- [x] Ajouter des tests de reflection qui echouent lorsqu'un enum/champ exportable est ajoute sans mapping de capacite.
- [x] Distinguer `Supported`, `Blocked`, `Deprecated` sans encoder un statut TF100Web dans la couche Domain.
- [x] Tester projet vide, combinaison complexe, groupes, tableaux, fragments et deduplication.

**Validation:**

```powershell
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~RuntimeContracts" --no-restore
```

Expected: tous les cas cibles passent; l'ajout artificiel d'une variante non mappee fait echouer le test d'exhaustivite.

**Commit:** `feat: add typed scada runtime capability registry`

### Task 2: Generer la matrice de couverture depuis le registre

**Files:**
- Create: `tools/docs/generate-runtime-capability-matrix.ps1`
- Create: `docs/10_generated/RUNTIME_CAPABILITY_MATRIX_V2.md`
- Modify: `tools/docs/verify-docs.ps1`
- Modify: `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md`

**Interfaces:** Le registre code est la source; Markdown est un artefact verifie.

- [x] Generer id, version, owner, statut, artefacts et tests.
- [x] Faire echouer `verify-docs` si la matrice est stale.
- [x] Interdire une ligne `Supported` sans preuve Builder/runtime/TF100Web.
- [x] Documenter les capacites bloquees sans les presenter comme comportement actif.

**Commit:** `docs: generate runtime capability coverage matrix`

---

## Phase 2 - Manifest 2.3 et validation Builder

### Task 3: Ajouter le contrat `RuntimeContract`

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100PackageValidation.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
- Modify: tests de package/manifest concernes

**Interfaces:**
- Consumes: resultat de `ScadaRuntimeCapabilityAnalyzer`, hash du runtime package.
- Produces: manifest 2.3 avec `RuntimeContract.Version`, `RequiredCapabilities`, `RuntimeSha256`.

- [x] Ajouter le modele serialise exact et l'ordre deterministe des capabilities.
- [x] Calculer le hash sur le runtime effectivement package.
- [x] Faire echouer l'export si une feature emise n'est pas declaree ou est `Blocked` pour la cible FT100Web stricte.
- [x] Garder une voie explicite de generation 2.1/2.2 uniquement pour fixtures de compatibilite; aucun nouvel export operateur ne l'utilise par defaut.
- [x] Valider unknown capability, doublon, version, hash absent/invalide et package altere.

**Validation:**

```powershell
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~Ft100SceneExporterTests|FullyQualifiedName~Ft100Package" --no-restore
```

**Commit:** `feat: publish runtime capabilities in manifest 2.3`

### Task 4: Produire le package de conformance deterministe

**Files:**
- Create: `tests/ScadaBuilderV2.Tests/RuntimeContracts/ScadaV2RuntimeConformanceProjectFactory.cs`
- Create: `tests/conformance/expected-runtime-capabilities.json`
- Create: `tests/ScadaBuilderV2.Tests/RuntimeContracts/RuntimeConformancePackageTests.cs`
- Generate: `tests/conformance/artifacts/scada-v2-runtime-conformance.sb2`
- Generate: `tests/conformance/artifacts/scada-v2-runtime-conformance.sha256`

**Interfaces:** Une fixture minimale par capacite; aucun chemin utilisateur ou projet client.

- [x] Couvrir tous les axes de la matrice §4 de la specification.
- [x] Garder les fixtures petites, nommees par capability id et independantes.
- [x] Verifier generation deterministe, manifest, DOM, CSS, assets et runtime hash.
- [x] Ajouter un index machine-readable des valeurs/gestes/resultats attendus.
- [x] Interdire les secrets, mappings industriels et HTML de projet client.

**Commit:** `test: add deterministic scada runtime conformance package`

---

## Phase 3 - Runtime semantique partage unique

### Task 5: Fermer les trous unitaires Etat/Expression/Effet

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/expression-evaluator.js`
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/state-engine.js`
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/effect-applier.js`
- Modify: `tests/runtime-js/expression-evaluator.test.mjs`
- Modify: `tests/runtime-js/state-engine.test.mjs`
- Modify: `tests/runtime-js/effect-applier.test.mjs`

**Interfaces:** Consomme les formes JSON existantes; produit des transitions deterministes sans styles cumulatifs.

- [x] Ajouter des tests table-driven pour chaque node, operateur, fonction, fallback et champ d'effet.
- [x] Tester toutes les transitions entre effets, restauration baseline, slots multiples et re-init.
- [x] Corriger les gaps decouverts par les tests, incluant animations/halo et variables non definies.
- [x] Verifier tokens texte, valeurs nulles, erreurs arithmetiques et coercitions documentees.

**Commit:** `test: complete shared state expression and effect semantics`

### Task 6: Completer `CommandConfig` sans branche host parallele

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/command-dispatcher.js`
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/input-edit-guard.js`
- Modify: `tests/runtime-js/command-dispatcher.test.mjs`
- Modify: tests runtime input concernes

**Interfaces:** Produit des intents host canoniques; ecrit uniquement via `TagBridge`.

- [x] Tester les 5 triggers, 7 kinds, 4 write modes, confirmations et enabled/disabled.
- [x] Implementer `Momentary` avec press/release reels et cleanup sur DOM remplace/perte de focus.
- [x] Garantir idempotence, permissions, valeurs manquantes et concurrence write/readback.
- [x] Normaliser navigate/popup/openUrl/back vers un seul schema d'intent versionne.
- [x] Interdire toute implementation equivalente dans TF100Web hors adaptateur host.

**Commit:** `feat: complete canonical command runtime semantics`

### Task 7: Adapter toutes les actions objet au runtime partage

**Files:**
- Create: `src/ScadaBuilderV2.Rendering/Runtime/action-dispatcher.js`
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/scada-runtime.js`
- Modify: `src/ScadaBuilderV2.Rendering/ScadaBuilderV2.Rendering.csproj`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`
- Create: `tests/runtime-js/action-dispatcher.test.mjs`
- Modify: `tests/ScadaBuilderV2.Tests/Runtime/RuntimeJsModulesTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

**Interfaces:** Consomme actions/events exportes; reutilise `ExpressionEvaluator`, `TagBridge` et les intents host.

- [x] Adapter les 9 `ScadaActionKind` sans copier leurs semantiques dans TF100Web.
- [x] Implementer conditions, `All`/`Any`, missing policy, ordre et propagation.
- [x] Resoudre les cibles dans le root page-scope uniquement.
- [x] Remplacer les scripts inline semantiques par un bootstrap vers le runtime partage.
- [x] Bloquer explicitement toute action qui reste non supportee avant export 2.3.

**Commit:** `feat: unify object actions in shared scada runtime`

---

## Phase 4 - TF100Web intake 2.3 et services host

> **Authorization required before modifying `F:\Projet\Git\TF100Web`.** L'utilisateur a approuve le plan general, mais le worker doit verifier branche et worktree avant toute edition.

### Task 8: Negocier les capabilities au deploiement

**Files:**
- Modify: `F:\Projet\Git\TF100Web\frontend\scada_package.py`
- Modify: `F:\Projet\Git\TF100Web\core\management\commands\deploy_scada_builder.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_deploy.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`
- Vendor: fixture de conformance `.sb2` + SHA

**Interfaces:** Consomme manifest 2.3; compare `RequiredCapabilities` au registre host.

- [x] Definir le registre TF100Web des services/capacites reellement disponibles.
- [x] Refuser version/capability/hash non supporte avant remplacement du package actif.
- [x] Retourner diagnostics capability ids et versions, sans stack trace operateur.
- [x] Preserver et tester les profils 2.1/2.2.
- [x] Verifier que la fixture vendoree a le SHA produit par Builder.

**Validation:**

```powershell
Set-Location "F:\Projet\Git\TF100Web"
python manage.py test frontend.tests_scada_deploy frontend.tests_scada_package
python manage.py check
```

**Commit:** `feat: negotiate scada runtime capabilities`

### Task 9: Implementer l'adaptateur host unique

**Files:**
- Modify: `F:\Projet\Git\TF100Web\static\asset\js\station\visualisation_import.js`
- Create: `F:\Projet\Git\TF100Web\frontend\tests_runtime_js\host-adapter.test.mjs`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`

**Interfaces:** Services `navigate`, `history`, `popup`, `writeTag`, `openUrl`, diagnostics; aucune semantique AST/effet/action.

- [ ] Recevoir l'enveloppe d'intent canonique du runtime partage.
- [ ] Mapper navigation/history/popup vers les services host.
- [ ] Mapper ecriture vers l'unique endpoint/permission/CSRF existant.
- [ ] Appliquer politique URL et page scope.
- [ ] Supprimer/decommissionner les branches host qui interpretent la meme action en parallele.
- [ ] Tester intents invalides, doublons, origine message, page stale et permissions.

**Commit:** `refactor: use one scada runtime host adapter`

---

## Phase 5 - Lifecycle, bindings et performance generiques

### Task 10: Executer integralement `DEC-0046`

**Files:**
- Modify: `F:\Projet\Git\TF100Web\static\asset\js\station\visualisation_import.js`
- Create/Modify: tests `navigation-lifecycle.test.mjs`, `tag-cache-hydration.test.mjs`

- [ ] Generation latest-wins, AbortController et rejet stale.
- [ ] Historique/loading/popups possedes uniquement par la generation courante.
- [ ] Hydratation forced awaitable/coalescee; aucune no-op sous `pollInFlight`.
- [ ] Recalcul dependances du DOM accepte et notify meme valeur identique.
- [ ] Offline, timeout, erreur HTTP, session expiree, back/forward et reprise.

**Commit:** `fix: enforce latest-wins navigation and hydration`

### Task 11: Generaliser les bindings et controles

**Files:**
- Modify: `F:\Projet\Git\TF100Web\static\asset\js\station\visualisation_import.js`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_runtime_js\binding-runtime.test.mjs`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`

- [ ] Tester toutes les combinaisons read/write, cibles, slots, formats et datatypes.
- [ ] Tester focus/poll, Enter/Escape/blur/change, rejet, readback et permissions.
- [ ] Gerer mapping absent par fallback qualite/diagnostic non bloquant.
- [ ] Prouver qu'Element+ et Tableau utilisent cache et handler communs.

**Commit:** `test: complete generic scada binding conformance`

### Task 12: Optimiser composition et cache sans changer le contrat

**Files:**
- Modify: `F:\Projet\Git\TF100Web\frontend\scada_builder_composition.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\views.py`
- Modify: `F:\Projet\Git\TF100Web\core\management\commands\deploy_scada_builder.py`
- Modify: tests composition/deploiement/performance

- [ ] Instrumenter toutes les phases.
- [ ] Remplacer scans binding x fragment par index/passage unique.
- [ ] Cacher structure par generation de package et revision catalogue.
- [ ] Invalider atomiquement deploy/catalogue/rollback.
- [ ] Ajouter benchmarks parametres par taille, pages et bindings.

**Commit:** `perf: make scada composition linear and invalidatable`

---

## Phase 6 - Gate end-to-end general

### Task 13: Executer la fixture de conformance dans TF100Web

**Files:**
- Create: `F:\Projet\Git\TF100Web\frontend\tests_runtime_conformance.py`
- Create/Modify: harness JS/browser sous `frontend/tests_runtime_js/`
- Modify: CI/test documentation TF100Web

- [ ] Charger chaque fixture capability et comparer l'attendu machine-readable.
- [ ] Couvrir composition, runtime, intents host, snapshots, erreurs et securite.
- [ ] Faire echouer toute capability `Supported` sans test execute.
- [ ] Faire echouer toute capability `Blocked` acceptee au deploiement.
- [ ] Comparer SHA fixture Builder/TF100Web.

**Commit:** `test: enforce end-to-end scada runtime conformance`

### Task 14: Verifier preview/build/export/TF100Web

**Files:**
- Modify: `tests/ScadaBuilderV2.Tests/PreviewDocumentTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/RuntimeContracts/RuntimeConformancePackageTests.cs`
- Modify: parity tests TF100Web

- [ ] Comparer modele, preview markup, export markup, manifest capabilities et rendu host par fixture.
- [ ] Verifier exclusion editor-only et namespace.
- [ ] Verifier que le runtime package teste est celui dont le hash est manifeste/deploye.
- [ ] Ajouter un test qui interdit une nouvelle feature exportee sans capability et preuve.

**Commit:** `test: lock preview export and host runtime parity`

### Task 15: Executer les quatre integrations industrielles

**Files:**
- Test artifact: export valide `AMR_REF_SCADA_V2.sb2`
- No project mutation unless separately approved

- [ ] `win00003`: 8 navigations, rapid/latest-wins, back/forward.
- [ ] `win00004`: header/body/footer, assets, retour et footer.
- [ ] `win00008`: 8 etats, 2 lectures, 1 input, aller-retour `08 -> 12 -> 08`.
- [ ] `win00012_modern_no_legacy`: 56 boutons et 126 cellules; mapping absent = fallback qualite attendu, sans bloquer les autres.
- [ ] Ecritures uniquement dans la fenetre autorisee et avec readback.
- [ ] Conserver captures, timings, versions, hashes et diagnostics.

---

## Phase 7 - Documentation, livraison et rollback

### Task 16: Synchroniser contrats et preuves

**Files:**
- Modify: `docs/README.md`
- Modify: `docs/00_governance/DECISION_REGISTER_V2.md`
- Modify: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`
- Modify: `docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md`
- Modify: `docs/03_runtime_contracts/PREVIEW_BUILD_EXPORT_CONTRACT_V2.md`
- Modify: `docs/08_implementation_status/KNOWN_GAPS_V2.md`
- Modify: `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md`
- Modify: spec/plan et docs TF100Web

- [ ] Remplacer pending seulement apres commits/tests reels.
- [ ] Generer la matrice capability.
- [ ] Marquer chaque capability `Supported` ou `Blocked` avec preuve.
- [ ] Executer docs verification et `git diff --check`.

### Task 17: Livrer dans l'ordre et verifier rollback

- [ ] Deployer TF100Web 2.3-compatible, tests, `collectstatic`, cache invalidation, restart.
- [ ] Verifier `SupportedCapabilities` et hash runtime servis.
- [ ] Livrer Builder manifest 2.3 seulement ensuite.
- [ ] Deployer fixture puis package industriel.
- [ ] Tester rejet d'une capability inconnue sans remplacer le package actif.
- [ ] Tester rollback TF100Web et package, avec invalidation cache.

## Validation Checklist

- [ ] Chaque enum/champ exportable est mappe par reflection.
- [ ] Chaque capability `Supported` a preuves Builder, runtime et TF100Web.
- [ ] Chaque capability non supportee est bloquee avant operation.
- [ ] Manifest 2.3 negocie version, capabilities et hash.
- [ ] Un seul runtime interprete expressions, effets, commandes et actions.
- [ ] TF100Web reste adaptateur host, pas second moteur semantique.
- [ ] Toutes les pages/types/compositions/bindings/etats/commandes/actions sont couverts.
- [ ] Concurrence, offline, erreurs, permissions et securite sont couvertes.
- [ ] Les quatre pages industrielles passent; mapping absent traite en fallback non bloquant.
- [ ] Performance et invalidation cache passent leurs budgets.
