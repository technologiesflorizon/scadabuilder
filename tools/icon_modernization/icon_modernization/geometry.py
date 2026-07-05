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
