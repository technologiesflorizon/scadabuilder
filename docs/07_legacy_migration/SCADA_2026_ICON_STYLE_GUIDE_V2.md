# SCADA Builder V2 - SCADA 2026 Icon Style Guide

Date: 2026-07-05
Status: Active style guide
Document version: `V2.1.3.0004`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-05 | `V2.1.3.0004` | `PENDING` | Premiere icone approuvee (Ventilateur.sep, famille ventilateur) via la boucle interactive; ajout de la regle 6 interdisant les transformations autres que `translate` (contrainte decouverte en pratique: `tools/icon_modernization` rejette `rotate`/`scale`/`matrix`). |
| 2026-07-05 | `V2.1.3.0003` | `PENDING` | Creation du guide de style visuel pour la modernisation des icones Element+ (DEC-0033). |

## 1. Purpose

This guide constrains every Element+ icon artwork produced under
`docs/07_legacy_migration/MODERNIZATION_WORKFLOW_V2.md`, whether drafted by
Claude or by hand. It exists because an unconstrained AI redraw
(`Ventilateur2.sep`) produced a generic pastel "flat icon" look inconsistent
with an industrial SCADA interface, and inconsistent from one icon to the
next.

## 2. State Colors

Reuse the state semantics already active in the legacy `Condenser` runtime
pattern (`08_web_modernized/html_pages/win00008_updated.html`) rather than
inventing a new state vocabulary: `IsActive`, `IsFaulty`, `IsWarning`,
`IsCritAlarm`, `IsOffline`, `IsUnknown`. Per
`docs/06_ui_ux/ICON_STRATEGY_V2.md` guardrail 4, state must not rely on
color alone - pair every state color with a distinct shape/glyph change
(for example, a fault triangle badge, not just a red fill).

## 3. Drawing Rules

1. Flat fills only. No gradients, no drop shadows, no bevels - the pastel
   gradient look in `Ventilateur2.sep` is explicitly rejected.
2. Constant stroke width across an icon (no tapered or variable-width
   strokes).
3. Native inline SVG primitives only (`<path>`, `<line>`, `<rect>`,
   `<polyline>`, `<polygon>`, `<circle>`, `<ellipse>`). Never a raster image
   or an SVG re-encoded and embedded inside an `<image>` tag - this blocks
   any future CSS/state-driven recoloring.
4. Stay within the primitive subset `tools/icon_modernization` can verify:
   `M/L/H/V/C/Q/Z` path commands, no arcs, no `S`/`T` curve shorthand. If an
   icon seems to need an arc, approximate it with `C`/`Q` curves instead.
5. Every icon's outline must reach the edge of its own `viewBox` at the
   exact points needed to visually connect to neighboring icons (pipe ends,
   valve stems). These are the icon's junction points and are verified by
   `tools/icon_modernization` per
   `docs/07_legacy_migration/MODERNIZATION_WORKFLOW_V2.md` section 3. Icons
   with no external connection points (e.g. a free-standing fan glyph) are
   expected to produce zero junction points - this is a pass, not a gap.
6. No `transform` other than `translate` anywhere in the icon, including on
   nested elements. `tools/icon_modernization` raises a typed error on
   `rotate`/`scale`/`matrix`. For repeated rotated shapes (e.g. fan blades),
   compute the rotated coordinates by hand (or by script) and hardcode them
   as absolute point/path coordinates instead of using `transform="rotate(...)"`.

## 4. Reference Icons

Icons listed here are the imposed visual reference for every new icon of
the same family. An icon is added to this list only after a human approves
it during the interactive modernization loop.

| Family | `.sep` path | Approved on |
| --- | --- | --- |
| Fan / ventilateur | `projects/AMR_REF_SCADA_V2/library/elements/Ventilateur.sep` | 2026-07-05 |
