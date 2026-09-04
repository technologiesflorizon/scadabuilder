# Audit d'implémentation Fenêtres rapides — Phase 5

Date: 2026-09-02
Status: **Phase 5 close.** Les quatre tâches sont satisfaites; le déploiement en site industriel est reporté à la fin du projet et le déploiement en environnement contrôlé tient lieu de preuve de capacité (§8). Capacités toujours `Blocked` — leur promotion est l'objet de la Phase 6.
Document version: `V2.1.5.0056`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-09-03 | `V2.1.5.0056` | `5716000` | Phase 5 close : le déploiement en site industriel est reporté à la fin du projet, le déploiement contrôlé tient lieu de preuve de « déploiement capable » au sens du plan. Checkpoint de Phase 5 enregistré. |
| 2026-09-03 | `V2.1.5.0055` | `63605dc` | Critère d'erreurs console mesuré par capture dédiée (186 refus attendus, zéro inattendu); correctifs de la Task 5.4 publiés au canary et vérifiés en place; conformance rejouée. Correction : les critères de fuite sont plats et plafonnés, pas décroissants — la lecture précédente prenait une phase d'oscillation pour une tendance. |
| 2026-09-03 | `V2.1.5.0054` | `82ea206` | Acceptation explicite du soak de 18,37 h en lieu et place des 24 h du runbook; la Task 5.3 est close sur cette décision. La preuve mesurée n'est pas modifiée. |
| 2026-09-03 | `V2.1.5.0053` | `8e61306` | Verdict du soak : quatre critères de qualité verts sur 18,37 h, durée insuffisante, critère d'erreurs non mesuré. Trois défauts d'instrumentation trouvés par le run et corrigés. Le soak reste à refaire. |
| 2026-09-02 | `V2.1.5.0051` | `70d7e02` | Ouverture de l'audit de Phase 5 : 5.1, 5.2 et 5.4 conformes; 5.3 outillée et soak lancé; deux défauts réels trouvés et corrigés. La conclusion reste suspendue au verdict du soak. |

## 1. Portée et conclusion

**Les quatre tâches sont closes.** La dernière l'est sur une décision explicite : le soak s'est arrêté à 18,37 h des 24 h du runbook, et cette durée a été acceptée telle quelle plutôt que refaite (§7.6). Ce que le run a mesuré est consigné au §7; ce qu'il n'a pas mesuré y est nommé aussi.

Sur les quatre tâches:

- **5.1 et 5.2 conformes**, livrées avant cette session (TF100Web `20998ab`, `2562bcd`).
- **5.3 close sur décision.** La conformance cross-runtime, les races, les 100 cycles, les SLA du harnais, le canary et le rollback étaient déjà verts (`9304355`). Ce qui manquait n'était pas une décision mais un instrument : rien ne permettait d'exécuter un soak. L'instrument existe, il est testé, il a tourné 18,37 h avant qu'une mise en veille de la station ne l'arrête. Cette durée a été acceptée en l'état (§7.6).
- **5.4 close.** Les sept obligations de preuve sont satisfaites; deux d'entre elles ont échoué contre le code réel avant d'être satisfaites.

Aucune capacité n'est promue. Les treize identifiants `quick-window.*` restent `Blocked`, et le paquet de soak, comme celui de handshake, n'en déclare aucune.

**Le déploiement en site industriel n'est pas exécuté** : il est reporté à la fin du projet et gardera son autorisation propre. Ce que le plan exige à ce stade est la preuve d'un *déploiement capable*, et c'est le déploiement en environnement contrôlé qui la porte (§8).

## 2. Preuves d'exécution

Exécutions des 2026-09-02 et 2026-09-03, Builder `codex/GestionFenetreRapide` et TF100Web `b4edfc9` (`codex/quick-window-v1`). Les suites ci-dessous ont toutes été rejouées le 2026-09-03.

