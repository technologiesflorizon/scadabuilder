# SCADA Builder V2 - Documentation Index

Date: 2026-07-16
Status: Active enterprise documentation map
Document version: `V2.1.4.0041`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-16 | `V2.1.4.0041` | `PENDING` | `DEC-0043` implementee et validee : surface InputNumeric unique, identite A1 fiable, fallback Lire/Ecrire explicite et smoke isole reussi. |
| 2026-07-15 | `V2.1.4.0040` | `PENDING` | Approbation de `DEC-0043` : commande unique pour les cellules InputNumeric, identite A1 fiable et fallback Ecrire vers Lire; specification et plan correctifs ajoutes. |
| 2026-07-15 | `V2.1.4.0039` | `PENDING` | `DEC-0042` implemente en code : inputs numeriques cellule, manifest 2.2 et intake TF100Web 2.1/2.2; gates industriels et livraison ordonnee encore ouverts. |
| 2026-07-15 | `V2.1.4.0038` | `PENDING` | Integration de la revue du plan `DEC-0042` : nouvelles valeurs `TableEditKind` explicites et justification de `data-scada-step` sur la cible cellule TF100Web. |
| 2026-07-15 | `V2.1.4.0037` | `PENDING` | Approbation de la specification des inputs numeriques lies dans les cellules Tableau, ajout de `DEC-0042` et creation du plan cross-repo ordonnant TF100Web avant l'export `.sb2` 2.2. |
| 2026-07-15 | `V2.1.4.0036` | `PENDING` | Ajout de la specification cross-repo pour les bindings lecture/ecriture des cellules InputNumeric de Tableau dans TF100Web, sans support InputText dans cette tranche. |
| 2026-07-15 | `V2.1.4.0035` | `740796e` | Correction du hit-testing Tableau : les reperes A/1 ne recouvrent plus les cellules, le drag de plage exige un pointeur gauche actif et les scopes d'en-tete partagent le rendu de selection normalise. |
| 2026-07-15 | `V2.1.4.0034` | `b75f1d7` | Implementation de `DEC-0041` : verrou immediat avant preview, modes Tableau deterministes, etat A/1 effectif, payload editor-only teste et smoke WPF/WebView2 isole reussi. |
| 2026-07-15 | `V2.1.4.0033` | `e811253` | Approbation de la specification corrective Tableau/verrou et autorisation de son plan d'implementation. |
| 2026-07-15 | `V2.1.4.0032` | `ff21e33` | Ajout d'une specification et d'un plan correctifs autonomes pour le drag verrouille, les modes Tableau, l'acces aux cellules/pistes et les reperes A/1. |
| 2026-07-15 | `V2.1.4.0031` | `e127190` | Correction du ruban secondaire : hauteur augmentee, barre horizontale native retiree et navigation d'overflow par chevrons. |
| 2026-07-15 | `V2.1.4.0030` | `5d762bb` | Correction des interactions verrou/Tableau et clarification des reperes, fusion contextuelle et origine du format. |
| 2026-07-15 | `V2.1.4.0029` | `bbca8fa` | Modernisation compacte du ruban secondaire : commandes horizontales sur deux rangees, icones et galerie reduites. |
| 2026-07-15 | `V2.1.4.0028` | `c873744` | Correction des surfaces de base validables : Tableau ouvre le ruban contextuel sans modale; verrouillage visible et synchronisé dans Propriété, ruban Sélection, indicateur supérieur et menu contextuel Element+. |
| 2026-07-15 | `V2.1.4.0027` | `32a3ef6` | Cloture automatisee des tranches Tableau manquantes : view models dedies, inspecteur herite/personnalise/mixte, distribution/en-tetes, bridge diagnostique, scenario `win00012`, rendu HTML semantique et mesures Release 64 x 64; smoke WebView2 interactif isole encore requis. |
| 2026-07-15 | `V2.1.4.0026` | `0874416` | Implementation de `DEC-0040` : sous-surface Tableau sans dialogue, modes Objet/Cellules, contenu/format/bordures/pistes/en-tetes avances et verrouillage persistant Element+; validation interactive Release encore requise. |
| 2026-07-15 | `V2.1.4.0025` | `0b1fbf4` | Integration de la revue du plan `DEC-0040` : extraction structurelle, cas conditionnels export, tests de decouplage/resize, retrait controle du dialogue, preuve performance et staging documentaire explicite. |
| 2026-07-15 | `V2.1.4.0024` | `3f6e6a5` | Approbation de la specification d'authoring Tableau/verrouillage Element+, ajout de `DEC-0040` et creation de son plan d'implementation autonome. |
| 2026-07-15 | `V2.1.4.0023` | `18a9e9d` | Revue de la specification Tableau/verrouillage contre le code : migrations explicites, contrat JSON, ruban secondaire, bindings WPF, bordures par segment, auto-fit WebView, performance, classes et tests localises. |
| 2026-07-15 | `V2.1.4.0022` | `3a99b99` | Specification Tableau detaillee : bouton Ajouter, verrouillage persistant de tous les Element+, groupes, multiselection, surfaces partagees et decoupage concret en classes/methodes. |
| 2026-07-15 | `V2.1.4.0021` | `f77aedb` | Creation d'une specification autonome pour les outils UI d'authoring des tableaux, sans modifier la specification approuvee et implementee du Tableau moderne. |
| 2026-07-15 | `V2.1.4.0020` | `42b3105` | Premiere separation du nouveau besoin Tableau dans un document distinct, ensuite remplace par une specification autonome au vocabulaire et au cycle de vie independants. |
| 2026-07-14 | `V2.1.4.0019` | `08affb4` | Premiere redaction de l'extension Tableau, ensuite relocalisee dans une specification distincte afin de respecter son nouveau cycle de vie. |
| 2026-07-14 | `V2.1.4.0018` | `858473c` | Correction du layout commun des dialogues Tableau afin d'afficher leurs champs WPF en plus des boutons d'action. |
| 2026-07-14 | `V2.1.4.0017` | `a94016a` | Compactage du niveau 1 du ruban Inserer afin de rendre le niveau 2 entierement visible dans la hauteur normalisee. |
| 2026-07-14 | `V2.1.4.0016` | `10cfa72` | Implementation du Tableau Element+ moderne, edition type tableur, export `.sb2` sans bindings cellule, et ruban Inserer hierarchique a huit familles. |
| 2026-07-14 | `V2.1.4.0015` | `95a57ac` | Specification Tableau approuvee, `DEC-0039` enregistree et plan d'implementation executable ajoute. |
| 2026-07-14 | `V2.1.4.0014` | `a95addd` | Specification Tableau precisee avec surfaces de proprietes dediees, menu contextuel type tableur, dimensions manuelles, limite validee contre `win00012` et garde-fou strict hors `MainWindow`; une precedence de style detaillee reste a confirmer. |
| 2026-07-14 | `V2.1.4.0013` | `766f8e2` | Specification Tableau precisee : cellules texte ou inputs natifs, sans `ValueBindings` cellule par cellule. |
| 2026-07-14 | `V2.1.4.0012` | `da244d9` | Ajout du routage vers la specification draft du tableau moderne et du ruban Inserer hierarchique. |
| 2026-07-14 | `V2.1.4.0011` | `PENDING` | Gestion moderne des pages implémentée; contrats, état, surfaces, diagnostics, couverture et limites synchronisés. |
| 2026-07-14 | `V2.1.4.0010` | `c5d6f0e` | Ajout du routage vers la spécification approuvée et le plan d’implémentation de la gestion moderne des pages. |
| 2026-07-05 | `V2.1.3.0004` | `PENDING` | Ajout du champ `Component.Provenance` (Legacy/AiModernized) au contrat `.sep` (DEC-0034), avec badge "IA" dans la bibliotheque Element+ des deux applications. |
| 2026-07-05 | `V2.1.3.0003` | `PENDING` | Ajout du guide de style d'icones SCADA 2026 et du workflow interactif de modernisation Element+ (DEC-0033), en remplacement du pipeline autonome sep-ai-modernizer. |
| 2026-06-19 | `V2.1.3.0002` | `PENDING` | Ajout du color picker moderne pour les couleurs arriere-plan/bordure Style et Bouton Element+. |
| 2026-06-19 | `V2.1.3.0001` | `620e914` | Ajustement de la galerie Formes a des icones 32x32 sans libelles visibles. |
| 2026-06-19 | `V2.1.3.0000` | `b195fe0` | Correction de la galerie Formes du ruban Inserer, ajout Cercle/Triangle/Etoile, et placement Ligne/Fleche en deux points. |
| 2026-06-19 | `V2.1.2.0044` | `c50cbcf` | Extraction de la palette laterale d'outils vers le catalogue semantique d'icones et commandes. |
| 2026-06-19 | `V2.1.2.0043` | `fde1b31` | Cloture de la refonte du ruban superieur par retrait du fallback XAML statique. |
| 2026-06-19 | `V2.1.2.0042` | `0825cfe` | Branchement des commandes de ruban `object.group` et `object.ungroup` sur les workflows Element+ existants. |
| 2026-06-19 | `V2.1.2.0041` | `88a3e8b` | Extraction du catalogue de commandes de ruban dans la couche Application avec couverture de contrat. |
| 2026-06-19 | `V2.1.2.0040` | `335adfb` | Ajout du registre de commandes actif pour le rendu du ruban superieur. |
| 2026-06-19 | `V2.1.2.0039` | `e5f8a82` | Refonte du ruban superieur et normalisation du registre d'icones visibles. |
| 2026-06-19 | `V2.1.2.0038` | `6f76dc8` | Cloture du bloc boutons HMI avec parite metadata preview/export. |
| 2026-06-19 | `V2.1.2.0037` | `2a540d6` | Ajout des evenements runtime explicites pour boutons HMI standards. |
| 2026-06-19 | `V2.1.2.0036` | `8cc4d33` | Ajout du runtime disabled reel pour les boutons Element+. |
| 2026-06-19 | `V2.1.2.0035` | `588d712` | Ajout du runtime d'etat actif pour les boutons Toggle Element+. |
| 2026-06-19 | `V2.1.2.0034` | `61eef34` | Ajout du style appui/actif avance pour les boutons HMI Element+. |
| 2026-06-19 | `V2.1.2.0033` | `89d7165` | Ajout des symboles HMI Element+ interrupteur, disjoncteur, transformateur et balise alarme. |
| 2026-06-18 | `V2.1.2.0032` | `d5ee1fd` | Ajout des proprietes avancees Element+ opacite et rotation. |
| 2026-06-18 | `V2.1.2.0031` | `f6a85ed` | Ajout des symboles HMI Element+ moteur, ventilateur, convoyeur et jauge. |
| 2026-06-18 | `V2.1.2.0030` | `cae57c9` | Ajout des presets de boutons HMI Element+ et du champ exporte `ButtonKind`. |
| 2026-06-18 | `V2.1.2.0029` | `b97ef16` | Ajout des formes process HMI Element+ reservoir, tuyaux, vanne et pompe. |
| 2026-06-18 | `V2.1.2.0028` | `PENDING` | Ajout des formes HMI Element+ voyant et barres de valeur. |
| 2026-06-18 | `V2.1.2.0027` | `PENDING` | Ajout de la tranche formes standards Element+ et insertion de boutons depuis le ruban. |
| 2026-06-17 | `V2.1.2.0026` | `876a6be` | Correction du contrat `DisplayFormat` manifest et alignement TF100Web sur les datatypes de mapping. |
| 2026-06-17 | `V2.1.2.0025` | `58567eb` | Synchronisation du contrat TF100Web apres support des masques `DisplayFormat` `#` dans le runtime `.sb2`. |
| 2026-06-17 | `V2.1.2.0024` | `PENDING` | Refactor de l'onglet Donnees Element+: `Format affichage` devient le signal actif, `Mapping / Tag`, `Decimales` et `Unite` passent en legacy. |
| 2026-06-17 | `V2.1.2.0023` | `PENDING` | Ajout du statut de parite event SCADA Builder V2 / TF100Web et preparation de la prochaine tranche d'implementation. |
| 2026-06-17 | `V2.1.2.0022` | `PENDING` | Harmonisation du contrat `.sb2` pour les events de binding TF100Web `ValueBindings`. |
| 2026-06-17 | `V2.1.2.0021` | `1040889` | Correction du feedback `.sb2` pour qu'il soit applique au bon handler d'export. |
| 2026-06-17 | `V2.1.2.0020` | `c2f0b6f` | Correction du validateur CSS `.sb2` et ajout d'un indicateur de progression non bloquant pour l'export FT100. |
| 2026-06-17 | `V2.1.2.0019` | `bd6515e` | Ajout de l'export `.sb2` FT100 avec gate anti-collision DOM/CSS. |
| 2026-06-17 | `V2.1.2.0018` | `ad364a6` | Ajout du contrat d'intake FT100 audite dans TF100Web et de la reference source locale. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Ajout des effets visuels runtime standards. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Ajout du bridge lifecycle runtime global. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Ajout des groupes de conditions runtime et politique degradee explicite. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Ajout des options runtime avancees pour popup Fragment. |
| 2026-06-17 | `V2.1.2.0016` | `PENDING` | Ajout des actions runtime de bordure Element+ ciblee. |
| 2026-06-17 | `V2.1.2.0015` | `PENDING` | Ajout des actions runtime `Fermer popup` et `Basculer popup`. |
| 2026-06-17 | `V2.1.2.0014` | `PENDING` | Ajout de l'action runtime `Ouvrir popup` pour fragments compiles. |
| 2026-06-17 | `V2.1.2.0013` | `PENDING` | Ajout des filtres et du resume de catalogue tags dans l'editeur. |
| 2026-06-17 | `V2.1.2.0012` | `PENDING` | Ajout de l'application runtime des valeurs de tags lues aux Element+ lies. |
| 2026-06-17 | `V2.1.2.0010` | `PENDING` | Ajout des actions objet conditionnelles `Afficher`, `Masquer` et `Basculer visibilite`. |
| 2026-06-17 | `V2.1.2.0009` | `PENDING` | Remplacement de l'authoring `WriteTag` par les bindings Element+ `Lire valeur` et `Ecrire valeur`. |
| 2026-06-17 | `V2.1.2.0008` | `PENDING` | Ajout du catalogue tags TF100Web importe au projet et de l'authoring `WriteTag` Element+. |
| 2026-06-16 | `V2.1.2.0007` | `PENDING` | Ajout du curseur runtime par defaut pour boutons et cibles cliquables FT100/TF100Web. |
| 2026-06-16 | `V2.1.2.0006` | `PENDING` | Correction de l'export FT100 des events `Clic -> Changer de page` portes par des groupes Element+. |
| 2026-06-16 | `V2.1.2.0005` | `PENDING` | Ajout des metadonnees hover automatique des boutons Element+, de la tab Bouton et du CSS hover FT100. |
| 2026-06-16 | `V2.1.2.0004` | `PENDING` | Ajout du registre Evenement Element+ et de la premiere tranche Clic -> Changer de page. |
| 2026-06-16 | `V2.1.2.0003` | `PENDING` | Correction du groupement Element+: preservation de l'ordre visuel, hierarchie Element et mouvement solidaire. |
| 2026-06-16 | `V2.1.2.0002` | `PENDING` | Ajout du contrat de groupement scene Element+ only et de l'avertissement conversion legacy. |
| 2026-06-16 | `V2.1.2.0001` | `PENDING` | Correction du raccourci clavier WebView: Backspace ne supprime plus un Element+ selectionne et les champs editables ne declenchent pas les raccourcis scene. |
| 2026-06-16 | `V2.1.2.0000` | `PENDING` | Bump feature pour la conversion dynamique Element+ des boutons legacy, le menu Propriete contextualise et le rendu/export du texte des boutons. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Refonte de l'architecture documentaire en modules, ajout du registre decisionnel, des regles AGENTS, des contrats separes, des diagrammes Mermaid et du workflow de verification documentaire. |
| 2026-06-15 | `V2.1.1.0038` | `841d05a` | Ajout de la roadmap `On click -> open popup` et hover border sur element/groupe. |
| 2026-06-15 | `V2.1.1.0037` | `90c108b` | Ajout de la roadmap de developpement: events, tags TF100Web, Studio Element+, proprietes CSS, effets visuels et scripts globaux. |
| 2026-06-15 | `V2.1.1.0036` | `63c2475` | Generalisation du contrat de namespace CSS/DOM par page pour interdire les collisions de selecteurs en composition TF100Web. |
| 2026-06-15 | `V2.1.1.0035` | `63c2475` | Clarification du scoping CSS par page pour eviter les collisions header/body/footer sur les `data-id`. |
| 2026-06-15 | `V2.1.1.0034` | `63c2475` | Documentation du contrat selection polymorphe et suppression globale source/objet sans masquage durable. |
| 2026-06-15 | `V2.1.1.0033` | `63c2475` | Clarification du contrat de selection source `data-id`, incluant SVG, et du garde-fou inline limite aux couches HTML legacy. |
| 2026-06-15 | `V2.1.1.0032` | `63c2475` | Extension du garde-fou de geometrie inline aux objets source legacy persistants. |
| 2026-06-15 | `V2.1.1.0031` | `63c2475` | Documentation du contrat de composition header/body/footer TF100Web et du garde-fou de geometrie inline FT100. |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Creation de l'arbre documentaire stable, des regles de header et des decisions de deprecation `index.html`. |

