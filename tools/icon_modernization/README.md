# icon_modernization

Geometry-verification tool for the SCADA 2026 icon modernization workflow
(`docs/07_legacy_migration/MODERNIZATION_WORKFLOW_V2.md`).

It does not generate icon artwork. It verifies that a modernized candidate
SVG preserves the **junction points** of the original legacy icon - the
positions where the icon's outline touches its own bounding-box edge. This
catches the class of regression seen in `win00008_updated.html`, where
AI-regenerated piping kept the correct bounding box but no longer touched
its neighbors at the same relative position.

## Requirements

Python 3.13 standard library only. No virtualenv, no `pip install`. Verified
against `C:\Python313\python.exe`.

## Supported SVG subset

`<line>`, `<rect>`, `<polyline>`, `<polygon>`, `<path>` using only
`M/L/H/V/C/Q/Z` (upper or lower case), and `<g transform="translate(tx[,ty])">`
for grouping. Anything else (arcs, `S`/`T` curve shorthand, `scale`/`rotate`/
`matrix` transforms) raises `UnsupportedPathCommandError` or
`UnsupportedTransformError` rather than silently producing wrong geometry.
This mirrors the SCADA 2026 style guide's "flat fills, straight/curved
strokes, no arcs" rule - if an icon needs an unsupported primitive, redraw it
within the supported subset rather than widening the parser.

## Usage during an interactive modernization session

1. Extract (or locate) the legacy source icon as a standalone SVG file with
   `width`/`height` matching its `Bounds` in the `.sep`.
2. Draft the modernized candidate SVG, matching the SCADA 2026 style guide
   (`docs/07_legacy_migration/SCADA_2026_ICON_STYLE_GUIDE_V2.md`).
3. Run:

   ```bash
   "C:/Python313/python.exe" -m icon_modernization.cli check-junctions original.svg candidate.svg
   ```

4. Exit code `0` means every junction point on the original is matched by a
   candidate junction point on the same edge within 2 pixels (converted to a
   fraction of the candidate's own width/height). Exit code `1` lists
   `MISSING` (present in the original, absent in the candidate - the
   regression that broke win00008's piping) and `EXTRA` (present in the
   candidate, absent in the original - usually a sign the candidate drifted
   in scale or added spurious detail touching the frame) junction points.
5. Fix the candidate's geometry (not the tolerance) and re-run until exit
   code `0`, then proceed to the human visual-style review before accepting
   the `.sep`.

## Tests

```bash
cd tools/icon_modernization
"C:/Python313/python.exe" -m unittest discover -s tests -t . -v
```
