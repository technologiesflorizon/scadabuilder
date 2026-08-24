# Audit d'implémentation Fenêtres rapides — Phase 3

Date: 2026-08-24
Status: PASS — Phase 3 authoring conforme; Phase 4 non démarrée et capacités toujours `Blocked`
Document version: `V2.1.5.0036`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-08-24 | `V2.1.5.0036` | `3560f48` | Audit de clôture de la Phase 3 : tâches 3.1 à 3.6, ré-épinglage du moteur `DEC-0051` et rejeu des trois legs du gate Phase 0. |

## 1. Portée et conclusion

**PASS.** Les six tâches de la Phase 3 sont implémentées, testées et documentées. L'authoring des Fenêtres rapides est complet côté éditeur : groupe projet, contexte borné, Interface locale, commandes appelantes et liaisons typées, aperçu d'instance editor-only, frontière presse-papier et surface de réparation.

Aucune capacité runtime n'est promue par cette phase. `command.open-quick-window` et `command.close-quick-window` restent `Blocked` dans `ScadaRuntimeCapabilityCatalog` et dans la matrice générée `docs/10_generated/RUNTIME_CAPABILITY_MATRIX_V2.md`. Aucun package Fenêtre rapide productible ne peut être émis.

Cet audit ne couvre pas les Phases 4 à 7, non démarrées.

## 2. Preuves d'exécution

Exécutions du 2026-08-24 sur la machine de référence, au commit Builder `7b2979d` (`codex/GestionFenetreRapide`) et TF100Web `1e1400b` (`codex/quick-window-v1`).

| Vérification | Commande | Résultat |
| --- | --- | --- |
| Build solution | `dotnet build ScadaBuilderV2.sln` | 0 erreur |
| Suite complète | `dotnet test ScadaBuilderV2.sln` | 847/847 |
| Suites Fenêtres rapides ciblées | `--filter "FullyQualifiedName~QuickWindow\|FullyQualifiedName~SceneClipboardTests"` | 142/142 |
| Runtime JS | `npm --prefix tests/runtime-js test` | 68/68 |
| Gate documentaire | `tools/docs/verify-docs.ps1` | Errors: 0 |
| Checkpoint versionné | `tools/quick-window/record-checkpoint.ps1 -Phase 3` | Entrée `phase 3` écrite dans `tools/quick-window/checkpoints.json` |

Environnement : .NET SDK 8.0.26, Node `v24.15.0` conforme à l'épinglage `24.15.x`, WebView2 SDK `1.0.3967.48`, runtime Evergreen `151.0.4129.101`, Windows 10.0.26200.

## 3. Tâches et preuves associées

| Tâche | Livré | Commit | Couverture |
| --- | --- | --- | --- |
| 3.1 Groupe projet et contexte d'éditeur | Groupe `Fenêtres rapides` distinct, contexte borné masquant les commandes page-only, duplication indépendante, portée d'historique `QuickWindow` sur la pile unique, projection canvas editor-only en lecture seule | `2551d35`, `f4867fa` | `QuickWindowShellContractTests` |
| 3.2 Éditeur Interface locale | Panneau substitué au `Catalogue Tags`, tableau unique groupé public/privé avec filtres, édition inline et dialogue commun, compteurs d'usages et navigation, suppression référencée confirmée, sélecteurs restreints aux membres locaux | `fafdf53` | `QuickWindowInterfaceAuthoringTests` |
| 3.3 Open/Close et onglet `Liaisons` | `OpenQuickWindow` avec cible définition et sans cible page, `CloseQuickWindow(Self)` réservé au contenu, onglet conditionnel, colonnes et sources typées, statut `Outdated` en rouge | `9795cce` | `QuickWindowBindingAuthoringTests` |
| 3.4 Preview et banc d'essai | Module runtime hôte adapté du prototype gelé, aperçu d'instance avec chrome minimal, banc d'essai transitoire, bundle d'aperçu séparé du bundle exporté | `724e621` | `QuickWindowPreviewTests`, `tests/runtime-js/quick-window-host.test.mjs` |
| 3.5 Frontière presse-papier | Analyse fail-closed des six cas de franchissement, refus par défaut, `Coller sans liaisons` sans promotion ni référence orpheline, origine portée par le presse-papier partagé | `dae5b89` | `QuickWindowClipboardTests` |
| 3.6 Réparation des invocations `Outdated` | Liste page/appelant/commande/motif, navigation vers l'appelant, reliaison port par port, confirmation d'impact avant évolution d'interface, gate de build fermé jusqu'à réparation complète | `1d4604d` | `QuickWindowInterfaceVersioningTests` |