## 1. Role

This file is the required entry point for SCADA Builder V2 documentation.

Use it to locate the owner document before editing a contract, plan, status note, or decision. The documentation is now organized by ownership:

1. Governance and decisions.
2. Product objectives.
3. Software architecture.
4. Runtime contracts.
5. Editor contracts.
6. Studio Element+ contracts.
7. UI/UX contracts.
8. Legacy migration policy.
9. Implementation status.
10. Generated code documentation and diagrams.

## 2. Required Reading

Before changing documentation or code that affects documented behavior:

1. Read `docs/AGENTS.md`.
2. Read `docs/00_governance/DECISION_REGISTER_V2.md`.
3. Read the owner document listed below for the touched area.
4. If Studio Element+ selection, hit-testing, movement, grouping, properties, `.sep` export, or regression tests are touched, read `docs/05_studio_element_plus/STUDIO_ELEMENT_PLUS_SELECTION_CONTRACT_V2.md`.

## 3. Active Documentation Tree

Governance:

1. `00_governance/DOCUMENTATION_STANDARD_V2.md` - mandatory document structure, ownership, Mermaid, code-doc, and verification rules.
2. `00_governance/DECISION_REGISTER_V2.md` - authoritative decision registry; decisions are never deleted when superseded.
3. `00_governance/VERSIONING_AND_CHANGELOG_POLICY_V2.md` - version and history policy.
4. `00_governance/TEAM_WORKFLOW_V2.md` - team workflow for code, docs, decisions, tests, and reviews.
5. `00_governance/DOC_SYNC_SKILL_SPEC_V2.md` - required behavior for the `scada-v2-doc-sync` skill and verification script.

