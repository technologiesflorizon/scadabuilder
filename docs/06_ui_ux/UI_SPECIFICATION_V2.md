# SCADA Builder V2 - UI Specification

Date: 2026-07-16
Status: Active UI specification pointer
Document version: `V2.1.4.0041`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-16 | `V2.1.4.0041` | `PENDING` | Authoring InputNumeric Tableau fiabilise par une commande unique, une cible Tableau/A1 visible, une selection fraiche et l'alignement Lire depuis Ecrire. |
| 2026-07-15 | `V2.1.4.0034` | `b75f1d7` | Mode Objet initial et reselection deterministe; verrou de position immediat sans bloquer cellules/pistes; A/1 aligne sur la visibilite effective. |
| 2026-07-15 | `V2.1.4.0031` | `e127190` | Ruban secondaire sans scrollbar native, navigation d'overflow par chevrons et hauteur anti-clipping. |
| 2026-07-15 | `V2.1.4.0030` | `5d762bb` | Interactions Tableau corriges : cellules et pistes prioritaires, reperes A/1 masquables, fusion toggle et origine du format explicite. |
| 2026-07-15 | `V2.1.4.0029` | `bbca8fa` | Ruban secondaire modernise en boutons horizontaux compacts sur deux rangees, avec icones, galerie et espacement reduits. |
| 2026-07-15 | `V2.1.4.0027` | `88e865a` | Spécification UI synchronisée avec le ruban Tableau contextuel, l'inspecteur Hérité/Personnalisé/Mixte, les dimensions exactes, color pickers, bordures, distribution et en-têtes. |

## Table Authoring Surface

1. `Insérer > Données > Tableau` ouvre les outils sans dialogue; `Ajouter` arme le placement configuré.
2. Les modes Objet et Cellules sont exclusifs. Les headers editor-only sélectionnent rangée, colonne ou table sans déplacer l'objet.
3. Le panneau Propriété affiche portée, état de format, contenu, format, bordures, en-têtes, fusion, distribution et reset.
4. Les dialogues de format et bordure réutilisent le color picker du produit; les propriétés détaillées exposent X/Y/W/H exacts.
5. Le ruban garde visibles les commandes indisponibles avec une raison issue de l'état applicatif.
6. Le mode Cellules donne la priorite aux cellules, inputs et poignees de pistes; le tableau conteneur ne demarre aucun drag pour ces gestes.
7. Les reperes d'edition A/1 peuvent etre affiches ou masques depuis le ruban sans modifier le modele ni l'export.
8. Fusionner/Defusionner est une seule commande contextuelle dont le libelle et l'etat actif suivent la plage selectionnee.
9. Un Tableau nouvellement place ou reselectionne commence en mode Objet. Le mode Cellules s'active explicitement, par double-clic ou par `Afficher A/1`; `Escape` revient en mode Objet.
10. Le verrou protege immediatement X/Y sans animation de retour. Il ne bloque ni la selection, ni les cellules, ni les separateurs de pistes, ni les resizes qui conservent X/Y.
11. Le libelle `Afficher/Masquer A/1` decrit la visibilite effective : les reperes ne sont visibles qu'en mode Cellules et peuvent etre retablis immediatement apres masquage.
12. Le groupe `Input numerique` expose une seule commande `Configurer <A1>` pour une cellule ancre valide; les commandes visibles Lire et Ecrire sont retirees.
13. Le ruban, le panneau Propriete et le dialogue affichent le meme id Tableau et la meme adresse A1 editor-only.
14. Une selection provenant d'un autre Tableau est refusee; apres changement de Tableau, la configuration reste indisponible jusqu'au prochain clic cellule valide.
15. Le dialogue reste sur une page. Choisir Ecrire initialise immediatement Lire avec le meme tag seulement lorsque Lire est vide; une lecture distincte est preservee.
16. Le double-clic d'une cellule `InputNumeric` ouvre la configuration de cette cellule exacte; le double-clic d'une cellule texte conserve l'edition inline.
| 2026-07-13 | `V2.1.4.0003` | `b954d46` | Direction haut de gamme de l’inspecteur Style : sections, contrôles à état, icônes sémantiques et aperçu vivant. |
| 2026-06-19 | `V2.1.3.0002` | `PENDING` | Ajout du standard de polish produit concurrentiel face a ScadaPlant. |
| 2026-06-19 | `V2.1.3.0001` | `620e914` | Ajustement de la galerie Formes: icones 32x32 et boutons sans libelles visibles. |
| 2026-06-19 | `V2.1.3.0000` | `b195fe0` | Normalisation de la galerie Formes du ruban Inserer avec icones 64x64 et etat actif. |
| 2026-06-19 | `V2.1.2.0044` | `c50cbcf` | La palette laterale d'outils consomme maintenant le catalogue semantique d'icones. |
| 2026-06-19 | `V2.1.2.0043` | `fde1b31` | Le ruban superieur ne conserve plus de surface XAML statique parallele. |
| 2026-06-19 | `V2.1.2.0042` | `0825cfe` | Le ruban Selection execute maintenant Grouper et Degrouper pour les Element+ selectionnes. |
| 2026-06-19 | `V2.1.2.0041` | `88a3e8b` | Le catalogue du ruban est extrait de la fenetre WPF et couvert par tests de contrat. |
| 2026-06-19 | `V2.1.2.0040` | `335adfb` | Le ruban superieur consomme un registre actif de commandes et de groupes. |
| 2026-06-19 | `V2.1.2.0039` | `e5f8a82` | Refonte du ruban superieur en groupes visuels, onglet actif et overflow horizontal. |
| 2026-06-17 | `V2.1.2.0021` | `1040889` | Correction du feedback de progression pour cibler le handler `.sb2`. |
| 2026-06-17 | `V2.1.2.0020` | `c2f0b6f` | Ajout de l'indicateur de progression statut pour l'export FT100 `.sb2`. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du nouveau document proprietaire UI spec avec lien de migration vers l'ancien document. |

