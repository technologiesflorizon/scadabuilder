from __future__ import annotations

import xml.etree.ElementTree as ET
from dataclasses import dataclass

from icon_modernization.geometry import BBox, JunctionPoint, junction_points
from icon_modernization.svg_parse import extract_vertices


def _read_svg_bbox(svg_markup: str) -> BBox:
    root = ET.fromstring(svg_markup)
    width = float(root.get("width"))
    height = float(root.get("height"))
    return BBox(min_x=0.0, min_y=0.0, max_x=width, max_y=height)


def junction_points_for_svg(svg_markup: str, epsilon: float = 0.5) -> list[JunctionPoint]:
    vertices = extract_vertices(svg_markup)
    bbox = _read_svg_bbox(svg_markup)
    return junction_points(vertices, bbox, epsilon=epsilon)


@dataclass(frozen=True)
class ComparisonResult:
    matched: list[JunctionPoint]
    missing: list[JunctionPoint]
    extra: list[JunctionPoint]

    @property
    def ok(self) -> bool:
        return not self.missing and not self.extra


def compare_junction_points(
    original: list[JunctionPoint],
    candidate: list[JunctionPoint],
    tolerance_fraction: dict[str, float],
) -> ComparisonResult:
    remaining_candidates = list(candidate)
    matched: list[JunctionPoint] = []
    missing: list[JunctionPoint] = []

    for point in original:
        best_index = None
        best_distance = None
        edge_tolerance = tolerance_fraction[point.edge]
        for index, cand in enumerate(remaining_candidates):
            if cand.edge != point.edge:
                continue
            distance = abs(cand.fraction - point.fraction)
            if distance <= edge_tolerance and (best_distance is None or distance < best_distance):
                best_distance = distance
                best_index = index
        if best_index is None:
            missing.append(point)
        else:
            matched.append(point)
            remaining_candidates.pop(best_index)

    return ComparisonResult(matched=matched, missing=missing, extra=remaining_candidates)
