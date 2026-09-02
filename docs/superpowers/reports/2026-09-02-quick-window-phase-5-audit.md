# Audit d'implémentation Fenêtres rapides — Phase 5

Date: 2026-09-02
Status: EN COURS — 5.1, 5.2 et 5.4 conformes; 5.3 outillée et soak 24 h en cours; production non déployée; capacités toujours `Blocked`
Document version: `V2.1.5.0051`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-09-02 | `V2.1.5.0051` | `70d7e02` | Ouverture de l'audit de Phase 5 : 5.1, 5.2 et 5.4 conformes; 5.3 outillée et soak lancé; deux défauts réels trouvés et corrigés. La conclusion reste suspendue au verdict du soak. |

## 1. Portée et conclusion

**Pas de conclusion.** Ce rapport est ouvert pendant la phase, pas après elle. La politique de clôture exige un rapport d'audit *et* un checkpoint versionné, et aucun des deux ne peut être signé tant que le soak de 24 h n'a pas rendu son verdict. L'écrire maintenant fige ce qui est établi pendant que les preuves sont fraîches; il sera complété demain.

Sur les quatre tâches:

- **5.1 et 5.2 conformes**, livrées avant cette session (TF100Web `20998ab`, `2562bcd`).
- **5.3 outillée et en cours.** La conformance cross-runtime, les races, les 100 cycles, les SLA du harnais, le canary et le rollback étaient déjà verts (`9304355`). Ce qui manquait n'était pas une décision mais un instrument : rien ne permettait d'exécuter un soak. L'instrument existe désormais, il est testé, et le soak tourne depuis le 2026-09-02 12:59.
- **5.4 close.** Les sept obligations de preuve sont satisfaites; deux d'entre elles ont échoué contre le code réel avant d'être satisfaites.

Aucune capacité n'est promue. Les treize identifiants `quick-window.*` restent `Blocked`, et le paquet de soak, comme celui de handshake, n'en déclare aucune.

**Le déploiement production n'est pas exécuté** et reste soumis à une autorisation distincte.

## 2. Preuves d'exécution

Exécutions du 2026-09-02, Builder `2c3f451` (`codex/GestionFenetreRapide`) et TF100Web `70d7e02` (`codex/quick-window-v1`).

| Vérification | Commande | Résultat |
| --- | --- | --- |
| Suite complète Builder | `dotnet test ScadaBuilderV2.sln` | 878/879, 1 inconclusive |
| Runtime JS Builder | `npm --prefix tests/runtime-js test` | 80/80 |
| Runtime JS TF100Web | `node --test frontend/tests_runtime_js/*.test.mjs` | 87/87 |
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

## 7. Soak en cours — limites actées

Le soak tourne sur un canary WSL réel (`127.0.0.1:8010`, base `tf100_canary` dédiée, `STATIC_ROOT` distinct, paquet SHA `f0647722`, génération `ad35f17a`). Quatre choses qu'il ne prouvera pas, à lire avec son verdict :

- **Écriture non éprouvée.** Sans PLC, `StationMappingWriteView` échoue au driver; seul le chemin d'échec est exercé.
- **Aucune page 2.1/2.2/2.3 sur ce canary.** Le critère de non-régression sur les pages existantes est dégénéré : il prouve qu'une page se compose encore après 24 h, pas l'absence d'impact sur des pages historiques absentes.
- **Chemin clic → commande → intention hors couverture.** Le gate d'export refuse toute commande `OpenQuickWindow` tant que les capacités sont `Blocked`; aucune page appelante cliquable ne peut exister avant la Phase 6.
- **Cookies relâchés** pour du HTTP sur boucle locale, concession de transport orthogonale à l'endurance.

Le run de validation préalable a mesuré un **p95 d'ouverture chaude de 150 ms dans un vrai navigateur**, sous le plafond de 500 ms de la Phase 0. C'est le chiffre qui manquait : le p95 antérieur venait du harnais, sans moteur de rendu.

## 8. Reste à faire avant la Phase 6

- [ ] Verdict du soak 24 h et sa consignation dans ce rapport.
- [ ] `tools/quick-window/record-checkpoint.ps1 -Phase 5` une fois le soak vert et les deux worktrees propres.
- [ ] Publier au canary les corrections de `visualisation_import.js` et `scada_builder_composition.py`, délibérément non déployées pendant le soak, puis rejouer la conformance.
- [ ] Déploiement production, sur autorisation distincte, avec smoke read-only et possibilité de redéploiement immédiat du paquet known-good.
- [ ] Décider du sort de l'écart `FR-UI-03`.