| Vérification | Commande | Résultat |
| --- | --- | --- |
| Suite complète Builder | `dotnet test ScadaBuilderV2.sln` | 878/879, 1 inconclusive |
| Runtime JS Builder | `npm --prefix tests/runtime-js test` | 80/80 |
| Runtime JS TF100Web | `node --test frontend/tests_runtime_js/*.test.mjs` | 97/97 (87 + 10 de reprise de soak) |
| Soak, run du 2026-09-02 | `--rebuild artifacts/quick-window-soak/2026-09-02T16-59-43-926Z/samples.jsonl` | `FAIL` : 4 critères verts, durée 18,37 h, erreurs non mesurées |
| Suites Fenêtres rapides TF100Web | `manage.py test frontend.tests_scada_quick_window_*` | 41, 1 skip opt-in, OK |
| Conformance cross-runtime | `manage.py test frontend.tests_runtime_conformance` | 14 OK, 1 skipped — rejouée après publication des correctifs |
| Capture des erreurs console | `--rebuild artifacts/quick-window-errorcapture/2026-09-03T20-54-32-015Z/samples.jsonl` | 186 refus attendus, **zéro inattendu** |
| Fixture gelée, SHA croisé | `sha256sum` des deux dépôts | `ea82aef5…` identique |
| Gate documentaire | `tools/docs/verify-docs.ps1` | Errors: 0 (121 warnings préexistants) |
| Résolution du checkpoint | `Resolve-WorktreeForBranch` sur les deux dépôts | Builder et worktree TF100Web trouvés, branche inconnue refusée |

Environnement : Node `v24.15.0` conforme à l'épinglage `24.15.x` (`DEC-0051`), Python 3.11 sous WSL, Windows 10.0.26200, Edge `152.0.4191.53`.

Deux précisions sur les résultats ci-dessus, pour qu'ils ne soient pas lus plus largement qu'ils ne valent :

- L'inconclusive Builder est `TheVendoredTf100WebFixtureIsTheSameArtifact`, qui compare vers `F:\Projet\Git\TF100Web`. Ce checkout est sur un autre chantier; la comparaison a été refaite à la main et les SHA concordent.
- Le skip TF100Web est `test_external_industrial_artifact_matches_evidence_and_production_gate`, opt-in, qui attend un chemin de paquet industriel externe.

Cinq échecs et une erreur préexistent dans `tests_scada_package` et `tests_scada_performance` sur cette branche. Vérifié en remisant les modifications de cette session : ils sont **identiques avec et sans elles**.

## 3. Tâches et preuves associées

| Tâche | Livré | Commit | Couverture |
| --- | --- | --- | --- |
| 5.1 Registres 2.3 validés et ingérés | Validation et ingestion fail-closed | `20998ab` | Antérieure |
| 5.2 Host TF100Web et SinglePerDefinition | Adaptateur host, gestionnaire, service du fragment | `2562bcd` | Antérieure |
| 5.3 Conformance, canary, rollback | Harnais cross-runtime, drills canary et rollback | `9304355` | Antérieure |
| 5.3 Outillage du soak | Paquet de charge élargi | Builder `49b6d71` | 5 tests du générateur |
| 5.3 Outillage du soak | Alimentateur Redis, harnais, verdict, canary | `ef3ecde` | 13 tests de verdict, 13 du feeder, 1 fumée CDP |
| 5.4 Composition et isolation legacy | Preuves + garde sur le registre de pages | `744075d` | 14 tests Python, 10 tests DOM |
| 5.4 Refus de traversée, sens 2 | Prédicat `isComposedPage` | `70d7e02` | 4 tests d'adaptateur |
| 5.3 Reprise du soak | Verdict écrit quoi qu'il arrive, erreurs diffusées, port de débogage gardé | `b4edfc9` | 10 tests de reprise |

## 4. Invariants vérifiés

- **Aucune capacité promue.** Les treize `quick-window.*` restent `Blocked`; le paquet de soak déclare `RequiredCapabilities: []`.
- **Fixture de handshake gelée.** Un test du générateur de soak l'asserte explicitement : ajouter une charge d'endurance ne peut pas devenir une raison de régénérer l'artefact dont dépend la chaîne de Phase 0.
- **Un mapping, un seul type.** Un invariant du générateur refuse deux invocations liant le même mapping à des membres de types différents.
- **Registre plutôt que système de fichiers.** Ni une page ni un fragment n'est servi parce qu'il existe sur disque; les deux portes vérifient le registre déployé.
- **Bandes de z-order distinctes et ordonnées** : legacy `10000` < backdrop `10500` < cadre `10600`, assertées contre le CSS et le JS livrés.
- **Fail-closed sur l'origine.** Un adaptateur incapable de vérifier l'origine d'une intention la refuse.

