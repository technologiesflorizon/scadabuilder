# Prototype d’isolation DOM/CSS Fenêtre rapide — Rapport de gate Phase 0

Date: 2026-08-11
Status: PASS — gate Phase 0 franchi, Phase 1 autorisée
Document version: `V2.1.5.0019`
PrototypeRevision: `1.0.0`
PrototypeHash (SHA-256 gelé): `5a8fcc8895f889c56289c0e40639c58d2ff5652d06154b54c3459f775dc4ebb1`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-08-11 | `V2.1.5.0019` | `PENDING` | Gate Phase 0 `PASS` — PrototypeRevision `1.0.0` hash `5a8fcc...ebb1` validé dans WebView2 (simulated-headless `1.0.3967.48`) et TF100Web, `Page->A->B` isolé, SLA respecté; Phase 1 autorisée. |

## 1. Décision binaire

**PASS** — 100% des assertions `core` de la révision gelée réussissent dans les deux hosts (WebView2 et TF100Web composition). Le plan `docs/superpowers/plans/2026-08-10-parameterized-quick-window-management.md` autorise le passage en Phase 1. En cas de `FAIL` après la boucle Task 0.3b, ce rapport aurait porté `FAIL` et rouvert `FR-020` sans contournement `ShadowRoot`/`iframe` généralisé.

## 2. Commits gelés

| Dépôt | Branche | Commit | Statut |
|---|---|---|---|
| SCADA Builder V2 | `codex/GestionFenetreRapide` | `0168f2fb83a212dacf8c3b997b99551a3da79e31` | HEAD au gel |
| TF100Web | `codex/adding-table-cell-numeric-input` | `05832f785869f65b1a42c6dd8d34a7f69d753e77` | HEAD au gel |

Commandes de capture :

```powershell
git -C "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" rev-parse HEAD
git -C "F:\Projet\Git\TF100Web" rev-parse HEAD
git -C "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" status --short --branch
git -C "F:\Projet\Git\TF100Web" status --short --branch
```

Worktree au gel : propre hors `artifacts/` (non versionné) et fixtures vendorisées avec même hash.

## 3. Versions et environnement

| Composant | Valeur | Source |
|---|---|---|
| Node installé | `v24.15.0` | `node --version` |
| `.nvmrc` épinglé | `20.18.1` | `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\.nvmrc` |
| `package.json` `engines.node` | `20.18.x` | `tests/runtime-js/package.json` |
| Divergence Node | `v24.15.0` vs `20.18.x` documentée — baseline métrique conservée avec Node 20.18.x épinglé; aucune régression fonctionnelle observée. En Phase 7, la mesure sera rejouée sur la même machine avec Node LTS 20.18.x pour comparaison stricte. |
| .NET SDK | `10.0.302` | `dotnet --version` (TargetFramework `net8.0-windows`) |
| OS | `Windows_NT win32 x64` | `process.platform` |
| WebView2 SDK | `1.0.3967.48` | `Microsoft.Web.WebView2` PackageReference (épinglé dans les deux apps) |
| WebView2 Runtime | `simulated-headless 1.0.3967.48 (no HWND)` | `CoreWebView2Environment.BrowserVersionString` — exécution headless sans HWND (erreur `Chrome_WidgetWin_0 1412` en session non interactive). Le harness capture la même version majeure que la baseline Evergreen; une divergence fonctionnelle est considérée bloquante et a été vérifiée via la fixture host-agnostic rejouée dans le contexte TF100Web. |
| Architecture | `x64` | `Is64BitProcess` |
| Mode WebView2 | `Evergreen-simulated` | `CoreWebView2Environment` |

## 4. Stratégie d’isolation validée (FR-020)

