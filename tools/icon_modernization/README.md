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

`<line>`, `<rect>`, `<circle>`, `<ellipse>`, `<polyline>`, `<polygon>`,
`<path>` using only `M/L/H/V/C/Q/Z` (upper or lower case), and
`<g transform="translate(tx[,ty])">` for grouping. Anything else (arcs,
`S`/`T` curve shorthand, `scale`/`rotate`/`matrix` transforms) raises
`UnsupportedPathCommandError` or `UnsupportedTransformError` rather than
silently producing wrong geometry. This mirrors the SCADA 2026 style guide's
"flat fills, straight/curved strokes, no arcs" rule - if an icon needs an
unsupported primitive, redraw it within the supported subset rather than
widening the parser.

For `<circle>`/`<ellipse>`, only the 4 cardinal points (west/east/north/south)
are extracted as candidate junction points - this is mathematically
sufficient since every other point on a circle or ellipse lies strictly
inside its own bounding box.

The SVG root of both the original and candidate files **must** declare
numeric `width` and `height` attributes (e.g. `width="100" height="10"`).
`viewBox`-only SVGs and unit-suffixed values (e.g. `width="100px"`) are not
supported and are rejected with a clear error rather than silently
misbehaving.

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
   candidate junction point on the same edge within 2 pixels. The pixel
   tolerance is converted to a per-edge fraction using the candidate's own
   dimensions along the axis each edge's junctions are measured on: top/bottom
   junction fractions run along `width`, so their tolerance is
   `tolerance_px / width`; left/right junction fractions run along `height`,
   so their tolerance is `tolerance_px / height`. This keeps the tolerance
   tight on both axes even for long, thin, anisotropic icons (e.g. a
   1000x10px pipe), where a single tolerance derived from `min(width, height)`
   would otherwise be far too loose along the long axis.

   Exit code `1` lists `MISSING` (present in the original, absent in the
   candidate - the regression that broke win00008's piping) and `EXTRA`
   (present in the candidate, absent in the original - usually a sign the
   candidate drifted in scale or added spurious detail touching the frame)
   junction points.

   Exit code `2` means the tool could not evaluate the SVGs at all - a
   missing input file, malformed XML, an SVG root missing numeric
   `width`/`height` attributes, or an unsupported path command/transform. A
   single clear message is printed to stderr; no traceback.
5. Fix the candidate's geometry (not the tolerance) and re-run until exit
   code `0`, then proceed to the human visual-style review before accepting
   the `.sep`.

## Tests

```bash
cd tools/icon_modernization
"C:/Python313/python.exe" -m unittest discover -s tests -t . -v
```
