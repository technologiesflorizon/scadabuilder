# QuickWindow DOM/CSS Isolation Prototype — Phase 0 Gate (FR-020 / FR-026)

**PrototypeRevision:** `1.0.0`  
**Status:** Gate bloquant avant toute modification de production (`src/`, `frontend/scada_*.py`, `templates/`, `static/` interdits)  
**Spec:** `docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md:FR-020`  
**Plan:** `docs/superpowers/plans/2026-08-10-parameterized-quick-window-management.md#phase-0`

## Objectif

Prouver que le contenu moderne d’une Fenêtre rapide peut être monté dans une racine DOM standard appartenant à son instance, avec un namespace CSS/ID stable dérivé de `QuickWindowDefinitionKey`, des sélecteurs root-scoped, un `RuntimeInstanceId` inscrit sur la racine, et un nettoyage idempotent à la fermeture — sans `ShadowRoot`, sans `iframe` généralisé et sans réécriture arbitraire à chaque ouverture.

La fixture joue `Page -> A -> B` (A enfant de Page, B enfant de A). Deux définitions A/B réutilisent volontairement les mêmes ids/classes auteur (`sensorValue`, `actionButton`, `shared-class`, `pulse`, `page-gradient`, `for`/`aria-*`/`href`/`url(#...)`) et prouve que la compilation les isole par préfixe `qw-<8>` stable.

## Contenu

| Fichier | Rôle |
|---|---|
| `index.html` | Hôte statique déterministe : page + `qw-host-root` + boutons d’ouverture + rendu `evidence` |
| `prototype.css` | Styles sentinelles : page `#page-root` vs ` [data-qw-def="qw-…"]` namespacés + backdrop/frame host-owned |
| `prototype.js` | Manager host-agnostic `SinglePerDefinition`, générations monotones, stale-hydration, cascade, counters instrumentés |
| `assertions.js` | Assertions `core` machine-readable (ids, CSS, lifecycle, races, focus) + `hostExtensions` namespacées |
| `evidence.schema.json` | Schéma versionné `1.0.0` — chaque run émet `prototypeRevision`, `prototypeHash`, `versions`, `assertions`, `metrics`, `overall: PASS|FAIL` |
| `tests/runtime-js/quick-window-dom-css-isolation.test.mjs` | Suite Node `node:test` qui exécute la même fixture hors host produit (Task 0.1 Step 4) |

## Invariants couverts (core)

- ids DOM, `for`, `aria-*`, `href`/`xlink:href`, `url(#...)`, classes, keyframes, tables, inputs, `data-scada-*`
- Sélecteurs relatifs à la racine d’instance; triple identité `[data-qw-def][data-qw-inv][data-qw-inst]`
- Un seul `TagCache`/`poller`/`pont`; souscriptions/écritures par `RuntimeInstanceId`
- 100 cycles sans croissance; `X` / `Escape` même chemin; cascade parent→enfant; focus initial + trapped modal + retour owner
- Races : double open concurrent, navigation pendant Mounting, close pendant Hydrating, open autre invocation pendant Closing, snapshot stale, double dispose idempotent — une seule génération `Active`
- Profondeur 3 (`Page->A->B->C`) et cycle (`A->B->A`) rejetés `depth-exceeded`/`cycle`

## Lancement

```powershell
# Task 0.1 Step 4 — sans host produit
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
npm --prefix tests/runtime-js test

# Task 0.2 — WebView2 harness (même fixture, même PrototypeRevision)
dotnet run --project tools/QuickWindowIsolationPrototype.App --configuration Release -- --output artifacts/quick-window-isolation/builder-webview2.json

# Task 0.3 — TF100Web (fixture vendorisée avec PrototypeRevision + SHA-256)
Set-Location "F:\Projet\Git\TF100Web"
node --test frontend/tests_runtime_js/quick-window-dom-css-isolation.test.mjs
python -m pytest frontend/tests_scada_quick_window_isolation_prototype.py
```

## Hash et versioning

- À chaque modification du schéma, de la fixture ou d’une assertion `core` : incrémenter `PrototypeRevision`, recalculer le SHA-256 du manifest (`tools/prototypes/quick-window-dom-css-isolation/**`), resynchroniser TF100Web et rejouer Tasks 0.1/0.2/0.3.
- Le hash gelé est inscrit dans le rapport `docs/superpowers/reports/2026-08-10-quick-window-dom-css-isolation-prototype.md` et dans `tests/runtime-js` / `frontend/tests_runtime_js` (test SHA-256 strict).

## Performance (Task 0.1 Step 5)

Sur machine de référence après 10 warmups : 30 ouvertures froides + 100 chaudes. Consigner CPU/RAM/OS/WebView2/Node (`20.18.x` épinglée via `.nvmrc` + `package.json` `engines.node`) et p50/p95 `request→Active`. SLA : `p95 chaud ≤500 ms`, `p95 froid ≤1500 ms`, régression ≤10% entre fixture gelée et portage prod.

## Gate

`Task 0.4` exige `100%` core `PASS` dans les deux hosts. Si `FAIL` après boucle `0.3b`, écrire `FAIL` et rouvrir `FR-020` — pas de contournement `ShadowRoot`/`iframe` généralisé.

## Captures & artefacts

- `artifacts/quick-window-isolation/builder-webview2.json` — sortie harness WebView2 (capture visuelle + BrowserVersionString)
- `artifacts/quick-window-isolation/tf100web.json` — sortie TF100Web
- `artifacts/quick-window-isolation/evidence.json` — run local (si `--output`)
