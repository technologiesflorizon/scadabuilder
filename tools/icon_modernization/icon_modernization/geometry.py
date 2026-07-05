from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class Point:
    x: float
    y: float


@dataclass(frozen=True)
class BBox:
    min_x: float
    min_y: float
    max_x: float
    max_y: float

    @property
    def width(self) -> float:
        return self.max_x - self.min_x

    @property
    def height(self) -> float:
        return self.max_y - self.min_y


def compute_bbox(vertices: list[Point]) -> BBox:
    if not vertices:
        raise ValueError("compute_bbox requires at least one vertex")
    xs = [v.x for v in vertices]
    ys = [v.y for v in vertices]
    return BBox(min_x=min(xs), min_y=min(ys), max_x=max(xs), max_y=max(ys))


@dataclass(frozen=True)
class JunctionPoint:
    edge: str
    fraction: float


def junction_points(vertices: list[Point], bbox: BBox, epsilon: float = 0.5) -> list[JunctionPoint]:
    if bbox.width <= 0 or bbox.height <= 0:
        raise ValueError("junction_points requires a non-degenerate bbox")

    points: list[JunctionPoint] = []
    for v in vertices:
        if abs(v.x - bbox.min_x) <= epsilon:
            points.append(JunctionPoint(edge="left", fraction=(v.y - bbox.min_y) / bbox.height))
        if abs(v.x - bbox.max_x) <= epsilon:
            points.append(JunctionPoint(edge="right", fraction=(v.y - bbox.min_y) / bbox.height))
        if abs(v.y - bbox.min_y) <= epsilon:
            points.append(JunctionPoint(edge="top", fraction=(v.x - bbox.min_x) / bbox.width))
        if abs(v.y - bbox.max_y) <= epsilon:
            points.append(JunctionPoint(edge="bottom", fraction=(v.x - bbox.min_x) / bbox.width))
    return points
