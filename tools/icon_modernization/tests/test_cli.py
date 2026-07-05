import contextlib
import io
import os
import tempfile
import unittest

from icon_modernization.cli import run_check_junctions

_MATCHING_ORIGINAL = (
    '<svg xmlns="http://www.w3.org/2000/svg" width="100" height="10">'
    '<line x1="0" y1="5" x2="100" y2="5"/></svg>'
)
_MATCHING_CANDIDATE = (
    '<svg xmlns="http://www.w3.org/2000/svg" width="100" height="10">'
    '<line x1="0" y1="4" x2="100" y2="4"/></svg>'
)
_MISMATCHED_CANDIDATE = (
    '<svg xmlns="http://www.w3.org/2000/svg" width="100" height="10">'
    '<line x1="0" y1="9" x2="100" y2="9"/></svg>'
)


def _write_temp_svg(directory: str, name: str, markup: str) -> str:
    path = os.path.join(directory, name)
    with open(path, "w", encoding="utf-8") as f:
        f.write(markup)
    return path


class TestRunCheckJunctions(unittest.TestCase):
    def test_matching_geometry_within_tolerance_returns_zero(self):
        with tempfile.TemporaryDirectory() as tmp:
            original = _write_temp_svg(tmp, "original.svg", _MATCHING_ORIGINAL)
            candidate = _write_temp_svg(tmp, "candidate.svg", _MATCHING_CANDIDATE)
            buffer = io.StringIO()
            with contextlib.redirect_stdout(buffer):
                exit_code = run_check_junctions(original, candidate, tolerance_px=2.0)
            self.assertEqual(exit_code, 0)
            self.assertIn("OK", buffer.getvalue())

    def test_mismatched_geometry_beyond_tolerance_returns_one(self):
        with tempfile.TemporaryDirectory() as tmp:
            original = _write_temp_svg(tmp, "original.svg", _MATCHING_ORIGINAL)
            candidate = _write_temp_svg(tmp, "candidate.svg", _MISMATCHED_CANDIDATE)
            buffer = io.StringIO()
            with contextlib.redirect_stdout(buffer):
                exit_code = run_check_junctions(original, candidate, tolerance_px=2.0)
            self.assertEqual(exit_code, 1)
            self.assertIn("FAIL", buffer.getvalue())
            self.assertIn("MISSING", buffer.getvalue())


class TestRunCheckJunctionsErrorHandling(unittest.TestCase):
    def test_missing_original_file_returns_two(self):
        with tempfile.TemporaryDirectory() as tmp:
            candidate = _write_temp_svg(tmp, "candidate.svg", _MATCHING_CANDIDATE)
            nonexistent = os.path.join(tmp, "does_not_exist.svg")
            out, err = io.StringIO(), io.StringIO()
            with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
                exit_code = run_check_junctions(nonexistent, candidate, tolerance_px=2.0)
            self.assertEqual(exit_code, 2)
            self.assertIn(nonexistent, out.getvalue() + err.getvalue())

    def test_missing_candidate_file_returns_two(self):
        with tempfile.TemporaryDirectory() as tmp:
            original = _write_temp_svg(tmp, "original.svg", _MATCHING_ORIGINAL)
            nonexistent = os.path.join(tmp, "does_not_exist.svg")
            out, err = io.StringIO(), io.StringIO()
            with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
                exit_code = run_check_junctions(original, nonexistent, tolerance_px=2.0)
            self.assertEqual(exit_code, 2)
            self.assertIn(nonexistent, out.getvalue() + err.getvalue())

    def test_viewbox_only_svg_returns_two(self):
        viewbox_only = (
            '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 10">'
            '<line x1="0" y1="5" x2="100" y2="5"/></svg>'
        )
        with tempfile.TemporaryDirectory() as tmp:
            original = _write_temp_svg(tmp, "original.svg", _MATCHING_ORIGINAL)
            candidate = _write_temp_svg(tmp, "candidate.svg", viewbox_only)
            out, err = io.StringIO(), io.StringIO()
            with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
                exit_code = run_check_junctions(original, candidate, tolerance_px=2.0)
            self.assertEqual(exit_code, 2)
            message = out.getvalue() + err.getvalue()
            self.assertIn("width", message)
            self.assertIn("height", message)

    def test_unit_suffixed_dimensions_returns_two(self):
        px_suffixed = (
            '<svg xmlns="http://www.w3.org/2000/svg" width="100px" height="10px">'
            '<line x1="0" y1="5" x2="100" y2="5"/></svg>'
        )
        with tempfile.TemporaryDirectory() as tmp:
            original = _write_temp_svg(tmp, "original.svg", _MATCHING_ORIGINAL)
            candidate = _write_temp_svg(tmp, "candidate.svg", px_suffixed)
            out, err = io.StringIO(), io.StringIO()
            with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
                exit_code = run_check_junctions(original, candidate, tolerance_px=2.0)
            self.assertEqual(exit_code, 2)

    def test_malformed_xml_returns_two(self):
        malformed = '<svg xmlns="http://www.w3.org/2000/svg" width="100" height="10"><line x1="0"'
        with tempfile.TemporaryDirectory() as tmp:
            original = _write_temp_svg(tmp, "original.svg", _MATCHING_ORIGINAL)
            candidate = _write_temp_svg(tmp, "candidate.svg", malformed)
            out, err = io.StringIO(), io.StringIO()
            with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
                exit_code = run_check_junctions(original, candidate, tolerance_px=2.0)
            self.assertEqual(exit_code, 2)

    def test_path_terminating_mid_command_returns_two(self):
        truncated_path = (
            '<svg xmlns="http://www.w3.org/2000/svg" width="100" height="10">'
            '<path d="M0,0 L10"/></svg>'
        )
        with tempfile.TemporaryDirectory() as tmp:
            original = _write_temp_svg(tmp, "original.svg", _MATCHING_ORIGINAL)
            candidate = _write_temp_svg(tmp, "candidate.svg", truncated_path)
            out, err = io.StringIO(), io.StringIO()
            with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
                exit_code = run_check_junctions(original, candidate, tolerance_px=2.0)
            self.assertEqual(exit_code, 2)


if __name__ == "__main__":
    unittest.main()
