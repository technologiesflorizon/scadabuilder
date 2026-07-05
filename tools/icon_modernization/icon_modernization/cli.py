from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET

from icon_modernization.junctions import compare_junction_points, junction_points_for_svg
from icon_modernization.svg_parse import UnsupportedPathCommandError, UnsupportedTransformError


def _read_svg_dimensions(svg_markup: str, path: str) -> tuple[float, float]:
    root = ET.fromstring(svg_markup)
    width_attr = root.get("width")
    height_attr = root.get("height")
    try:
        width = float(width_attr) if width_attr is not None else None
        height = float(height_attr) if height_attr is not None else None
    except ValueError:
        width = height = None
    if width is None or height is None:
        raise ValueError(
            f"{path}: SVG root must declare numeric 'width' and 'height' attributes "
            "(viewBox-only or unit-suffixed values like '100px' are not supported)"
        )
    return width, height


def run_check_junctions(original_path: str, candidate_path: str, tolerance_px: float) -> int:
    try:
        with open(original_path, "r", encoding="utf-8") as f:
            original_svg = f.read()
    except FileNotFoundError:
        print(f"ERROR: original file not found: {original_path}", file=sys.stderr)
        return 2
    try:
        with open(candidate_path, "r", encoding="utf-8") as f:
            candidate_svg = f.read()
    except FileNotFoundError:
        print(f"ERROR: candidate file not found: {candidate_path}", file=sys.stderr)
        return 2

    # Validate each SVG's XML well-formedness and numeric width/height up front, so
    # errors can be attributed to the correct file rather than surfacing as a raw
    # traceback from deep inside vertex/bbox extraction.
    try:
        _read_svg_dimensions(original_svg, original_path)
    except ET.ParseError as exc:
        print(f"ERROR: malformed XML in {original_path}: {exc}", file=sys.stderr)
        return 2
    except ValueError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2

    try:
        width, height = _read_svg_dimensions(candidate_svg, candidate_path)
    except ET.ParseError as exc:
        print(f"ERROR: malformed XML in {candidate_path}: {exc}", file=sys.stderr)
        return 2
    except ValueError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2

    try:
        original_points = junction_points_for_svg(original_svg)
        candidate_points = junction_points_for_svg(candidate_svg)
    except (UnsupportedPathCommandError, UnsupportedTransformError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2

    tol_x = tolerance_px / width
    tol_y = tolerance_px / height
    tolerance_fraction = {"top": tol_x, "bottom": tol_x, "left": tol_y, "right": tol_y}

    result = compare_junction_points(original_points, candidate_points, tolerance_fraction)

    print(f"Matched: {len(result.matched)}")
    for point in result.missing:
        print(f"MISSING junction on {point.edge} edge at {point.fraction:.3f}")
    for point in result.extra:
        print(f"EXTRA junction on {point.edge} edge at {point.fraction:.3f}")

    if result.ok:
        print("OK: junction points preserved within tolerance")
        return 0
    print("FAIL: junction points diverge beyond tolerance")
    return 1


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="icon_modernization")
    subparsers = parser.add_subparsers(dest="command", required=True)

    check = subparsers.add_parser("check-junctions", help="Compare junction points between two SVG files")
    check.add_argument("original", help="Path to the original (legacy) SVG file")
    check.add_argument("candidate", help="Path to the modernized candidate SVG file")
    check.add_argument("--tolerance-px", type=float, default=2.0)

    args = parser.parse_args(argv)

    if args.command == "check-junctions":
        return run_check_junctions(args.original, args.candidate, args.tolerance_px)

    parser.error(f"Unknown command: {args.command}")
    return 2


if __name__ == "__main__":
    sys.exit(main())