## 1. Active Direction

This document owns future UI specification updates.

Historical UI material is archived in `docs/09_archive/deprecated/UI_SPEC_V2.md` and `docs/09_archive/deprecated/UI_DIRECTION_V2.md`. New active UI contracts must be added here or to a more specific owner document.

## 2. Immediate Rule

UI behavior that maps to commands, state, actions, selection, or menus must be documented in the corresponding `04_editor` contract rather than duplicated here.

## 3. Product Polish Bar

SCADA Builder V2 targets a competitive product quality level against ScadaPlant. UI work is complete only when it reads as a coherent industrial SCADA/HMI tool rather than an internal prototype.

Required UI polish standards:

1. Command surfaces, editor interactions, property panels, and preview/export feedback must be consistent, readable, and production-oriented.
2. Visible placeholders, generic command icons, clipped controls, inconsistent spacing, weak active/disabled states, and unfinished affordances must be removed or explicitly documented as known gaps.
3. Dense operational surfaces are preferred over decorative or marketing-style layouts; the interface must support repeated engineering work, fast scanning, and predictable command execution.
4. Editor-only geometry, selection overlays, previews, handles, diagnostics, and helper layers must remain visually distinct in the editor and must not leak into preview, FT100/TF100Web export, or `.sep` geometry.

## 4. Status Bar Feedback

Long-running export commands must keep the main shell responsive and surface progress in the bottom status bar. FT100 `.sb2` export uses an indeterminate progress bar at the bottom right while package staging, validation, and archive creation are active.

## 5. Top Ribbon Strategy

The top ribbon is a command surface, not the owner of command behavior. It must expose current commands through stable labels, icons, tooltips, and command routing while keeping disabled or future commands visually distinct.

Current shell rules:

1. The active top tab is visually marked so the user can identify the selected command family.
2. Ribbon content is grouped by task family, for example `Projet`, `Import`, `Export`, `Historique`, `Champs`, `Formes`, `HMI process`, `HMI electrique`, and `Boutons`.
3. Long command families must use horizontal overflow or galleries rather than being clipped by the window width.
4. Visible command buttons use French labels in the shell. Stable command ids remain implementation contracts and do not depend on the visible label.
5. Commands that are visible but not wired in the current implementation remain disabled and expose a tooltip explaining that the command is future work.
6. Insert commands use normalized semantic icon keys instead of temporary text glyphs such as `BTN`, `LED`, `TNK`, or `123`.
7. The visible ribbon content for the active tab is bound to a command registry containing `CommandId`, label, tooltip or disabled reason, `IconKey`, group, order, and executable state.
8. The renderer uses shared command/group templates so a new command is added through registry metadata before a new XAML button is considered.
9. The canonical visible command catalog lives in the Application layer and is regression-tested for tab coverage, unique command ids, semantic icon keys, and disabled reasons.
10. The `Selection` tab exposes executable group and ungroup actions for Element+ selections; blocked selection states are reported through status feedback from the existing workflow.
11. The main shell ribbon has a single dynamic command surface. Static per-tab XAML button rows must not be restored as a second command source.
12. The left tool palette uses the same command metadata adapter as the ribbon for labels, disabled tooltips, and semantic `Icon.Tool.*` lookup.
13. The Insert `Formes` group is an icon gallery capped at four columns. Shape buttons use 26x26 semantic icons, do not show redundant visible shape-name labels, expose labels through tooltips, wrap to a second row as needed, and show selected state while placement is active.
14. Line and arrow insertion uses a two-point canvas workflow with editor-only preview feedback between clicks.
15. Second-level command groups use a compact two-row layout. Regular commands are horizontal 28-pixel buttons with 16-pixel icons and concise labels; full labels remain discoverable through tooltips. Shape-gallery buttons and group spacing use the same dense industrial rhythm.
16. Horizontal overflow uses compact previous/next chevrons instead of a native scrollbar. The ribbon reserves enough vertical space for both command rows and group headers without clipping.

## 6. Element+ Style Inspector

The Style tab uses task-oriented sections (`Typographie`, `Couleurs`, `Bordure`, `Ombre et effets`, and `CSS avancé`). Formatting controls use active-state toggles or radio buttons, advanced fields expose tooltips and validation, and the modal and docked surfaces use the same property names and defaults. A local WPF preview reflects uncommitted values before application; it is a temporary projection and is never exported.
