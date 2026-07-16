# Runtime partage Etat et bindings Tableau - Plan d'implementation

Date: 2026-07-16
Status: Implemented
Document version: `V2.1.4.0044`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-16 | `V2.1.4.0044` | `de37a35`, TF100Web `9d5d400` | Plan execute; correctifs cross-repo, regressions ciblees et build valides. |

**Goal:** Restaurer des boutons Etat lisibles et rendre fonctionnelles les 126 lectures/ecritures des cellules Tableau sans creer de runtime parallele.

**Architecture:** TF100Web etend son cache et son binding numerique commun aux mappings resolus des cellules. Le runtime package Builder rend les effets d'etat reversibles et place le filtre sous le contenu.

**Tech Stack:** JavaScript navigateur, Python/Django, .NET 8, MSTest, Node test runner, PowerShell.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-16-shared-runtime-visual-and-table-binding-correction-design.md`.
- Manifest 2.2 et `TableCellBindings` restent inchanges.
- Aucun polling, controle ou handler specialise Tableau.
- Les changements projet non relies deja presents dans le worktree Builder ne sont pas stages.

## Phase 1 - TF100Web

### Task 1: Unifier collecte, lecture, ecriture et hydratation

**Files:**
- Modify: `F:\Projet\Git\TF100Web\static\asset\js\station\visualisation_import.js`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`
- Modify: `F:\Projet\Git\TF100Web\docs\SCADA_BUILDER_SB2_RUNTIME.md`

**Interfaces:** Consomme les attributs mapping injectes par l'intake; produit un seul flux snapshot et ValueBinding moderne.

- [x] Collecter les mappings resolus lecture/ecriture.
- [x] Appliquer les lectures aux inputs standards et Tableau.
- [x] Lier l'ecriture ValueBinding une seule fois et conserver les gestes operateur.
- [x] Forcer le premier snapshot avant evaluation de la nouvelle page.
- [x] Ajouter les regressions de contrat et executer les tests cibles.
- [x] Committer TF100Web independamment (`9d5d400`).

## Phase 2 - SCADA Builder V2

### Task 2: Rendre EffectApplier reversible et le filtre lisible

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/effect-applier.js`
- Modify: `tests/runtime-js/effect-applier.test.mjs`
- Modify: `tests/runtime-js/state-engine.test.mjs`

**Interfaces:** Consomme les `ScadaEffectBlock` existants; preserve le contrat Etat et le DOM exporte.

- [x] Capturer/restaurer les proprietes runtime gerees.
- [x] Placer l'overlay sous le contenu semantique.
- [x] Tester fallback vers actif, actif vers arrete et retrait d'effet.
- [x] Executer les tests runtime et C# cibles.

## Phase 3 - Gouvernance et livraison

### Task 3: Synchroniser contrats, version et preuves

**Files:**
- Modify: `VERSION`, `docs/README.md`, decision, contrat runtime et statut/couverture.
- Modify: specification et plan de cette tranche.

- [x] Ajouter `DEC-0045` et le bump d'iteration `V2.1.4.0044`.
- [x] Consigner les tests et les limites industrielles restantes.
- [x] Executer build, suites ciblees, verification docs et `git diff --check`.
- [x] Committer uniquement code/tests/docs de cette tranche.
- [x] Generer et valider un nouveau `.sb2` sans stager les donnees projet utilisateur.

## Validation Checklist

- [x] Les toggles confirment vert/rouge avec texte non teinte et opacite restauree.
- [x] Les 126 mappings cellule entrent dans le snapshot partage.
- [x] Les inputs existants sont conserves et ne sont pas ecrases pendant l'edition.
- [x] Aucun nouveau chemin Tableau specialise n'existe.
- [x] Le manifest reste 2.2 et le package est valide.
