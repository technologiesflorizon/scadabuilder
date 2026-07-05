import unittest

from icon_modernization.geometry import JunctionPoint
from icon_modernization.junctions import compare_junction_points, junction_points_for_svg


class TestJunctionPointsForSvg(unittest.TestCase):
    def test_horizontal_pipe_touches_left_and_right_edges(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg" width="100" height="10"><line x1="0" y1="5" x2="100" y2="5"/></svg>'
        points = junction_points_for_svg(svg)
        self.assertIn(JunctionPoint(edge="left", fraction=0.5), points)
        self.assertIn(JunctionPoint(edge="right", fraction=0.5), points)


class TestCompareJunctionPoints(unittest.TestCase):
    def test_exact_match_is_ok(self):
        original = [JunctionPoint(edge="left", fraction=0.5)]
        candidate = [JunctionPoint(edge="left", fraction=0.5)]
        result = compare_junction_points(original, candidate, tolerance_fraction=0.02)
        self.assertTrue(result.ok)
        self.assertEqual(result.matched, original)

    def test_within_tolerance_is_ok(self):
        original = [JunctionPoint(edge="left", fraction=0.50)]
        candidate = [JunctionPoint(edge="left", fraction=0.51)]
        result = compare_junction_points(original, candidate, tolerance_fraction=0.02)
        self.assertTrue(result.ok)

    def test_beyond_tolerance_reports_missing_and_extra(self):
        original = [JunctionPoint(edge="left", fraction=0.10)]
        candidate = [JunctionPoint(edge="left", fraction=0.90)]
        result = compare_junction_points(original, candidate, tolerance_fraction=0.02)
        self.assertFalse(result.ok)
        self.assertEqual(result.missing, original)
        self.assertEqual(result.extra, candidate)

    def test_different_edge_does_not_match(self):
        original = [JunctionPoint(edge="left", fraction=0.5)]
        candidate = [JunctionPoint(edge="right", fraction=0.5)]
        result = compare_junction_points(original, candidate, tolerance_fraction=0.02)
        self.assertFalse(result.ok)
        self.assertEqual(result.missing, original)
        self.assertEqual(result.extra, candidate)

    def test_nearest_candidate_is_matched_on_same_edge(self):
        original = [JunctionPoint(edge="top", fraction=0.30)]
        candidate = [JunctionPoint(edge="top", fraction=0.32), JunctionPoint(edge="top", fraction=0.90)]
        result = compare_junction_points(original, candidate, tolerance_fraction=0.05)
        self.assertTrue(JunctionPoint(edge="top", fraction=0.30) in result.matched)
        self.assertEqual(result.extra, [JunctionPoint(edge="top", fraction=0.90)])


if __name__ == "__main__":
    unittest.main()
