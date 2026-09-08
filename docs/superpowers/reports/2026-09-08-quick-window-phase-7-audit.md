# Audit d'implémentation Fenêtres rapides — Phase 7

Date: 2026-09-08
Status: **Phase 7 close.** Les trois tâches sont satisfaites. La Validation Checklist du plan est passée : 36 items verts, 2 satisfaits sur décision explicite et 1 gate industriel délibérément laissé ouvert (§6). Onze capacités `quick-window.*` sont `Supported`, deux restent `Blocked`.
Document version: `V2.1.6.0005`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-09-08 | `V2.1.6.0005` | `ec5f1e9` | Clôture de la Phase 7 : rapport d'audit et checkpoint versionné. Validation Checklist passée et consignée item par item. |

## 1. Portée et conclusion

**Les trois tâches sont closes.** La Phase 7 est la dernière du plan `2026-08-10-parameterized-quick-window-management.md`.

- **7.1 close, après avoir été bloquée par son propre audit.** Le premier point de la tâche impose d'auditer avant de créer. L'audit a conclu `BLOCKED` : l'inventaire opérateur de `win00054` est un contrôleur de moteur à quatre modes, mais le `TagCatalog` du projet de référence ne porte **aucune commande d'écriture de moteur** — ses onze mappings moteur sont des états de contacteur en lecture seule. Deux jeux complets et compatibles n'existaient donc pas. Sur décision, la verticale a été bâtie sur un catalogue de tags synthétique `scada-v2-win00054-synthetic-tags-v1`, et le projet de référence industriel n'a pas été touché.
- **7.2 close avec un gate ouvert.** L'acceptation est verte sur toutes les suites, le rejeu de la baseline Phase 0 ne dérive pas (0,0 %) et les SLA d'ouverture sont mesurés sur des ouvertures **réelles** dans Edge contre le canary, pas sur un harnais synthétique. Aucune écriture PLC réelle n'a été exécutée : le gate industriel reste ouvert et est nommé comme tel.
- **7.3 close.** Contrats, registre de décisions, couverture et diagrammes sont synchronisés sur l'état réel. Deux défauts ont été corrigés au passage (§5).

**Ce que la phase ne prouve pas** est aussi net que ce qu'elle prouve : aucune liaison à un automate réel, aucune écriture PLC nominale, et le canary porte encore le paquet de soak d'avant la promotion, dont les `RequiredCapabilities` sont vides.

## 2. Preuves d'exécution

Exécutions du 2026-09-08. Builder `codex/GestionFenetreRapide`, TF100Web `49701bb` (`codex/quick-window-v1`). Toutes les suites ci-dessous ont été rejouées ce jour, après les modifications de la Task 7.3.

| Vérification | Commande | Résultat |
| --- | --- | --- |
| Suite complète Builder | `dotnet test ScadaBuilderV2.sln` | **892/892**, aucun ignoré |
| Runtime JS Builder | `node --test tests/runtime-js/*.test.mjs` | **80/80** |
| Runtime JS TF100Web | `node --test frontend/tests_runtime_js/*.test.mjs` | **97/97** |
| Suites Fenêtres rapides TF100Web | `manage.py test frontend.tests_scada_quick_window_*` | **46/46**, aucun ignoré |
| Conformance cross-runtime | `manage.py test frontend.tests_runtime_conformance` | **14, 1 skip opt-in**; 126/126 sondes exécutées |
| Déploiement et composition TF100Web | `manage.py test frontend.tests_scada_deploy frontend.tests_scada_page_composition` | **41/41** |
| Gate documentaire | `tools/docs/verify-docs.ps1` | **Errors: 0** (121 warnings préexistants) |
| Dette de commit | `tools/docs/resolve-pending-commits.py --check` | **0 placeholder résolvable** |

Environnement : Node `v24.15.0` conforme à l'épinglage `24.15.x` de `DEC-0051` — **la même version que les rapports de Phase 0**, comme la checklist l'exige —, Python sous WSL Ubuntu, Windows 10.0.26200.

Deux résultats à ne pas lire plus largement qu'ils ne valent :

- Le skip TF100Web est `test_external_industrial_artifact_matches_evidence_and_production_gate`, opt-in, qui attend un chemin de paquet industriel externe.
- `frontend.tests_scada_package` reste à **79 tests, 5 échecs et 1 erreur**, exactement la baseline préexistante consignée dans l'entrée 28 de `KNOWN_GAPS_V2.md`. Ces échecs portent sur des attentes d'assets sans rapport avec ce chantier; ils sont inchangés par la Phase 7.

