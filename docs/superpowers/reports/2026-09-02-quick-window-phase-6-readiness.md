# Préparation de la Phase 6 — promotion des capacités et export

Date: 2026-09-02
Status: PRÉPARATION — aucune capacité promue, aucun gate levé, aucun bump `feature` effectué
Document version: `V2.1.5.0052`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-09-02 | `V2.1.5.0052` | `PENDING` | Note de préparation : inventaire des treize capacités par couche, arithmétique de version, vérification de l'outillage, et deux écarts constatés dans l'énoncé de la Phase 6. |

## 1. Ce que ce document est, et n'est pas

La Phase 6 promeut des capacités et ouvre l'export. Les deux actions sont interdites tant que le soak, le canary et le déploiement production ne sont pas verts — la politique le dit deux fois, dans la Task 6.1 et dans les contraintes globales. **Rien n'a donc été promu, levé ni bumpé.**

Ce document prépare la décision : il établit, capacité par capacité, ce qui est réellement prouvé et à quelle couche, pour que la promotion soit une lecture de preuves et non un jugement au moment de l'exécution.

## 2. Inventaire des treize capacités

Trois couches doivent prouver une capacité avant sa promotion : le Builder (modèle, validation, compilation), le runtime partagé (sémantique portable), et le host TF100Web (déploiement réel). La colonne TF100Web renvoie aux assertions nommées du harnais cross-runtime (`quick-window-cross-runtime-harness.mjs`) ou aux suites du host.

| Capacité | Propriétaire | Builder | Runtime partagé | TF100Web | Promouvoir ? |
| --- | --- | --- | --- | --- | --- |
| `command.open-quick-window` | SharedRuntime | oui | oui | `open-mounts-through-the-packaged-runtime` | **Oui** |
| `command.close-quick-window` | SharedRuntime | oui | oui | `close-self-disposes-frame-and-subscriptions` | **Oui** |
| `quick-window.definition` | PackageTransport | oui | oui | registres ingérés, fragments servis | **Oui** |
| `quick-window.local-interface.typed` | SharedRuntime | oui | oui | `open-mounts-through-the-packaged-runtime` | **Oui** |
| `quick-window.port-binding` | SharedRuntime | oui | oui | `another-invocation-replaces-without-cross-mapping` | **Oui** |
| `quick-window.port.required` | SharedRuntime | oui | oui | `required-port-unbound-is-refused-before-any-frame` | **Oui** |
| `quick-window.instance.single-per-definition` | Tf100WebHost | s.o. | oui | `another-invocation-replaces-without-cross-mapping` | **Oui** |
| `quick-window.lifecycle.host-owned` | Tf100WebHost | s.o. | oui | `navigation-invalidates-every-live-instance`, `a-hundred-cycles-leak-nothing` | **Oui** |
| `quick-window.dom.scoped-root` | Tf100WebHost | s.o. | oui | suite host: contenu joignable seulement par sa racine | **Oui** |
| `quick-window.presentation.backdrop` | Tf100WebHost | s.o. | s.o. | suite host + isolation legacy: backdrop distinct, libéré au dernier cadre | **Oui** |
| `quick-window.nesting.depth-2` | Tf100WebHost | oui | oui | `depth-2-is-reachable-and-depth-3-is-refused` | **À décider** — voir §3 |
| `quick-window.binding.parent-port` | SharedRuntime | oui | oui | **aucune** | **Non** |
| `quick-window.legacy-fragment-adapter` | Tf100WebHost | **aucune** | **aucune** | **aucune** | **Non** |

## 3. Deux écarts dans l'énoncé de la Phase 6

**La Task 6.1 demande de laisser `nesting.depth-2` bloquée « tant qu'elle n'est pas livrée ». Elle l'est.** La profondeur 2 est implémentée dans le runtime partagé, couverte côté Builder, et prouvée au niveau TF100Web par une assertion nommée du harnais cross-runtime ainsi que par `tests_runtime_conformance.py`. Le paquet de soak l'exerce en continu depuis le lancement. L'instruction paraît antérieure à cette livraison; elle mérite une décision explicite plutôt qu'une application machinale.

Les deux autres exclusions restent exactes, pour des raisons différentes :

- `binding.parent-port` est bien implémentée côté Builder et runtime, mais **aucun paquet déployé ne l'exerce** : la fixture de handshake et le manifeste de soak ne portent que `SourceKind: "Tag"`, avec `ParentMemberKey: null` partout. Elle est donc non prouvée à la couche host, et le catalogue la déclare lui-même « outside the first vertical slice ».
- `legacy-fragment-adapter` n'a **aucune implémentation nulle part** — ni Builder, ni runtime, ni TF100Web. Elle n'existe que dans le catalogue et son test de catalogue.

