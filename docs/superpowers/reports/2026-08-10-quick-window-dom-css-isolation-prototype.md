# Prototype d’isolation DOM/CSS Fenêtre rapide — Rapport de gate Phase 0

Date: 2026-08-13
Status: PASS — gate Phase 0 corrigé et validé dans les deux hosts
Document version: `V2.1.5.0033`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-08-24 | `V2.1.5.0033` | `85e088d` | Leg Edge/TF100Web rejoué sur le moteur épinglé (`PASS`, révision `1.0.2`, 100 cycles, un poller, Edge `151.0.4129.101`); dérive de la fixture vendorisée corrigée (CRLF et espaces de fin) et épinglage LF ajouté des deux côtés. |
| 2026-08-24 | `V2.1.5.0032` | `1fd1d14` | Leg WebView2 réel rejoué sur Node `24.15.0` (`PASS`, hash gelé inchangé, WebView2 Runtime `151.0.4129.101`); le harnais lit désormais l'épinglage depuis `.nvmrc`. Leg TF100Web toujours à rejouer. |
| 2026-08-24 | `V2.1.5.0031` | `cd61f0e` | Ré-épinglage du moteur sur Node `24.15.x` (`DEC-0051`) : fixture et hash gelés inchangés, leg Node rejoué `PASS` sur `v24.15.0`; legs WebView2 réel et TF100Web à rejouer. |
| 2026-08-13 | `V2.1.5.0021` | `b353e37` | Remplacement des preuves simulées par des assertions DOM réelles, exécution WebView2 réelle, exécution Edge/TF100Web réelle, 100 cycles et hash gelé strict. |
| 2026-08-11 | `V2.1.5.0019` | `b353e37` | Rapport initial déclaré PASS; invalidé par l’audit correctif du 2026-08-13 parce qu’il utilisait un host simulé, des assertions permissives et une version Node non conforme. |

## 0. Ré-épinglage du moteur (2026-08-24, `DEC-0051`)

Le moteur épinglé passe de Node `20.18.x` à `24.15.x`. Les preuves ci-dessous restent celles produites le 2026-08-13 sur `v20.18.1` et ne sont ni réécrites ni supprimées : elles constituent le dossier de la validation initiale.

Le leg Node headless a été rejoué le 2026-08-24 sur `v24.15.0`. Résultat `PASS` avec le même hash de fixture gelée `be4db555a46e20d74cf0a60be38ea6919eb51235c2668158b9e2abda1b10cde9`, ce qui montre que les invariants `FR-020` et `FR-026` ne dépendent pas de la version majeure du moteur.

Le leg WebView2 réel côté Builder a été rejoué le 2026-08-24 par `tools/QuickWindowIsolationPrototype.App` : `PASS`, hash de fixture gelée identique, Node `v24.15.0`, WebView2 SDK `1.0.3967.48` et Runtime Evergreen `151.0.4129.101`. Le harnais ne code plus la version épinglée en dur : il la dérive de `.nvmrc`, de sorte qu'un futur ré-épinglage ne demande aucune modification de code.

Le leg Edge/TF100Web a été rejoué le 2026-08-24 sur la branche `codex/quick-window-v1` du dépôt TF100Web : `PASS`, révision `1.0.2`, 100 cycles, un seul poller, Node `v24.15.0`, Edge `151.0.4129.101` en `--headless=new`. L'artefact `artifacts/quick-window-isolation/tf100web.json` est désormais cette capture réelle et remplace l'exécution simulée du 2026-08-11 qu'avait invalidée l'audit.

Ce rejeu a révélé une fragilité du gel, indépendante du ré-épinglage : la fixture vendorisée dans TF100Web ne produisait plus le hash gelé pour deux raisons sans rapport avec son contenu. Le checkout la convertissait en CRLF alors que le gate hache des octets, et `README.md` avait perdu les deux espaces de fin de deux sauts de ligne markdown lors de la vendorisation. Les deux dépôts épinglent maintenant le répertoire de fixture en LF via `.gitattributes` et `README.md` est redevenu identique octet pour octet, de sorte que le hash gelé tienne dans tout clone au lieu de tenir par accident de l'arbre de travail local. Le hash gelé `be4db555a46e20d74cf0a60be38ea6919eb51235c2668158b9e2abda1b10cde9` est inchangé.