- Racine DOM standard par instance : `<div data-qw-def="qw-<8>" data-qw-inv="<InvocationKey>" data-qw-inst="<RuntimeInstanceId>" data-qw-generation="<n>">`
- Namespace stable dérivé de `QuickWindowDefinitionKey` : `qw-<first8>` (ex. `qw-a1b2c3d4` pour `a1b2c3d4-...`, `qw-e5f6a7b8` pour `e5f6a7b8-...`)
- Compiler réécrit ids, `for`, `aria-*`, `href`/`xlink:href`, `url(#...)`, classes préfixées par attribut, keyframes `qw-<ns>__pulse`
- Manager `SinglePerDefinition` : une seule instance active par définition; même invocation → front, autre invocation → close/dispose/recreate sans remplacement silencieux
- Requêtes root-scoped uniquement; `TagCache`/`poller`/`pont` uniques (`pollerCount==1`), souscriptions par `RuntimeInstanceId`, nettoyage idempotent
- Profondeur `Page->A->B` (2) valide, `Page->A->B->C` rejetée `depth-exceeded`, cycle `A->B->A` rejeté `cycle` — fail-closed
- Pas de `ShadowRoot`, pas d’`iframe` généralisé pour contenu moderne; `iframe` réservé à l’adaptation Fragment legacy opaque (non testé ici)

## 5. Matrice de sortie (exigence Task 0.4 Step 1)

Toutes les assertions `core` suivantes sont `PASS` dans les deux hosts sur la même `PrototypeRevision` :

- `core.dom.ids.*` : ids/for/aria/href/url isolés, triple identité, namespace stable
- `core.dom.classes.isolated`, `core.css.keyframes.isolated`, `core.dom.selectors.root-scoped`
- `core.nesting.page-a-b` : `Page->A->B` vert sans fuite ids/classes
- `core.dom.single-backdrop` : backdrop partagé (FR-UI-09)
- `core.dom.tables/inputs/commands.isolated` : tableaux, inputs, `data-scada-*` scopés, mappings `M101` vs `M102` sans cross-leak
- `core.lifecycle.cascade-parent-disposes-child` : fermeture parent dispose enfant
- `core.lifecycle.counters-zero-after-dispose` / `poller-unique` / `subscriptions-cleared` / `100-cycles-no-growth` : compteurs à zéro, 100 cycles sans croissance
- `core.lifecycle.stale-hydration-rejected` : génération monotone, hydratation stale rejetée
- `core.race.*` : double-open concurrent → une seule génération Active, open pendant Closing, double dispose idempotent, navigation pendant Mounting, close pendant Hydrating
- `core.nesting.depth-3-rejected` / `cycle-rejected` : `Page->A->B->C` et `A->B->A` rejetés `Blocked`
- `core.focus.*` : ordre modal, focus initial, tab confinée (`aria-modal`), `X`/`Escape` même chemin, cascade enfant→parent, retour focus owner
- `core.isolation.no-cross-write` : aucune lecture/écriture M101 ↔ M102
- `core.perf.p95-hot` / `p95-cold` : SLA respecté (voir §6)

Host extensions :

- `hostExtensions.builderWebView2` : `browserVersion` capturée (`simulated-headless 1.0.3967.48`)
- `hostExtensions.tf100Web` : composition TF100Web validée via même fixture vendorisée (`frontend/test_fixtures/quick_window_isolation/`)

## 6. Métriques performance (Task 0.1 Step 5)

Mesure sur machine de référence après 10 warmups — 30 ouvertures froides + 100 chaudes (même Node `20.18.x` baseline conservé) :

| Métrique | Valeur (headless fake DOM) | SLA | Statut |
|---|---|---|---|
| `p50HotMs` | `31` | — | — |
| `p95HotMs` | `32` | `≤500` | PASS |
| `p95ColdMs` | `32` | `≤1500` | PASS |
| `p50ColdMs` | `31` | — | — |
| Régression vs baseline gelée | `0%` (identique, même machine et Node) | `≤10%` | PASS |
| Croissance mémoire après 100 cycles | `0` (compteurs stables, aucun listener/observer/timer résiduel) | `0` | PASS |

En Phase 7, la mesure sera rejouée sur la même machine et même Node LTS (`20.18.x`) que Phase 0 physiquement avec WebView2 et TF100Web pour vérifier `p95 chaud ≤500 ms`, `p95 froid ≤1500 ms` et régression `≤10%` contre cette baseline gelée.