Product and architecture:

1. `01_product/APPLICATION_OBJECTIVES_V2.md` - product objectives and non-negotiable application goals.
2. `02_architecture/GLOBAL_ARCHITECTURE_V2.md` - global software architecture and module boundaries.
3. `02_architecture/APPLICATION_FLOW_V2.md` - end-to-end flow from input to preview, Studio Element+, export, and tests.
4. `02_architecture/MODULE_BOUNDARIES_V2.md` - ownership matrix for Domain, Application, Infrastructure, Rendering, App, and Studio.
5. `02_architecture/DATA_MODEL_OVERVIEW_V2.md` - high-level project, scene, element, event, and package model.

Runtime contracts:

1. `03_runtime_contracts/PREVIEW_BUILD_EXPORT_CONTRACT_V2.md` - preview/build/export parity.
2. `03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md` - normalized package contract.
3. `03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md` - project and scene model contract.
4. `03_runtime_contracts/VERSIONING_CONTRACT_V2.md` - runtime/product version contract.

Editor contracts:

1. `04_editor/COMMANDS_CONTRACT_V2.md` - command registry, ids, enablement, dispatch, and ownership.
2. `04_editor/STATE_MANAGEMENT_CONTRACT_V2.md` - project, scene, selection, dirty state, and undo/redo ownership.
3. `04_editor/ACTIONS_EVENTS_CONTRACT_V2.md` - runtime actions, object events, tags, popup, hover, and scripts.
4. `04_editor/SELECTION_CONTRACT_V2.md` - global SCADA Builder V2 source/object selection contract.
5. `04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md` - ribbon, context menu, panels, and command surfaces.
6. `04_editor/PROPERTIES_PANEL_CONTRACT_V2.md` - property inspector ownership and validation.