**La Task 6.2 suppose un « gate temporaire » à retirer. Il n'y en a pas.** `EnsureRuntimeCapabilitiesExportable` est un gate **générique**, piloté par le catalogue : il refuse l'export strict 2.3 dès que l'analyse renvoie au moins une capacité `Blocked`, sans jamais nommer de capacité. `Ft100PackageValidation` valide les registres de fenêtres rapides — identité, doublons, collisions avec des pages, chemins contractuels — mais ne gate sur aucune capacité.

Conséquence : **la Task 6.2 ne demande aucune modification du code de production.** Dès que la Task 6.1 fait passer une capacité de `Blocked` à `Supported`, l'analyseur cesse de la signaler et l'export fonctionne. Les fichiers `Ft100SceneExporter.cs` et `Ft100PackageValidation.cs` listés dans la tâche n'ont vraisemblablement pas à être touchés. Ce qui reste de la 6.2 est réel mais entièrement en tests : prouver qu'un projet n'utilisant que des capacités `Supported` s'exporte, qu'un projet touchant une capacité encore `Blocked` est toujours rejeté, et que le paquet produit reste déterministe, sans donnée editor-only ni géométrie de chrome host dans le contenu.

## 4. Arithmétique de version

`VERSION` valait `V2.1.5.0047` alors que le plan était porté à `V2.1.5.0051` : les bumps de document de cette session n'avaient pas été répercutés, alors que les deux valeurs bougent ensemble dans toute l'histoire du dépôt. `VERSION` est réaligné sur `V2.1.5.0052` par un bump `iteration`, ce que la politique autorise explicitement pour un travail préparatoire tant que les capacités restent `Blocked`.

Le bump `feature` de la Phase 6, vérifié avec l'outil officiel, produira :

```
python bump_scada_v2_version.py V2.1.5.0052 feature   →   V2.1.6.0000
```

Il ne doit être effectué qu'au moment de la promotion, et seulement si aucune capacité, canary, déploiement ou export ne reste bloqué.

## 5. Outillage vérifié

| Élément | État |
| --- | --- |
| `tools/RuntimeCapabilityMatrixGenerator` | Présent |
| `tools/docs/generate-runtime-capability-matrix.ps1` | Exécuté, sortie 0, **aucune dérive** de la matrice |
| `tools/docs/verify-docs.ps1` | Errors: 0 (121 warnings préexistants) |
| `bump_scada_v2_version.py` | Présent, arithmétique vérifiée |
| `tools/quick-window/record-checkpoint.ps1` | Corrigé ce jour : résout le worktree portant la branche, refuse sinon |
| `tests/conformance/expected-runtime-capabilities.json` | 13 fixtures quick-window, toutes `Blocked` / `strict-export-rejected` |

## 6. Préconditions avant d'exécuter la Phase 6

Toutes doivent être vraies. Aucune ne l'est aujourd'hui.

- [ ] Soak 24 h vert, verdict consigné dans l'audit de Phase 5.
- [ ] Checkpoint `-Phase 5` enregistré, les deux worktrees propres.
- [ ] Correctifs de `visualisation_import.js` et `scada_builder_composition.py` publiés au canary, conformance rejouée.
- [ ] Déploiement production exécuté sur autorisation distincte, avec smoke read-only et rollback disponible.
- [ ] Décision explicite sur `nesting.depth-2` (§3).
- [ ] Décision sur l'écart `FR-UI-03` (barre de titre vide), qui régénère la fixture de handshake gelée.

## 7. Ordre d'exécution proposé

1. Mettre à jour les onze (ou douze, selon la décision `depth-2`) entrées du catalogue de `Blocked` à `Supported`; laisser `binding.parent-port` et `legacy-fragment-adapter` bloquées.
2. Mettre à jour `expected-runtime-capabilities.json` et la fixture de capacités TF100Web correspondante.
3. Régénérer la matrice et passer le gate stale.
4. Bump `feature` vers `V2.1.6.0000`, itération remise à `0000`, synchroniser les documents propriétaires touchés.
5. Commit `feat: promote proven quick window capabilities`.
6. Ajouter les tests d'export de la Task 6.2 — sans modifier le code de production, sauf preuve contraire à ce moment-là.
7. Commit `feat: enable strict quick window export`.
