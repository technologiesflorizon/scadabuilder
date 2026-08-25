# Audit d'implémentation Fenêtres rapides — Phase 4

Date: 2026-08-25
Status: PASS — Phase 4 conforme; Phase 5 non démarrée et capacités toujours `Blocked`
Document version: `V2.1.5.0044`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-08-25 | `V2.1.5.0044` | `c3cfce9` | MySQL disponible sous WSL : les suites Django adossees a la base passent; le dernier prerequis d'infrastructure de la Phase 5 est leve. |
| 2026-08-25 | `V2.1.5.0043` | `94f1a2b` | Correction : la suite Django de TF100Web s'exécute sous WSL; le blocage `fcntl` est levé et le reste à faire est requalifié. |
| 2026-08-25 | `V2.1.5.0042` | `3e87e33` | Audit de clôture de la Phase 4 : contrat package figé, capacités enregistrées, compilation déterministe, runtime partagé et round-trip package exécuté. |

## 1. Portée et conclusion

**PASS.** Les cinq tâches de la Phase 4 sont implémentées, testées et documentées. Le paquet `.sb2` sait transporter des Fenêtres rapides, le runtime partagé sait les résoudre de façon portable, et le premier round-trip réel Builder → package → TF100Web est vert **avant** toute ligne d'adaptateur host.

Aucune capacité n'est promue. Les treize identifiants `quick-window.*` restent `Blocked` dans le catalogue et dans la matrice générée. Le paquet de handshake ne déclare aucune capacité, ce qui est exactement ce qu'un package strict 2.3 peut déclarer aujourd'hui, et le gate structurel empêche tout export produit d'en émettre un.

Cet audit ne couvre pas les Phases 5 à 7, non démarrées.

## 2. Preuves d'exécution

Exécutions du 2026-08-25, Builder `7e8e814` (`codex/GestionFenetreRapide`) et TF100Web `efebd43` (`codex/quick-window-v1`).

| Vérification | Commande | Résultat |
| --- | --- | --- |
| Suite complète Builder | `dotnet test ScadaBuilderV2.sln` | 874/874 |
| Suites ciblées Fenêtres rapides et contrats runtime | `--filter "…QuickWindow\|…RuntimeContracts\|…Ft100PackageValidatorTests"` | 189/189 |
| Runtime JS Builder | `npm --prefix tests/runtime-js test` | 80/80 |
| Suites Fenêtres rapides TF100Web | `python -m pytest frontend/tests_scada_quick_window_*.py` | 16/16 |
| Gate documentaire | `tools/docs/verify-docs.ps1` | Errors: 0 |
| Checkpoint versionné | `tools/quick-window/record-checkpoint.ps1 -Phase 4` | Entrée `phase 4` écrite |

Environnement : .NET SDK 8.0.26, Node `v24.15.0` conforme à l'épinglage `24.15.x` (`DEC-0051`), Python 3.13.13, Windows 10.0.26200.

## 3. Tâches et preuves associées

| Tâche | Livré | Commit | Couverture |
| --- | --- | --- | --- |
| 4.0 Contrat package figé | Layout `.sb2` et layout déployé documentés avant toute compilation, vérifiés contre `scada_package.py`, `scada_builder_composition.py` et `deploy_scada_builder.py` | `c4f7391` | Contrat §12, gate documentaire |
| 4.1 Capacités `Blocked` | 13 identifiants granulaires, tous `Blocked`, chacun dérivé de son propre déclencheur; aucun identifiant parapluie ni `command.toggle-quick-window` | `b1ec4cd` | `ScadaRuntimeCapabilityCatalogTests`, `ScadaRuntimeCapabilityAnalyzerTests` |
| 4.2 Compilation et gate | Registres et contenu déterministes sous `qw-<key8>`, validation package, gate structurel sans bypass laissant zéro artefact | `824d656` | `QuickWindowExporterTests`, `Ft100PackageValidatorTests` |
| 4.3 Runtime partagé | `quick-window-runtime.js` portable, sans overlay ni chrome, injection rejetée avant souscription, écriture limitée à l'instance, cleanup idempotent | `5d2bfb3` | `tests/runtime-js/quick-window-runtime.test.mjs` |
| 4.4 Round-trip exécuté | Paquet de handshake généré par le seul harnais protégé, accepté par l'intake de production TF100Web, exécuté par le runtime qu'il embarque | `6f56cb8` / TF100Web `efebd43` | `QuickWindowRuntimeHandshakeFixtureTests`, `tests_scada_quick_window_contract_handshake.py`, `quick-window-runtime-handshake.test.mjs` |

## 4. Invariants vérifiés