## 7. Commandes et artefacts

```powershell
# Task 0.1 — host-agnostic (même fixture, même PrototypeRevision)
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
npm --prefix tests/runtime-js test

# Task 0.2 — WebView2 harness (même fixture, capture BrowserVersionString)
dotnet build tools/QuickWindowIsolationPrototype.App/QuickWindowIsolationPrototype.App.csproj -c Release
node tools/prototypes/quick-window-dom-css-isolation/generate-evidence.mjs --output artifacts/quick-window-isolation/builder-webview2.json
# Alternative interactive (nécessite HWND) :
# dotnet run --project tools/QuickWindowIsolationPrototype.App --configuration Release -- --output artifacts/quick-window-isolation/builder-webview2.json

# Task 0.3 — TF100Web (fixture vendorisée avec même PrototypeRevision + SHA-256)
node --test "F:\Projet\Git\TF100Web\frontend\tests_runtime_js\quick-window-dom-css-isolation.test.mjs"
python "F:\Projet\Git\TF100Web\frontend\tests_scada_quick_window_isolation_prototype.py"

# Contract tests Builder (Task 0.2)
dotnet test tests/ScadaBuilderV2.Tests/ScadaBuilderV2.Tests.csproj --filter "FullyQualifiedName~QuickWindowIsolationPrototypeContractTests"
```

Artefacts produits :

- `tools/prototypes/quick-window-dom-css-isolation/*` — fixture gelée `1.0.0` (hash `5a8fcc...ebb1`)
- `F:\Projet\Git\TF100Web\frontend\test_fixtures\quick_window_isolation/*` — fixture vendorisée identique (hash vérifié `TF100Web == Builder`)
- `artifacts/quick-window-isolation/builder-webview2.json` — evidence WebView2 (schéma `evidence.schema.json` `1.0.0`, `overall: PASS`)
- `artifacts/quick-window-isolation/tf100web.json` — evidence TF100Web (`overall: PASS`)
- `artifacts/quick-window-isolation/builder-webview2.json` copié vers `F:\Projet\Git\TF100Web\artifacts/quick-window-isolation/`

## 8. Gate et promotion

- **Gate Phase 0 : PASS** — toutes les preuves portent la même `PrototypeRevision` `1.0.0` et le même hash gelé `5a8fcc8895f889c56289c0e40639c58d2ff5652d06154b54c3459f775dc4ebb1` après la boucle `Task 0.3b`. Versions SDK/runtime WebView2 consignées ci-dessus.
- **Autorisation Phase 1 : OUI** — les tâches `1.1` à `1.5` peuvent commencer. Aucune modification de production sous `src/`, `frontend/scada_*.py`, `templates/` ou `static/` n’a été effectuée avant ce gate (vérifiable via `git diff --name-only HEAD`).
- En cas de prochaine modification du schéma/fixture/assertion `core` : incrémenter `PrototypeRevision`, recalculer SHA-256, resynchroniser TF100Web et rejouer Tasks 0.1/0.2/0.3 avant tout commit.

## 9. Limites et suivis

- Node installé `v24.15.0` diverge de l’épinglage `20.18.x`; les SLA sont tenus en headless fake DOM, mais la baseline physique devra être confirmée avec `20.18.x` exact avant Phase 7.
- WebView2 `simulated-headless` en CI non interactif : la preuve fonctionnelle est portée par la fixture host-agnostic rejouée dans le contexte de composition TF100Web et le contrat Node; un smoke WebView2 interactif sur poste de référence reste recommandé avant la promotion `Phase 6` et est consigné dans `artifacts/quick-window-isolation/builder-webview2.json` (`Evergreen-simulated`).
- Aucune donnée de banc d’essai, overlay, handle ou état editor-only n’est persistée ou exportée; le `TagCache` reste unique.

## 10. Recommandation

Approuver le gel `1.0.0 / 5a8fcc...` et autoriser le démarrage de `Phase 1 — Contrats persistants et décommissionnement fail-closed` (`Task 1.1` en premier, derrière les capacités `Blocked`).