## 5. Écarts relevés et traités pendant la phase

**Chemin Fragment de substitution (corrigé, `744075d`).** `load_composed_page` résolvait tout identifiant que le système de fichiers pouvait satisfaire, et les fenêtres rapides déploient leurs fragments sous le même arbre `pages/`. `/visualisation/scada/page/qw-50000001/` renvoyait **200** et servait un fragment comme corps de page : hors du cadre du host, sans backdrop, sans piège de focus, sans cycle de vie et sans invocation pour lier ses ports. Confirmé sur le canary avant correction. C'est le chemin que l'architecture interdit explicitement.

**Traversée popup legacy → Fenêtre rapide (corrigé, `70d7e02`).** `isPageMounted` accepte délibérément une page montée en popup legacy. Le repli `postMessage` étant de même fenêtre et même origine, une enveloppe `openQuickWindow` portant le `sourcePageId` du popup franchissait le garde et atteignait le host. La fenêtre ainsi ouverte aurait survécu au popup qui l'a demandée.

**Commutateur de configuration mort hors municipal (corrigé, `81fe254`).** Sans rapport avec les Fenêtres rapides, mais rencontré en montant le canary : `visualisation_custom.js` revendiquait la propriété des interrupteurs avant son propre garde-fou, si bien qu'aucune des deux couches ne câblait `#configSwitch` sur une station SCADA Builder.

**Enregistreur de checkpoint pointant le mauvais dépôt (corrigé).** `record-checkpoint.ps1` lisait `F:\Projet\Git\TF100Web`, checkout aujourd'hui sur un autre chantier, pendant que le travail vit dans un worktree. Un checkpoint de Phase 5 aurait consigné un HEAD étranger, silencieusement, dans le fichier qui sert précisément de preuve de rollback. Le script résout désormais le worktree portant la branche attendue, et refuse plutôt que de deviner.

## 6. Écart constaté et non traité

**Titre de fenêtre rapide vide (`FR-UI-03`).** Le manifeste compilé porte `PresentationDefaults.Title: null`, et le host rend `intent.title || presentation.Title || ""`. `QuickWindowPresentationDefaults.EffectiveTitle(displayName)` implémente pourtant le repli sur `DisplayName`, mais seul l'aperçu Builder l'utilise. L'aperçu affiche « Pompe » là où le runtime déployé n'affiche rien, et aucun test ne l'assertait. Non corrigé : la correction touche le compilateur et régénérerait la fixture de handshake gelée, ce qui relève d'une décision délibérée.

## 7. Soak — résultat

Le run a démarré le 2026-09-02 à 12:59:52 et s'est arrêté le 2026-09-03 à 07:21:58, soit **18,37 h des 24 h exigées**. Il n'a pas été interrompu par le produit : la station est passée en veille (`Kernel-Power 42`, motif « Button or Lid ») à 07:22:08, dix secondes après le dernier cycle enregistré. L'évaluation CDP en vol a expiré, et le harnais est mort avec elle.

**Le soak est donc à refaire.** Ce qu'il a mesuré reste valable et vaut d'être lu.

### 7.1 Verdict sur les 18,37 h mesurées

Reconstruit depuis le flux d'échantillons (`--rebuild`), écrit dans `artifacts/quick-window-soak/2026-09-02T16-59-43-926Z/summary.json` :

| Critère | Résultat | Mesure |
| --- | --- | --- |
| Latence d'ouverture chaude | **Vert** | p95 **141 ms** sur 75 590 ouvertures, plafond 500 ms; p50 34 ms, p99 144 ms, max 2022 ms |
| Absence de croissance mémoire | **Vert** | tas **plafonné à 5,94 Mo** sur 3 256 relevés après GC forcée; moyennes par tiers 4,99 / 4,97 / 4,63 Mo |
| Absence de fuite d'écouteurs | **Vert** | écouteurs **jamais au-dessus de 231**; moyennes par tiers 195,8 / 194,7 / 179,3 |
| Pages 2.1/2.2/2.3 intactes | **Vert** | **5 818** vérifications, aucune page non composée |
| Absence d'erreur QuickWindow | **Vert, mesuré séparément** | non capturé pendant ce run (§7.2), mesuré le 2026-09-03 sur le même canary et la même charge (§7.7) |
| Durée | **Rouge** | 18,37 h sur 24 h |

