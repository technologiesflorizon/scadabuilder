# SCADA Builder V2 - Studio Element+ Selection Contract

Date: 2026-06-16
Status: Approved decision base
Document version: `V2.1.1.0039`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Migration du contrat de selection Studio Element+ vers le nouveau module documentaire. |

## 1. Contract

Studio Element+ selection must behave as a modern component editor selection model and preserve canonical modifiers.

## 2. Canonical Modifiers

1. Plain click selects one element and replaces the current selection.
2. `Shift + clic` adds the clicked element to the current selection.
3. `Alt + clic` removes the clicked element from the current selection.
4. Click on empty workzone clears selection when no drag selection is active.

`Ctrl` may exist as compatibility, but it must not replace `Shift` add and `Alt` remove.

## 3. Rectangle Selection

1. Plain drag replaces selection with intersecting editable elements.
2. `Shift + drag` adds intersecting elements to the current selection.
3. `Alt + drag` removes intersecting elements from the current selection.
4. Selection uses intersection unless a later decision changes it.
5. Selection rectangles are editor UI only and must never be exported into `.sep`.

## 4. Export Guardrail

The following must never become `.sep` component geometry:

1. Selection overlays.
2. Handles.
3. Drag rectangles.
4. Workzone/canvas frame.
5. Zoom and pan state.
6. Diagnostics and test UI.
7. Temporary outlines or labels.

## 5. Regression Rule

Tests must be added or updated when selection behavior changes.

## 6. Implemented Slice V2.1.0.0000

The first implementation slice covers selection add/remove rules, rectangle selection replace/add/remove rules, movement, keyboard movement, delete, copy, paste, duplicate, select-all, mutable bounds, and regression tests.

## 7. Implemented Slice V2.1.0.0001

The second implementation slice covers undo/redo snapshots, group/ungroup state, lock/unlock, hide/show, alignment, distribution, and editor-state tests.

## 8. Implemented Slice V2.1.0.0002

The third implementation slice covers resize handles, resize drag messages, selection resizing, common geometry editing, equalize width/height, and resize regression coverage.

## 9. Implemented Slice V2.1.0.0003

The fourth implementation slice covers visual group frames, Element list state suffixes, export metadata for visible grouped/locked items, and group-frame regression contracts.

## 10. Final Test And Polish Slice V2.1.0.0004

The final validation and polish slice covers:

1. `Escape` clears the active workzone selection and keeps the Element list synchronized.
2. Common geometry fields are populated from the current selection when values match, and left blank for mixed values.
3. The Structure tab exposes live visibility, lock, group, and selection summaries instead of placeholder content.
4. Regression contracts lock the final interaction surface so future Studio Element+ work preserves the editor baseline.

## 11. Related Decision

`DEC-0008` - Studio Element+ Canonical Selection Modifiers.

## 12. Related Tests

1. `tests/ScadaBuilderV2.Tests/ElementStudioEditorStateTests.cs`
2. `tests/ScadaBuilderV2.Tests/ElementStudioSourceRenderingTests.cs`
3. `tests/ScadaBuilderV2.Tests/StudioElementPlusContractTests.cs`