Studio Element+:

1. `05_studio_element_plus/STUDIO_ELEMENT_PLUS_ARCHITECTURE_V2.md` - SCADA Builder to Studio flow.
2. `05_studio_element_plus/STUDIO_ELEMENT_PLUS_SELECTION_CONTRACT_V2.md` - canonical Studio selection contract.
3. `05_studio_element_plus/STUDIO_ELEMENT_PLUS_SEP_CONTRACT_V2.md` - `.sep` package and export boundary.

UI/UX:

1. `06_ui_ux/UI_ARCHITECTURE_V2.md` - UI shell, surfaces, and interaction ownership.
2. `06_ui_ux/UI_SPECIFICATION_V2.md` - active UI specification.
3. `06_ui_ux/ICON_STRATEGY_V2.md` - icon strategy and licensing.
4. `06_ui_ux/RESPONSIVE_MODEL_V2.md` - responsive model.

Legacy migration:

1. `07_legacy_migration/LEGACY_SOURCE_POLICY_V2.md` - legacy source policy, sanitized-source decision, and `win00008`/`win00009` baseline.
2. `07_legacy_migration/REFERENCE_PROJECT_NOTES_V2.md` - reference project notes.
3. `07_legacy_migration/MODERNIZATION_WORKFLOW_V2.md` - modernization workflow.
4. `07_legacy_migration/SCADA_2026_ICON_STYLE_GUIDE_V2.md` - icon visual style guide and junction-point contract for Element+ modernization.

