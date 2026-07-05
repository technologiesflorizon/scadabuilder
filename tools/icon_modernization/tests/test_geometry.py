import unittest

from icon_modernization.geometry import BBox, JunctionPoint, Point, compute_bbox, junction_points


class TestComputeBBox(unittest.TestCase):
    def test_single_point(self):
        bbox = compute_bbox([Point(3, 4)])
        self.assertEqual((bbox.min_x, bbox.min_y, bbox.max_x, bbox.max_y), (3, 4, 3, 4))

    def test_multiple_points(self):
        bbox = compute_bbox([Point(0, 0), Point(10, 5), Point(-2, 8)])
        self.assertEqual((bbox.min_x, bbox.min_y, bbox.max_x, bbox.max_y), (-2, 0, 10, 8))

    def test_width_and_height(self):
        bbox = compute_bbox([Point(0, 0), Point(10, 4)])
        self.assertEqual(bbox.width, 10)
        self.assertEqual(bbox.height, 4)

    def test_empty_raises(self):
        with self.assertRaises(ValueError):
            compute_bbox([])


class TestJunctionPoints(unittest.TestCase):
    def test_point_on_left_edge(self):
        bbox = BBox(min_x=0, min_y=0, max_x=10, max_y=20)
        points = junction_points([Point(0, 5)], bbox)
        self.assertEqual(points, [JunctionPoint(edge="left", fraction=0.25)])

    def test_point_on_right_edge(self):
        bbox = BBox(min_x=0, min_y=0, max_x=10, max_y=20)
        points = junction_points([Point(10, 15)], bbox)
        self.assertEqual(points, [JunctionPoint(edge="right", fraction=0.75)])

    def test_point_on_top_and_bottom_edges(self):
        bbox = BBox(min_x=0, min_y=0, max_x=10, max_y=20)
        top = junction_points([Point(2, 0)], bbox)
        bottom = junction_points([Point(8, 20)], bbox)
        self.assertEqual(top, [JunctionPoint(edge="top", fraction=0.2)])
        self.assertEqual(bottom, [JunctionPoint(edge="bottom", fraction=0.8)])

    def test_corner_point_matches_two_edges(self):
        bbox = BBox(min_x=0, min_y=0, max_x=10, max_y=20)
        points = junction_points([Point(0, 0)], bbox)
        self.assertEqual(
            sorted(points, key=lambda p: p.edge),
            sorted([JunctionPoint(edge="left", fraction=0.0), JunctionPoint(edge="top", fraction=0.0)], key=lambda p: p.edge),
        )

    def test_interior_point_produces_no_junction(self):
        bbox = BBox(min_x=0, min_y=0, max_x=10, max_y=20)
        points = junction_points([Point(5, 10)], bbox)
        self.assertEqual(points, [])

    def test_epsilon_tolerance(self):
        bbox = BBox(min_x=0, min_y=0, max_x=10, max_y=20)
        points = junction_points([Point(0.3, 5)], bbox, epsilon=0.5)
        self.assertEqual(points, [JunctionPoint(edge="left", fraction=0.25)])

    def test_degenerate_bbox_raises(self):
        bbox = compute_bbox([Point(3, 3), Point(3, 9)])
        with self.assertRaises(ValueError):
            junction_points([Point(3, 5)], bbox)


if __name__ == "__main__":
    unittest.main()
