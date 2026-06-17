# SCADA Builder V2 - UI Specification

Date: 2026-06-17
Status: Active UI specification pointer
Document version: `V2.1.2.0021`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-17 | `V2.1.2.0021` | `PENDING` | Correction du feedback de progression pour cibler le handler `.sb2`. |
| 2026-06-17 | `V2.1.2.0020` | `c2f0b6f` | Ajout de l'indicateur de progression statut pour l'export FT100 `.sb2`. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du nouveau document proprietaire UI spec avec lien de migration vers l'ancien document. |

## 1. Active Direction

This document owns future UI specification updates.

Historical UI material is archived in `docs/09_archive/deprecated/UI_SPEC_V2.md` and `docs/09_archive/deprecated/UI_DIRECTION_V2.md`. New active UI contracts must be added here or to a more specific owner document.

## 2. Immediate Rule

UI behavior that maps to commands, state, actions, selection, or menus must be documented in the corresponding `04_editor` contract rather than duplicated here.

## 3. Status Bar Feedback

Long-running export commands must keep the main shell responsive and surface progress in the bottom status bar. FT100 `.sb2` export uses an indeterminate progress bar at the bottom right while package staging, validation, and archive creation are active.
