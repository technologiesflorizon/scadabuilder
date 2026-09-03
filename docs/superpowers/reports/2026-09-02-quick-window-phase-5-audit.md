# Audit d'implémentation Fenêtres rapides — Phase 5

Date: 2026-09-02
Status: EN COURS — 5.1, 5.2 et 5.4 conformes; 5.3 **non close** : le soak s'est arrêté à 18,37 h sur une mise en veille de la station; production non déployée; capacités toujours `Blocked`
Document version: `V2.1.5.0053`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-09-03 | `V2.1.5.0053` | `PENDING` | Verdict du soak : quatre critères de qualité verts sur 18,37 h, durée insuffisante, critère d'erreurs non mesuré. Trois défauts d'instrumentation trouvés par le run et corrigés. Le soak reste à refaire. |
| 2026-09-02 | `V2.1.5.0051` | `70d7e02` | Ouverture de l'audit de Phase 5 : 5.1, 5.2 et 5.4 conformes; 5.3 outillée et soak lancé; deux défauts réels trouvés et corrigés. La conclusion reste suspendue au verdict du soak. |

## 1. Portée et conclusion

**Pas de conclusion.** Ce rapport est ouvert pendant la phase, pas après elle. La politique de clôture exige un rapport d'audit *et* un checkpoint versionné, et aucun des deux ne peut être signé tant qu'un soak de 24 h n'a pas rendu son verdict. Celui du 2026-09-02 s'est arrêté à 18,37 h : ce qu'il a mesuré est consigné au §7, la phase reste ouverte.

Sur les quatre tâches:

- **5.1 et 5.2 conformes**, livrées avant cette session (TF100Web `20998ab`, `2562bcd`).
- **5.3 outillée, exécutée, non close.** La conformance cross-runtime, les races, les 100 cycles, les SLA du harnais, le canary et le rollback étaient déjà verts (`9304355`). Ce qui manquait n'était pas une décision mais un instrument : rien ne permettait d'exécuter un soak. L'instrument existe, il est testé, il a tourné 18,37 h — et il s'est arrêté avant la 24<sup>e</sup> heure sur une mise en veille de la station. Le détail est au §7.
- **5.4 close.** Les sept obligations de preuve sont satisfaites; deux d'entre elles ont échoué contre le code réel avant d'être satisfaites.

Aucune capacité n'est promue. Les treize identifiants `quick-window.*` restent `Blocked`, et le paquet de soak, comme celui de handshake, n'en déclare aucune.

**Le déploiement production n'est pas exécuté** et reste soumis à une autorisation distincte.

## 2. Preuves d'exécution

Exécutions des 2026-09-02 et 2026-09-03, Builder `2c3f451` (`codex/GestionFenetreRapide`) et TF100Web `b4edfc9` (`codex/quick-window-v1`).

| Vérification | Commande | Résultat |
| --- | --- | --- |
| Suite complète Builder | `dotnet test ScadaBuilderV2.sln` | 878/879, 1 inconclusive |
| Runtime JS Builder | `npm --prefix tests/runtime-js test` | 80/80 |
| Runtime JS TF100Web | `node --test frontend/tests_runtime_js/*.test.mjs` | 97/97 (87 + 10 de reprise de soak) |
| Soak, run du 2026-09-02 | `--rebuild artifacts/quick-window-soak/2026-09-02T16-59-43-926Z/samples.jsonl` | `FAIL` : 4 critères verts, durée 18,37 h, erreurs non mesurées |
| Suites Fenêtres rapides TF100Web | `pytest frontend/tests_scada_quick_window_*.py` | 37 + 11 subtests |
| Conformance cross-runtime | `manage.py test frontend.tests_runtime_conformance` | 14 OK, 1 skipped |
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
| Absence de croissance mémoire | **Vert** | tas médian **−19,3 %** entre premier et dernier quart (5,51 → 4,45 Mo), pente négative |
| Absence de fuite d'écouteurs | **Vert** | **231 → 165** écouteurs entre quarts, sur 3 256 échantillons après GC forcée |
| Pages 2.1/2.2/2.3 intactes | **Vert** | **5 818** vérifications, aucune page non composée |
| Absence d'erreur QuickWindow | **Non mesuré** | voir §7.2 |
| Durée | **Rouge** | 18,37 h sur 24 h |

Volumétrie : 90 541 enregistrements, aucun malformé, 81 410 cycles mesurés (hors 56 cycles de chauffe), **75 595 ouvertures acceptées** et **5 815 refus, tous `required-port-unbound`** — le refus que le paquet de soak est fait pour provoquer. Aucun autre code de refus n'apparaît de la première heure à la dernière, et le compteur de cadres orphelins est resté à zéro sur toute la durée.

La queue de latence, que le p95 masque et qui est le seul endroit où quelque chose bouge : p99,9 à 153 ms, p99,99 à 267 ms, et **deux ouvertures seulement au-dessus du plafond de 500 ms sur 75 595** (0,003 %). L'une à 705 ms, deux secondes avant la mise en veille — c'est la veille. L'autre à 2022 ms le 2026-09-02 à 22:17, isolée et inexpliquée : à surveiller au prochain run, pas à écarter. Les fermetures ne bougent pas du tout (p50 1 ms, p99 2 ms, max 41 ms).

