# SCADA Builder V2 - Studio Element+ SEP Contract

Date: 2026-06-16
Status: Active `.sep` package contract
Document version: `V2.1.1.0039`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du contrat `.sep` Studio Element+. |

## 1. Contract

`.sep` is the editable Studio Element+ component source format. One `.sep` file contains exactly one Element+ component.

The `.sep` output is the editable Element+ component source file.

The `.sep` shared library is stored under:

```text
projects/<project-id>/library/elements/*.sep
```

`.sep` is the shared library format consumed by Studio Element+ and SCADA Builder V2.

## 2. Rules

1. `.sep` must preserve component metadata, geometry, source traceability, and embedded assets required to reopen the component.
2. `.sep` must not infer its save target from legacy source page paths.
3. Hidden source items are not exported as visible component geometry.
4. Editor-only overlays and workzone state are excluded.
5. Component packages require schema/version metadata.
6. The Studio workzone is editor state and must not become exported Element+ geometry.
7. Legacy selection UI artifacts must not become part of the component payload.
8. The Studio workzone/canvas/viewport is an editor surface and must not be exported as part of the Element+ payload.
9. Ensure the workzone, zoom level, pan position, selection rectangle, and editor UI overlays are not exported as component geometry.

## 3. Drawing Primitive Contract

Drawing tools create real component primitives, not temporary editor overlays.

Required drawing tool behavior:

1. Make drawing tools functional:
2. `Ligne`: create line primitives.
3. `Polyline`: create editable polyline primitives.
4. `Rectangle`: create editable rectangle primitives.
5. `Polygone`: create editable polygon primitives.
6. `Image`: import and embed raster images into the `.sep`.
7. Ensure created primitives become part of the Element+ component model, not editor-only overlays.

## 4. Related Tests

1. `tests/ScadaBuilderV2.Tests/StudioElementPlusContractTests.cs`
2. `tests/ScadaBuilderV2.Tests/ElementStudioImportPackageWriterTests.cs`
