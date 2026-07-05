from __future__ import annotations

import re
import xml.etree.ElementTree as ET

from icon_modernization.geometry import Point


class UnsupportedTransformError(ValueError):
    pass


class UnsupportedPathCommandError(ValueError):
    pass


_PATH_TOKEN_RE = re.compile(r"([A-Za-z])|(-?\d*\.?\d+(?:[eE][-+]?\d+)?)")


def _tokenize_path(d: str) -> list[str]:
    tokens = []
    for cmd, num in _PATH_TOKEN_RE.findall(d):
        if cmd:
            tokens.append(cmd)
        elif num:
            tokens.append(num)
    return tokens


def _parse_path_vertices(d: str) -> list[Point]:
    tokens = _tokenize_path(d)
    vertices: list[Point] = []
    i = 0
    cur_x = cur_y = 0.0
    cmd = None

    def read_floats(n: int) -> list[float]:
        nonlocal i
        vals = [float(tokens[i + k]) for k in range(n)]
        i += n
        return vals

    while i < len(tokens):
        token = tokens[i]
        if token.isalpha():
            cmd = token
            i += 1
            continue
        if cmd is None:
            raise UnsupportedPathCommandError(f"Path data must start with a command: {d!r}")

        if cmd in ("M", "L"):
            cur_x, cur_y = read_floats(2)
            vertices.append(Point(cur_x, cur_y))
        elif cmd in ("m", "l"):
            dx, dy = read_floats(2)
            cur_x, cur_y = cur_x + dx, cur_y + dy
            vertices.append(Point(cur_x, cur_y))
        elif cmd == "H":
            (cur_x,) = read_floats(1)
            vertices.append(Point(cur_x, cur_y))
        elif cmd == "h":
            (dx,) = read_floats(1)
            cur_x = cur_x + dx
            vertices.append(Point(cur_x, cur_y))
        elif cmd == "V":
            (cur_y,) = read_floats(1)
            vertices.append(Point(cur_x, cur_y))
        elif cmd == "v":
            (dy,) = read_floats(1)
            cur_y = cur_y + dy
            vertices.append(Point(cur_x, cur_y))
        elif cmd == "C":
            _, _, _, _, cur_x, cur_y = read_floats(6)
            vertices.append(Point(cur_x, cur_y))
        elif cmd == "c":
            _, _, _, _, dx, dy = read_floats(6)
            cur_x, cur_y = cur_x + dx, cur_y + dy
            vertices.append(Point(cur_x, cur_y))
        elif cmd == "Q":
            _, _, cur_x, cur_y = read_floats(4)
            vertices.append(Point(cur_x, cur_y))
        elif cmd == "q":
            _, _, dx, dy = read_floats(4)
            cur_x, cur_y = cur_x + dx, cur_y + dy
            vertices.append(Point(cur_x, cur_y))
        elif cmd in ("Z", "z"):
            cmd = None
        else:
            raise UnsupportedPathCommandError(f"Unsupported path command {cmd!r} in {d!r}")

    return vertices


def _strip_ns(tag: str) -> str:
    return tag.split("}", 1)[-1] if "}" in tag else tag


def _parse_points_attr(points_attr: str) -> list[Point]:
    tokens = points_attr.replace(",", " ").split()
    return [Point(float(tokens[i]), float(tokens[i + 1])) for i in range(0, len(tokens) - 1, 2)]


def _parse_translate(transform: str) -> tuple[float, float]:
    transform = transform.strip()
    if not transform.startswith("translate(") or not transform.endswith(")"):
        raise UnsupportedTransformError(f"Unsupported transform: {transform!r}")
    inner = transform[len("translate("):-1]
    parts = [p for p in inner.replace(",", " ").split() if p]
    if len(parts) == 1:
        return float(parts[0]), 0.0
    if len(parts) == 2:
        return float(parts[0]), float(parts[1])
    raise UnsupportedTransformError(f"Unsupported transform: {transform!r}")


def extract_vertices(svg_markup: str) -> list[Point]:
    root = ET.fromstring(svg_markup)
    return _extract_from_element(root, offset_x=0.0, offset_y=0.0)


def _extract_from_element(element: ET.Element, offset_x: float, offset_y: float) -> list[Point]:
    tag = _strip_ns(element.tag)
    dx, dy = offset_x, offset_y
    transform = element.get("transform")
    if transform:
        tx, ty = _parse_translate(transform)
        dx, dy = offset_x + tx, offset_y + ty

    vertices: list[Point] = []

    if tag == "line":
        x1, y1 = float(element.get("x1", "0")), float(element.get("y1", "0"))
        x2, y2 = float(element.get("x2", "0")), float(element.get("y2", "0"))
        vertices += [Point(x1 + dx, y1 + dy), Point(x2 + dx, y2 + dy)]
    elif tag in ("polyline", "polygon"):
        for p in _parse_points_attr(element.get("points", "")):
            vertices.append(Point(p.x + dx, p.y + dy))
    elif tag == "rect":
        x, y = float(element.get("x", "0")), float(element.get("y", "0"))
        w, h = float(element.get("width", "0")), float(element.get("height", "0"))
        for cx, cy in ((x, y), (x + w, y), (x, y + h), (x + w, y + h)):
            vertices.append(Point(cx + dx, cy + dy))
    elif tag == "path":
        for p in _parse_path_vertices(element.get("d", "")):
            vertices.append(Point(p.x + dx, p.y + dy))

    for child in element:
        vertices += _extract_from_element(child, dx, dy)

    return vertices
