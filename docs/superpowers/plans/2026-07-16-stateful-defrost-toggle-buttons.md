# Boutons de degivrage dynamiques - Plan d'implementation

Date: 2026-07-16
Status: Implemented
Document version: `V2.1.4.0043`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-16 | `V2.1.4.0043` | `8489dbd` | Plan execute : TF100Web livre en premier, cible texte bouton et scene appliquees, regressions et documentation synchronisees. |
| 2026-07-16 | `V2.1.4.0043` | `8489dbd` | Plan cree depuis la specification approuvee de `DEC-0044`. |

**Goal:** Configurer les 56 boutons On/Off de `win00012_modern_no_legacy` avec les etats PLC vert/rouge et le texte dynamique, sans dupliquer le runtime Etat/Commande.

**Architecture:** SCADA Builder V2 fournit la cible semantique et la configuration d'etat; TF100Web complete la collecte des dependances de commande dans son cache existant. Le snapshot, l'evaluation, l'effet, le toggle et l'ecriture restent ceux du pipeline partage.

## Tache 1 - Durcir l'intake TF100Web

Files:

- Modify: `F:\Projet\Git\TF100Web\static\asset\js\station\visualisation_import.js`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`
- Modify: `F:\Projet\Git\TF100Web\docs\SCADA_BUILDER_SB2_RUNTIME.md`

Actions:

1. Faire collecter `data-scada-command-config` par `_collectRequiredMappingIds`.
2. Reutiliser le normaliseur et le `Set` existants pour dedupliquer les ids.
3. Tester les cles lecture/ecriture de commande et la conservation du polling existant.
4. Executer les tests TF100Web cibles et committer la correction sur la branche active.

## Tache 2 - Rendre le libelle de bouton ciblable

Files:

- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
- Modify: `tests/runtime-js/state-engine.test.mjs`

Actions:

1. Envelopper le libelle exporte dans `[data-scada-text]`.
2. Produire le meme descendant semantique dans le DOM WebView.
3. Ajouter les regressions export et runtime true/false.

## Tache 3 - Configurer la scene de reference

Files:

- Modify: `projects/AMR_REF_SCADA_V2/scenes/win00012_modern_no_legacy.scene.json`
- Create: `tests/ScadaBuilderV2.Tests/Win00012DefrostToggleConfigurationTests.cs`

Actions:

1. Ajouter deux etats deterministes aux 56 boutons.
2. Utiliser le `ReadTagId` de leur commande Toggle comme reference canonique.
3. Verifier nombre, noms, expressions, couleurs, opacites, textes et coherence du tag.

## Tache 4 - Valider et synchroniser

Files:

- Modify: `VERSION`
- Modify: `docs/00_governance/DECISION_REGISTER_V2.md`
- Modify: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`
- Modify: `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`
- Modify: `docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md`
- Modify: `docs/08_implementation_status/KNOWN_GAPS_V2.md`
- Modify: `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md`
- Modify: `docs/README.md`
- Modify: specification et plan de cette tranche

Actions:

1. Executer les tests C#, runtime JavaScript et TF100Web cibles.
2. Executer build, validation documentaire et `git diff --check`.
3. Passer specification, plan et `DEC-0044` au statut implemente avec preuves reelles.
4. Appliquer le bump d'iteration `V2.1.4.0043` et committer SCADA Builder V2 sur la branche active.

## Execution Record

1. TF100Web : commit `29ebd35`; `node --check` reussi et 12/12 tests `ScadaRuntimeInitContractTests` reussis. Le runner Django complet Windows reste bloque par la dependance POSIX `fcntl`, donc la classe cible a ete executee apres `django.setup()` sans le check URL global.
2. SCADA Builder V2 : build complet reussi; 3/3 tests cibles d'export/WebView/scene reussis; 20/20 tests runtime JavaScript reussis.
3. Suite C# complete : 661/666 reussis; les cinq echecs historiques sont inchanges et sans lien avec `DEC-0044`.
4. Documentation : contrat Etat/Commande, intake FT100Web, decision, fonctionnalites, gaps, couverture, index et diagramme export synchronises.
5. Verificateur documentaire : 167 erreurs historiques de metadata, baseline inchangee; aucune ne cible les nouveaux documents `DEC-0044`.
