# SCADA Builder V2 - UI Specification

Date: 2026-06-17
Status: Active UI specification pointer
Document version: `V2.1.2.0040`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
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

## 3. Status Bar Feedback

Long-running export commands must keep the main shell responsive and surface progress in the bottom status bar. FT100 `.sb2` export uses an indeterminate progress bar at the bottom right while package staging, validation, and archive creation are active.

## 4. Top Ribbon Strategy

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