Implementation status:

1. `08_implementation_status/IMPLEMENTED_FEATURES_V2.md` - current implemented features.
2. `08_implementation_status/REGRESSION_COVERAGE_V2.md` - regression coverage map.
3. `08_implementation_status/KNOWN_GAPS_V2.md` - gaps that must not be documented as implemented behavior.

Active specifications and implementation plans:

1. `superpowers/specs/2026-07-14-page-commands-design.md` - implemented architecture and product decisions for modern page management.
2. `superpowers/plans/2026-07-14-page-management-commands.md` - implementation record for page identity, commands, persistence, diagnostics, WPF surfaces, and `.sb2` compatibility; manual isolated-copy UI verification and real-project migration remain gated.
3. `superpowers/specs/2026-07-14-modern-table-and-insert-ribbon-design.md` - implemented `DEC-0039` core design for the model-backed modern table and hierarchical Insert ribbon.
4. `superpowers/specs/2026-07-15-table-ui-authoring-and-element-lock-design.md` - approved `DEC-0040` design for advanced Table UI authoring plus persistent Element+ position locking, group/multiselection semantics, shared lock surfaces, and explicit class/method boundaries.
5. `superpowers/plans/2026-07-15-table-ui-authoring-and-element-lock.md` - implementation record for advanced Table authoring and global Element+ position locking; automated validation is complete and the isolated interactive Release performance gate remains.
6. `superpowers/specs/2026-07-15-table-lock-interaction-regression-correction-design.md` - approved corrective specification for locked drag enforcement, deterministic Table modes, internal cell/track access, and effective A/1 guide state.
7. `superpowers/plans/2026-07-15-table-lock-interaction-regression-correction.md` - approved correction plan derived from the regression specification.
8. `superpowers/plans/2026-07-14-modern-table-and-insert-ribbon.md` - implementation record for the approved and implemented modern table core and hierarchical Insert ribbon.
9. `superpowers/specs/2026-07-15-table-cell-numeric-input-tf100web-design.md` - implemented-in-code `DEC-0042` cross-repository specification for functional numeric inputs inside Table cells; industrial delivery gates remain open.
10. `superpowers/plans/2026-07-15-table-cell-numeric-input-tf100web.md` - executed cross-repository implementation plan with local 2.1/2.2 evidence and TF100Web-first delivery gates still pending.
11. `superpowers/specs/2026-07-15-table-numeric-cell-authoring-correction-design.md` - implemented `DEC-0043` corrective specification for one configuration command, fresh cell identity, A1 display, double-click authoring, and explicit Write-to-Read defaulting.
12. `superpowers/plans/2026-07-15-table-numeric-cell-authoring-correction.md` - executed implementation record for the `DEC-0043` authoring correction, including automated coverage and an isolated `win00012_modern_no_legacy` smoke.

