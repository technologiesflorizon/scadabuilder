from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET

from icon_modernization.junctions import compare_junction_points, junction_points_for_svg


def _read_svg_dimensions(svg_markup: str) -> tuple[float, float]:
    root = ET.fromstring(svg_markup)
    return float(root.get("width")), float(root.get("height"))


def run_check_junctions(original_path: str, candidate_path: str, tolerance_px: float) -> int:
    with open(original_path, "r", encoding="utf-8") as f:
        original_svg = f.read()
    with open(candidate_path, "r", encoding="utf-8") as f:
        candidate_svg = f.read()

    original_points = junction_points_for_svg(original_svg)
    candidate_points = junction_points_for_svg(candidate_svg)

    width, height = _read_svg_dimensions(candidate_svg)
    tolerance_fraction = tolerance_px / min(width, height)

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