## 3. Tâches et preuves associées

| Tâche | Livré | Commit | Couverture |
| --- | --- | --- | --- |
| 7.1 Audit de mapping `win00054` | Verdict `BLOCKED` documenté avant toute création | `511621d` | `win00054-quick-window-mapping-audit.json` |
| 7.1 Verticale sur catalogue synthétique | Définition moteur, 8 membres, invocations `M101`/`M102` | `ac9003a` | `Win00054QuickWindowIntegrationTests` 11/11 |
| 7.2 Acceptation complète | Rejeu Phase 0, SLA réels, mémoire après cycles | `58b6018` | `win00054-quick-window-acceptance.json` |
| 7.3 Synchronisation documentaire | 6 contrats, registre, couverture, 3 diagrammes | `d5f9ab1` | `verify-docs` Errors: 0 |
| 7.3 Résolution de dette | 9 placeholders `PENDING` fermés | `8e2ff75` | `resolve-pending-commits --check` |

## 4. Mesures de la Task 7.2

Les SLA sont mesurés sur des ouvertures réelles dans Edge contre le canary WSL, et non sur un harnais sans moteur de rendu. Le plan demande 30 ouvertures froides et 100 chaudes; les runs réels en fournissent 104 et 77 973.

| Mesure | Valeur | Plafond | Verdict |
| --- | --- | --- | --- |
| Ouverture froide `p50` / `p95` / max | 43 / **144** / 199 ms | `p95 ≤ 1500 ms` | `PASS` |
| Ouverture chaude `p50` / `p95` / `p99` / max | 34 / **141** / 145 / 2022 ms | `p95 ≤ 500 ms` | `PASS` |
| Rejeu de la baseline Phase 0 | delta max **0,0 %** | `≤ 10 %` | `PASS`, hash de prototype inchangé |
| Mémoire après 3 256 relevés sur 18,37 h | plafond 5,94 Mo / 231 écouteurs | plafonné, moyennes par tiers plates | `PASS` |

Le maximum chaud de 2 022 ms est une queue, pas une tendance : le `p99` reste à 145 ms sur 77 973 ouvertures.

## 5. Défauts trouvés pendant la phase et corrigés

1. **Le bloc de vérification du plan ne vérifiait pas ce qu'il annonçait.** Ses trois commandes `rg` excluaient l'archive par `--glob '!09_archive/**'`, motif qui n'exclut rien quand le chemin cherché est `docs`. Le contrôle des décisions ouvertes remontait 11 correspondances alors qu'une seule est réelle — sa propre auto-référence —, ce qui aurait masqué une régression. Corrigé en `'!**/09_archive/**'`.
2. **Le relevé d'exécution du plan portait encore `Phase 7: non démarrée`** alors que 7.1 et 7.2 étaient closes.
3. **Six documents affirmaient au présent des faits devenus faux à la Phase 6** : trois compteurs de conformance périmés, deux affirmations que les treize capacités restaient `Blocked`, une clause interdisant toute capacité `quick-window.*` en `Required`, et le statut d'implémentation de `DEC-0050` figé à la Task 3.1. Détail dans le message de `d5f9ab1`.
4. **L'absence de `quick-window-host.js` du bundle exporté était attribuée à la mauvaise cause.** Le contrat l'expliquait par le statut `Blocked` des capacités; la vraie raison est la propriété du module — il appartient à TF100Web et ne figure dans aucun `RuntimeModuleOrder` du Builder. La promotion n'y change rien, et l'explication précédente aurait fait attendre son apparition après la Phase 6.
5. **Le dernier `PENDING` réel du dépôt**, sur `DEC-0045`, est résolu vers `0168f2f`, vérifié comme étant bien le commit de `V2.1.5.0002`.

## 6. Validation Checklist du plan

39 items. **36 verts, 2 satisfaits sur décision explicite, 1 gate délibérément ouvert.** Les items dont la preuve est portée par une classe de test nommée renvoient à `REGRESSION_COVERAGE_V2.md`; toutes ces suites sont vertes dans la baseline du §2.

**Verts sur preuve directe de cette session :**

