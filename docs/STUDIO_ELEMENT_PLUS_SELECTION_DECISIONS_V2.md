# SCADA Builder V2 - Studio Element+ Selection Decisions

Date: 2026-06-15
Status: Approved decision base
Document version: `V2.1.1.0030`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0030` | `PENDING` | Normalisation du header documentaire et rattachement a l'arbre documentaire stable. |
| 2026-06-15 | `V2.1.0.0000` | `2b59efb` | Baseline initiale du depot SCADA Builder V2. |

## 1. Objective

Studio Element+ must behave as a modern SCADA element editor for source and component geometry selection.

Selection behavior is not a visual-only affordance. It is the foundation for moving, grouping, deleting, inspecting, aligning, and publishing Element+ component content without accidental geometry changes or editor-state export.

## 2. Canonical Selection Modifiers

The canonical Studio Element+ multi-selection modifiers are:

1. Plain click selects one element and replaces the current selection.
2. `Shift + clic` adds the clicked element to the current selection.
3. `Alt + clic` removes the clicked element from the current selection.
4. Click on empty workzone clears the selection when no drag selection is active.

`Ctrl`/platform aliases may be supported as compatibility shortcuts, but they must not replace the canonical `Shift` add and `Alt` remove behavior.

## 3. Rectangle Selection

The workzone must support drag rectangle selection.

Required behavior:

1. Plain drag selection replaces the current selection with all intersecting editable elements.
2. `Shift + drag` adds intersecting elements to the current selection.
3. `Alt + drag` removes intersecting elements from the current selection.
4. The selection rectangle is editor UI only and must never be exported into `.sep` component geometry.
5. Selection should use intersection with the rectangle unless a later decision explicitly changes this rule.

## 4. Selection State

Studio Element+ must keep one authoritative selection model shared by:

1. The workzone.
2. The imported Element list.
3. The property panel.
4. Context commands.
5. Future movement, grouping, alignment, and delete commands.

Required behavior:

1. Multi-selection stores selected source/component ids, not only a primary selected item.
2. The last selected item may be used as the active item for property focus.
3. Workzone highlights and Element list selection must stay synchronized.
4. Selection must target real imported/component elements, not visual labels, handles, overlays, or workzone editor artifacts.

## 5. Manipulation Baseline

The following editor behaviors are required before Studio Element+ can be considered a reliable SCADA element editor:

1. Select one element by click.
2. Add elements with `Shift + clic`.
3. Remove elements with `Alt + clic`.
4. Select multiple elements with a drag rectangle.
5. Move a single selected element.
6. Move a multi-selection while preserving relative offsets.
7. Delete, duplicate, copy, and paste selected elements.
8. Group and ungroup selected elements.
9. Align and distribute selected elements.
10. Keep undo/redo entries for destructive or geometry-changing actions.
11. Keep locked elements visible but non-movable and normally non-selectable.
12. Keep hidden elements non-visible and non-selectable.

## 6. Regression Requirements

Any Studio Element+ work touching selection, hit-testing, movement, grouping, properties, or `.sep` export must preserve these regression points:

1. `Shift + clic` adds an element to the selection.
2. `Alt + clic` removes an element from the selection.
3. Drag rectangle supports replace, add with `Shift`, and remove with `Alt`.
4. Workzone and Element list remain synchronized after selection changes.
5. Selection overlays, handles, and drag rectangles are never exported as component geometry.
6. Tests must be added or updated when selection behavior changes.

Associated regression coverage:

```text
tests/ScadaBuilderV2.Tests/ElementStudioSourceRenderingTests.cs
tests/ScadaBuilderV2.Tests/StudioElementPlusContractTests.cs
tests/ScadaBuilderV2.Tests/ElementStudioEditorStateTests.cs
```

## 7. Implemented Slice V2.1.0.0000

The first implementation slice covers:

1. Selection add/remove rules in the workzone.
2. Rectangle selection replace/add/remove rules.
3. Single and multi-selection movement with relative offsets preserved.
4. Keyboard movement with arrow keys and accelerated movement with `Shift`.
5. Delete, copy, paste, duplicate, and select-all commands.
6. Mutable Studio item bounds so `.sep` output uses the edited positions.
7. Regression tests for each implemented operation and a combined alternating-operation scenario.

## 8. Implemented Slice V2.1.0.0001

The second implementation slice covers:

1. Undo and redo snapshots for Studio Element+ editing mutations.
2. Logical group and ungroup state for selected Studio items.
3. Lock and unlock behavior, with locked items excluded from normal selection and movement.
4. Hide and show behavior, with hidden items excluded from selection, workzone rendering, and `.sep` export.
5. Align left/top and distribute horizontal/vertical commands exposed in Studio Element+.
6. Additional editor-state tests for undo/redo, group/ungroup, lock, visibility, alignment, distribution, and combined alternating operations.

## 9. Implemented Slice V2.1.0.0002

The third implementation slice covers:

1. Workzone resize handles on selected, unlocked items.
2. Resize drag messages from WebView to the Studio workspace model.
3. Selection resizing while preserving item positions.
4. Common selection geometry editing for X, Y, width, and height.
5. Equalize selected width and height commands.
6. Full horizontal/vertical alignment commands exposed in the Studio Element+ ribbon.
7. Regression tests for resize, common bounds, equalized sizes, and workzone resize contract.

## 10. Implemented Slice V2.1.0.0003

The final functional slice before final-test/polish covers:

1. Visual group frames for grouped Studio Element+ items in the workzone.
2. Element list state suffixes for hidden, locked, and grouped items.
3. Export metadata for visible grouped/locked items.
4. Regression contracts for group-frame rendering and state metadata.

Final test pass and UI polish are intentionally reserved for the next tranche.

## 11. Final Test And Polish Slice V2.1.0.0004

The final validation and polish slice covers:

1. `Escape` clears the active workzone selection and keeps the Element list synchronized.
2. Common geometry fields are populated from the current selection when values match, and left blank for mixed values.
3. The Structure tab exposes live visibility, lock, group, and selection summaries instead of placeholder content.
4. Regression contracts lock the final interaction surface so future Studio Element+ work preserves the editor baseline.