Les deux critères de fuite ne sont pas seulement dans la tolérance, ils sont **négatifs** : le tas et les écouteurs décroissent entre le premier et le dernier quart. Ce n'est pas une fuite contenue, c'est un régime stable atteint après la chauffe.

### 7.2 Le critère d'erreurs n'a pas été mesuré, et n'est pas déclaré vert

Le pilote accumulait les erreurs console en mémoire et ne les écrivait qu'à la toute fin. Un run qui n'atteint pas sa fin les perd entièrement — c'est exactement ce qui s'est produit. Le flux contient donc 5 815 cycles refusés et **aucun enregistrement d'erreur**, ce qui se lirait naïvement comme « aucune erreur inattendue ».

Ce serait transformer une absence de preuve en assurance. Le reconstructeur refuse désormais cette lecture : un flux portant des refus sans le moindre enregistrement d'erreur fait échouer le critère avec la raison « non mesuré ». Le verdict du run est en conséquence `FAIL`, sur ce critère et sur la durée.

### 7.3 Trois défauts d'instrumentation, trouvés par le run et corrigés

Aucun ne touche le produit; tous trois auraient faussé ou détruit la preuve d'un nouveau run.

1. **Un run qui meurt n'écrivait aucun verdict.** Le résumé n'était produit qu'après la boucle. Reconstituer les 18 h a demandé un script jetable. Le harnais écrit désormais son résumé quoi qu'il arrive, marqué `completed: false` avec la cause; `--rebuild` refait le même calcul depuis un flux orphelin, pour le cas où le processus est tué net.
2. **Le chemin d'erreur ne fermait pas le navigateur.** Le `catch` de tête posait un code de sortie sans appeler `shutdown()`; la socket devtools et le processus fils gardaient la boucle d'événements vivante. Cinq heures après sa mort, le run tenait encore **seize processus Edge** et 917 Mo de profils de navigateur. Deux harnais zombies étaient dans cet état au moment du constat.
3. **Rien n'empêchait deux runs de se partager un navigateur.** Le port de débogage vaut 9333 par défaut et n'était jamais vérifié. Deux runs lancés le 2026-09-02 (12:25 et 12:59) ont pris le même port : Chromium n'échoue pas bruyamment dans ce cas, il se contente de ne pas écouter, et le second harnais a piloté le navigateur du premier. Le run de 12:25 s'est effondré en 98 `invocation-missing`, 10 `runtime-unavailable` et 8 `frame-never-appeared` qui ne devaient rien au runtime, puis s'est arrêté à la seconde près où le second a pris la main. Le harnais refuse maintenant de démarrer sur un port qu'un navigateur sert déjà.

Couverture : `frontend/tests_runtime_js/quick-window-soak-recovery.test.mjs`, 10 tests.

### 7.4 Deux angles morts de l'environnement, à corriger avant de relancer

- **La station peut se mettre en veille.** C'est la cause directe de l'arrêt. Un run de 24 h exige que la veille soit désactivée pour sa durée.
- **Le canary et l'alimentateur n'ont laissé aucun journal.** Lancés comme simples processus d'arrière-plan, sans unité systemd ni redirection, ils sont morts avec la veille et n'ont rien écrit. Impossible donc de recouper côté serveur les 18 h mesurées côté navigateur. Redis, lui, a survécu (WSL : 9 jours d'uptime), ce qui confirme le `vmIdleTimeout` mais ne dit rien du reste.

### 7.5 Limites actées du dispositif

Le soak tourne sur un canary WSL réel (`127.0.0.1:8010`, base `tf100_canary` dédiée, `STATIC_ROOT` distinct, paquet SHA `f0647722`, génération `ad35f17a`). Quatre choses qu'il ne prouvera pas, à lire avec son verdict :

- **Écriture non éprouvée.** Sans PLC, `StationMappingWriteView` échoue au driver; seul le chemin d'échec est exercé.
- **Aucune page 2.1/2.2/2.3 sur ce canary.** Le critère de non-régression sur les pages existantes est dégénéré : il prouve qu'une page se compose encore après 24 h, pas l'absence d'impact sur des pages historiques absentes.
- **Chemin clic → commande → intention hors couverture.** Le gate d'export refuse toute commande `OpenQuickWindow` tant que les capacités sont `Blocked`; aucune page appelante cliquable ne peut exister avant la Phase 6.
- **Cookies relâchés** pour du HTTP sur boucle locale, concession de transport orthogonale à l'endurance.

Le p95 d'ouverture chaude est désormais mesuré **dans un vrai navigateur** : 141 ms sur 75 590 ouvertures, sous le plafond de 500 ms de la Phase 0. C'est le chiffre qui manquait — le p95 antérieur venait du harnais, sans moteur de rendu.

## 8. Reste à faire avant la Phase 6

- [ ] **Refaire le soak sur 24 h pleines**, veille de la station désactivée, canary et alimentateur lancés avec journalisation. Le harnais corrigé écrira son verdict quoi qu'il arrive.
- [ ] `tools/quick-window/record-checkpoint.ps1 -Phase 5` une fois le soak vert et les deux worktrees propres.
- [ ] Publier au canary les corrections de `visualisation_import.js` et `scada_builder_composition.py`, délibérément non déployées pendant le soak, puis rejouer la conformance.
- [ ] Déploiement production, sur autorisation distincte, avec smoke read-only et possibilité de redéploiement immédiat du paquet known-good.
- [ ] Décider du sort de l'écart `FR-UI-03`.