Generated documentation:

1. `10_generated/CODE_MAP_V2.md` - generated or verified code map.
2. `10_generated/MODULE_FUNCTION_INDEX_V2.md` - generated public function index and doc coverage.
3. `10_generated/COMMAND_FLOW_DIAGRAM_V2.md` - command flow diagram.
4. `10_generated/STATE_FLOW_DIAGRAM_V2.md` - state flow diagram.
5. `10_generated/EXPORT_FLOW_DIAGRAM_V2.md` - export flow diagram.
6. `10_generated/STUDIO_ELEMENT_PLUS_FLOW_DIAGRAM_V2.md` - Studio Element+ flow diagram.

## 4. Current Contract Guardrails

These guardrails are active decisions in `00_governance/DECISION_REGISTER_V2.md`:

1. Current FT100/TF100Web exports use root `manifest.json` plus `<page-id>/<page-id>.html`; `index.html` is deprecated for current packages.
2. Preview, build, and export consume the same V2 project model.
3. Editor overlays, layout tools, diagnostics, selection handles, drag rectangles, and test panels must not become runtime/export geometry.
4. `08_web_modernized` is comparison/history material by default and is not raw source of truth without an explicit sanitized-source decision.
5. `win00009` is the known-good comparison baseline; `win00008` is a known divergence/regression candidate.
6. Selection is polymorphic: present source nodes and Element+ scene objects remain selectable according to their contract.
7. Durable source deletion uses scene state and `RemovedSourceElementIds`, not WebView masking or inventory omission.
8. Exported CSS, DOM ids, and runtime action targets are page-namespaced for TF100Web composition.
9. Scene grouping is Element+ only; legacy/source nodes must be converted to Element+ before they can be grouped.
10. Imported TF100Web tags are project-level catalog data; Element+ value bindings use all enabled tags for `Lire valeur`, require writeable tags for `Ecrire valeur`, and export through the FT100/TF100Web manifest/runtime bridge. The editor `Catalogue Tags` panel exposes search, device, datatype, access, and state filters plus a filtered summary.
11. Element+ object visibility actions may be conditioned by imported tag values with deterministic operators; boolean `Vrai/Faux` conditions require boolean tags.
12. Runtime TF100Web can push tag values into read-bound Element+ objects through `window.scadaBuilderSetTagValue(tagId, value, meta)` or the `scada-builder-tag-value` browser event.
13. Popup actions `Ouvrir popup`, `Fermer popup`, and `Basculer popup` target compiled `Fragment` pages only; build/export validation rejects missing, non-fragment, excluded popup targets, and missing host regions for host-region popups.
14. Runtime border actions `Afficher bordure`, `Masquer bordure`, and `Basculer bordure` target Element+ objects through the standard page-scoped `scada-runtime-border-highlight` CSS class.
15. Runtime action conditions support optional compound groups with `All` or `Any` mode and explicit missing-tag policy.
16. Exported pages expose `window.scadaBuilderRuntime` and lifecycle events for page ready, action executed, and runtime errors.
17. Standard runtime visual effects include blink, glow, pulse, alarm highlight, and degraded treatment through page-scoped CSS classes.
18. Current TF100Web intake source is `F:\Projet\Git\TF100Web` on branch `implementation_scada_builder`; as audited through commit `3c795c2`, TF100Web extracts only `<div id="ft100-<page-id>">`, loads sibling CSS/assets, composes header/body/footer fragments, and executes host-side navigation plus mapping refresh/write behavior.
19. SCADA Builder exporter-emitted page scripts are not executed by the current TF100Web fragment intake. Documentation must separate exporter behavior from TF100Web-executed behavior until parity is implemented.
20. `.sb2` is the preferred FT100 transfer artifact. It is a ZIP archive whose top-level entry is `scada-builder-v2-ft100-package/`.
21. `.sb2` export rewrites legacy source ids under `ft100-<page-id>__legacy-*` before validation, then blocks packages that still contain duplicate DOM ids, unscoped DOM ids, unsafe paths, missing page roots, invalid header/footer references, or generated global CSS selectors that could collide in TF100Web composition.
22. FT100 `.sb2` export must keep the WPF shell responsive and show an indeterminate progress indicator in the bottom status bar while package generation and archive creation are running.
23. `ReadTag` and `WriteTag` are runtime binding events. Current TF100Web `.sb2` intake must consume SCADA Builder V2 `ValueBindings.ReadTagId` and `ValueBindings.WriteTagId`, resolve `tf100.mapping.<id>` to TF100Web mappings, and inject host runtime attributes onto page-scoped Element+ DOM ids.
24. Not every SCADA Builder V2 event family is currently functional in TF100Web. `03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md` owns the event parity matrix and next implementation tranche; `08_implementation_status/KNOWN_GAPS_V2.md` owns the active gap list.
25. Element+ `Donnees` authoring uses `Format affichage` as the active numeric display signal. Hash masks such as `##.#` and `###.#` are exported through `Objects[].Data.DisplayFormat` and interpreted by TF100Web against `RegisterMapping.DataType`: `FLOAT32` and `FLOAT64` round raw values directly, integer datatypes scale by mask decimals, and unknown datatypes fall back to direct rounding. `Mapping / Tag`, `Decimales`, and `Unite` are legacy model fields and are not active authoring controls. `Min` and `Max` are input constraints only for non-read-only numeric inputs.
26. Standard and HMI Element+ shapes created from SCADA Builder V2 persist `ShapeKind` and render/export as Element+-owned SVG content. Standard authoring includes rectangle, ellipse, circle, triangle, star, line, and arrow; line and arrow persist explicit start/end coordinates captured by a two-point Insert workflow. They remain real scene objects; editor-only placement previews, selection overlays, handles, drag rectangles, workzone state, zoom, and pan must not be exported.