- Matrice `FR-001..036` + `FR-UI-01..26` : **62 lignes pour 62 FR**, aucune cellule vide, aucune FR orpheline — la spec et l'Annexe A nomment exactement le même ensemble.
- `PresentationDefaults` porte exactement `Title`, `Position=Center`, `Backdrop`, `Chrome` borné, `IsDraggable=true`, `IsResizable=false`, `IsViewportConstrained=true`, et aucune autre propriété V1.
- Aucun `InstanceKey` n'existe : les treize occurrences du dépôt sont des **gardes contre lui** — validation structurelle et tests qui asserte son absence.
- `MainWindow.xaml.cs` ne porte que cinq lignes de câblage Fenêtre rapide, sous garde d'architecture.
- Node `v24.15.0`, identique entre `.nvmrc`, `engines.node`, le rapport de Phase 0 et celui-ci.
- Politique de version respectée : bumps `iteration` avant activation, bump `feature` exactement à la frontière de promotion de Phase 6 (`V2.1.5.0056` → `V2.1.6.0000`), aucun bump `production`.
- Chaque phase possède une entrée dans `tools/quick-window/checkpoints.json` versionné et un rapport sous `docs/superpowers/reports/`.
- Suites complètes vertes contre des baselines fraîches (§2).

**Satisfaits sur décision explicite, non sur mesure :**

- **Soak de 24 h.** Le run s'est arrêté à 18,37 h, une mise en veille de la station l'ayant tué. Cette durée a été **acceptée en l'état sur décision du 2026-09-03** plutôt que refaite. Les quatre critères de qualité sont verts sur la durée mesurée; ce que le run n'a pas prouvé est nommé au §7 de l'audit de Phase 5.
- **Déploiement en production avant promotion.** La mise en service en site industriel est reportée à la fin du projet. Ce qui tient lieu de preuve de « déploiement capable » est le déploiement en environnement contrôlé, décidé et documenté au §8 de l'audit de Phase 5.

**Gate délibérément ouvert :**

- **Écriture PLC réelle.** Aucune écriture ni readback n'a été exécutée; faute d'automate, seul le chemin d'échec est exercé. La checklist prévoit exactement ce cas : « sinon le gate industriel reste ouvert ». Il l'est. La livraison n'est pas présentée comme validée en production.

## 7. Écarts connus non corrigés

1. ~~**`FR-UI-03` — barre de titre vide dans le runtime déployé.**~~ **Corrigé le 2026-09-08**, après clôture de la phase. Le compilateur applique désormais `EffectiveTitle(displayName)`, comme il le faisait déjà pour la scène. Ce que l'écart apprend sur le dispositif de preuve mérite d'être retenu : **aucun gate ne pouvait le voir**, les deux définitions de la fabrique de conformance portant un `Title` explicite, donc la branche de repli n'était compilée par aucun artefact gelé. Le paquet de soak l'exerçait pourtant : ses quatre définitions ont tourné 18,37 h sur le canary sans aucun titre, sans que rien ne l'assert. Deux tests couvrent maintenant les deux branches; les fixtures de handshake et de soak sont régénérées et re-vendorisées.
2. **Le canary porte un paquet d'avant la promotion.** Le `.sb2` déployé est celui du soak, exporté avant la Phase 6 : ses `RequiredCapabilities` sont vides. Rien n'a donc encore fait passer un paquet **déclarant les onze capacités promues** par la négociation de capacités de TF100Web dans un déploiement vivant. Le refus fail-closed est prouvé par test, l'acceptation ne l'est que par test.
3. **`quick-window.binding.parent-port` et `quick-window.legacy-fragment-adapter` restent `Blocked`**, pour des raisons différentes : la première est implémentée côté Builder et runtime partagé mais aucun paquet déployé ne l'exerce, donc elle n'a pas de preuve host; la seconde n'a aucune implémentation nulle part.
4. **Cinq échecs et une erreur préexistants** dans `frontend.tests_scada_package`, sans rapport avec ce chantier et à traiter pour eux-mêmes.

## 8. Conclusion

La Phase 7 est close et, avec elle, le plan `2026-08-10-parameterized-quick-window-management.md`. Les Fenêtres rapides paramétrées existent comme entités typées distinctes, de l'authoring WPF jusqu'au montage dans TF100Web, avec onze capacités promues sur preuve exécutable aux trois couches et un export strict 2.3 ouvert pour elles seules.

Ce qui reste ouvert est nommé au §7 et n'est pas présenté comme fait : l'écriture PLC réelle, la mise en service industrielle, un déploiement vivant portant un paquet post-promotion, et le repli de titre `FR-UI-03`.
