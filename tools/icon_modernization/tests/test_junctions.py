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
        result = compare_junction_points(
            original,
            candidate,
            tolerance_fraction={"top": 0.02, "right": 0.02, "bottom": 0.02, "left": 0.02},
        )
        self.assertTrue(result.ok)
        self.assertEqual(result.matched, original)

    def test_within_tolerance_is_ok(self):
        original = [JunctionPoint(edge="left", fraction=0.50)]
        candidate = [JunctionPoint(edge="left", fraction=0.51)]
        result = compare_junction_points(
            original,
            candidate,
            tolerance_fraction={"top": 0.02, "right": 0.02, "bottom": 0.02, "left": 0.02},
        )
        self.assertTrue(result.ok)

    def test_beyond_tolerance_reports_missing_and_extra(self):
        original = [JunctionPoint(edge="left", fraction=0.10)]
        candidate = [JunctionPoint(edge="left", fraction=0.90)]
        result = compare_junction_points(
            original,
            candidate,
            tolerance_fraction={"top": 0.02, "right": 0.02, "bottom": 0.02, "left": 0.02},
        )
        self.assertFalse(result.ok)
        self.assertEqual(result.missing, original)
        self.assertEqual(result.extra, candidate)

    def test_different_edge_does_not_match(self):
        original = [JunctionPoint(edge="left", fraction=0.5)]
        candidate = [JunctionPoint(edge="right", fraction=0.5)]
        result = compare_junction_points(
            original,
            candidate,
            tolerance_fraction={"top": 0.02, "right": 0.02, "bottom": 0.02, "left": 0.02},
        )
        self.assertFalse(result.ok)
        self.assertEqual(result.missing, original)
        self.assertEqual(result.extra, candidate)

    def test_nearest_candidate_is_matched_on_same_edge(self):
        original = [JunctionPoint(edge="top", fraction=0.30)]
        candidate = [JunctionPoint(edge="top", fraction=0.32), JunctionPoint(edge="top", fraction=0.90)]
        result = compare_junction_points(
            original,
            candidate,
            tolerance_fraction={"top": 0.05, "right": 0.05, "bottom": 0.05, "left": 0.05},
        )
        self.assertTrue(JunctionPoint(edge="top", fraction=0.30) in result.matched)
        self.assertEqual(result.extra, [JunctionPoint(edge="top", fraction=0.90)])

    def test_per_edge_tolerance_uses_matching_points_own_edge(self):
        # left/right use a tight tolerance, top/bottom use a loose one; a
        # left-edge point outside the tight tolerance must fail even though
        # it would pass under the loose top/bottom tolerance.
        original = [JunctionPoint(edge="left", fraction=0.10), JunctionPoint(edge="top", fraction=0.10)]
        candidate = [JunctionPoint(edge="left", fraction=0.13), JunctionPoint(edge="top", fraction=0.13)]
        result = compare_junction_points(
            original,
            candidate,
            tolerance_fraction={"top": 0.05, "right": 0.05, "bottom": 0.05, "left": 0.01},
        )
        self.assertIn(JunctionPoint(edge="left", fraction=0.10), result.missing)
        self.assertIn(JunctionPoint(edge="top", fraction=0.10), result.matched)

    def test_anisotropic_long_thin_icon_top_edge_drift_fails_with_per_edge_tolerance(self):
        # A 1000-wide/10-tall pipe icon: a top-edge junction drifting by 50px
        # (5% of width, far more than the intended 2px) must be reported as a
        # mismatch. Under the old single scalar tolerance_fraction computed as
        # tolerance_px / min(width, height) = 2 / 10 = 0.2, this drift (0.05 of
        # width) would have incorrectly passed.
        original_svg = (
            '<svg xmlns="http://www.w3.org/2000/svg" width="1000" height="10">'
            '<line x1="500" y1="0" x2="0" y2="5"/></svg>'
        )
        candidate_svg = (
            '<svg xmlns="http://www.w3.org/2000/svg" width="1000" height="10">'
            '<line x1="550" y1="0" x2="0" y2="5"/></svg>'
        )
        original_points = junction_points_for_svg(original_svg)
        candidate_points = junction_points_for_svg(candidate_svg)

        tolerance_px = 2.0
        width, height = 1000.0, 10.0
        tol_x = tolerance_px / width
        tol_y = tolerance_px / height
        tolerance_fraction = {"top": tol_x, "bottom": tol_x, "left": tol_y, "right": tol_y}

        result = compare_junction_points(original_points, candidate_points, tolerance_fraction)
        self.assertFalse(result.ok)
        self.assertTrue(any(p.edge == "top" for p in result.missing))


if __name__ == "__main__":
    unittest.main()