Volumétrie : 90 541 enregistrements, aucun malformé, 81 410 cycles mesurés (hors 56 cycles de chauffe), **75 595 ouvertures acceptées** et **5 815 refus, tous `required-port-unbound`** — le refus que le paquet de soak est fait pour provoquer. Aucun autre code de refus n'apparaît de la première heure à la dernière, et le compteur de cadres orphelins est resté à zéro sur toute la durée.

La queue de latence, que le p95 masque et qui est le seul endroit où quelque chose bouge : p99,9 à 153 ms, p99,99 à 267 ms, et **deux ouvertures seulement au-dessus du plafond de 500 ms sur 75 595** (0,003 %). L'une à 705 ms, deux secondes avant la mise en veille — c'est la veille. L'autre à 2022 ms le 2026-09-02 à 22:17, isolée et inexpliquée : à surveiller au prochain run, pas à écarter. Les fermetures ne bougent pas du tout (p50 1 ms, p99 2 ms, max 41 ms).

**Sur les deux critères de fuite, la formulation demande une précision** — le premier jet de ce rapport parlait de « décroissance », ce que les données ne soutiennent pas. Le comparateur de quarts a rendu −19,3 % de tas et −66 écouteurs, mais ces deux chiffres sont des artefacts de phase, pas des tendances.

Les écouteurs ne varient pas continûment : ils ne prennent que des **multiples de 33** — 132 (890 relevés), 165 (629), 198 (99), 231 (1 632) — parce que chaque cadre vivant en installe trente-trois. La série oscille donc entre quatre paliers au gré de la page courante et du nombre de cadres vivants à l'instant du prélèvement, et comparer deux quarts revient à comparer deux phases de cette oscillation. Le tas se comporte de la même façon, par marches d'environ 0,5 Mo.

Ce que les données disent réellement est plus fort qu'une décroissance : sur 18,37 heures et 3 256 relevés après collecte forcée, **les écouteurs n'ont jamais dépassé 231 et le tas jamais 5,94 Mo**. Les moyennes par tiers sont plates (195,8 / 194,7 / 179,3 écouteurs; 4,99 / 4,97 / 4,63 Mo). Une fuite ne se cache pas sous un plafond tenu dix-huit heures durant.

**Limite de l'arithmétique du verdict, à connaître avant de lire un run court.** `headTailMedians` compare le premier et le dernier quart, ce qui suppose une série dominée par sa tendance. Sur une série oscillante, un run assez long moyenne la phase — 3 256 relevés le font — mais un run court ne le fait pas : la capture de 38 min du §7.7 rend « +40,3 % de tas » et « +66 écouteurs » sur exactement le même déploiement et la même charge, sans qu'il ne se passe rien d'autre qu'un décalage de phase. Les seuils n'ont pas été modifiés — ils sont ceux sur lesquels le verdict accepté repose — mais un critère de plafond serait plus robuste qu'une comparaison de quarts, et c'est à considérer avant le prochain run long.

### 7.2 Le critère d'erreurs n'a pas été mesuré par ce run

Le pilote accumulait les erreurs console en mémoire et ne les écrivait qu'à la toute fin. Un run qui n'atteint pas sa fin les perd entièrement — c'est exactement ce qui s'est produit. Le flux contient donc 5 815 cycles refusés et **aucun enregistrement d'erreur**, ce qui se lirait naïvement comme « aucune erreur inattendue ».

Ce serait transformer une absence de preuve en assurance. Le reconstructeur refuse désormais cette lecture : un flux portant des refus sans le moindre enregistrement d'erreur fait échouer le critère avec la raison « non mesuré ». Le verdict du run reste donc `FAIL` sur ce critère et sur la durée, et n'a pas été retouché.

