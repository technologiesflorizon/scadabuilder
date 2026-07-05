import unittest

from icon_modernization.geometry import Point, compute_bbox


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


if __name__ == "__main__":
    unittest.main()