- **Aucun export produit possible.** `ExportProjectAsync` et `ExportProjectArchiveAsync` exécutent le gate avant de créer le moindre répertoire; un projet portant des Fenêtres rapides échoue et ne laisse aucun artefact, pas même un répertoire vide. Aucun `allowBlocked`, variable d'environnement, profil caché ou branche conditionnelle n'existe, et un test scanne les lignes exécutables pour le garantir.
- **Le compilateur est hors de portée du produit.** `QuickWindowCompiler` est `internal` et n'est atteignable que par `InternalsVisibleTo` depuis les tests, afin de produire la fixture non livrable.
- **Une seule sémantique, deux hosts.** Le runtime partagé ne dessine rien : overlay, chrome, focus et montage appartiennent à l'adaptateur host versionné, atteint par l'enveloppe d'intention existante.
- **Fail-closed avant souscription.** Une liaison portant `<script>`, `javascript:`, `../`, un `#` initial ou un handler inline refuse l'invocation, n'atteint jamais le host et n'émet aucune écriture.
- **Isolation des invocations.** Une écriture n'est acceptée que par un port inscriptible de sa propre instance; M101 et M102 ne partagent aucun mapping, ce que le round-trip vérifie sur le paquet réel.
- **Déterminisme du transport.** Définitions, invocations, membres, liaisons et fichiers sont ordonnés par clé durable ou par chemin; deux compilations produisent les mêmes octets, et un projet sans Fenêtre rapide conserve un manifest identique à l'octet près.

## 5. Écarts relevés et traités pendant la phase

1. **Manifest modifié pour tous les projets.** La première version de la Task 4.2 émettait `QuickWindows: null` même sans Fenêtre rapide, ce qui changeait les octets de tout package. Détecté par les tests de dérive du paquet de conformance et du projet de référence, corrigé en n'ajoutant les registres que lorsqu'une définition existe.
2. **Rangée de couverture endommagée.** Une insertion documentaire de la Task 4.1 avait tronqué le préfixe d'une ligne de `REGRESSION_COVERAGE_V2.md`. Réparée dans le commit documentaire de la Task 4.2.
3. **Commande de vérification inexistante.** La Task 4.3 pointait `src/ScadaBuilderV2.Rendering/Runtime/tests`, répertoire qui n'existe pas. Corrigée vers `npm --prefix tests/runtime-js test`.
4. **Dérive de preuve assumée.** Ajouter `quick-window-runtime.js` au bundle change le hash du runtime et donc les octets de tout `.sb2`. Le paquet de conformance et la preuve d'acceptation industrielle ont été régénérés délibérément; le diff industriel se limite aux hashes et à l'horodatage.

## 6. Reste à faire avant la Phase 5

- **Exécution Django : résolue par WSL.** Le blocage `fcntl` relevé initialement ne vaut que pour l'interpréteur Windows. Sous WSL Ubuntu, la chaîne d'import se résout et `manage.py test frontend.tests_scada_package` s'exécute : 55 tests, 5 échecs et 1 erreur **préexistants**, identiques lorsque les changements Fenêtre rapide sont mis de côté (`git stash`). Ces six échecs concernent des attentes d'assets et de déploiement sans rapport avec ce chantier et doivent être traités pour eux-mêmes.
- **Suites adossées à la base : levées.** Le serveur MySQL de la distribution WSL est actif et satisfait le prérequis. `frontend.tests_scada_deploy` et `frontend.tests_scada_page_composition` passent **39/39**, base de test `test_tf100` créée puis détruite par le runner. Un détail d'exécution compte : `root@localhost` est configuré en `auth_socket`, donc une connexion TCP avec le mot de passe de `tf100web/settings.py` est refusée (`1698`); les suites doivent être lancées sous l'utilisateur `root` de WSL (`wsl -u root`), qui s'authentifie par la socket Unix. Le serveur héberge d'autres bases applicatives, que le runner Django ne touche pas.
- **Aucune infrastructure ne bloque plus la Phase 5.** Les six échecs de `frontend.tests_scada_package` sont identiques avec et sans base : ils ne dépendent pas du serveur et restent à traiter pour eux-mêmes.
- **Environnements Python sous WSL.** Le dépôt TF100Web embarque déjà `.venv-wsl` (Django 4.2.16, sans `pytest`). Un second environnement `~/.venvs/tf100web` a été créé pour cette vérification avec `requirements.txt` complet plus `pytest`; il est jetable et peut être supprimé.
- **Outils Windows absents sous WSL.** Deux tests échouent sous WSL faute d'outil installé dans la distribution : le leg Edge de la Phase 0 pointe le chemin Windows de `msedge.exe`, et le handshake runtime exige `node`. Les deux passent sous Windows. Aucun n'est un échec de logique.
- **Ce que TF100Web doit encore recevoir.** L'ingestion des registres `QuickWindows[]`/`QuickWindowInvocations[]`, un chargeur de fragment par namespace avec résolution du CSS aplati, l'ajout de `openQuickWindow`/`closeQuickWindow` aux `acceptedKinds` de l'adaptateur host existant avec sortie avant le garde `validPageId`, les services d'overlay et de cycle de vie, et l'appel de `ScadaRuntime.loadQuickWindowRegistries(manifest)` par la vue.
- **Aucune extension de `SUPPORTED_SCADA_RUNTIME_CAPABILITIES` avant la Phase 6.** L'étendre plus tôt ouvrirait la porte à un package non prouvé.
- Les deux dépôts portent des commits locaux; leur publication reste une décision explicite.