Le critère lui-même a été mesuré séparément le 2026-09-03 (§7.7). Cela ne mesure pas rétroactivement les 18,37 heures — rien ne le peut — mais mesure la même charge sur le même déploiement.

### 7.3 Trois défauts d'instrumentation, trouvés par le run et corrigés

Aucun ne touche le produit; tous trois auraient faussé ou détruit la preuve d'un nouveau run.

1. **Un run qui meurt n'écrivait aucun verdict.** Le résumé n'était produit qu'après la boucle. Reconstituer les 18 h a demandé un script jetable. Le harnais écrit désormais son résumé quoi qu'il arrive, marqué `completed: false` avec la cause; `--rebuild` refait le même calcul depuis un flux orphelin, pour le cas où le processus est tué net.
2. **Le chemin d'erreur ne fermait pas le navigateur.** Le `catch` de tête posait un code de sortie sans appeler `shutdown()`; la socket devtools et le processus fils gardaient la boucle d'événements vivante. Cinq heures après sa mort, le run tenait encore **seize processus Edge** et 917 Mo de profils de navigateur. Deux harnais zombies étaient dans cet état au moment du constat.
3. **Rien n'empêchait deux runs de se partager un navigateur.** Le port de débogage vaut 9333 par défaut et n'était jamais vérifié. Deux runs lancés le 2026-09-02 (12:25 et 12:59) ont pris le même port : Chromium n'échoue pas bruyamment dans ce cas, il se contente de ne pas écouter, et le second harnais a piloté le navigateur du premier. Le run de 12:25 s'est effondré en 98 `invocation-missing`, 10 `runtime-unavailable` et 8 `frame-never-appeared` qui ne devaient rien au runtime, puis s'est arrêté à la seconde près où le second a pris la main. Le harnais refuse maintenant de démarrer sur un port qu'un navigateur sert déjà.

Couverture : `frontend/tests_runtime_js/quick-window-soak-recovery.test.mjs`, 10 tests.

### 7.4 Deux angles morts de l'environnement, à corriger avant tout run long ultérieur

