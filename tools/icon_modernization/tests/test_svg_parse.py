import unittest

from icon_modernization.geometry import Point
from icon_modernization.svg_parse import UnsupportedTransformError, extract_vertices


class TestExtractVerticesBasicShapes(unittest.TestCase):
    def test_line(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg"><line x1="0" y1="0" x2="10" y2="20"/></svg>'
        self.assertEqual(extract_vertices(svg), [Point(0, 0), Point(10, 20)])

    def test_rect(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg"><rect x="1" y="2" width="10" height="5"/></svg>'
        self.assertEqual(
            extract_vertices(svg),
            [Point(1, 2), Point(11, 2), Point(1, 7), Point(11, 7)],
        )

    def test_polyline(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg"><polyline points="0,0 5,5 10,0"/></svg>'
        self.assertEqual(extract_vertices(svg), [Point(0, 0), Point(5, 5), Point(10, 0)])

    def test_polygon_comma_and_space_separators(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg"><polygon points="0,0 5 5 10,0"/></svg>'
        self.assertEqual(extract_vertices(svg), [Point(0, 0), Point(5, 5), Point(10, 0)])

    def test_nested_group_translate_offset_applied(self):
        svg = (
            '<svg xmlns="http://www.w3.org/2000/svg">'
            '<g transform="translate(10,20)"><line x1="0" y1="0" x2="1" y2="1"/></g>'
            "</svg>"
        )
        self.assertEqual(extract_vertices(svg), [Point(10, 20), Point(11, 21)])

    def test_nested_group_translate_single_argument(self):
        svg = (
            '<svg xmlns="http://www.w3.org/2000/svg">'
            '<g transform="translate(5)"><line x1="0" y1="0" x2="0" y2="0"/></g>'
            "</svg>"
        )
        self.assertEqual(extract_vertices(svg), [Point(5, 0), Point(5, 0)])

    def test_unsupported_transform_raises(self):
        svg = (
            '<svg xmlns="http://www.w3.org/2000/svg">'
            '<g transform="scale(2)"><line x1="0" y1="0" x2="1" y2="1"/></g>'
            "</svg>"
        )
        with self.assertRaises(UnsupportedTransformError):
            extract_vertices(svg)

    def test_multiple_shapes_combined(self):
        svg = (
            '<svg xmlns="http://www.w3.org/2000/svg">'
            '<line x1="0" y1="0" x2="1" y2="1"/>'
            '<rect x="0" y="0" width="2" height="2"/>'
            "</svg>"
        )
        vertices = extract_vertices(svg)
        self.assertEqual(len(vertices), 6)


if __name__ == "__main__":
    unittest.main()
