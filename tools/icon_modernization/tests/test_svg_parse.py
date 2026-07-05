import unittest

from icon_modernization.geometry import Point
from icon_modernization.svg_parse import UnsupportedPathCommandError, UnsupportedTransformError, extract_vertices


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

    def test_circle_produces_four_cardinal_points(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg"><circle cx="10" cy="10" r="5"/></svg>'
        self.assertEqual(
            extract_vertices(svg),
            [Point(5, 10), Point(15, 10), Point(10, 5), Point(10, 15)],
        )

    def test_circle_inside_translated_group(self):
        svg = (
            '<svg xmlns="http://www.w3.org/2000/svg">'
            '<g transform="translate(100,200)"><circle cx="10" cy="10" r="5"/></g>'
            "</svg>"
        )
        self.assertEqual(
            extract_vertices(svg),
            [Point(105, 210), Point(115, 210), Point(110, 205), Point(110, 215)],
        )

    def test_ellipse_produces_four_cardinal_points_with_distinct_radii(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg"><ellipse cx="10" cy="20" rx="3" ry="7"/></svg>'
        self.assertEqual(
            extract_vertices(svg),
            [Point(7, 20), Point(13, 20), Point(10, 13), Point(10, 27)],
        )

    def test_multiple_shapes_combined(self):
        svg = (
            '<svg xmlns="http://www.w3.org/2000/svg">'
            '<line x1="0" y1="0" x2="1" y2="1"/>'
            '<rect x="0" y="0" width="2" height="2"/>'
            "</svg>"
        )
        vertices = extract_vertices(svg)
        self.assertEqual(len(vertices), 6)


class TestExtractVerticesPath(unittest.TestCase):
    def test_moveto_lineto_absolute(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg"><path d="M0,0 L10,0 L10,10"/></svg>'
        self.assertEqual(extract_vertices(svg), [Point(0, 0), Point(10, 0), Point(10, 10)])

    def test_moveto_lineto_relative(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg"><path d="m0,0 l10,0 l0,10"/></svg>'
        self.assertEqual(extract_vertices(svg), [Point(0, 0), Point(10, 0), Point(10, 10)])

    def test_implicit_repeated_lineto(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg"><path d="M0,0 L10,0 20,0 30,0"/></svg>'
        self.assertEqual(
            extract_vertices(svg),
            [Point(0, 0), Point(10, 0), Point(20, 0), Point(30, 0)],
        )

    def test_horizontal_and_vertical_lineto(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg"><path d="M0,0 H10 V10 h-5 v-5"/></svg>'
        self.assertEqual(
            extract_vertices(svg),
            [Point(0, 0), Point(10, 0), Point(10, 10), Point(5, 10), Point(5, 5)],
        )

    def test_cubic_curve_keeps_only_endpoint(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg"><path d="M0,0 C1,9 9,9 10,0"/></svg>'
        self.assertEqual(extract_vertices(svg), [Point(0, 0), Point(10, 0)])

    def test_quadratic_curve_keeps_only_endpoint(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg"><path d="M0,0 Q5,9 10,0"/></svg>'
        self.assertEqual(extract_vertices(svg), [Point(0, 0), Point(10, 0)])

    def test_close_path_is_a_no_op_for_vertices(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg"><path d="M0,0 L10,0 L10,10 Z"/></svg>'
        self.assertEqual(extract_vertices(svg), [Point(0, 0), Point(10, 0), Point(10, 10)])

    def test_path_inside_translated_group(self):
        svg = (
            '<svg xmlns="http://www.w3.org/2000/svg">'
            '<g transform="translate(100,200)"><path d="M0,0 L1,1"/></g>'
            "</svg>"
        )
        self.assertEqual(extract_vertices(svg), [Point(100, 200), Point(101, 201)])

    def test_unsupported_arc_command_raises(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg"><path d="M0,0 A5,5 0 0 1 10,10"/></svg>'
        with self.assertRaises(UnsupportedPathCommandError):
            extract_vertices(svg)

    def test_bare_token_after_close_path_raises(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg"><path d="M0,0 L10,0 Z 5,5"/></svg>'
        with self.assertRaises(UnsupportedPathCommandError):
            extract_vertices(svg)

    def test_path_command_missing_coordinate_raises(self):
        svg = '<svg xmlns="http://www.w3.org/2000/svg"><path d="M0,0 L10"/></svg>'
        with self.assertRaises(UnsupportedPathCommandError):
            extract_vertices(svg)


if __name__ == "__main__":
    unittest.main()