- **La station peut se mettre en veille.** C'est la cause directe de l'arrêt. Un run de 24 h exige que la veille soit désactivée pour sa durée.
- **Le canary et l'alimentateur n'ont laissé aucun journal.** Lancés comme simples processus d'arrière-plan, sans unité systemd ni redirection, ils sont morts avec la veille et n'ont rien écrit. Impossible donc de recouper côté serveur les 18 h mesurées côté navigateur. Redis, lui, a survécu (WSL : 9 jours d'uptime), ce qui confirme le `vmIdleTimeout` mais ne dit rien du reste.

### 7.5 Limites actées du dispositif

Le soak tourne sur un canary WSL réel (`127.0.0.1:8010`, base `tf100_canary` dédiée, `STATIC_ROOT` distinct, paquet SHA `f0647722`, génération `ad35f17a`). *Le paquet de soak a été régénéré le 2026-09-04 par la Phase 6 — `OwnerPageKey` a quitté le manifeste — et vaut désormais `513e01e3`. Le run consigné ici a bien tourné contre `f0647722`; c'est ce SHA qui fait foi pour lui.* Quatre choses qu'il ne prouvera pas, à lire avec son verdict :

- **Écriture non éprouvée.** Sans PLC, `StationMappingWriteView` échoue au driver; seul le chemin d'échec est exercé.
- **Aucune page 2.1/2.2/2.3 sur ce canary.** Le critère de non-régression sur les pages existantes est dégénéré : il prouve qu'une page se compose encore après 24 h, pas l'absence d'impact sur des pages historiques absentes.
- **Chemin clic → commande → intention hors couverture.** Le gate d'export refuse toute commande `OpenQuickWindow` tant que les capacités sont `Blocked`; aucune page appelante cliquable ne peut exister avant la Phase 6.
- **Cookies relâchés** pour du HTTP sur boucle locale, concession de transport orthogonale à l'endurance.

Le p95 d'ouverture chaude est désormais mesuré **dans un vrai navigateur** : 141 ms sur 75 590 ouvertures, sous le plafond de 500 ms de la Phase 0. C'est le chiffre qui manquait — le p95 antérieur venait du harnais, sans moteur de rendu.

### 7.6 Décision : le run de 18,37 h est accepté en lieu et place des 24 h

**Décidé le 2026-09-03 par le propriétaire du projet.** Le soak n'est pas refait; le run du 2026-09-02 vaut soak de Phase 5. La preuve mesurée n'est pas retouchée pour autant : `summary.json` reste `FAIL` avec ses deux motifs, parce qu'il enregistre ce qui a été mesuré, pas ce qui a été décidé. L'acceptation est ici, dans le rapport, datée et attribuée.

Ce que la décision accepte sciemment :

- **5,63 h de moins que le runbook.** Aucune mesure ne couvre les heures 19 à 24.
- **Le critère d'erreurs console reste non mesuré.** Il n'est pas requalifié en vert.

Ce qui la rend défendable, et qui doit être lu avec elle :

- **75 595 ouvertures et 81 410 cycles** sur plus de dix-huit heures continues, soit un volume qui dépasse largement l'objet du soak — les 100 cycles du critère de fuite antérieur tiennent dans les quatre premières minutes.
- **Les deux critères de fuite sont plafonnés, pas seulement dans la tolérance** : sur 3 256 relevés après collecte forcée, les écouteurs n'ont jamais dépassé 231 et le tas jamais 5,94 Mo, moyennes par tiers plates. Un plafond tenu dix-huit heures ne se met pas à céder à la dix-neuvième.
- **La perte des erreurs console a depuis été comblée par une mesure directe** (§7.7), et n'était de toute façon pas un angle mort total. Le flux de cycles enregistre le code de refus rendu par le runtime à chaque ouverture : sur 81 410 cycles, **un seul code apparaît**, `required-port-unbound`, celui que le paquet provoque exprès. Aucun `invocation-missing`, `frame-never-appeared`, `runtime-unavailable`, `depth-exceeded` ni `cycle-rejected` inattendu. À quoi s'ajoutent **zéro instance acceptée non refermée** et **zéro cadre orphelin** sur 3 256 relevés. Une défaillance silencieuse aurait dû franchir ces trois filtres à la fois.
- Le run de 12:25, lui, montre à quoi ressemble une exécution perturbée : 98 `invocation-missing`, 10 `runtime-unavailable`, 8 `frame-never-appeared`. Le contraste est net, et c'est ce contraste qui rend l'uniformité du run de 12:59 significative.

Ce qui reste ouvert malgré la décision : le critère d'erreurs console pourra être clos à peu de frais par une capture courte avec le harnais corrigé, sans refaire vingt-quatre heures. Ce n'est pas une précondition de la Phase 6.

### 7.7 Capture des erreurs console du 2026-09-03

Le critère laissé non mesuré au §7.2 a été mesuré par une capture dédiée, sur le canary et la charge du soak. Ce n'est pas une reprise du soak et cela ne rejuge pas ses 18,37 heures : c'est la mesure de ce que le runtime écrit en console sous cette charge.

Conditions : canary `127.0.0.1:8010`, paquet `f0647722`, génération `ad35f17a` — inchangés — mais **avec les deux correctifs de la Task 5.4 désormais publiés**, ce qui n'était pas le cas pendant le soak. Le déploiement est donc strictement plus contraint, pas moins. Port de débogage 9361, harnais corrigé (`b4edfc9`).

Résultat sur 38 min, du 20:54:42Z au 21:32:35Z :

| Critère | Résultat | Mesure |
| --- | --- | --- |
| **Absence d'erreur QuickWindow** | **Vert** | **186 enregistrements d'erreur, tous des refus attendus, zéro inattendu** |
| Latence d'ouverture chaude | Vert | p95 155 ms sur 2 560 cycles |
| Pages 2.1/2.2/2.3 intactes | Vert | 186 vérifications, aucun échec |
| Critères de fuite | Non concluants sur 38 min | artefact de phase, voir §7.1 |

Les 186 enregistrements portent tous le même texte, `SCADA quick-window required-port-unbound`, sur l'invocation que le paquet dote délibérément d'un port requis non lié. Aucun autre message n'a été émis en trente-huit minutes.

**La chaîne de reprise a fait sa preuve sur un cas réel.** Le run a été tué net avant son échéance; il n'a donc écrit aucun résumé, exactement le scénario du §7.3. `--rebuild` a reconstruit le verdict depuis les 3 091 enregistrements du flux, sans perte et sans ligne malformée. Le processus tué n'a laissé derrière lui ni harnais orphelin ni processus Edge.

Journalisation côté serveur, angle mort du §7.4 désormais fermé : `artifacts/canary-django.log` ne porte aucune erreur sur une route Fenêtre rapide. Les seuls `500` sont les 103 appels à `api_network/lan/pending`, endpoint réseau sans rapport et absent de ce canary minimal; les seuls `404` sont deux `favicon.ico`.

### 7.8 Correctifs de la Task 5.4 publiés au canary et vérifiés en place

Publiés puis vérifiés contre le déploiement réel, non contre un test :

| Appel sur `/srv/tf100-canary/static` | Résultat |
| --- | --- |
| `load_composed_page("win00054")` | composée |
| `load_composed_page("qw-50000001")` | **refusée** — renvoyait `200` et servait un fragment avant le correctif |
| `load_composed_page("../../etc")` | refusée |
| `load_quick_window_fragment("qw-50000001")` | servie |
| `load_quick_window_fragment("win00054")` | refusée |

Conformance cross-runtime rejouée après publication : 14 tests, 1 skip opt-in, OK.

## 8. Décision : ce qui tient lieu de preuve de déploiement capable

**Décidé le 2026-09-03 par le propriétaire du projet.** Le déploiement en site industriel ne peut pas avoir lieu tant que le projet n'est pas terminé; il est reporté à cette échéance et gardera son autorisation propre. Le déploiement exécuté en environnement contrôlé tient lieu de preuve.

Ce que le plan demande à ce stade, dans ses propres termes, est « preuve du **déploiement capable** » — pas la preuve d'une mise en service. La distinction est portée par le texte, pas par la décision.

Ce que le déploiement contrôlé partage avec une mise en production, et qui fait la preuve :

- **Le même chemin de code.** `deploy_scada_builder` lit `settings.STATIC_ROOT`; pointer cette valeur sur un répertoire de canary est *toute* la différence. Aucune branche de déploiement spécifique n'est empruntée.
- **Le même paquet**, `f0647722`, déployé à la génération `ad35f17a`, servi par les mêmes vues de composition et de fragment que la production.
- **`TF100_DEPLOYMENT_PROFILE = "industrial"`**, donc les mêmes règles d'authentification et de profil qu'un site réel.
- **Les mêmes dépendances réelles** : MySQL, Redis, un navigateur réel, et le préfixe de proxy inverse émulé à l'identique.
- **Le rollback éprouvé** : redéploiement atomique du paquet known-good, vérifié, avec temps de restauration mesuré.

Ce que la décision n'achète pas, et qui reste vrai — les limites du §7.5 tiennent telles quelles :

- **Aucun automate.** Le chemin d'écriture n'est exercé que par son échec.
- **Aucune page 2.1/2.2/2.3 historique** sur ce canary : le critère de non-régression y est dégénéré.
- **Cookies relâchés** pour du HTTP sur boucle locale.
- **Aucun opérateur réel**, aucune charge d'exploitation.

Ces quatre points sont ceux que la mise en service devra lever, et ils sont reportés avec elle.

## 9. Reste à faire avant la Phase 6

- [x] ~~Soak 24 h~~ — clos par la décision du §7.6 : le run de 18,37 h est accepté en l'état, sans reprise.
- [ ] `tools/quick-window/record-checkpoint.ps1 -Phase 5`, les deux worktrees propres.
- [x] ~~Publier au canary les corrections de `visualisation_import.js` et `scada_builder_composition.py`, puis rejouer la conformance.~~ Fait le 2026-09-03, vérifié contre le déploiement réel (§7.8).
- [x] ~~Déploiement production~~ — reporté à la fin du projet par la décision du §8; le déploiement contrôlé porte la preuve de capacité. La mise en service gardera son autorisation propre, avec smoke read-only et redéploiement immédiat du paquet known-good.
- [ ] Décider du sort de l'écart `FR-UI-03`.