## 4. Invariants vérifiés

- **Aucun artefact d'éditeur exportable.** L'aperçu d'instance, son chrome, les données du banc d'essai et la projection canvas sont editor-only. `QuickWindowPreviewTests` vérifie qu'un document exportable ne contient ni `data-qw-test-values` ni `qw-frame`, et que le bundle runtime exporté ne contient pas le gestionnaire hôte.
- **Une seule sémantique d'instance.** `ScadaRuntime.QuickWindowHost` est l'unique implémentation de `SinglePerDefinition`, des générations monotones, du rejet d'hydratation obsolète, de `X`/`Escape`/`Self`, du backdrop partagé non fermant et de la cascade. Le montage et la disposition délèguent au runtime partagé; l'adaptateur Builder ne duplique aucune politique.
- **Pile d'historique unique.** Aucune Fenêtre rapide ne crée de second service d'historique, de presse-papier ou de gestionnaire d'overlay. Le presse-papier de scène existant a été étendu de son contexte d'origine.
- **Fail-closed sur la frontière.** Un contenu de Fenêtre rapide ne référence jamais un tag physique du projet et une page ne référence jamais un membre d'Interface locale; le refus est le défaut et le retrait de références ne promeut rien.
- **Versionnement d'interface.** L'`InterfaceVersion` n'est incrémentée que lorsque le contrat public change; une transition cassante laisse les invocations `Outdated` avec leurs liaisons préservées et bloque build et export jusqu'à réparation explicite.

## 5. Écarts relevés et traités pendant la phase

1. **Épinglage du moteur Node.** Le poste de référence était passé en `v24.15.0` alors que le gate épinglait `20.18.x`. Décision `DEC-0051` : ré-épinglage sur `24.15.x`, fixture et hash gelés inchangés. Les trois legs du gate Phase 0 ont été rejoués `PASS` sur le moteur épinglé (`cd61f0e`, `1fd1d14`, `85e088d`).
2. **Fragilité du gel de la fixture.** Le rejeu a montré que le hash gelé ne tenait que par l'état de l'arbre de travail : la copie vendorisée dans TF100Web était convertie en CRLF et `README.md` y avait perdu les espaces de fin de deux sauts de ligne markdown. Les deux dépôts épinglent désormais le répertoire de fixture en LF et la copie est redevenue identique octet pour octet.
3. **Preuve TF100Web invalide.** L'artefact `tf100web.json` était l'exécution simulée du 2026-08-11 que l'audit correctif avait invalidée. Il est remplacé par une capture Edge `--headless=new` réelle, révision `1.0.2`, 100 cycles, un seul poller.

## 6. Reste à faire avant la Phase 4

- Le leg Edge/TF100Web est rejoué mais **rien n'est poussé** : les deux dépôts portent des commits locaux (`codex/GestionFenetreRapide` et `codex/quick-window-v1`).
- La Phase 4 commence par la Task 4.0, gate bloquant : figer le contrat package Fenêtre rapide avant toute compilation, ce qui exige l'inspection des fonctions et tests TF100Web correspondants.
- Aucune capacité ne peut être promue avant la Phase 6, et seulement avec les preuves Builder, runtime partagé et TF100Web exigées par `DEC-0047`.