## 5. Decommissioned Legacy Documents

The original top-level Markdown files have been decommissioned as active documentation and moved to:

```text
docs/09_archive/deprecated/
```

They are historical/source material only. They must not receive new active contracts.

Examples:

1. `docs/09_archive/deprecated/ARCHITECTURE_V2.md` -> active content lives in `02_architecture/*`.
2. `docs/09_archive/deprecated/COMMANDS_AND_STATE.md` -> active content lives in `04_editor/COMMANDS_CONTRACT_V2.md` and `04_editor/STATE_MANAGEMENT_CONTRACT_V2.md`.
3. `docs/09_archive/deprecated/PAGE_MANIFEST_OBJECT_ACTIONS_PLAN_V2.md` -> active content lives in `04_editor/ACTIONS_EVENTS_CONTRACT_V2.md` and `08_implementation_status/*`.
4. `docs/09_archive/deprecated/STUDIO_ELEMENT_PLUS_SELECTION_DECISIONS_V2.md` -> active content lives in `05_studio_element_plus/STUDIO_ELEMENT_PLUS_SELECTION_CONTRACT_V2.md`.

The decommission map is `docs/09_archive/DECOMMISSION_REPORT_V2.md`.

## 6. Validation Commands

Run documentation validation after documentation changes:

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
rg -n "index\.html|08_web_modernized|source_html|Open[ ]Decisions|Document version|Historique des changements|PENDING" docs
```

Run tests when documentation claims implemented behavior changed:

```powershell
dotnet test ScadaBuilderV2.sln --no-restore
```