## 1. Décision

**PASS.** La fixture gelée `PrototypeRevision 1.0.2` réussit les assertions core dans un WebView2 réel et dans le navigateur Edge headless utilisé par le test de composition TF100Web. Le hash SHA-256 commun est:

`be4db555a46e20d74cf0a60be38ea6919eb51235c2668158b9e2abda1b10cde9`

Le PASS initial du 2026-08-11 ne constitue pas une preuve valide. La présente révision est l’unique baseline Phase 0 autorisant les contrats inertes de Phase 1.

## 2. Environnement qualifié

| Surface | Version observée | Résultat |
| --- | --- | --- |
| Node de gate | `20.18.1` | Conforme à `.nvmrc` et `engines.node` |
| WebView2 SDK | `1.0.3967.48` | Conforme au projet de harness |
| WebView2 Runtime | `151.0.4129.78` | Exécution réelle, non simulée |
| TF100Web browser test | Microsoft Edge installé, mode headless | Exécution DOM réelle |

Les commits de correction des deux dépôts sont `PENDING` jusqu’au commit de cette tranche. Aucun déploiement, accès PLC ou modification du host TF100Web de production n’a été réalisé.

## 3. Matrice d’assertions

Les assertions couvrent réellement:

1. les identifiants et références DOM scoppés pour `Page -> A -> B`;
2. l’isolation CSS, y compris keyframes, classes et sélecteurs identiques;
3. les états, tableaux, inputs, cibles de commande et écritures propres à chaque invocation;
4. le focus initial, le confinement Tab/Shift+Tab, `Escape`, `X`, la cascade enfant/parent et la restitution du focus;
5. les listeners, observers, timers, souscriptions, cache et pont d’écriture revenus à la baseline;
6. 100 cycles d’ouverture/fermeture sans croissance;
7. les doubles ouvertures, navigation pendant montage, fermeture pendant hydratation, dispose idempotent et rejet des générations stale;
8. un seul poller/cache/pont partagé et aucune écriture croisée.

Les tests ne contiennent plus de court-circuit `|| true`, de résultat statique ou de runtime `simulated-headless`.

## 4. Performance de la fixture

Sur la machine de référence, le harness WebView2 a mesuré environ `22 ms` au p95 chaud et `22 ms` au p95 froid. Ces valeurs satisfont les seuils Phase 0 de `500 ms` chaud et `1 500 ms` froid. Elles décrivent uniquement la fixture; la comparaison avec le portage de production reste un gate Phase 7.

## 5. Commandes de preuve

```powershell
& "C:\Users\mathi\AppData\Local\npm-cache\_npx\118cdd991bfdaaba\node_modules\node\bin\node.exe" --test tests/runtime-js/quick-window-dom-css-isolation.test.mjs
dotnet run --project tools/QuickWindowIsolationPrototype.App --configuration Release -- --output artifacts/quick-window-isolation/builder-webview2.json
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~QuickWindowIsolationPrototypeContractTests"

Set-Location "F:\Projet\Git\TF100Web"
& "C:\Users\mathi\AppData\Local\npm-cache\_npx\118cdd991bfdaaba\node_modules\node\bin\node.exe" --test frontend/tests_runtime_js/quick-window-dom-css-isolation.test.mjs
.\.venv\Scripts\python.exe -m pytest frontend/tests_scada_quick_window_isolation_prototype.py
```

Résultats: Builder Node `5/5`, WebView2 `32/32` assertions, TF100Web Node `3/3`, TF100Web Python/Edge `6/6`.

## 6. Portée autorisée

La Phase 1 peut définir et persister des contrats encore inertes. Toutes les capacités `quick-window.*` et `command.*-quick-window` restent `Blocked`. Le PASS Phase 0 n’autorise ni authoring QuickWindow, ni preview de production, ni compilation/export `.sb2`, ni activation TF100Web.
